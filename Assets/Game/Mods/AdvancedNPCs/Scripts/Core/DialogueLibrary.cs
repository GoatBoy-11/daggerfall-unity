using System;
using System.Collections.Generic;

namespace AdvancedNPCs.Core
{
    /// <summary>The topics and greetings one ANPC uses: its types in order, then its folder's own file.</summary>
    public class ComposedDialogue
    {
        public readonly List<string> Types = new List<string>();
        public readonly List<DialogueTopic> Topics = new List<DialogueTopic>();
        public readonly List<DialogueAnswer> Greetings = new List<DialogueAnswer>();

        public bool IsEmpty
        {
            get { return Topics.Count == 0 && Greetings.Count == 0; }
        }
    }

    /// <summary>All dialogue types (ANPCs/_Dialogue/*.json) by id (spec C1 §3).</summary>
    public class DialogueLibrary
    {
        readonly Dictionary<string, DialogueFile> files = new Dictionary<string, DialogueFile>(StringComparer.Ordinal);

        /// <summary>Adds or replaces a type (by its Name).</summary>
        public void Add(DialogueFile file)
        {
            if (file != null && !string.IsNullOrEmpty(file.Name))
                files[file.Name] = file;
        }

        public bool Has(string id)
        {
            return id != null && files.ContainsKey(id);
        }

        public int Count
        {
            get { return files.Count; }
        }

        public IEnumerable<DialogueFile> Files
        {
            get { return files.Values; }
        }

        /// <summary>Types in listed order, then the folder's file; a topic with an earlier topic's id replaces it in place.</summary>
        public ComposedDialogue Compose(NpcDefinition d)
        {
            ComposedDialogue c = new ComposedDialogue();
            if (d == null)
                return c;
            foreach (string type in d.Dialogue)
            {
                DialogueFile file;
                if (files.TryGetValue(type, out file))
                {
                    c.Types.Add(type);
                    Merge(c, file);
                }
            }
            if (d.OwnDialogue != null)
                Merge(c, d.OwnDialogue);
            return c;
        }

        static void Merge(ComposedDialogue c, DialogueFile file)
        {
            foreach (DialogueTopic t in file.Topics)
            {
                int at = c.Topics.FindIndex(delegate (DialogueTopic existing) { return existing.Id == t.Id; });
                if (at >= 0)
                    c.Topics[at] = t;
                else
                    c.Topics.Add(t);
            }
            c.Greetings.AddRange(file.Greetings);
        }

        /// <summary>
        /// Problems only visible once files are combined: dialogue types that do not exist, and asked / notAsked
        /// naming no topic of the ANPC's composed set. One line each.
        /// </summary>
        public List<string> Check(IEnumerable<NpcDefinition> definitions)
        {
            List<string> messages = new List<string>();
            foreach (NpcDefinition d in definitions)
            {
                foreach (string type in d.Dialogue)
                {
                    string m = d.SourceFile + ": dialogue \"" + type + "\": no such file in _Dialogue, ignored";
                    if (!files.ContainsKey(type) && !messages.Contains(m))
                        messages.Add(m);
                }

                ComposedDialogue c = Compose(d);
                HashSet<string> ids = new HashSet<string>();
                Dictionary<string, DialogueTopic> captions = new Dictionary<string, DialogueTopic>(StringComparer.OrdinalIgnoreCase);
                foreach (DialogueTopic t in c.Topics)
                {
                    ids.Add(t.Id);
                    DialogueTopic same;
                    if (captions.TryGetValue(t.Caption, out same))
                    {
                        string m = t.Source + ": topic \"" + t.Caption + "\": " + d.Id + " already has a topic with this caption (id \"" + same.Id +
                                   "\" in " + same.Source + "); give them different captions, or the same id to replace it";
                        if (!messages.Contains(m))
                            messages.Add(m);
                    }
                    else
                    {
                        captions[t.Caption] = t;
                    }
                }
                foreach (DialogueTopic t in c.Topics)
                {
                    List<string> named = new List<string>();
                    if (t.When != null)
                        named.AddRange(t.When.AskedIds);
                    foreach (DialogueAnswer a in t.Answers)
                    {
                        if (a.When != null)
                            named.AddRange(a.When.AskedIds);
                    }
                    foreach (string id in named)
                    {
                        string m = t.Source + ": topic \"" + t.Caption + "\": asked/notAsked: no topic \"" + id + "\" for " + d.Id + " (check its caption or id)";
                        if (!ids.Contains(id) && !messages.Contains(m))
                            messages.Add(m);
                    }
                }
            }
            return messages;
        }
    }
}

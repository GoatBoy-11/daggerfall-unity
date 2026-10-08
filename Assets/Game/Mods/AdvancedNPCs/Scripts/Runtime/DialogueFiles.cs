using System;
using System.Collections.Generic;
using System.IO;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>Reads dialogue types (ANPCs/_Dialogue/*.json) and folders' dialogue.json, and reports problems (spec C1 §3).</summary>
    public static class DialogueFiles
    {
        public const string FolderName = "_Dialogue";

        /// <summary>Reads every _Dialogue/*.json. Problems are logged and added to problems (when given).</summary>
        public static DialogueLibrary Load(string root, List<string> problems = null)
        {
            DialogueLibrary library = new DialogueLibrary();
            Dictionary<string, string> sources = new Dictionary<string, string>(StringComparer.Ordinal);
            string folder = Path.Combine(root, FolderName);
            if (!Directory.Exists(folder))
                return library;
            string[] paths = AnpcFiles.FilesWithExtension(folder, ".json");
            Array.Sort(paths, StringComparer.OrdinalIgnoreCase);
            foreach (string path in paths)
            {
                string fileName = Path.GetFileName(path);
                string source = FolderName + "/" + fileName;
                string id = DialogueIds.Normalize(NameListParser.Normalize(fileName));
                if (id.Length == 0)
                {
                    Report(source + ": file: the file name needs letters or digits, ignored", problems);
                    continue;
                }
                string other;
                if (sources.TryGetValue(id, out other))
                {
                    Report(source + ": file: same dialogue type name \"" + id + "\" as " + other + ", ignored (rename one of them)", problems);
                    continue;
                }
                try
                {
                    DialogueParseResult r = DialogueParser.Parse(source, File.ReadAllText(path));
                    foreach (string m in r.Messages)
                        Report(m, problems);
                    if (r.File == null)
                        continue;
                    r.File.Name = id;
                    sources[id] = source;
                    library.Add(r.File);
                }
                catch (Exception e)
                {
                    Report(source + ": file: could not read (" + e.Message + ")", problems);
                }
            }
            AdvancedNpcsMod.Log("Loaded " + library.Count + " dialogue type(s).");
            return library;
        }

        static void Report(string message, List<string> problems)
        {
            AdvancedNpcsMod.Log(message);
            if (problems != null)
                problems.Add(message);
        }

        /// <summary>Re-reads every folder's dialogue.json into the catalog's definitions (anpc_reload_dialogue).</summary>
        public static void ReloadOwn(DefinitionCatalog catalog, List<string> problems)
        {
            Dictionary<string, NpcDefinition> byFolder = new Dictionary<string, NpcDefinition>(StringComparer.Ordinal);
            foreach (NpcDefinition d in All(catalog))
                byFolder[d.Folder] = d;
            List<string> messages = new List<string>();
            foreach (AnpcFolder folder in AnpcFiles.ReadFolders())
            {
                NpcDefinition d;
                if (byFolder.TryGetValue(folder.Name, out d))
                    d.OwnDialogue = DefinitionCatalog.ParseOwnDialogue(folder.Name, folder.DialogueJson, messages);
            }
            foreach (string m in messages)
                Report(m, problems);
        }

        /// <summary>Logs problems found once files are combined, and quest-global names DFU does not know.</summary>
        public static int Check(DialogueLibrary library, DefinitionCatalog catalog, List<string> problems = null)
        {
            List<NpcDefinition> all = All(catalog);
            List<string> messages = library.Check(all);
            List<string> unknownGlobals = new List<string>();
            // HasInstance: the Instance getter would create a QuestMachine if none exists yet.
            if (DaggerfallWorkshop.Game.Questing.QuestMachine.HasInstance && DaggerfallWorkshop.Game.Questing.QuestMachine.Instance.GlobalVarsTable != null)
            {
                List<DialogueFile> files = new List<DialogueFile>(library.Files);
                foreach (NpcDefinition d in all)
                {
                    if (d.OwnDialogue != null)
                        files.Add(d.OwnDialogue);
                }
                foreach (DialogueFile f in files)
                {
                    foreach (Condition c in Conditions(f))
                    {
                        foreach (string name in c.QuestGlobals)
                        {
                            if (GameFacts.GlobalVarId(name) < 0 && !unknownGlobals.Contains(name))
                            {
                                unknownGlobals.Add(name);
                                messages.Add("dialogue: questGlobal \"" + name + "\": not in DFU's Quests-GlobalVars table (use a name from it or a number 0-63); it never holds");
                            }
                        }
                    }
                }
            }
            foreach (string m in messages)
                Report(m, problems);
            return messages.Count;
        }

        static IEnumerable<Condition> Conditions(DialogueFile f)
        {
            foreach (DialogueTopic t in f.Topics)
            {
                if (t.When != null)
                    yield return t.When;
                foreach (DialogueAnswer a in t.Answers)
                {
                    if (a.When != null)
                        yield return a.When;
                }
            }
            foreach (DialogueAnswer g in f.Greetings)
            {
                if (g.When != null)
                    yield return g.When;
            }
        }

        static List<NpcDefinition> All(DefinitionCatalog catalog)
        {
            List<NpcDefinition> all = new List<NpcDefinition>(catalog.ById.Values);
            all.AddRange(catalog.Generics);
            return all;
        }
    }
}

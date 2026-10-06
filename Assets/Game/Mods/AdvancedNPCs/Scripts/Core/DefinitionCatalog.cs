using System;
using System.Collections.Generic;

namespace AdvancedNPCs.Core
{
    /// <summary>The files of one ANPC folder as read from disk; a null text means the file is missing.</summary>
    public class AnpcFolder
    {
        public readonly string Name;
        public readonly string NpcJson;
        public readonly string DialogueJson;

        public AnpcFolder(string name, string npcJson, string dialogueJson)
        {
            Name = name;
            NpcJson = npcJson;
            DialogueJson = dialogueJson;
        }
    }

    /// <summary>All loaded ANPC definitions plus the messages produced while loading them (spec §6, §14).</summary>
    public class DefinitionCatalog
    {
        /// <summary>Unique ANPCs by id.</summary>
        public readonly Dictionary<string, NpcDefinition> ById = new Dictionary<string, NpcDefinition>(StringComparer.Ordinal);
        /// <summary>Generic templates, in folder order.</summary>
        public readonly List<NpcDefinition> Generics = new List<NpcDefinition>();
        public readonly List<string> Messages = new List<string>();

        public int Count
        {
            get { return ById.Count + Generics.Count; }
        }

        public static DefinitionCatalog Build(IEnumerable<AnpcFolder> folders)
        {
            DefinitionCatalog catalog = new DefinitionCatalog();
            List<AnpcFolder> ordered = new List<AnpcFolder>(folders);
            ordered.Sort(delegate (AnpcFolder a, AnpcFolder b) { return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase); });

            foreach (AnpcFolder folder in ordered)
            {
                if (folder.Name != null && folder.Name.StartsWith("_", StringComparison.Ordinal))
                    continue; // shared data such as _Portraits
                if (folder.NpcJson == null)
                {
                    catalog.Messages.Add(folder.Name + "/npc.json: file: missing, folder skipped");
                    continue;
                }

                ParseResult result = DefinitionParser.ParseFolder(folder.Name, folder.NpcJson);
                catalog.Messages.AddRange(result.Warnings);
                if (!result.Ok)
                {
                    catalog.Messages.Add(result.Error);
                    continue;
                }

                NpcDefinition d = result.Definition;
                if (d.Kind == NpcKind.Generic)
                {
                    if (folder.DialogueJson != null)
                        catalog.Messages.Add(folder.Name + "/dialogue.json: file: topics are for unique ANPCs only, ignored");
                    catalog.Generics.Add(d);
                }
                else if (catalog.ById.ContainsKey(d.Id))
                {
                    catalog.Messages.Add(d.SourceFile + ": folder: duplicate id \"" + d.Id + "\", skipped");
                }
                else
                {
                    catalog.ById.Add(d.Id, d);
                }
            }

            catalog.Messages.Add("Loaded " + catalog.ById.Count + " unique ANPC(s) and " + catalog.Generics.Count + " generic template(s).");
            return catalog;
        }

        /// <summary>Unique ANPCs that live in this town.</summary>
        public List<NpcDefinition> ForLocation(string region, string place)
        {
            List<NpcDefinition> found = new List<NpcDefinition>();
            if (region == null || place == null)
                return found;
            foreach (NpcDefinition d in ById.Values)
            {
                if (SamePlaceName(d.Region, region) && SamePlaceName(d.Place, place))
                    found.Add(d);
            }
            return found;
        }

        /// <summary>Every portrait name any definition refers to, distinct and sorted.</summary>
        public List<string> ReferencedPortraits()
        {
            List<string> names = new List<string>();
            List<NpcDefinition> all = new List<NpcDefinition>(ById.Values);
            all.AddRange(Generics);
            foreach (NpcDefinition d in all)
            {
                foreach (string p in d.Portraits)
                {
                    if (!names.Contains(p))
                        names.Add(p);
                }
            }
            names.Sort(StringComparer.Ordinal);
            return names;
        }

        /// <summary>Region and place names match ignoring case and surrounding spaces.</summary>
        public static bool SamePlaceName(string a, string b)
        {
            return a != null && b != null && string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }
}

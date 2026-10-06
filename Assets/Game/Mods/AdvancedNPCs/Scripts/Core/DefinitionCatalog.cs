using System;
using System.Collections.Generic;
using System.Linq;

namespace AdvancedNPCs.Core
{
    /// <summary>All loaded NPC definitions, keyed by id, plus the messages produced while loading them.</summary>
    public class DefinitionCatalog
    {
        public readonly Dictionary<string, NpcDefinition> ById = new Dictionary<string, NpcDefinition>(StringComparer.Ordinal);
        public readonly List<string> Messages = new List<string>();

        public int Count
        {
            get { return ById.Count; }
        }

        public static DefinitionCatalog Build(IEnumerable<KeyValuePair<string, string>> files)
        {
            DefinitionCatalog catalog = new DefinitionCatalog();
            IEnumerable<KeyValuePair<string, string>> ordered =
                files.OrderBy(f => f.Key, StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, string> file in ordered)
            {
                ParseResult result = DefinitionParser.Parse(file.Key, file.Value);
                if (!result.Ok)
                {
                    catalog.Messages.Add(result.Error);
                    continue;
                }

                NpcDefinition existing;
                if (catalog.ById.TryGetValue(result.Definition.Id, out existing))
                {
                    catalog.Messages.Add(file.Key + ": id: duplicate \"" + result.Definition.Id +
                                         "\" (already defined in " + existing.SourceFile + "), skipped");
                    continue;
                }

                catalog.ById.Add(result.Definition.Id, result.Definition);
            }

            catalog.Messages.Add("Loaded " + catalog.Count + " NPC definition(s).");
            return catalog;
        }

        public List<NpcDefinition> ForLocation(string region, string place)
        {
            List<NpcDefinition> found = new List<NpcDefinition>();
            if (region == null || place == null)
                return found;
            foreach (NpcDefinition d in ById.Values)
            {
                if (Same(d.Region, region) && Same(d.Place, place))
                    found.Add(d);
            }
            return found;
        }

        static bool Same(string a, string b)
        {
            return string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }
}

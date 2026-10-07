using System;
using System.Text.RegularExpressions;

namespace AdvancedNPCs.Core
{
    public class MigrationResult
    {
        public string Id;
        public string NpcJson;
        public string Error;

        public bool Ok
        {
            get { return Error == null; }
        }
    }

    /// <summary>
    /// Converts a 1a definition file into the text of ANPCs/&lt;id&gt;/npc.json: the same text with the "id"
    /// member replaced by "kind": "unique", so the author's other fields, order and formatting survive (spec §11).
    /// </summary>
    public static class Migration
    {
        // Ids only contain [a-z0-9_], so the value has no escapes.
        static readonly Regex IdMember = new Regex("\"id\"\\s*:\\s*\"[^\"]*\"");

        public static MigrationResult Convert(string fileName, string json)
        {
            MigrationResult m = new MigrationResult();
            ParseResult old = DefinitionParser.Parse(fileName, json);
            if (!old.Ok)
            {
                m.Error = old.Error;
                return m;
            }

            string body = ReplaceIdWithKind(json);
            ParseResult check = DefinitionParser.ParseFolder(old.Definition.Id, body);
            if (!check.Ok)
            {
                m.Error = fileName + ": could not convert (" + check.Error + ")";
                return m;
            }
            m.Id = old.Definition.Id;
            m.NpcJson = body;
            return m;
        }

        /// <summary>Puts "kind": "unique" where the "id" member was; commas and line endings stay as they were.</summary>
        public static string ReplaceIdWithKind(string json)
        {
            Match match = IdMember.Match(json);
            if (!match.Success)
                return json;
            return json.Substring(0, match.Index) + "\"kind\": \"unique\"" + json.Substring(match.Index + match.Length);
        }
    }
}

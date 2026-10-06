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
    /// Converts a 1a definition file into the text of ANPCs/&lt;id&gt;/npc.json: the same text without the "id"
    /// member, so the author's other fields, order and formatting survive (spec §11).
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

            string body = RemoveId(json);
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

        public static string RemoveId(string json)
        {
            Match match = IdMember.Match(json);
            if (!match.Success)
                return json;

            int start = match.Index;
            int end = match.Index + match.Length;
            int after = end;
            while (after < json.Length && (json[after] == ' ' || json[after] == '\t'))
                after++;
            if (after < json.Length && json[after] == ',')
            {
                // "id": "x", other... -> remove the member, its comma and the spaces after it.
                end = after + 1;
                while (end < json.Length && (json[end] == ' ' || json[end] == '\t'))
                    end++;
            }
            else
            {
                // Last member: remove the comma that precedes it instead.
                int before = start - 1;
                while (before >= 0 && char.IsWhiteSpace(json[before]))
                    before--;
                if (before >= 0 && json[before] == ',')
                    start = before;
            }

            string result = json.Substring(0, start) + json.Substring(end);

            // If the member had its own line, that line is now blank: drop it (keeps \r\n or \n endings intact).
            int lineEnd = result.IndexOf('\n', start);
            if (lineEnd >= 0)
            {
                int lineStart = start > 0 ? result.LastIndexOf('\n', start - 1) + 1 : 0;
                if (result.Substring(lineStart, lineEnd - lineStart).Trim().Length == 0)
                    result = result.Remove(lineStart, lineEnd - lineStart + 1);
            }
            return result;
        }
    }
}

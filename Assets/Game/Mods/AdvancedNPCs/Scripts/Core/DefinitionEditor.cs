using System.Text.RegularExpressions;

namespace AdvancedNPCs.Core
{
    /// <summary>
    /// Rewrites only the "location" and "position" of a definition file's text, leaving the author's other
    /// fields, order and formatting alone (used by the anpc_place console command).
    /// </summary>
    public static class DefinitionEditor
    {
        static readonly Regex LocationPattern = new Regex("\"location\"\\s*:\\s*\\{[^{}]*\\}");
        static readonly Regex PositionPattern = new Regex("\"position\"\\s*:\\s*\\[[^\\[\\]]*\\]");

        public static string SetPlacement(string json, string region, string place, float x, float y, float z)
        {
            json = ReplaceOrAdd(json, LocationPattern, PositionFormat.LocationJson(region, place));
            json = ReplaceOrAdd(json, PositionPattern, PositionFormat.PositionJson(x, y, z));
            return json;
        }

        static string ReplaceOrAdd(string json, Regex pattern, string replacement)
        {
            if (pattern.IsMatch(json))
                return pattern.Replace(json, delegate (Match m) { return replacement; }, 1);

            // Add as the last field of the top-level object.
            int close = json.LastIndexOf('}');
            if (close < 0)
                return json;
            string before = json.Substring(0, close).TrimEnd();
            bool needsComma = before.Length > 0 && before[before.Length - 1] != '{' && before[before.Length - 1] != ',';
            return before + (needsComma ? "," : "") + "\n  " + replacement + "\n" + json.Substring(close);
        }
    }
}

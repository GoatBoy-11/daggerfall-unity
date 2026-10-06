using System.Globalization;

namespace AdvancedNPCs.Core
{
    /// <summary>Builds the JSON snippet printed by the anpc_pos console command.</summary>
    public static class PositionFormat
    {
        public static string ToJsonSnippet(string region, string place, float x, float y, float z)
        {
            return "\"location\": { \"region\": \"" + Escape(region) + "\", \"place\": \"" + Escape(place) + "\" },\n" +
                   "\"position\": [" + Num(x) + ", " + Num(y) + ", " + Num(z) + "]";
        }

        static string Num(float f)
        {
            return f.ToString("0.##", CultureInfo.InvariantCulture);
        }

        static string Escape(string s)
        {
            if (s == null)
                return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}

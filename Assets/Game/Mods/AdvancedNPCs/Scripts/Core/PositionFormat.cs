using System.Globalization;

namespace AdvancedNPCs.Core
{
    /// <summary>Builds the JSON printed by anpc_pos and written by anpc_place. Always uses '.' decimals.</summary>
    public static class PositionFormat
    {
        public static string ToJsonSnippet(string region, string place, float x, float y, float z)
        {
            return LocationJson(region, place) + ",\n" + PositionJson(x, y, z);
        }

        public static string LocationJson(string region, string place)
        {
            return "\"location\": { \"region\": \"" + Escape(region) + "\", \"place\": \"" + Escape(place) + "\" }";
        }

        public static string PositionJson(float x, float y, float z)
        {
            return "\"position\": [" + Num(x) + ", " + Num(y) + ", " + Num(z) + "]";
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

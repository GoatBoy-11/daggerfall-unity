using System.Globalization;
using System.Collections.Generic;
using System.Text;

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

        public static string SpotJson(string region,string place,string context,float x,float y,float z)
        {
            return "{\n  \"relativeTo\": \"area\",\n  \"location\": { \"region\": \""+Escape(region)+"\", \"place\": \""+Escape(place)+"\", \"context\": \""+Escape(context)+"\" },\n  "+PositionJson(x,y,z)+"\n}";
        }

        public static string PositionJson(float x, float y, float z)
        {
            return "\"position\": [" + Num(x) + ", " + Num(y) + ", " + Num(z) + "]";
        }

        public static string PatrolJson(IList<PackagePoint> points)
        {
            StringBuilder text = new StringBuilder("{\n  \"type\": \"patrol\",\n  \"parameters\": {\n    \"points\": [");
            for (int i = 0; i < points.Count; i++)
            {
                if (i > 0) text.Append(',');
                PackagePoint point = points[i];
                text.Append("\n      [").Append(FieldReader.Num(point.X)).Append(", ").Append(FieldReader.Num(point.Y)).Append(", ").Append(FieldReader.Num(point.Z)).Append(']');
            }
            return text.Append("\n    ]\n  }\n}").ToString();
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

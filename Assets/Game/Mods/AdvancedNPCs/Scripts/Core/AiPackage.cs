using System;
using System.Collections.Generic;

namespace AdvancedNPCs.Core
{
    public class PackagePoint
    {
        public double X, Y, Z;
        public PackagePoint(double x, double y, double z) { X = x; Y = y; Z = z; }
    }

    public class AiPackage
    {
        public string Id;
        public string Type;
        public Dictionary<string, object> Parameters;
    }

    /// <summary>A validated, per-schedule copy. Never edits shared JSON defaults.</summary>
    public class PackageSettings
    {
        public string Type;
        public PackagePoint Center = new PackagePoint(0, 0, 0);
        public PackagePoint Target;
        public List<PackagePoint> Points = new List<PackagePoint>();
        public double Radius = 8;
        public double Pause = 3;
        public bool Run;
    }

    public static class PackageParser
    {
        public static readonly string[] Keys = { "type", "parameters" };
        public static readonly string[] StandKeys = { };
        public static readonly string[] WanderKeys = { "center", "radius", "pause", "run" };
        public static readonly string[] TravelKeys = { "target", "run" };
        public static readonly string[] PatrolKeys = { "points", "pauseAtStops", "run" };

        public static AiPackage Parse(string id, string json, out string error)
        {
            error = null;
            if (!DefinitionParser.IsValidFolderName(id)) { error = "invalid package filename"; return null; }
            Dictionary<string, object> root;
            try
            {
                List<string> duplicates = new List<string>();
                root = Json.Parse(json, duplicates) as Dictionary<string, object>;
                if (duplicates.Count > 0) { error = "duplicate field " + duplicates[0]; return null; }
            }
            catch (JsonException e) { error = e.Message; return null; }
            if (root == null) { error = "must be an object"; return null; }
            List<string> unknown = FieldReader.UnknownKeys(root, Keys);
            if (unknown.Count > 0) { error = "unknown field " + unknown[0]; return null; }
            AiPackage package = new AiPackage();
            package.Id = id;
            if (FieldReader.Text(root, "type", null, out package.Type) != null || string.IsNullOrEmpty(package.Type))
            { error = "type is required"; return null; }
            if (root.ContainsKey("parameters"))
            {
                package.Parameters = root["parameters"] as Dictionary<string, object>;
                if (package.Parameters == null) { error = "parameters must be an object"; return null; }
            }
            else package.Parameters = new Dictionary<string, object>();
            if (Resolve(package, null, out error) == null) return null;
            return package;
        }

        public static PackageSettings Resolve(AiPackage package, Dictionary<string, object> overrides, out string error)
        {
            error = null;
            if (package == null) { error = "package is missing or invalid"; return null; }
            Dictionary<string, object> p = new Dictionary<string, object>(package.Parameters);
            if (overrides != null)
                foreach (KeyValuePair<string, object> pair in overrides) p[pair.Key] = pair.Value;
            PackageSettings s = new PackageSettings();
            s.Type = package.Type;
            string[] allowed;
            switch (s.Type)
            {
                case "stand": allowed = StandKeys; break;
                case "wander": allowed = WanderKeys; break;
                case "travel_to": allowed = TravelKeys; break;
                case "patrol": allowed = PatrolKeys; break;
                default: error = "unknown type " + s.Type; return null;
            }
            List<string> unknown = FieldReader.UnknownKeys(p, allowed);
            if (unknown.Count > 0) { error = "parameters: unknown field " + unknown[0] + " for " + s.Type; return null; }
            if (p.ContainsKey("run") && (!(p["run"] is bool))) { error = "run must be true or false"; return null; }
            if (p.ContainsKey("run")) s.Run = (bool)p["run"];
            if (s.Type == "wander")
            {
                if (p.ContainsKey("center") && (s.Center = Point(p["center"])) == null)
                { error = "center must be [x,y,z] metre offsets, each -100 to 100"; return null; }
                if (!Number(p, "radius", 8, 0, 50, out s.Radius) || !Number(p, "pause", 3, 0, 3600, out s.Pause))
                { error = "radius must be 0-50 metres and pause 0-3600 game seconds"; return null; }
            }
            if (s.Type == "travel_to")
            {
                object target;
                if (!p.TryGetValue("target", out target) || (s.Target = Point(target)) == null)
                { error = "target is required: [x,y,z] offsets, each -100 to 100"; return null; }
            }
            if (s.Type == "patrol")
            {
                object points;
                p.TryGetValue("points", out points);
                List<object> list = points as List<object>;
                if (list == null || list.Count < 2 || list.Count > 32)
                { error = "points must contain 2-32 [x,y,z] offsets"; return null; }
                foreach (object raw in list)
                {
                    PackagePoint point = Point(raw);
                    if (point == null) { error = "points must be [x,y,z] offsets, each -100 to 100"; return null; }
                    s.Points.Add(point);
                }
                if (!Number(p, "pauseAtStops", 10, 0, 3600, out s.Pause))
                { error = "pauseAtStops must be 0-3600 game seconds"; return null; }
            }
            return s;
        }

        static bool Number(Dictionary<string, object> p, string name, double fallback, double min, double max, out double value)
        {
            value = fallback;
            object raw;
            if (!p.TryGetValue(name, out raw)) return true;
            if (!(raw is double)) return false;
            value = (double)raw;
            return !double.IsNaN(value) && !double.IsInfinity(value) && value >= min && value <= max;
        }

        static PackagePoint Point(object raw)
        {
            List<object> xyz = raw as List<object>;
            if (xyz == null || xyz.Count != 3) return null;
            double[] v = new double[3];
            for (int i = 0; i < 3; i++)
            {
                if (!(xyz[i] is double)) return null;
                v[i] = (double)xyz[i];
                if (double.IsNaN(v[i]) || double.IsInfinity(v[i]) || Math.Abs(v[i]) > 100) return null;
            }
            return new PackagePoint(v[0], v[1], v[2]);
        }
    }
}

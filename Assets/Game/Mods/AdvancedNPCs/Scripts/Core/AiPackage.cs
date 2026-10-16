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
        public string Animation;
        public bool OnlyInterruptAtCombat;
        public string FollowTarget = "player";
        public double FollowDistance = 2, RunDistance = 6;
        public PackagePoint Position;
        public List<string> Spots = new List<string>();
        public double? Facing;
        public bool Loop;
        public List<PackageStep> Steps = new List<PackageStep>();
        public double IdleIntervalMin = 6, IdleIntervalMax = 15;
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
        public static readonly string[] WorkKeys = { "position", "spot", "animation", "facing", "run" };
        public static readonly string[] SequenceKeys = { "steps", "loop" };
        public static readonly string[] StepKeys = { "package", "parameters", "duration" };
        public static readonly string[] StandKeys = { };
        public static readonly string[] FollowKeys = { "target", "distance", "runDistance" };
        public static readonly string[] GuardKeys = { "position", "idleInterval", "spot" };
        public static readonly string[] AnimationKeys = { "animation", "only_interrupt_at_combat" };
        public static readonly string[] WanderKeys = { "center", "radius", "pause", "run" };
        public static readonly string[] CivilianKeys = { "center", "radius" };
        public static readonly string[] TravelKeys = { "target", "run", "spot" };
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
                case "work_at": allowed = WorkKeys; break;
                case "sequence": allowed = SequenceKeys; break;
                case "stand": allowed = StandKeys; break;
                case "follow": allowed = FollowKeys; break;
                case "guard_spot": allowed = GuardKeys; break;
                case "play_animation": allowed = AnimationKeys; break;
                case "wander": allowed = WanderKeys; break;
                case "wander_civilian": allowed = CivilianKeys; break;
                case "travel_to": allowed = TravelKeys; break;
                case "patrol": allowed = PatrolKeys; break;
                default: error = "unknown type " + s.Type; return null;
            }
            List<string> unknown = FieldReader.UnknownKeys(p, allowed);
            if (unknown.Count > 0) { error = "parameters: unknown field " + unknown[0] + " for " + s.Type; return null; }
            if (p.ContainsKey("run") && (!(p["run"] is bool))) { error = "run must be true or false"; return null; }
            if (p.ContainsKey("run")) s.Run = (bool)p["run"];
            if (s.Type == "play_animation" || s.Type == "work_at")
            {
                object flag;
                if (p.TryGetValue("only_interrupt_at_combat", out flag))
                {
                    if (!(flag is bool)) { error = "only_interrupt_at_combat must be true or false"; return null; }
                    s.OnlyInterruptAtCombat = (bool)flag;
                }
                if ((p.ContainsKey("animation") && p["animation"] == null) ||
                    FieldReader.Text(p, "animation", "idle", out s.Animation) != null || !PackageAnimations.ValidName(s.Animation))
                { error = "animation must be a sheet or family name without .png (letters, digits and underscores)"; return null; }
            }
            if (s.Type == "follow")
            {
                if ((p.ContainsKey("target") && p["target"] == null) || FieldReader.Text(p, "target", "player", out s.FollowTarget) != null ||
                    string.IsNullOrEmpty(s.FollowTarget) || s.FollowTarget.Length > 200 || s.FollowTarget.Trim() != s.FollowTarget)
                { error = "target must be player or an exact spawned ANPC id/key (max 200 characters)"; return null; }
                if (!Number(p, "distance", 2, 0.5, 20, out s.FollowDistance) || !Number(p, "runDistance", 6, 1, 100, out s.RunDistance) ||
                    s.RunDistance < s.FollowDistance + 0.5)
                { error = "distance must be 0.5-20 metres; runDistance 1-100 and at least distance + 0.5"; return null; }
            }
            if (s.Type == "work_at" || s.Type == "guard_spot")
            {
                if (p.ContainsKey("position") && (s.Position = Point(p["position"], 1000000)) == null)
                { error = "position must be [x,y,z] local-area coordinates, each -1000000 to 1000000"; return null; }
            }
            if (p.ContainsKey("spot"))
            {
                List<string> names = p["spot"] is string ? new List<string> {(string)p["spot"]} : null;
                string nameError = names != null ? null : FieldReader.Texts(p, "spot", out names);
                if (nameError != null || names == null || names.Count == 0 || names.Count > 16)
                { error = "spot must be a name or list of 1-16 names/tags"; return null; }
                foreach (string name in names)
                {
                    if (!DefinitionParser.IsValidFolderName(name)) { error = "spot names/tags must be lowercase identifiers"; return null; }
                    if (!s.Spots.Contains(name)) s.Spots.Add(name);
                }
                if (p.ContainsKey("position") || p.ContainsKey("target")) { error = "spot cannot be combined with position or target"; return null; }
            }
            if (s.Type == "work_at" && p.ContainsKey("facing"))
            {
                double facing;
                if (!Number(p,"facing",0,0,360,out facing)) { error = "facing must be 0-360 degrees in the area's axes"; return null; }
                s.Facing = facing;
            }
            if (s.Type == "sequence")
            {
                object raw;
                if (p.TryGetValue("loop",out raw))
                {
                    if (!(raw is bool)) { error = "loop must be true or false"; return null; }
                    s.Loop = (bool)raw;
                }
                p.TryGetValue("steps",out raw);
                var steps = raw as List<object>;
                if (steps == null || steps.Count == 0 || steps.Count > 32) { error = "steps must contain 1-32 package references"; return null; }
                foreach (object item in steps)
                {
                    var row = item as Dictionary<string,object>;
                    if (row == null || FieldReader.UnknownKeys(row,StepKeys).Count > 0) { error = "invalid sequence step fields"; return null; }
                    var step = new PackageStep();
                    if (FieldReader.Text(row,"package",null,out step.Package) != null || !DefinitionParser.IsValidFolderName(step.Package))
                    { error = "step package must be a package filename without .json"; return null; }
                    if (row.TryGetValue("parameters",out raw))
                    {
                        step.Parameters = raw as Dictionary<string,object>;
                        if (step.Parameters == null) { error = "step parameters must be an object"; return null; }
                    }
                    if (row.ContainsKey("duration") && !Number(row,"duration",-1,0.1,86400,out step.Duration))
                    { error = "step duration must be 0.1-86400 active gameplay seconds"; return null; }
                    s.Steps.Add(step);
                }
            }
            if (s.Type == "guard_spot")
            {
                object raw;
                if (p.TryGetValue("idleInterval", out raw))
                {
                    List<object> interval = raw as List<object>;
                    if (interval == null || interval.Count != 2 || !(interval[0] is double) || !(interval[1] is double))
                    { error = "idleInterval must be [min,max] seconds"; return null; }
                    s.IdleIntervalMin = (double)interval[0];
                    s.IdleIntervalMax = (double)interval[1];
                    if (double.IsNaN(s.IdleIntervalMin) || double.IsNaN(s.IdleIntervalMax) ||
                        s.IdleIntervalMin < 0.1 || s.IdleIntervalMax > 3600 || s.IdleIntervalMin > s.IdleIntervalMax)
                    { error = "idleInterval must be ordered [min,max], each 0.1-3600 seconds"; return null; }
                }
            }
            if (s.Type == "wander" || s.Type == "wander_civilian")
            {
                if (p.ContainsKey("center") && (s.Center = Point(p["center"])) == null)
                { error = "center must be [x,y,z] metre offsets, each -100 to 100"; return null; }
                bool civilian = s.Type == "wander_civilian";
                if (!Number(p, "radius", civilian ? 40 : 8, 0, 50, out s.Radius) || !Number(p, "pause", civilian ? 0 : 3, 0, 3600, out s.Pause))
                { error = "radius must be 0-50 metres and pause 0-3600 game seconds"; return null; }
            }
            if (s.Type == "travel_to")
            {
                object target;
                if (s.Spots.Count == 0 && (!p.TryGetValue("target", out target) || (s.Target = Point(target)) == null))
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

        internal static bool Number(Dictionary<string, object> p, string name, double fallback, double min, double max, out double value)
        {
            value = fallback;
            object raw;
            if (!p.TryGetValue(name, out raw)) return true;
            if (!(raw is double)) return false;
            value = (double)raw;
            return !double.IsNaN(value) && !double.IsInfinity(value) && value >= min && value <= max;
        }

        internal static PackagePoint Point(object raw, double limit = 100)
        {
            List<object> xyz = raw as List<object>;
            if (xyz == null || xyz.Count != 3) return null;
            double[] v = new double[3];
            for (int i = 0; i < 3; i++)
            {
                if (!(xyz[i] is double)) return null;
                v[i] = (double)xyz[i];
                if (double.IsNaN(v[i]) || double.IsInfinity(v[i]) || Math.Abs(v[i]) > limit) return null;
            }
            return new PackagePoint(v[0], v[1], v[2]);
        }
    }
}

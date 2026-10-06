using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace AdvancedNPCs.Core
{
    public class ParseResult
    {
        public NpcDefinition Definition;
        public string Error;

        public bool Ok
        {
            get { return Definition != null; }
        }
    }

    /// <summary>Turns one definition file into a validated NpcDefinition or a single error line.</summary>
    public static class DefinitionParser
    {
        static readonly Regex IdPattern = new Regex("^[a-z0-9_]+$");

        public static ParseResult Parse(string fileName, string json)
        {
            if (string.IsNullOrEmpty(json) || json.Trim().Length == 0)
                return Fail(fileName, "file", "empty");

            object root;
            try
            {
                root = Json.Parse(json);
            }
            catch (JsonException e)
            {
                return Fail(fileName, "file", "invalid JSON (" + e.Message + ")");
            }
            Dictionary<string, object> o = root as Dictionary<string, object>;
            if (o == null)
                return Fail(fileName, "file", "invalid JSON (top level must be an object)");

            string problem;

            string id;
            if ((problem = Text(o, "id", null, out id)) != null)
                return Fail(fileName, "id", problem);
            if (string.IsNullOrEmpty(id))
                return Fail(fileName, "id", "required");
            if (!IdPattern.IsMatch(id))
                return Fail(fileName, "id", "use only lowercase letters, digits and underscore (got \"" + id + "\")");

            string name;
            if ((problem = Text(o, "name", null, out name)) != null)
                return Fail(fileName, "name", problem);
            if (string.IsNullOrEmpty(name))
                return Fail(fileName, "name", "required");

            object locationValue;
            o.TryGetValue("location", out locationValue);
            Dictionary<string, object> location = locationValue as Dictionary<string, object>;
            if (locationValue != null && location == null)
                return Fail(fileName, "location", "must be an object with region and place");
            if (location == null)
                location = new Dictionary<string, object>();

            string region;
            if ((problem = Text(location, "region", null, out region)) != null)
                return Fail(fileName, "location.region", problem);
            if (string.IsNullOrEmpty(region))
                return Fail(fileName, "location.region", "required");
            string place;
            if ((problem = Text(location, "place", null, out place)) != null)
                return Fail(fileName, "location.place", problem);
            if (string.IsNullOrEmpty(place))
                return Fail(fileName, "location.place", "required");

            double[] position;
            if (!Numbers(o, "position", out position) || position == null || position.Length != 3)
                return Fail(fileName, "position", "required, must be [x, y, z]");

            string rawClass;
            if ((problem = Text(o, "baseClass", "Spellsword", out rawClass)) != null)
                return Fail(fileName, "baseClass", problem);
            string baseClass = HumanClasses.Canonical(rawClass);
            if (baseClass == null)
                return Fail(fileName, "baseClass", "unknown class \"" + rawClass + "\"");

            string rawGender;
            if ((problem = Text(o, "gender", "", out rawGender)) != null)
                return Fail(fileName, "gender", problem);
            string gender;
            if (string.IsNullOrEmpty(rawGender))
                gender = "";
            else if (string.Equals(rawGender, "Male", StringComparison.OrdinalIgnoreCase))
                gender = "Male";
            else if (string.Equals(rawGender, "Female", StringComparison.OrdinalIgnoreCase))
                gender = "Female";
            else
                return Fail(fileName, "gender", "must be Male or Female (got \"" + rawGender + "\")");

            string rawBravery;
            if ((problem = Text(o, "bravery", "Normal", out rawBravery)) != null)
                return Fail(fileName, "bravery", problem);
            Bravery bravery;
            if (!TryParseBravery(rawBravery, out bravery))
                return Fail(fileName, "bravery", "must be Coward, Normal or Brave (got \"" + rawBravery + "\")");

            double flee;
            if ((problem = Number(o, "fleeHealthPercent", 25, out flee)) != null || flee != Math.Floor(flee))
                return Fail(fileName, "fleeHealthPercent", "must be a whole number");
            if (flee < 1 || flee > 99)
                return Fail(fileName, "fleeHealthPercent", "must be 1-99 (got " + Num(flee) + ")");

            double[] calm;
            if (!Numbers(o, "calmDownHours", out calm) || (calm != null && calm.Length != 2))
                return Fail(fileName, "calmDownHours", "must be [min, max]");
            if (calm == null)
                calm = new double[] { 6, 48 };
            if (!(calm[0] > 0) || calm[0] > calm[1])
                return Fail(fileName, "calmDownHours", "need 0 < min <= max (got [" + Num(calm[0]) + ", " + Num(calm[1]) + "])");

            double wander;
            if ((problem = Number(o, "wanderRadius", 8, out wander)) != null)
                return Fail(fileName, "wanderRadius", problem);
            if (wander < 0)
                return Fail(fileName, "wanderRadius", "must be 0 or more (got " + Num(wander) + ")");

            bool crime;
            if ((problem = Bool(o, "crimeOnAttack", true, out crime)) != null)
                return Fail(fileName, "crimeOnAttack", problem);

            NpcDefinition d = new NpcDefinition();
            d.Id = id;
            d.Name = name;
            d.Region = region;
            d.Place = place;
            d.X = (float)position[0];
            d.Y = (float)position[1];
            d.Z = (float)position[2];
            d.BaseClass = baseClass;
            d.Gender = gender;
            d.Bravery = bravery;
            d.FleeHealthPercent = (int)flee;
            d.CalmDownMinHours = (float)calm[0];
            d.CalmDownMaxHours = (float)calm[1];
            d.CrimeOnAttack = crime;
            d.WanderRadius = (float)wander;
            d.SourceFile = fileName;

            ParseResult ok = new ParseResult();
            ok.Definition = d;
            return ok;
        }

        // Field readers: a missing or null key yields the fallback; a value of the wrong type yields a problem.

        static string Text(Dictionary<string, object> o, string key, string fallback, out string value)
        {
            object raw;
            value = fallback;
            if (!o.TryGetValue(key, out raw) || raw == null)
                return null;
            value = raw as string;
            return value == null ? "must be text" : null;
        }

        static string Number(Dictionary<string, object> o, string key, double fallback, out double value)
        {
            object raw;
            value = fallback;
            if (!o.TryGetValue(key, out raw) || raw == null)
                return null;
            if (!(raw is double))
                return "must be a number";
            value = (double)raw;
            return null;
        }

        static string Bool(Dictionary<string, object> o, string key, bool fallback, out bool value)
        {
            object raw;
            value = fallback;
            if (!o.TryGetValue(key, out raw) || raw == null)
                return null;
            if (!(raw is bool))
                return "must be true or false";
            value = (bool)raw;
            return null;
        }

        /// <summary>False if present but not an array of numbers; values is null when the key is missing.</summary>
        static bool Numbers(Dictionary<string, object> o, string key, out double[] values)
        {
            object raw;
            values = null;
            if (!o.TryGetValue(key, out raw) || raw == null)
                return true;
            List<object> list = raw as List<object>;
            if (list == null)
                return false;
            values = new double[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                if (!(list[i] is double))
                {
                    values = null;
                    return false;
                }
                values[i] = (double)list[i];
            }
            return true;
        }

        static bool TryParseBravery(string value, out Bravery bravery)
        {
            bravery = Bravery.Normal;
            if (value == null)
                return false;
            foreach (Bravery b in (Bravery[])Enum.GetValues(typeof(Bravery)))
            {
                if (string.Equals(b.ToString(), value.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    bravery = b;
                    return true;
                }
            }
            return false;
        }

        static string Num(double f)
        {
            return f.ToString("0.###", CultureInfo.InvariantCulture);
        }

        static ParseResult Fail(string fileName, string field, string problem)
        {
            ParseResult r = new ParseResult();
            r.Error = fileName + ": " + field + ": " + problem;
            return r;
        }
    }
}

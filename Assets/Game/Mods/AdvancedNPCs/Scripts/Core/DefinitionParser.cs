using System;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;

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

            NpcDefinitionJson raw = new NpcDefinitionJson();
            try
            {
                JsonUtility.FromJsonOverwrite(json, raw);
            }
            catch (Exception e)
            {
                return Fail(fileName, "file", "invalid JSON (" + e.Message + ")");
            }

            if (string.IsNullOrEmpty(raw.id))
                return Fail(fileName, "id", "required");
            if (!IdPattern.IsMatch(raw.id))
                return Fail(fileName, "id", "use only lowercase letters, digits and underscore (got \"" + raw.id + "\")");
            if (string.IsNullOrEmpty(raw.name))
                return Fail(fileName, "name", "required");
            if (raw.location == null || string.IsNullOrEmpty(raw.location.region))
                return Fail(fileName, "location.region", "required");
            if (string.IsNullOrEmpty(raw.location.place))
                return Fail(fileName, "location.place", "required");
            if (raw.position == null || raw.position.Length != 3)
                return Fail(fileName, "position", "required, must be [x, y, z]");

            string baseClass = HumanClasses.Canonical(raw.baseClass);
            if (baseClass == null)
                return Fail(fileName, "baseClass", "unknown class \"" + raw.baseClass + "\"");

            string gender;
            if (string.IsNullOrEmpty(raw.gender))
                gender = "";
            else if (string.Equals(raw.gender, "Male", StringComparison.OrdinalIgnoreCase))
                gender = "Male";
            else if (string.Equals(raw.gender, "Female", StringComparison.OrdinalIgnoreCase))
                gender = "Female";
            else
                return Fail(fileName, "gender", "must be Male or Female (got \"" + raw.gender + "\")");

            Bravery bravery;
            if (!TryParseBravery(raw.bravery, out bravery))
                return Fail(fileName, "bravery", "must be Coward, Normal or Brave (got \"" + raw.bravery + "\")");

            if (raw.fleeHealthPercent < 1 || raw.fleeHealthPercent > 99)
                return Fail(fileName, "fleeHealthPercent", "must be 1-99 (got " + raw.fleeHealthPercent + ")");

            if (raw.calmDownHours == null || raw.calmDownHours.Length != 2)
                return Fail(fileName, "calmDownHours", "must be [min, max]");
            float min = raw.calmDownHours[0];
            float max = raw.calmDownHours[1];
            if (!(min > 0f) || min > max)
                return Fail(fileName, "calmDownHours", "need 0 < min <= max (got [" + Num(min) + ", " + Num(max) + "])");

            if (raw.wanderRadius < 0f)
                return Fail(fileName, "wanderRadius", "must be 0 or more (got " + Num(raw.wanderRadius) + ")");

            NpcDefinition d = new NpcDefinition();
            d.Id = raw.id;
            d.Name = raw.name;
            d.Region = raw.location.region;
            d.Place = raw.location.place;
            d.X = raw.position[0];
            d.Y = raw.position[1];
            d.Z = raw.position[2];
            d.BaseClass = baseClass;
            d.Gender = gender;
            d.Bravery = bravery;
            d.FleeHealthPercent = raw.fleeHealthPercent;
            d.CalmDownMinHours = min;
            d.CalmDownMaxHours = max;
            d.CrimeOnAttack = raw.crimeOnAttack;
            d.WanderRadius = raw.wanderRadius;
            d.SourceFile = fileName;

            ParseResult ok = new ParseResult();
            ok.Definition = d;
            return ok;
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

        static string Num(float f)
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

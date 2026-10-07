using System;
using System.Collections.Generic;

namespace AdvancedNPCs.Core
{
    /// <summary>
    /// One name list (spec v2.1 §5): either a vanilla-format bank (Style + Sets of name parts, glued by that
    /// vanilla style's rules) or a simple list of whole names (Style null).
    /// </summary>
    public class NameList
    {
        public string Name;
        /// <summary>breton, redguard, nord, darkelf, highelf, woodelf, khajiit or imperial; null for a simple list.</summary>
        public string Style;
        public readonly List<string[]> Sets = new List<string[]>();
        public readonly List<string> Male = new List<string>();
        public readonly List<string> Female = new List<string>();
        public readonly List<string> Surnames = new List<string>();
        /// <summary>Simple lists: text put before the surname by gender, e.g. Orsimer "gro-" / "gra-".</summary>
        public string MaleSurnamePrefix = "";
        public string FemaleSurnamePrefix = "";

        public bool IsSimple
        {
            get { return Style == null; }
        }
    }

    public class NameListResult
    {
        public NameList List;
        public string Error;
    }

    /// <summary>Reads _Namelists/*.json in either format.</summary>
    public static class NameListParser
    {
        public static readonly string[] Styles = { "breton", "redguard", "nord", "darkelf", "highelf", "woodelf", "khajiit", "imperial" };

        /// <summary>Sets a vanilla style uses (DFU NameHelper): 6, Nord 4, Redguard 5; 0 for an unknown style.</summary>
        public static int RequiredSets(string style)
        {
            if (style == "nord")
                return 4;
            if (style == "redguard")
                return 5;
            return Array.IndexOf(Styles, style) >= 0 ? 6 : 0;
        }

        /// <summary>List names as written in npc.json and as file names: trimmed, lowercase, no ".json".</summary>
        public static string Normalize(string name)
        {
            if (name == null)
                return "";
            string n = name.Trim().ToLowerInvariant();
            if (n.EndsWith(".json", StringComparison.Ordinal))
                n = n.Substring(0, n.Length - 5);
            return n;
        }

        public static NameListResult Parse(string name, string json)
        {
            string file = "_Namelists/" + name + ".json";
            NameListResult r = new NameListResult();
            object root;
            try
            {
                root = Json.Parse(json);
            }
            catch (JsonException e)
            {
                r.Error = file + ": file: invalid JSON (" + e.Message + ")";
                return r;
            }
            Dictionary<string, object> o = root as Dictionary<string, object>;
            if (o == null)
            {
                r.Error = file + ": file: invalid JSON (top level must be an object)";
                return r;
            }

            NameList list = new NameList();
            list.Name = name;
            string error;
            if (o.ContainsKey("sets"))
                error = ReadVanilla(file, o, list);
            else if (o.ContainsKey("male") || o.ContainsKey("female"))
                error = ReadSimple(file, o, list);
            else
                error = file + ": file: needs \"sets\" (vanilla format) or \"male\"/\"female\" (simple format)";

            if (error != null)
                r.Error = error;
            else
                r.List = list;
            return r;
        }

        static string ReadVanilla(string file, Dictionary<string, object> o, NameList list)
        {
            string problem;
            string rawStyle;
            if ((problem = FieldReader.Text(o, "style", null, out rawStyle)) != null)
                return file + ": style: " + problem;
            if (string.IsNullOrEmpty(rawStyle))
                return file + ": style: required (" + string.Join(", ", Styles) + ")";
            string style = rawStyle.Trim().ToLowerInvariant();
            if (RequiredSets(style) == 0)
                return file + ": style: must be one of " + string.Join(", ", Styles) + " (got \"" + rawStyle + "\")";

            List<Dictionary<string, object>> sets;
            if ((problem = FieldReader.Objects(o, "sets", out sets)) != null)
                return file + ": sets: " + problem;
            for (int i = 0; i < sets.Count; i++)
            {
                object raw;
                sets[i].TryGetValue("parts", out raw);
                List<object> items = raw as List<object>;
                if (items == null || items.Count == 0)
                    return file + ": sets[" + i + "]: needs at least one part";
                string[] parts = new string[items.Count];
                for (int p = 0; p < items.Count; p++)
                {
                    parts[p] = items[p] as string;
                    if (parts[p] == null)
                        return file + ": sets[" + i + "]: parts must be texts";
                }
                list.Sets.Add(parts);
            }
            if (list.Sets.Count < RequiredSets(style))
                return file + ": sets: style " + style + " needs " + RequiredSets(style) + " sets (got " + list.Sets.Count + ")";
            list.Style = style;
            return null;
        }

        static string ReadSimple(string file, Dictionary<string, object> o, NameList list)
        {
            string[] keys = { "male", "female", "surnames" };
            foreach (string key in keys)
            {
                object raw;
                if (!o.TryGetValue(key, out raw) || raw == null)
                    continue;
                List<object> items = raw as List<object>;
                if (items == null)
                    return file + ": " + key + ": must be a list of texts";
                List<string> target = key == "male" ? list.Male : (key == "female" ? list.Female : list.Surnames);
                foreach (object item in items)
                {
                    string s = item as string;
                    if (s == null || s.Trim().Length == 0)
                        return file + ": " + key + ": must be a list of non-empty texts";
                    target.Add(s.Trim());
                }
            }
            if (list.Male.Count == 0 && list.Female.Count == 0)
                return file + ": male: needs at least one name in male or female";

            string problem;
            if ((problem = FieldReader.Text(o, "maleSurnamePrefix", "", out list.MaleSurnamePrefix)) != null)
                return file + ": maleSurnamePrefix: " + problem;
            if ((problem = FieldReader.Text(o, "femaleSurnamePrefix", "", out list.FemaleSurnamePrefix)) != null)
                return file + ": femaleSurnamePrefix: " + problem;
            return null;
        }
    }

    /// <summary>Builds a person's name from a list with the person's own random sequence.</summary>
    public static class NameGenerator
    {
        /// <param name="nordSuffix">DFU's "nordSurnameImmutableSuffix" text ("sen").</param>
        public static string Generate(NameList list, string gender, SeededRandom rng, string nordSuffix)
        {
            bool female = gender == "Female";
            if (list.IsSimple)
            {
                List<string> firsts = female ? (list.Female.Count > 0 ? list.Female : list.Male)
                                             : (list.Male.Count > 0 ? list.Male : list.Female);
                string first = rng.Pick(firsts);
                if (list.Surnames.Count == 0)
                    return first;
                string prefix = female ? list.FemaleSurnamePrefix : list.MaleSurnamePrefix;
                return first + " " + prefix + rng.Pick(list.Surnames);
            }

            List<string[]> s = list.Sets;
            if (list.Style == "redguard")
            {
                // Single name: 0+1+2, then 3 for 75% of men, 4 for every woman.
                string name = rng.Pick(s[0]) + rng.Pick(s[1]) + rng.Pick(s[2]);
                if (female)
                    return name + rng.Pick(s[4]);
                return rng.Next(100) < 75 ? name + rng.Pick(s[3]) : name;
            }

            string firstName = female ? rng.Pick(s[2]) + rng.Pick(s[3]) : rng.Pick(s[0]) + rng.Pick(s[1]);
            string surname = list.Style == "nord"
                ? rng.Pick(s[0]) + rng.Pick(s[1]) + nordSuffix
                : rng.Pick(s[4]) + rng.Pick(s[5]);
            return firstName + " " + surname;
        }
    }
}

using System;
using System.Collections.Generic;

namespace AdvancedNPCs.Core
{
    /// <summary>npc.json "disease": what an ANPC's melee hits may pass on to the player (spec animal-ai, Disease).</summary>
    public class DiseaseSettings
    {
        /// <summary>% chance per melee hit on the player (0-100).</summary>
        public int Chance = 5;
        /// <summary>DFU Diseases enum values (index into DiseaseRules.Names); one is picked per infection.</summary>
        public readonly List<int> Diseases = new List<int>();
    }

    /// <summary>Reads "disease". No Unity or DFU types: names are DFU's Diseases enum, in its order (copied).</summary>
    public static class DiseaseRules
    {
        /// <summary>DaggerfallWorkshop.Game.MagicAndEffects.Diseases names; the index is the enum value.</summary>
        public static readonly string[] Names =
        {
            "WitchesPox", "Plague", "YellowFever", "StomachRot", "Consumption", "BrainFever", "SwampRot", "CalironsCurse",
            "Cholera", "Leprosy", "WoundRot", "RedDeath", "BloodRot", "TyphoidFever", "Dementia", "Chrondiasis", "WizardFever",
        };
        static readonly string[] Keys = { "chance", "diseases" };

        /// <summary>"Stomach Rot" / "stomach_rot" / "StomachRot" -> its enum value; -1 if unknown.</summary>
        public static int Index(string raw)
        {
            if (raw == null)
                return -1;
            string k = raw.Replace(" ", "").Replace("_", "").Replace("'", "").Trim();
            for (int i = 0; i < Names.Length; i++)
                if (string.Equals(Names[i], k, StringComparison.OrdinalIgnoreCase))
                    return i;
            return -1;
        }

        /// <summary>Missing or null = none (settings null). Null, or a problem with its key ("disease.diseases").</summary>
        public static string Read(Dictionary<string, object> root, out string key, out DiseaseSettings settings, List<string> warnings)
        {
            settings = null;
            key = "disease";
            object raw;
            if (!root.TryGetValue("disease", out raw) || raw == null)
                return null;
            Dictionary<string, object> o = raw as Dictionary<string, object>;
            if (o == null)
                return "must be an object like { \"chance\": 5, \"diseases\": [\"Plague\"] }";
            foreach (string k in FieldReader.UnknownKeys(o, Keys))
                warnings.Add("disease." + k + ": unknown field (use chance, diseases), ignored");
            DiseaseSettings s = new DiseaseSettings();
            double chance;
            if (FieldReader.Number(o, "chance", 5, out chance) != null || (o.ContainsKey("chance") && o["chance"] == null) ||
                double.IsNaN(chance) || chance < 0 || chance > 100 || chance != Math.Floor(chance))
            {
                key = "disease.chance";
                return "must be a whole number from 0 to 100";
            }
            s.Chance = (int)chance;
            List<string> names;
            object single;
            if (o.TryGetValue("diseases", out single) && single is string)
                names = new List<string> { (string)single };
            else if (FieldReader.Texts(o, "diseases", out names) != null)
                names = null;
            if (names == null || names.Count == 0)
            {
                key = "disease.diseases";
                return "required: a disease name or a list of them (" + string.Join(", ", Names) + ")";
            }
            foreach (string n in names)
            {
                int i = Index(n);
                if (i < 0)
                {
                    key = "disease.diseases";
                    return "unknown disease \"" + n + "\" (use " + string.Join(", ", Names) + ")";
                }
                if (!s.Diseases.Contains(i))
                    s.Diseases.Add(i);
            }
            settings = s;
            return null;
        }
    }
}

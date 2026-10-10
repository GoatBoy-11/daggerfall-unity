using System;
using System.Collections.Generic;

namespace AdvancedNPCs.Core
{
    public class NpcMagicDefinition
    {
        public readonly List<string> Spells = new List<string>();
        public bool UsesMagicka = true;
        public int MaxMagicka = 150;
        public string Recovery = "onAreaEntry";
        public float CooldownMin = 4;
        public float CooldownMax = 7;
        public float RestHours = 8;
        public int Skill = 80;
    }

    public static class NpcMagicParser
    {
        static readonly string[] Keys = { "spells", "usesMagicka", "maxMagicka", "recovery", "castCooldown", "restHours", "skill" };
        public static string Read(Dictionary<string, object> root, string file, List<string> warnings, out NpcMagicDefinition result)
        {
            result = null;
            Dictionary<string, object> o;
            string problem = FieldReader.Object(root, "magic", out o);
            if (problem != null || o == null) return problem;
            NpcMagicDefinition d = new NpcMagicDefinition();
            foreach (string key in FieldReader.UnknownKeys(o, Keys))
                warnings.Add(file + ": magic." + key + ": unknown field, ignored");
            List<string> spells;
            if ((problem = FieldReader.Texts(o, "spells", out spells)) != null) return "spells: " + problem;
            if (spells == null) return "spells: required (list of standard spell names, or [] to disable spells)";
            foreach (string spell in spells)
            {
                string name = spell.Trim();
                if (!d.Spells.Exists(delegate(string x) { return string.Equals(x, name, StringComparison.OrdinalIgnoreCase); }))
                    d.Spells.Add(name);
            }
            if ((problem = FieldReader.Bool(o, "usesMagicka", true, out d.UsesMagicka)) != null)
                return "usesMagicka: " + problem;
            double n;
            if (FieldReader.Number(o, "maxMagicka", 150, out n) != null || !Finite(n) || n < 0 || n > 1000000 || n != Math.Floor(n))
                return "maxMagicka: must be a whole number from 0 to 1000000";
            d.MaxMagicka = (int)n;
            if (FieldReader.Number(o, "skill", 80, out n) != null || !Finite(n) || n < 1 || n > 100 || n != Math.Floor(n))
                return "skill: must be a whole number from 1 to 100";
            d.Skill = (int)n;
            if (FieldReader.Number(o, "restHours", 8, out n) != null || !Finite(n) || n <= 0 || n > 720)
                return "restHours: must be above 0 and at most 720";
            d.RestHours = (float)n;
            string recovery;
            if (FieldReader.Text(o, "recovery", "onAreaEntry", out recovery) != null)
                return "recovery: must be onAreaEntry or rest";
            if (string.Equals(recovery.Trim(), "onAreaEntry", StringComparison.OrdinalIgnoreCase)) d.Recovery = "onAreaEntry";
            else if (string.Equals(recovery.Trim(), "rest", StringComparison.OrdinalIgnoreCase)) d.Recovery = "rest";
            else return "recovery: must be onAreaEntry or rest";
            double[] cooldown;
            if (!FieldReader.Numbers(o, "castCooldown", out cooldown) ||
                (cooldown != null && (cooldown.Length != 2 || !Finite(cooldown[0]) || !Finite(cooldown[1]) ||
                 cooldown[0] < 1 || cooldown[0] > cooldown[1] || cooldown[1] > 3600)))
                return "castCooldown: must be [min, max] seconds with 1 <= min <= max <= 3600";
            if (cooldown != null) { d.CooldownMin = (float)cooldown[0]; d.CooldownMax = (float)cooldown[1]; }
            result = d;
            return null;
        }
        static bool Finite(double n) { return !double.IsNaN(n) && !double.IsInfinity(n); }
    }

    /// <summary>Pure resource and visit rules, shared by runtime and tests.</summary>
    public static class NpcMagicRules
    {
        public static bool CanAfford(bool usesMagicka, int current, int cost)
        {
            return cost >= 0 && (!usesMagicka || current >= cost);
        }
        public static void Enter(NpcMagicDefinition d, NpcState s, long visit, ulong now)
        {
            if (!s.magicInitialized || (d.Recovery == "onAreaEntry" && s.magicVisit != visit))
            {
                s.magicka = d.MaxMagicka;
                s.magicRemainder = 0;
                s.magicUpdatedAt = now;
                s.magicRestAfter = now;
                s.magicCooldown = 0;
            }
            s.magicka = Math.Max(0, Math.Min(d.MaxMagicka, s.magicka));
            s.magicMax = d.MaxMagicka;
            s.magicUnlimited = !d.UsesMagicka;
            s.magicRefillsOnEntry = d.Recovery == "onAreaEntry";
            s.magicInitialized = true;
            s.magicVisit = visit;
        }
        public static void Recover(NpcMagicDefinition d, NpcState s, ulong now, bool inCombat)
        {
            ulong from = Math.Max(s.magicUpdatedAt, s.magicRestAfter);
            if (inCombat) s.magicRestAfter = now + 60;
            else if (d.UsesMagicka && d.Recovery == "rest" && now > from && s.magicka < d.MaxMagicka)
            {
                double points = s.magicRemainder + (now - from) * (double)d.MaxMagicka / (d.RestHours * 3600.0);
                int gained = (int)Math.Min(d.MaxMagicka, Math.Floor(points));
                s.magicka = Math.Min(d.MaxMagicka, s.magicka + gained);
                s.magicRemainder = s.magicka == d.MaxMagicka ? 0 : points - gained;
            }
            s.magicUpdatedAt = now;
        }
    }
}

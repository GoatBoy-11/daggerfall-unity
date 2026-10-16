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
        public NpcHealingDefinition Healing;
    }

    public class NpcHealingDefinition
    {
        public bool Allies = true;
        public string Player = "friendly";
        public bool OutsideCombat = true;
        public double HealthThreshold = 0.6;
    }

    /// <summary>Consent, allegiance and health checks for optional support healing.</summary>
    public static class NpcHealingRules
    {
        public const float Range = 2.5f;
        public static bool NeedsHealing(int health, int maxHealth, double threshold)
        {
            return health > 0 && maxHealth > 0 && (double)health / maxHealth < threshold;
        }
        public static bool FriendlyPlayer(bool enemy, bool angry, string reaction)
        {
            return !enemy && !angry && (reaction == "likes" || reaction == "loves");
        }
        public static bool Allies(bool enemy, bool angry, bool otherEnemy, bool otherAngry, bool fightingEachOther)
        {
            return !fightingEachOther && enemy == otherEnemy && (enemy || (!angry && !otherAngry));
        }
    }

    public static class NpcMagicParser
    {
        static readonly string[] Keys = { "spells", "usesMagicka", "maxMagicka", "recovery", "castCooldown", "restHours", "skill", "healing" };
        static readonly string[] HealingKeys = { "allies", "player", "outsideCombat", "healthThreshold" };
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
            Dictionary<string, object> healing;
            if ((problem = FieldReader.Object(o, "healing", out healing)) != null) return "healing: " + problem;
            if (healing != null)
            {
                NpcHealingDefinition h = new NpcHealingDefinition();
                foreach (string key in FieldReader.UnknownKeys(healing, HealingKeys))
                    warnings.Add(file + ": magic.healing." + key + ": unknown field, ignored");
                if ((problem = FieldReader.Bool(healing, "allies", true, out h.Allies)) != null) return "healing.allies: " + problem;
                if ((problem = FieldReader.Bool(healing, "outsideCombat", true, out h.OutsideCombat)) != null) return "healing.outsideCombat: " + problem;
                string player;
                if (FieldReader.Text(healing, "player", "friendly", out player) != null) return "healing.player: must be friendly or none";
                h.Player = player.Trim().ToLowerInvariant();
                if (h.Player != "friendly" && h.Player != "none") return "healing.player: must be friendly or none";
                if (FieldReader.Number(healing, "healthThreshold", 0.6, out n) != null || !Finite(n) || n <= 0 || n > 1)
                    return "healing.healthThreshold: must be above 0 and at most 1";
                h.HealthThreshold = n;
                d.Healing = h;
            }
            result = d;
            return null;
        }
        static bool Finite(double n) { return !double.IsNaN(n) && !double.IsInfinity(n); }
    }

    /// <summary>Pure resource and visit rules, shared by runtime and tests.</summary>
    public static class NpcMagicRules
    {
        public static bool TimedCompanion(NpcState s)
        {
            return s.companion != null && !s.dead && !s.hostile && CompanionCombatRules.Contract(s.companion.status);
        }
        public static bool CanAfford(bool usesMagicka, int current, int cost)
        {
            return cost >= 0 && (!usesMagicka || current >= cost);
        }
        public static void Enter(NpcMagicDefinition d, NpcState s, long visit, ulong now)
        {
            if (!s.magicInitialized || (s.companion == null && d.Recovery == "onAreaEntry" && s.magicVisit != visit))
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
            s.magicRefillsOnEntry = d.Recovery == "onAreaEntry" && !TimedCompanion(s);
            s.magicInitialized = true;
            s.magicVisit = visit;
        }
        public static void Recover(NpcMagicDefinition d, NpcState s, ulong now, bool inCombat)
        {
            ulong from = Math.Max(s.magicUpdatedAt, s.magicRestAfter);
            if (inCombat) s.magicRestAfter = now + 60;
            else if (d.UsesMagicka && (d.Recovery == "rest" || TimedCompanion(s)) && now > from && s.magicka < d.MaxMagicka)
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

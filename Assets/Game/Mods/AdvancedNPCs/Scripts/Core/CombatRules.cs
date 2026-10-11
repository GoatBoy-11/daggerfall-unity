using System;
using System.Collections.Generic;

namespace AdvancedNPCs.Core
{
    /// <summary>
    /// Optional "combat" tuning in npc.json: { "health": x, "damage": x, "attackSpeed": x }, all multipliers
    /// of DFU's own values (1 = unchanged). "damage" scales a creature's melee damage ranges; human classes take
    /// their damage from weapons, so it needs a creature baseClass. No Unity or DFU types.
    /// </summary>
    public static class CombatRules
    {
        public const double MinScale = 0.05, MaxScale = 10, MinSpeed = 0.25, MaxSpeed = 4;
        static readonly string[] Keys = { "health", "damage", "attackSpeed" };

        /// <summary>Reads "combat" from an npc.json root. Returns null, or a problem with its key ("combat.health").</summary>
        public static string Read(Dictionary<string, object> root, bool isCreature, out string key,
            out float health, out float damage, out float attackSpeed)
        {
            health = damage = attackSpeed = 1f;
            key = "combat";
            object raw;
            if (!root.TryGetValue("combat", out raw) || raw == null)
                return null;
            Dictionary<string, object> o = raw as Dictionary<string, object>;
            if (o == null)
                return "must be an object like { \"health\": 0.7, \"damage\": 0.7, \"attackSpeed\": 1.3 }";
            foreach (string k in o.Keys)
            {
                if (Array.IndexOf(Keys, k) < 0)
                {
                    key = "combat." + k;
                    return "unknown setting (use health, damage or attackSpeed)";
                }
            }
            double h, d, s;
            string problem;
            if ((problem = Scale(o, "health", MinScale, MaxScale, out h, ref key)) != null) return problem;
            if ((problem = Scale(o, "damage", MinScale, MaxScale, out d, ref key)) != null) return problem;
            if ((problem = Scale(o, "attackSpeed", MinSpeed, MaxSpeed, out s, ref key)) != null) return problem;
            if (o.ContainsKey("damage") && !isCreature)
            {
                key = "combat.damage";
                return "needs a creature baseClass (human classes take their damage from weapons)";
            }
            health = (float)h; damage = (float)d; attackSpeed = (float)s;
            return null;
        }

        static string Scale(Dictionary<string, object> o, string name, double min, double max, out double value, ref string key)
        {
            string problem = FieldReader.Number(o, name, 1, out value);
            if (problem == null && (o.ContainsKey(name) && o[name] == null || double.IsNaN(value) || value < min || value > max))
                problem = "must be a number from " + FieldReader.Num(min) + " to " + FieldReader.Num(max);
            if (problem != null)
                key = "combat." + name;
            return problem;
        }

        /// <summary>Scaled max health: rounded, never below 1 (0 stays 0).</summary>
        public static int ScaleHealth(int maxHealth, float scale)
        {
            if (maxHealth <= 0)
                return maxHealth;
            return Math.Max(1, (int)Math.Round(maxHealth * scale, MidpointRounding.AwayFromZero));
        }

        /// <summary>Scaled damage range: an unused attack (0-0) stays unused; otherwise min >= 1 and max >= min.</summary>
        public static void ScaleDamage(int min, int max, float scale, out int scaledMin, out int scaledMax)
        {
            if (min <= 0 && max <= 0)
            {
                scaledMin = min; scaledMax = max;
                return;
            }
            scaledMin = Math.Max(1, (int)Math.Round(min * scale, MidpointRounding.AwayFromZero));
            scaledMax = Math.Max(scaledMin, (int)Math.Round(max * scale, MidpointRounding.AwayFromZero));
        }

        /// <summary>
        /// DFU's EnemyAttack resets MeleeTimer to the wait before the next swing. A timer that went up since the last
        /// frame is a fresh reset and is divided by the attack speed; the normal countdown is left alone.
        /// </summary>
        public static float PacedMeleeTimer(float previous, float current, float attackSpeed)
        {
            if (attackSpeed == 1f || current <= previous + 1e-4f)
                return current;
            return current / attackSpeed;
        }
    }
}

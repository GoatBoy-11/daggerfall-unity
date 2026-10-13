using System;
using System.Collections.Generic;

namespace AdvancedNPCs.Core
{
    /// <summary>
    /// Optional "combat" tuning in npc.json: { "health": x, "damage": x, "attackSpeed": x }, all multipliers
    /// of DFU's own values (1 = unchanged). "damage" scales a creature's melee damage ranges; human classes take
    /// their damage from weapons, so it needs a creature baseClass. "rangedAtDistance": true lets a human class
    /// shoot DFU's bow at a distant target, and a creature throw (off by default); "rangedAnimation" names the sheet
    /// it plays; "projectileDamage" ([min, max] or one number) sets what a ranged hit does (DFU's to-hit roll stays).
    /// "runSpeed" (1-2, default 1.2) is running speed as a multiple of walking; "canRun": false never runs.
    /// No Unity or DFU types.
    /// </summary>
    public static class CombatRules
    {
        public const double MinScale = 0.05, MaxScale = 10, MinSpeed = 0.25, MaxSpeed = 4;
        public const int MaxProjectileDamage = 1000;
        static readonly string[] Keys = { "health", "damage", "attackSpeed", "rangedAtDistance", "rangedAnimation", "projectileDamage", "runSpeed", "canRun" };

        /// <summary>Reads "combat" from an npc.json root. Returns null, or a problem with its key ("combat.health").</summary>
        public static string Read(Dictionary<string, object> root, bool isCreature, out string key,
            out float health, out float damage, out float attackSpeed, out bool rangedAtDistance, out string rangedAnimation,
            out int projectileMin, out int projectileMax, out float runSpeed, out bool canRun)
        {
            health = damage = attackSpeed = 1f;
            rangedAtDistance = false;
            rangedAnimation = null;
            projectileMin = projectileMax = 0;
            runSpeed = GaitRules.DefaultRunSpeed;
            canRun = true;
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
                    return "unknown setting (use health, damage, attackSpeed, rangedAtDistance, rangedAnimation, projectileDamage, runSpeed or canRun)";
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
            object rawRanged;
            if (o.TryGetValue("rangedAtDistance", out rawRanged))
            {
                key = "combat.rangedAtDistance";
                if (!(rawRanged is bool))
                    return "must be true or false";
                rangedAtDistance = (bool)rawRanged;
            }
            object rawSheet;
            if (o.TryGetValue("rangedAnimation", out rawSheet))
            {
                key = "combat.rangedAnimation";
                string sheet = rawSheet as string;
                if (sheet == null || !SpriteStates.IsAttackSheet(sheet))
                    return "must be the name of an attack_range*, attack_ranged* or attack* sheet, like \"attack_range_1\"";
                rangedAnimation = sheet;
            }
            if (o.ContainsKey("projectileDamage"))
            {
                key = "combat.projectileDamage";
                problem = ProjectileDamage(o["projectileDamage"], out projectileMin, out projectileMax);
                if (problem != null)
                    return problem;
                if (!rangedAtDistance)
                    return "needs \"rangedAtDistance\": true";
            }
            if (o.ContainsKey("runSpeed"))
            {
                double rs;
                key = "combat.runSpeed";
                if (o["runSpeed"] == null || FieldReader.Number(o, "runSpeed", GaitRules.DefaultRunSpeed, out rs) != null ||
                    double.IsNaN(rs) || rs < GaitRules.MinRunSpeed || rs > GaitRules.MaxRunSpeed)
                    return "must be a number from 1 to 2 (times walking speed)";
                runSpeed = (float)rs;
            }
            object rawCanRun;
            if (o.TryGetValue("canRun", out rawCanRun))
            {
                key = "combat.canRun";
                if (!(rawCanRun is bool))
                    return "must be true or false";
                canRun = (bool)rawCanRun;
            }
            key = "combat";
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

        static string ProjectileDamage(object raw, out int min, out int max)
        {
            min = max = 0;
            const string Shape = "must be a whole number from 1 to 1000, or [min, max] of them";
            List<object> pair = raw as List<object>;
            if (raw is double)
            {
                if (!Whole((double)raw, out min))
                    return Shape;
                max = min;
                return null;
            }
            if (pair == null || pair.Count != 2 || !(pair[0] is double) || !(pair[1] is double) ||
                !Whole((double)pair[0], out min) || !Whole((double)pair[1], out max))
                return Shape;
            if (max < min)
                return "min must not be above max";
            return null;
        }

        static bool Whole(double d, out int value)
        {
            value = 0;
            if (d != Math.Floor(d) || d < 1 || d > MaxProjectileDamage)
                return false;
            value = (int)d;
            return true;
        }

        /// <summary>DFU's bow range (EnemyAttack min/maxRangedDistance, 6 to 51.2 m): where a creature may throw.</summary>
        public static bool InThrowRange(float distance)
        {
            return distance > 6f && distance < 51.2f;
        }

        /// <summary>A projectileDamage hit: min to max (both included) from a roll in [0, 1).</summary>
        public static int RollProjectileDamage(int min, int max, float roll)
        {
            int n = min + (int)Math.Floor(roll * (max - min + 1));
            return Math.Min(max, Math.Max(min, n));
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

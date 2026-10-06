using System;

namespace AdvancedNPCs.Core
{
    /// <summary>
    /// Health is saved as a fraction because DFU rolls a new max health (scaled by player level)
    /// every time a class enemy is created.
    /// </summary>
    public static class HealthRules
    {
        public static float Fraction(int current, int max)
        {
            if (max <= 0 || current >= max)
                return 1f;
            return (float)current / max;
        }

        /// <summary>Health to restore for a freshly spawned NPC. Unknown or non-positive fractions mean full health.</summary>
        public static int Restore(int maxHealth, float fraction)
        {
            if (maxHealth <= 0 || fraction <= 0f || fraction >= 1f)
                return maxHealth;
            return Math.Max(1, (int)Math.Round(maxHealth * fraction));
        }
    }
}

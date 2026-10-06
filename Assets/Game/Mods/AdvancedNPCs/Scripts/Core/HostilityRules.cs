using System;

namespace AdvancedNPCs.Core
{
    public enum CombatChoice
    {
        Fight,
        Flee,
    }

    public enum HostileFlip
    {
        None,
        PlayerAttack,
        EngineSweep,
    }

    /// <summary>Pure decision rules for Advanced NPC hostility. No Unity or DFU types.</summary>
    public static class HostilityRules
    {
        public const ulong SecondsPerHour = 3600;

        /// <summary>Fight or flee for the given bravery and current health fraction (0..1).</summary>
        public static CombatChoice Decide(Bravery bravery, float healthFraction, int fleeHealthPercent)
        {
            switch (bravery)
            {
                case Bravery.Coward:
                    return CombatChoice.Flee;
                case Bravery.Brave:
                    return CombatChoice.Fight;
                default:
                    return healthFraction * 100f < fleeHealthPercent ? CombatChoice.Flee : CombatChoice.Fight;
            }
        }

        /// <summary>In-game second at which a player-hostile NPC calms down.</summary>
        public static ulong NewCalmDeadline(ulong now, float minHours, float maxHours, Random rng)
        {
            double hours = minHours + rng.NextDouble() * (maxHours - minHours);
            return now + (ulong)Math.Round(hours * SecondsPerHour);
        }

        public static bool IsCalmDue(ulong now, ulong deadline)
        {
            return now >= deadline;
        }

        /// <summary>
        /// Explains why EnemyMotor.IsHostile turned true while the brain believed the NPC was calm.
        /// A real player attack always damages the NPC or points its senses at the player;
        /// GameManager.MakeEnemiesHostile() does neither.
        /// </summary>
        public static HostileFlip ClassifyHostileFlip(bool brainHostile, bool motorHostile, bool healthDropped, bool targetIsPlayer)
        {
            if (brainHostile || !motorHostile)
                return HostileFlip.None;
            return (healthDropped || targetIsPlayer) ? HostileFlip.PlayerAttack : HostileFlip.EngineSweep;
        }

        /// <summary>
        /// Who gets the blame for a death. DFU raises OnDeath before it tells the NPC who hit it,
        /// so a calm NPC that dies without fighting a creature must have been killed by the player.
        /// </summary>
        public static bool KilledByPlayer(bool hostileToPlayer, bool fightingCreature)
        {
            return hostileToPlayer || !fightingCreature;
        }
    }
}

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
        /// A real player attack damages the NPC, or runs EnemyMotor.MakeEnemyHostileToAttacker which both
        /// targets the player and raises the attacker signal (GiveUpTimer). GameManager.MakeEnemiesHostile()
        /// does neither; the NPC's own senses may target the player during the sweep, but without the signal.
        /// </summary>
        public static HostileFlip ClassifyHostileFlip(bool brainHostile, bool motorHostile, bool healthDropped,
            bool targetIsPlayer, bool attackerSignal)
        {
            if (brainHostile || !motorHostile)
                return HostileFlip.None;
            return (healthDropped || (targetIsPlayer && attackerSignal)) ? HostileFlip.PlayerAttack : HostileFlip.EngineSweep;
        }

        /// <summary>
        /// Who gets the blame for a death, decided after the killing blow's frame. DFU raises OnDeath before it
        /// tells the NPC who hit it, but by the end of that frame only a player attack has turned
        /// EnemyMotor.IsHostile on for an NPC that was calm. An NPC already hostile to the player is blamed on
        /// the player unless it was busy with a creature.
        /// </summary>
        public static bool KilledByPlayer(bool wasHostileToPlayer, bool fightingCreature, bool motorHostileAfterHit)
        {
            if (!wasHostileToPlayer)
                return motorHostileAfterHit;
            return !fightingCreature;
        }
    }
}

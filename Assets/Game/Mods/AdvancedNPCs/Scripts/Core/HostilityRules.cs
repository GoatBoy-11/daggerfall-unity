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

    /// <summary>A Normal ANPC's one flee roll while its health is below the line (kept per ANPC, not saved).</summary>
    public struct FleeRoll
    {
        public bool Below;
        public bool Flee;
    }

    /// <summary>Pure decision rules for Advanced NPC hostility. No Unity or DFU types.</summary>
    public static class HostilityRules
    {
        public const ulong SecondsPerHour = 3600;

        /// <summary>
        /// Fight or flee for the bravery and current health fraction (0..1). Prudent flees below fleeHealthPercent;
        /// Normal rolls once (fleeChance %) when it first drops below half of it and keeps that answer while below.
        /// </summary>
        public static CombatChoice Decide(Bravery bravery, float healthFraction, int fleeHealthPercent, int fleeChance,
            ref FleeRoll roll, double random01)
        {
            switch (bravery)
            {
                case Bravery.Coward:
                    return CombatChoice.Flee;
                case Bravery.Brave:
                    return CombatChoice.Fight;
                case Bravery.Prudent:
                    return healthFraction * 100f < fleeHealthPercent ? CombatChoice.Flee : CombatChoice.Fight;
                default:
                    if (healthFraction * 100f >= fleeHealthPercent / 2f)
                    {
                        roll.Below = false;
                        return CombatChoice.Fight;
                    }
                    if (!roll.Below)
                    {
                        roll.Below = true;
                        roll.Flee = random01 * 100.0 < fleeChance;
                    }
                    return roll.Flee ? CombatChoice.Flee : CombatChoice.Fight;
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
        /// The player target and the signal still count when something (EnemySenses' language pacification roll)
        /// has already turned IsHostile off again before the brain looked.
        /// </summary>
        public static HostileFlip ClassifyHostileFlip(bool brainHostile, bool motorHostile, bool healthDropped,
            bool targetIsPlayer, bool attackerSignal)
        {
            if (brainHostile)
                return HostileFlip.None;
            bool playerSignal = targetIsPlayer && attackerSignal;
            if (!motorHostile)
                return playerSignal ? HostileFlip.PlayerAttack : HostileFlip.None;
            return (healthDropped || playerSignal) ? HostileFlip.PlayerAttack : HostileFlip.EngineSweep;
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

using System;
using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class HostilityRulesTests
    {
        [TestCase(1.0f)]
        [TestCase(0.01f)]
        public void Coward_AlwaysFlees(float health)
        {
            Assert.AreEqual(CombatChoice.Flee, HostilityRules.Decide(Bravery.Coward, health, 25));
        }

        [TestCase(1.0f)]
        [TestCase(0.01f)]
        public void Brave_AlwaysFights(float health)
        {
            Assert.AreEqual(CombatChoice.Fight, HostilityRules.Decide(Bravery.Brave, health, 25));
        }

        [TestCase(1.0f, CombatChoice.Fight)]
        [TestCase(0.25f, CombatChoice.Fight)]
        [TestCase(0.24f, CombatChoice.Flee)]
        [TestCase(0.0f, CombatChoice.Flee)]
        public void Normal_FleesBelowThreshold(float health, CombatChoice expected)
        {
            Assert.AreEqual(expected, HostilityRules.Decide(Bravery.Normal, health, 25));
        }

        [Test]
        public void CalmDeadline_IsWithinRange()
        {
            Random rng = new Random(1234);
            for (int i = 0; i < 200; i++)
            {
                ulong deadline = HostilityRules.NewCalmDeadline(1000UL, 6f, 48f, rng);
                Assert.GreaterOrEqual(deadline, 1000UL + 6UL * 3600UL);
                Assert.LessOrEqual(deadline, 1000UL + 48UL * 3600UL);
            }
        }

        [Test]
        public void CalmDeadline_EqualMinMax_IsExact()
        {
            Assert.AreEqual(500UL + 2UL * 3600UL, HostilityRules.NewCalmDeadline(500UL, 2f, 2f, new Random(1)));
        }

        [Test]
        public void CalmDeadline_ReHitLater_MovesForward()
        {
            Random rng = new Random(7);
            ulong first = HostilityRules.NewCalmDeadline(0UL, 1f, 1f, rng);
            ulong second = HostilityRules.NewCalmDeadline(1800UL, 1f, 1f, rng);
            Assert.Greater(second, first);
        }

        [TestCase(99UL, 100UL, false)]
        [TestCase(100UL, 100UL, true)]
        [TestCase(10000000UL, 100UL, true)]
        public void IsCalmDue(ulong now, ulong deadline, bool expected)
        {
            Assert.AreEqual(expected, HostilityRules.IsCalmDue(now, deadline));
        }

        // brainHostile, motorHostile, healthDropped, targetIsPlayer, attackerSignal -> expected
        [TestCase(false, false, false, false, false, HostileFlip.None)]
        [TestCase(false, false, true, false, false, HostileFlip.None)]
        [TestCase(false, true, false, false, false, HostileFlip.EngineSweep)]
        [TestCase(false, true, true, false, false, HostileFlip.PlayerAttack)]
        [TestCase(false, true, false, true, true, HostileFlip.PlayerAttack)]
        [TestCase(false, true, true, true, true, HostileFlip.PlayerAttack)]
        [TestCase(true, true, false, false, false, HostileFlip.None)]
        [TestCase(true, true, true, true, true, HostileFlip.None)]
        [TestCase(true, false, false, false, false, HostileFlip.None)]
        public void ClassifyHostileFlip(bool brainHostile, bool motorHostile, bool healthDropped, bool targetIsPlayer, bool attackerSignal, HostileFlip expected)
        {
            Assert.AreEqual(expected, HostilityRules.ClassifyHostileFlip(brainHostile, motorHostile, healthDropped, targetIsPlayer, attackerSignal));
        }

        [Test]
        public void ClassifyHostileFlip_BystanderSensesPickPlayerDuringSweep_IsEngineSweep()
        {
            // Engine sweep made a bystander hostile and its own EnemySenses then targeted the player,
            // but MakeEnemyHostileToAttacker never ran on it (no attacker signal, no damage).
            Assert.AreEqual(HostileFlip.EngineSweep, HostilityRules.ClassifyHostileFlip(false, true, false, true, false));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ClassifyHostileFlip_PlayerHitUndoneByPacifyRoll_IsStillPlayerAttack(bool healthDropped)
        {
            // EnemySenses' language pacification roll can turn IsHostile off again before the brain looks,
            // but the attacker signal and the player target are still there.
            Assert.AreEqual(HostileFlip.PlayerAttack, HostilityRules.ClassifyHostileFlip(false, false, healthDropped, true, true));
        }

        [Test]
        public void ClassifyHostileFlip_CalmWithPlayerTargetButNoSignal_IsNone()
        {
            Assert.AreEqual(HostileFlip.None, HostilityRules.ClassifyHostileFlip(false, false, true, true, false));
        }

        // wasHostileToPlayer, fightingCreature, motorHostileAfterHit -> killed by player?
        [TestCase(false, false, true, true, TestName = "KilledByPlayer_OneHitOnCalmNpcByPlayer")]
        [TestCase(false, false, false, false, TestName = "KilledByPlayer_OneHitOnCalmNpcByCreature")]
        [TestCase(false, true, true, true, TestName = "KilledByPlayer_PlayerFinishesNpcFightingCreature")]
        [TestCase(false, true, false, false, TestName = "KilledByPlayer_CreatureKillsNpcFightingIt")]
        [TestCase(true, false, true, true, TestName = "KilledByPlayer_WhileHostileToPlayer")]
        [TestCase(true, true, true, false, TestName = "KilledByPlayer_HostileButFightingCreature_NotBlamed")]
        public void KilledByPlayer(bool wasHostileToPlayer, bool fightingCreature, bool motorHostileAfterHit, bool expected)
        {
            Assert.AreEqual(expected, HostilityRules.KilledByPlayer(wasHostileToPlayer, fightingCreature, motorHostileAfterHit));
        }
    }
}

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

        // brainHostile, motorHostile, healthDropped, targetIsPlayer -> expected
        [TestCase(false, false, false, false, HostileFlip.None)]
        [TestCase(false, false, true, false, HostileFlip.None)]
        [TestCase(false, true, false, false, HostileFlip.EngineSweep)]
        [TestCase(false, true, true, false, HostileFlip.PlayerAttack)]
        [TestCase(false, true, false, true, HostileFlip.PlayerAttack)]
        [TestCase(false, true, true, true, HostileFlip.PlayerAttack)]
        [TestCase(true, true, false, false, HostileFlip.None)]
        [TestCase(true, true, true, true, HostileFlip.None)]
        [TestCase(true, false, false, false, HostileFlip.None)]
        public void ClassifyHostileFlip(bool brainHostile, bool motorHostile, bool healthDropped, bool targetIsPlayer, HostileFlip expected)
        {
            Assert.AreEqual(expected, HostilityRules.ClassifyHostileFlip(brainHostile, motorHostile, healthDropped, targetIsPlayer));
        }

        [Test]
        public void KilledByPlayer_OneHitOnCalmNpc_CountsAsPlayer()
        {
            Assert.IsTrue(HostilityRules.KilledByPlayer(false, false));
        }

        [Test]
        public void KilledByPlayer_WhileHostileToPlayer_CountsAsPlayer()
        {
            Assert.IsTrue(HostilityRules.KilledByPlayer(true, false));
        }

        [Test]
        public void KilledByPlayer_CalmNpcFightingCreature_CountsAsCreature()
        {
            Assert.IsFalse(HostilityRules.KilledByPlayer(false, true));
        }
    }
}

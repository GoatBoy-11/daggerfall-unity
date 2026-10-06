using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class HealthRulesTests
    {
        [TestCase(20, 20, 1f)]
        [TestCase(10, 20, 0.5f)]
        [TestCase(5, 0, 1f)]
        [TestCase(30, 20, 1f)]
        public void Fraction(int current, int max, float expected)
        {
            Assert.AreEqual(expected, HealthRules.Fraction(current, max));
        }

        [TestCase(150, 1f, 150)]
        [TestCase(150, 0.5f, 75)]
        [TestCase(150, 0.001f, 1)]
        [TestCase(150, 0f, 150)]
        [TestCase(150, -1f, 150)]
        [TestCase(0, 0.5f, 0)]
        public void Restore(int maxHealth, float fraction, int expected)
        {
            Assert.AreEqual(expected, HealthRules.Restore(maxHealth, fraction));
        }

        [Test]
        public void UnhurtNpc_RespawningWithHigherMaxHealth_IsStillUnhurt()
        {
            // Met at player level 1 (max 20, never hit); DFU rolls max 150 when it respawns at level 20.
            float stored = HealthRules.Fraction(20, 20);
            Assert.AreEqual(150, HealthRules.Restore(150, stored));
        }
    }
}

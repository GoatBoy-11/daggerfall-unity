using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    /// <summary>Who a calm Brave ANPC attacks on sight (spec lazy-sprites §D).</summary>
    public class AggroRulesTests
    {
        [TestCase(true, true, false, false, true, true, TestName = "IsFoe_EnemyAnpc")]
        [TestCase(true, false, false, false, true, false, TestName = "IsFoe_AnpcOnlyAngryAtPlayer")]
        [TestCase(true, true, false, false, false, false, TestName = "IsFoe_DeadEnemyAnpc")]
        [TestCase(false, false, true, false, true, true, TestName = "IsFoe_HostileVanillaEnemy")]
        [TestCase(false, false, true, true, true, false, TestName = "IsFoe_CityWatchOrAlly")]
        [TestCase(false, false, false, false, true, false, TestName = "IsFoe_PeacefulVanilla")]
        public void IsFoe(bool isAnpc, bool anpcIsEnemy, bool vanillaHostile, bool watchOrAlly, bool alive, bool expected)
        {
            Assert.AreEqual(expected, AggroRules.IsFoe(isAnpc, anpcIsEnemy, vanillaHostile, watchOrAlly, alive));
        }
    }
}

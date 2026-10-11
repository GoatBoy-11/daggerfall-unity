using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    /// <summary>Running while chasing or fleeing (spec run/greet/turret §2).</summary>
    public class GaitRulesTests
    {
        [Test]
        public void WalkSpeed_IsDfuEnemyFormula()
        {
            Assert.AreEqual(5f, GaitRules.WalkSpeed(50), 1e-5);
            Assert.AreEqual(3.75f, GaitRules.WalkSpeed(0), 1e-5);
        }

        [Test]
        public void RunSpeed_ScalesWalk_UnlessItCannotRun()
        {
            Assert.AreEqual(6f, GaitRules.RunSpeed(5f, 1.2f, true), 1e-5);
            Assert.AreEqual(5f, GaitRules.RunSpeed(5f, 1.2f, false), 1e-5);
        }

        [Test]
        public void Fighting_FarAndSeen_Runs()
        {
            Assert.AreEqual(Gait.Run, GaitRules.Fighting(true, 10f, false, true));
        }

        [Test]
        public void Fighting_Near_Walks()
        {
            Assert.AreEqual(Gait.Walk, GaitRules.Fighting(true, 3f, false, true));
            Assert.AreEqual(Gait.Walk, GaitRules.Fighting(true, 3.9f, true, true));
        }

        [Test]
        public void Fighting_BetweenStopAndStart_KeepsWhatItWasDoing()
        {
            Assert.AreEqual(Gait.Run, GaitRules.Fighting(true, 5f, true, true));
            Assert.AreEqual(Gait.Walk, GaitRules.Fighting(true, 5f, false, true));
        }

        [Test]
        public void Fighting_OutOfSight_Walks()
        {
            Assert.AreEqual(Gait.Walk, GaitRules.Fighting(false, 20f, true, true));
        }

        [Test]
        public void Fighting_CannotRun_Walks()
        {
            Assert.AreEqual(Gait.Walk, GaitRules.Fighting(true, 20f, true, false));
        }

        [Test]
        public void Fleeing_RunsWhileMoving()
        {
            Assert.AreEqual(Gait.Run, GaitRules.Fleeing(true, true));
            Assert.AreEqual(Gait.Walk, GaitRules.Fleeing(true, false));
            Assert.AreEqual(Gait.Still, GaitRules.Fleeing(false, true));
        }

        [Test]
        public void IsJump_OnlyForWorldShiftSizedSteps()
        {
            Assert.IsFalse(GaitRules.IsJump(0.2f));     // a frame of running
            Assert.IsFalse(GaitRules.IsJump(5f));       // a long hitch at run speed
            Assert.IsTrue(GaitRules.IsJump(819.2f));    // DFU's floating origin moving the ANPC itself
        }

        [Test]
        public void Calm_WalksOrStands()
        {
            Assert.AreEqual(Gait.Walk, GaitRules.Calm(true));
            Assert.AreEqual(Gait.Still, GaitRules.Calm(false));
        }
    }
}

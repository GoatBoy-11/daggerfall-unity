using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    /// <summary>Personal opinion of the player and update LOD (spec opinion-lod §A-B).</summary>
    public class OpinionLodTests
    {
        const ulong Day = 86400;

        [Test]
        public void Current_DriftsTowardZeroTwoPointsPerDay()
        {
            Assert.AreEqual(-25, OpinionRules.Current(-25, 0, 0));
            Assert.AreEqual(-23, OpinionRules.Current(-25, 0, Day));
            Assert.AreEqual(16, OpinionRules.Current(20, 0, 2 * Day));
            Assert.AreEqual(0, OpinionRules.Current(3, 0, 5 * Day));      // never overshoots
            Assert.AreEqual(0, OpinionRules.Current(-3, 0, 5 * Day));
            Assert.AreEqual(10, OpinionRules.Current(10, 5 * Day, Day));    // a clock that went back changes nothing
        }

        [Test]
        public void Change_DecaysThenAddsAndClamps()
        {
            Assert.AreEqual(-25, OpinionRules.Change(0, 0, 0, OpinionRules.Assault));
            Assert.AreEqual(-48, OpinionRules.Change(-25, 0, Day, OpinionRules.Assault));   // -23 then -25
            Assert.AreEqual(100, OpinionRules.Change(95, 0, 0, 50));
            Assert.AreEqual(-100, OpinionRules.Change(-90, 0, 0, -50));
        }

        [TestCase(0, 0, "neutral")]
        [TestCase(0, 10, "likes")]
        [TestCase(5, 25, "loves")]
        [TestCase(12, -25, "dislikes")]
        public void Band_AddsOpinionToDfuReaction(int reaction, int opinion, string expected)
        {
            Assert.AreEqual(expected, OpinionRules.Band(reaction, opinion));
        }

        [Test]
        public void State_WithAnOpinionIsSaved()
        {
            NpcState s = new NpcState();
            Assert.IsTrue(s.IsDefault());
            s.opinion = -5;
            Assert.IsFalse(s.IsDefault());
        }

        [TestCase(10f, false, 1)]
        [TestCase(39.9f, false, 1)]
        [TestCase(40f, false, 3)]
        [TestCase(99f, false, 3)]
        [TestCase(100f, false, 8)]
        [TestCase(500f, false, 8)]
        [TestCase(500f, true, 1)]
        public void Interval_ByDistanceUnlessBusy(float distance, bool busy, int expected)
        {
            Assert.AreEqual(expected, NpcLod.Interval(distance, busy));
        }

        [Test]
        public void Due_SpreadsFarAnpcsOverFrames()
        {
            Assert.IsTrue(NpcLod.Due(0, 0, 1));
            Assert.IsTrue(NpcLod.Due(7, 0, 1));
            Assert.IsTrue(NpcLod.Due(8, 0, 8));
            Assert.IsFalse(NpcLod.Due(9, 0, 8));
            Assert.IsTrue(NpcLod.Due(5, 3, 8));       // (5 + 3) % 8 == 0
            Assert.IsFalse(NpcLod.Due(5, 4, 8));
        }
    }
}

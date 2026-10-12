using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    /// <summary>How a body is drawn: death sheet rows, the resting front/back frame, billboards (spec corpse-sound settings).</summary>
    public class CorpseRulesTests
    {
        [Test]
        public void RestRow_IsFrontOrBack()
        {
            Assert.AreEqual("front", SpriteSetParser.Directions[CorpseRules.RestRow(0.0)]);
            Assert.AreEqual("front", SpriteSetParser.Directions[CorpseRules.RestRow(0.49)]);
            Assert.AreEqual("back", SpriteSetParser.Directions[CorpseRules.RestRow(0.5)]);
            Assert.AreEqual("back", SpriteSetParser.Directions[CorpseRules.RestRow(1.0)]);
        }

        [Test]
        public void Billboard_StaticPictureAlways()
        {
            Assert.IsTrue(CorpseRules.Billboard(true, true, true));
            Assert.IsTrue(CorpseRules.Billboard(true, true, false));
        }

        [Test]
        public void Billboard_LastFrameUnlessDirectionalCorpses()
        {
            Assert.IsTrue(CorpseRules.Billboard(false, true, false));
            Assert.IsFalse(CorpseRules.Billboard(false, true, true));
        }

        [Test]
        public void Billboard_NeverWhileTheDeathAnimationPlays()
        {
            Assert.IsFalse(CorpseRules.Billboard(false, false, false));
            Assert.IsFalse(CorpseRules.Billboard(false, false, true));
        }

        [Test]
        public void Row_BillboardKeepsItsRestRow_OtherwiseTheView()
        {
            Assert.AreEqual(4, CorpseRules.Row(true, 4, 2));
            Assert.AreEqual(2, CorpseRules.Row(false, 4, 2));
        }

        [Test]
        public void Frame_PlaysThenHoldsTheLast()
        {
            Assert.AreEqual(0, CorpseRules.Frame(false, 0f, 8, 6));
            Assert.AreEqual(3, CorpseRules.Frame(false, 0.4f, 8, 6));
            Assert.AreEqual(5, CorpseRules.Frame(false, 9f, 8, 6));
            Assert.AreEqual(5, CorpseRules.Frame(true, 0f, 8, 6));     // no death animation: the last frame at once
        }
    }
}

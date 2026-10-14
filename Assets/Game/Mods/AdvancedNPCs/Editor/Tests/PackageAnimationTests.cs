using System;
using System.Collections.Generic;
using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class PackageAnimationTests
    {
        [TestCase("pray", "pray", true)]
        [TestCase("pray", "pray_1", true)]
        [TestCase("pray", "pray_22", true)]
        [TestCase("pray", "PRAY_2", true)]
        [TestCase("pray", "pray_fast", false)]
        [TestCase("pray", "prayer_1", false)]
        [TestCase("pray", "pray_", false)]
        [TestCase("pray", "pray_1_extra", false)]
        [TestCase("pray_1", "pray_1", true)]
        [TestCase("pray_1", "pray_2", false)]
        [TestCase("pray_1", "pray_1_2", false)]
        public void FamiliesAndExactSheets(string request, string sheet, bool matches)
        {
            Assert.AreEqual(matches, PackageAnimations.Matches(request, sheet));
        }

        [TestCase("pray.png")]
        [TestCase("../pray")]
        [TestCase("")]
        [TestCase(" eat")]
        [TestCase("pray/idle")]
        public void InvalidNameRejected(string request) { Assert.IsFalse(PackageAnimations.ValidName(request)); }

        [Test]
        public void ReferencedCustomSheetIsOptIn()
        {
            Assert.IsFalse(PackageAnimations.Referenced(null, "pray_1"));
            Assert.IsTrue(PackageAnimations.Referenced(new[] { "pray" }, "pray_1"));
            Assert.IsFalse(PackageAnimations.Referenced(new[] { "pray_2" }, "pray_1"));
        }

        [Test]
        public void VariantsHaveNoIdleFallbackOrProjectiles()
        {
            SpriteSet set = new SpriteSet();
            foreach (string name in new[] { "idle", "eat_2", "eat_1", "eat_extra" })
                set.Animations.Add(name, new SpriteAnimation { Name = name, Frames = 4 });
            set.Projectiles.Add("eat_3", new SpriteAnimation { Name = "eat_3" });
            var variants = PackageAnimations.Variants(set, "eat");
            Assert.AreEqual(2, variants.Count);
            Assert.AreEqual("eat_1", variants[0].Name);
            Assert.AreEqual("eat_2", variants[1].Name);
            Assert.AreEqual(0, PackageAnimations.Variants(set, "pray").Count);
        }

        [Test]
        public void RandomChoiceNeverImmediatelyRepeats()
        {
            for (int previous = 0; previous < 4; previous++)
                foreach (double roll in new[] { 0, 0.25, 0.5, 0.9, 1 })
                {
                    int index = PackageAnimations.Pick(4, previous, roll);
                    Assert.That(index, Is.InRange(0, 3));
                    Assert.AreNotEqual(previous, index);
                }
            Assert.AreEqual(0, PackageAnimations.Pick(1, 0, 1));
        }

        [Test]
        public void LoopsSwitchOnlyAfterCompleteCycleAndRespectDifferentLengths()
        {
            var a = new SpriteAnimation { Name = "eat_1", Frames = 4 };
            var b = new SpriteAnimation { Name = "eat_2", Frames = 6 };
            var loop = new PackageAnimationLoop(new[] { a, b }, 8, () => 0);
            Assert.AreEqual(a, loop.Current);
            loop.Advance(0.499);
            Assert.AreEqual(a, loop.Current);
            loop.Advance(0.001);
            Assert.AreEqual(b, loop.Current);
            Assert.AreEqual(0, loop.Elapsed, 1e-9);
            loop.Advance(0.75);
            Assert.AreEqual(a, loop.Current);
            Assert.AreEqual(2, loop.Cycles);
        }

        [Test]
        public void SingleSheetRepeatsAndLargeTimeStepPreservesRemainder()
        {
            var loop = new PackageAnimationLoop(new[] { new SpriteAnimation { Name = "pray_1", Frames = 4 } }, 8, () => 0);
            loop.Advance(2.125);
            Assert.AreEqual("pray_1", loop.Current.Name);
            Assert.AreEqual(4, loop.Cycles);
            Assert.AreEqual(0.125, loop.Elapsed);
        }

        [Test]
        public void PlayPackageDefaultsAndNpcOverridesAreIndependent()
        {
            string error;
            var package = PackageParser.Parse("play_animation", "{\"type\":\"play_animation\"}", out error);
            Assert.IsNotNull(package, error);
            var overrides = (Dictionary<string, object>)Json.Parse("{\"animation\":\"pray\"}");
            Assert.AreEqual("pray", PackageParser.Resolve(package, overrides, out error).Animation);
            Assert.AreEqual("idle", PackageParser.Resolve(package, null, out error).Animation);
            var motion = new PackageMotion(PackageParser.Resolve(package, overrides, out error), () => 0);
            Assert.IsNull(motion.Tick(new PackagePoint(0, 0, 0), 0, 1));
            Assert.IsFalse(motion.Failed);
        }

        [TestCase("{\"animation\":null}")]
        [TestCase("{\"animation\":5}")]
        [TestCase("{\"animation\":\"pray.png\"}")]
        [TestCase("{\"animation\":\"../pray\"}")]
        [TestCase("{\"animation\":\"pray\",\"run\":true}")]
        public void InvalidAnimationParametersFallBack(string json)
        {
            string error;
            var package = PackageParser.Parse("play_animation", "{\"type\":\"play_animation\"}", out error);
            Assert.IsNotNull(package, error);
            Assert.IsNull(PackageParser.Resolve(package, (Dictionary<string, object>)Json.Parse(json), out error));
            Assert.IsNotNull(error);
        }

        [TestCase(359, false)]
        [TestCase(360, true)]
        [TestCase(479, true)]
        [TestCase(480, false)]
        [TestCase(720, true)]
        [TestCase(779, true)]
        [TestCase(780, false)]
        [TestCase(1080, true)]
        [TestCase(1199, true)]
        [TestCase(1200, false)]
        public void MonkPrayerHasVisibleGaps(int minute, bool praying)
        {
            string error;
            var schedule = AiSchedule.Read(Json.Parse("[{\"hours\":[\"06:00\",\"08:00\"],\"package\":\"play_animation\"},{\"hours\":[\"12:00\",\"13:00\"],\"package\":\"play_animation\"},{\"hours\":[\"18:00\",\"20:00\"],\"package\":\"play_animation\"}]"), out error);
            Assert.IsNull(error);
            Assert.AreEqual(praying, AiSchedule.Select(schedule, minute) != null);
        }
    }
}

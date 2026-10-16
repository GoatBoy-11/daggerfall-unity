using System;
using System.Collections.Generic;
using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class CivilianPackageTests
    {
        static PackageSettings Settings(string parameters = "{}")
        {
            string error;
            var package = PackageParser.Parse("wander_civilian", "{\"type\":\"wander_civilian\",\"parameters\":" + parameters + "}", out error);
            Assert.IsNotNull(package, error);
            return PackageParser.Resolve(package, null, out error);
        }
        static SpriteAnimation Sheet(string name, int frames = 4) { return new SpriteAnimation { Name = name, Frames = frames }; }

        [Test]
        public void DefaultsAreWiderWalkingAndNormallyInterruptible()
        {
            var settings = Settings();
            Assert.AreEqual(40, settings.Radius);
            Assert.AreEqual(0, settings.Pause);
            Assert.IsFalse(settings.Run);
            Assert.IsFalse(settings.OnlyInterruptAtCombat);
            string error;
            var package = PackageParser.Parse("wander_civilian", "{\"type\":\"wander_civilian\"}", out error);
            var overrides = Json.Parse("{\"radius\":25,\"center\":[2,0,3]}") as Dictionary<string, object>;
            var resolved = PackageParser.Resolve(package, overrides, out error);
            Assert.AreEqual(25, resolved.Radius);
            Assert.AreEqual(2, resolved.Center.X);
            Assert.AreEqual(40, PackageParser.Resolve(package, null, out error).Radius);
            Assert.AreEqual(0, package.Parameters.Count);
        }

        [TestCase("{\"radius\":-1}")]
        [TestCase("{\"radius\":51}")]
        [TestCase("{\"radius\":\"40\"}")]
        [TestCase("{\"radius\":null}")]
        [TestCase("{\"center\":[0,0]}")]
        [TestCase("{\"center\":[101,0,0]}")]
        [TestCase("{\"run\":true}")]
        [TestCase("{\"pause\":3}")]
        [TestCase("{\"only_interrupt_at_combat\":true}")]
        public void InvalidParametersAreRejected(string parameters)
        {
            string error;
            Assert.IsNull(PackageParser.Parse("wander_civilian", "{\"type\":\"wander_civilian\",\"parameters\":" + parameters + "}", out error));
            Assert.IsNotEmpty(error);
        }

        [TestCase(0)]
        [TestCase(0.49999)]
        public void IdleHalfDoesNotDependOnNumberOfActivitySheets(double roll)
        {
            var pool = PackageAnimations.CivilianVariants(new[] {Sheet("idle"), Sheet("IDLE_17"), Sheet("pray_2"), Sheet("play"), Sheet("emote_3"), Sheet("dance_99")}, roll);
            Assert.AreEqual(2, pool.Count);
            foreach (var sheet in pool) Assert.IsTrue(PackageAnimations.Matches("idle", sheet.Name));
        }

        [TestCase(0.5)]
        [TestCase(0.99999)]
        public void ActivityHalfIncludesOnlyMatchingFamiliesAndNumericSuffixes(double roll)
        {
            var pool = PackageAnimations.CivilianVariants(new[] {Sheet("idle"), Sheet("pray"), Sheet("play_23"), Sheet("EMOTE_8"), Sheet("dance_99"), Sheet("playful"), Sheet("dance_fast"), Sheet("pray_2_extra")}, roll);
            CollectionAssert.AreEquivalent(new[] {"pray", "play_23", "EMOTE_8", "dance_99"}, pool.ConvertAll(a => a.Name));
        }

        [Test]
        public void MissingActivitiesFallBackToIdle()
        {
            var pool = PackageAnimations.CivilianVariants(new[] {Sheet("idle_1"), Sheet("idle_2"), Sheet("walk")}, 0.75);
            CollectionAssert.AreEquivalent(new[] {"idle_1", "idle_2"}, pool.ConvertAll(a => a.Name));
        }

        [TestCase(1)]
        [TestCase(4)]
        public void FinitePauseChangesVariantsAtCycleEndsAndFinishesExactly(int cycles)
        {
            var loop = new PackageAnimationLoop(new[] {Sheet("idle_1"), Sheet("idle_2", 8)}, 8, () => 0, cycles);
            Assert.AreEqual("idle_1", loop.Current.Name);
            loop.Advance(0.49);
            Assert.AreEqual(0, loop.Cycles);
            loop.Advance(0.01);
            Assert.AreEqual(1, loop.Cycles);
            if (cycles > 1)
            {
                Assert.AreEqual("idle_2", loop.Current.Name);
                loop.Advance(1);
                Assert.AreEqual("idle_1", loop.Current.Name);
                loop.Advance(1.5);
            }
            Assert.IsTrue(loop.Finished);
            Assert.AreEqual(cycles, loop.Cycles);
            loop.Advance(100);
            Assert.AreEqual(cycles, loop.Cycles);
        }

        [Test]
        public void RoadSelectionKeepsHeadingButCanTurnAndHandlesBlockedTiles()
        {
            Assert.AreEqual(0, PackageMotion.CivilianDirection(new[] {4,4,0,0}, 0, 1, 1, 1));
            Assert.AreEqual(1, PackageMotion.CivilianDirection(new[] {1,4,0,0}, 0, 1, 1, 0));
            Assert.AreEqual(0, PackageMotion.CivilianDirection(new[] {1,4,0,0}, 0, 1, 0, 0));
            Assert.AreEqual(1, PackageMotion.CivilianDirection(new[] {4,4,0,0}, 0, 0, 1, 1));
            Assert.AreEqual(3, PackageMotion.CivilianDirection(new[] {0,0,0,2}, 0, 1, 1, 0));
            Assert.AreEqual(-1, PackageMotion.CivilianDirection(new[] {0,0,0,0}, -1, 1, 1, 0));
        }

        [Test]
        public void GridTargetsAreUsedAndBlockedCivilianTargetsCanBeRetried()
        {
            int picks = 0;
            var motion = new PackageMotion(Settings(), () => 0, current => { picks++; return new PackagePoint(4,0,0); });
            var origin = new PackagePoint(0,0,0);
            Assert.AreEqual(4, motion.Tick(origin, 0, 0).X);
            Assert.IsNull(motion.Tick(origin, 1, 10));
            Assert.IsFalse(motion.Failed);
            Assert.AreEqual(4, motion.Tick(origin, 2, 0).X);
            Assert.AreEqual(2, picks);
            Assert.IsNull(motion.Tick(new PackagePoint(4,0,0), 3, 0));
            var blocked = new PackageMotion(Settings(), () => 0, current => null);
            Assert.IsNull(blocked.Tick(origin, 0, 0));
            Assert.IsFalse(blocked.Failed);
        }

        [Test]
        public void WithoutGridUsesConfiguredHomeRelativeRadius()
        {
            var motion = new PackageMotion(Settings("{\"center\":[10,0,0],\"radius\":40}"), () => 0.25);
            var target = motion.Tick(new PackagePoint(0,0,0), 0, 0);
            Assert.AreEqual(10, target.X, 1e-6);
            Assert.AreEqual(20, target.Z, 1e-6);
        }
    }
}

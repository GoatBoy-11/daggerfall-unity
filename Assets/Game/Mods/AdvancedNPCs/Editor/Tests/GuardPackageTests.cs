using System;
using System.Collections.Generic;
using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class GuardPackageTests
    {
        static PackageSettings Settings(string json)
        {
            string error;
            AiPackage package = PackageParser.Parse("guard_spot", json, out error);
            Assert.IsNotNull(package, error);
            return PackageParser.Resolve(package, null, out error);
        }
        [Test]
        public void DefaultsAndOverridesDoNotMutateSharedPackage()
        {
            string error;
            AiPackage package = PackageParser.Parse("guard_spot", "{\"type\":\"guard_spot\"}", out error);
            PackageSettings defaults = PackageParser.Resolve(package, null, out error);
            Assert.IsNull(defaults.Position);
            Assert.AreEqual(6, defaults.IdleIntervalMin);
            Assert.AreEqual(15, defaults.IdleIntervalMax);
            var overrides = Json.Parse("{\"position\":[1234,2,-5678],\"idleInterval\":[2,4]}") as Dictionary<string, object>;
            var resolved = PackageParser.Resolve(package, overrides, out error);
            Assert.IsNotNull(resolved, error);
            Assert.AreEqual(1234, resolved.Position.X);
            Assert.AreEqual(-5678, resolved.Position.Z);
            Assert.AreEqual(2, resolved.IdleIntervalMin);
            Assert.AreEqual(4, resolved.IdleIntervalMax);
            Assert.AreEqual(0, package.Parameters.Count);
            Assert.IsNull(defaults.Position);
        }
        [TestCase("{\"position\":null}")]
        [TestCase("{\"position\":[1,2]}")]
        [TestCase("{\"position\":[1,2,1000001]}")]
        [TestCase("{\"position\":[1,2,\"3\"]}")]
        [TestCase("{\"idleInterval\":6}")]
        [TestCase("{\"idleInterval\":[6]}")]
        [TestCase("{\"idleInterval\":[0,15]}")]
        [TestCase("{\"idleInterval\":[15,6]}")]
        [TestCase("{\"idleInterval\":[6,3601]}")]
        [TestCase("{\"idleInterval\":[6,null]}")]
        [TestCase("{\"location\":{}}")]
        [TestCase("{\"animation\":\"idle\"}")]
        public void InvalidGuardParametersFail(string parameters)
        {
            string error;
            Assert.IsNull(PackageParser.Parse("guard_spot", "{\"type\":\"guard_spot\",\"parameters\":" + parameters + "}", out error));
            Assert.IsNotEmpty(error);
        }
        [Test]
        public void GuardReturnsToSamePostAndDetectsStalledMovement()
        {
            var settings = Settings("{\"type\":\"guard_spot\",\"parameters\":{\"position\":[1234,1,5000]}}");
            settings.Target = settings.Position;
            var motion = new PackageMotion(settings, () => 0);
            var post = new PackagePoint(1234,1,5000);
            Assert.IsNull(motion.Tick(post, 0, 0.1));
            Assert.AreSame(settings.Target, motion.Tick(new PackagePoint(1230,1,5000), 1, 0.1));
            Assert.IsNull(motion.Tick(post, 2, 0.1));
            Assert.AreSame(settings.Target, motion.Tick(new PackagePoint(1230,1,5000), 3, 0.1));
            motion.Tick(new PackagePoint(1230,1,5000), 4, 10);
            Assert.IsTrue(motion.Failed);
        }
        static SpriteAnimation Sheet(string name, int frames = 4) { return new SpriteAnimation { Name = name, Frames = frames }; }
        [Test]
        public void FrozenPosePrefersIdleOneThenBareThenLowestNumericSuffix()
        {
            Assert.AreEqual(2, GuardAnimationLoop.FrozenIndex(new[] {Sheet("idle_2"), Sheet("idle"), Sheet("IDLE_1")}));
            Assert.AreEqual(1, GuardAnimationLoop.FrozenIndex(new[] {Sheet("idle_2"), Sheet("idle")}));
            Assert.AreEqual(1, GuardAnimationLoop.FrozenIndex(new[] {Sheet("idle_10"), Sheet("idle_2"), Sheet("idle_20")}));
        }
        [TestCase(0, 6)]
        [TestCase(0.5, 10.5)]
        [TestCase(1, 15)]
        public void IntervalUsesRandomRangeAndStartsFrozen(double roll, double wait)
        {
            var loop = new GuardAnimationLoop(new[] {Sheet("idle_1")}, 8, 6, 15, () => roll);
            loop.Advance(wait - 0.001);
            Assert.IsFalse(loop.Playing);
            Assert.AreEqual(0, loop.Elapsed);
            loop.Advance(0.002);
            Assert.IsTrue(loop.Playing);
            Assert.AreEqual(0.001, loop.Elapsed, 1e-6);
        }
        [Test]
        public void PlaysAnyVariantOnceThenRestoresFrozenPose()
        {
            double[] rolls = {0,1,0}; int next = 0;
            var loop = new GuardAnimationLoop(new[] {Sheet("idle_1"), Sheet("idle_2", 6)}, 8, 1, 1, () => rolls[next++]);
            loop.Advance(1.25);
            Assert.AreEqual("idle_2", loop.Current.Name);
            Assert.AreEqual(0.25, loop.Elapsed);
            loop.Advance(0.5);
            Assert.IsFalse(loop.Playing);
            Assert.AreEqual("idle_1", loop.Current.Name);
            Assert.AreEqual(0, loop.Elapsed);
            Assert.AreEqual(1, loop.Cycles);
        }
        [Test]
        public void LongFramePreservesIntervalAndAnimationRemainder()
        {
            var loop = new GuardAnimationLoop(new[] {Sheet("idle")}, 8, 1, 1, () => 0);
            loop.Advance(4.25);
            Assert.AreEqual(2, loop.Cycles);
            Assert.IsTrue(loop.Playing);
            Assert.AreEqual(0.25, loop.Elapsed);
            loop.Advance(double.NaN); loop.Advance(double.PositiveInfinity); loop.Advance(-1);
            Assert.AreEqual(0.25, loop.Elapsed);
        }
        [Test]
        public void GuardNightScheduleEndsAtSix()
        {
            var schedule = new List<AiScheduleEntry> { new AiScheduleEntry {Package="guard_spot", From=1200, To=360} };
            Assert.IsNotNull(AiSchedule.Select(schedule, 1439));
            Assert.IsNotNull(AiSchedule.Select(schedule, 359));
            Assert.IsNull(AiSchedule.Select(schedule, 360));
            Assert.IsNotNull(AiSchedule.Select(schedule, 1200));
        }
    }
}

using System;
using System.Collections.Generic;
using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class PackageTests
    {
        static List<AiScheduleEntry> Schedule(string json)
        {
            string error;
            List<AiScheduleEntry> result = AiSchedule.Read(Json.Parse(json), out error);
            Assert.IsNull(error, error);
            return result;
        }

        static AiPackage Package(string json)
        {
            string error;
            AiPackage result = PackageParser.Parse("test", json, out error);
            Assert.IsNotNull(result, error);
            return result;
        }

        static PackageSettings Settings(string json)
        {
            string error;
            return PackageParser.Resolve(Package(json), null, out error);
        }

        [Test]
        public void NoSchedule_LeavesOldDefinitionAndAiAvailable()
        {
            ParseResult result = DefinitionParser.ParseFolder("citizen", "{}");
            Assert.IsTrue(result.Ok, result.Error);
            Assert.AreEqual(0, result.Definition.Schedule.Count);
            Assert.IsNull(AiSchedule.Select(result.Definition.Schedule, 500));
        }

        [Test]
        public void InvalidSchedule_DoesNotDiscardNpc()
        {
            ParseResult result = DefinitionParser.ParseFolder("citizen", "{\"wanderRadius\": 7, \"schedule\": 5}");
            Assert.IsTrue(result.Ok, result.Error);
            Assert.AreEqual(7, result.Definition.WanderRadius);
            Assert.AreEqual(0, result.Definition.Schedule.Count);
            Assert.That(result.Warnings.Exists(w => w.Contains("schedule") && w.Contains("current AI")));
        }

        [TestCase(479, "rest")]
        [TestCase(480, "work")]
        [TestCase(1079, "work")]
        [TestCase(1080, "rest")]
        public void TimedEntry_BeatsFallback_InEitherOrder(int minute, string expected)
        {
            foreach (string json in new[] {
                "[{\"package\":\"rest\"},{\"package\":\"work\",\"hours\":[\"08:00\",\"18:00\"]}]",
                "[{\"package\":\"work\",\"hours\":[\"08:00\",\"18:00\"]},{\"package\":\"rest\"}]" })
                Assert.AreEqual(expected, AiSchedule.Select(Schedule(json), minute).Package);
        }

        [TestCase(1379, false)]
        [TestCase(1380, true)]
        [TestCase(0, true)]
        [TestCase(419, true)]
        [TestCase(420, false)]
        public void Midnight_GapsUseCurrentAi(int minute, bool active)
        {
            var entries = Schedule("[{\"package\":\"night\",\"hours\":[\"23:00\",\"07:00\"]}]");
            Assert.AreEqual(active, AiSchedule.Select(entries, minute) != null);
        }

        [Test]
        public void AdjacentMinuteSlots_DoNotOverlap()
        {
            var entries = Schedule("[{\"package\":\"a\",\"hours\":[\"08:30\",\"09:15\"]},{\"package\":\"b\",\"hours\":[\"09:15\",\"10:00\"]}]");
            Assert.AreEqual("b", AiSchedule.Select(entries, 555).Package);
        }

        [TestCase("[{\"package\":\"a\"},{\"package\":\"b\"}]")]
        [TestCase("[{\"package\":\"a\",\"hours\":[\"23:00\",\"07:00\"]},{\"package\":\"b\",\"hours\":[\"06:59\",\"08:00\"]}]")]
        [TestCase("[{\"package\":\"a\",\"hours\":[\"08:00\",\"08:00\"]}]")]
        [TestCase("[{\"package\":\"a\",\"hours\":[\"24:00\",\"08:00\"]}]")]
        [TestCase("[{\"package\":\"a\",\"hours\":[\"8:00\",\"08:00\"]}]")]
        [TestCase("[{\"package\":\"a\",\"hours\":[\"08:60\",\"09:00\"]}]")]
        [TestCase("[{\"package\":\"a\",\"hours\":null}]")]
        [TestCase("[{\"package\":\"../a\"}]")]
        [TestCase("[{\"package\":\"a\",\"parameters\":null}]")]
        [TestCase("[{\"package\":\"a\",\"hour\":5}]")]
        [TestCase("[null]")]
        public void MalformedSchedule_FallsBackAsAWhole(string json)
        {
            string error;
            var result = AiSchedule.Read(Json.Parse(json), out error);
            Assert.AreEqual(0, result.Count);
            Assert.IsNotNull(error);
        }

        [Test]
        public void MidnightOccurrence_RemainsTheSameAcrossMidnight()
        {
            var entry = Schedule("[{\"package\":\"night\",\"hours\":[\"23:00\",\"07:00\"]}]")[0];
            Assert.AreEqual(AiSchedule.Occurrence(entry, 86400 + 23 * 3600), AiSchedule.Occurrence(entry, 2 * 86400 + 2 * 3600));
            Assert.AreNotEqual(AiSchedule.Occurrence(entry, 86400 + 23 * 3600), AiSchedule.Occurrence(entry, 2 * 86400 + 23 * 3600));
        }

        [Test]
        public void Overrides_AreIsolatedAndValidated()
        {
            var package = Package("{\"type\":\"wander\",\"parameters\":{\"radius\":8,\"pause\":5}}");
            string error;
            var custom = PackageParser.Resolve(package, (Dictionary<string, object>)Json.Parse("{\"radius\":2}"), out error);
            Assert.IsNull(error);
            Assert.AreEqual(2, custom.Radius);
            Assert.AreEqual(5, custom.Pause);
            Assert.AreEqual(8, PackageParser.Resolve(package, null, out error).Radius);
            Assert.AreEqual(8d, package.Parameters["radius"]);
            Assert.IsNull(PackageParser.Resolve(package, (Dictionary<string, object>)Json.Parse("{\"radius\":-1}"), out error));
            Assert.IsNotNull(error);
        }

        [TestCase("{\"type\":\"unknown\"}")]
        [TestCase("{\"type\":\"stand\",\"parameters\":{\"radius\":1}}")]
        [TestCase("{\"type\":\"wander\",\"parameters\":{\"radius\":null}}")]
        [TestCase("{\"type\":\"wander\",\"parameters\":{\"radius\":\"8\"}}")]
        [TestCase("{\"type\":\"wander\",\"parameters\":{\"run\":1}}")]
        [TestCase("{\"type\":\"wander\",\"parameters\":{\"center\":[0,0]}}")]
        [TestCase("{\"type\":\"wander\",\"parameters\":{\"center\":[101,0,0]}}")]
        [TestCase("{\"type\":\"travel_to\"}")]
        [TestCase("{\"type\":\"patrol\",\"parameters\":{\"points\":[[0,0,0]]}}")]
        [TestCase("{\"type\":\"stand\",\"type\":\"wander\"}")]
        [TestCase("{\"type\":\"stand\",\"paramters\":{}}")]
        [TestCase("[]")]
        [TestCase("{")]
        public void BadPackages_AreRejected(string json)
        {
            string error;
            Assert.IsNull(PackageParser.Parse("test", json, out error));
            Assert.IsNotNull(error);
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void NonFiniteOverride_Rejected(double radius)
        {
            string error;
            Assert.IsNull(PackageParser.Resolve(Package("{\"type\":\"wander\"}"),
                new Dictionary<string, object> { { "radius", radius } }, out error));
        }

        [Test]
        public void MissingPackage_ResolvesToFailure()
        {
            string error;
            Assert.IsNull(PackageParser.Resolve(null, null, out error));
            Assert.IsNotNull(error);
        }

        [Test]
        public void Stand_HoldsWithoutAMovementTarget()
        {
            var motion = new PackageMotion(Settings("{\"type\":\"stand\"}"), () => 0.5);
            Assert.IsNull(motion.Tick(new PackagePoint(2, 0, 2), 0, 1));
            Assert.IsFalse(motion.Failed);
        }

        [Test]
        public void Travel_HoldsAtTarget_AndReturnsAfterDisplacement()
        {
            var motion = new PackageMotion(Settings("{\"type\":\"travel_to\",\"parameters\":{\"target\":[5,0,0]}}"), () => 0);
            Assert.AreEqual(5, motion.Tick(new PackagePoint(0, 0, 0), 0, 0.1).X);
            Assert.IsNull(motion.Tick(new PackagePoint(5, 0, 0), 1, 0.1));
            Assert.AreEqual(5, motion.Tick(new PackagePoint(8, 0, 0), 2, 0.1).X);
        }

        [Test]
        public void UnreachableTarget_FailsAfterTenActiveSeconds()
        {
            var motion = new PackageMotion(Settings("{\"type\":\"travel_to\",\"parameters\":{\"target\":[5,0,0]}}"), () => 0);
            var origin = new PackagePoint(0, 0, 0);
            motion.Tick(origin, 0, 0);
            Assert.IsNotNull(motion.Tick(origin, 9, 9));
            Assert.IsNull(motion.Tick(origin, 10, 1));
            Assert.IsTrue(motion.Failed);
        }

        [Test]
        public void Progress_ResetsStallTimer_AndElapsedGameTimeAloneDoesNotFail()
        {
            var motion = new PackageMotion(Settings("{\"type\":\"travel_to\",\"parameters\":{\"target\":[50,0,0]}}"), () => 0);
            motion.Tick(new PackagePoint(0, 0, 0), 0, 0);
            motion.Tick(new PackagePoint(0, 0, 0), 36000, 0.1); // interruption doesn't tick active time
            for (int i = 1; i < 20; i++) motion.Tick(new PackagePoint(i, 0, 0), 36000 + i, 1);
            Assert.IsFalse(motion.Failed);
        }

        [Test]
        public void WrongFloor_DoesNotCountAsArrival()
        {
            var motion = new PackageMotion(Settings("{\"type\":\"travel_to\",\"parameters\":{\"target\":[0,10,0]}}"), () => 0);
            Assert.IsNotNull(motion.Tick(new PackagePoint(0, 0, 0), 0, 0));
        }

        [Test]
        public void Patrol_WaitsAndLoops_WithIndependentProgress()
        {
            var settings = Settings("{\"type\":\"patrol\",\"parameters\":{\"points\":[[0,0,0],[5,0,0]],\"pauseAtStops\":10}}");
            var a = new PackageMotion(settings, () => 0);
            var b = new PackageMotion(settings, () => 0);
            Assert.IsNull(a.Tick(new PackagePoint(0, 0, 0), 0, 0));
            Assert.AreEqual(1, a.PointIndex);
            Assert.AreEqual(0, b.PointIndex);
            Assert.IsNull(a.Tick(new PackagePoint(0, 0, 0), 9, 1));
            Assert.AreEqual(5, a.Tick(new PackagePoint(0, 0, 0), 10, 1).X);
            Assert.IsNull(a.Tick(new PackagePoint(5, 0, 0), 11, 1));
            Assert.AreEqual(0, a.PointIndex);
            Assert.AreEqual(0, a.Tick(new PackagePoint(5, 0, 0), 21, 1).X);
        }

        [Test]
        public void Wander_UsesItsOwnCenterAndRadius()
        {
            var motion = new PackageMotion(Settings("{\"type\":\"wander\",\"parameters\":{\"center\":[10,0,0],\"radius\":4}}"), () => 0.25);
            var target = motion.Tick(new PackagePoint(0, 0, 0), 0, 0);
            Assert.AreEqual(10, target.X, 0.001);
            Assert.AreEqual(2, target.Z, 0.001);
        }
    }
}

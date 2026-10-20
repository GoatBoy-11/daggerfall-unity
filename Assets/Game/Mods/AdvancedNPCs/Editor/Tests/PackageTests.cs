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
        public void ShelterDefaultsAndOverridesLeaveSharedPackageUnchanged()
        {
            var package = Package("{\"type\":\"seek_shelter\"}");
            string error;
            var defaults = PackageParser.Resolve(package, null, out error);
            Assert.AreEqual(16, defaults.Radius);
            CollectionAssert.AreEqual(new[] { "shelter" }, defaults.Spots);
            var overrides = (Dictionary<string, object>)Json.Parse("{\"radius\":12,\"spot\":[\"awning\",\"arch\"],\"run\":true}");
            var settings = PackageParser.Resolve(package, overrides, out error);
            Assert.AreEqual(12, settings.Radius);
            Assert.IsTrue(settings.Run);
            CollectionAssert.AreEqual(new[] { "awning", "arch" }, settings.Spots);
            Assert.AreEqual(16, PackageParser.Resolve(package, null, out error).Radius);
        }

        [TestCase("{\"radius\":0}")]
        [TestCase("{\"radius\":25}")]
        [TestCase("{\"radius\":\"16\"}")]
        [TestCase("{\"spot\":[]}")]
        [TestCase("{\"run\":1}")]
        [TestCase("{\"target\":[0,0,0]}")]
        public void ShelterRejectsInvalidParameters(string parameters)
        {
            string error;
            Assert.IsNull(PackageParser.Parse("shelter", "{\"type\":\"seek_shelter\",\"parameters\":" + parameters + "}", out error));
            Assert.IsNotEmpty(error);
        }

        [Test]
        public void ShelterMotionHoldsAndReturnsToTheSameReservedTarget()
        {
            var settings = Settings("{\"type\":\"seek_shelter\"}");
            settings.Target = new PackagePoint(4, 0, 0);
            var motion = new PackageMotion(settings, () => 0);
            Assert.AreEqual(4, motion.Tick(new PackagePoint(0, 0, 0), 0, 0).X);
            Assert.IsNull(motion.Tick(new PackagePoint(4, 0, 0), 1, 1));
            Assert.AreEqual(4, motion.Tick(new PackagePoint(6, 0, 0), 2, 1).X);
        }

        [Test]
        public void ScheduleDiagnosticsExplainPriorityConditionsAndHours()
        {
            var entries = Schedule("[{\"package\":\"fallback\"},{\"package\":\"normal\",\"hours\":[\"08:00\",\"18:00\"]},{\"package\":\"rain\",\"when\":{\"weather\":\"rain\"}},{\"package\":\"inside\",\"when\":{\"indoors\":true}}]");
            var facts = new FakeFacts();
            string text = AiSchedule.Describe(entries, 600, facts);
            StringAssert.Contains("fallback - eligible; overridden by slot 2", text);
            StringAssert.Contains("normal - selected", text);
            StringAssert.Contains("rain - condition unmet: weather", text);
            StringAssert.Contains("inside - condition unmet: indoors", text);
            facts.Rain = true; facts.Inside = true;
            text = AiSchedule.Describe(entries, 600, facts);
            StringAssert.Contains("rain - selected", text);
            StringAssert.Contains("inside - eligible; overridden by slot 3", text);
            facts.Rain = false; facts.Inside = false;
            text = AiSchedule.Describe(entries, 1080, facts);
            StringAssert.Contains("normal - outside hours", text);
            StringAssert.Contains("fallback - selected", text);
            Assert.AreSame(entries[0], AiSchedule.Select(entries, 1080, facts));
            StringAssert.Contains("No eligible entry", AiSchedule.Describe(Schedule("[{\"package\":\"night\",\"hours\":[\"23:00\",\"07:00\"]}]"), 420, facts));
            StringAssert.Contains("No schedule", AiSchedule.Describe(null, 0, facts));
            StringAssert.Contains("game facts unavailable", AiSchedule.Describe(entries, 600, null));
        }

        [TestCase("")]
        [TestCase("{")]
        [TestCase("[]")]
        [TestCase("{\"schedule\":[],\"schedule\":[]}")]
        [TestCase("{\"schedule\":5}")]
        [TestCase("{\"schedule\":[{\"package\":\"test\",\"hours\":[\"08:00\",\"18:00\"]},{\"package\":\"test\",\"hours\":[\"17:00\",\"19:00\"]}]}")]
        [TestCase("{\"schedule\":[{\"package\":\"missing\"}]}")]
        [TestCase("{\"schedule\":[{\"package\":\"test\",\"parameters\":{\"radius\":5}}]}")]
        [TestCase("{\"schedule\":[{\"package\":\"test\",\"when\":{\"variables\":{\"unknown\":true}}}]}")]
        [TestCase("{\"schedule\":[{\"package\":\"test\",\"when\":{\"weater\":\"rain\"}}]}")]
        public void InvalidScheduleReloadRetainsPreviousEntries(string json)
        {
            var definition = new NpcDefinition { Schedule = Schedule("[{\"package\":\"test\"}]") };
            var previous = definition.Schedule;
            var package = Package("{\"type\":\"stand\"}");
            string error;
            Assert.IsFalse(AiSchedule.TryReload(definition, json, id => id == "test" ? package : null, out error));
            Assert.IsNotEmpty(error);
            Assert.AreSame(previous, definition.Schedule);
        }

        [Test]
        public void ScheduleReloadReplacesOnlyScheduleAndSupportsRemoval()
        {
            var definition = new NpcDefinition { Name = "Original", WanderRadius = 7, Schedule = Schedule("[{\"package\":\"test\"}]") };
            var previous = definition.Schedule;
            var package = Package("{\"type\":\"stand\"}");
            string error;
            Assert.IsTrue(AiSchedule.TryReload(definition, "{\"name\":\"Edited\",\"wanderRadius\":2,\"schedule\":[{\"package\":\"test\",\"hours\":[\"23:00\",\"07:00\"]}]}", id => package, out error), error);
            Assert.AreNotSame(previous, definition.Schedule);
            Assert.AreEqual("Original", definition.Name);
            Assert.AreEqual(7, definition.WanderRadius);
            Assert.IsNotNull(AiSchedule.Select(definition.Schedule, 0));
            Assert.IsTrue(AiSchedule.TryReload(definition, "{}", id => package, out error), error);
            Assert.IsEmpty(definition.Schedule);
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
        public void StuckWanderPicksAnotherPointInsteadOfFailing()
        {
            double[] rolls = { 0, 1, 0.5, 1 };
            int next = 0;
            var motion = new PackageMotion(Settings("{\"type\":\"wander\",\"parameters\":{\"radius\":8,\"pause\":0}}"), () => rolls[next++ % rolls.Length]);
            var origin = new PackagePoint(0, 0, 0);
            PackagePoint first = motion.Tick(origin, 0, 0);
            Assert.IsNotNull(first);
            Assert.IsNull(motion.Tick(origin, 10, 10));
            Assert.IsFalse(motion.Failed);
            PackagePoint second = motion.Tick(origin, 11, 0);
            Assert.IsNotNull(second);
            Assert.AreNotEqual(first.X, second.X);
        }

        [Test]
        public void BlockedTargetRetriesTwiceAfterActiveDelayAndKeepsPatrolProgress()
        {
            var settings = Settings("{\"type\":\"patrol\",\"parameters\":{\"points\":[[0,0,0],[5,0,0]],\"pauseAtStops\":0}}");
            var motion = new PackageMotion(settings, () => 0);
            var origin = new PackagePoint(0, 0, 0);
            motion.Tick(origin, 0, 0);
            Assert.AreEqual(1, motion.PointIndex);
            motion.Tick(origin, 0, 0);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                Assert.IsNull(motion.Tick(origin, 0, 10));
                Assert.IsTrue(motion.Failed);
                Assert.IsTrue(motion.CanRetry);
                Assert.IsFalse(motion.Retry(0));
                Assert.IsFalse(motion.Retry(14));
                Assert.IsFalse(motion.Retry(-20));
                Assert.IsTrue(motion.Retry(1));
                Assert.AreEqual(attempt + 1, motion.RetryCount);
                Assert.AreEqual(1, motion.PointIndex);
                Assert.AreEqual(5, motion.Tick(origin, 99999, 0).X);
            }
            motion.Tick(origin, 99999, 10);
            Assert.IsTrue(motion.Failed);
            Assert.IsFalse(motion.CanRetry);
            Assert.IsFalse(motion.Retry(100));
            Assert.IsNull(motion.Tick(origin, 999999, 100));
        }

        [Test]
        public void TemporaryBlockCanRecoverAndArriveAtTheSameDestination()
        {
            var motion = new PackageMotion(Settings("{\"type\":\"travel_to\",\"parameters\":{\"target\":[5,0,0]}}"), () => 0);
            var origin = new PackagePoint(0, 0, 0);
            motion.Tick(origin, 0, 0);
            motion.Tick(origin, 10, 10);
            Assert.IsTrue(motion.Retry(15));
            Assert.AreEqual(5, motion.Tick(new PackagePoint(3, 0, 0), 25, 1).X);
            Assert.IsNull(motion.Tick(new PackagePoint(5, 0, 0), 26, 1));
            Assert.IsFalse(motion.Failed);
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

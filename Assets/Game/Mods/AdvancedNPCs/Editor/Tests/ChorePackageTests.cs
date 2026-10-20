using System.Collections.Generic;
using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class ChorePackageTests
    {
        static SpriteAnimation Sheet(string name) { return new SpriteAnimation { Name = name, CellWidth = 10, Frames = 4 }; }

        static PackageSettings Resolve(string parameters, out string error)
        {
            AiPackage package = PackageParser.Parse("chore", "{ \"type\": \"chore\"" + (parameters == null ? "" : ", \"parameters\": { " + parameters + " }") + " }", out error);
            return package == null ? null : PackageParser.Resolve(package, null, out error);
        }

        [Test]
        public void DefaultsCoverSweepChopScrubAndWork()
        {
            string error;
            PackageSettings s = Resolve(null, out error);
            Assert.IsNotNull(s, error);
            CollectionAssert.AreEqual(new[] { "sweep", "chop", "scrub", "work" }, s.ChoreAnimations);
            Assert.AreEqual(4, s.Radius);
            Assert.AreEqual(0, s.Pause);
            Assert.AreEqual(new[] { 2, 4 }, new[] { s.CyclesMin, s.CyclesMax });
        }

        [Test]
        public void ParametersOverride()
        {
            string error;
            PackageSettings one = Resolve("\"animation\": \"work\", \"radius\": 0, \"cycles\": [1, 1]", out error);
            Assert.IsNotNull(one, error);
            CollectionAssert.AreEqual(new[] { "work" }, one.ChoreAnimations);
            Assert.AreEqual(new[] { 1, 1 }, new[] { one.CyclesMin, one.CyclesMax });
            PackageSettings list = Resolve("\"animation\": [\"sweep\", \"dust_2\"], \"center\": [1, 0, -2]", out error);
            Assert.IsNotNull(list, error);
            CollectionAssert.AreEqual(new[] { "sweep", "dust_2" }, list.ChoreAnimations);
            foreach (string bad in new[] { "\"animation\": []", "\"animation\": \"sweep.png\"", "\"cycles\": [0, 2]", "\"radius\": 51", "\"pause\": 3" })
                Assert.IsNull(Resolve(bad, out error), bad);
        }

        [Test]
        public void StopsPlayChoreSheetsElseIdle()
        {
            var sheets = new[] { Sheet("idle_1"), Sheet("work_1"), Sheet("sweep"), Sheet("walk_1"), Sheet("sweeping_1") };
            var names = new List<string>();
            foreach (SpriteAnimation a in PackageAnimations.ChoreVariants(sheets, PackageAnimations.ChoreFamilies)) names.Add(a.Name);
            CollectionAssert.AreEquivalent(new[] { "work_1", "sweep" }, names);
            names.Clear();
            foreach (SpriteAnimation a in PackageAnimations.ChoreVariants(new[] { Sheet("idle"), Sheet("walk") }, PackageAnimations.ChoreFamilies)) names.Add(a.Name);
            CollectionAssert.AreEqual(new[] { "idle" }, names);
        }

        [Test]
        public void ChoreRetargetsWhenStuckAndNeverFails()
        {
            string error;
            PackageSettings s = Resolve("\"radius\": 5", out error);
            var motion = new PackageMotion(s, () => 0.5, null);
            PackagePoint here = new PackagePoint(0, 0, 0);
            Assert.IsNotNull(motion.Tick(here, 0, 0));
            for (int i = 0; i < 12; i++) motion.Tick(here, i, 1);
            Assert.IsFalse(motion.Failed);
        }
    }
}

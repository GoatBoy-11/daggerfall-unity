using System;
using System.Collections.Generic;
using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class MealAnimationTests
    {
        static SpriteAnimation Sheet(string name, int frames = 4) { return new SpriteAnimation { Name = name, CellWidth = 10, Frames = frames }; }

        /// <summary>Cycles in order: the family of each cycle as it starts (fps 4, 4-frame sheets: one second each).</summary>
        static List<string> Run(MealAnimationLoop loop, int cycles)
        {
            var families = new List<string> { loop.Family };
            for (int i = 1; i < cycles; i++)
            {
                loop.Advance(1.0);
                families.Add(loop.Family);
            }
            return families;
        }

        [Test]
        public void BoutsOfMealCyclesAlternateWithIdleBreaks()
        {
            var r = new Random(7);
            var loop = new MealAnimationLoop(new[] { Sheet("eat_1"), Sheet("drink"), Sheet("idle_1"), Sheet("walk_1") }, 4, 2, 2, 1, 1, r.NextDouble);
            List<string> seen = Run(loop, 12);
            // 2 meal cycles, 1 idle, repeated.
            for (int i = 0; i < seen.Count; i++)
                Assert.AreEqual(i % 3 == 2, seen[i] == MealAnimationLoop.IdleFamily, string.Join(",", seen.ToArray()));
            CollectionAssert.Contains(seen, MealAnimationLoop.EatFamily);
            CollectionAssert.Contains(seen, MealAnimationLoop.DrinkFamily);
            Assert.AreEqual(12, loop.Started);
        }

        [Test]
        public void OnlyOneFamilyPlaysAlone()
        {
            var loop = new MealAnimationLoop(new[] { Sheet("drink_1"), Sheet("drink_2"), Sheet("idle") }, 4, 3, 3, 1, 1, new Random(1).NextDouble);
            foreach (string f in Run(loop, 8)) Assert.AreNotEqual(MealAnimationLoop.EatFamily, f);
        }

        [Test]
        public void WithoutEatOrDrinkOnlyIdlePlays()
        {
            var loop = new MealAnimationLoop(new[] { Sheet("idle_1"), Sheet("idle_2") }, 4, 2, 5, 0, 0, new Random(3).NextDouble);
            foreach (string f in Run(loop, 10)) Assert.AreEqual(MealAnimationLoop.IdleFamily, f);
        }

        [Test]
        public void WithoutIdleOrWithZeroBreaksBoutsFollowEachOther()
        {
            var noIdle = new MealAnimationLoop(new[] { Sheet("eat") }, 4, 1, 2, 1, 3, new Random(5).NextDouble);
            foreach (string f in Run(noIdle, 10)) Assert.AreEqual(MealAnimationLoop.EatFamily, f);
            var noBreaks = new MealAnimationLoop(new[] { Sheet("eat"), Sheet("idle") }, 4, 1, 2, 0, 0, new Random(5).NextDouble);
            foreach (string f in Run(noBreaks, 10)) Assert.AreEqual(MealAnimationLoop.EatFamily, f);
        }

        [Test]
        public void SheetsWithoutUsableFamiliesAreRejected()
        {
            Assert.IsFalse(MealAnimationLoop.CanPlay(new[] { Sheet("walk_1"), Sheet("eating_1") }));
            Assert.IsTrue(MealAnimationLoop.CanPlay(new[] { Sheet("walk_1"), Sheet("idle") }));
            Assert.Throws<ArgumentException>(() => new MealAnimationLoop(new[] { Sheet("walk_1") }, 4, 1, 1, 1, 1, new Random(1).NextDouble));
        }

        [Test]
        public void PackageParsesCycleRanges()
        {
            string error;
            AiPackage package = PackageParser.Parse("eat", "{ \"type\": \"eat\", \"parameters\": { \"mealCycles\": [3, 6], \"breakCycles\": [0, 2] } }", out error);
            Assert.IsNotNull(package, error);
            PackageSettings s = PackageParser.Resolve(package, null, out error);
            Assert.AreEqual(new[] { 3, 6, 0, 2 }, new[] { s.MealMin, s.MealMax, s.BreakMin, s.BreakMax });
            PackageSettings defaults = PackageParser.Resolve(PackageParser.Parse("eat", "{ \"type\": \"eat\" }", out error), null, out error);
            Assert.AreEqual(new[] { 2, 5, 1, 3 }, new[] { defaults.MealMin, defaults.MealMax, defaults.BreakMin, defaults.BreakMax });
            foreach (string bad in new[] { "\"mealCycles\": [0, 2]", "\"mealCycles\": [4, 2]", "\"breakCycles\": [1.5, 2]", "\"breakCycles\": 2", "\"animation\": \"eat\"" })
                Assert.IsNull(PackageParser.Parse("eat", "{ \"type\": \"eat\", \"parameters\": { " + bad + " } }", out error), bad);
        }

        [Test]
        public void EatAndDrinkAreSoundPools()
        {
            Assert.AreEqual(SoundPools.Eat, SoundPools.PoolOf("eat_3"));
            Assert.AreEqual(SoundPools.Drink, SoundPools.PoolOf("drink"));
            Assert.IsNull(SoundPools.PoolOf("eating_1"));
        }
    }
}

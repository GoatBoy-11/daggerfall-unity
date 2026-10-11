using System;
using System.Collections.Generic;
using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class SpriteStatesTests
    {
        static SpriteSet Set(params string[] sheets)
        {
            SpriteSet s = new SpriteSet();
            foreach (string name in sheets)
            {
                SpriteAnimation a = new SpriteAnimation();
                a.Name = name;
                a.CellWidth = 16;
                a.Frames = 2;
                s.Animations[name] = a;
            }
            return s;
        }

        static List<string> Names(List<SpriteAnimation> list)
        {
            List<string> names = new List<string>();
            foreach (SpriteAnimation a in list)
                names.Add(a.Name);
            return names;
        }

        [TestCase("idle", "idle")]
        [TestCase("idle_2", "idle")]
        [TestCase("Walk_1", "walk")]
        [TestCase("hit_1", "hit")]
        [TestCase("attack_10", "attack")]
        [TestCase("death", "death")]
        [TestCase("cast", "cast")]
        [TestCase("Cast_1", "cast")]
        [TestCase("attack_range", "attack_range")]
        [TestCase("attack_range_1", "attack_range")]
        [TestCase("Attack_Range_12", "attack_range")]
        [TestCase("attack_range_x", null)]
        [TestCase("attack_ranged_1", null)]
        [TestCase("run", "run")]
        [TestCase("Run_2", "run")]
        [TestCase("greet", "greet")]
        [TestCase("greet_1", "greet")]
        [TestCase("talk_1", null)]
        [TestCase("pray_1", null)]
        [TestCase("idle_x", null)]
        [TestCase("death_static", null)]
        [TestCase("deathstatic", null)]
        [TestCase("dance_1", null)]
        public void StateOf(string sheet, string expected)
        {
            Assert.AreEqual(expected, SpriteStates.StateOf(sheet));
        }

        [Test]
        public void Variants_AreSortedPerState()
        {
            SpriteSet s = Set("idle_2", "idle_1", "walk_1", "attack_1", "attack_2");
            CollectionAssert.AreEqual(new[] { "idle_1", "idle_2" }, Names(SpriteStates.Variants(s, SpriteStates.Idle)));
            CollectionAssert.AreEqual(new[] { "attack_1", "attack_2" }, Names(SpriteStates.Variants(s, SpriteStates.Attack)));
        }

        [Test]
        public void Fallbacks()
        {
            SpriteSet s = Set("idle_1");
            CollectionAssert.AreEqual(new[] { "idle_1" }, Names(SpriteStates.Variants(s, SpriteStates.Walk)));
            CollectionAssert.AreEqual(new[] { "idle_1" }, Names(SpriteStates.Variants(s, SpriteStates.Hit)));
            CollectionAssert.AreEqual(new[] { "idle_1" }, Names(SpriteStates.Variants(s, SpriteStates.Attack)));
            Assert.AreEqual(0, SpriteStates.Variants(s, SpriteStates.Death).Count);
            // No cast and no attack sheet: the spell is released without an animation.
            Assert.AreEqual(0, SpriteStates.Variants(s, SpriteStates.Cast).Count);
        }

        [Test]
        public void Casting_PrefersDedicatedSheetThenFallsBackToAttack()
        {
            CollectionAssert.AreEqual(new[] { "cast_1" }, Names(SpriteStates.Variants(Set("idle_1", "attack_1", "cast_1"), SpriteStates.Cast)));
            CollectionAssert.AreEqual(new[] { "attack_1", "attack_2" }, Names(SpriteStates.Variants(Set("idle_1", "attack_2", "attack_1"), SpriteStates.Cast)));
        }

        [Test]
        public void RangedAttack_PrefersDedicatedSheetThenFallsBackToAttackThenIdle()
        {
            SpriteSet both = Set("idle_1", "attack_1", "attack_range_1");
            CollectionAssert.AreEqual(new[] { "attack_range_1" }, Names(SpriteStates.Variants(both, SpriteStates.RangedAttack)));
            // Melee swings never pick the bow sheet.
            CollectionAssert.AreEqual(new[] { "attack_1" }, Names(SpriteStates.Variants(both, SpriteStates.Attack)));
            CollectionAssert.AreEqual(new[] { "attack_1" }, Names(SpriteStates.Variants(Set("idle_1", "attack_1"), SpriteStates.RangedAttack)));
            CollectionAssert.AreEqual(new[] { "idle_1" }, Names(SpriteStates.Variants(Set("idle_1"), SpriteStates.RangedAttack)));
        }

        [Test]
        public void RowDirection_IsTheCentreOfItsRow()
        {
            float[][] forwards = { new[] { 0f, 1f }, new[] { 1f, 0f }, new[] { -0.6f, 0.8f } };
            foreach (float[] f in forwards)
            {
                for (int row = 0; row < 8; row++)
                {
                    float x, z;
                    SpriteDirections.RowDirection(row, f[0], f[1], out x, out z);
                    Assert.AreEqual(1.0, Math.Sqrt(x * x + z * z), 1e-5);
                    Assert.AreEqual(row, SpriteDirections.Row(x, z, f[0], f[1]));
                }
            }
            // Row 2 (camera on the NPC's right): facing +Z, the NPC's right is +X.
            float rx, rz;
            SpriteDirections.RowDirection(2, 0f, 1f, out rx, out rz);
            Assert.AreEqual(1f, rx, 1e-5);
            Assert.AreEqual(0f, rz, 1e-5);
        }

        [Test]
        public void Run_FallsBackToWalkThenIdle()
        {
            CollectionAssert.AreEqual(new[] { "run" }, Names(SpriteStates.Variants(Set("idle_1", "walk", "run"), SpriteStates.Run)));
            CollectionAssert.AreEqual(new[] { "walk" }, Names(SpriteStates.Variants(Set("idle_1", "walk"), SpriteStates.Run)));
            CollectionAssert.AreEqual(new[] { "idle_1" }, Names(SpriteStates.Variants(Set("idle_1"), SpriteStates.Run)));
        }

        [Test]
        public void Walk_FallsBackToRun()
        {
            CollectionAssert.AreEqual(new[] { "run" }, Names(SpriteStates.Variants(Set("idle_1", "run"), SpriteStates.Walk)));
            CollectionAssert.AreEqual(new[] { "walk" }, Names(SpriteStates.Variants(Set("idle_1", "walk", "run"), SpriteStates.Walk)));
        }

        [Test]
        public void Greet_HasNoFallback()
        {
            Assert.AreEqual(0, SpriteStates.Variants(Set("idle_1", "walk", "attack_1"), SpriteStates.Greet).Count);
            CollectionAssert.AreEqual(new[] { "greet_1" }, Names(SpriteStates.Variants(Set("idle_1", "greet_1"), SpriteStates.Greet)));
        }

        [Test]
        public void IsStationary_WhenNoWalkAndNoRun()
        {
            Assert.IsTrue(SpriteStates.IsStationary(Set("idle_1", "attack_1", "death_1")));
            Assert.IsFalse(SpriteStates.IsStationary(Set("idle_1", "walk")));
            Assert.IsFalse(SpriteStates.IsStationary(Set("idle_1", "run")));
        }

        [Test]
        public void IsValid_NeedsIdle()
        {
            Assert.IsTrue(SpriteStates.IsValid(Set("idle")));
            Assert.IsFalse(SpriteStates.IsValid(Set("walk_1", "death")));
        }

        [Test]
        public void PickSet_RepeatableAndSpread()
        {
            Assert.AreEqual(0, SpriteStates.PickSet(1, 123u));
            Assert.AreEqual(SpriteStates.PickSet(3, 77u), SpriteStates.PickSet(3, 77u));
            HashSet<int> seen = new HashSet<int>();
            for (uint seed = 1; seed < 40; seed++)
            {
                int i = SpriteStates.PickSet(3, StableHash.Of("wench@1#" + seed));
                Assert.IsTrue(i >= 0 && i < 3);
                seen.Add(i);
            }
            Assert.AreEqual(3, seen.Count);
        }

        // NPC faces +Z; the camera direction is given relative to the NPC.
        [TestCase(0f, 1f, 0)]
        [TestCase(1f, 1f, 1)]
        [TestCase(1f, 0f, 2)]
        [TestCase(1f, -1f, 3)]
        [TestCase(0f, -1f, 4)]
        [TestCase(-1f, -1f, 5)]
        [TestCase(-1f, 0f, 6)]
        [TestCase(-1f, 1f, 7)]
        public void Directions_AllSectors(float camX, float camZ, int row)
        {
            Assert.AreEqual(row, SpriteDirections.Row(camX, camZ, 0f, 1f));
        }

        [Test]
        public void Directions_FollowTheNpcsFacing()
        {
            // NPC faces +X (its right is -Z); camera at +X is in front of it.
            Assert.AreEqual(0, SpriteDirections.Row(1f, 0f, 1f, 0f));
            Assert.AreEqual(2, SpriteDirections.Row(0f, -1f, 1f, 0f));
        }

        [Test]
        public void Directions_BoundaryIsStable()
        {
            float a = (float)(22.5 * Math.PI / 180);
            int onBoundary = SpriteDirections.Row((float)Math.Sin(a), (float)Math.Cos(a), 0f, 1f);
            Assert.IsTrue(onBoundary == 0 || onBoundary == 1);
            Assert.AreEqual(onBoundary, SpriteDirections.Row((float)Math.Sin(a), (float)Math.Cos(a), 0f, 1f));
            float below = (float)(22.0 * Math.PI / 180);
            Assert.AreEqual(0, SpriteDirections.Row((float)Math.Sin(below), (float)Math.Cos(below), 0f, 1f));
            float back = (float)(179.0 * Math.PI / 180);
            Assert.AreEqual(4, SpriteDirections.Row((float)Math.Sin(back), (float)Math.Cos(back), 0f, 1f));
            Assert.AreEqual(4, SpriteDirections.Row(-(float)Math.Sin(back), (float)Math.Cos(back), 0f, 1f));
        }

        [Test]
        public void Directions_ZeroVectors_AreFront()
        {
            Assert.AreEqual(0, SpriteDirections.Row(0f, 0f, 0f, 1f));
            Assert.AreEqual(0, SpriteDirections.Row(1f, 0f, 0f, 0f));
        }
    }
}

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

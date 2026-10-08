using System;
using System.Collections.Generic;
using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class SpotPickerTests
    {
        static List<float[]> Line(int n)
        {
            List<float[]> points = new List<float[]>();
            for (int i = 0; i < n; i++)
                points.Add(new float[] { i * 5f, 0f, 0f });
            return points;
        }

        [Test]
        public void Pick_AvoidsEntrance_Distinct_Repeatable()
        {
            List<float[]> points = Line(20);
            List<int> picks = SpotPicker.Pick(points, new float[] { 0, 0, 0 }, 20f, 5, 42);
            Assert.AreEqual(5, picks.Count);
            CollectionAssert.AllItemsAreUnique(picks);
            foreach (int i in picks)
                Assert.GreaterOrEqual(points[i][0], 20f);
            CollectionAssert.AreEqual(picks, SpotPicker.Pick(points, new float[] { 0, 0, 0 }, 20f, 5, 42));
            Assert.AreNotEqual(string.Join(",", picks), string.Join(",", SpotPicker.Pick(points, new float[] { 0, 0, 0 }, 20f, 5, 43)));
        }

        [Test]
        public void Pick_TooFewSpots_RestMinusOne()
        {
            List<int> picks = SpotPicker.Pick(Line(6), new float[] { 0, 0, 0 }, 20f, 4, 1);
            CollectionAssert.AreEquivalent(new[] { 4, 5, -1, -1 }, picks);
            CollectionAssert.AreEqual(new[] { -1 }, SpotPicker.Pick(new List<float[]>(), null, 0, 1, 1));
        }

        [Test]
        public void Pick_NoAvoid_UsesAll()
        {
            Assert.AreEqual(3, SpotPicker.Pick(Line(3), null, 50f, 3, 9).FindAll(delegate (int i) { return i >= 0; }).Count);
        }

        [Test]
        public void GroupOffsets_FirstAtSpot_RestTwoToThreeMetres_Spaced()
        {
            for (uint seed = 0; seed < 50; seed++)
            {
                List<float[]> offsets = SpotPicker.GroupOffsets(4, seed);
                Assert.AreEqual(4, offsets.Count);
                Assert.AreEqual(0f, offsets[0][0]);
                Assert.AreEqual(0f, offsets[0][1]);
                for (int i = 1; i < offsets.Count; i++)
                {
                    double r = Math.Sqrt(offsets[i][0] * offsets[i][0] + offsets[i][1] * offsets[i][1]);
                    Assert.That(r, Is.InRange(2.0, 3.0));
                    for (int j = 0; j < i; j++)
                    {
                        double dx = offsets[i][0] - offsets[j][0], dz = offsets[i][1] - offsets[j][1];
                        Assert.GreaterOrEqual(Math.Sqrt(dx * dx + dz * dz), 1.0, "seed " + seed);
                    }
                }
            }
        }

        [Test]
        public void Sort_IgnoresFloatJitterBelowOneCentimetre()
        {
            // Same marker grid x, read back through a transform round trip: 12.00001 vs 11.99999.
            List<float[]> visit1 = new List<float[]> { new float[] { 12.00001f, 0, 5 }, new float[] { 11.99999f, 0, 9 } };
            List<float[]> visit2 = new List<float[]> { new float[] { 11.99999f, 0, 5 }, new float[] { 12.00001f, 0, 9 } };
            SpotPicker.Sort(visit1);
            SpotPicker.Sort(visit2);
            Assert.AreEqual(5f, visit1[0][2]);
            Assert.AreEqual(5f, visit2[0][2]);
        }

        [Test]
        public void Sort_StableOrder()
        {
            List<float[]> points = new List<float[]> { new float[] { 2, 0, 1 }, new float[] { 1, 5, 0 }, new float[] { 1, 0, 3 } };
            SpotPicker.Sort(points);
            Assert.AreEqual(new float[] { 1, 0, 3 }, points[0]);
            Assert.AreEqual(new float[] { 1, 5, 0 }, points[1]);
            Assert.AreEqual(new float[] { 2, 0, 1 }, points[2]);
        }
    }

    public class EncounterRulesTests
    {
        [Test]
        public void RollClock_ResetsWhenTimeWentBack()
        {
            Assert.AreEqual(50UL, EncounterRules.RollClock(1000UL, 50UL), "an earlier save was loaded");
            Assert.AreEqual(1000UL, EncounterRules.RollClock(1000UL, 1005UL));
        }

        [TestCase(0UL, 9UL, 0)]
        [TestCase(0UL, 10UL, 1)]
        [TestCase(0UL, 600UL, 1)]
        public void DueRolls_OneStepAtMost(ulong last, ulong now, int expected)
        {
            Assert.AreEqual(expected, EncounterRules.DueRolls(last, now));
        }

        [Test]
        public void RingPoint_DistanceAndOutOfView()
        {
            Random rng = new Random(5);
            for (int i = 0; i < 500; i++)
            {
                float[] p = EncounterRules.RingPoint(100f, 200f, 0f, 1f, rng.NextDouble(), rng.NextDouble());
                double dx = p[0] - 100, dz = p[1] - 200;
                double r = Math.Sqrt(dx * dx + dz * dz);
                Assert.That(r, Is.InRange(40.0, 80.0));
                Assert.IsTrue(EncounterRules.OutOfView(100f, 200f, 0f, 1f, p[0], p[1]), "point " + p[0] + "," + p[1]);
            }
        }

        [Test]
        public void OutOfView_AheadIsInView()
        {
            Assert.IsFalse(EncounterRules.OutOfView(0, 0, 0, 1, 0, 50));
            Assert.IsFalse(EncounterRules.OutOfView(0, 0, 0, 1, 10, 50));
            Assert.IsTrue(EncounterRules.OutOfView(0, 0, 0, 1, 50, 0));
            Assert.IsTrue(EncounterRules.OutOfView(0, 0, 0, 1, 0, -50));
        }

        [TestCase(false, true, true)]
        [TestCase(true, true, false)]
        [TestCase(false, false, false)]
        public void MayRoll_OnlyInWildernessOnHud(bool inLocation, bool onHud, bool expected)
        {
            Assert.AreEqual(expected, EncounterRules.MayRoll(inLocation, onHud));
        }

        [TestCase(0, 2, 0, 4, true)]
        [TestCase(2, 2, 2, 4, false)]
        [TestCase(1, 2, 4, 4, false)]
        [TestCase(0, 1, 0, 0, false)]
        public void CanSpawn_Caps(int ofTemplate, int max, int total, int cap, bool expected)
        {
            Assert.AreEqual(expected, EncounterRules.CanSpawn(ofTemplate, max, total, cap));
        }

        [TestCase(151f, false, true)]
        [TestCase(151f, true, false)]
        [TestCase(100f, false, false)]
        public void ShouldDespawn_FarAndUnseen(float distance, bool inView, bool expected)
        {
            Assert.AreEqual(expected, EncounterRules.ShouldDespawn(distance, inView));
        }
    }
}

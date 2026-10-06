using System.Collections.Generic;
using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class SeedingTests
    {
        // FNV-1a 32 reference vectors; these values must never change (they key generic people in saves).
        [TestCase("", 2166136261u)]
        [TestCase("a", 3826002220u)]
        [TestCase("foobar", 3214735720u)]
        [TestCase("bram", 1802036227u)]
        [TestCase("commoner@1234#2", 3591463060u)]
        public void StableHash_KnownVectors(string text, uint expected)
        {
            Assert.AreEqual(expected, StableHash.Of(text));
        }

        [Test]
        public void StableHash_Null_HashesLikeEmpty()
        {
            Assert.AreEqual(StableHash.Of(""), StableHash.Of(null));
        }

        [Test]
        public void SeededRandom_Seed1_MatchesXorshift32()
        {
            SeededRandom r = new SeededRandom(1);
            Assert.AreEqual(270369u, r.NextUInt());
            Assert.AreEqual(67634689u, r.NextUInt());
            Assert.AreEqual(2647435461u, r.NextUInt());
        }

        [Test]
        public void SeededRandom_SeedZero_UsesFixedNonZeroSeed()
        {
            Assert.AreEqual(1359758873u, new SeededRandom(0).NextUInt());
        }

        [Test]
        public void SeededRandom_SameSeed_SameSequence()
        {
            SeededRandom a = new SeededRandom(77);
            SeededRandom b = new SeededRandom(77);
            for (int i = 0; i < 20; i++)
                Assert.AreEqual(a.NextUInt(), b.NextUInt());
        }

        [Test]
        public void SeededRandom_Next_StaysInRange()
        {
            SeededRandom r = new SeededRandom(42);
            for (int i = 0; i < 1000; i++)
            {
                int v = r.Next(7);
                Assert.IsTrue(v >= 0 && v < 7, "got " + v);
            }
            Assert.AreEqual(0, r.Next(0));
            Assert.AreEqual(0, r.Next(-3));
        }

        [Test]
        public void SeededRandom_Range_IsInclusive()
        {
            SeededRandom r = new SeededRandom(5);
            HashSet<int> seen = new HashSet<int>();
            for (int i = 0; i < 300; i++)
            {
                int v = r.Range(1, 3);
                Assert.IsTrue(v >= 1 && v <= 3, "got " + v);
                seen.Add(v);
            }
            CollectionAssert.AreEquivalent(new[] { 1, 2, 3 }, seen);
            Assert.AreEqual(5, r.Range(5, 5));
            Assert.AreEqual(5, r.Range(5, 2));
        }

        [Test]
        public void SeededRandom_Pick_UsesNext()
        {
            // First draw for seed 1 is 270369, and 270369 % 3 == 0.
            Assert.AreEqual("a", new SeededRandom(1).Pick(new[] { "a", "b", "c" }));
        }

        [TestCase("Breton", "Male", 0, 0, 192)]
        [TestCase("Breton", "Female", 2, 3, 27)]
        [TestCase("Redguard", "Male", 1, 23, 335)]
        [TestCase("Nord", "Female", 2, 5, 53)]
        [TestCase("Khajiit", "Male", 0, 0, 192)]
        [TestCase("Nord", "Male", 9, 99, 215)]
        public void VanillaFaces_FaceRecord(string race, string gender, int outfit, int face, int expected)
        {
            Assert.AreEqual(expected, VanillaFaces.FaceRecord(race, gender, outfit, face));
        }
    }
}

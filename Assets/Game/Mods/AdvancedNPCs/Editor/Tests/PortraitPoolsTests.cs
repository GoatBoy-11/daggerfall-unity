using System.Collections.Generic;
using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class PortraitPoolsTests
    {
        static List<string> Pool(string baseName, params string[] files)
        {
            return PortraitPools.Pool(new List<string>(files), baseName);
        }

        [Test]
        public void SingleUnnumbered()
        {
            CollectionAssert.AreEqual(new[] { "bram" }, Pool("bram", "bram", "cora"));
        }

        [Test]
        public void NumberedSet()
        {
            CollectionAssert.AreEqual(new[] { "bram_1", "bram_2", "bram_3" }, Pool("bram", "bram_3", "bram_1", "bram_2"));
        }

        [Test]
        public void OnlyNumberOne_IsUsed()
        {
            CollectionAssert.AreEqual(new[] { "bram_1" }, Pool("bram", "bram_1"));
        }

        [Test]
        public void UnnumberedAndNumbered_Both()
        {
            CollectionAssert.AreEqual(new[] { "bram", "bram_1" }, Pool("bram", "bram_1", "bram"));
        }

        [Test]
        public void SimilarNames_AreNotInThePool()
        {
            CollectionAssert.AreEqual(new string[0], Pool("bram", "bramble", "bram_x", "bram_", "bram_1a"));
        }

        [Test]
        public void Joined_IsDistinctAndSorted()
        {
            List<string> files = new List<string> { "a", "a_2", "b_1" };
            CollectionAssert.AreEqual(new[] { "a", "a_2", "b_1" }, PortraitPools.Joined(files, new[] { "b", "a", "a" }));
        }

        [Test]
        public void Choose_IsStablePerSeedAndInPool()
        {
            List<string> pool = new List<string> { "bram_1", "bram_2", "bram_3" };
            string first = PortraitPools.Choose(pool, 12345u);
            Assert.AreEqual(first, PortraitPools.Choose(pool, 12345u));
            CollectionAssert.Contains(pool, first);
            Assert.IsNull(PortraitPools.Choose(new List<string>(), 1u));
        }

        [Test]
        public void Choose_SpreadsOverThePool()
        {
            List<string> pool = new List<string> { "bram_1", "bram_2", "bram_3" };
            HashSet<string> seen = new HashSet<string>();
            for (int n = 0; n < 30; n++)
                seen.Add(PortraitPools.Choose(pool, StableHash.Of("cooper@1234#" + n)));
            Assert.AreEqual(3, seen.Count);
        }

        [Test]
        public void StateWithLockedPortrait_IsSaved()
        {
            NpcStateTable t = new NpcStateTable();
            t.GetOrCreate("cooper@1234#0").portrait = "bram_2";
            NpcState copy = t.Snapshot()["cooper@1234#0"];
            Assert.AreEqual("bram_2", copy.portrait);
        }

        [Test]
        public void RandomEachVisit_PortraitFollowsThePersonsSeed()
        {
            // Same slot key, different visits: the face must re-roll with the person (spec v2.1 §6.2).
            NpcDefinition t = new NpcDefinition();
            t.Id = "cooper";
            t.Kind = NpcKind.Generic;
            t.Gender = "";
            t.Spawn = new GenericSpawn();
            t.Spawn.CountMin = 1;
            t.Spawn.CountMax = 1;
            List<string> pool = new List<string> { "bram_1", "bram_2", "bram_3" };
            HashSet<string> faces = new HashSet<string>();
            for (uint visit = 1; visit <= 30; visit++)
            {
                List<NpcInstance> plan = PopulationPlanner.Plan(new[] { t }, new TownInfo(1234, "R", "P", "TownCity", "Breton"),
                    GenericMode.RandomEachVisit, 12, 10, visit, new FixedNames());
                faces.Add(PortraitPools.Choose(pool, plan[0].Seed));
            }
            Assert.AreEqual(3, faces.Count);
        }

        class FixedNames : INameSource
        {
            public string Generate(string listName, string race, string gender, uint seed)
            {
                return "N";
            }
        }
    }
}

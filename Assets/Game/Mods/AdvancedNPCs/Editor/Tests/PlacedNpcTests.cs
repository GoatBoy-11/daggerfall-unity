using System.Collections.Generic;
using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class PlacedNpcTests
    {
        class FakeNames : INameSource
        {
            public string Generate(string listName, string race, string gender, uint seed)
            {
                return race + " " + gender + " " + seed;
            }
        }

        static NpcDefinition Template(string id)
        {
            NpcDefinition d = new NpcDefinition();
            d.Id = id;
            d.Folder = id;
            d.Kind = NpcKind.Generic;
            d.Name = "";
            d.Gender = "";
            d.BaseClass = "Bard";
            d.Spawn = new GenericSpawn();
            d.Spawn.CountMin = 1;
            d.Spawn.CountMax = 1;
            return d;
        }

        [Test]
        public void Add_NumbersPeoplePerTemplateAndTown()
        {
            PlacedNpcList list = new PlacedNpcList();
            PlacedNpc a = list.Add("wench", 7, "Daggerfall", "Daggerfall", 1f, 2f, 3f);
            PlacedNpc b = list.Add("wench", 7, "Daggerfall", "Daggerfall", 4f, 5f, 6f);
            PlacedNpc other = list.Add("wench", 8, "Daggerfall", "Ashfield", 0f, 0f, 0f);
            PlacedNpc smith = list.Add("smith", 7, "Daggerfall", "Daggerfall", 0f, 0f, 0f);
            Assert.AreEqual("wench@7+1", a.Key());
            Assert.AreEqual("wench@7+2", b.Key());
            Assert.AreEqual("wench@8+1", other.Key());
            Assert.AreEqual("smith@7+1", smith.Key());
            Assert.AreEqual(4f, b.x);
            Assert.AreEqual(3, list.ForTown(7).Count);
            Assert.AreEqual(1, list.ForTown(8).Count);
        }

        [Test]
        public void Remove_NumbersNeverCollide()
        {
            PlacedNpcList list = new PlacedNpcList();
            list.Add("wench", 7, "R", "P", 0f, 0f, 0f);
            PlacedNpc second = list.Add("wench", 7, "R", "P", 0f, 0f, 0f);
            PlacedNpc third = list.Add("wench", 7, "R", "P", 0f, 0f, 0f);
            Assert.IsTrue(list.Remove(second.Key()));
            Assert.IsFalse(list.Remove(second.Key()));
            Assert.IsNull(list.Find(second.Key()));
            Assert.AreSame(third, list.Find(third.Key()));
            Assert.AreEqual("wench@7+4", list.Add("wench", 7, "R", "P", 0f, 0f, 0f).Key());
        }

        [Test]
        public void SnapshotAndRestore_AreCopies()
        {
            PlacedNpcList list = new PlacedNpcList();
            list.Add("wench", 7, "R", "P", 1f, 2f, 3f);
            List<PlacedNpc> saved = list.Snapshot();
            saved[0].x = 99f;
            Assert.AreEqual(1f, list.ForTown(7)[0].x);

            PlacedNpcList back = new PlacedNpcList();
            back.Restore(saved);
            saved[0].x = 50f;
            Assert.AreEqual(99f, back.ForTown(7)[0].x);
            Assert.AreEqual("wench@7+2", back.Add("wench", 7, "R", "P", 0f, 0f, 0f).Key());

            back.Restore(null);
            Assert.AreEqual(0, back.ForTown(7).Count);
            List<PlacedNpc> withNull = new List<PlacedNpc>();
            withNull.Add(null);
            back.Restore(withNull);
            Assert.AreEqual(0, back.Snapshot().Count);
        }

        [Test]
        public void ForPlaced_IsAFixedPersistentPersonRolledFromItsKey()
        {
            PlacedNpcList list = new PlacedNpcList();
            PlacedNpc p = list.Add("wench", 7, "R", "P", 1f, 2f, 3f);
            NpcDefinition t = Template("wench");
            t.Portraits.Add("wench_unique");
            NpcInstance i = PopulationPlanner.ForPlaced(t, p, "Breton", new FakeNames());
            NpcInstance again = PopulationPlanner.ForPlaced(t, p, "Breton", new FakeNames());

            Assert.AreEqual("wench@7+1", i.Key);
            Assert.AreEqual(StableHash.Of("wench@7+1"), i.Seed);
            Assert.IsTrue(i.Persistent);
            Assert.IsTrue(i.HasFixedPosition);
            Assert.AreEqual(new[] { 1f, 2f, 3f }, new[] { i.X, i.Y, i.Z });
            Assert.AreEqual("Breton", i.Race);
            Assert.AreEqual("wench_unique", i.PortraitName);
            Assert.AreSame(t, i.Definition);
            Assert.AreEqual(i.Name + i.Gender + i.FaceOutfit + i.FaceVariant, again.Name + again.Gender + again.FaceOutfit + again.FaceVariant);
            StringAssert.StartsWith("Breton " + i.Gender, i.Name);
        }

        [Test]
        public void ForPlaced_PeopleDiffer()
        {
            PlacedNpcList list = new PlacedNpcList();
            NpcDefinition t = Template("wench");
            HashSet<string> names = new HashSet<string>();
            for (int n = 0; n < 5; n++)
                names.Add(PopulationPlanner.ForPlaced(t, list.Add("wench", 7, "R", "P", 0f, 0f, 0f), "Nord", new FakeNames()).Name);
            Assert.AreEqual(5, names.Count);
        }

        [Test]
        public void ForPlaced_TemplateGenderAndRaceWin()
        {
            NpcDefinition t = Template("wench");
            t.Gender = "Female";
            t.Race = "Redguard";
            PlacedNpcList list = new PlacedNpcList();
            NpcInstance i = PopulationPlanner.ForPlaced(t, list.Add("wench", 7, "R", "P", 0f, 0f, 0f), "Breton", new FakeNames());
            Assert.AreEqual("Female", i.Gender);
            Assert.AreEqual("Redguard", i.Race);
        }
    }
}

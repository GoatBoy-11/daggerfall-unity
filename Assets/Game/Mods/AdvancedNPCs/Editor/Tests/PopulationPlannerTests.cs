using System.Collections.Generic;
using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class PopulationPlannerTests
    {
        class FakeNames : INameSource
        {
            public readonly List<string> Calls = new List<string>();

            public string Generate(string race, string gender, uint seed)
            {
                Calls.Add(race + "/" + gender);
                return race + " " + gender + " " + seed;
            }
        }

        static NpcDefinition Template(string id, int min, int max)
        {
            NpcDefinition d = new NpcDefinition();
            d.Id = id;
            d.Folder = id;
            d.Kind = NpcKind.Generic;
            d.Name = "";
            d.Gender = "";
            d.BaseClass = "Bard";
            d.Spawn = new GenericSpawn();
            d.Spawn.CountMin = min;
            d.Spawn.CountMax = max;
            return d;
        }

        static TownInfo Town(int mapId)
        {
            return new TownInfo(mapId, "Daggerfall", "Daggerfall", "TownCity", "Breton");
        }

        static List<NpcInstance> Plan(GenericMode mode, int cap, int cells, uint visitSeed, params NpcDefinition[] templates)
        {
            return PopulationPlanner.Plan(templates, Town(1234), mode, cap, cells, visitSeed, new FakeNames());
        }

        static string Describe(List<NpcInstance> plan)
        {
            List<string> parts = new List<string>();
            foreach (NpcInstance i in plan)
                parts.Add(i.Key + "|" + i.Name + "|" + i.Gender + "|" + i.PortraitName + "|" + i.FaceOutfit + "/" + i.FaceVariant + "|" + i.CellIndex);
            return string.Join("\n", parts.ToArray());
        }

        static List<string> Keys(List<NpcInstance> plan)
        {
            List<string> keys = new List<string>();
            foreach (NpcInstance i in plan)
                keys.Add(i.Key);
            return keys;
        }

        [Test]
        public void KeyFor_Format()
        {
            Assert.AreEqual("commoner@1234#2", PopulationPlanner.KeyFor("commoner", 1234, 2));
        }

        [Test]
        public void Keys_FollowTemplateMapAndIndex()
        {
            List<NpcInstance> plan = Plan(GenericMode.SamePeople, 12, 100, 0, Template("commoner", 3, 3));
            CollectionAssert.AreEqual(new[] { "commoner@1234#0", "commoner@1234#1", "commoner@1234#2" }, Keys(plan));
            foreach (NpcInstance i in plan)
            {
                Assert.AreEqual("commoner", i.Definition.Id);
                Assert.AreEqual(StableHash.Of(i.Key), i.Seed);
                Assert.IsTrue(i.Persistent);
                Assert.AreEqual("Breton", i.Race);
            }
        }

        [Test]
        public void SamePeople_IsRepeatable_AndIgnoresVisitSeed()
        {
            NpcDefinition t = Template("commoner", 1, 5);
            Assert.AreEqual(Describe(Plan(GenericMode.SamePeople, 12, 500, 1, t)), Describe(Plan(GenericMode.SamePeople, 12, 500, 999, t)));
        }

        [Test]
        public void RandomMode_DependsOnVisitSeed_AndIsNotPersistent()
        {
            NpcDefinition t = Template("commoner", 4, 4);
            List<NpcInstance> a = Plan(GenericMode.RandomEachVisit, 12, 500, 1, t);
            List<NpcInstance> b = Plan(GenericMode.RandomEachVisit, 12, 500, 2, t);
            Assert.AreNotEqual(Describe(a), Describe(b));
            Assert.AreEqual(Describe(a), Describe(Plan(GenericMode.RandomEachVisit, 12, 500, 1, t)));
            foreach (NpcInstance i in a)
                Assert.IsFalse(i.Persistent);
        }

        [Test]
        public void SamePlaceNameDifferentMap_DifferentKeysAndPeople()
        {
            NpcDefinition t = Template("commoner", 3, 3);
            List<NpcInstance> one = PopulationPlanner.Plan(new[] { t }, new TownInfo(1, "Daggerfall", "Daggerfall", "TownCity", "Breton"),
                GenericMode.SamePeople, 12, 500, 0, new FakeNames());
            List<NpcInstance> two = PopulationPlanner.Plan(new[] { t }, new TownInfo(2, "Sentinel", "Daggerfall", "TownCity", "Breton"),
                GenericMode.SamePeople, 12, 500, 0, new FakeNames());
            foreach (string key in Keys(one))
                CollectionAssert.DoesNotContain(Keys(two), key);
            Assert.AreNotEqual(one[0].Seed, two[0].Seed);
        }

        [Test]
        public void Cap_KeepsTemplateIdOrderThenIndex()
        {
            List<NpcInstance> plan = Plan(GenericMode.SamePeople, 4, 100, 0, Template("b", 3, 3), Template("a", 3, 3));
            CollectionAssert.AreEqual(new[] { "a@1234#0", "a@1234#1", "a@1234#2", "b@1234#0" }, Keys(plan));
        }

        [Test]
        public void CapZero_PlansNothing()
        {
            Assert.AreEqual(0, Plan(GenericMode.SamePeople, 0, 100, 0, Template("a", 3, 3)).Count);
        }

        [Test]
        public void CountZero_PlansNothing()
        {
            Assert.AreEqual(0, Plan(GenericMode.SamePeople, 12, 100, 0, Template("a", 0, 0)).Count);
        }

        [Test]
        public void CountRange_StaysWithinBounds()
        {
            List<NpcDefinition> templates = new List<NpcDefinition>();
            for (int k = 0; k < 50; k++)
                templates.Add(Template("t" + k.ToString("00"), 1, 3));
            List<NpcInstance> plan = PopulationPlanner.Plan(templates, Town(1234), GenericMode.SamePeople, 1000, 1000, 0, new FakeNames());
            Dictionary<string, int> perTemplate = new Dictionary<string, int>();
            foreach (NpcInstance i in plan)
            {
                int n;
                perTemplate.TryGetValue(i.Definition.Id, out n);
                perTemplate[i.Definition.Id] = n + 1;
            }
            HashSet<int> counts = new HashSet<int>();
            foreach (NpcDefinition t in templates)
            {
                int n;
                perTemplate.TryGetValue(t.Id, out n);
                Assert.IsTrue(n >= 1 && n <= 3, t.Id + " has " + n);
                counts.Add(n);
            }
            Assert.IsTrue(counts.Count > 1, "every template rolled the same count");
        }

        [Test]
        public void FixedPositions_TakePrecedence()
        {
            NpcDefinition t = Template("guard", 3, 3);
            SpawnPlace place = new SpawnPlace();
            place.Region = "Daggerfall";
            place.Place = "Daggerfall";
            place.Positions.Add(new float[] { 1, 2, 3 });
            place.Positions.Add(new float[] { 4, 5, 6 });
            t.Spawn.Places.Add(place);
            List<NpcInstance> plan = Plan(GenericMode.SamePeople, 12, 100, 0, t);
            Assert.AreEqual(3, plan.Count);
            Assert.IsTrue(plan[0].HasFixedPosition);
            Assert.AreEqual(1f, plan[0].X);
            Assert.AreEqual(-1, plan[0].CellIndex);
            Assert.IsTrue(plan[1].HasFixedPosition);
            Assert.AreEqual(6f, plan[1].Z);
            Assert.IsFalse(plan[2].HasFixedPosition);
            Assert.IsTrue(plan[2].CellIndex >= 0 && plan[2].CellIndex < 100);
        }

        [Test]
        public void NoCells_MarksInstancesUnplaced()
        {
            List<NpcInstance> plan = Plan(GenericMode.SamePeople, 12, 0, 0, Template("a", 3, 3));
            Assert.AreEqual(3, plan.Count);
            foreach (NpcInstance i in plan)
            {
                Assert.IsFalse(i.HasFixedPosition);
                Assert.AreEqual(-1, i.CellIndex);
            }
        }

        [Test]
        public void FewCells_ReusesCells()
        {
            List<NpcInstance> plan = Plan(GenericMode.SamePeople, 12, 1, 0, Template("a", 3, 3));
            Assert.AreEqual(3, plan.Count);
            foreach (NpcInstance i in plan)
                Assert.AreEqual(0, i.CellIndex);
        }

        [Test]
        public void ManyCells_AreDistinct()
        {
            List<NpcInstance> plan = Plan(GenericMode.SamePeople, 12, 10000, 0, Template("a", 8, 8));
            HashSet<int> cells = new HashSet<int>();
            foreach (NpcInstance i in plan)
                cells.Add(i.CellIndex);
            Assert.AreEqual(8, cells.Count);
        }

        [Test]
        public void NonMatchingTownAndUniqueDefinitions_AreIgnored()
        {
            NpcDefinition tavernOnly = Template("barkeep", 2, 2);
            tavernOnly.Spawn.LocationTypes.Clear();
            tavernOnly.Spawn.LocationTypes.Add("Tavern");
            NpcDefinition unique = Template("bram", 2, 2);
            unique.Kind = NpcKind.Unique;
            Assert.AreEqual(0, Plan(GenericMode.SamePeople, 12, 100, 0, tavernOnly, unique).Count);
        }

        [Test]
        public void Names_FixedNameListOrGenerator()
        {
            NpcDefinition fixedName = Template("guard", 2, 2);
            fixedName.Name = "Guard";
            foreach (NpcInstance i in Plan(GenericMode.SamePeople, 12, 100, 0, fixedName))
                Assert.AreEqual("Guard", i.Name);

            NpcDefinition listed = Template("listed", 4, 4);
            listed.Names.Add("Ann");
            listed.Names.Add("Bo");
            foreach (NpcInstance i in Plan(GenericMode.SamePeople, 12, 100, 0, listed))
                CollectionAssert.Contains(new[] { "Ann", "Bo" }, i.Name);

            NpcDefinition generated = Template("nord", 2, 2);
            generated.Race = "Nord";
            FakeNames names = new FakeNames();
            List<NpcInstance> plan = PopulationPlanner.Plan(new[] { generated }, Town(1234), GenericMode.SamePeople, 12, 100, 0, names);
            Assert.AreEqual(2, names.Calls.Count);
            Assert.AreEqual("Nord/" + plan[0].Gender, names.Calls[0]);
            StringAssert.StartsWith("Nord " + plan[0].Gender + " ", plan[0].Name);
        }

        [Test]
        public void PortraitsAndGender()
        {
            NpcDefinition t = Template("a", 4, 4);
            t.Portraits.Add("c1");
            t.Portraits.Add("c2");
            t.Gender = "Female";
            foreach (NpcInstance i in Plan(GenericMode.SamePeople, 12, 100, 0, t))
            {
                CollectionAssert.Contains(new[] { "c1", "c2" }, i.PortraitName);
                Assert.AreEqual("Female", i.Gender);
            }
            foreach (NpcInstance i in Plan(GenericMode.SamePeople, 12, 100, 0, Template("b", 2, 2)))
                Assert.IsNull(i.PortraitName);
        }
    }
}

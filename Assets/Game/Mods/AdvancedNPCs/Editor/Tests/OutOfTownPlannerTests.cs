using System.Collections.Generic;
using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class OutOfTownPlannerTests
    {
        class Names : INameSource
        {
            public string Generate(string listName, string race, string gender, uint seed)
            {
                return race + " " + gender + " " + seed;
            }
        }

        static NpcDefinition Template(string id, string spawn)
        {
            ParseResult r = DefinitionParser.ParseFolder(id, "{ \"kind\": \"generic\", \"spawn\": " + spawn + " }");
            Assert.IsTrue(r.Ok, r.Error);
            return r.Definition;
        }

        static PlaceInfo Crypt(int mapId)
        {
            return PlaceInfo.Dungeon(mapId, "Daggerfall", "Castle Woodcroft", "Breton", "Crypt");
        }

        static PlaceInfo Tavern(int mapId, int buildingKey)
        {
            return PlaceInfo.Interior(mapId, "Daggerfall", "Daggerfall", "Breton", "tavern", null, buildingKey);
        }

        static List<NpcInstance> Plan(PlaceInfo place, GenericMode mode, uint seed, List<string> explain, params NpcDefinition[] templates)
        {
            return OutOfTownPlanner.Plan(templates, place, mode, 30, seed, new Names(), new FakeFacts(), explain);
        }

        static List<string> Keys(List<NpcInstance> people)
        {
            return people.ConvertAll(delegate (NpcInstance i) { return i.Key; });
        }

        [Test]
        public void PlaceKeys()
        {
            Assert.AreEqual("12:dungeon", Crypt(12).PlaceKey);
            Assert.AreEqual("12:b65794", Tavern(12, 65794).PlaceKey);
        }

        [Test]
        public void Chance100_Always_KeysSeedsGroups()
        {
            NpcDefinition bandit = Template("bandit", "{ \"dungeons\": { \"chance\": 100, \"count\": [3, 3] } }");
            List<string> explain = new List<string>();
            List<NpcInstance> people = Plan(Crypt(12), GenericMode.SamePeople, 1, explain, bandit);
            CollectionAssert.AreEqual(new[] { "bandit@12:dungeon#0", "bandit@12:dungeon#1", "bandit@12:dungeon#2" }, Keys(people));
            Assert.IsTrue(people.TrueForAll(delegate (NpcInstance i) { return i.Persistent && i.Group == 0 && i.Seed == StableHash.Of(i.Key); }));
            StringAssert.StartsWith("bandit: chance 100, rolled ", explain[0]);
            StringAssert.EndsWith(": 3 here", explain[0]);
        }

        [Test]
        public void Chance0_Never()
        {
            NpcDefinition bandit = Template("bandit", "{ \"dungeons\": { \"chance\": 0 } }");
            for (uint seed = 0; seed < 50; seed++)
                CollectionAssert.IsEmpty(Plan(Crypt((int)seed), GenericMode.RandomEachVisit, seed, new List<string>(), bandit));
        }

        [Test]
        public void SameMode_RepeatsExactly_RandomModeVaries()
        {
            NpcDefinition bandit = Template("bandit", "{ \"dungeons\": { \"chance\": 50, \"count\": [1, 3] } }");
            NpcDefinition rat = Template("rat_man", "{ \"dungeons\": { \"chance\": 50, \"count\": [1, 3] } }");
            for (int map = 0; map < 20; map++)
            {
                List<string> a = Keys(Plan(Crypt(map), GenericMode.SamePeople, 1, new List<string>(), bandit, rat));
                List<string> b = Keys(Plan(Crypt(map), GenericMode.SamePeople, 999, new List<string>(), rat, bandit));
                CollectionAssert.AreEqual(a, b);
            }
            HashSet<string> outcomes = new HashSet<string>();
            for (uint seed = 0; seed < 40; seed++)
                outcomes.Add(string.Join(",", Keys(Plan(Crypt(7), GenericMode.RandomEachVisit, seed, new List<string>(), bandit)).ToArray()));
            Assert.Greater(outcomes.Count, 2);
            List<NpcInstance> random = Plan(Crypt(7), GenericMode.RandomEachVisit, 3, new List<string>(), Template("x", "{ \"dungeons\": { \"chance\": 100 } }"));
            Assert.IsFalse(random[0].Persistent);
        }

        [Test]
        public void SameMode_ChanceIsAboutRight()
        {
            NpcDefinition bandit = Template("bandit", "{ \"dungeons\": { \"chance\": 30 } }");
            int here = 0;
            for (int map = 0; map < 1000; map++)
                here += Plan(Crypt(map), GenericMode.SamePeople, 0, new List<string>(), bandit).Count;
            Assert.That(here, Is.InRange(230, 370));
        }

        [Test]
        public void DungeonTypeFilter_Explained()
        {
            NpcDefinition bandit = Template("bandit", "{ \"dungeons\": { \"chance\": 100, \"dungeonTypes\": [\"HumanStronghold\", \"Prison\"] } }");
            List<string> explain = new List<string>();
            CollectionAssert.IsEmpty(Plan(Crypt(1), GenericMode.SamePeople, 0, explain, bandit));
            CollectionAssert.AreEqual(new[] { "bandit: dungeon type Crypt is not one of HumanStronghold, Prison" }, explain);
        }

        [Test]
        public void InteriorFilters_BuildingAndGuild()
        {
            NpcDefinition patron = Template("patron", "{ \"interiors\": { \"chance\": 100, \"buildings\": [\"Tavern\", \"Mages Guild\"] } }");
            Assert.AreEqual(1, Plan(Tavern(1, 5), GenericMode.SamePeople, 0, new List<string>(), patron).Count);
            PlaceInfo mages = PlaceInfo.Interior(1, "R", "P", "Breton", "guildhall", "magesguild", 6);
            PlaceInfo fighters = PlaceInfo.Interior(1, "R", "P", "Breton", "guildhall", "fightersguild", 7);
            Assert.AreEqual(1, Plan(mages, GenericMode.SamePeople, 0, new List<string>(), patron).Count);
            List<string> explain = new List<string>();
            CollectionAssert.IsEmpty(Plan(fighters, GenericMode.SamePeople, 0, explain, patron));
            CollectionAssert.AreEqual(new[] { "patron: building guildhall (fightersguild) is not one of tavern, magesguild" }, explain);
            Assert.AreEqual("patron@1:b5#0", Plan(Tavern(1, 5), GenericMode.SamePeople, 0, new List<string>(), patron)[0].Key);
        }

        [Test]
        public void TemplatesWithoutTheBlock_Skipped_NotExplained()
        {
            NpcDefinition townOnly = Template("baker", "{ \"count\": [1, 1] }");
            List<string> explain = new List<string>();
            CollectionAssert.IsEmpty(Plan(Crypt(1), GenericMode.SamePeople, 0, explain, townOnly));
            CollectionAssert.IsEmpty(explain);
        }

        [Test]
        public void When_Fails_Explained()
        {
            NpcDefinition ghost = Template("ghost", "{ \"dungeons\": { \"chance\": 100, \"when\": { \"time\": \"night\" } } }");
            List<string> explain = new List<string>();
            CollectionAssert.IsEmpty(OutOfTownPlanner.Plan(new[] { ghost }, Crypt(1), GenericMode.SamePeople, 30, 0, new Names(), new FakeFacts(), explain));
            CollectionAssert.AreEqual(new[] { "ghost: when: time does not hold" }, explain);
            Assert.AreEqual(1, OutOfTownPlanner.Plan(new[] { ghost }, Crypt(1), GenericMode.SamePeople, 30, 0, new Names(), new FakeFacts { Night = true }, new List<string>()).Count);
        }

        [Test]
        public void ChanceMissed_Explained()
        {
            NpcDefinition bandit = Template("bandit", "{ \"dungeons\": { \"chance\": 1 } }");
            List<string> explain = new List<string>();
            int map = 0;
            while (Plan(Crypt(map), GenericMode.SamePeople, 0, explain, bandit).Count > 0 || explain.Count == 0)
            {
                explain.Clear();
                map++;
            }
            StringAssert.IsMatch(@"^bandit: chance 1, rolled \d+: not here$", explain[0]);
        }

        [Test]
        public void Cap_LimitsTotal_Explained()
        {
            NpcDefinition a = Template("a", "{ \"dungeons\": { \"chance\": 100, \"count\": [3, 3] } }");
            NpcDefinition b = Template("b", "{ \"dungeons\": { \"chance\": 100, \"count\": [3, 3] } }");
            List<string> explain = new List<string>();
            List<NpcInstance> people = OutOfTownPlanner.Plan(new[] { a, b }, Crypt(1), GenericMode.SamePeople, 4, 0, new Names(), new FakeFacts(), explain);
            Assert.AreEqual(4, people.Count);
            Assert.AreEqual(1, people[3].Group);
            StringAssert.EndsWith(": 1 of 3 here (limit 4 reached)", explain[1]);
        }

        [Test]
        public void ForEncounter_KeyNeverSaved_RolledFromSeed()
        {
            NpcDefinition pilgrim = Template("pilgrim", "{ \"wilderness\": { \"chance\": 10 } }");
            NpcInstance a = OutOfTownPlanner.ForEncounter(pilgrim, 17, 1234, "Nord", new Names());
            NpcInstance b = OutOfTownPlanner.ForEncounter(pilgrim, 18, 1234, "Nord", new Names());
            Assert.AreEqual("pilgrim@wild#17", a.Key);
            Assert.IsFalse(a.Persistent);
            Assert.AreEqual(a.Name, b.Name);
            Assert.AreEqual("Nord", a.Race);
            Assert.AreEqual(1234u, a.Seed);
        }

        [Test]
        public void PlacedKeys_PerContext_OldSavesUnchanged()
        {
            PlacedNpcList list = new PlacedNpcList();
            PlacedNpc town = list.Add("wench", 12, "R", "P", 1, 2, 3);
            PlacedNpc dungeon = list.Add("wench", 12, "R", "P", 1, 2, 3, "dungeon");
            PlacedNpc inside = list.Add("wench", 12, "R", "P", 1, 2, 3, "b5");
            PlacedNpc inside2 = list.Add("wench", 12, "R", "P", 1, 2, 3, "b5");
            Assert.AreEqual("wench@12+1", town.Key());
            Assert.AreEqual("wench@12:dungeon+1", dungeon.Key());
            Assert.AreEqual("wench@12:b5+1", inside.Key());
            Assert.AreEqual("wench@12:b5+2", inside2.Key());
            Assert.AreEqual(1, list.ForTown(12).Count);
            Assert.AreEqual(2, list.ForPlace(12, "b5").Count);
            PlacedNpc old = new PlacedNpc { template = "wench", mapId = 12, number = 3 };
            Assert.AreEqual("wench@12+3", old.Key());
            Assert.IsTrue(old.IsTown);
        }
    }
}

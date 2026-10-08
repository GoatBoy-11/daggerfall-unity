using System.Collections.Generic;
using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class OutsideSpawnParserTests
    {
        static ParseResult Parse(string spawnJson)
        {
            return DefinitionParser.ParseFolder("bandit", "{ \"kind\": \"generic\", \"spawn\": " + spawnJson + " }");
        }

        static void HasWarning(ParseResult r, string part)
        {
            Assert.IsTrue(r.Warnings.Exists(delegate (string m) { return m.Contains(part); }),
                "no warning containing \"" + part + "\" in:\n" + string.Join("\n", r.Warnings.ToArray()));
        }

        static TownInfo Town(string type)
        {
            return new TownInfo(1, "Daggerfall", "Daggerfall", type, "Breton");
        }

        [Test]
        public void Dungeons_Defaults_AndTypes()
        {
            ParseResult r = Parse("{ \"dungeons\": { \"chance\": 30, \"dungeonTypes\": [\"human stronghold\", \"Crypt\"] } }");
            Assert.IsTrue(r.Ok, r.Error);
            CollectionAssert.IsEmpty(r.Warnings);
            DungeonSpawn d = r.Definition.Spawn.Dungeons;
            Assert.AreEqual(30, d.Chance);
            Assert.AreEqual(1, d.CountMin);
            Assert.AreEqual(1, d.CountMax);
            CollectionAssert.AreEqual(new[] { "HumanStronghold", "Crypt" }, d.DungeonTypes);
            Assert.IsNull(d.When);
        }

        [Test]
        public void Interiors_BuildingsAndGuilds_Count()
        {
            ParseResult r = Parse("{ \"interiors\": { \"buildings\": [\"Tavern\", \"Fighters Guild\", \"general store\"], \"chance\": 50, \"count\": [1, 2] } }");
            Assert.IsTrue(r.Ok, r.Error);
            InteriorSpawn i = r.Definition.Spawn.Interiors;
            CollectionAssert.AreEqual(new[] { "tavern", "fightersguild", "generalstore" }, i.Buildings);
            Assert.AreEqual(50, i.Chance);
            Assert.AreEqual(1, i.CountMin);
            Assert.AreEqual(2, i.CountMax);
        }

        [Test]
        public void Interiors_Matches()
        {
            InteriorSpawn i = Parse("{ \"interiors\": { \"buildings\": [\"Tavern\", \"Fighters Guild\", \"House\"], \"chance\": 50 } }").Definition.Spawn.Interiors;
            Assert.IsTrue(i.Matches("tavern", null));
            Assert.IsTrue(i.Matches("guildhall", "fightersguild"));
            Assert.IsFalse(i.Matches("guildhall", "magesguild"));
            Assert.IsTrue(i.Matches("house", null));
            Assert.IsFalse(i.Matches("temple", null));
            InteriorSpawn any = Parse("{ \"interiors\": { \"buildings\": [\"GuildHall\"], \"chance\": 50 } }").Definition.Spawn.Interiors;
            Assert.IsTrue(any.Matches("guildhall", "magesguild"));
        }

        [Test]
        public void Wilderness_DefaultsAndWhen()
        {
            ParseResult r = Parse("{ \"wilderness\": { \"chance\": 10, \"max\": 2, \"when\": { \"time\": \"night\" } } }");
            Assert.IsTrue(r.Ok, r.Error);
            WildernessSpawn w = r.Definition.Spawn.Wilderness;
            Assert.AreEqual(10, w.Chance);
            Assert.AreEqual(2, w.Max);
            Assert.IsFalse(w.When.Holds(new FakeFacts { Night = false }));
            Assert.IsTrue(w.When.Holds(new FakeFacts { Night = true }));
            Assert.AreEqual(1, Parse("{ \"wilderness\": { \"chance\": 10 } }").Definition.Spawn.Wilderness.Max);
        }

        [TestCase("{ \"dungeons\": { \"chance\": 101 } }", "spawn.dungeons.chance: must be a whole number 0-100, dungeons ignored")]
        [TestCase("{ \"dungeons\": { \"chance\": 12.5 } }", "spawn.dungeons.chance: must be a whole number 0-100, dungeons ignored")]
        [TestCase("{ \"dungeons\": { } }", "spawn.dungeons.chance: required, dungeons ignored")]
        [TestCase("{ \"dungeons\": { \"chance\": 5, \"count\": [0, 2] } }", "spawn.dungeons.count: need whole numbers 1 <= min <= max <= 10")]
        [TestCase("{ \"dungeons\": { \"chance\": 5, \"count\": [3, 11] } }", "spawn.dungeons.count: need whole numbers 1 <= min <= max <= 10")]
        [TestCase("{ \"interiors\": { \"chance\": 5 } }", "spawn.interiors.buildings: required, interiors ignored")]
        [TestCase("{ \"wilderness\": { \"chance\": 5, \"max\": 11 } }", "spawn.wilderness.max: must be a whole number 1-10, wilderness ignored")]
        public void BadBlock_SkippedWithMessage_TemplateStillLoads(string spawn, string message)
        {
            ParseResult r = Parse(spawn);
            Assert.IsTrue(r.Ok, r.Error);
            HasWarning(r, "bandit/npc.json: " + message);
        }

        [Test]
        public void BadBlock_OthersStillWork()
        {
            ParseResult r = Parse("{ \"dungeons\": { \"chance\": 500 }, \"wilderness\": { \"chance\": 5 } }");
            Assert.IsNull(r.Definition.Spawn.Dungeons);
            Assert.IsNotNull(r.Definition.Spawn.Wilderness);
        }

        [Test]
        public void UnknownDungeonType_ListsAllowed()
        {
            ParseResult r = Parse("{ \"dungeons\": { \"chance\": 5, \"dungeonTypes\": [\"Castle\"] } }");
            HasWarning(r, "spawn.dungeons.dungeonTypes: unknown dungeon type \"Castle\" (use Crypt, OrcStronghold, HumanStronghold");
            Assert.IsNull(r.Definition.Spawn.Dungeons);
        }

        [Test]
        public void UnknownBuilding_ListsAllowed()
        {
            ParseResult r = Parse("{ \"interiors\": { \"chance\": 5, \"buildings\": [\"Brothel\"] } }");
            HasWarning(r, "spawn.interiors.buildings: unknown building \"Brothel\" (use Alchemist, Armorer");
        }

        [Test]
        public void UnknownField_Hint()
        {
            ParseResult r = Parse("{ \"dungeons\": { \"chance\": 5, \"cuont\": [1, 2] } }");
            HasWarning(r, "spawn.dungeons.cuont: unknown field (did you mean \"count\"?), ignored");
            ParseResult block = Parse("{ \"dungeon\": { \"chance\": 5 } }");
            HasWarning(block, "spawn.dungeon: unknown field (did you mean \"dungeons\"?), ignored");
        }

        [Test]
        public void ConversationConditions_RejectedInSpawn()
        {
            ParseResult r = Parse("{ \"wilderness\": { \"chance\": 5, \"when\": { \"asked\": \"x\", \"reaction\": \"likes\" } } }");
            HasWarning(r, "spawn.wilderness: when: asked: only allowed in dialogue");
            HasWarning(r, "spawn.wilderness: when: reaction: only allowed in dialogue");
            Assert.IsFalse(r.Definition.Spawn.Wilderness.When.Holds(new FakeFacts()));
        }

        [Test]
        public void OnlyContextBlocks_NoTowns()
        {
            GenericSpawn s = Parse("{ \"dungeons\": { \"chance\": 5 } }").Definition.Spawn;
            Assert.IsFalse(s.HasTownRules);
            SpawnPlace place;
            Assert.IsFalse(s.Matches(Town("TownCity"), out place));
        }

        [Test]
        public void ContextBlocksWithLocationTypes_TownsToo()
        {
            GenericSpawn s = Parse("{ \"locationTypes\": [\"TownCity\"], \"dungeons\": { \"chance\": 5 } }").Definition.Spawn;
            Assert.IsTrue(s.HasTownRules);
            SpawnPlace place;
            Assert.IsTrue(s.Matches(Town("TownCity"), out place));
        }

        [Test]
        public void CountWithoutTownRules_Warns()
        {
            ParseResult r = Parse("{ \"count\": [1, 2], \"dungeons\": { \"chance\": 5 } }");
            HasWarning(r, "spawn.count: only for towns (add \"locationTypes\" to spawn in towns too), ignored");
        }

        [Test]
        public void OldTemplate_Unchanged()
        {
            GenericSpawn s = Parse("{ \"count\": [1, 3] }").Definition.Spawn;
            Assert.IsTrue(s.HasTownRules);
            Assert.IsNull(s.Dungeons);
            Assert.IsNull(s.Interiors);
            Assert.IsNull(s.Wilderness);
            SpawnPlace place;
            Assert.IsTrue(s.Matches(Town("TownVillage"), out place));
        }
    }
}

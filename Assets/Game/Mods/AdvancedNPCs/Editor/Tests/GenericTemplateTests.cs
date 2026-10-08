using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class GenericTemplateTests
    {
        static ParseResult Parse(string fields)
        {
            return DefinitionParser.ParseFolder("commoner", "{ \"kind\": \"generic\"" + (fields.Length > 0 ? ", " + fields : "") + " }");
        }

        static TownInfo Town(string region, string place, string type)
        {
            return new TownInfo(1, region, place, type, "Breton");
        }

        [Test]
        public void Minimal_AppliesDefaults()
        {
            ParseResult r = Parse("");
            Assert.IsTrue(r.Ok, r.Error);
            NpcDefinition d = r.Definition;
            Assert.AreEqual(NpcKind.Generic, d.Kind);
            Assert.AreEqual("", d.Name);
            Assert.AreEqual(0, d.Names.Count);
            Assert.AreEqual(0, d.Portraits.Count);
            Assert.IsNotNull(d.Spawn);
            CollectionAssert.AreEqual(new[] { "TownCity", "TownHamlet", "TownVillage" }, d.Spawn.LocationTypes);
            Assert.AreEqual(0, d.Spawn.Places.Count);
            Assert.AreEqual(1, d.Spawn.CountMin);
            Assert.AreEqual(3, d.Spawn.CountMax);
            Assert.AreEqual("Spellsword", d.BaseClass);
            Assert.AreEqual(0, r.Warnings.Count);
        }

        [Test]
        public void FullTemplate_IsRead()
        {
            ParseResult r = Parse(
                "\"name\": \"Guard\", \"names\": [\"Ann\", \"Bo\"], \"portrait\": \"A\", \"portraits\": [\"a.png\", \"b\"], " +
                "\"spawn\": { \"locationTypes\": [\"towncity\", \"Tavern\"], \"count\": [2, 4], " +
                "\"places\": [ { \"region\": \"Daggerfall\", \"place\": \"Daggerfall\", \"positions\": [[1, 2, 3], [4.5, 5, 6]] } ] }, " +
                "\"bravery\": \"Coward\"");
            Assert.IsTrue(r.Ok, r.Error);
            NpcDefinition d = r.Definition;
            Assert.AreEqual("Guard", d.Name);
            CollectionAssert.AreEqual(new[] { "Ann", "Bo" }, d.Names);
            CollectionAssert.AreEqual(new[] { "a", "b" }, d.Portraits);
            CollectionAssert.AreEqual(new[] { "TownCity", "Tavern" }, d.Spawn.LocationTypes);
            Assert.AreEqual(2, d.Spawn.CountMin);
            Assert.AreEqual(4, d.Spawn.CountMax);
            Assert.AreEqual(1, d.Spawn.Places.Count);
            Assert.AreEqual("Daggerfall", d.Spawn.Places[0].Region);
            Assert.AreEqual(2, d.Spawn.Places[0].Positions.Count);
            Assert.AreEqual(4.5f, d.Spawn.Places[0].Positions[1][0]);
            Assert.AreEqual(Bravery.Coward, d.Bravery);
        }

        [TestCase("\"location\": { \"region\": \"R\", \"place\": \"P\" }", "commoner/npc.json: location: belongs to unique ANPCs; generic templates use spawn")]
        [TestCase("\"position\": [0, 0, 0]", "commoner/npc.json: position: belongs to unique ANPCs; generic templates use spawn")]
        [TestCase("\"spawn\": { \"locationTypes\": [\"Castle\"] }", "commoner/npc.json: spawn.locationTypes: unknown location type \"Castle\"")]
        [TestCase("\"spawn\": { \"count\": [3, 1] }", "commoner/npc.json: spawn.count: need whole numbers 0 <= min <= max <= 20 (got [3, 1])")]
        [TestCase("\"spawn\": { \"count\": [0, 21] }", "commoner/npc.json: spawn.count: need whole numbers 0 <= min <= max <= 20 (got [0, 21])")]
        [TestCase("\"spawn\": { \"count\": [1.5, 2] }", "commoner/npc.json: spawn.count: need whole numbers 0 <= min <= max <= 20 (got [1.5, 2])")]
        [TestCase("\"spawn\": { \"count\": 2 }", "commoner/npc.json: spawn.count: must be [min, max]")]
        [TestCase("\"spawn\": { \"places\": [ { \"place\": \"P\" } ] }", "commoner/npc.json: spawn.places[0].region: required")]
        [TestCase("\"spawn\": { \"places\": [ { \"region\": \"R\", \"place\": \"P\", \"positions\": [[1, 2]] } ] }", "commoner/npc.json: spawn.places[0].positions: must be a list of [x, y, z]")]
        [TestCase("\"spawn\": []", "commoner/npc.json: spawn: must be an object")]
        [TestCase("\"names\": [\"\"]", "commoner/npc.json: names: must be a list of non-empty texts")]
        public void BadValues_AreRejected(string fields, string expected)
        {
            Assert.AreEqual(expected, Parse(fields).Error);
        }

        [Test]
        public void UnknownSpawnFields_Warn()
        {
            ParseResult r = Parse("\"spawn\": { \"colour\": 1, \"places\": [ { \"region\": \"R\", \"place\": \"P\", \"size\": 2 } ] }");
            Assert.IsTrue(r.Ok, r.Error);
            CollectionAssert.Contains(r.Warnings, "commoner/npc.json: spawn.colour: unknown field, ignored");
            CollectionAssert.Contains(r.Warnings, "commoner/npc.json: spawn.places[0].size: unknown field, ignored");
        }

        [Test]
        public void Matches_DefaultTypes()
        {
            GenericSpawn s = Parse("").Definition.Spawn;
            SpawnPlace place;
            Assert.IsTrue(s.Matches(Town("R", "P", "TownCity"), out place));
            Assert.IsNull(place);
            Assert.IsTrue(s.Matches(Town("R", "P", "TownVillage"), out place));
            Assert.IsFalse(s.Matches(Town("R", "P", "Tavern"), out place));
        }

        [Test]
        public void Matches_PlacesOverrideTypes_IgnoringCaseAndSpaces()
        {
            GenericSpawn s = Parse("\"spawn\": { \"locationTypes\": [\"Tavern\"], \"places\": [ { \"region\": \"Daggerfall\", \"place\": \"Daggerfall\" } ] }").Definition.Spawn;
            SpawnPlace place;
            Assert.IsTrue(s.Matches(Town(" daggerfall", "DAGGERFALL ", "TownCity"), out place));
            Assert.AreEqual("Daggerfall", place.Place);
            Assert.IsFalse(s.Matches(Town("Wayrest", "Wayrest", "Tavern"), out place));
        }

        [Test]
        public void Catalog_PutsTemplatesInGenerics_WithTheirOwnDialogue()
        {
            DefinitionCatalog c = DefinitionCatalog.Build(new[]
            {
                new AnpcFolder("commoner", "{ \"kind\": \"generic\", \"portraits\": [\"c1\"] }",
                    "{ \"topics\": [ { \"caption\": \"Weather\", \"answers\": [\"Grey.\"] } ] }"),
            });
            Assert.AreEqual(0, c.ById.Count);
            Assert.AreEqual(1, c.Generics.Count);
            Assert.AreEqual("Weather", c.Generics[0].OwnDialogue.Topics[0].Caption);
            Assert.AreEqual("commoner", c.Generics[0].OwnDialogue.Name);
            CollectionAssert.Contains(c.Messages, "Loaded 0 unique ANPC(s) and 1 generic template(s).");
            CollectionAssert.AreEqual(new[] { "c1" }, c.ReferencedPortraits());
        }
    }
}

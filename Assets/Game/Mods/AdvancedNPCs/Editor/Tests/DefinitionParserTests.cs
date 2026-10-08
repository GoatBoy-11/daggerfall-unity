using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class DefinitionParserTests
    {
        const string Minimal =
            "{ \"id\": \"bram\", \"name\": \"Bram\", " +
            "\"location\": { \"region\": \"Daggerfall\", \"place\": \"Daggerfall\" }, " +
            "\"position\": [1.5, 2, -3] }";

        static string With(string extraFields)
        {
            return Minimal.Substring(0, Minimal.Length - 1) + ", " + extraFields + " }";
        }

        [Test]
        public void Dialogue_OneOrMoreTypes_Normalised()
        {
            ParseResult one = DefinitionParser.Parse("bram.json", With("\"dialogue\": \"Tavern Wench\""));
            Assert.IsTrue(one.Ok, one.Error);
            CollectionAssert.AreEqual(new[] { "tavern_wench" }, one.Definition.Dialogue);
            ParseResult two = DefinitionParser.Parse("bram.json", With("\"dialogue\": [\"gossip\", \"tavern_wench\"]"));
            CollectionAssert.AreEqual(new[] { "gossip", "tavern_wench" }, two.Definition.Dialogue);
            CollectionAssert.IsEmpty(DefinitionParser.Parse("bram.json", Minimal).Definition.Dialogue);
        }

        [Test]
        public void Dialogue_WrongType_WarnsAndNpcStillLoads()
        {
            ParseResult r = DefinitionParser.Parse("bram.json", With("\"dialogue\": 5"));
            Assert.IsTrue(r.Ok, r.Error);
            CollectionAssert.IsEmpty(r.Definition.Dialogue);
            CollectionAssert.Contains(r.Warnings, "bram.json: dialogue: must be a dialogue type name or a list of names, ignored");
        }

        [Test]
        public void Minimal_AppliesDefaults()
        {
            ParseResult r = DefinitionParser.Parse("bram.json", Minimal);
            Assert.IsTrue(r.Ok, r.Error);
            NpcDefinition d = r.Definition;
            Assert.AreEqual("bram", d.Id);
            Assert.AreEqual("Bram", d.Name);
            Assert.AreEqual("Daggerfall", d.Region);
            Assert.AreEqual("Daggerfall", d.Place);
            Assert.AreEqual(1.5f, d.X);
            Assert.AreEqual(2f, d.Y);
            Assert.AreEqual(-3f, d.Z);
            Assert.AreEqual("Spellsword", d.BaseClass);
            Assert.AreEqual("", d.Gender);
            Assert.AreEqual(Bravery.Normal, d.Bravery);
            Assert.AreEqual(25, d.FleeHealthPercent);
            Assert.AreEqual(6f, d.CalmDownMinHours);
            Assert.AreEqual(48f, d.CalmDownMaxHours);
            Assert.IsTrue(d.CrimeOnAttack);
            Assert.AreEqual(8f, d.WanderRadius);
            Assert.AreEqual("bram.json", d.SourceFile);
        }

        [Test]
        public void AllFields_AreReadAndCanonicalised()
        {
            ParseResult r = DefinitionParser.Parse("x.json", With(
                "\"baseClass\": \"nightblade\", \"gender\": \"female\", \"bravery\": \"brave\", " +
                "\"fleeHealthPercent\": 40, \"calmDownHours\": [1, 2], \"crimeOnAttack\": false, \"wanderRadius\": 0"));
            Assert.IsTrue(r.Ok, r.Error);
            Assert.AreEqual("Nightblade", r.Definition.BaseClass);
            Assert.AreEqual("Female", r.Definition.Gender);
            Assert.AreEqual(Bravery.Brave, r.Definition.Bravery);
            Assert.AreEqual(40, r.Definition.FleeHealthPercent);
            Assert.AreEqual(1f, r.Definition.CalmDownMinHours);
            Assert.AreEqual(2f, r.Definition.CalmDownMaxHours);
            Assert.IsFalse(r.Definition.CrimeOnAttack);
            Assert.AreEqual(0f, r.Definition.WanderRadius);
        }

        [Test]
        public void WindowsLineEndingsAndIndentation_Parse()
        {
            string json = "{\r\n  \"id\": \"bram\",\r\n  \"name\": \"Bram\",\r\n" +
                "  \"location\": { \"region\": \"A\", \"place\": \"B\" },\r\n  \"position\": [0, 0, 0]\r\n}\r\n";
            Assert.IsTrue(DefinitionParser.Parse("f.json", json).Ok);
        }

        [Test]
        public void CoreDoesNotDependOnUnity()
        {
            // In game, UnityEngine.JsonUtility left nested objects empty for classes from DFU's runtime-compiled
            // mod assembly ("location.region: required" for valid files). Core must parse JSON itself.
            foreach (System.Reflection.AssemblyName referenced in typeof(DefinitionParser).Assembly.GetReferencedAssemblies())
                StringAssert.DoesNotStartWith("UnityEngine", referenced.Name);
        }

        [Test]
        public void EscapesAndUnicode_Parse()
        {
            ParseResult r = DefinitionParser.Parse("f.json", With("\"name\": \"Br\\u00e9m \\\"the\\\" Cooper\""));
            Assert.IsTrue(r.Ok, r.Error);
            Assert.AreEqual("Brém \"the\" Cooper", r.Definition.Name);
        }

        [Test]
        public void TrailingGarbage_IsRejected()
        {
            ParseResult r = DefinitionParser.Parse("f.json", Minimal + " extra");
            Assert.IsFalse(r.Ok);
            StringAssert.StartsWith("f.json: file: invalid JSON", r.Error);
        }

        // With() appends after the minimal fields; a repeated key takes the last value.
        [TestCase("\"name\": 5", "f.json: name: must be text")]
        [TestCase("\"fleeHealthPercent\": \"abc\"", "f.json: fleeHealthPercent: must be a whole number")]
        [TestCase("\"fleeHealthPercent\": 12.5", "f.json: fleeHealthPercent: must be a whole number")]
        [TestCase("\"crimeOnAttack\": \"yes\"", "f.json: crimeOnAttack: must be true or false")]
        [TestCase("\"wanderRadius\": \"far\"", "f.json: wanderRadius: must be a number")]
        [TestCase("\"calmDownHours\": [1, \"x\"]", "f.json: calmDownHours: must be [min, max]")]
        public void WrongTypes_AreRejected(string field, string expected)
        {
            ParseResult r = DefinitionParser.Parse("f.json", With(field));
            Assert.IsFalse(r.Ok);
            Assert.AreEqual(expected, r.Error);
        }

        [TestCase("{ \"id\": \"b\", \"name\": \"B\", \"location\": \"Daggerfall\", \"position\": [0,0,0] }", "f.json: location: must be an object with region and place")]
        [TestCase("{ \"id\": \"b\", \"name\": \"B\", \"location\": { \"region\": \"A\", \"place\": \"B\" }, \"position\": [0,\"x\",0] }", "f.json: position: required, must be [x, y, z]")]
        [TestCase("[1, 2]", "f.json: file: invalid JSON (top level must be an object)")]
        public void WrongShapes_AreRejected(string json, string expected)
        {
            ParseResult r = DefinitionParser.Parse("f.json", json);
            Assert.IsFalse(r.Ok);
            Assert.AreEqual(expected, r.Error);
        }

        [TestCase("", "f.json: file: empty")]
        [TestCase("{ not json", "f.json: file: invalid JSON")]
        public void BrokenFiles_AreRejected(string json, string expectedPrefix)
        {
            ParseResult r = DefinitionParser.Parse("f.json", json);
            Assert.IsFalse(r.Ok);
            StringAssert.StartsWith(expectedPrefix, r.Error);
        }

        [TestCase("{ \"name\": \"B\", \"location\": { \"region\": \"A\", \"place\": \"B\" }, \"position\": [0,0,0] }", "f.json: id: required")]
        [TestCase("{ \"id\": \"Bram\", \"name\": \"B\", \"location\": { \"region\": \"A\", \"place\": \"B\" }, \"position\": [0,0,0] }", "f.json: id: use only lowercase letters, digits and underscore (got \"Bram\")")]
        [TestCase("{ \"id\": \"b\", \"location\": { \"region\": \"A\", \"place\": \"B\" }, \"position\": [0,0,0] }", "f.json: name: required")]
        [TestCase("{ \"id\": \"b\", \"name\": \"B\", \"position\": [0,0,0] }", "f.json: location.region: required")]
        [TestCase("{ \"id\": \"b\", \"name\": \"B\", \"location\": { \"region\": \"A\" }, \"position\": [0,0,0] }", "f.json: location.place: required")]
        [TestCase("{ \"id\": \"b\", \"name\": \"B\", \"location\": { \"region\": \"A\", \"place\": \"B\" } }", "f.json: position: required, must be [x, y, z]")]
        [TestCase("{ \"id\": \"b\", \"name\": \"B\", \"location\": { \"region\": \"A\", \"place\": \"B\" }, \"position\": [0,0] }", "f.json: position: required, must be [x, y, z]")]
        public void MissingOrBadRequiredFields_AreRejected(string json, string expected)
        {
            ParseResult r = DefinitionParser.Parse("f.json", json);
            Assert.IsFalse(r.Ok);
            Assert.AreEqual(expected, r.Error);
        }

        [TestCase("\"baseClass\": \"Spelsword\"", "f.json: baseClass: unknown class \"Spelsword\"")]
        [TestCase("\"baseClass\": \"Knight_CityWatch\"", "f.json: baseClass: unknown class \"Knight_CityWatch\"")]
        [TestCase("\"baseClass\": \"Rat\"", "f.json: baseClass: a creature (\"Rat\") needs \"attitude\": \"hostile\"")]
        [TestCase("\"gender\": \"Other\"", "f.json: gender: must be Male or Female (got \"Other\")")]
        [TestCase("\"bravery\": \"Heroic\"", "f.json: bravery: must be Coward, Normal or Brave (got \"Heroic\")")]
        [TestCase("\"bravery\": \"1\"", "f.json: bravery: must be Coward, Normal or Brave (got \"1\")")]
        [TestCase("\"fleeHealthPercent\": 0", "f.json: fleeHealthPercent: must be 1-99 (got 0)")]
        [TestCase("\"fleeHealthPercent\": 100", "f.json: fleeHealthPercent: must be 1-99 (got 100)")]
        [TestCase("\"calmDownHours\": [5]", "f.json: calmDownHours: must be [min, max]")]
        [TestCase("\"calmDownHours\": [0, 5]", "f.json: calmDownHours: need 0 < min <= max (got [0, 5])")]
        [TestCase("\"calmDownHours\": [9, 5]", "f.json: calmDownHours: need 0 < min <= max (got [9, 5])")]
        [TestCase("\"wanderRadius\": -1", "f.json: wanderRadius: must be 0 or more (got -1)")]
        public void BadOptionalFields_AreRejected(string field, string expected)
        {
            ParseResult r = DefinitionParser.Parse("f.json", With(field));
            Assert.IsFalse(r.Ok);
            Assert.AreEqual(expected, r.Error);
        }
    }
}

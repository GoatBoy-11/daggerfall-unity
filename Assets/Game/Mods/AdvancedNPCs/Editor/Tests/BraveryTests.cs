using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    /// <summary>"bravery", "fleeChance" and "aggroRange" in npc.json (spec lazy-sprites §C-D).</summary>
    public class BraveryTests
    {
        const string Unique =
            "{ \"kind\": \"unique\", \"name\": \"Grub\", " +
            "\"location\": { \"region\": \"Daggerfall\", \"place\": \"Daggerfall\" }, " +
            "\"position\": [1.5, 2, -3] }";

        static ParseResult With(string extraFields)
        {
            return DefinitionParser.ParseFolder("grub", Unique.Substring(0, Unique.Length - 1) + ", " + extraFields + " }");
        }

        [Test]
        public void Defaults_NormalWithThirtyPercent()
        {
            ParseResult r = DefinitionParser.ParseFolder("grub", Unique);
            Assert.IsTrue(r.Ok, r.Error);
            Assert.AreEqual(Bravery.Normal, r.Definition.Bravery);
            Assert.AreEqual(30, r.Definition.FleeChance);
        }

        [Test]
        public void Prudent_Parses()
        {
            ParseResult r = With("\"bravery\": \"prudent\"");
            Assert.IsTrue(r.Ok, r.Error);
            Assert.AreEqual(Bravery.Prudent, r.Definition.Bravery);
        }

        [Test]
        public void FleeChance_Reads()
        {
            ParseResult r = With("\"fleeChance\": 75");
            Assert.IsTrue(r.Ok, r.Error);
            Assert.AreEqual(75, r.Definition.FleeChance);
        }

        [TestCase("-1")]
        [TestCase("101")]
        [TestCase("12.5")]
        [TestCase("\"half\"")]
        public void FleeChance_RejectsBadValues(string value)
        {
            ParseResult r = With("\"fleeChance\": " + value);
            Assert.IsFalse(r.Ok);
            StringAssert.Contains("fleeChance", r.Error);
        }

        [Test]
        public void UnknownBravery_ListsAllFour()
        {
            ParseResult r = With("\"bravery\": \"Reckless\"");
            Assert.IsFalse(r.Ok);
            StringAssert.Contains("Coward, Normal, Prudent or Brave", r.Error);
        }
    }
}

using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class FolderParserTests
    {
        const string Unique =
            "{ \"kind\": \"unique\", \"name\": \"Bram\", " +
            "\"location\": { \"region\": \"Daggerfall\", \"place\": \"Daggerfall\" }, " +
            "\"position\": [1.5, 2, -3] }";

        static string With(string extraFields)
        {
            return Unique.Substring(0, Unique.Length - 1) + ", " + extraFields + " }";
        }

        [Test]
        public void Unique_FolderNameIsId_DefaultsApply()
        {
            ParseResult r = DefinitionParser.ParseFolder("bram", Unique);
            Assert.IsTrue(r.Ok, r.Error);
            NpcDefinition d = r.Definition;
            Assert.AreEqual("bram", d.Id);
            Assert.AreEqual("bram", d.Folder);
            Assert.AreEqual("bram/npc.json", d.SourceFile);
            Assert.AreEqual(NpcKind.Unique, d.Kind);
            Assert.AreEqual("Bram", d.Name);
            Assert.AreEqual(-3f, d.Z);
            Assert.AreEqual("", d.Race);
            Assert.AreEqual(0, d.Portraits.Count);
            Assert.AreEqual("Spellsword", d.BaseClass);
            Assert.AreEqual(8f, d.WanderRadius);
            Assert.AreEqual(0, r.Warnings.Count);
        }

        [Test]
        public void Unique_KindPortraitAndRace_AreRead()
        {
            ParseResult r = DefinitionParser.ParseFolder("bram",
                With("\"kind\": \"Unique\", \"portrait\": \"Bram.png\", \"race\": \"nord\""));
            Assert.IsTrue(r.Ok, r.Error);
            CollectionAssert.AreEqual(new[] { "bram" }, r.Definition.Portraits);
            Assert.AreEqual("Nord", r.Definition.Race);
        }

        [Test]
        public void BadRace_IsRejected()
        {
            ParseResult r = DefinitionParser.ParseFolder("bram", With("\"race\": \"Khajiit\""));
            Assert.AreEqual("bram/npc.json: race: must be Breton, Redguard or Nord (got \"Khajiit\")", r.Error);
        }

        [Test]
        public void BadKind_IsRejected()
        {
            ParseResult r = DefinitionParser.ParseFolder("bram", With("\"kind\": \"boss\""));
            Assert.AreEqual("bram/npc.json: kind: must be unique or generic (got \"boss\")", r.Error);
        }

        [Test]
        public void IdDifferentFromFolder_WarnsAndUsesFolder()
        {
            ParseResult r = DefinitionParser.ParseFolder("bram", With("\"id\": \"old_bram\""));
            Assert.IsTrue(r.Ok, r.Error);
            Assert.AreEqual("bram", r.Definition.Id);
            CollectionAssert.Contains(r.Warnings, "bram/npc.json: id: \"old_bram\" differs from the folder name; using \"bram\"");
        }

        [Test]
        public void IdSameAsFolder_NoWarning()
        {
            ParseResult r = DefinitionParser.ParseFolder("bram", With("\"id\": \"bram\""));
            Assert.IsTrue(r.Ok, r.Error);
            Assert.AreEqual(0, r.Warnings.Count);
        }

        [Test]
        public void UnknownField_Warns()
        {
            ParseResult r = DefinitionParser.ParseFolder("bram", With("\"bravry\": \"Brave\""));
            Assert.IsTrue(r.Ok, r.Error);
            CollectionAssert.Contains(r.Warnings, "bram/npc.json: bravry: unknown field, ignored");
        }

        [Test]
        public void GenericOnlyField_OnUnique_IsRejected()
        {
            ParseResult r = DefinitionParser.ParseFolder("bram", With("\"spawn\": {}"));
            Assert.AreEqual("bram/npc.json: spawn: belongs to generic templates (\"kind\": \"generic\")", r.Error);
        }

        [TestCase("Bram")]
        [TestCase("my npc")]
        [TestCase("_portraits")]
        [TestCase("")]
        public void InvalidFolderName_IsRejected(string folder)
        {
            ParseResult r = DefinitionParser.ParseFolder(folder, Unique);
            Assert.IsFalse(r.Ok);
            StringAssert.StartsWith(folder + ": folder: use only lowercase letters, digits and underscore", r.Error);
        }

        [Test]
        public void MissingName_ReportsFolderPath()
        {
            ParseResult r = DefinitionParser.ParseFolder("bram",
                "{ \"kind\": \"unique\", \"location\": { \"region\": \"R\", \"place\": \"P\" }, \"position\": [0,0,0] }");
            Assert.AreEqual("bram/npc.json: name: required", r.Error);
        }

        [Test]
        public void InvalidJson_ReportsFolderPath()
        {
            ParseResult r = DefinitionParser.ParseFolder("bram", "{ nope");
            StringAssert.StartsWith("bram/npc.json: file: invalid JSON", r.Error);
        }

        [Test]
        public void LegacyParse_StillReadsIdFromFile()
        {
            ParseResult r = DefinitionParser.Parse("bram.json",
                "{ \"id\": \"bram\", \"name\": \"Bram\", \"location\": { \"region\": \"R\", \"place\": \"P\" }, \"position\": [0,0,0] }");
            Assert.IsTrue(r.Ok, r.Error);
            Assert.AreEqual("bram", r.Definition.Id);
            Assert.AreEqual(NpcKind.Unique, r.Definition.Kind);
            Assert.AreEqual("bram.json", r.Definition.SourceFile);
        }

        [Test]
        public void MissingKind_IsGeneric()
        {
            ParseResult r = DefinitionParser.ParseFolder("commoner", "{ \"baseClass\": \"Bard\" }");
            Assert.IsTrue(r.Ok, r.Error);
            Assert.AreEqual(NpcKind.Generic, r.Definition.Kind);
        }

        [Test]
        public void LocationWithoutKind_HintsUnique()
        {
            ParseResult r = DefinitionParser.ParseFolder("bram",
                "{ \"name\": \"Bram\", \"location\": { \"region\": \"R\", \"place\": \"P\" }, \"position\": [0,0,0] }");
            Assert.AreEqual("bram/npc.json: location: belongs to unique ANPCs — add \"kind\": \"unique\"", r.Error);
        }

        [Test]
        public void NameList_IsReadAndNormalized()
        {
            ParseResult r = DefinitionParser.ParseFolder("commoner", "{ \"nameList\": \" Pirates.json \" }");
            Assert.IsTrue(r.Ok, r.Error);
            Assert.AreEqual("pirates", r.Definition.NameList);
            Assert.AreEqual(0, r.Warnings.Count);
        }
    }
}

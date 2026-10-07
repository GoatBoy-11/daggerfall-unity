using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class MigrationTests
    {
        const string Pretty =
            "{\n  \"id\": \"bram\",\n  \"name\": \"Bram\",\n  \"location\": { \"region\": \"R\", \"place\": \"P\" },\n  \"position\": [1, 2, 3]\n}\n";
        const string PrettyWithoutId =
            "{\n  \"kind\": \"unique\",\n  \"name\": \"Bram\",\n  \"location\": { \"region\": \"R\", \"place\": \"P\" },\n  \"position\": [1, 2, 3]\n}\n";

        [Test]
        public void PrettyFile_ReplacesIdWithKind()
        {
            MigrationResult m = Migration.Convert("bram.json", Pretty);
            Assert.IsTrue(m.Ok, m.Error);
            Assert.AreEqual("bram", m.Id);
            Assert.AreEqual(PrettyWithoutId, m.NpcJson);
        }

        [Test]
        public void CrlfFile_KeepsLineEndings()
        {
            MigrationResult m = Migration.Convert("bram.json", Pretty.Replace("\n", "\r\n"));
            Assert.IsTrue(m.Ok, m.Error);
            Assert.AreEqual(PrettyWithoutId.Replace("\n", "\r\n"), m.NpcJson);
        }

        [Test]
        public void IdLast_ReplacedInPlace()
        {
            string json = "{\n  \"name\": \"Bram\",\n  \"location\": { \"region\": \"R\", \"place\": \"P\" },\n  \"position\": [1, 2, 3],\n  \"id\": \"bram\"\n}";
            MigrationResult m = Migration.Convert("bram.json", json);
            Assert.IsTrue(m.Ok, m.Error);
            Assert.AreEqual("{\n  \"name\": \"Bram\",\n  \"location\": { \"region\": \"R\", \"place\": \"P\" },\n  \"position\": [1, 2, 3],\n  \"kind\": \"unique\"\n}", m.NpcJson);
        }

        [Test]
        public void OneLineFile_ReplacedInline()
        {
            MigrationResult m = Migration.Convert("bram.json",
                "{ \"id\": \"bram\", \"name\": \"Bram\", \"location\": { \"region\": \"R\", \"place\": \"P\" }, \"position\": [0,0,0] }");
            Assert.IsTrue(m.Ok, m.Error);
            Assert.AreEqual("{ \"kind\": \"unique\", \"name\": \"Bram\", \"location\": { \"region\": \"R\", \"place\": \"P\" }, \"position\": [0,0,0] }", m.NpcJson);
        }

        [Test]
        public void InvalidFile_IsNotMigrated()
        {
            MigrationResult m = Migration.Convert("bad.json", "{ \"id\": \"bad\" }");
            Assert.IsFalse(m.Ok);
            Assert.AreEqual("bad.json: name: required", m.Error);
        }

        [Test]
        public void Result_ParsesAsFolderWithoutWarnings()
        {
            MigrationResult m = Migration.Convert("bram.json", Pretty);
            ParseResult r = DefinitionParser.ParseFolder(m.Id, m.NpcJson);
            Assert.IsTrue(r.Ok, r.Error);
            Assert.AreEqual(0, r.Warnings.Count);
            Assert.AreEqual("bram", r.Definition.Id);
            Assert.AreEqual(3f, r.Definition.Z);
        }
    }
}

using System.Collections.Generic;
using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class DefinitionCatalogTests
    {
        static string Unique(string region, string place)
        {
            return "{ \"kind\": \"unique\", \"name\": \"N\", \"location\": { \"region\": \"" + region +
                   "\", \"place\": \"" + place + "\" }, \"position\": [0,0,0] }";
        }

        static string UniqueWithPortrait(string portrait)
        {
            return "{ \"kind\": \"unique\", \"name\": \"N\", \"location\": { \"region\": \"R\", \"place\": \"P\" }, \"position\": [0,0,0], " +
                   "\"portrait\": \"" + portrait + "\" }";
        }

        static AnpcFolder Folder(string name, string npcJson)
        {
            return new AnpcFolder(name, npcJson, null);
        }

        [Test]
        public void FolderDialogue_ParsedWithMessages_BadFileKeepsNpc()
        {
            DefinitionCatalog c = DefinitionCatalog.Build(new[]
            {
                new AnpcFolder("a", Unique("R", "P"), "{ \"topics\": [ { \"caption\": \"Ale\", \"answers\": [\"Hi {playr}\"] } ] }"),
                new AnpcFolder("b", Unique("R", "P"), "{ broken"),
            });
            Assert.AreEqual("Ale", c.ById["a"].OwnDialogue.Topics[0].Caption);
            Assert.IsNull(c.ById["b"].OwnDialogue);
            Assert.IsTrue(c.ById.ContainsKey("b"));
            Assert.IsTrue(c.Messages.Exists(delegate (string m) { return m.StartsWith("a/dialogue.json: topic \"Ale\": answers: answer 1: unknown placeholder {playr}"); }));
            Assert.IsTrue(c.Messages.Exists(delegate (string m) { return m.StartsWith("b/dialogue.json: file: invalid JSON"); }));
        }

        [Test]
        public void ValidFolders_AreIndexedById()
        {
            DefinitionCatalog c = DefinitionCatalog.Build(new[]
            {
                Folder("a", Unique("R", "P")),
                Folder("b", Unique("R", "P")),
            });
            Assert.AreEqual(2, c.Count);
            Assert.IsTrue(c.ById.ContainsKey("a"));
            Assert.IsTrue(c.ById.ContainsKey("b"));
            Assert.AreEqual(0, c.Generics.Count);
            CollectionAssert.Contains(c.Messages, "Loaded 2 unique ANPC(s) and 0 generic template(s).");
        }

        [Test]
        public void BadFolder_IsSkippedAndLogged_OthersStillLoad()
        {
            DefinitionCatalog c = DefinitionCatalog.Build(new[]
            {
                Folder("bad", "{ nope"),
                Folder("good", Unique("R", "P")),
            });
            Assert.AreEqual(1, c.Count);
            Assert.IsTrue(c.Messages.Exists(delegate (string m) { return m.StartsWith("bad/npc.json: file: invalid JSON"); }));
        }

        [Test]
        public void InvalidFolderName_IsReported_OthersStillLoad()
        {
            DefinitionCatalog c = DefinitionCatalog.Build(new[]
            {
                Folder("My NPC", Unique("R", "P")),
                Folder("good", Unique("R", "P")),
            });
            Assert.AreEqual(1, c.Count);
            Assert.IsTrue(c.ById.ContainsKey("good"));
            Assert.IsTrue(c.Messages.Exists(delegate (string m) { return m.StartsWith("My NPC: folder: use only lowercase letters"); }));
        }

        [Test]
        public void UnderscoreFolders_AreSharedDataNotNpcs()
        {
            DefinitionCatalog c = DefinitionCatalog.Build(new[] { Folder("_Portraits", null) });
            Assert.AreEqual(0, c.Count);
            CollectionAssert.AreEqual(new[] { "Loaded 0 unique ANPC(s) and 0 generic template(s)." }, c.Messages);
        }

        [Test]
        public void FolderWithoutNpcJson_IsSkippedWithWarning()
        {
            DefinitionCatalog c = DefinitionCatalog.Build(new[] { Folder("empty", null) });
            Assert.AreEqual(0, c.Count);
            CollectionAssert.Contains(c.Messages, "empty/npc.json: file: missing, folder skipped");
        }

        [Test]
        public void ParserWarnings_AreCollected()
        {
            string json = "{ \"kind\": \"unique\", \"name\": \"N\", \"location\": { \"region\": \"R\", \"place\": \"P\" }, \"position\": [0,0,0], \"colour\": \"red\" }";
            DefinitionCatalog c = DefinitionCatalog.Build(new[] { Folder("a", json) });
            Assert.AreEqual(1, c.Count);
            CollectionAssert.Contains(c.Messages, "a/npc.json: colour: unknown field, ignored");
        }

        [Test]
        public void ReferencedPortraits_AreDistinctAndSorted()
        {
            DefinitionCatalog c = DefinitionCatalog.Build(new[]
            {
                Folder("a", UniqueWithPortrait("z")),
                Folder("b", UniqueWithPortrait("A.png")),
                Folder("c", UniqueWithPortrait("z")),
                Folder("d", Unique("R", "P")),
            });
            CollectionAssert.AreEqual(new[] { "a", "z" }, c.ReferencedPortraits());
        }

        [Test]
        public void ForLocation_IgnoresCaseAndSpaces()
        {
            DefinitionCatalog c = DefinitionCatalog.Build(new[]
            {
                Folder("a", Unique("Daggerfall", "Daggerfall")),
                Folder("b", Unique("Wayrest", "Wayrest")),
            });
            List<NpcDefinition> found = c.ForLocation("daggerfall ", " DAGGERFALL");
            Assert.AreEqual(1, found.Count);
            Assert.AreEqual("a", found[0].Id);
        }

        [Test]
        public void ForLocation_NullNames_ReturnEmpty()
        {
            DefinitionCatalog c = DefinitionCatalog.Build(new[] { Folder("a", Unique("R", "P")) });
            Assert.AreEqual(0, c.ForLocation(null, null).Count);
        }

        [Test]
        public void NoFolders_IsEmptyNotError()
        {
            DefinitionCatalog c = DefinitionCatalog.Build(new AnpcFolder[0]);
            Assert.AreEqual(0, c.Count);
            CollectionAssert.Contains(c.Messages, "Loaded 0 unique ANPC(s) and 0 generic template(s).");
        }
    }
}

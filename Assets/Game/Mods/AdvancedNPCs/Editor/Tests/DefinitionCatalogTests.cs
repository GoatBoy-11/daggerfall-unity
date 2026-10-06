using System.Collections.Generic;
using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class DefinitionCatalogTests
    {
        static string Npc(string id, string region, string place)
        {
            return "{ \"id\": \"" + id + "\", \"name\": \"N\", \"location\": { \"region\": \"" + region +
                   "\", \"place\": \"" + place + "\" }, \"position\": [0,0,0] }";
        }

        static KeyValuePair<string, string> File(string name, string text)
        {
            return new KeyValuePair<string, string>(name, text);
        }

        [Test]
        public void ValidFiles_AreIndexedById()
        {
            DefinitionCatalog c = DefinitionCatalog.Build(new[]
            {
                File("a.json", Npc("a", "R", "P")),
                File("b.json", Npc("b", "R", "P")),
            });
            Assert.AreEqual(2, c.Count);
            Assert.IsTrue(c.ById.ContainsKey("a"));
            Assert.IsTrue(c.ById.ContainsKey("b"));
            CollectionAssert.Contains(c.Messages, "Loaded 2 NPC definition(s).");
        }

        [Test]
        public void BadFile_IsSkippedAndLogged_OthersStillLoad()
        {
            DefinitionCatalog c = DefinitionCatalog.Build(new[]
            {
                File("bad.json", "{ nope"),
                File("good.json", Npc("good", "R", "P")),
            });
            Assert.AreEqual(1, c.Count);
            Assert.IsTrue(c.Messages.Exists(m => m.StartsWith("bad.json: file: invalid JSON")));
        }

        [Test]
        public void DuplicateId_FirstAlphabeticalWins()
        {
            DefinitionCatalog c = DefinitionCatalog.Build(new[]
            {
                File("z.json", Npc("same", "R", "Second")),
                File("a.json", Npc("same", "R", "First")),
            });
            Assert.AreEqual(1, c.Count);
            Assert.AreEqual("First", c.ById["same"].Place);
            CollectionAssert.Contains(c.Messages, "z.json: id: duplicate \"same\" (already defined in a.json), skipped");
        }

        [Test]
        public void ForLocation_IgnoresCaseAndSpaces()
        {
            DefinitionCatalog c = DefinitionCatalog.Build(new[]
            {
                File("a.json", Npc("a", "Daggerfall", "Daggerfall")),
                File("b.json", Npc("b", "Wayrest", "Wayrest")),
            });
            List<NpcDefinition> found = c.ForLocation("daggerfall ", " DAGGERFALL");
            Assert.AreEqual(1, found.Count);
            Assert.AreEqual("a", found[0].Id);
        }

        [Test]
        public void ForLocation_NullNames_ReturnEmpty()
        {
            DefinitionCatalog c = DefinitionCatalog.Build(new[] { File("a.json", Npc("a", "R", "P")) });
            Assert.AreEqual(0, c.ForLocation(null, null).Count);
        }

        [Test]
        public void NoFiles_IsEmptyNotError()
        {
            DefinitionCatalog c = DefinitionCatalog.Build(new KeyValuePair<string, string>[0]);
            Assert.AreEqual(0, c.Count);
            CollectionAssert.Contains(c.Messages, "Loaded 0 NPC definition(s).");
        }
    }
}

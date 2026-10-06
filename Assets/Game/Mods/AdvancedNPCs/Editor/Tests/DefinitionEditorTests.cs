using System.Globalization;
using System.Threading;
using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class DefinitionEditorTests
    {
        const string File =
            "{\n" +
            "  \"id\": \"bram\",\n" +
            "  \"name\": \"Bram\",\n" +
            "  \"location\": { \"region\": \"Daggerfall\", \"place\": \"Daggerfall\" },\n" +
            "  \"position\": [6, 1, 6],\n" +
            "  \"bravery\": \"Brave\"\n" +
            "}\n";

        [Test]
        public void ReplacesLocationAndPosition_KeepsEverythingElse()
        {
            string edited = DefinitionEditor.SetPlacement(File, "Wayrest", "Wayrest", 12.5f, 0.96f, -3.25f);
            StringAssert.Contains("\"location\": { \"region\": \"Wayrest\", \"place\": \"Wayrest\" }", edited);
            StringAssert.Contains("\"position\": [12.5, 0.96, -3.25]", edited);
            StringAssert.Contains("\"bravery\": \"Brave\"", edited);
            StringAssert.DoesNotContain("[6, 1, 6]", edited);
        }

        [Test]
        public void EditedFile_ParsesWithNewPlacement()
        {
            string edited = DefinitionEditor.SetPlacement(File, "Wayrest", "Wayrest", 12.5f, 0.96f, -3.25f);
            ParseResult r = DefinitionParser.Parse("bram.json", edited);
            Assert.IsTrue(r.Ok, r.Error);
            Assert.AreEqual("Wayrest", r.Definition.Region);
            Assert.AreEqual(12.5f, r.Definition.X);
            Assert.AreEqual(-3.25f, r.Definition.Z);
            Assert.AreEqual(Bravery.Brave, r.Definition.Bravery);
        }

        [Test]
        public void MissingKeys_AreAdded()
        {
            string edited = DefinitionEditor.SetPlacement("{ \"id\": \"bram\", \"name\": \"Bram\" }", "R", "P", 1f, 2f, 3f);
            ParseResult r = DefinitionParser.Parse("bram.json", edited);
            Assert.IsTrue(r.Ok, r.Error);
            Assert.AreEqual("R", r.Definition.Region);
            Assert.AreEqual(3f, r.Definition.Z);
        }

        [Test]
        public void MultiLineLocation_IsReplaced()
        {
            string json = "{ \"id\": \"bram\", \"name\": \"Bram\",\n  \"location\": {\n    \"region\": \"A\",\n    \"place\": \"B\"\n  },\n  \"position\": [\n 1,\n 2,\n 3\n ] }";
            string edited = DefinitionEditor.SetPlacement(json, "R", "P", 4f, 5f, 6f);
            ParseResult r = DefinitionParser.Parse("bram.json", edited);
            Assert.IsTrue(r.Ok, r.Error);
            Assert.AreEqual("P", r.Definition.Place);
            Assert.AreEqual(4f, r.Definition.X);
        }

        [Test]
        public void UsesDotUnderCommaCulture()
        {
            CultureInfo old = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                StringAssert.Contains("[12.5, 0, 0]", DefinitionEditor.SetPlacement(File, "R", "P", 12.5f, 0f, 0f));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = old;
            }
        }
    }
}

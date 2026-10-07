using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class NameListTests
    {
        static string Sets(params string[] parts)
        {
            string s = "";
            foreach (string p in parts)
                s += (s.Length > 0 ? ", " : "") + "{ \"parts\": [\"" + p + "\"] }";
            return "[" + s + "]";
        }

        static NameList Vanilla(string style, params string[] parts)
        {
            NameListResult r = NameListParser.Parse("x", "{ \"style\": \"" + style + "\", \"sets\": " + Sets(parts) + " }");
            Assert.IsNull(r.Error, r.Error);
            return r.List;
        }

        static NameList Simple(string json)
        {
            NameListResult r = NameListParser.Parse("x", json);
            Assert.IsNull(r.Error, r.Error);
            return r.List;
        }

        [Test]
        public void Vanilla_Parses()
        {
            NameList l = Vanilla("Nord", "A", "b", "C", "d");
            Assert.AreEqual("x", l.Name);
            Assert.AreEqual("nord", l.Style);
            Assert.IsFalse(l.IsSimple);
            Assert.AreEqual(4, l.Sets.Count);
        }

        [Test]
        public void UnknownStyle_IsRejected()
        {
            NameListResult r = NameListParser.Parse("x", "{ \"style\": \"orc\", \"sets\": " + Sets("A") + " }");
            Assert.AreEqual("_Namelists/x.json: style: must be one of breton, redguard, nord, darkelf, highelf, woodelf, khajiit, imperial (got \"orc\")", r.Error);
        }

        [Test]
        public void TooFewSets_IsRejected()
        {
            NameListResult r = NameListParser.Parse("x", "{ \"style\": \"breton\", \"sets\": " + Sets("A", "b") + " }");
            Assert.AreEqual("_Namelists/x.json: sets: style breton needs 6 sets (got 2)", r.Error);
        }

        [Test]
        public void NullSets_IsRejected()
        {
            NameListResult r = NameListParser.Parse("x", "{ \"style\": \"nord\", \"sets\": null }");
            Assert.AreEqual("_Namelists/x.json: sets: must be a list of objects", r.Error);
        }

        [Test]
        public void EmptySet_IsRejected()
        {
            NameListResult r = NameListParser.Parse("x",
                "{ \"style\": \"nord\", \"sets\": [ { \"parts\": [\"A\"] }, { \"parts\": [] }, { \"parts\": [\"C\"] }, { \"parts\": [\"d\"] } ] }");
            Assert.AreEqual("_Namelists/x.json: sets[1]: needs at least one part", r.Error);
        }

        [Test]
        public void Simple_Parses()
        {
            NameList l = Simple("{ \"male\": [\"Ulf\"], \"female\": [\"Astrid\"], \"surnames\": [\"Stormborn\"] }");
            Assert.IsTrue(l.IsSimple);
            CollectionAssert.AreEqual(new[] { "Ulf" }, l.Male);
            CollectionAssert.AreEqual(new[] { "Astrid" }, l.Female);
            CollectionAssert.AreEqual(new[] { "Stormborn" }, l.Surnames);
        }

        [Test]
        public void Simple_OneGenderEnough()
        {
            Assert.AreEqual(0, Simple("{ \"female\": [\"Astrid\"] }").Male.Count);
        }

        [Test]
        public void Simple_NoNames_IsRejected()
        {
            NameListResult r = NameListParser.Parse("x", "{ \"male\": [], \"surnames\": [\"Stormborn\"] }");
            Assert.AreEqual("_Namelists/x.json: male: needs at least one name in male or female", r.Error);
        }

        [Test]
        public void Neither_IsRejected()
        {
            NameListResult r = NameListParser.Parse("x", "{ \"names\": [\"Ulf\"] }");
            Assert.AreEqual("_Namelists/x.json: file: needs \"sets\" (vanilla format) or \"male\"/\"female\" (simple format)", r.Error);
        }

        [Test]
        public void InvalidJson_IsRejected()
        {
            StringAssert.StartsWith("_Namelists/x.json: file: invalid JSON", NameListParser.Parse("x", "{ nope").Error);
        }

        [Test]
        public void Normalize()
        {
            Assert.AreEqual("pirates", NameListParser.Normalize(" Pirates.JSON "));
            Assert.AreEqual("", NameListParser.Normalize(null));
        }

        [Test]
        public void RequiredSets()
        {
            Assert.AreEqual(6, NameListParser.RequiredSets("breton"));
            Assert.AreEqual(4, NameListParser.RequiredSets("nord"));
            Assert.AreEqual(5, NameListParser.RequiredSets("redguard"));
            Assert.AreEqual(0, NameListParser.RequiredSets("orc"));
        }

        [Test]
        public void Generate_StandardStyle()
        {
            NameList l = Vanilla("breton", "A", "b", "C", "d", "E", "f");
            Assert.AreEqual("Ab Ef", NameGenerator.Generate(l, "Male", new SeededRandom(1), "sen"));
            Assert.AreEqual("Cd Ef", NameGenerator.Generate(l, "Female", new SeededRandom(1), "sen"));
        }

        [Test]
        public void Generate_NordSurname()
        {
            NameList l = Vanilla("nord", "A", "b", "C", "d");
            Assert.AreEqual("Ab Absen", NameGenerator.Generate(l, "Male", new SeededRandom(1), "sen"));
            Assert.AreEqual("Cd Absen", NameGenerator.Generate(l, "Female", new SeededRandom(1), "sen"));
        }

        [Test]
        public void Generate_RedguardSingleName()
        {
            NameList l = Vanilla("redguard", "A", "b", "c", "D", "E");
            Assert.AreEqual("AbcE", NameGenerator.Generate(l, "Female", new SeededRandom(1), "sen"));
            for (uint seed = 1; seed < 20; seed++)
            {
                string male = NameGenerator.Generate(l, "Male", new SeededRandom(seed), "sen");
                Assert.IsTrue(male == "Abc" || male == "AbcD", male);
            }
        }

        [Test]
        public void Generate_Simple()
        {
            NameList l = Simple("{ \"male\": [\"Ulf\"], \"surnames\": [\"Stormborn\"] }");
            Assert.AreEqual("Ulf Stormborn", NameGenerator.Generate(l, "Female", new SeededRandom(1), "sen"));
            NameList noSurname = Simple("{ \"female\": [\"Astrid\"] }");
            Assert.AreEqual("Astrid", NameGenerator.Generate(noSurname, "Male", new SeededRandom(1), "sen"));
        }

        [Test]
        public void Generate_SimpleWithGenderedSurnamePrefix()
        {
            NameList l = Simple("{ \"male\": [\"Gharol\"], \"female\": [\"Shel\"], \"surnames\": [\"Rugdush\"], " +
                                "\"maleSurnamePrefix\": \"gro-\", \"femaleSurnamePrefix\": \"gra-\" }");
            Assert.AreEqual("Gharol gro-Rugdush", NameGenerator.Generate(l, "Male", new SeededRandom(1), "sen"));
            Assert.AreEqual("Shel gra-Rugdush", NameGenerator.Generate(l, "Female", new SeededRandom(1), "sen"));
        }

        [Test]
        public void SurnamePrefix_MustBeText()
        {
            NameListResult r = NameListParser.Parse("x", "{ \"male\": [\"Gharol\"], \"maleSurnamePrefix\": 3 }");
            Assert.AreEqual("_Namelists/x.json: maleSurnamePrefix: must be text", r.Error);
        }
    }
}

using System.Collections.Generic;
using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    /// <summary>"replaces": generic ANPCs standing in for vanilla flat NPCs.</summary>
    public class ReplaceRulesTests
    {
        static ParseResult Generic(string extra)
        {
            return DefinitionParser.ParseFolder("bald_mage", "{ \"kind\": \"generic\"" + (extra.Length > 0 ? ", " + extra : "") + " }");
        }

        [Test]
        public void ParsesSpriteAndFactionIds()
        {
            ParseResult r = Generic("\"replaces\": [\"177.2\", \"177 3\", \"faction:1003\"]");
            Assert.IsTrue(r.Ok, r.Error);
            ReplaceSettings s = r.Definition.Replace;
            Assert.AreEqual(3, s.Targets.Count);
            Assert.IsTrue(s.Matches(177, 2, 0));
            Assert.IsTrue(s.Matches(177, 3, 0));
            Assert.IsTrue(s.Matches(1, 1, 1003));
            Assert.IsFalse(s.Matches(177, 4, 0));
            Assert.IsTrue(s.Portrait, "portrait replaced by default");
            Assert.IsFalse(s.Name, "vanilla name kept by default");
            Assert.AreEqual(0f, s.StayNear);
        }

        [Test]
        public void SingleIdAndSettings()
        {
            ParseResult r = Generic("\"replaces\": \"177_2\", \"replacePortrait\": false, \"replaceName\": true, \"stayNear\": 3");
            Assert.IsTrue(r.Ok, r.Error);
            Assert.IsTrue(r.Definition.Replace.Matches(177, 2, 0));
            Assert.IsFalse(r.Definition.Replace.Portrait);
            Assert.IsTrue(r.Definition.Replace.Name);
            Assert.AreEqual(3f, r.Definition.Replace.StayNear);
        }

        [Test]
        public void NoSpawnBlock_OnlyStandsIn()
        {
            Assert.IsFalse(Generic("\"replaces\": \"177.2\"").Definition.Spawn.HasTownRules);
            Assert.IsTrue(Generic("").Definition.Spawn.HasTownRules);
            Assert.IsNull(Generic("").Definition.Replace);
        }

        [Test]
        public void Rejects_BadIdsAndStrayFields()
        {
            Assert.IsFalse(Generic("\"replaces\": \"bald man\"").Ok);
            Assert.IsFalse(Generic("\"replaces\": []").Ok);
            Assert.IsFalse(Generic("\"replaces\": \"177.2\", \"stayNear\": -1").Ok);
            Assert.IsFalse(Generic("\"stayNear\": 2").Ok, "stayNear without replaces");
            ParseResult unique = DefinitionParser.ParseFolder("one", "{ \"kind\": \"unique\", \"name\": \"A\", " +
                "\"location\": { \"region\": \"Daggerfall\", \"place\": \"Daggerfall\" }, \"position\": [0, 0, 0], \"replaces\": \"177.2\" }");
            Assert.IsFalse(unique.Ok);
        }

        [Test]
        public void ReplacesFlat_ParsesAndNormalises()
        {
            ParseResult r = Generic("\"replacesFlat\": [\"1234/b5678/-90\", \" 7 / Town / 12 \"]");
            Assert.IsTrue(r.Ok, r.Error);
            Assert.AreEqual(new[] { "1234/b5678/-90", "7/town/12" }, r.Definition.Replace.Flats.ToArray());
            Assert.AreEqual(0, r.Definition.Replace.Targets.Count, "replacesFlat alone needs no replaces");
            Assert.IsTrue(Generic("\"replacesFlat\": \"3/dungeon/4\", \"stayNear\": 2").Ok, "settings work with replacesFlat alone");
            Assert.IsFalse(Generic("\"replacesFlat\": \"177.2\"").Ok);
            Assert.IsFalse(Generic("\"replacesFlat\": \"1/inn/2\"").Ok);
            Assert.IsFalse(Generic("\"replacesFlat\": []").Ok);
            Assert.IsFalse(Generic("\"replacesFlat\": \"1/town/2\"").Definition.Spawn.HasTownRules, "no spawn block: only stands in");
            Assert.IsFalse(Generic("\"replaces\": [], \"replacesFlat\": \"1/town/2\"").Ok);
            Assert.AreEqual("replace@" + ReplaceRules.FlatId(5, "b12", 777), ReplaceRules.KeyFor(5, "b12", 777), "save key unchanged");
        }

        [Test]
        public void ReplacesFlat_WinsOverSpriteAndFaction()
        {
            NpcDefinition sprite = Generic("\"replaces\": [\"177.2\", \"faction:1003\"]").Definition;
            sprite.Id = "a_sprite";
            NpcDefinition one = Generic("\"replacesFlat\": \"5/b12/777\"").Definition;
            one.Id = "z_one";
            NpcDefinition two = Generic("\"replaces\": \"177.9\", \"replacesFlat\": \"5/b12/777\"").Definition;
            two.Id = "y_two";
            List<NpcDefinition> templates = new List<NpcDefinition> { sprite, one, two };

            List<NpcDefinition> exact = ReplaceRules.Candidates(templates, 177, 2, 1003, "5/b12/777");
            Assert.AreEqual(new[] { "y_two", "z_one" }, exact.ConvertAll(d => d.Id).ToArray(), "only the flat's own replacers, random between them");
            List<NpcDefinition> other = ReplaceRules.Candidates(templates, 177, 2, 0, "5/b12/778");
            Assert.AreEqual(new[] { "a_sprite" }, other.ConvertAll(d => d.Id).ToArray(), "other flats fall back to sprite ids");
            Assert.AreEqual(0, ReplaceRules.Candidates(templates, 1, 1, 0, "5/b12/778").Count);
        }

        [Test]
        public void Pick_IsFixedPerFlat_AndSpreadsAcrossFlats()
        {
            List<NpcDefinition> templates = new List<NpcDefinition>();
            foreach (string id in new[] { "b", "a", "c" })
            {
                NpcDefinition d = Generic("\"replaces\": \"177.2\"").Definition;
                d.Id = id;
                templates.Add(d);
            }
            List<NpcDefinition> c = ReplaceRules.Candidates(templates, 177, 2, 0);
            Assert.AreEqual("a", c[0].Id, "sorted by id");
            Assert.AreEqual(0, ReplaceRules.Candidates(templates, 177, 1, 0).Count);
            string key = ReplaceRules.KeyFor(5, "b12", 777);
            Assert.AreSame(ReplaceRules.Pick(c, key), ReplaceRules.Pick(c, key));
            HashSet<string> seen = new HashSet<string>();
            for (int h = 0; h < 60; h++)
                seen.Add(ReplaceRules.Pick(c, ReplaceRules.KeyFor(5, "b12", h)).Id);
            Assert.AreEqual(3, seen.Count);
        }
    }
}

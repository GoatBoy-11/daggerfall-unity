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

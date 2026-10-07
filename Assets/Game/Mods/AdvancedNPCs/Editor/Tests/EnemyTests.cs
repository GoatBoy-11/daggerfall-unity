using System.Collections.Generic;
using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class EnemyTests
    {
        const string Unique =
            "{ \"kind\": \"unique\", \"name\": \"Bram\", " +
            "\"location\": { \"region\": \"Daggerfall\", \"place\": \"Daggerfall\" }, " +
            "\"position\": [1.5, 2, -3] }";

        static ParseResult With(string extraFields)
        {
            return DefinitionParser.ParseFolder("bram", Unique.Substring(0, Unique.Length - 1) + ", " + extraFields + " }");
        }

        [Test]
        public void Calm_IsTheDefault()
        {
            ParseResult r = DefinitionParser.ParseFolder("bram", Unique);
            Assert.IsTrue(r.Ok, r.Error);
            Assert.IsFalse(r.Definition.Hostile);
            Assert.IsFalse(r.Definition.IsCreature);
            Assert.AreEqual(-1, r.Definition.HostileFrom);
            Assert.AreEqual(-1, r.Definition.HostileTo);
        }

        [Test]
        public void Hostile_AlwaysAndWithHours()
        {
            ParseResult always = With("\"attitude\": \"Hostile\"");
            Assert.IsTrue(always.Ok, always.Error);
            Assert.IsTrue(always.Definition.Hostile);
            Assert.AreEqual(-1, always.Definition.HostileFrom);

            ParseResult night = With("\"attitude\": \"hostile\", \"hostileHours\": [20, 6]");
            Assert.IsTrue(night.Ok, night.Error);
            Assert.AreEqual(20, night.Definition.HostileFrom);
            Assert.AreEqual(6, night.Definition.HostileTo);
        }

        [Test]
        public void Creature_NeedsHostile_AndIsCanonical()
        {
            ParseResult orc = With("\"attitude\": \"hostile\", \"baseClass\": \"orcshaman\"");
            Assert.IsTrue(orc.Ok, orc.Error);
            Assert.AreEqual("OrcShaman", orc.Definition.BaseClass);
            Assert.IsTrue(orc.Definition.IsCreature);
        }

        [TestCase("\"attitude\": \"angry\"", "bram/npc.json: attitude: must be calm or hostile (got \"angry\")")]
        [TestCase("\"hostileHours\": [20, 6]", "bram/npc.json: hostileHours: only with \"attitude\": \"hostile\"")]
        [TestCase("\"attitude\": \"hostile\", \"hostileHours\": [20]", "bram/npc.json: hostileHours: must be [from, to], whole hours 0-23")]
        [TestCase("\"attitude\": \"hostile\", \"hostileHours\": [20, 24]", "bram/npc.json: hostileHours: must be [from, to], whole hours 0-23")]
        [TestCase("\"attitude\": \"hostile\", \"hostileHours\": [1.5, 6]", "bram/npc.json: hostileHours: must be [from, to], whole hours 0-23")]
        [TestCase("\"baseClass\": \"Orc\"", "bram/npc.json: baseClass: a creature (\"Orc\") needs \"attitude\": \"hostile\"")]
        [TestCase("\"attitude\": \"hostile\", \"hostileHours\": [20, 6], \"baseClass\": \"Orc\"", "bram/npc.json: baseClass: a creature (\"Orc\") is always hostile; remove hostileHours")]
        [TestCase("\"attitude\": \"hostile\", \"baseClass\": \"Horse_Invalid\"", "bram/npc.json: baseClass: unknown class \"Horse_Invalid\"")]
        [TestCase("\"attitude\": \"hostile\", \"baseClass\": \"Knight_CityWatch\"", "bram/npc.json: baseClass: unknown class \"Knight_CityWatch\"")]
        public void BadEnemyFields_AreRejected(string fields, string expected)
        {
            ParseResult r = With(fields);
            Assert.IsFalse(r.Ok);
            Assert.AreEqual(expected, r.Error);
        }

        [TestCase(false, -1, -1, 12, false, false, false)]
        [TestCase(true, -1, -1, 12, false, false, true)]
        [TestCase(true, 20, 6, 22, false, false, true)]
        [TestCase(true, 20, 6, 3, false, false, true)]
        [TestCase(true, 20, 6, 6, false, false, false)]
        [TestCase(true, 20, 6, 12, false, false, false)]
        [TestCase(true, 20, 6, 20, false, false, true)]
        [TestCase(true, 9, 17, 9, false, false, true)]
        [TestCase(true, 9, 17, 17, false, false, false)]
        [TestCase(true, 5, 5, 14, false, false, true)]
        [TestCase(false, -1, -1, 12, true, false, true)]
        [TestCase(true, -1, -1, 12, false, true, false)]
        [TestCase(false, -1, -1, 12, true, true, false)]
        public void IsEnemyNow(bool hostile, int from, int to, int hour, bool forcedOn, bool forcedOff, bool expected)
        {
            Assert.AreEqual(expected, EnemyRules.IsEnemyNow(hostile, from, to, hour, forcedOn, forcedOff));
        }

        [Test]
        public void Creatures_ListMatchesDfuMonsters()
        {
            Assert.AreEqual("SkeletalWarrior", Creatures.Canonical(" skeletalwarrior "));
            Assert.AreEqual("Dragonling_Alternate", Creatures.Canonical("Dragonling_Alternate"));
            Assert.IsNull(Creatures.Canonical("Horse_Invalid"));
            Assert.IsNull(Creatures.Canonical("Spellsword"));
            Assert.AreEqual(42, Creatures.Names.Length);
        }

        [Test]
        public void EnemySwitches_AreSavedState()
        {
            NpcState s = new NpcState();
            Assert.IsTrue(s.IsDefault());
            s.enemyOn = true;
            Assert.IsFalse(s.IsDefault());
            NpcState c = s.Clone();
            Assert.IsTrue(c.enemyOn);
            s.enemyOn = false;
            s.enemyOff = true;
            Assert.IsFalse(s.IsDefault());
            Assert.IsTrue(s.Clone().enemyOff);
        }
    }
}

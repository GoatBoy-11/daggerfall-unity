using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class NpcMagicTests
    {
        static ParseResult Parse(string fields)
        {
            return DefinitionParser.ParseFolder("mage", "{\"magic\":{" + fields + "}}");
        }

        [Test]
        public void OmittedMagicKeepsVanillaAndExplicitEmptyBookDisablesSpells()
        {
            Assert.IsNull(DefinitionParser.ParseFolder("mage", "{}").Definition.Magic);
            ParseResult r = Parse("\"spells\":[]");
            Assert.IsTrue(r.Ok, r.Error);
            Assert.AreEqual(0, r.Definition.Magic.Spells.Count);
            Assert.IsTrue(r.Definition.Magic.UsesMagicka);
            Assert.AreEqual(150, r.Definition.Magic.MaxMagicka);
            Assert.AreEqual("onAreaEntry", r.Definition.Magic.Recovery);
            Assert.AreEqual(4, r.Definition.Magic.CooldownMin);
            Assert.AreEqual(7, r.Definition.Magic.CooldownMax);
        }

        [Test]
        public void FullConfigurationTrimsAndDeduplicatesNames()
        {
            ParseResult r = Parse("\"spells\":[\" Fireball \",\"fireball\",\"Heal\"],\"usesMagicka\":false," +
                "\"maxMagicka\":0,\"castCooldown\":[2.5,9],\"recovery\":\"Rest\",\"restHours\":4,\"skill\":55");
            Assert.IsTrue(r.Ok, r.Error);
            NpcMagicDefinition d = r.Definition.Magic;
            CollectionAssert.AreEqual(new[] { "Fireball", "Heal" }, d.Spells);
            Assert.IsFalse(d.UsesMagicka);
            Assert.AreEqual(0, d.MaxMagicka);
            Assert.AreEqual(2.5, d.CooldownMin);
            Assert.AreEqual(9, d.CooldownMax);
            Assert.AreEqual("rest", d.Recovery);
            Assert.AreEqual(4, d.RestHours);
            Assert.AreEqual(55, d.Skill);
        }

        [TestCase("\"spells\":\"Fireball\"")]
        [TestCase("\"spells\":[\"\"]")]
        [TestCase("\"spells\":[14]")]
        [TestCase("\"spells\":[],\"maxMagicka\":-1")]
        [TestCase("\"spells\":[],\"maxMagicka\":1.5")]
        [TestCase("\"spells\":[],\"maxMagicka\":1000001")]
        [TestCase("\"spells\":[],\"usesMagicka\":\"false\"")]
        [TestCase("\"spells\":[],\"castCooldown\":[0,2]")]
        [TestCase("\"spells\":[],\"castCooldown\":[5,2]")]
        [TestCase("\"spells\":[],\"castCooldown\":[2]")]
        [TestCase("\"spells\":[],\"castCooldown\":[2,3601]")]
        [TestCase("\"spells\":[],\"recovery\":\"instantly\"")]
        [TestCase("\"spells\":[],\"restHours\":0")]
        [TestCase("\"spells\":[],\"skill\":101")]
        [TestCase("\"spells\":[],\"skill\":0")]
        [TestCase("")]
        public void InvalidSettingsExplainTheMagicField(string fields)
        {
            ParseResult r = Parse(fields);
            Assert.IsFalse(r.Ok);
            StringAssert.Contains("mage/npc.json: magic", r.Error);
        }

        [Test]
        public void UnknownNestedFieldsWarn()
        {
            ParseResult r = Parse("\"spells\":[],\"castCooldon\":[4,7]");
            Assert.IsTrue(r.Ok, r.Error);
            Assert.AreEqual(1, r.Warnings.Count);
            StringAssert.Contains("magic.castCooldon", r.Warnings[0]);
        }

        [TestCase(true, 9, 10, false)]
        [TestCase(true, 10, 10, true)]
        [TestCase(true, 0, 10, false)]
        [TestCase(false, 0, 10, true)]
        [TestCase(false, 0, -1, false)]
        public void FullCostIsRequiredUnlessUnlimited(bool uses, int current, int cost, bool expected)
        {
            Assert.AreEqual(expected, NpcMagicRules.CanAfford(uses, current, cost));
        }

        [Test]
        public void FreshStateStartsFullAndReloadKeepsDepletionAndCooldown()
        {
            NpcMagicDefinition d = new NpcMagicDefinition();
            NpcStateTable table = new NpcStateTable();
            NpcState s = table.GetOrCreate("mage");
            NpcMagicRules.Enter(d, s, 10, 1000);
            Assert.AreEqual(150, s.magicka);
            s.magicka = 23;
            s.magicCooldown = 6;
            s.magicRestAfter = 1060;
            s.magicRemainder = 0.75;
            NpcStateTable restored = new NpcStateTable();
            restored.Restore(table.Snapshot());
            NpcState copy = restored.GetOrCreate("mage");
            NpcMagicRules.Enter(d, copy, 10, 1000);
            Assert.AreEqual(23, copy.magicka);
            Assert.AreEqual(6, copy.magicCooldown);
            Assert.AreEqual(1060, copy.magicRestAfter);
            Assert.AreEqual(0.75, copy.magicRemainder);
            Assert.AreEqual(1000, copy.magicUpdatedAt);
            copy.magicka = 1;
            Assert.AreEqual(23, s.magicka);
            NpcMagicRules.Enter(d, copy, 12, 1010);
            Assert.AreEqual(150, copy.magicka);
            Assert.AreEqual(0, copy.magicCooldown);
        }

        [Test]
        public void RestDoesNotRefillOnEntryAndAccumulatesFractionalRecovery()
        {
            NpcMagicDefinition d = new NpcMagicDefinition { Recovery = "rest", MaxMagicka = 100, RestHours = 1 };
            NpcState s = new NpcState();
            NpcMagicRules.Enter(d, s, 1, 100);
            s.magicka = 0;
            NpcMagicRules.Enter(d, s, 3, 118);
            Assert.AreEqual(0, s.magicka);
            NpcMagicRules.Recover(d, s, 118, false);
            Assert.AreEqual(0, s.magicka);
            Assert.AreEqual(0.5, s.magicRemainder, 0.001);
            NpcMagicRules.Recover(d, s, 136, false);
            Assert.AreEqual(1, s.magicka);
            NpcMagicRules.Recover(d, s, 9999, false);
            Assert.AreEqual(100, s.magicka);
            Assert.AreEqual(0, s.magicRemainder);
        }

        [Test]
        public void CombatBlocksRecoveryAndDelaysRestForOneGameMinute()
        {
            NpcMagicDefinition d = new NpcMagicDefinition { Recovery = "rest", MaxMagicka = 100, RestHours = 1 };
            NpcState s = new NpcState();
            NpcMagicRules.Enter(d, s, 1, 100);
            s.magicka = 0;
            NpcMagicRules.Recover(d, s, 3700, true);
            Assert.AreEqual(0, s.magicka);
            NpcMagicRules.Recover(d, s, 3759, false);
            Assert.AreEqual(0, s.magicka);
            NpcMagicRules.Recover(d, s, 3796, false);
            Assert.AreEqual(1, s.magicka);
        }

        [Test]
        public void FullReadyCasterIsNotSavedButDepletedOrCoolingOneIs()
        {
            NpcMagicDefinition d = new NpcMagicDefinition();
            NpcStateTable table = new NpcStateTable();
            NpcState s = table.GetOrCreate("mage");
            NpcMagicRules.Enter(d, s, 1, 100);
            Assert.IsTrue(s.IsDefault());
            Assert.AreEqual(0, table.Snapshot().Count);
            s.magicCooldown = 3;
            Assert.IsFalse(s.IsDefault());
            s.magicCooldown = 0;
            s.magicka = 149;
            Assert.IsFalse(s.IsDefault());
            Assert.AreEqual(1, table.Snapshot().Count);
            s.hostile = true;
            s.magicka = 150;
            Assert.IsFalse(s.IsDefault());
        }

        [Test]
        public void UnlimitedCasterIsSettledWhateverItsMagickaOnceCooldownEnds()
        {
            NpcMagicDefinition d = new NpcMagicDefinition { UsesMagicka = false };
            NpcState s = new NpcState();
            NpcMagicRules.Enter(d, s, 1, 100);
            s.magicka = 0;
            s.magicCooldown = 2;
            Assert.IsFalse(s.IsDefault());
            s.magicCooldown = 0;
            Assert.IsTrue(s.IsDefault());
        }

        [Test]
        public void LeavingAnAreaForgetsOnlyEntryRefillCastersFromOtherVisits()
        {
            NpcStateTable table = new NpcStateTable();
            NpcState left = table.GetOrCreate("left");
            NpcState here = table.GetOrCreate("here");
            NpcState rester = table.GetOrCreate("rester");
            NpcMagicRules.Enter(new NpcMagicDefinition(), left, 1, 100);
            NpcMagicRules.Enter(new NpcMagicDefinition(), here, 2, 100);
            NpcMagicRules.Enter(new NpcMagicDefinition { Recovery = "rest" }, rester, 1, 100);
            left.magicka = here.magicka = rester.magicka = 5;
            left.hostile = true;
            table.ForgetMagicOutside(2);
            Assert.IsFalse(left.magicInitialized);
            Assert.AreEqual(0, left.magicka);
            Assert.IsTrue(left.hostile, "only magic is forgotten");
            Assert.AreEqual(5, here.magicka);
            Assert.AreEqual(5, rester.magicka, "rest recovery keeps its pool between visits");
            NpcMagicRules.Enter(new NpcMagicDefinition(), left, 3, 200);
            Assert.AreEqual(150, left.magicka);
            Assert.AreEqual(0, left.magicCooldown);
        }

        [Test]
        public void SettledInputsSurviveSnapshotAndRestore()
        {
            NpcMagicDefinition d = new NpcMagicDefinition { MaxMagicka = 80, Recovery = "rest" };
            NpcStateTable table = new NpcStateTable();
            NpcState s = table.GetOrCreate("mage");
            NpcMagicRules.Enter(d, s, 1, 100);
            s.magicka = 10;
            NpcStateTable restored = new NpcStateTable();
            restored.Restore(table.Snapshot());
            NpcState copy = restored.GetOrCreate("mage");
            Assert.AreEqual(80, copy.magicMax);
            Assert.IsFalse(copy.magicRefillsOnEntry);
            Assert.IsFalse(copy.magicUnlimited);
            Assert.IsFalse(copy.IsDefault());
        }

        [Test]
        public void AreaRecoveryDoesNotRegenerateOverTimeAndChangedMaximumClamps()
        {
            NpcMagicDefinition d = new NpcMagicDefinition();
            NpcState s = new NpcState();
            NpcMagicRules.Enter(d, s, 1, 100);
            s.magicka = 125;
            NpcMagicRules.Recover(d, s, 99999, false);
            Assert.AreEqual(125, s.magicka);
            d.MaxMagicka = 30;
            NpcMagicRules.Enter(d, s, 1, 99999);
            Assert.AreEqual(30, s.magicka);
        }
    }
}

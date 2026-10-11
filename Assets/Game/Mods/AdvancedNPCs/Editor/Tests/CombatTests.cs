using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    /// <summary>Optional "combat" tuning in npc.json: health, damage and attack speed multipliers.</summary>
    public class CombatTests
    {
        const string Unique =
            "{ \"kind\": \"unique\", \"name\": \"Grub\", " +
            "\"location\": { \"region\": \"Daggerfall\", \"place\": \"Daggerfall\" }, " +
            "\"position\": [1.5, 2, -3] }";

        static ParseResult With(string extraFields)
        {
            return DefinitionParser.ParseFolder("grub", Unique.Substring(0, Unique.Length - 1) + ", " + extraFields + " }");
        }

        const string Orc = "\"attitude\": \"hostile\", \"baseClass\": \"Orc\"";

        [Test]
        public void NoCombat_KeepsVanillaStats()
        {
            ParseResult r = DefinitionParser.ParseFolder("grub", Unique);
            Assert.IsTrue(r.Ok, r.Error);
            Assert.AreEqual(1f, r.Definition.HealthScale);
            Assert.AreEqual(1f, r.Definition.DamageScale);
            Assert.AreEqual(1f, r.Definition.AttackSpeed);
            Assert.IsFalse(r.Definition.HasCombatTuning);
        }

        [Test]
        public void Combat_ReadsAllThreeMultipliers()
        {
            ParseResult r = With(Orc + ", \"combat\": { \"health\": 0.7, \"damage\": 0.75, \"attackSpeed\": 1.3 }");
            Assert.IsTrue(r.Ok, r.Error);
            Assert.AreEqual(0.7f, r.Definition.HealthScale, 1e-6);
            Assert.AreEqual(0.75f, r.Definition.DamageScale, 1e-6);
            Assert.AreEqual(1.3f, r.Definition.AttackSpeed, 1e-6);
            Assert.IsTrue(r.Definition.HasCombatTuning);
        }

        [Test]
        public void Combat_MissingKeysDefaultToOne()
        {
            ParseResult r = With(Orc + ", \"combat\": { \"attackSpeed\": 2 }");
            Assert.IsTrue(r.Ok, r.Error);
            Assert.AreEqual(1f, r.Definition.HealthScale);
            Assert.AreEqual(1f, r.Definition.DamageScale);
            Assert.AreEqual(2f, r.Definition.AttackSpeed);
        }

        [Test]
        public void Combat_RejectsOutOfRangeValues()
        {
            Assert.IsFalse(With(Orc + ", \"combat\": { \"health\": 0 }").Ok);
            Assert.IsFalse(With(Orc + ", \"combat\": { \"health\": 11 }").Ok);
            Assert.IsFalse(With(Orc + ", \"combat\": { \"damage\": -1 }").Ok);
            Assert.IsFalse(With(Orc + ", \"combat\": { \"attackSpeed\": 0.1 }").Ok);
            Assert.IsFalse(With(Orc + ", \"combat\": { \"attackSpeed\": 5 }").Ok);
            StringAssert.Contains("combat.attackSpeed", With(Orc + ", \"combat\": { \"attackSpeed\": 5 }").Error);
        }

        [Test]
        public void Combat_RejectsUnknownKeysAndNonObjects()
        {
            ParseResult typo = With(Orc + ", \"combat\": { \"atackSpeed\": 1.2 }");
            Assert.IsFalse(typo.Ok);
            StringAssert.Contains("atackSpeed", typo.Error);
            Assert.IsFalse(With(Orc + ", \"combat\": 1.2").Ok);
        }

        [Test]
        public void Combat_DamageNeedsACreature()
        {
            ParseResult human = With("\"attitude\": \"hostile\", \"baseClass\": \"Warrior\", \"combat\": { \"damage\": 0.5 }");
            Assert.IsFalse(human.Ok);
            StringAssert.Contains("creature", human.Error);
            Assert.IsTrue(With("\"baseClass\": \"Warrior\", \"combat\": { \"health\": 1.5, \"attackSpeed\": 1.2 }").Ok);
        }

        [Test]
        public void ScaleHealth_RoundsAndNeverDropsBelowOne()
        {
            Assert.AreEqual(17, CombatRules.ScaleHealth(24, 0.7f));
            Assert.AreEqual(1, CombatRules.ScaleHealth(1, 0.1f));
            Assert.AreEqual(24, CombatRules.ScaleHealth(24, 1f));
            Assert.AreEqual(0, CombatRules.ScaleHealth(0, 0.7f));
        }

        [Test]
        public void ScaleDamage_KeepsAValidRange()
        {
            int min, max;
            CombatRules.ScaleDamage(1, 6, 0.7f, out min, out max);
            Assert.AreEqual(1, min);
            Assert.AreEqual(4, max);
            CombatRules.ScaleDamage(2, 3, 0.1f, out min, out max);
            Assert.AreEqual(1, min);
            Assert.AreEqual(1, max);
            CombatRules.ScaleDamage(0, 0, 0.5f, out min, out max);   // an unused second attack stays unused
            Assert.AreEqual(0, min);
            Assert.AreEqual(0, max);
            CombatRules.ScaleDamage(5, 10, 2f, out min, out max);
            Assert.AreEqual(10, min);
            Assert.AreEqual(20, max);
        }

        [Test]
        public void PacedMeleeTimer_OnlyShortensFreshResets()
        {
            // A fresh reset (the timer jumped up) is divided by the attack speed...
            Assert.AreEqual(1f, CombatRules.PacedMeleeTimer(0f, 1.3f, 1.3f), 1e-5);
            // ...while the normal countdown is left alone.
            Assert.AreEqual(0.9f, CombatRules.PacedMeleeTimer(1f, 0.9f, 1.3f), 1e-5);
            Assert.AreEqual(2f, CombatRules.PacedMeleeTimer(0f, 2f, 1f), 1e-5);
        }
    }
}

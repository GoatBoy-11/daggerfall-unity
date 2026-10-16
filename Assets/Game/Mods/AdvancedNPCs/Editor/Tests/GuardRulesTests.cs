using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class GuardRulesTests
    {
        [Test]
        public void GuardIsOptInAndDoesNotChangeClassOrRoutine()
        {
            ParseResult ordinary = DefinitionParser.ParseFolder("test", "{\"kind\":\"generic\"}");
            Assert.IsTrue(ordinary.Ok, ordinary.Error);
            Assert.IsFalse(ordinary.Definition.Guard);
            ParseResult guard = DefinitionParser.ParseFolder("test", "{\"kind\":\"generic\",\"guard\":true,\"baseClass\":\"Knight\",\"schedule\":[{\"package\":\"wander\"}]}");
            Assert.IsTrue(guard.Ok, guard.Error);
            Assert.IsTrue(guard.Definition.Guard);
            Assert.AreEqual("Knight", guard.Definition.BaseClass);
            Assert.AreEqual("wander", guard.Definition.Schedule[0].Package);
        }
        [TestCase("\"guard\":\"yes\"")]
        [TestCase("\"guard\":true,\"attitude\":\"hostile\"")]
        [TestCase("\"guard\":true,\"baseClass\":\"Rat\",\"attitude\":\"hostile\"")]
        public void InvalidGuardDefinitionFails(string fields)
        { Assert.IsFalse(DefinitionParser.ParseFolder("test", "{\"kind\":\"generic\"," + fields + "}").Ok); }
        [Test]
        public void AngerAndEnemyFlagsDoNotMakeAnNpcWanted()
        {
            var state = new NpcState { hostile = true, enemyOn = true };
            Assert.IsFalse(GuardRules.Wanted(state, 4, 100));
            state.wantedCrime = 4; state.wantedRegion = 4;
            Assert.IsTrue(GuardRules.Wanted(state, 4, 100));
            Assert.IsFalse(GuardRules.Wanted(state, 3, 100));
            state.dead = true;
            Assert.IsFalse(GuardRules.Wanted(state, 4, 100));
        }
        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(16)]
        public void UnknownOrEmptyCrimeCannotJustifyArrest(int crime)
        { Assert.IsFalse(GuardRules.Wanted(new NpcState { wantedCrime = crime }, 0, 0)); }
        [Test]
        public void ArrestIsNotDeathAndCustodyExpiresAtOneDay()
        {
            var state = new NpcState { wantedCrime = 5, wantedRegion = 4, hostile = true, hostileUntil = 999, enemyOn = true };
            GuardRules.Arrest(state, 100);
            Assert.IsFalse(state.dead);
            Assert.IsFalse(state.hostile);
            Assert.IsFalse(state.enemyOn);
            Assert.IsTrue(state.enemyOff);
            Assert.AreEqual(0, state.wantedCrime);
            Assert.IsTrue(GuardRules.InCustody(state, 100 + GuardRules.CustodySeconds - 1));
            Assert.IsFalse(GuardRules.InCustody(state, 100 + GuardRules.CustodySeconds));
            Assert.IsFalse(GuardRules.Wanted(state, 4, 100));
        }
        [Test]
        public void WantedAndCustodySurviveStateSnapshots()
        {
            var table = new NpcStateTable();
            var state = table.GetOrCreate("suspect");
            state.wantedCrime = 4; state.wantedRegion = 17;
            var restored = new NpcStateTable();
            restored.Restore(table.Snapshot());
            Assert.IsTrue(GuardRules.Wanted(restored.GetOrCreate("suspect"), 17, 0));
            GuardRules.Arrest(state, 100);
            restored.Restore(table.Snapshot());
            Assert.IsTrue(GuardRules.InCustody(restored.GetOrCreate("suspect"), 101));
            Assert.IsFalse(new NpcState().Clone().dead);
            Assert.IsTrue(new NpcState().IsDefault());
        }
        [TestCase(Bravery.Normal, 1f, true)]
        [TestCase(Bravery.Coward, 1f, true)]
        [TestCase(Bravery.Prudent, 1f, true)]
        [TestCase(Bravery.Brave, 1f, false)]
        [TestCase(Bravery.Brave, 0.25f, true)]
        public void OnlyHealthyBraveCriminalsResist(Bravery bravery, float health, bool expected)
        { Assert.AreEqual(expected, GuardRules.Surrenders(bravery, health)); }
    }
}

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
            Assert.IsFalse(GuardRules.Wanted(state, 4));
            state.wantedCrime = 4; state.wantedRegion = 4;
            Assert.IsTrue(GuardRules.Wanted(state, 4));
            Assert.IsFalse(GuardRules.Wanted(state, 3));
            state.dead = true;
            Assert.IsFalse(GuardRules.Wanted(state, 4));
        }
        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(16)]
        public void UnknownOrEmptyCrimeCannotJustifyEnforcement(int crime)
        { Assert.IsFalse(GuardRules.Wanted(new NpcState { wantedCrime = crime }, 0)); }
        [Test]
        public void WantedOffencesSurviveStateSnapshots()
        {
            var table = new NpcStateTable();
            var state = table.GetOrCreate("suspect");
            state.wantedCrime = 4; state.wantedRegion = 17;
            var restored = new NpcStateTable();
            restored.Restore(table.Snapshot());
            Assert.IsTrue(GuardRules.Wanted(restored.GetOrCreate("suspect"), 17));
            state.wantedCrime = 0;
            restored.Restore(table.Snapshot());
            Assert.IsFalse(GuardRules.Wanted(restored.GetOrCreate("suspect"), 17));
            Assert.IsTrue(new NpcState().IsDefault());
        }
    }
}

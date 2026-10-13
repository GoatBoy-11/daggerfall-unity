using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    /// <summary>Greetings (spec run/greet/turret §5).</summary>
    public class GreetRulesTests
    {
        [TestCase(true, "likes", true)]
        [TestCase(true, "loves", true)]
        [TestCase(true, "neutral", false)]
        [TestCase(true, "dislikes", false)]
        [TestCase(false, "loves", false)]
        public void LikesPlayer(bool calm, string band, bool expected)
        {
            Assert.AreEqual(expected, GreetRules.LikesPlayer(calm, band));
        }

        [Test]
        public void SameSide_BothPeaceful_OrBothIdleEnemies()
        {
            Assert.IsTrue(GreetRules.SameSide(GreetRules.Peaceful, GreetRules.Peaceful));
            Assert.IsTrue(GreetRules.SameSide(GreetRules.IdleEnemy, GreetRules.IdleEnemy));    // bandits before they spot you
            Assert.IsFalse(GreetRules.SameSide(GreetRules.Peaceful, GreetRules.IdleEnemy));
            Assert.IsFalse(GreetRules.SameSide(GreetRules.IdleEnemy, GreetRules.Peaceful));
            Assert.IsFalse(GreetRules.SameSide(GreetRules.Busy, GreetRules.Busy));
            Assert.IsFalse(GreetRules.SameSide(GreetRules.Peaceful, GreetRules.Busy));
        }

        [Test]
        public void Side_PeacefulIdleEnemyOrBusy()
        {
            Assert.AreEqual(GreetRules.Peaceful, GreetRules.Side(true, false, false, false));
            Assert.AreEqual(GreetRules.Busy, GreetRules.Side(true, true, false, false));       // angry at the player
            Assert.AreEqual(GreetRules.Busy, GreetRules.Side(false, false, false, false));     // fleeing a creature
            Assert.AreEqual(GreetRules.IdleEnemy, GreetRules.Side(false, true, true, true));   // an enemy that has no target
            Assert.AreEqual(GreetRules.Busy, GreetRules.Side(false, true, true, false));       // an enemy that spotted someone
        }

        [Test]
        public void NewEncounter_LikedAndLuckyRoll_Greets()
        {
            GreetMemory m = new GreetMemory();
            Assert.IsTrue(m.Consider("player", true, true, 10.0, 0.1, GreetRules.Chance));
            Assert.AreEqual(1, m.Count);
        }

        [Test]
        public void UnluckyRoll_DoesNotGreet()
        {
            Assert.IsFalse(new GreetMemory().Consider("player", true, true, 10.0, 0.9, GreetRules.Chance));
        }

        [Test]
        public void CertainChance_GreetsEvenOnTheTopRoll()
        {
            // Unity's Random.value includes 1.
            Assert.IsTrue(new GreetMemory().Consider("player", true, true, 10.0, 1.0, 1.0));
        }

        [Test]
        public void NotLiked_OrOutOfRange_DoesNotGreet()
        {
            Assert.IsFalse(new GreetMemory().Consider("player", true, false, 10.0, 0.0, 1.0));
            Assert.IsFalse(new GreetMemory().Consider("player", false, true, 10.0, 0.0, 1.0));
        }

        [Test]
        public void StayingInRange_DoesNotRollAgain()
        {
            GreetMemory m = new GreetMemory();
            Assert.IsFalse(m.Consider("bob", true, true, 10.0, 0.9, 0.25));   // the encounter's one roll fails
            Assert.IsFalse(m.Consider("bob", true, true, 10.1, 0.0, 0.25));   // still the same encounter
            Assert.IsFalse(m.Consider("bob", false, true, 10.2, 0.0, 0.25));  // leaves
            Assert.IsTrue(m.Consider("bob", true, true, 10.3, 0.0, 0.25));    // a new encounter rolls again
        }

        [Test]
        public void LeaveAll_EndsEveryEncounter()
        {
            // The greeter stops checking while the player is far away (spec tools-quality B2.9): nobody stays "near".
            GreetMemory m = new GreetMemory();
            Assert.IsFalse(m.Consider("player", true, true, 10.0, 0.9, 0.25));
            m.LeaveAll();
            Assert.IsTrue(m.Consider("player", true, true, 10.1, 0.0, 0.25));
        }

        [Test]
        public void Cooldown_BlocksTheSameTargetForTwoHours()
        {
            GreetMemory m = new GreetMemory();
            Assert.IsTrue(m.Consider("bob", true, true, 10.0, 0.0, 1.0));
            m.Consider("bob", false, true, 10.5, 0.0, 1.0);
            Assert.IsFalse(m.Consider("bob", true, true, 11.0, 0.0, 1.0));    // 1 hour later
            m.Consider("bob", false, true, 11.5, 0.0, 1.0);
            Assert.IsTrue(m.Consider("bob", true, true, 12.1, 0.0, 1.0));     // over 2 hours later
            Assert.IsTrue(m.Consider("ann", true, true, 12.1, 0.0, 1.0));     // another target is separate
        }

        [Test]
        public void Met_CountsAsTheSameEncounter()
        {
            // After a conversation the partner is not greeted again until it leaves and comes back.
            GreetMemory m = new GreetMemory();
            m.Met("anna");
            Assert.IsFalse(m.Consider("anna", true, true, 10.0, 0.0, 1.0));
            Assert.IsFalse(m.Consider("anna", false, true, 10.1, 0.0, 1.0));
            Assert.IsTrue(m.Consider("anna", true, true, 10.2, 0.0, 1.0));
        }

        [TestCase(true, false, true)]
        [TestCase(false, true, true)]
        [TestCase(true, true, true)]
        [TestCase(false, false, false)]
        public void NeedsGreeter_GreetFilesOrAConversation(bool greetFiles, bool converses, bool expected)
        {
            // Without greet files the greeter only starts conversations (also with the vanilla sprite).
            Assert.AreEqual(expected, GreetRules.NeedsGreeter(greetFiles, converses));
        }
    }
}

using System.Collections.Generic;
using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class ReplyParserTests
    {
        const string Src = "_Dialogue/t.json";

        static DialogueParseResult Parse(string topic)
        {
            return DialogueParser.Parse(Src, "{ \"topics\": [" + topic + "] }");
        }

        static void HasMessage(DialogueParseResult r, string part)
        {
            Assert.IsTrue(r.Messages.Exists(delegate (string m) { return m.Contains(part); }),
                "no message containing \"" + part + "\" in:\n" + string.Join("\n", r.Messages.ToArray()));
        }

        [Test]
        public void TopicReplies_WithActions()
        {
            DialogueParseResult r = Parse("{ \"caption\": \"Ale\", \"answers\": [\"Want one?\"], \"replies\": [ " +
                "{ \"text\": \"A mug, please. (2 gold)\", \"takeGold\": 2, \"reputation\": 1, \"answers\": [\"Here you go.\"] }, " +
                "{ \"text\": \"Not today.\", \"answers\": \"Suit yourself.\" } ] }");
            CollectionAssert.IsEmpty(r.Messages);
            DialogueTopic t = r.File.Topics[0];
            Assert.AreEqual(2, t.Replies.Count);
            DialogueReply buy = t.Replies[0];
            Assert.AreEqual("A mug, please. (2 gold)", buy.Text);
            Assert.AreEqual("a_mug_please_2_gold", buy.Id);
            Assert.AreEqual(2, buy.Actions.TakeGold);
            Assert.AreEqual(1, buy.Actions.Reputation);
            Assert.AreEqual("Here you go.", buy.Answers[0].Text);
            Assert.AreEqual("Suit yourself.", t.Replies[1].Answers[0].Text);
            Assert.IsTrue(t.Replies[1].Actions.IsEmpty);
        }

        [Test]
        public void AnswerReplies_AndNesting()
        {
            DialogueParseResult r = Parse("{ \"caption\": \"Jory\", \"answers\": [ { \"text\": \"He vanished.\", \"replies\": [ " +
                "{ \"text\": \"I'll look for him.\", \"sets\": \"promised\", \"answers\": [ { \"text\": \"Bless you.\", \"replies\": [ " +
                "{ \"text\": \"Where should I start?\", \"answers\": [\"The old warehouse.\"] } ] } ] } ] } ] }");
            CollectionAssert.IsEmpty(r.Messages);
            DialogueReply look = r.File.Topics[0].Answers[0].Replies[0];
            CollectionAssert.AreEqual(new[] { "promised" }, look.Sets);
            Assert.AreEqual("Where should I start?", look.Answers[0].Replies[0].Text);
        }

        [Test]
        public void ReplyWhen_ToneAllowed()
        {
            DialogueParseResult r = Parse("{ \"caption\": \"A\", \"answers\": [\"x\"], \"replies\": [ { \"text\": \"Oi!\", \"when\": { \"tone\": \"blunt\" }, \"answers\": [\"y\"] } ] }");
            CollectionAssert.IsEmpty(r.Messages);
            Assert.IsTrue(r.File.Topics[0].Replies[0].When.UsesTone);
        }

        [Test]
        public void TooDeep_Warns()
        {
            string reply = "{ \"text\": \"deep\", \"answers\": [\"end\"] }";
            for (int i = 0; i < 9; i++)
                reply = "{ \"text\": \"r" + i + "\", \"answers\": [\"a\"], \"replies\": [" + reply + "] }";
            DialogueParseResult r = Parse("{ \"caption\": \"A\", \"answers\": [\"x\"], \"replies\": [" + reply + "] }");
            HasMessage(r, "replies: nested deeper than 8 levels, the deeper ones are ignored");
            Assert.IsNotNull(r.File.Topics[0]);
        }

        [Test]
        public void BadReplies_SkippedWithMessage()
        {
            DialogueParseResult r = Parse("{ \"caption\": \"A\", \"answers\": [\"x\"], \"replies\": [ { \"answers\": [\"y\"] }, { \"text\": \"No answer\" }, \"just text\", { \"text\": \"ok\", \"answers\": [\"z\"] } ] }");
            Assert.AreEqual(1, r.File.Topics[0].Replies.Count);
            HasMessage(r, "topic \"A\": replies: reply 1: text: required, reply skipped");
            HasMessage(r, "topic \"A\": replies: reply 2: answers: required (one or more texts), reply skipped");
            HasMessage(r, "topic \"A\": replies: reply 3: must be an object with \"text\" and \"answers\", reply skipped");
        }

        [TestCase("\"giveGold\": 0", "giveGold: must be a whole number 1-100000, ignored")]
        [TestCase("\"takeGold\": 2.5", "takeGold: must be a whole number 1-100000, ignored")]
        [TestCase("\"reputation\": 50", "reputation: must be a whole number -20 to 20 (not 0), ignored")]
        [TestCase("\"becomeEnemy\": \"yes\"", "becomeEnemy: must be true or false, ignored")]
        [TestCase("\"giveItem\": \"\"", "giveItem: must be an item name, ignored")]
        [TestCase("\"startQuest\": 5", "startQuest: must be a quest name, ignored")]
        [TestCase("\"giveGld\": 5", "giveGld: unknown field (did you mean \"giveGold\"?), ignored")]
        public void BadActions_Warn(string field, string message)
        {
            DialogueParseResult r = Parse("{ \"caption\": \"A\", \"answers\": [\"x\"], \"replies\": [ { \"text\": \"t\", " + field + ", \"answers\": [\"y\"] } ] }");
            HasMessage(r, "replies: reply 1: " + message);
            Assert.AreEqual(1, r.File.Topics[0].Replies.Count);
        }

        [Test]
        public void ActionsOnTopicsAndAnswers()
        {
            DialogueParseResult r = Parse("{ \"caption\": \"Job\", \"startQuest\": \"A0C00Y00\", \"answers\": [ { \"text\": \"Here's coin.\", \"giveGold\": 50, \"giveItem\": \"Ruby\", \"endConversation\": true } ] }");
            CollectionAssert.IsEmpty(r.Messages);
            Assert.AreEqual("A0C00Y00", r.File.Topics[0].Actions.StartQuest);
            DialogueActions a = r.File.Topics[0].Answers[0].Actions;
            Assert.AreEqual(50, a.GiveGold);
            Assert.AreEqual("Ruby", a.GiveItem);
            Assert.IsTrue(a.EndConversation);
        }

        [Test]
        public void Greetings_NoRepliesOrActions()
        {
            DialogueParseResult r = DialogueParser.Parse(Src, "{ \"greetings\": [ { \"text\": \"Hi\", \"giveGold\": 5, \"replies\": [] } ] }");
            HasMessage(r, "greetings: greeting 1: giveGold: not allowed in greetings, ignored");
            HasMessage(r, "greetings: greeting 1: replies: not allowed in greetings, ignored");
        }
    }
}

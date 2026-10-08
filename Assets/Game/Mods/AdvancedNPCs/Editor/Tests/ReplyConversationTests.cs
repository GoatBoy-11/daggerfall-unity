using System;
using System.Collections.Generic;
using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    /// <summary>Records actions in order (C2).</summary>
    public class FakeActions : IDialogueActions
    {
        public readonly List<string> Done = new List<string>();
        readonly FakeFacts facts;

        public FakeActions(FakeFacts facts)
        {
            this.facts = facts;
        }

        public void GiveGold(int amount) { facts.GoldValue += amount; Done.Add("giveGold " + amount); }
        public void TakeGold(int amount) { facts.GoldValue -= amount; Done.Add("takeGold " + amount); }
        public void GiveItem(string name) { facts.Items.Add(name); Done.Add("giveItem " + name); }
        public void TakeItem(string name) { facts.Items.Remove(name); Done.Add("takeItem " + name); }
        public void ChangeReputation(int amount) { Done.Add("reputation " + amount); }
        public void StartQuest(string name) { Done.Add("startQuest " + name); }
        public void BecomeEnemy() { Done.Add("becomeEnemy"); }
        public void EndConversation() { Done.Add("endConversation"); }
    }

    public class ReplyConversationTests
    {
        static DialogueTopic Topic(string json)
        {
            DialogueParseResult r = DialogueParser.Parse("t.json", "{ \"topics\": [" + json + "] }");
            Assert.IsNotNull(r.File, string.Join("\n", r.Messages.ToArray()));
            CollectionAssert.IsEmpty(r.Messages);
            return r.File.Topics[0];
        }

        static List<string> Texts(List<DialogueReply> replies)
        {
            return replies.ConvertAll(delegate (DialogueReply r) { return r.Text; });
        }

        const string Ale = "{ \"caption\": \"Ale\", \"answers\": [\"Want one?\"], \"replies\": [ " +
            "{ \"text\": \"A mug, please.\", \"takeGold\": 2, \"reputation\": 1, \"sets\": \"had_ale\", \"answers\": [\"Here you go.\"] }, " +
            "{ \"text\": \"Insult the ale\", \"when\": { \"tone\": \"blunt\" }, \"becomeEnemy\": true, \"answers\": [\"Get out!\"] }, " +
            "{ \"text\": \"Not today.\", \"answers\": [\"Suit yourself.\"] } ] }";

        [Test]
        public void Replies_WhenAndAffordability()
        {
            DialogueTopic t = Topic(Ale);
            FakeFacts poor = new FakeFacts { GoldValue = 1, ToneValue = Tones.Normal };
            DialogueAnswer answer;
            Conversation.Ask(t, poor, new Random(1), poor, new FakeActions(poor), out answer);
            CollectionAssert.AreEqual(new[] { "Not today." }, Texts(Conversation.Replies(answer, t.Replies, poor)));
            FakeFacts rich = new FakeFacts { GoldValue = 10, ToneValue = Tones.Blunt };
            CollectionAssert.AreEqual(new[] { "A mug, please.", "Insult the ale", "Not today." }, Texts(Conversation.Replies(answer, t.Replies, rich)));
        }

        [Test]
        public void Say_AppliesActionsInOrder_MarksAsked()
        {
            DialogueTopic t = Topic(Ale);
            FakeFacts f = new FakeFacts { GoldValue = 10, ToneValue = Tones.Normal };
            FakeActions act = new FakeActions(f);
            DialogueAnswer next;
            string text = Conversation.Say(t.Replies[0], t.Id, f, new Random(1), f, act, out next);
            Assert.AreEqual("Here you go.", text);
            CollectionAssert.AreEqual(new[] { "takeGold 2", "reputation 1" }, act.Done);
            Assert.AreEqual(8, f.GoldValue);
            CollectionAssert.Contains(f.Flags, "had_ale");
            CollectionAssert.Contains(f.AskedTopics, "ale_a_mug_please"); // "asked": "Ale/A mug, please." normalises the same
        }

        [Test]
        public void ActionOrder_TakesGivesReputationFlagsThenQuestEnemyEnd()
        {
            DialogueTopic t = Topic("{ \"caption\": \"Deal\", \"answers\": [\"x\"], \"replies\": [ { \"text\": \"Deal.\", \"endConversation\": true, " +
                "\"becomeEnemy\": true, \"startQuest\": \"Q1\", \"reputation\": -2, \"giveItem\": \"Ruby\", \"giveGold\": 5, \"takeItem\": \"Key\", " +
                "\"takeGold\": 1, \"answers\": [\"Done.\"] } ] }");
            FakeFacts f = new FakeFacts { GoldValue = 3 };
            f.Items.Add("Key");
            FakeActions act = new FakeActions(f);
            DialogueAnswer next;
            Conversation.Say(t.Replies[0], t.Id, f, new Random(1), f, act, out next);
            CollectionAssert.AreEqual(new[] { "takeGold 1", "takeItem Key", "giveGold 5", "giveItem Ruby", "reputation -2", "startQuest Q1", "becomeEnemy", "endConversation" }, act.Done);
        }

        [Test]
        public void AnswerRepliesWinOverTopicReplies()
        {
            DialogueTopic t = Topic("{ \"caption\": \"Jory\", \"answers\": [ { \"text\": \"He vanished.\", \"replies\": [ { \"text\": \"Own\", \"answers\": [\"a\"] } ] } ], " +
                "\"replies\": [ { \"text\": \"Topic's\", \"answers\": [\"b\"] } ] }");
            FakeFacts f = new FakeFacts();
            DialogueAnswer answer;
            Conversation.Ask(t, f, new Random(1), f, new FakeActions(f), out answer);
            CollectionAssert.AreEqual(new[] { "Own" }, Texts(Conversation.Replies(answer, t.Replies, f)));
        }

        [Test]
        public void NestedReplies_FromChosenAnswerElseReply()
        {
            DialogueTopic t = Topic("{ \"caption\": \"J\", \"answers\": [\"x\"], \"replies\": [ { \"text\": \"Look\", \"answers\": [\"Bless you.\"], " +
                "\"replies\": [ { \"text\": \"Where?\", \"answers\": [\"Warehouse.\"] } ] } ] }");
            FakeFacts f = new FakeFacts();
            DialogueAnswer next;
            Conversation.Say(t.Replies[0], t.Id, f, new Random(1), f, new FakeActions(f), out next);
            CollectionAssert.AreEqual(new[] { "Where?" }, Texts(Conversation.Replies(next, t.Replies[0].Replies, f)));
        }

        [Test]
        public void TopicWithTakeGold_HiddenWhenPoor()
        {
            DialogueTopic t = Topic("{ \"caption\": \"Bribe\", \"takeGold\": 50, \"answers\": [\"Thanks.\"] }");
            Assert.AreEqual("needs 50 gold", Conversation.WhyHidden(t, new FakeFacts { GoldValue = 10 }));
            Assert.IsNull(Conversation.WhyHidden(t, new FakeFacts { GoldValue = 50 }));
        }

        [Test]
        public void CanSay_RecheckedAtPickTime()
        {
            DialogueTopic t = Topic(Ale);
            Assert.IsFalse(Conversation.CanSay(t.Replies[0], new FakeFacts { GoldValue = 1 }));
            Assert.IsTrue(Conversation.CanSay(t.Replies[2], new FakeFacts { GoldValue = 1 }));
        }

        [Test]
        public void Ask_TopicActionsRun()
        {
            DialogueTopic t = Topic("{ \"caption\": \"Job\", \"startQuest\": \"A0C00Y00\", \"answers\": [ { \"text\": \"Coin.\", \"giveGold\": 5 } ] }");
            FakeFacts f = new FakeFacts();
            FakeActions act = new FakeActions(f);
            DialogueAnswer answer;
            Conversation.Ask(t, f, new Random(1), f, act, out answer);
            CollectionAssert.AreEqual(new[] { "giveGold 5", "startQuest A0C00Y00" }, act.Done);
        }
    }
}

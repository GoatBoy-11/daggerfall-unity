using System;
using System.Collections.Generic;
using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class ConversationTests
    {
        static ComposedDialogue Dialogue(string json)
        {
            DialogueParseResult r = DialogueParser.Parse("t.json", json);
            Assert.IsNotNull(r.File, string.Join("\n", r.Messages.ToArray()));
            NpcDefinition d = new NpcDefinition();
            d.OwnDialogue = r.File;
            return new DialogueLibrary().Compose(d);
        }

        static List<string> Shown(ComposedDialogue d, FakeFacts f)
        {
            List<string> captions = new List<string>();
            foreach (DialogueTopic t in Conversation.Visible(d, f, Conversation.MaxVisible))
                captions.Add(t.Caption);
            return captions;
        }

        [Test]
        public void SpecificAnswerBeatsPlain()
        {
            ComposedDialogue d = Dialogue("{ \"topics\": [ { \"caption\": \"Ale\", \"answers\": [ \"Plain.\", { \"when\": { \"time\": \"night\" }, \"text\": \"Night ale.\" } ] } ] }");
            FakeFacts night = new FakeFacts { Night = true };
            Assert.AreEqual("Night ale.", Conversation.Ask(d.Topics[0], night, new Random(1), night));
            FakeFacts day = new FakeFacts();
            Assert.AreEqual("Plain.", Conversation.Ask(d.Topics[0], day, new Random(1), day));
        }

        [Test]
        public void RandomAmongWinners()
        {
            ComposedDialogue d = Dialogue("{ \"topics\": [ { \"caption\": \"Ale\", \"answers\": [ \"A\", \"B\", \"C\" ] } ] }");
            HashSet<string> seen = new HashSet<string>();
            Random rng = new Random(7);
            FakeFacts f = new FakeFacts();
            for (int i = 0; i < 60; i++)
                seen.Add(Conversation.Ask(d.Topics[0], f, rng, f));
            CollectionAssert.AreEquivalent(new[] { "A", "B", "C" }, seen);
        }

        [Test]
        public void OnlyConditionalAnswers_TopicHiddenWhenNoneApplies()
        {
            ComposedDialogue d = Dialogue("{ \"topics\": [ { \"caption\": \"Night talk\", \"answers\": [ { \"when\": { \"time\": \"night\" }, \"text\": \"N\" } ] } ] }");
            CollectionAssert.IsEmpty(Shown(d, new FakeFacts()));
            Assert.AreEqual("no answer applies", Conversation.WhyHidden(d.Topics[0], new FakeFacts()));
            CollectionAssert.AreEqual(new[] { "Night talk" }, Shown(d, new FakeFacts { Night = true }));
        }

        [Test]
        public void ToneOnlyAnswers_AlwaysShown_AnswerFollowsTone()
        {
            ComposedDialogue d = Dialogue("{ \"topics\": [ { \"caption\": \"Ale\", \"answers\": [ " +
                "{ \"when\": { \"tone\": \"blunt\" }, \"text\": \"Blunt.\" }, { \"when\": { \"tone\": \"normal\" }, \"text\": \"Normal.\" } ] } ] }");
            CollectionAssert.AreEqual(new[] { "Ale" }, Shown(d, new FakeFacts { ToneValue = Tones.Polite }));
            FakeFacts blunt = new FakeFacts { ToneValue = Tones.Blunt };
            Assert.AreEqual("Blunt.", Conversation.Ask(d.Topics[0], blunt, new Random(1), blunt));
            FakeFacts polite = new FakeFacts { ToneValue = Tones.Polite };
            Assert.AreEqual("Blunt.", Conversation.Ask(d.Topics[0], polite, new Random(1), polite), "no answer fits: first answer");
        }

        [Test]
        public void ToneAnswerWithPlainFallback()
        {
            ComposedDialogue d = Dialogue("{ \"topics\": [ { \"caption\": \"Ale\", \"answers\": [ { \"when\": { \"tone\": \"blunt\" }, \"text\": \"Blunt.\" }, \"Plain.\" ] } ] }");
            FakeFacts polite = new FakeFacts { ToneValue = Tones.Polite };
            Assert.AreEqual("Plain.", Conversation.Ask(d.Topics[0], polite, new Random(1), polite));
        }

        [Test]
        public void Once_HiddenAfterAsked()
        {
            ComposedDialogue d = Dialogue("{ \"topics\": [ { \"caption\": \"Favour\", \"once\": true, \"answers\": [\"Thanks.\"] } ] }");
            FakeFacts f = new FakeFacts();
            CollectionAssert.AreEqual(new[] { "Favour" }, Shown(d, f));
            Conversation.Ask(d.Topics[0], f, new Random(1), f);
            CollectionAssert.IsEmpty(Shown(d, f));
            Assert.AreEqual("once (already asked)", Conversation.WhyHidden(d.Topics[0], f));
        }

        [Test]
        public void FollowUpAppearsAfterParentAsked()
        {
            ComposedDialogue d = Dialogue("{ \"topics\": [ { \"caption\": \"Any rumours?\", \"answers\": [\"A sailor vanished.\"] }, " +
                "{ \"caption\": \"The sailor\", \"when\": { \"asked\": \"Any rumours?\" }, \"answers\": [\"Jory.\"] } ] }");
            FakeFacts f = new FakeFacts();
            CollectionAssert.AreEqual(new[] { "Any rumours?" }, Shown(d, f));
            Assert.AreEqual("when: asked", Conversation.WhyHidden(d.Topics[1], f));
            Conversation.Ask(d.Topics[0], f, new Random(1), f);
            CollectionAssert.AreEqual(new[] { "Any rumours?", "The sailor" }, Shown(d, f));
        }

        [Test]
        public void SetsAndClears_TopicAndAnswer()
        {
            ComposedDialogue d = Dialogue("{ \"topics\": [ { \"caption\": \"Ale\", \"sets\": \"had_ale\", \"clears\": \"sober\", " +
                "\"answers\": [ { \"text\": \"Drink.\", \"sets\": \"tipsy\", \"clears\": \"thirsty\" } ] } ] }");
            FakeFacts f = new FakeFacts();
            f.Flags.Add("sober");
            f.Flags.Add("thirsty");
            Conversation.Ask(d.Topics[0], f, new Random(1), f);
            CollectionAssert.AreEquivalent(new[] { "had_ale", "tipsy" }, f.Flags);
            CollectionAssert.Contains(f.AskedTopics, "ale");
        }

        [Test]
        public void FlagsSetByOneTopicShowAnother()
        {
            ComposedDialogue d = Dialogue("{ \"topics\": [ { \"caption\": \"Rumours\", \"sets\": \"heard\", \"answers\": [\"x\"] }, " +
                "{ \"caption\": \"Secret\", \"when\": { \"flags\": \"heard\" }, \"answers\": [\"y\"] } ] }");
            FakeFacts f = new FakeFacts();
            Conversation.Ask(d.Topics[0], f, new Random(1), f);
            CollectionAssert.Contains(Shown(d, f), "Secret");
        }

        [Test]
        public void Visible_CappedAtMax()
        {
            List<string> topics = new List<string>();
            for (int i = 0; i < 35; i++)
                topics.Add("{ \"caption\": \"T" + i + "\", \"answers\": [\"x\"] }");
            ComposedDialogue d = Dialogue("{ \"topics\": [" + string.Join(",", topics.ToArray()) + "] }");
            Assert.AreEqual(30, Conversation.Visible(d, new FakeFacts(), Conversation.MaxVisible).Count);
        }

        [Test]
        public void Greeting_PickedByConditions_NullWhenNone()
        {
            ComposedDialogue d = Dialogue("{ \"greetings\": [ { \"when\": { \"reaction\": \"dislikes\" }, \"text\": \"You again.\" }, " +
                "{ \"when\": { \"time\": \"night\" }, \"text\": \"Evening.\" } ] }");
            Assert.AreEqual("You again.", Conversation.Greeting(d, new FakeFacts { ReactionValue = "dislikes" }, new Random(1)));
            Assert.IsNull(Conversation.Greeting(d, new FakeFacts(), new Random(1)));
            Assert.IsNull(Conversation.Greeting(Dialogue("{ \"topics\": [] }"), new FakeFacts(), new Random(1)));
        }

        [Test]
        public void EmptyWhen_CountsAsPlain()
        {
            ComposedDialogue d = Dialogue("{ \"topics\": [ { \"caption\": \"Ale\", \"answers\": [ { \"when\": {}, \"text\": \"A\" }, \"B\" ] } ] }");
            HashSet<string> seen = new HashSet<string>();
            Random rng = new Random(3);
            FakeFacts f = new FakeFacts();
            for (int i = 0; i < 40; i++)
                seen.Add(Conversation.Ask(d.Topics[0], f, rng, f));
            CollectionAssert.AreEquivalent(new[] { "A", "B" }, seen);
        }

        [Test]
        public void Question_UsesToneAndCaption()
        {
            ComposedDialogue d = Dialogue("{ \"topics\": [ { \"caption\": \"Ale\", \"answers\": [\"x\"] } ] }");
            Assert.AreEqual("What do you know about Ale?", Conversation.Question(d.Topics[0], Tones.Blunt));
        }

        [Test]
        public void Question_DefaultLowercasesLeadingArticle_AuthoredKept()
        {
            ComposedDialogue d = Dialogue("{ \"topics\": [ { \"caption\": \"The house ale\", \"answers\": [\"x\"] }, " +
                "{ \"caption\": \"Theodor\", \"answers\": [\"x\"] }, " +
                "{ \"caption\": \"The sailor\", \"question\": \"About {topic}?\", \"answers\": [\"x\"] } ] }");
            Assert.AreEqual("Tell me about the house ale.", Conversation.Question(d.Topics[0], Tones.Normal));
            Assert.AreEqual("Tell me about Theodor.", Conversation.Question(d.Topics[1], Tones.Normal));
            Assert.AreEqual("About The sailor?", Conversation.Question(d.Topics[2], Tones.Normal));
        }

        [Test]
        public void Question_DefaultDropsCaptionPunctuation()
        {
            ComposedDialogue d = Dialogue("{ \"topics\": [ { \"caption\": \"Any rumours?\", \"answers\": [\"x\"] } ] }");
            Assert.AreEqual("Could you tell me about Any rumours?", Conversation.Question(d.Topics[0], Tones.Polite));
            Assert.AreEqual("Tell me about Any rumours.", Conversation.Question(d.Topics[0], Tones.Normal));
        }
    }

    public class FlagSetTests
    {
        [Test]
        public void SetClearHas_Normalised()
        {
            FlagSet flags = new FlagSet();
            Assert.IsTrue(flags.Set("Heard Sailor"));
            Assert.IsFalse(flags.Set("heard_sailor"));
            Assert.IsTrue(flags.Has("HEARD sailor"));
            CollectionAssert.AreEqual(new[] { "heard_sailor" }, flags.Names());
            Assert.IsTrue(flags.Clear("heard_sailor"));
            Assert.IsFalse(flags.Has("heard_sailor"));
            Assert.IsFalse(flags.Set("???"));
        }

        [Test]
        public void Restore_NullSafe_Sorted()
        {
            FlagSet flags = new FlagSet();
            flags.Set("x");
            flags.Restore(null);
            CollectionAssert.IsEmpty(flags.Names());
            flags.Restore(new[] { "b", "A", "" });
            CollectionAssert.AreEqual(new[] { "a", "b" }, flags.Names());
        }
    }
}

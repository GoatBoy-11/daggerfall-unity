using System.Collections.Generic;
using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class DialogueParserTests
    {
        const string Src = "_Dialogue/t.json";

        static DialogueParseResult Parse(string json)
        {
            return DialogueParser.Parse(Src, json);
        }

        static string Topics(params string[] topics)
        {
            return "{ \"topics\": [" + string.Join(",", topics) + "] }";
        }

        static void HasMessage(DialogueParseResult r, string part)
        {
            Assert.IsTrue(r.Messages.Exists(delegate (string m) { return m.Contains(part); }),
                "no message containing \"" + part + "\" in:\n" + string.Join("\n", r.Messages.ToArray()));
        }

        [Test]
        public void MinimalTopic()
        {
            DialogueParseResult r = Parse(Topics("{ \"caption\": \"The house ale\", \"answers\": [\"Cheap.\"] }"));
            Assert.IsNotNull(r.File);
            CollectionAssert.IsEmpty(r.Messages);
            DialogueTopic t = r.File.Topics[0];
            Assert.AreEqual("The house ale", t.Caption);
            Assert.AreEqual("the_house_ale", t.Id);
            Assert.AreEqual(Src, t.Source);
            Assert.AreEqual("Cheap.", t.Answers[0].Text);
            Assert.IsNull(t.Answers[0].When);
            Assert.IsNull(t.When);
            Assert.IsFalse(t.Once);
        }

        [Test]
        public void AnswersMayBeASingleText()
        {
            DialogueParseResult r = Parse(Topics("{ \"caption\": \"Ale\", \"answers\": \"Cheap.\" }"));
            Assert.AreEqual("Cheap.", r.File.Topics[0].Answers[0].Text);
        }

        [Test]
        public void IdGiven_IsNormalised()
        {
            DialogueParseResult r = Parse(Topics("{ \"caption\": \"Any rumours?\", \"id\": \"Rumours\", \"answers\": [\"x\"] }"));
            Assert.AreEqual("rumours", r.File.Topics[0].Id);
        }

        [Test]
        public void DuplicateId_SecondSkipped()
        {
            DialogueParseResult r = Parse(Topics(
                "{ \"caption\": \"Ale\", \"answers\": [\"a\"] }",
                "{ \"caption\": \"ale!\", \"answers\": [\"b\"] }",
                "{ \"caption\": \"Bread\", \"answers\": [\"c\"] }"));
            Assert.AreEqual(2, r.File.Topics.Count);
            Assert.AreEqual("Bread", r.File.Topics[1].Caption);
            HasMessage(r, Src + ": topic \"ale!\": id: \"ale\" is used by an earlier topic, topic skipped");
        }

        [Test]
        public void MissingCaptionOrAnswers_TopicSkipped_OthersKept()
        {
            DialogueParseResult r = Parse(Topics(
                "{ \"answers\": [\"a\"] }",
                "{ \"caption\": \"No answers\" }",
                "{ \"caption\": \"Empty\", \"answers\": [] }",
                "{ \"caption\": \"Good\", \"answers\": [\"ok\"] }"));
            Assert.AreEqual(1, r.File.Topics.Count);
            Assert.AreEqual("Good", r.File.Topics[0].Caption);
            HasMessage(r, Src + ": topic 1: caption: required, topic skipped");
            HasMessage(r, Src + ": topic \"No answers\": answers: required");
            HasMessage(r, Src + ": topic \"Empty\": answers: required");
        }

        [Test]
        public void AnswerObjects_WithWhenSetsClears()
        {
            DialogueParseResult r = Parse(Topics(
                "{ \"caption\": \"Ale\", \"answers\": [ { \"when\": { \"tone\": \"blunt\" }, \"text\": \"Drink it.\", \"sets\": \"Rude Patron\", \"clears\": [\"polite_patron\"] }, \"Fine ale.\" ] }"));
            CollectionAssert.IsEmpty(r.Messages);
            DialogueAnswer a = r.File.Topics[0].Answers[0];
            Assert.AreEqual("Drink it.", a.Text);
            Assert.IsTrue(a.When.UsesTone);
            CollectionAssert.AreEqual(new[] { "rude_patron" }, a.Sets);
            CollectionAssert.AreEqual(new[] { "polite_patron" }, a.Clears);
            Assert.AreEqual("Fine ale.", r.File.Topics[0].Answers[1].Text);
        }

        [Test]
        public void BadAnswer_SkippedWithMessage()
        {
            DialogueParseResult r = Parse(Topics("{ \"caption\": \"Ale\", \"answers\": [ { \"when\": {} }, 5, \"ok\" ] }"));
            Assert.AreEqual(1, r.File.Topics[0].Answers.Count);
            HasMessage(r, "topic \"Ale\": answers: answer 1: text: required, answer skipped");
            HasMessage(r, "topic \"Ale\": answers: answer 2: must be a text or an object with \"text\", answer skipped");
        }

        [Test]
        public void TopicWhen_ToneNotAllowed()
        {
            DialogueParseResult r = Parse(Topics("{ \"caption\": \"Ale\", \"when\": { \"tone\": \"blunt\", \"time\": \"night\" }, \"answers\": [\"x\"] }"));
            HasMessage(r, "topic \"Ale\": when: tone: only allowed in answers; this condition never holds until fixed");
            Assert.IsFalse(r.File.Topics[0].When.Holds(new FakeFacts { Night = false }));
        }

        [Test]
        public void Question_TextOrPerTone()
        {
            DialogueParseResult r = Parse(Topics(
                "{ \"caption\": \"A\", \"question\": \"Ale?\", \"answers\": [\"x\"] }",
                "{ \"caption\": \"B\", \"question\": { \"polite\": \"Please?\", \"blunt\": \"Now.\", \"rude\": \"Oi\" }, \"answers\": [\"x\"] }"));
            Assert.AreEqual("Ale?", r.File.Topics[0].Question.For(Tones.Blunt));
            Assert.AreEqual("Please?", r.File.Topics[1].Question.For(Tones.Polite));
            Assert.AreEqual("Now.", r.File.Topics[1].Question.For(Tones.Blunt));
            Assert.AreEqual(QuestionText.Default(Tones.Normal), r.File.Topics[1].Question.For(Tones.Normal));
            HasMessage(r, "topic \"B\": question: rude: unknown tone (use polite, normal or blunt), ignored");
        }

        [Test]
        public void SetsClearsOnce()
        {
            DialogueParseResult r = Parse(Topics("{ \"caption\": \"A\", \"answers\": [\"x\"], \"sets\": [\"Heard Sailor\"], \"clears\": \"old\", \"once\": true }"));
            DialogueTopic t = r.File.Topics[0];
            CollectionAssert.AreEqual(new[] { "heard_sailor" }, t.Sets);
            CollectionAssert.AreEqual(new[] { "old" }, t.Clears);
            Assert.IsTrue(t.Once);
        }

        [Test]
        public void Once_MustBeBool()
        {
            DialogueParseResult r = Parse(Topics("{ \"caption\": \"A\", \"answers\": [\"x\"], \"once\": \"yes\" }"));
            HasMessage(r, "topic \"A\": once: must be true or false, ignored");
            Assert.IsFalse(r.File.Topics[0].Once);
        }

        [Test]
        public void TooManyTopics_WarnsAndTruncates()
        {
            List<string> topics = new List<string>();
            for (int i = 0; i < 45; i++)
                topics.Add("{ \"caption\": \"T" + i + "\", \"answers\": [\"x\"] }");
            DialogueParseResult r = Parse(Topics(topics.ToArray()));
            Assert.AreEqual(DialogueParser.MaxTopics, r.File.Topics.Count);
            HasMessage(r, Src + ": topics: more than 40 topics, the rest are ignored");
        }

        [Test]
        public void LongCaption_Warns()
        {
            DialogueParseResult r = Parse(Topics("{ \"caption\": \"A caption that is far too long\", \"answers\": [\"x\"] }"));
            Assert.AreEqual(1, r.File.Topics.Count);
            HasMessage(r, "caption: longer than 24 characters, may not fit the topic list");
        }

        [Test]
        public void UnknownFields_Warn_WithSuggestion()
        {
            DialogueParseResult r = Parse("{ \"topic\": [], \"topics\": [ { \"caption\": \"A\", \"answer\": [\"x\"], \"answers\": [\"y\"] } ] }");
            HasMessage(r, Src + ": topic: unknown field (did you mean \"topics\"?), ignored");
            HasMessage(r, Src + ": topic \"A\": answer: unknown field (did you mean \"answers\"?), ignored");
        }

        [Test]
        public void UnknownPlaceholder_Warns()
        {
            DialogueParseResult r = Parse(Topics("{ \"caption\": \"A\", \"question\": \"About {topic}?\", \"answers\": [\"Hi {playr}.\"] }"));
            HasMessage(r, "topic \"A\": answers: answer 1: unknown placeholder {playr}");
            Assert.IsFalse(r.Messages.Exists(delegate (string m) { return m.Contains("{topic}"); }));
        }

        [Test]
        public void Greetings_ParsedAndToneWarns()
        {
            DialogueParseResult r = Parse("{ \"greetings\": [ { \"when\": { \"tone\": \"blunt\" }, \"text\": \"Hm.\" }, \"Welcome, {player}!\" ] }");
            Assert.IsNotNull(r.File);
            Assert.AreEqual(2, r.File.Greetings.Count);
            Assert.AreEqual("Welcome, {player}!", r.File.Greetings[1].Text);
            HasMessage(r, Src + ": greetings: greeting 1: when: tone: only allowed in answers; this condition never holds until fixed");
        }

        [Test]
        public void Greeting_SetsNotAllowed()
        {
            DialogueParseResult r = Parse("{ \"greetings\": [ { \"text\": \"Hi\", \"sets\": \"x\" } ] }");
            HasMessage(r, "greetings: greeting 1: sets: not allowed in greetings, ignored");
            CollectionAssert.IsEmpty(r.File.Greetings[0].Sets);
        }

        [Test]
        public void InvalidJson_NoFile()
        {
            DialogueParseResult r = Parse("{ \"topics\": [ ");
            Assert.IsNull(r.File);
            HasMessage(r, Src + ": file: invalid JSON");
        }

        [Test]
        public void NothingInFile_NoFile()
        {
            DialogueParseResult r = Parse("{ }");
            Assert.IsNull(r.File);
            HasMessage(r, Src + ": file: needs \"topics\" or \"greetings\"");
        }

        [Test]
        public void TopicsNotAList()
        {
            DialogueParseResult r = Parse("{ \"topics\": { \"caption\": \"A\" } }");
            HasMessage(r, Src + ": topics: must be a list of topic objects");
        }
    }
}

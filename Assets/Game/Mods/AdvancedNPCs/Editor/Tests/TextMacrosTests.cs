using System.Collections.Generic;
using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class TextMacrosTests
    {
        [TestCase("Any rumours?", "any_rumours")]
        [TestCase("  The  North-Gate ", "the_north_gate")]
        [TestCase("tavern_wench", "tavern_wench")]
        [TestCase("Ale!!", "ale")]
        [TestCase("", "")]
        [TestCase("???", "")]
        public void NormalizeIds(string text, string expected)
        {
            Assert.AreEqual(expected, DialogueIds.Normalize(text));
        }

        [Test]
        public void Expand_ReplacesKnownAndKeepsUnknown()
        {
            Dictionary<string, string> values = new Dictionary<string, string> { { "player", "Testo" }, { "town", "Daggerfall" } };
            Assert.AreEqual("Hello Testo of Daggerfall, {foo} and {npc}.",
                TextMacros.Expand("Hello {player} of {town}, {foo} and {npc}.", values));
        }

        [Test]
        public void Expand_LeavesUnbalancedBraces()
        {
            Assert.AreEqual("a {player b} c", TextMacros.Expand("a {player b} c", new Dictionary<string, string> { { "player", "X" } }));
        }

        [Test]
        public void Unknown_FindsTyposAndTopicOnlyWhereAllowed()
        {
            CollectionAssert.AreEqual(new[] { "playr" }, TextMacros.Unknown("Hi {playr}, {npc}, {topic}", true));
            CollectionAssert.AreEqual(new[] { "playr", "topic" }, TextMacros.Unknown("Hi {playr}, {npc}, {topic}", false));
            CollectionAssert.IsEmpty(TextMacros.Unknown("plain text", false));
        }

        [TestCase(0, "Could you tell me about {topic}?")]
        [TestCase(1, "Tell me about {topic}.")]
        [TestCase(2, "What do you know about {topic}?")]
        [TestCase(-1, "Tell me about {topic}.")]
        public void Question_DefaultsPerTone(int tone, string expected)
        {
            Assert.AreEqual(expected, new QuestionText().For(tone));
        }

        [Test]
        public void Question_FallbackChain()
        {
            QuestionText all = new QuestionText { All = "Ale?" };
            Assert.AreEqual("Ale?", all.For(Tones.Blunt));
            QuestionText some = new QuestionText { Normal = "Ale, please.", Blunt = "Ale. Now." };
            Assert.AreEqual("Ale. Now.", some.For(Tones.Blunt));
            Assert.AreEqual("Ale, please.", some.For(Tones.Polite));
            Assert.AreEqual("Ale, please.", some.For(Tones.Normal));
        }

        [TestCase("Polite", 0)]
        [TestCase(" blunt ", 2)]
        [TestCase("rude", -1)]
        public void ParseTone(string text, int expected)
        {
            Assert.AreEqual(expected, Tones.Parse(text));
        }
    }
}

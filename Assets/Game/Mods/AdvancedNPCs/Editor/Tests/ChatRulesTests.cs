using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    /// <summary>ANPC conversations (spec conversations).</summary>
    public class ChatRulesTests
    {
        [TestCase(0.0, 4)]
        [TestCase(0.5, 6)]
        [TestCase(0.999, 8)]
        [TestCase(1.0, 8)]
        public void Lines_FourToEight(double roll, int expected)
        {
            Assert.AreEqual(expected, ChatRules.Lines(roll));
        }

        [Test]
        public void Pause_FromPointFourToOne()
        {
            Assert.AreEqual(0.4, ChatRules.Pause(0.0), 1e-9);
            Assert.AreEqual(1.0, ChatRules.Pause(1.0), 1e-9);
        }

        [Test]
        public void PairKey_IgnoresOrder()
        {
            Assert.AreEqual(ChatRules.PairKey("bram", "anna"), ChatRules.PairKey("anna", "bram"));
            Assert.AreNotEqual(ChatRules.PairKey("bram", "anna"), ChatRules.PairKey("bram", "cora"));
        }

        [Test]
        public void NextClip_NeverRepeatsWithTwoOrMore()
        {
            for (int last = 0; last < 3; last++)
            {
                foreach (double roll in new[] { 0.0, 0.3, 0.6, 0.99, 1.0 })
                {
                    int i = ChatRules.NextClip(3, last, roll);
                    Assert.AreNotEqual(last, i);
                    Assert.That(i, Is.InRange(0, 2));
                }
            }
        }

        [Test]
        public void NextClip_OneClipAndNone()
        {
            Assert.AreEqual(0, ChatRules.NextClip(1, 0, 0.7));
            Assert.AreEqual(-1, ChatRules.NextClip(0, -1, 0.7));
        }

        [Test]
        public void NextClip_LastOutOfRange_IsIgnored()
        {
            Assert.That(ChatRules.NextClip(2, 5, 1.0), Is.InRange(0, 1));     // the set was reloaded with fewer clips
        }

        [Test]
        public void Ineligible_Reasons()
        {
            Assert.IsNull(ChatRules.Ineligible(true, false, true, true, 2, true));
            Assert.AreEqual("not opted in", ChatRules.Ineligible(false, false, true, true, 2, true));
            Assert.AreEqual("already talking", ChatRules.Ineligible(true, true, true, true, 2, true));
            Assert.AreEqual("not calm", ChatRules.Ineligible(true, false, false, true, 2, true));
            Assert.AreEqual("voices not ANPC", ChatRules.Ineligible(true, false, true, false, 2, true));
            Assert.AreEqual("no talk sounds", ChatRules.Ineligible(true, false, true, true, 0, true));
            Assert.AreEqual("vanilla sprite", ChatRules.Ineligible(true, false, true, true, 2, false));
        }

        [Test]
        public void Memory_CooldownTwoHours_PerPair()
        {
            ChatMemory m = new ChatMemory();
            string ab = ChatRules.PairKey("a", "b");
            Assert.IsTrue(m.CanStart(ab, 10.0));
            m.Started(ab, 10.0);
            Assert.IsFalse(m.CanStart(ab, 11.9));
            Assert.IsTrue(m.CanStart(ab, 12.0));
            Assert.IsTrue(m.CanStart(ChatRules.PairKey("a", "c"), 10.5));
        }

        [Test]
        public void Memory_TimeWentBack_Allows()
        {
            ChatMemory m = new ChatMemory();
            m.Started("a|b", 50.0);
            Assert.IsTrue(m.CanStart("a|b", 40.0));     // an earlier save was loaded
        }

        [Test]
        public void Session_Alternates_AndEndsAfterLastLine()
        {
            ChatSession s = new ChatSession(3);
            Assert.IsTrue(s.NeedsLine);
            Assert.AreEqual(0, s.Speaker);
            Assert.AreEqual(1, s.Line);
            s.BeginLine(1.0);
            Assert.IsTrue(s.Speaking);
            s.Tick(0.5, 0.0);
            Assert.IsFalse(s.NeedsLine);
            s.Tick(0.6, 0.0);               // the line is over; the 0.4 s pause starts (0.1 s already gone)
            Assert.IsFalse(s.NeedsLine);
            Assert.IsFalse(s.Speaking);
            s.Tick(0.3, 0.0);               // the pause is over
            Assert.IsTrue(s.NeedsLine);
            Assert.AreEqual(1, s.Speaker);
            Assert.AreEqual(2, s.Line);
            s.BeginLine(1.0);
            s.Tick(2.0, 1.0);               // the line and the whole pause in one step
            Assert.IsTrue(s.NeedsLine);
            Assert.AreEqual(0, s.Speaker);
            Assert.AreEqual(3, s.Line);
            s.BeginLine(0.5);
            s.Tick(0.5, 0.0);
            Assert.IsTrue(s.Over);          // no pause after the last line
            Assert.IsFalse(s.NeedsLine);
        }

        [Test]
        public void Session_NothingPlayed_Ends()
        {
            ChatSession s = new ChatSession(4);
            s.BeginLine(0);
            Assert.IsTrue(s.Over);
        }

        [Test]
        public void Session_End_StopsEverything()
        {
            ChatSession s = new ChatSession(4);
            s.BeginLine(1.0);
            s.End();
            Assert.IsTrue(s.Over);
            s.Tick(5.0, 0.0);
            Assert.IsFalse(s.NeedsLine);
            Assert.IsFalse(s.Speaking);
        }

        [Test]
        public void Session_TickWhileWaitingForLine_DoesNothing()
        {
            ChatSession s = new ChatSession(4);
            s.Tick(10.0, 0.0);
            Assert.IsTrue(s.NeedsLine);
            Assert.AreEqual(1, s.Line);
        }
    }
}

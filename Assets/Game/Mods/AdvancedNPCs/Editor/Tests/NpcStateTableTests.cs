using System.Collections.Generic;
using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class NpcStateTableTests
    {
        [Test]
        public void GetOrCreate_NewId_IsFresh()
        {
            NpcStateTable t = new NpcStateTable();
            NpcState s = t.GetOrCreate("bram");
            Assert.IsFalse(s.dead);
            Assert.IsFalse(s.hostile);
            Assert.AreEqual(0UL, s.hostileUntil);
            Assert.AreEqual(1f, s.healthFraction);
            Assert.IsTrue(t.Has("bram"));
        }

        [Test]
        public void Asked_IsSaved_AndCopied()
        {
            NpcStateTable t = new NpcStateTable();
            NpcState s = t.GetOrCreate("bram");
            Assert.IsTrue(s.IsDefault());
            s.MarkAsked("ale");
            s.MarkAsked("ale");
            Assert.IsFalse(s.IsDefault());
            Assert.IsTrue(s.HasAsked("ale"));
            Dictionary<string, NpcState> snap = t.Snapshot();
            s.MarkAsked("bread");
            CollectionAssert.AreEqual(new[] { "ale" }, snap["bram"].asked);
        }

        [Test]
        public void Asked_MissingInOldSave_IsEmpty()
        {
            NpcState old = new NpcState();
            old.asked = null;
            Assert.IsFalse(old.HasAsked("ale"));
            Assert.IsTrue(old.IsDefault());
            Assert.IsNull(old.Clone().asked);
        }

        [Test]
        public void GetOrCreate_ReturnsSameInstance()
        {
            NpcStateTable t = new NpcStateTable();
            t.GetOrCreate("bram").dead = true;
            Assert.IsTrue(t.GetOrCreate("bram").dead);
        }

        [Test]
        public void Snapshot_IsIndependentCopy()
        {
            NpcStateTable t = new NpcStateTable();
            t.GetOrCreate("bram").healthFraction = 0.5f;
            Dictionary<string, NpcState> snap = t.Snapshot();
            t.GetOrCreate("bram").healthFraction = 0.25f;
            Assert.AreEqual(0.5f, snap["bram"].healthFraction);
        }

        [Test]
        public void Snapshot_OmitsFreshStates()
        {
            NpcStateTable t = new NpcStateTable();
            t.GetOrCreate("fresh");
            t.GetOrCreate("hurt").healthFraction = 0.5f;
            t.GetOrCreate("dead").dead = true;
            t.GetOrCreate("angry").hostile = true;
            Dictionary<string, NpcState> snap = t.Snapshot();
            Assert.IsFalse(snap.ContainsKey("fresh"));
            Assert.IsTrue(snap.ContainsKey("hurt"));
            Assert.IsTrue(snap.ContainsKey("dead"));
            Assert.IsTrue(snap.ContainsKey("angry"));
            Assert.IsTrue(t.Has("fresh")); // only the snapshot drops it
        }

        [Test]
        public void IsDefault_IgnoresOldCalmDeadline()
        {
            NpcState s = new NpcState();
            s.hostileUntil = 500UL;
            Assert.IsTrue(s.IsDefault());
            s.hostile = true;
            Assert.IsFalse(s.IsDefault());
        }

        [Test]
        public void Restore_ReplacesContentAndKeepsUnknownIds()
        {
            NpcStateTable t = new NpcStateTable();
            t.GetOrCreate("old").dead = true;

            Dictionary<string, NpcState> saved = new Dictionary<string, NpcState>();
            NpcState s = new NpcState();
            s.hostile = true;
            s.hostileUntil = 99UL;
            s.healthFraction = 0.7f;
            saved["bram"] = s;
            saved["removed_from_disk"] = new NpcState();

            t.Restore(saved);
            s.healthFraction = 0.1f; // mutating the source must not affect the table

            Assert.IsFalse(t.Has("old"));
            Assert.IsTrue(t.Has("removed_from_disk"));
            NpcState r = t.GetOrCreate("bram");
            Assert.IsTrue(r.hostile);
            Assert.AreEqual(99UL, r.hostileUntil);
            Assert.AreEqual(0.7f, r.healthFraction);
        }

        [Test]
        public void Restore_Null_Clears()
        {
            NpcStateTable t = new NpcStateTable();
            t.GetOrCreate("bram");
            t.Restore(null);
            Assert.IsFalse(t.Has("bram"));
        }

        [Test]
        public void Remove_ForgetsOneId()
        {
            NpcStateTable t = new NpcStateTable();
            t.GetOrCreate("selftest_a").dead = true;
            t.GetOrCreate("bram");
            t.Remove("selftest_a");
            Assert.IsFalse(t.Has("selftest_a"));
            Assert.IsTrue(t.Has("bram"));
        }

        [Test]
        public void Clear_ResetsEverything()
        {
            NpcStateTable t = new NpcStateTable();
            t.GetOrCreate("bram").dead = true;
            t.Clear();
            Assert.IsFalse(t.Has("bram"));
            Assert.IsFalse(t.GetOrCreate("bram").dead);
        }
    }
}

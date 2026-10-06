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
            Assert.AreEqual(-1, s.health);
            Assert.IsTrue(t.Has("bram"));
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
            t.GetOrCreate("bram").health = 10;
            Dictionary<string, NpcState> snap = t.Snapshot();
            t.GetOrCreate("bram").health = 3;
            Assert.AreEqual(10, snap["bram"].health);
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
            s.health = 7;
            saved["bram"] = s;
            saved["removed_from_disk"] = new NpcState();

            t.Restore(saved);
            s.health = 1; // mutating the source must not affect the table

            Assert.IsFalse(t.Has("old"));
            Assert.IsTrue(t.Has("removed_from_disk"));
            NpcState r = t.GetOrCreate("bram");
            Assert.IsTrue(r.hostile);
            Assert.AreEqual(99UL, r.hostileUntil);
            Assert.AreEqual(7, r.health);
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

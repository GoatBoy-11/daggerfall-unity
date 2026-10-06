using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class SpawnRulesTests
    {
        [TestCase(true, false, false)]
        [TestCase(true, true, true)]
        [TestCase(true, true, false)]
        public void DeadNpc_IsNeverSpawned(bool dead, bool hasLive, bool liveInThisLocation)
        {
            Assert.AreEqual(SpawnAction.Skip, SpawnRules.Decide(dead, hasLive, liveInThisLocation));
        }

        [Test]
        public void NoLiveCopy_Spawns()
        {
            Assert.AreEqual(SpawnAction.Spawn, SpawnRules.Decide(false, false, false));
        }

        [Test]
        public void LiveCopyInThisTown_IsKept()
        {
            Assert.AreEqual(SpawnAction.Skip, SpawnRules.Decide(false, true, true));
        }

        [Test]
        public void LiveCopyUnderOldTownBeingDestroyed_IsReplaced()
        {
            // Fast travel: DFU destroys the old town object over many frames while it builds the new one.
            Assert.AreEqual(SpawnAction.ReplaceStale, SpawnRules.Decide(false, true, false));
        }
    }
}

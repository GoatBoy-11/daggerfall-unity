namespace AdvancedNPCs.Core
{
    public enum SpawnAction
    {
        Spawn,
        Skip,
        ReplaceStale,
    }

    /// <summary>What to do with an NPC when its town's GameObject is (re)created.</summary>
    public static class SpawnRules
    {
        /// <param name="dead">NPC is dead in the saved state.</param>
        /// <param name="hasLive">A spawned copy of the NPC still exists.</param>
        /// <param name="liveInThisLocation">That copy is parented to the town being spawned.</param>
        public static SpawnAction Decide(bool dead, bool hasLive, bool liveInThisLocation)
        {
            if (dead)
                return SpawnAction.Skip;
            if (!hasLive)
                return SpawnAction.Spawn;
            // DFU destroys an old town object over many frames after fast travel or load, while it builds
            // the new one; a copy under a different town object is stale and must make way.
            return liveInThisLocation ? SpawnAction.Skip : SpawnAction.ReplaceStale;
        }
    }
}

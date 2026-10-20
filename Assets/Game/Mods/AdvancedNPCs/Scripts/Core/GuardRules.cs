namespace AdvancedNPCs.Core
{
    /// <summary>Lawful pursuit is separate from personal anger and enemy attitude.</summary>
    public static class GuardRules
    {
        public static bool ValidCrime(int crime) { return crime >= 1 && crime <= 15; }
        public static bool Wanted(NpcState state, int region)
        { return !state.dead && ValidCrime(state.wantedCrime) && state.wantedRegion == region; }
    }
}

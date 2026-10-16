namespace AdvancedNPCs.Core
{
    /// <summary>Lawful pursuit is separate from personal anger and enemy attitude.</summary>
    public static class GuardRules
    {
        public const ulong CustodySeconds = 24 * 60 * 60;
        public static bool ValidCrime(int crime) { return crime >= 1 && crime <= 15; }
        public static bool InCustody(NpcState state, ulong now) { return state.arrestedUntil > now; }
        public static bool Wanted(NpcState state, int region, ulong now)
        { return !state.dead && !InCustody(state, now) && ValidCrime(state.wantedCrime) && state.wantedRegion == region; }
        public static bool Surrenders(Bravery bravery, float healthFraction)
        { return bravery != Bravery.Brave || healthFraction <= 0.25f; }
        public static void Arrest(NpcState state, ulong now)
        {
            state.wantedCrime = 0;
            state.wantedRegion = 0;
            state.arrestedUntil = now + CustodySeconds;
            state.hostile = false;
            state.hostileUntil = 0;
            state.enemyOn = false;
            state.enemyOff = true;
        }
    }
}

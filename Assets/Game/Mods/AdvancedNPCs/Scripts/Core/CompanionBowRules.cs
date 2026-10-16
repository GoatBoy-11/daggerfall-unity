namespace AdvancedNPCs.Core
{
    /// <summary>Separate enter/leave distances prevent repeated weapon switching at the edge of melee.</summary>
    public static class CompanionBowRules
    {
        public const float MeleeRange=6, DrawRange=8, MaximumRange=25, ShotSeconds=2;
        public static bool UseBow(bool equipped, bool ready, float distance)
        { return ready && distance>=(equipped?MeleeRange:DrawRange); }
        public static bool InRange(float distance)
        { return distance>=MeleeRange && distance<=MaximumRange; }
    }
}

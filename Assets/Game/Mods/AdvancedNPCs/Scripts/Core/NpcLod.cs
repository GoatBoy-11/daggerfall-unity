namespace AdvancedNPCs.Core
{
    /// <summary>
    /// Update level of detail (spec opinion-lod §B): ANPCs far from the player do their per-frame work less often.
    /// No Unity or DFU types.
    /// </summary>
    public static class NpcLod
    {
        public const float NearDistance = 40f;
        public const float FarDistance = 100f;
        /// <summary>Beyond this the greeter does not look for anyone (no one sees a greeting that far away).</summary>
        public const float GreetDistance = 30f;
        public const int Phases = 8;

        /// <summary>Every how many frames the work runs: 1 near or when busy, 3 farther, 8 far away.</summary>
        public static int Interval(float distanceToPlayer, bool busy)
        {
            if (busy || distanceToPlayer < NearDistance)
                return 1;
            return distanceToPlayer < FarDistance ? 3 : 8;
        }

        /// <summary>Whether this frame is one of the ANPC's working frames (its phase spreads far ANPCs over frames).</summary>
        public static bool Due(int frame, int phase, int interval)
        {
            return interval <= 1 || (frame + phase) % interval == 0;
        }
    }
}

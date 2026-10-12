namespace AdvancedNPCs.Core
{
    /// <summary>Brave patrols (spec lazy-sprites §D): who a calm Brave ANPC attacks on sight. No Unity or DFU types.</summary>
    public static class AggroRules
    {
        public const float DefaultRange = 15f;
        public const double MinRange = 1, MaxRange = 50;
        /// <summary>Seconds between looks around.</summary>
        public const float CheckInterval = 0.5f;

        /// <summary>
        /// An enemy ANPC (hostile attitude), or a vanilla enemy hostile to the player that is not the city watch or a
        /// player ally; alive. ANPCs only angry at the player are not foes.
        /// </summary>
        public static bool IsFoe(bool isAnpc, bool anpcIsEnemy, bool vanillaHostile, bool watchOrAlly, bool alive)
        {
            if (!alive)
                return false;
            return isAnpc ? anpcIsEnemy : vanillaHostile && !watchOrAlly;
        }
    }
}

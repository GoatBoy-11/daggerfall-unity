namespace AdvancedNPCs.Core
{
    /// <summary>Brave patrols (spec lazy-sprites §D): who a calm Brave ANPC attacks on sight. No Unity or DFU types.</summary>
    public static class AggroRules
    {
        public const float DefaultRange = 15f;
        public const double MinRange = 1, MaxRange = 50;
        /// <summary>Seconds between looks around.</summary>
        public const float CheckInterval = 0.5f;
        /// <summary>A creature fight ends when the foe is farther than this (metres) for the usual range.</summary>
        public const float BaseGiveUpDistance = 40f;

        /// <summary>
        /// Distance at which an ANPC gives up a fight with a creature: 40 m, or 10 m beyond its aggroRange, so a patrol
        /// never starts a fight that is given up at once.
        /// </summary>
        public static float GiveUpDistance(float aggroRange)
        {
            return System.Math.Max(BaseGiveUpDistance, aggroRange + 10f);
        }

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

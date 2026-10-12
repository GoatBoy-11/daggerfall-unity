using System;

namespace AdvancedNPCs.Core
{
    /// <summary>
    /// An ANPC's personal opinion of the player (spec opinion-lod §A): -100..100, added to DFU's regional reaction,
    /// drifting back toward 0 by 2 points per game day. No Unity or DFU types.
    /// </summary>
    public static class OpinionRules
    {
        public const int Min = -100, Max = 100;
        public const int DecayPerDay = 2;
        /// <summary>The player attacked this ANPC.</summary>
        public const int Assault = -25;
        /// <summary>This ANPC saw the player attack or kill another ANPC.</summary>
        public const int Witness = -10;
        /// <summary>The enemy this ANPC fought died with the player close by.</summary>
        public const int Helped = 10;
        public const float WitnessRange = 15f;
        public const float HelpRange = 20f;
        const ulong SecondsPerDay = 86400;

        /// <summary>The opinion now, after drifting toward 0 since the last change (game seconds).</summary>
        public static int Current(int opinion, ulong at, ulong now)
        {
            if (opinion == 0 || now <= at)
                return opinion;
            ulong days = (now - at) / SecondsPerDay;
            long drift = (long)Math.Min(days * DecayPerDay, (ulong)Math.Abs(opinion));
            return opinion > 0 ? opinion - (int)drift : opinion + (int)drift;
        }

        /// <summary>The opinion after a change: drifted first, then changed, clamped to -100..100.</summary>
        public static int Change(int opinion, ulong at, ulong now, int delta)
        {
            return Math.Max(Min, Math.Min(Max, Current(opinion, at, now) + delta));
        }

        /// <summary>DFU's reaction plus the opinion, as a dialogue band (dislikes, neutral, likes, loves).</summary>
        public static string Band(int reaction, int opinion)
        {
            return ConditionParser.ReactionBand(reaction + opinion);
        }
    }
}

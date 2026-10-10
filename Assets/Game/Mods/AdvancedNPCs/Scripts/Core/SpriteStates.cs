using System;
using System.Collections.Generic;

namespace AdvancedNPCs.Core
{
    /// <summary>Sheet names to animation states (spec 1b §6.1): idle, idle_2, walk_1, ... ; fallbacks; set choice.</summary>
    public static class SpriteStates
    {
        public const string Idle = "idle";
        public const string Walk = "walk";
        public const string Hit = "hit";
        public const string Attack = "attack";
        public const string Cast = "cast";
        public const string Death = "death";

        static readonly string[] All = { Idle, Walk, Hit, Attack, Cast, Death };

        /// <summary>The state of a sheet name ("walk" or "walk_&lt;number&gt;", any case), or null.</summary>
        public static string StateOf(string sheetName)
        {
            if (string.IsNullOrEmpty(sheetName))
                return null;
            string n = sheetName.ToLowerInvariant();
            foreach (string state in All)
            {
                if (n == state)
                    return state;
                if (n.Length > state.Length + 1 && n.StartsWith(state + "_", StringComparison.Ordinal) && IsDigits(n, state.Length + 1))
                    return state;
            }
            return null;
        }

        /// <summary>
        /// Sheets for a state, sorted by name; cast falls back to attack sheets only (none: the spell plays no animation);
        /// walk, hit and attack fall back to idle; death has no fallback.
        /// </summary>
        public static List<SpriteAnimation> Variants(SpriteSet set, string state)
        {
            List<SpriteAnimation> found = Own(set, state);
            if (found.Count == 0 && state == Cast)
                return Own(set, Attack);
            if (found.Count == 0 && (state == Walk || state == Hit || state == Attack))
                return Variants(set, Idle);
            return found;
        }

        static List<SpriteAnimation> Own(SpriteSet set, string state)
        {
            List<SpriteAnimation> found = new List<SpriteAnimation>();
            foreach (SpriteAnimation a in set.Animations.Values)
            {
                if (StateOf(a.Name) == state)
                    found.Add(a);
            }
            found.Sort(delegate (SpriteAnimation x, SpriteAnimation y) { return string.CompareOrdinal(x.Name, y.Name); });
            return found;
        }

        /// <summary>A set can only be drawn if it has an idle sheet.</summary>
        public static bool IsValid(SpriteSet set)
        {
            foreach (SpriteAnimation a in set.Animations.Values)
            {
                if (StateOf(a.Name) == Idle)
                    return true;
            }
            return false;
        }

        /// <summary>Which of several sprite sets this person wears: same seed, same set (spec 1b §6.5).</summary>
        public static int PickSet(int count, uint seed)
        {
            if (count <= 1)
                return 0;
            return new SeededRandom(seed ^ 0x2545F491u).Next(count);
        }

        static bool IsDigits(string s, int from)
        {
            for (int i = from; i < s.Length; i++)
            {
                if (s[i] < '0' || s[i] > '9')
                    return false;
            }
            return true;
        }
    }

    /// <summary>Which of the 8 sheet rows to draw (spec 1b §6.2, the same idea as DFU's billboard orientation).</summary>
    public static class SpriteDirections
    {
        /// <summary>
        /// Row 0 front, 1 front_right, 2 right, 3 back_right, 4 back, 5 back_left, 6 left, 7 front_left, from the
        /// flat direction to the camera (cam) and the NPC's flat forward (fwd). Camera on the NPC's right gives row 2.
        /// </summary>
        public static int Row(float camX, float camZ, float fwdX, float fwdZ)
        {
            if ((camX == 0 && camZ == 0) || (fwdX == 0 && fwdZ == 0))
                return 0;
            // The NPC's right, seen from above with +Y up: (fwd.z, -fwd.x).
            double side = camX * fwdZ - camZ * fwdX;
            double ahead = camX * fwdX + camZ * fwdZ;
            double degrees = Math.Atan2(side, ahead) * 180.0 / Math.PI;
            int row = (int)Math.Floor(degrees / 45.0 + 0.5);
            return ((row % 8) + 8) % 8;
        }
    }
}

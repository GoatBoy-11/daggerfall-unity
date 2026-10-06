using System.Collections.Generic;

namespace AdvancedNPCs.Core
{
    /// <summary>Small deterministic PRNG (xorshift32). The same seed always gives the same sequence.</summary>
    public class SeededRandom
    {
        uint state;

        public SeededRandom(uint seed)
        {
            state = seed == 0 ? 0x9E3779B9u : seed; // xorshift never leaves 0
        }

        public uint NextUInt()
        {
            uint x = state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            state = x;
            return x;
        }

        /// <summary>0 &lt;= result &lt; max; 0 when max &lt;= 0.</summary>
        public int Next(int max)
        {
            if (max <= 0)
                return 0;
            return (int)(NextUInt() % (uint)max);
        }

        /// <summary>min &lt;= result &lt;= max; min when max &lt;= min.</summary>
        public int Range(int min, int max)
        {
            if (max <= min)
                return min;
            return min + Next(max - min + 1);
        }

        /// <summary>One element of a non-empty list.</summary>
        public T Pick<T>(IList<T> list)
        {
            return list[Next(list.Count)];
        }
    }
}

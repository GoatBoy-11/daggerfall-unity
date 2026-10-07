using System;
using System.Collections.Generic;

namespace AdvancedNPCs.Core
{
    /// <summary>
    /// Portrait pools (spec v2.1 §6): "bram" stands for bram.png and every bram_&lt;number&gt;.png in _Portraits.
    /// Names here are normalised (PortraitNames): lowercase, no ".png".
    /// </summary>
    public static class PortraitPools
    {
        public static List<string> Pool(ICollection<string> files, string baseName)
        {
            List<string> pool = new List<string>();
            foreach (string file in files)
            {
                if (file == baseName || IsNumbered(file, baseName))
                    pool.Add(file);
            }
            pool.Sort(StringComparer.Ordinal);
            return pool;
        }

        /// <summary>The pools of several names together, without repeats.</summary>
        public static List<string> Joined(ICollection<string> files, IList<string> baseNames)
        {
            List<string> joined = new List<string>();
            foreach (string baseName in baseNames)
            {
                foreach (string file in Pool(files, baseName))
                {
                    if (!joined.Contains(file))
                        joined.Add(file);
                }
            }
            joined.Sort(StringComparer.Ordinal);
            return joined;
        }

        /// <summary>
        /// One file of the pool for the person with this seed (spec v2.1 §6.2): the same person always gets the same
        /// file, a re-rolled person (Random each visit) a new one; null for an empty pool.
        /// </summary>
        public static string Choose(List<string> pool, uint seed)
        {
            if (pool == null || pool.Count == 0)
                return null;
            // Mixed so the pick does not mirror the person's other seeded choices.
            return pool[new SeededRandom(seed ^ 0x5F3759DFu).Next(pool.Count)];
        }

        static bool IsNumbered(string file, string baseName)
        {
            if (file.Length <= baseName.Length + 1 || !file.StartsWith(baseName + "_", StringComparison.Ordinal))
                return false;
            for (int i = baseName.Length + 1; i < file.Length; i++)
            {
                if (file[i] < '0' || file[i] > '9')
                    return false;
            }
            return true;
        }
    }
}

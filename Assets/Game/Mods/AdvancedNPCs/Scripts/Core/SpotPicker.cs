using System;
using System.Collections.Generic;

namespace AdvancedNPCs.Core
{
    /// <summary>Chooses where groups stand among candidate points (outside-towns spec §5). Points are {x, y, z}.</summary>
    public static class SpotPicker
    {
        /// <summary>
        /// One distinct candidate index per group, chosen by seed among the candidates at least avoidRadius from
        /// avoid (no limit when avoid is null); -1 for groups left without a spot.
        /// </summary>
        public static List<int> Pick(IList<float[]> candidates, float[] avoid, float avoidRadius, int groups, uint seed)
        {
            List<int> usable = new List<int>();
            for (int i = 0; i < candidates.Count; i++)
            {
                if (avoid == null || Distance(candidates[i], avoid) >= avoidRadius)
                    usable.Add(i);
            }
            SeededRandom rng = new SeededRandom(seed);
            for (int i = usable.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                int t = usable[i];
                usable[i] = usable[j];
                usable[j] = t;
            }
            List<int> picks = new List<int>();
            for (int g = 0; g < groups; g++)
                picks.Add(g < usable.Count ? usable[g] : -1);
            return picks;
        }

        /// <summary>
        /// Where each member of a group stands relative to its spot, as {dx, dz}: the first on it, the rest 2-3 m
        /// around it and at least 1 m from each other.
        /// </summary>
        public static List<float[]> GroupOffsets(int size, uint seed)
        {
            List<float[]> offsets = new List<float[]>();
            if (size <= 0)
                return offsets;
            offsets.Add(new float[] { 0f, 0f });
            SeededRandom rng = new SeededRandom(seed);
            for (int n = 1; n < size; n++)
            {
                float[] best = null;
                for (int attempt = 0; attempt < 30 && best == null; attempt++)
                {
                    double angle = rng.Next(3600) / 3600.0 * 2 * Math.PI;
                    double r = 2.0 + rng.Next(1000) / 1000.0;
                    float[] o = { (float)(Math.Cos(angle) * r), (float)(Math.Sin(angle) * r) };
                    bool spaced = offsets.TrueForAll(delegate (float[] p) { return Flat(p, o) >= 1.0; });
                    if (spaced)
                        best = o;
                }
                if (best == null)
                {
                    // Crowded ring: spread evenly instead (2.5 m, equal angles).
                    double angle = 2 * Math.PI * n / size;
                    best = new float[] { (float)(Math.Cos(angle) * 2.5), (float)(Math.Sin(angle) * 2.5) };
                }
                offsets.Add(best);
            }
            return offsets;
        }

        /// <summary>
        /// Stable order (x, then y, then z, each to the centimetre), so the same place gives the same candidate list every
        /// visit even when its points come back through a transform with tiny float differences.
        /// </summary>
        public static void Sort(List<float[]> points)
        {
            points.Sort(delegate (float[] a, float[] b)
            {
                int c = Cm(a[0]).CompareTo(Cm(b[0]));
                if (c == 0)
                    c = Cm(a[1]).CompareTo(Cm(b[1]));
                if (c == 0)
                    c = Cm(a[2]).CompareTo(Cm(b[2]));
                return c;
            });
        }

        static long Cm(float v)
        {
            return (long)Math.Round(v * 100.0);
        }

        static double Distance(float[] a, float[] b)
        {
            double dx = a[0] - b[0], dy = a[1] - b[1], dz = a[2] - b[2];
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        static double Flat(float[] a, float[] b)
        {
            double dx = a[0] - b[0], dz = a[1] - b[1];
            return Math.Sqrt(dx * dx + dz * dz);
        }
    }
}

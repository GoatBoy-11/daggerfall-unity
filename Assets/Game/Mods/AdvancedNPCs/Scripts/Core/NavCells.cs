using System;
using System.Collections.Generic;

namespace AdvancedNPCs.Core
{
    /// <summary>
    /// Walkable cells of a town's navigation grid, numbered in row order (y, then x), without building a list
    /// of every cell: a big city has over 200,000 of them and only a dozen are needed.
    /// </summary>
    public static class NavCells
    {
        public static int Count(int width, int height, Func<int, int, bool> walkable)
        {
            int count = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (walkable(x, y))
                        count++;
                }
            }
            return count;
        }

        /// <summary>Grid coordinates [x, y] of the walkable cells with these indices; unknown indices are left out.</summary>
        public static Dictionary<int, int[]> Resolve(int width, int height, Func<int, int, bool> walkable, ICollection<int> indices)
        {
            Dictionary<int, int[]> cells = new Dictionary<int, int[]>();
            HashSet<int> wanted = new HashSet<int>(indices);
            int n = 0;
            for (int y = 0; y < height && cells.Count < wanted.Count; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!walkable(x, y))
                        continue;
                    if (wanted.Contains(n))
                        cells[n] = new int[] { x, y };
                    n++;
                }
            }
            return cells;
        }
    }
}

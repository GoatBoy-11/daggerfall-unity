using System.Collections.Generic;
using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class NavCellsTests
    {
        // 3 x 2 grid, walkable cells marked 1:
        //   y=0: 1 0 1
        //   y=1: 0 1 1
        static readonly int[,] Grid = { { 1, 0 }, { 0, 1 }, { 1, 1 } }; // [x, y]

        static bool Walkable(int x, int y)
        {
            return Grid[x, y] == 1;
        }

        [Test]
        public void Count_CountsWalkableCells()
        {
            Assert.AreEqual(4, NavCells.Count(3, 2, Walkable));
            Assert.AreEqual(0, NavCells.Count(0, 0, Walkable));
        }

        [Test]
        public void Resolve_MapsIndicesInRowOrder()
        {
            // Walkable order: (0,0) (2,0) (1,1) (2,1)
            Dictionary<int, int[]> cells = NavCells.Resolve(3, 2, Walkable, new[] { 1, 3 });
            Assert.AreEqual(2, cells.Count);
            CollectionAssert.AreEqual(new[] { 2, 0 }, cells[1]);
            CollectionAssert.AreEqual(new[] { 2, 1 }, cells[3]);
        }

        [Test]
        public void Resolve_IgnoresIndicesBeyondTheGrid()
        {
            Dictionary<int, int[]> cells = NavCells.Resolve(3, 2, Walkable, new[] { 0, 9 });
            Assert.AreEqual(1, cells.Count);
            CollectionAssert.AreEqual(new[] { 0, 0 }, cells[0]);
        }

        [Test]
        public void AnyMatches_OnlyWhenATemplateSpawnsInTheTown()
        {
            NpcDefinition t = new NpcDefinition();
            t.Id = "commoner";
            t.Kind = NpcKind.Generic;
            t.Spawn = new GenericSpawn();
            List<NpcDefinition> templates = new List<NpcDefinition> { t };
            Assert.IsTrue(PopulationPlanner.AnyMatches(templates, new TownInfo(1, "R", "P", "TownCity", "Breton")));
            Assert.IsFalse(PopulationPlanner.AnyMatches(templates, new TownInfo(1, "R", "P", "DungeonKeep", "Breton")));
            Assert.IsFalse(PopulationPlanner.AnyMatches(new List<NpcDefinition>(), new TownInfo(1, "R", "P", "TownCity", "Breton")));
        }
    }
}

using System.Collections.Generic;
using System.Globalization;

namespace AdvancedNPCs.Core
{
    /// <summary>The "GenericPeople" mod setting (spec §8).</summary>
    public enum GenericMode
    {
        SamePeople,
        RandomEachVisit,
    }

    /// <summary>Makes a person's name for a race and gender; the same seed must give the same name.</summary>
    public interface INameSource
    {
        string Generate(string race, string gender, uint seed);
    }

    /// <summary>Decides which generic people a town gets (spec §7.4). Pure and deterministic.</summary>
    public static class PopulationPlanner
    {
        const int MaxCellAttempts = 10;

        public static string KeyFor(string templateId, int mapId, int n)
        {
            return templateId + "@" + mapId.ToString(CultureInfo.InvariantCulture) + "#" + n.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>True if any generic template spawns in this town (checked before scanning its grid).</summary>
        public static bool AnyMatches(IList<NpcDefinition> templates, TownInfo town)
        {
            if (templates == null)
                return false;
            foreach (NpcDefinition t in templates)
            {
                SpawnPlace place;
                if (t.Kind == NpcKind.Generic && t.Spawn != null && t.Spawn.Matches(town, out place))
                    return true;
            }
            return false;
        }

        /// <param name="cellCount">Number of walkable cells in the town; instances get an index into that list.</param>
        /// <param name="visitSeed">Seed for "Random each visit"; ignored for "Same people every visit".</param>
        public static List<NpcInstance> Plan(IList<NpcDefinition> templates, TownInfo town, GenericMode mode, int cap,
            int cellCount, uint visitSeed, INameSource names)
        {
            List<NpcInstance> result = new List<NpcInstance>();
            if (cap <= 0 || templates == null)
                return result;

            bool same = mode == GenericMode.SamePeople;
            SeededRandom visit = new SeededRandom(visitSeed);
            HashSet<int> usedCells = new HashSet<int>();

            List<NpcDefinition> ordered = new List<NpcDefinition>(templates);
            ordered.Sort(delegate (NpcDefinition a, NpcDefinition b) { return string.CompareOrdinal(a.Id, b.Id); });

            foreach (NpcDefinition t in ordered)
            {
                if (t.Kind != NpcKind.Generic || t.Spawn == null)
                    continue;
                SpawnPlace place;
                if (!t.Spawn.Matches(town, out place))
                    continue;

                string townKey = t.Id + "@" + town.MapId.ToString(CultureInfo.InvariantCulture);
                SeededRandom countRandom = new SeededRandom(same ? StableHash.Of(townKey) : visit.NextUInt());
                int count = countRandom.Range(t.Spawn.CountMin, t.Spawn.CountMax);

                for (int n = 0; n < count && result.Count < cap; n++)
                {
                    NpcInstance i = new NpcInstance();
                    i.Key = KeyFor(t.Id, town.MapId, n);
                    i.Definition = t;
                    i.Persistent = same;
                    i.Seed = same ? StableHash.Of(i.Key) : visit.NextUInt();

                    // Fixed order: gender, name, portrait, face, position.
                    SeededRandom rng = new SeededRandom(i.Seed);
                    i.Gender = NpcInstance.PickGender(t.Gender, rng);
                    i.Race = string.IsNullOrEmpty(t.Race) ? town.DefaultRace : t.Race;
                    i.Name = PickName(t, i, rng, names);
                    i.PortraitName = t.Portraits.Count > 0 ? rng.Pick(t.Portraits) : null;
                    i.FaceOutfit = rng.Next(VanillaFaces.OutfitVariants);
                    i.FaceVariant = rng.Next(VanillaFaces.FaceVariants);

                    if (place != null && n < place.Positions.Count)
                    {
                        float[] p = place.Positions[n];
                        i.HasFixedPosition = true;
                        i.X = p[0];
                        i.Y = p[1];
                        i.Z = p[2];
                    }
                    else
                    {
                        i.CellIndex = PickCell(rng, cellCount, usedCells);
                    }
                    result.Add(i);
                }
            }
            return result;
        }

        static string PickName(NpcDefinition t, NpcInstance i, SeededRandom rng, INameSource names)
        {
            if (t.Names.Count > 0)
                return rng.Pick(t.Names);
            if (!string.IsNullOrEmpty(t.Name))
                return t.Name;
            return names.Generate(i.Race, i.Gender, rng.NextUInt());
        }

        /// <summary>A cell not used yet if one turns up within a few tries; -1 when the town has no cells.</summary>
        static int PickCell(SeededRandom rng, int cellCount, HashSet<int> used)
        {
            if (cellCount <= 0)
                return -1;
            int cell = rng.Next(cellCount);
            for (int attempt = 1; attempt < MaxCellAttempts && used.Contains(cell); attempt++)
                cell = rng.Next(cellCount);
            used.Add(cell);
            return cell;
        }
    }
}

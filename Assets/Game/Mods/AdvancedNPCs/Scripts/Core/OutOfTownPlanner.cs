using System.Collections.Generic;
using System.Globalization;

namespace AdvancedNPCs.Core
{
    /// <summary>A dungeon or a building the player has entered (outside-towns spec §6).</summary>
    public class PlaceInfo
    {
        public const string DungeonKind = "dungeon";
        public const string InteriorKind = "interior";

        public string Kind;
        public int MapId;
        public string Region;
        public string Place;
        /// <summary>The region's people ("Breton", "Redguard", "Nord").</summary>
        public string DefaultRace;
        /// <summary>Dungeons: a DungeonTypeNames name.</summary>
        public string DungeonType;
        /// <summary>Interiors: a BuildingNames key ("tavern", "guildhall", "house", ...).</summary>
        public string Building;
        /// <summary>Interiors: for guild halls the guild's key ("fightersguild", ...), else null.</summary>
        public string Guild;
        public int BuildingKey;

        public static PlaceInfo Dungeon(int mapId, string region, string place, string defaultRace, string dungeonType)
        {
            PlaceInfo p = new PlaceInfo();
            p.Kind = DungeonKind;
            p.MapId = mapId;
            p.Region = region;
            p.Place = place;
            p.DefaultRace = defaultRace;
            p.DungeonType = dungeonType;
            return p;
        }

        public static PlaceInfo Interior(int mapId, string region, string place, string defaultRace, string building, string guild, int buildingKey)
        {
            PlaceInfo p = new PlaceInfo();
            p.Kind = InteriorKind;
            p.MapId = mapId;
            p.Region = region;
            p.Place = place;
            p.DefaultRace = defaultRace;
            p.Building = building;
            p.Guild = guild;
            p.BuildingKey = buildingKey;
            return p;
        }

        /// <summary>The placed-person context: "dungeon" or "b&lt;buildingKey&gt;".</summary>
        public string Context
        {
            get { return Kind == DungeonKind ? "dungeon" : "b" + BuildingKey.ToString(CultureInfo.InvariantCulture); }
        }

        /// <summary>"&lt;mapId&gt;:dungeon" or "&lt;mapId&gt;:b&lt;buildingKey&gt;"; people are "template@PlaceKey#n".</summary>
        public string PlaceKey
        {
            get { return MapId.ToString(CultureInfo.InvariantCulture) + ":" + Context; }
        }

        public bool IsDungeon
        {
            get { return Kind == DungeonKind; }
        }
    }

    /// <summary>
    /// Who appears in a dungeon or building (outside-towns spec §3, §6): per template with the matching block, the
    /// filter, `when`, one chance roll per visit and a count; keys and seeds as for town people. Every template with
    /// the block gets one explanation line (anpc_here).
    /// </summary>
    public static class OutOfTownPlanner
    {
        /// <param name="facts">For `when`; null skips the check.</param>
        /// <param name="explain">Receives one line per template that has the place's block.</param>
        public static List<NpcInstance> Plan(IList<NpcDefinition> templates, PlaceInfo place, GenericMode mode, int cap, uint visitSeed,
            INameSource names, IDialogueFacts facts, List<string> explain)
        {
            List<NpcInstance> result = new List<NpcInstance>();
            if (templates == null || place == null)
                return result;
            bool same = mode == GenericMode.SamePeople;
            SeededRandom visit = new SeededRandom(visitSeed);

            List<NpcDefinition> ordered = new List<NpcDefinition>(templates);
            ordered.Sort(delegate (NpcDefinition a, NpcDefinition b) { return string.CompareOrdinal(a.Id, b.Id); });

            int group = 0;
            foreach (NpcDefinition t in ordered)
            {
                if (t.Kind != NpcKind.Generic || t.Spawn == null)
                    continue;
                int chance, min, max;
                Condition when;
                string mismatch;
                if (!Rules(t, place, out chance, out min, out max, out when, out mismatch))
                    continue;
                if (mismatch != null)
                {
                    Explain(explain, t.Id + ": " + mismatch);
                    continue;
                }
                if (facts != null && when != null)
                {
                    string failing = when.FirstFailing(facts);
                    if (failing != null)
                    {
                        Explain(explain, t.Id + ": when: " + failing + " does not hold");
                        continue;
                    }
                }

                SeededRandom roll = new SeededRandom(same ? StableHash.Of(t.Id + "@" + place.PlaceKey) : visit.NextUInt());
                int rolled = roll.Next(100);
                string head = t.Id + ": chance " + chance + ", rolled " + rolled + ": ";
                if (rolled >= chance)
                {
                    Explain(explain, head + "not here");
                    continue;
                }
                int count = roll.Range(min, max);
                int made = 0;
                for (int n = 0; n < count && result.Count < cap; n++)
                {
                    NpcInstance i = new NpcInstance();
                    i.Key = t.Id + "@" + place.PlaceKey + "#" + n.ToString(CultureInfo.InvariantCulture);
                    i.Definition = t;
                    i.Persistent = same;
                    i.Seed = same ? StableHash.Of(i.Key) : visit.NextUInt();
                    i.Group = group;
                    PopulationPlanner.RollPerson(t, i, place.DefaultRace, names);
                    result.Add(i);
                    made++;
                }
                Explain(explain, head + (made == count ? count + " here" : made + " of " + count + " here (limit " + cap + " reached)"));
                if (made > 0)
                    group++;
            }
            return result;
        }

        /// <summary>False if the template has no block for this kind of place; mismatch set when the filter fails.</summary>
        static bool Rules(NpcDefinition t, PlaceInfo place, out int chance, out int min, out int max, out Condition when, out string mismatch)
        {
            chance = 0;
            min = 1;
            max = 1;
            when = null;
            mismatch = null;
            if (place.IsDungeon)
            {
                DungeonSpawn d = t.Spawn.Dungeons;
                if (d == null)
                    return false;
                chance = d.Chance;
                min = d.CountMin;
                max = d.CountMax;
                when = d.When;
                if (!d.Matches(place.DungeonType))
                    mismatch = "dungeon type " + place.DungeonType + " is not one of " + string.Join(", ", d.DungeonTypes.ToArray());
                return true;
            }
            InteriorSpawn s = t.Spawn.Interiors;
            if (s == null)
                return false;
            chance = s.Chance;
            min = s.CountMin;
            max = s.CountMax;
            when = s.When;
            if (!s.Matches(place.Building, place.Guild))
                mismatch = "building " + place.Building + (place.Guild != null ? " (" + place.Guild + ")" : "") + " is not one of " +
                           string.Join(", ", s.Buildings.ToArray());
            return true;
        }

        static void Explain(List<string> explain, string line)
        {
            if (explain != null)
                explain.Add(line);
        }
    }
}

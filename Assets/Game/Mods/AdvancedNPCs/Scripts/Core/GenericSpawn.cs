using System;
using System.Collections.Generic;

namespace AdvancedNPCs.Core
{
    /// <summary>The town a generic template is planned for.</summary>
    public class TownInfo
    {
        public readonly int MapId;
        public readonly string Region;
        public readonly string Place;
        /// <summary>A LocationTypeNames name, e.g. "TownCity".</summary>
        public readonly string LocationType;
        /// <summary>The region's people ("Breton", "Redguard", "Nord").</summary>
        public readonly string DefaultRace;

        public TownInfo(int mapId, string region, string place, string locationType, string defaultRace)
        {
            MapId = mapId;
            Region = region;
            Place = place;
            LocationType = locationType;
            DefaultRace = defaultRace;
        }
    }

    /// <summary>One entry of spawn.places: a town and, optionally, fixed spots there (from anpc_pos).</summary>
    public class SpawnPlace
    {
        public string Region;
        public string Place;
        public readonly List<float[]> Positions = new List<float[]>();
    }

    /// <summary>Where, and how many, instances of a generic template appear (spec §7.3).</summary>
    public class GenericSpawn
    {
        public readonly List<string> LocationTypes = new List<string>(LocationTypeNames.Default);
        public readonly List<SpawnPlace> Places = new List<SpawnPlace>();
        public int CountMin = 1;
        public int CountMax = 3;

        /// <summary>
        /// True if the template spawns in this town. When spawn.places is given only those towns match (and
        /// place is the matching entry); otherwise the town's location type must be listed (place is null).
        /// </summary>
        public bool Matches(TownInfo town, out SpawnPlace place)
        {
            place = null;
            if (Places.Count > 0)
            {
                foreach (SpawnPlace p in Places)
                {
                    if (DefinitionCatalog.SamePlaceName(p.Region, town.Region) && DefinitionCatalog.SamePlaceName(p.Place, town.Place))
                    {
                        place = p;
                        return true;
                    }
                }
                return false;
            }
            foreach (string type in LocationTypes)
            {
                if (string.Equals(type, town.LocationType, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }

    /// <summary>DFRegion.LocationTypes names (copied: Core has no DFU references).</summary>
    public static class LocationTypeNames
    {
        public static readonly string[] All =
        {
            "TownCity", "TownHamlet", "TownVillage", "HomeFarms", "DungeonLabyrinth", "ReligionTemple", "Tavern",
            "DungeonKeep", "HomeWealthy", "ReligionCult", "DungeonRuin", "HomePoor", "Graveyard", "Coven", "HomeYourShips",
        };

        public static readonly string[] Default = { "TownCity", "TownHamlet", "TownVillage" };

        public static string Canonical(string raw)
        {
            if (raw == null)
                return null;
            foreach (string name in All)
            {
                if (string.Equals(name, raw.Trim(), StringComparison.OrdinalIgnoreCase))
                    return name;
            }
            return null;
        }
    }
}

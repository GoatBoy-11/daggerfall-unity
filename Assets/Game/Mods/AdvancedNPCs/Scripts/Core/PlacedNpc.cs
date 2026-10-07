using System;
using System.Collections.Generic;
using System.Globalization;

namespace AdvancedNPCs.Core
{
    /// <summary>
    /// One extra person made from a generic template with anpc_spawn, kept in the save game. Field names are part of
    /// the save format. Position is relative to the town's origin.
    /// </summary>
    [Serializable]
    public class PlacedNpc
    {
        public string template;
        public int mapId;
        public string region;
        public string place;
        public float x;
        public float y;
        public float z;
        /// <summary>1, 2, ... per template and town.</summary>
        public int number;

        /// <summary>template@mapId+number: never the same as a planned person's template@mapId#n.</summary>
        public string Key()
        {
            return template + "@" + mapId.ToString(CultureInfo.InvariantCulture) + "+" + number.ToString(CultureInfo.InvariantCulture);
        }

        public PlacedNpc Clone()
        {
            return (PlacedNpc)MemberwiseClone();
        }
    }

    /// <summary>Every person placed with anpc_spawn in this game.</summary>
    public class PlacedNpcList
    {
        readonly List<PlacedNpc> placed = new List<PlacedNpc>();

        public PlacedNpc Add(string template, int mapId, string region, string place, float x, float y, float z)
        {
            int number = 1;
            foreach (PlacedNpc p in placed)
            {
                if (p.template == template && p.mapId == mapId && p.number >= number)
                    number = p.number + 1;
            }
            PlacedNpc added = new PlacedNpc();
            added.template = template;
            added.mapId = mapId;
            added.region = region;
            added.place = place;
            added.x = x;
            added.y = y;
            added.z = z;
            added.number = number;
            placed.Add(added);
            return added;
        }

        public List<PlacedNpc> ForTown(int mapId)
        {
            return placed.FindAll(delegate (PlacedNpc p) { return p.mapId == mapId; });
        }

        public PlacedNpc Find(string key)
        {
            return placed.Find(delegate (PlacedNpc p) { return p.Key() == key; });
        }

        public bool Remove(string key)
        {
            return placed.RemoveAll(delegate (PlacedNpc p) { return p.Key() == key; }) > 0;
        }

        public List<PlacedNpc> Snapshot()
        {
            return placed.ConvertAll(delegate (PlacedNpc p) { return p.Clone(); });
        }

        public void Restore(List<PlacedNpc> saved)
        {
            placed.Clear();
            if (saved == null)
                return;
            foreach (PlacedNpc p in saved)
            {
                if (p != null && !string.IsNullOrEmpty(p.template))
                    placed.Add(p.Clone());
            }
        }

        public void Clear()
        {
            placed.Clear();
        }
    }
}

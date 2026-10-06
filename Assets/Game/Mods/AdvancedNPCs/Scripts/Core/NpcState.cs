using System;
using System.Collections.Generic;

namespace AdvancedNPCs.Core
{
    /// <summary>What we remember about one NPC between visits and saves. Field names are part of the save format.</summary>
    [Serializable]
    public class NpcState
    {
        public bool dead;
        public bool hostile;
        public ulong hostileUntil;
        public int health = -1;

        public NpcState Clone()
        {
            NpcState c = new NpcState();
            c.dead = dead;
            c.hostile = hostile;
            c.hostileUntil = hostileUntil;
            c.health = health;
            return c;
        }
    }

    /// <summary>All NPC states keyed by definition id. Entries for ids without a definition are kept.</summary>
    public class NpcStateTable
    {
        readonly Dictionary<string, NpcState> states = new Dictionary<string, NpcState>(StringComparer.Ordinal);

        public NpcState GetOrCreate(string id)
        {
            NpcState s;
            if (!states.TryGetValue(id, out s))
            {
                s = new NpcState();
                states.Add(id, s);
            }
            return s;
        }

        public bool Has(string id)
        {
            return states.ContainsKey(id);
        }

        public Dictionary<string, NpcState> Snapshot()
        {
            Dictionary<string, NpcState> copy = new Dictionary<string, NpcState>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, NpcState> kv in states)
                copy.Add(kv.Key, kv.Value.Clone());
            return copy;
        }

        public void Restore(Dictionary<string, NpcState> saved)
        {
            states.Clear();
            if (saved == null)
                return;
            foreach (KeyValuePair<string, NpcState> kv in saved)
            {
                if (kv.Key != null && kv.Value != null)
                    states[kv.Key] = kv.Value.Clone();
            }
        }

        public void Clear()
        {
            states.Clear();
        }
    }
}

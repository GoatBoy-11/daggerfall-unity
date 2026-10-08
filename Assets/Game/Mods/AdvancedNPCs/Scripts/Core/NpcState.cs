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

        /// <summary>Current health as a fraction of max (DFU re-rolls max health on every spawn).</summary>
        public float healthFraction = 1f;

        /// <summary>Portrait file shown at the first talk; kept for the rest of the game (null: not locked yet).</summary>
        public string portrait;

        /// <summary>Switched to an enemy with anpc_hostile / SetHostile (overrides the definition).</summary>
        public bool enemyOn;
        /// <summary>Switched calm with anpc_hostile / SetHostile (overrides a hostile definition).</summary>
        public bool enemyOff;

        /// <summary>Dialogue topic ids asked of this person (null in older saves).</summary>
        public List<string> asked;

        public bool HasAsked(string topicId)
        {
            return asked != null && asked.Contains(topicId);
        }

        public void MarkAsked(string topicId)
        {
            if (asked == null)
                asked = new List<string>();
            if (!asked.Contains(topicId))
                asked.Add(topicId);
        }

        public NpcState Clone()
        {
            NpcState c = new NpcState();
            c.dead = dead;
            c.hostile = hostile;
            c.hostileUntil = hostileUntil;
            c.healthFraction = healthFraction;
            c.portrait = portrait;
            c.enemyOn = enemyOn;
            c.enemyOff = enemyOff;
            c.asked = asked != null ? new List<string>(asked) : null;
            return c;
        }

        /// <summary>Alive, calm, full health, no locked portrait, nothing asked: nothing worth saving (spec §8, v2.1 §6).</summary>
        public bool IsDefault()
        {
            return !dead && !hostile && healthFraction >= 0.999f && string.IsNullOrEmpty(portrait) && !enemyOn && !enemyOff &&
                   (asked == null || asked.Count == 0);
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

        public void Remove(string id)
        {
            states.Remove(id);
        }

        public bool Has(string id)
        {
            return states.ContainsKey(id);
        }

        /// <summary>Copy of every state that differs from fresh; fresh ones are not saved.</summary>
        public Dictionary<string, NpcState> Snapshot()
        {
            Dictionary<string, NpcState> copy = new Dictionary<string, NpcState>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, NpcState> kv in states)
            {
                if (!kv.Value.IsDefault())
                    copy.Add(kv.Key, kv.Value.Clone());
            }
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

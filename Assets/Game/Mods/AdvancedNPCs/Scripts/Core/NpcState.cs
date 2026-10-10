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

        // Missing in old saves: initialize at full magicka on the first visit.
        public bool magicInitialized;
        public int magicka;
        public long magicVisit;
        public ulong magicUpdatedAt;
        public ulong magicRestAfter;
        public double magicRemainder;
        public float magicCooldown;
        // Copied from the definition on each area entry, so IsDefault can tell a full, ready caster (same as fresh).
        public int magicMax;
        public bool magicUnlimited;
        public bool magicRefillsOnEntry;

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
            c.magicInitialized = magicInitialized;
            c.magicka = magicka;
            c.magicVisit = magicVisit;
            c.magicUpdatedAt = magicUpdatedAt;
            c.magicRestAfter = magicRestAfter;
            c.magicRemainder = magicRemainder;
            c.magicCooldown = magicCooldown;
            c.magicMax = magicMax;
            c.magicUnlimited = magicUnlimited;
            c.magicRefillsOnEntry = magicRefillsOnEntry;
            return c;
        }

        /// <summary>Full magicka (or unlimited) and no cooldown: a fresh start gives the same caster, so nothing to save.</summary>
        public bool MagicSettled()
        {
            return !magicInitialized || (magicCooldown <= 0 && (magicUnlimited || magicka >= magicMax));
        }

        /// <summary>Back to never-seen: the next area entry starts at full magicka with no cooldown.</summary>
        public void ForgetMagic()
        {
            magicInitialized = false;
            magicka = 0;
            magicVisit = 0;
            magicUpdatedAt = 0;
            magicRestAfter = 0;
            magicRemainder = 0;
            magicCooldown = 0;
            magicMax = 0;
            magicUnlimited = false;
            magicRefillsOnEntry = false;
        }

        /// <summary>Alive, calm, full health, no locked portrait, nothing asked: nothing worth saving (spec §8, v2.1 §6).</summary>
        public bool IsDefault()
        {
            return MagicSettled() && !dead && !hostile && healthFraction >= 0.999f && string.IsNullOrEmpty(portrait) && !enemyOn && !enemyOff &&
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

        /// <summary>
        /// The player moved to another area: casters that refill on entry will start full there anyway,
        /// so their magic from earlier visits is dropped (not saved).
        /// </summary>
        public void ForgetMagicOutside(long visit)
        {
            foreach (NpcState s in states.Values)
            {
                if (s.magicInitialized && s.magicRefillsOnEntry && s.magicVisit != visit)
                    s.ForgetMagic();
            }
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

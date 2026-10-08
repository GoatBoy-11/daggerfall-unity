using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Utility;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>
    /// Wilderness encounters (outside-towns spec §2.4, §5): while the player is outside every location with only the
    /// HUD open, every 10 in-game minutes each template with a "wilderness" block rolls its chance; a success puts one
    /// person out of view 40-80 m away on dry, not too steep ground. They are never saved and go when far behind,
    /// when the player enters a location or a building, or when the world is rebuilt (fast travel, load).
    /// </summary>
    public class WildernessSpawner : MonoBehaviour
    {
        const int Tries = 8;
        const float MaxSlope = 35f;
        const float DespawnCheckSeconds = 1f;

        AdvancedNpcsMod owner;
        NpcSpawner spawner;
        readonly List<NpcBrain> alive = new List<NpcBrain>();
        readonly Dictionary<string, string> lastRoll = new Dictionary<string, string>();
        readonly System.Random rng = new System.Random();
        ulong lastRollMinute;
        bool wasOutside;
        int counter;
        float nextDespawnCheck;

        public void Init(AdvancedNpcsMod mod, NpcSpawner npcSpawner)
        {
            owner = mod;
            spawner = npcSpawner;
            StreamingWorld.OnClearStreamingWorld += DespawnAll;
        }

        void OnDestroy()
        {
            StreamingWorld.OnClearStreamingWorld -= DespawnAll;
        }

        /// <summary>Wilderness people alive now.</summary>
        public List<NpcBrain> Alive
        {
            get
            {
                alive.RemoveAll(delegate (NpcBrain b) { return b == null; });
                return new List<NpcBrain>(alive);
            }
        }

        static ulong NowMinute
        {
            get { return DaggerfallUnity.Instance.WorldTime.Now.ToClassicDaggerfallTime(); }
        }

        void Update()
        {
            if (owner == null || GameManager.Instance == null || GameManager.Instance.PlayerGPS == null)
                return;
            GameManager gm = GameManager.Instance;
            bool outside = !gm.PlayerEnterExit.IsPlayerInside && !gm.PlayerGPS.IsPlayerInLocationRect;
            if (!outside)
            {
                if (wasOutside)
                    DespawnAll();
                wasOutside = false;
                return;
            }
            if (!wasOutside)
            {
                // Just left a location: the first roll comes 10 minutes later.
                wasOutside = true;
                lastRollMinute = NowMinute;
            }

            if (Time.unscaledTime >= nextDespawnCheck)
            {
                nextDespawnCheck = Time.unscaledTime + DespawnCheckSeconds;
                DespawnFar();
            }
            if (!owner.Config.Wilderness || owner.Config.MaxWildernessAround <= 0)
                return;
            if (!EncounterRules.MayRoll(gm.PlayerGPS.IsPlayerInLocationRect, gm.IsPlayerOnHUD))
                return;
            ulong now = NowMinute;
            lastRollMinute = EncounterRules.RollClock(lastRollMinute, now);
            if (EncounterRules.DueRolls(lastRollMinute, now) == 0)
                return;
            lastRollMinute = now;
            Roll();
        }

        /// <summary>One roll for every template with a wilderness block (also the self-test).</summary>
        public List<NpcBrain> Roll()
        {
            List<NpcBrain> made = new List<NpcBrain>();
            List<NpcDefinition> templates = new List<NpcDefinition>(owner.Catalog.Generics);
            templates.Sort(delegate (NpcDefinition a, NpcDefinition b) { return string.CompareOrdinal(a.Id, b.Id); });
            GameFacts facts = new GameFacts(null, owner.Flags, "", null);
            string time = DaggerfallUnity.Instance.WorldTime.Now.MinTimeString();
            foreach (NpcDefinition t in templates)
            {
                WildernessSpawn w = t.Spawn != null ? t.Spawn.Wilderness : null;
                if (w == null)
                    continue;
                int ofTemplate = Alive.FindAll(delegate (NpcBrain b) { return b.Instance != null && b.Instance.Definition == t; }).Count;
                string failing = w.When != null ? w.When.FirstFailing(facts) : null;
                if (failing != null)
                {
                    lastRoll[t.Id] = time + ": when: " + failing + " does not hold";
                    continue;
                }
                if (!EncounterRules.CanSpawn(ofTemplate, w.Max, Alive.Count, owner.Config.MaxWildernessAround))
                {
                    lastRoll[t.Id] = time + ": " + ofTemplate + " of max " + w.Max + " alive (" + Alive.Count + " of " +
                                     owner.Config.MaxWildernessAround + " in all), no roll";
                    continue;
                }
                int rolled = rng.Next(100);
                if (rolled >= w.Chance)
                {
                    lastRoll[t.Id] = time + ": chance " + w.Chance + ", rolled " + rolled + ": nobody";
                    continue;
                }
                NpcBrain brain = SpawnNear(t);
                lastRoll[t.Id] = time + ": chance " + w.Chance + ", rolled " + rolled + ": " + (brain != null ? brain.Id + " appeared" : "no good spot this time");
                if (brain != null)
                    made.Add(brain);
            }
            return made;
        }

        /// <summary>One person of the template out of view 40-80 m away, or null if 8 tries find no dry, gentle ground.</summary>
        public NpcBrain SpawnNear(NpcDefinition t)
        {
            Transform player = GameManager.Instance.PlayerObject.transform;
            Camera cam = GameManager.Instance.MainCamera;
            Vector3 view = cam != null ? cam.transform.forward : player.forward;
            Vector3 feet;
            if (!FindGround(player.position, view, out feet))
                return null;
            Transform parent = GameObjectHelper.GetBestParent();
            NpcInstance instance = OutOfTownPlanner.ForEncounter(t, ++counter, (uint)rng.Next(), NpcSpawner.DefaultRace(GameManager.Instance.PlayerGPS.CurrentClimateIndex), owner.Names);
            Vector3 local = parent.InverseTransformPoint(feet);
            instance.X = local.x;
            instance.Y = local.y;
            instance.Z = local.z;
            instance.HasFixedPosition = true;
            NpcBrain brain = spawner.SpawnChecked(instance, new NpcState(), parent);
            if (brain != null)
            {
                brain.transform.rotation = Quaternion.LookRotation(Flat(player.position - feet));
                alive.Add(brain);
            }
            return brain;
        }

        bool FindGround(Vector3 player, Vector3 view, out Vector3 feet)
        {
            feet = player;
            for (int i = 0; i < Tries; i++)
            {
                float[] p = EncounterRules.RingPoint(player.x, player.z, view.x, view.z, rng.NextDouble(), rng.NextDouble());
                RaycastHit hit;
                Vector3 top = new Vector3(p[0], player.y + 200f, p[1]);
                if (!Physics.Raycast(top, Vector3.down, out hit, 400f, SpotFinder.Mask, QueryTriggerInteraction.Ignore))
                    continue;
                if (Vector3.Angle(hit.normal, Vector3.up) > MaxSlope || !DryLand(hit))
                    continue;
                feet = hit.point;
                return true;
            }
            return false;
        }

        /// <summary>Dirt, grass or stone terrain tiles, like DFU's own nature flats (TerrainNature); never water.</summary>
        static bool DryLand(RaycastHit hit)
        {
            DaggerfallTerrain dfTerrain = hit.collider.GetComponent<DaggerfallTerrain>();
            Terrain terrain = hit.collider.GetComponent<Terrain>();
            if (dfTerrain == null || terrain == null || dfTerrain.MapData.tilemapSamples == null)
                return false;
            byte[,] tiles = dfTerrain.MapData.tilemapSamples;
            int dim = tiles.GetLength(0);
            Vector3 local = hit.collider.transform.InverseTransformPoint(hit.point);
            Vector3 size = terrain.terrainData.size;
            int x = Mathf.Clamp((int)(local.x / size.x * dim), 0, dim - 1);
            int y = Mathf.Clamp((int)(local.z / size.z * dim), 0, dim - 1);
            int tile = tiles[x, y] & 0x3F;
            return tile >= 1 && tile <= 3;
        }

        void DespawnFar()
        {
            Transform player = GameManager.Instance.PlayerObject.transform;
            Camera cam = GameManager.Instance.MainCamera;
            Vector3 view = cam != null ? cam.transform.forward : player.forward;
            foreach (NpcBrain b in Alive)
            {
                Vector3 p = b.transform.position;
                bool inView = !EncounterRules.OutOfView(player.position.x, player.position.z, view.x, view.z, p.x, p.z);
                if (EncounterRules.ShouldDespawn(Vector3.Distance(player.position, p), inView))
                    Despawn(b);
            }
        }

        void Despawn(NpcBrain b)
        {
            alive.Remove(b);
            if (b != null)
                NpcBrain.Discard(b);
        }

        public void DespawnAll()
        {
            foreach (NpcBrain b in Alive)
                Despawn(b);
            alive.Clear();
        }

        /// <summary>Lines for anpc_here: each wilderness template, its rule and its last roll.</summary>
        public List<string> Explain()
        {
            List<string> lines = new List<string>();
            foreach (NpcDefinition t in owner.Catalog.Generics)
            {
                WildernessSpawn w = t.Spawn != null ? t.Spawn.Wilderness : null;
                if (w == null)
                    continue;
                string last;
                lastRoll.TryGetValue(t.Id, out last);
                lines.Add(t.Id + ": chance " + w.Chance + " every " + EncounterRules.RollMinutes + " min, max " + w.Max + "; last roll " + (last ?? "none yet"));
            }
            return lines;
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0;
            return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
        }
    }
}

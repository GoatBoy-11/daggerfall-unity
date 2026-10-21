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
    /// person, or a group (spec animal-ai §1.2), out of view 40-80 m away on ground of the block's terrain, not too steep.
    /// Animals count toward their own cap (Wildlife settings). They are never saved and go when far behind, when the
    /// player enters a location or a building, or when the world is rebuilt (fast travel, load).
    /// </summary>
    public class WildernessSpawner : MonoBehaviour
    {
        const int Tries = 8;
        const int ShoreTries = 16;
        const int MemberTries = 4;
        const float MaxSlope = 35f;
        const float DespawnCheckSeconds = 1f;
        /// <summary>Wilderness groups get ids far above the per-place group indexes (allies need the same parent anyway).</summary>
        const int FirstGroup = 100000;

        AdvancedNpcsMod owner;
        NpcSpawner spawner;
        readonly List<NpcBrain> alive = new List<NpcBrain>();
        readonly Dictionary<string, string> lastRoll = new Dictionary<string, string>();
        readonly System.Random rng = new System.Random();
        ulong lastRollMinute;
        bool wasOutside;
        int counter;
        int groups = FirstGroup;
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

        /// <summary>Wilderness people and animals alive now.</summary>
        public List<NpcBrain> Alive
        {
            get
            {
                alive.RemoveAll(delegate (NpcBrain b) { return b == null; });
                return new List<NpcBrain>(alive);
            }
        }

        /// <summary>Alive wilderness animals (animals: true) or people (false).</summary>
        public int CountAlive(bool animals)
        {
            int n = 0;
            foreach (NpcBrain b in Alive)
                if (b.Instance != null && (b.Instance.Definition.Animal != null) == animals)
                    n++;
            return n;
        }

        static ulong NowMinute
        {
            get { return DaggerfallUnity.Instance.WorldTime.Now.ToClassicDaggerfallTime(); }
        }

        bool PeopleOn { get { return owner.Config.Wilderness && owner.Config.MaxWildernessAround > 0; } }
        bool AnimalsOn { get { return owner.Config.Wildlife && owner.Config.MaxWildlifeAround > 0; } }

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
            if (!PeopleOn && !AnimalsOn)
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
                bool animal = t.Animal != null;
                if (animal ? !AnimalsOn : !PeopleOn)
                {
                    lastRoll[t.Id] = time + ": " + (animal ? "Wildlife" : "Wilderness") + " setting off, no roll";
                    continue;
                }
                int cap = animal ? owner.Config.MaxWildlifeAround : owner.Config.MaxWildernessAround;
                int aliveOfKind = CountAlive(animal);
                int ofTemplate = Alive.FindAll(delegate (NpcBrain b) { return b.Instance != null && b.Instance.Definition == t; }).Count;
                string failing = w.When != null ? w.When.FirstFailing(facts) : null;
                if (failing != null)
                {
                    lastRoll[t.Id] = time + ": when: " + failing + " does not hold";
                    continue;
                }
                if (!EncounterRules.CanSpawn(ofTemplate, w.Max, aliveOfKind, cap))
                {
                    lastRoll[t.Id] = time + ": " + ofTemplate + " of max " + w.Max + " alive (" + aliveOfKind + " of " + cap +
                                     (animal ? " animals" : " people") + " in all), no roll";
                    continue;
                }
                int chance = animal ? Mathf.Min(100, Mathf.RoundToInt(w.Chance * owner.Config.WildlifeChanceFactor)) : w.Chance;
                int rolled = rng.Next(100);
                if (rolled >= chance)
                {
                    lastRoll[t.Id] = time + ": chance " + chance + ", rolled " + rolled + ": nobody";
                    continue;
                }
                int size = TerrainRules.GroupSize(w.GroupMin, w.GroupMax, rng.NextDouble(), w.Max - ofTemplate, cap - aliveOfKind);
                if (size == 0)
                {
                    lastRoll[t.Id] = time + ": rolled " + rolled + ", but room for " + Mathf.Min(w.Max - ofTemplate, cap - aliveOfKind) +
                                     " and the group needs " + w.GroupMin;
                    continue;
                }
                string why;
                List<NpcBrain> group = SpawnGroup(t, size, out why);
                lastRoll[t.Id] = time + ": chance " + chance + ", rolled " + rolled + ": " +
                                 (group.Count > 0 ? group.Count + " appeared (" + group[0].Id + (group.Count > 1 ? " leads" : "") + ")" : why);
                made.AddRange(group);
            }
            return made;
        }

        /// <summary>One of the template out of view 40-80 m away, or null if no spot of its terrain was found.</summary>
        public NpcBrain SpawnNear(NpcDefinition t)
        {
            string why;
            List<NpcBrain> one = SpawnGroup(t, 1, out why);
            return one.Count > 0 ? one[0] : null;
        }

        /// <summary>
        /// count of the template around one spot out of view 40-80 m away: the first on ground of the block's terrain,
        /// the others on dry ground within 6 m of it (a member without a spot is left out). All share one group; the
        /// first leads (animals). why says what went wrong when nobody appeared.
        /// </summary>
        public List<NpcBrain> SpawnGroup(NpcDefinition t, int count, out string why)
        {
            List<NpcBrain> made = new List<NpcBrain>();
            why = null;
            Transform player = GameManager.Instance.PlayerObject.transform;
            Camera cam = GameManager.Instance.MainCamera;
            Vector3 view = cam != null ? cam.transform.forward : player.forward;
            string terrain = t.Spawn != null && t.Spawn.Wilderness != null ? t.Spawn.Wilderness.Terrain : TerrainRules.Dry;
            Vector3 anchor;
            if (!FindGround(player.position, view, terrain, out anchor))
            {
                why = terrain == TerrainRules.NearWater ? "no shore found this time" : "no " + terrain + " ground found this time";
                return made;
            }
            int group = count > 1 || t.Animal != null ? ++groups : -1;
            Transform parent = GameObjectHelper.GetBestParent();
            NpcBrain leader = Place(t, anchor, group, parent, player.position);
            if (leader == null)
            {
                why = "spawn failed (see the log)";
                return made;
            }
            made.Add(leader);
            for (int i = 1; i < count; i++)
            {
                Vector3 feet;
                if (!FindNear(anchor, out feet))
                    continue;
                NpcBrain member = Place(t, feet, group, parent, player.position);
                if (member == null)
                    continue;
                made.Add(member);
                NpcAnimal a = member.GetComponent<NpcAnimal>();
                if (a != null)
                    a.Leader = leader;
            }
            return made;
        }

        NpcBrain Place(NpcDefinition t, Vector3 feet, int group, Transform parent, Vector3 playerAt)
        {
            NpcInstance instance = OutOfTownPlanner.ForEncounter(t, ++counter, (uint)rng.Next(), NpcSpawner.DefaultRace(GameManager.Instance.PlayerGPS.CurrentClimateIndex), owner.Names);
            Vector3 local = parent.InverseTransformPoint(feet);
            instance.X = local.x;
            instance.Y = local.y;
            instance.Z = local.z;
            instance.HasFixedPosition = true;
            instance.Group = group;
            NpcBrain brain = spawner.SpawnChecked(instance, new NpcState(), parent);
            if (brain != null)
            {
                // People face the player's way; animals face any way (a herd all staring at you looks wrong).
                brain.transform.rotation = t.Animal != null ? Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f)
                    : Quaternion.LookRotation(Flat(playerAt - feet));
                alive.Add(brain);
            }
            return brain;
        }

        bool FindGround(Vector3 player, Vector3 view, string terrain, out Vector3 feet)
        {
            feet = player;
            int tries = terrain == TerrainRules.NearWater ? ShoreTries : Tries;
            for (int i = 0; i < tries; i++)
            {
                float[] p = EncounterRules.RingPoint(player.x, player.z, view.x, view.z, rng.NextDouble(), rng.NextDouble());
                RaycastHit hit;
                if (!Ground(new Vector3(p[0], player.y, p[1]), out hit) || !Gentle(hit) || !Accepts(hit, terrain))
                    continue;
                feet = hit.point;
                return true;
            }
            return false;
        }

        /// <summary>A dry, gentle spot 2-6 m from the group's first member.</summary>
        bool FindNear(Vector3 anchor, out Vector3 feet)
        {
            feet = anchor;
            for (int i = 0; i < MemberTries; i++)
            {
                double angle = rng.NextDouble() * System.Math.PI * 2.0;
                float r = 2f + (float)rng.NextDouble() * (AnimalRules.GroupSpread - 2f);
                Vector3 at = anchor + new Vector3((float)System.Math.Cos(angle) * r, 0f, (float)System.Math.Sin(angle) * r);
                RaycastHit hit;
                if (!Ground(at, out hit) || !Gentle(hit) || !Accepts(hit, TerrainRules.Dry))
                    continue;
                feet = hit.point;
                return true;
            }
            return false;
        }

        static bool Ground(Vector3 at, out RaycastHit hit)
        {
            Vector3 top = new Vector3(at.x, at.y + 200f, at.z);
            return Physics.Raycast(top, Vector3.down, out hit, 400f, SpotFinder.Mask, QueryTriggerInteraction.Ignore);
        }

        static bool Gentle(RaycastHit hit)
        {
            return Vector3.Angle(hit.normal, Vector3.up) <= MaxSlope;
        }

        /// <summary>The terrain tile under a hit passes the terrain rule (never water; like DFU's own nature flats).</summary>
        static bool Accepts(RaycastHit hit, string terrain)
        {
            byte[,] tiles;
            int x, y;
            if (!TileAt(hit, out tiles, out x, out y))
                return false;
            int tile = tiles[x, y] & 0x3F;
            bool waterNear = terrain == TerrainRules.NearWater && WaterNear(tiles, x, y);
            return TerrainRules.Accepts(terrain, tile, waterNear);
        }

        static bool TileAt(RaycastHit hit, out byte[,] tiles, out int x, out int y)
        {
            tiles = null;
            x = y = 0;
            DaggerfallTerrain dfTerrain = hit.collider.GetComponent<DaggerfallTerrain>();
            Terrain terrain = hit.collider.GetComponent<Terrain>();
            if (dfTerrain == null || terrain == null || dfTerrain.MapData.tilemapSamples == null)
                return false;
            tiles = dfTerrain.MapData.tilemapSamples;
            int dim = tiles.GetLength(0);
            Vector3 local = hit.collider.transform.InverseTransformPoint(hit.point);
            Vector3 size = terrain.terrainData.size;
            x = Mathf.Clamp((int)(local.x / size.x * dim), 0, dim - 1);
            y = Mathf.Clamp((int)(local.z / size.z * dim), 0, dim - 1);
            return true;
        }

        static bool WaterNear(byte[,] tiles, int x, int y)
        {
            int dim = tiles.GetLength(0);
            int n = TerrainRules.ShoreSamples;
            for (int i = Mathf.Max(0, x - n); i <= Mathf.Min(dim - 1, x + n); i++)
                for (int j = Mathf.Max(0, y - n); j <= Mathf.Min(dim - 1, y + n); j++)
                    if ((tiles[i, j] & 0x3F) == 0)
                        return true;
            return false;
        }

        /// <summary>"grass", "water near", ... for the ground right under a world point (anpc_here).</summary>
        public static string GroundAt(Vector3 at)
        {
            RaycastHit hit;
            byte[,] tiles;
            int x, y;
            if (!Ground(at, out hit) || !TileAt(hit, out tiles, out x, out y))
                return "not terrain";
            int tile = tiles[x, y] & 0x3F;
            string name = tile == 0 ? "water" : tile == 1 ? "dirt" : tile == 2 ? "grass" : tile == 3 ? "stone" : "blend tile " + tile;
            return name + (WaterNear(tiles, x, y) ? ", water within " + TerrainRules.ShoreSamples + " samples" : "");
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
            if (b != null && b.State.companion != null) { alive.Remove(b); return; }
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

        /// <summary>Lines for anpc_here: where the player stands, each wilderness template, its rule and its last roll.</summary>
        public List<string> Explain()
        {
            List<string> lines = new List<string>();
            GameManager gm = GameManager.Instance;
            if (gm != null && gm.PlayerObject != null && owner != null)
                lines.Add("climate " + ClimateNames.FromIndex(gm.PlayerGPS.CurrentClimateIndex) + ", ground " + GroundAt(gm.PlayerObject.transform.position) +
                          "; wildlife " + CountAlive(true) + " of " + owner.Config.MaxWildlifeAround + (owner.Config.Wildlife ? "" : " (off)") +
                          ", wilderness people " + CountAlive(false) + " of " + owner.Config.MaxWildernessAround + (owner.Config.Wilderness ? "" : " (off)"));
            foreach (NpcDefinition t in owner.Catalog.Generics)
            {
                WildernessSpawn w = t.Spawn != null ? t.Spawn.Wilderness : null;
                if (w == null)
                    continue;
                string last;
                lastRoll.TryGetValue(t.Id, out last);
                string group = w.GroupMax > 1 ? ", group " + w.GroupMin + "-" + w.GroupMax : "";
                string terrain = w.Terrain != TerrainRules.Dry ? ", on " + TerrainRules.Display[System.Array.IndexOf(TerrainRules.All, w.Terrain)] : "";
                lines.Add(t.Id + (t.Animal != null ? " (animal)" : "") + ": chance " + w.Chance + " every " + EncounterRules.RollMinutes + " min, max " + w.Max +
                          group + terrain + "; last roll " + (last ?? "none yet"));
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

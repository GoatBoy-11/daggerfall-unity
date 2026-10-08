using System.Collections.Generic;
using UnityEngine;
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>
    /// People in buildings and dungeons (outside-towns spec §5, §6): planned when the player enters (DFU raises the
    /// transition events after the place is laid out), placed on DFU's marker spots, children of the interior or
    /// dungeon object so they go when it goes. Also respawns them after a load inside.
    /// </summary>
    public class IndoorSpawner
    {
        public const float DungeonEntranceDistance = 20f;
        public const float DoorDistance = 2f;
        public const float IndoorWander = 3f;
        public const int MaxPerDungeon = 30;

        readonly AdvancedNpcsMod owner;
        readonly NpcSpawner spawner;
        readonly HashSet<string> warned = new HashSet<string>();

        /// <summary>The last dungeon or building planned, and what the planner said (anpc_here).</summary>
        public PlaceInfo LastPlace { get; private set; }
        public readonly List<string> LastExplain = new List<string>();
        public int LastSpawned { get; private set; }

        public IndoorSpawner(AdvancedNpcsMod owner, NpcSpawner spawner)
        {
            this.owner = owner;
            this.spawner = spawner;
        }

        public void Enable()
        {
            PlayerEnterExit.OnTransitionInterior += OnInterior;
            PlayerEnterExit.OnTransitionDungeonInterior += OnDungeon;
            owner.OnStateRestored += RespawnCurrent;
        }

        public void Disable()
        {
            PlayerEnterExit.OnTransitionInterior -= OnInterior;
            PlayerEnterExit.OnTransitionDungeonInterior -= OnDungeon;
            owner.OnStateRestored -= RespawnCurrent;
        }

        void OnInterior(PlayerEnterExit.TransitionEventArgs args)
        {
            SpawnInterior();
        }

        void OnDungeon(PlayerEnterExit.TransitionEventArgs args)
        {
            SpawnDungeon();
        }

        /// <summary>After a load or new game the town spawner clears every ANPC; bring back the current place's people.</summary>
        public void RespawnCurrent()
        {
            PlayerEnterExit pee = GameManager.Instance.PlayerEnterExit;
            if (pee.IsPlayerInsideBuilding)
                SpawnInterior();
            else if (pee.IsPlayerInsideDungeon)
                SpawnDungeon();
        }

        public List<NpcBrain> SpawnInterior()
        {
            DaggerfallInterior interior = GameManager.Instance.PlayerEnterExit.Interior;
            PlaceInfo place = interior != null ? InteriorPlace(interior) : null;
            if (place == null)
                return new List<NpcBrain>();
            Transform parent = interior.transform;
            List<float[]> candidates = SpotFinder.InteriorCandidates(interior);
            List<Vector3> doors = SpotFinder.InteriorDoors(interior);
            candidates.RemoveAll(delegate (float[] c)
            {
                Vector3 w = SpotFinder.World(parent, c);
                return doors.Exists(delegate (Vector3 d) { return Vector3.Distance(w, d) < DoorDistance; });
            });
            return SpawnPlace(place, parent, candidates, null, 0f, owner.Config.Interiors, owner.Config.MaxGenericPerTown, IndoorWander);
        }

        public List<NpcBrain> SpawnDungeon()
        {
            DaggerfallDungeon dungeon = GameManager.Instance.PlayerEnterExit.Dungeon;
            if (dungeon == null)
                return new List<NpcBrain>();
            DaggerfallDungeon.DungeonSummary s = dungeon.Summary;
            PlaceInfo place = PlaceInfo.Dungeon(s.ID, s.RegionName, s.LocationName, NpcSpawner.DefaultRace(s.LocationData.Climate.WorldClimate),
                s.DungeonType.ToString());
            Transform parent = dungeon.transform;
            // DFU puts the player on the start marker when entering; after a load the player may be anywhere.
            GameObject marker = dungeon.StartMarker != null ? dungeon.StartMarker : dungeon.EnterMarker;
            Vector3 entrance = marker != null ? marker.transform.position : GameManager.Instance.PlayerObject.transform.position;
            Vector3 local = parent.InverseTransformPoint(entrance);
            // Group members stand up to ~4.5 m from their spot, so spots keep 5 m more than the 20 m rule.
            return SpawnPlace(place, parent, SpotFinder.DungeonCandidates(dungeon), new float[] { local.x, local.y, local.z },
                DungeonEntranceDistance + 5f, owner.Config.Dungeons, MaxPerDungeon, float.MaxValue);
        }

        List<NpcBrain> SpawnPlace(PlaceInfo place, Transform parent, List<float[]> candidates, float[] avoid, float avoidRadius,
            bool enabled, int cap, float wanderCap)
        {
            List<NpcBrain> spawned = new List<NpcBrain>();
            LastPlace = place;
            LastExplain.Clear();
            LastSpawned = 0;

            // People placed with anpc_spawn are the player's own choice: always there, whatever the settings.
            foreach (PlacedNpc placed in owner.Placed.ForPlace(place.MapId, place.Context))
            {
                NpcBrain b = SpawnPlaced(placed, parent, place.DefaultRace, wanderCap);
                if (b != null)
                    spawned.Add(b);
            }
            if (!enabled)
            {
                LastExplain.Add("(" + (place.IsDungeon ? "Dungeons" : "Interiors") + " are turned off in the mod settings)");
                return spawned;
            }

            GenericMode mode = owner.Config.Mode;
            bool same = mode == GenericMode.SamePeople;
            uint visitSeed = (uint)Random.Range(int.MinValue, int.MaxValue);
            GameFacts facts = new GameFacts(null, owner.Flags, "", null);
            List<NpcInstance> plan = OutOfTownPlanner.Plan(owner.Catalog.Generics, place, mode, cap, visitSeed, owner.Names, facts, LastExplain);
            if (plan.Count == 0)
                return spawned;

            int groups = 0;
            Dictionary<int, int> sizes = new Dictionary<int, int>();
            foreach (NpcInstance i in plan)
            {
                groups = Mathf.Max(groups, i.Group + 1);
                sizes[i.Group] = sizes.ContainsKey(i.Group) ? sizes[i.Group] + 1 : 1;
            }
            List<int> picks = SpotPicker.Pick(candidates, avoid, avoidRadius, groups, same ? StableHash.Of(place.PlaceKey) : visitSeed ^ 0x9E3779B9u);
            Dictionary<int, int> member = new Dictionary<int, int>();
            int skipped = 0;
            foreach (NpcInstance instance in plan)
            {
                int g = instance.Group;
                int spot = picks[g];
                int m = member.ContainsKey(g) ? member[g] : 0;
                member[g] = m + 1;
                if (spot < 0)
                {
                    skipped++;
                    continue;
                }
                uint groupSeed = same ? StableHash.Of(place.PlaceKey + "/" + g) : visitSeed + (uint)g;
                float[] offset = SpotPicker.GroupOffsets(sizes[g], groupSeed)[m];
                Vector3 anchor = SpotFinder.World(parent, candidates[spot]);
                Vector3 feet;
                if (!SpotFinder.Near(anchor, anchor + new Vector3(offset[0], 0, offset[1]), out feet))
                {
                    skipped++;
                    continue;
                }
                Vector3 local = parent.InverseTransformPoint(feet);
                instance.X = local.x;
                instance.Y = local.y;
                instance.Z = local.z;
                instance.HasFixedPosition = true;
                NpcState state = instance.Persistent ? owner.States.GetOrCreate(instance.Key) : new NpcState();
                NpcBrain brain = spawner.SpawnChecked(instance, state, parent);
                if (brain != null)
                {
                    brain.WanderCap = wanderCap;
                    spawned.Add(brain);
                }
            }
            LastSpawned = spawned.Count;
            if (skipped > 0)
                LastExplain.Add(skipped + " planned person(s) had no free spot here and were left out");
            AdvancedNpcsMod.Log(place.Place + " (" + place.PlaceKey + "): " + spawned.Count + " ANPC(s) spawned (" + plan.Count + " planned, " +
                candidates.Count + " spots, " + mode + ").");
            return spawned;
        }

        public NpcBrain SpawnPlaced(PlacedNpc placed, Transform parent, string defaultRace, float wanderCap)
        {
            NpcDefinition template = owner.Catalog.Generics.Find(delegate (NpcDefinition t) { return t.Id == placed.template; });
            if (template == null)
            {
                if (warned.Add(placed.template))
                    AdvancedNpcsMod.Log(placed.Key() + ": no generic template \"" + placed.template + "\" any more; not spawned (kept in the save).");
                return null;
            }
            NpcInstance instance = PopulationPlanner.ForPlaced(template, placed, defaultRace, owner.Names);
            NpcBrain brain = spawner.SpawnChecked(instance, owner.States.GetOrCreate(instance.Key), parent);
            if (brain != null)
                brain.WanderCap = wanderCap;
            return brain;
        }

        /// <summary>The building the player is in: its type and, for guild halls, the guild (spike: read from the town's directory).</summary>
        public static PlaceInfo InteriorPlace(DaggerfallInterior interior)
        {
            GameManager gm = GameManager.Instance;
            DaggerfallLocation town = gm.StreamingWorld.CurrentPlayerLocationObject;
            if (town == null)
                return null;
            int key = interior.EntryDoor.buildingKey;
            string building = BuildingNames.FromBuildingType(gm.PlayerEnterExit.BuildingType.ToString());
            int faction = gm.PlayerEnterExit.BuildingDiscoveryData.factionID;
            BuildingDirectory dir = town.GetComponent<BuildingDirectory>();
            BuildingSummary summary;
            if (dir != null && dir.GetBuildingSummary(key, out summary))
            {
                building = BuildingNames.FromBuildingType(summary.BuildingType.ToString());
                faction = summary.FactionId;
            }
            string guild = building == BuildingNames.GuildHall ? GuildKey(gm.GuildManager.GetGuildGroup(faction)) : null;
            return PlaceInfo.Interior(town.Summary.MapID, town.Summary.RegionName, town.Summary.LocationName, NpcSpawner.DefaultRace(town),
                building, guild, key);
        }

        static string GuildKey(FactionFile.GuildGroups group)
        {
            switch (group)
            {
                case FactionFile.GuildGroups.FightersGuild: return "fightersguild";
                case FactionFile.GuildGroups.MagesGuild: return "magesguild";
                case FactionFile.GuildGroups.GeneralPopulace: return "thievesguild";
                case FactionFile.GuildGroups.DarkBrotherHood: return "darkbrotherhood";
                case FactionFile.GuildGroups.KnightlyOrder: return "knightlyorder";
                default: return null;
            }
        }
    }
}

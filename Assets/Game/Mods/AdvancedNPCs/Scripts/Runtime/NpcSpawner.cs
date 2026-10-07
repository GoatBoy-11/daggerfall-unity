using System;
using System.Collections.Generic;
using System.Collections;
using DaggerfallConnect.Utility;
using DaggerfallWorkshop.Game.Utility;
using UnityEngine;
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Utility;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>Spawns ANPCs when their town's GameObject is created.</summary>
    public class NpcSpawner
    {
        readonly AdvancedNpcsMod owner;

        // Towns currently built by StreamingWorld (several are loaded around the player at once).
        readonly List<DaggerfallLocation> knownLocations = new List<DaggerfallLocation>();
        readonly HashSet<int> townsWithoutCells = new HashSet<int>();

        public NpcSpawner(AdvancedNpcsMod owner)
        {
            this.owner = owner;
        }

        public void Enable()
        {
            StreamingWorld.OnCreateLocationGameObject += SpawnFor;
            StreamingWorld.OnUpdateLocationGameObject += OnLocationLaidOut;
            StreamingWorld.OnClearStreamingWorld += OnWorldCleared;
            owner.OnStateRestored += RespawnLoadedLocations;
        }

        public void Disable()
        {
            StreamingWorld.OnCreateLocationGameObject -= SpawnFor;
            StreamingWorld.OnUpdateLocationGameObject -= OnLocationLaidOut;
            StreamingWorld.OnClearStreamingWorld -= OnWorldCleared;
            owner.OnStateRestored -= RespawnLoadedLocations;
        }

        /// <summary>After a load or new game, rebuild ANPCs in every loaded town from the restored state.</summary>
        public void RespawnLoadedLocations()
        {
            NpcBrain.DespawnAll();
            List<DaggerfallLocation> towns = LoadedLocations();
            if (towns.Count == 0 && GameManager.Instance != null && GameManager.Instance.StreamingWorld != null)
            {
                DaggerfallLocation current = GameManager.Instance.StreamingWorld.CurrentPlayerLocationObject;
                if (current != null)
                    towns.Add(current);
            }
            foreach (DaggerfallLocation town in towns)
            {
                SpawnFor(town);
                SpawnGenerics(town, owner.Catalog.Generics, owner.Config.Mode, owner.Config.MaxGenericPerTown);
            }
        }

        // Fast travel, teleport and load tear the world down; old towns are destroyed over many frames.
        void OnWorldCleared()
        {
            NpcBrain.DespawnAll();
            knownLocations.Clear();
        }

        List<DaggerfallLocation> LoadedLocations()
        {
            // StreamingWorld deactivates a town object before destroying it, so inactive ones are on their way out.
            knownLocations.RemoveAll(delegate (DaggerfallLocation l) { return l == null || !l.gameObject.activeSelf; });
            return new List<DaggerfallLocation>(knownLocations);
        }

        public void SpawnFor(DaggerfallLocation location)
        {
            if (location == null)
                return;
            if (!knownLocations.Contains(location))
                knownLocations.Add(location);

            string defaultRace = DefaultRace(location);
            foreach (NpcDefinition def in owner.Catalog.ForLocation(location.Summary.RegionName, location.Summary.LocationName))
            {
                NpcInstance instance = NpcInstance.ForUnique(def, defaultRace);
                SpawnChecked(instance, owner.States.GetOrCreate(instance.Key), location);
            }
        }

        /// <summary>Spawns an ANPC that is not in the catalog (self-test). Its state lives in the normal table.</summary>
        public NpcBrain SpawnTest(NpcInstance instance, Transform parent)
        {
            return Spawn(instance, owner.States.GetOrCreate(instance.Key), parent);
        }

        // Raised once a town's blocks, and so its navigation grid, are complete. Towns streamed in while travelling
        // are laid out over many frames after OnCreateLocationGameObject, so generics must wait for this event.
        void OnLocationLaidOut(GameObject locationObject, bool allowYield)
        {
            DaggerfallLocation location = locationObject == null ? null : locationObject.GetComponent<DaggerfallLocation>();
            if (location != null)
                owner.StartCoroutine(SpawnGenericsNextFrame(location));
        }

        // A town built in one go raises the event in the frame it was created, before CityNavigation.Start has
        // linked the grid to its location (placing a person then throws). One frame later both are ready.
        IEnumerator SpawnGenericsNextFrame(DaggerfallLocation location)
        {
            yield return null;
            if (location == null || !location.gameObject.activeSelf)
                yield break;
            SpawnGenerics(location, owner.Catalog.Generics, owner.Config.Mode, owner.Config.MaxGenericPerTown);
        }

        /// <summary>Walkable cell count each town's generics were planned on, by map id (read by the self-test).</summary>
        public readonly Dictionary<int, int> PlannedCellCounts = new Dictionary<int, int>();

        /// <summary>Towns currently loaded.</summary>
        public List<DaggerfallLocation> LoadedTowns()
        {
            return LoadedLocations();
        }

        /// <summary>Plans and spawns a town's generic ANPCs (spec §7.4). Returns the ANPCs spawned now.</summary>
        public List<NpcBrain> SpawnGenerics(DaggerfallLocation location, IList<NpcDefinition> templates, GenericMode mode, int cap)
        {
            List<NpcBrain> spawned = new List<NpcBrain>();
            if (location == null || templates == null || templates.Count == 0 || cap <= 0)
                return spawned;

            TownInfo town = new TownInfo(location.Summary.MapID, location.Summary.RegionName, location.Summary.LocationName,
                location.Summary.LocationType.ToString(), DefaultRace(location));
            if (!PopulationPlanner.AnyMatches(templates, town))
                return spawned; // most locations (dungeons, farms, ...) never need their grid scanned

            CityNavigation nav = location.GetComponent<CityNavigation>();
            int cellCount = WalkableCount(location);
            PlannedCellCounts[town.MapId] = cellCount;
            uint visitSeed = (uint)UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            List<NpcInstance> plan = PopulationPlanner.Plan(templates, town, mode, cap, cellCount, visitSeed, owner.Names);
            if (plan.Count == 0)
                return spawned;

            List<int> wanted = new List<int>();
            foreach (NpcInstance instance in plan)
            {
                if (!instance.HasFixedPosition && instance.CellIndex >= 0)
                    wanted.Add(instance.CellIndex);
            }
            Dictionary<int, int[]> cells = nav == null
                ? new Dictionary<int, int[]>()
                : NavCells.Resolve(nav.NavGridWidth, nav.NavGridHeight, Walkable(nav), wanted);

            foreach (NpcInstance instance in plan)
            {
                if (!instance.HasFixedPosition)
                {
                    int[] cell;
                    if (instance.CellIndex < 0 || !cells.TryGetValue(instance.CellIndex, out cell))
                    {
                        if (townsWithoutCells.Add(town.MapId))
                            AdvancedNpcsMod.Log(town.Place + ": no walkable cells; generic ANPCs without fixed positions are not spawned there.");
                        continue;
                    }
                    try
                    {
                        Vector3 local = CellToLocal(location, nav, new DFPosition(cell[0], cell[1]));
                        instance.X = local.x;
                        instance.Y = local.y;
                        instance.Z = local.z;
                    }
                    catch (Exception e)
                    {
                        AdvancedNpcsMod.LogError(instance.Key + ": spawn failed (" + e.Message + ")");
                        continue;
                    }
                }
                NpcState state = instance.Persistent ? owner.States.GetOrCreate(instance.Key) : new NpcState();
                NpcBrain brain = SpawnChecked(instance, state, location);
                if (brain != null)
                    spawned.Add(brain);
            }
            AdvancedNpcsMod.Log(town.Place + ": " + spawned.Count + " generic ANPC(s) spawned (" + plan.Count + " planned, " +
                cellCount + " walkable cells, " + mode + ").");
            return spawned;
        }

        /// <summary>Number of street cells (navigation weight &gt; 0) in the town's grid.</summary>
        public static int WalkableCount(DaggerfallLocation location)
        {
            CityNavigation nav = location.GetComponent<CityNavigation>();
            return nav == null ? 0 : NavCells.Count(nav.NavGridWidth, nav.NavGridHeight, Walkable(nav));
        }

        static Func<int, int, bool> Walkable(CityNavigation nav)
        {
            return delegate (int x, int y) { return nav.GetNavGridWeightLocal(x, y) > 0; };
        }

        static Vector3 CellToLocal(DaggerfallLocation location, CityNavigation nav, DFPosition cell)
        {
            Vector3 scene = nav.WorldToScenePosition(nav.NavGridToWorldPosition(cell));
            return location.transform.InverseTransformPoint(scene);
        }

        /// <summary>The people of the town's own climate, used when an ANPC does not set its race.</summary>
        public static string DefaultRace(DaggerfallLocation location)
        {
            FactionFile.FactionRaces people = MapsFile.GetWorldClimateSettings((int)location.Summary.WorldClimate).People;
            if (people == FactionFile.FactionRaces.Redguard)
                return "Redguard";
            if (people == FactionFile.FactionRaces.Nord)
                return "Nord";
            return "Breton";
        }

        /// <summary>Spawns unless dead or already spawned in this town (SpawnRules). Returns the new brain or null.</summary>
        NpcBrain SpawnChecked(NpcInstance instance, NpcState state, DaggerfallLocation location)
        {
            NpcBrain existing = NpcBrain.Find(instance.Key);
            bool existingHere = existing != null && existing.transform.parent == location.transform;
            SpawnAction action = SpawnRules.Decide(state.dead, existing != null, existingHere);
            if (action == SpawnAction.Skip)
                return null;
            if (action == SpawnAction.ReplaceStale)
                NpcBrain.Discard(existing);
            try
            {
                return Spawn(instance, state, location.transform);
            }
            catch (Exception e)
            {
                AdvancedNpcsMod.LogError(instance.Key + ": spawn failed (" + e.Message + ")");
                return null;
            }
        }

        /// <summary>
        /// The portrait file for this person (spec v2.1 §6.2): the one locked at the first talk if it still exists,
        /// otherwise a seeded pick from the joined pools of its portrait names (re-locked if the old file is gone).
        /// </summary>
        string ResolvePortrait(NpcInstance instance, NpcState state, List<string> pool)
        {
            if (!string.IsNullOrEmpty(state.portrait) && owner.Portraits.Get(state.portrait) != null)
                return state.portrait;
            string chosen = PortraitPools.Choose(pool, instance.Seed);
            if (!string.IsNullOrEmpty(state.portrait) && chosen != null)
                state.portrait = chosen;
            return chosen;
        }

        NpcBrain Spawn(NpcInstance instance, NpcState state, Transform parent)
        {
            NpcDefinition def = instance.Definition;
            MobileTypes type = (MobileTypes)Enum.Parse(typeof(MobileTypes), def.BaseClass);
            MobileGender gender = instance.Gender == "Female" ? MobileGender.Female : MobileGender.Male;

            GameObject go = GameObjectHelper.CreateEnemy(instance.Name, type, new Vector3(instance.X, instance.Y, instance.Z),
                gender, parent, MobileReactions.Passive);

            // SerializableEnemy only registers with the vanilla save system when LoadID != 0.
            // Keeping it 0 means our own state table is the only save, so loads never duplicate ANPCs.
            DaggerfallEnemy enemy = go.GetComponent<DaggerfallEnemy>();
            if (enemy != null)
                enemy.LoadID = 0;

            // Townsfolk side: with Enemy Infighting on, enemies attack anything on another team, and class
            // enemies default to KnightsAndMages/Criminals. On the CityWatch team, guards leave them alone
            // while monsters (other teams) can still attack them.
            DaggerfallEntityBehaviour behaviour = go.GetComponent<DaggerfallEntityBehaviour>();
            if (behaviour != null && behaviour.Entity != null)
                behaviour.Entity.Team = MobileTeams.CityWatch;

            go.AddComponent<NpcMover>();
            NpcBrain brain = go.AddComponent<NpcBrain>();
            brain.Init(instance, state);
            List<string> pool = PortraitPools.Joined(owner.Portraits.Names, instance.Definition.Portraits);
            string portraitFile = ResolvePortrait(instance, state, pool);
            instance.PortraitName = portraitFile;
            go.AddComponent<NpcTalk>().Init(instance, owner.Portraits.Get(portraitFile), portraitFile, PortraitPools.IsLockable(pool));

            // Custom sprites (spec 1b): one of the folder's sprite sets, chosen by the person's seed.
            List<LoadedSpriteSet> spriteSets = owner.Sprites.For(def.Folder);
            if (spriteSets.Count > 0)
            {
                LoadedSpriteSet spriteSet = spriteSets[SpriteStates.PickSet(spriteSets.Count, instance.Seed)];
                float height = def.SpriteHeight > 0 ? def.SpriteHeight : NpcSprite.DefaultHeight(go);
                go.AddComponent<NpcSprite>().Init(spriteSet, height);
            }
            AdvancedNpcsMod.Log(instance.Key + " (" + instance.Name + "): spawned.");
            return brain;
        }
    }
}

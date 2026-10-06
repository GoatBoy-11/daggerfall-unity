using System;
using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Utility;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>Spawns Advanced NPCs when their town's GameObject is created.</summary>
    public class NpcSpawner
    {
        readonly AdvancedNpcsMod owner;

        // Towns currently built by StreamingWorld (several are loaded around the player at once).
        readonly List<DaggerfallLocation> knownLocations = new List<DaggerfallLocation>();

        public NpcSpawner(AdvancedNpcsMod owner)
        {
            this.owner = owner;
        }

        public void Enable()
        {
            StreamingWorld.OnCreateLocationGameObject += SpawnFor;
            StreamingWorld.OnClearStreamingWorld += OnWorldCleared;
            owner.OnStateRestored += RespawnLoadedLocations;
        }

        public void Disable()
        {
            StreamingWorld.OnCreateLocationGameObject -= SpawnFor;
            StreamingWorld.OnClearStreamingWorld -= OnWorldCleared;
            owner.OnStateRestored -= RespawnLoadedLocations;
        }

        /// <summary>After a load or new game, rebuild NPCs in every loaded town from the restored state.</summary>
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
                SpawnFor(town);
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
            knownLocations.RemoveAll(l => l == null || !l.gameObject.activeSelf);
            return new List<DaggerfallLocation>(knownLocations);
        }

        void SpawnFor(DaggerfallLocation location)
        {
            if (location == null)
                return;
            if (!knownLocations.Contains(location))
                knownLocations.Add(location);

            List<NpcDefinition> defs = owner.Catalog.ForLocation(location.Summary.RegionName, location.Summary.LocationName);
            foreach (NpcDefinition def in defs)
            {
                NpcState state = owner.States.GetOrCreate(def.Id);
                NpcBrain existing = NpcBrain.Find(def.Id);
                bool existingHere = existing != null && existing.transform.parent == location.transform;
                SpawnAction action = SpawnRules.Decide(state.dead, existing != null, existingHere);
                if (action == SpawnAction.Skip)
                    continue;
                if (action == SpawnAction.ReplaceStale)
                    NpcBrain.Discard(existing);
                try
                {
                    Spawn(def, state, location.transform);
                }
                catch (Exception e)
                {
                    AdvancedNpcsMod.LogError(def.Id + ": spawn failed (" + e.Message + ")");
                }
            }
        }

        static void Spawn(NpcDefinition def, NpcState state, Transform parent)
        {
            MobileTypes type = (MobileTypes)Enum.Parse(typeof(MobileTypes), def.BaseClass);
            MobileGender gender = MobileGender.Unspecified;
            if (def.Gender == "Male")
                gender = MobileGender.Male;
            else if (def.Gender == "Female")
                gender = MobileGender.Female;

            GameObject go = GameObjectHelper.CreateEnemy(def.Name, type, new Vector3(def.X, def.Y, def.Z),
                gender, parent, MobileReactions.Passive);

            // SerializableEnemy only registers with the vanilla save system when LoadID != 0.
            // Keeping it 0 means our own state table is the only save, so loads never duplicate NPCs.
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
            brain.Init(def, state);
            AdvancedNpcsMod.Log(def.Id + ": spawned in " + def.Place + ".");
        }
    }
}

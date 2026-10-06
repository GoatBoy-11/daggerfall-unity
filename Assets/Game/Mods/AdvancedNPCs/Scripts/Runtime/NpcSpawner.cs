using System;
using System.Collections.Generic;
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
            knownLocations.RemoveAll(delegate (DaggerfallLocation l) { return l == null || !l.gameObject.activeSelf; });
            return new List<DaggerfallLocation>(knownLocations);
        }

        public void SpawnFor(DaggerfallLocation location)
        {
            if (location == null)
                return;
            if (!knownLocations.Contains(location))
                knownLocations.Add(location);

            string defaultRace = DefaultRace();
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

        /// <summary>The region's people, used when an ANPC does not set its race.</summary>
        public static string DefaultRace()
        {
            FactionFile.FactionRaces people = GameManager.Instance.PlayerGPS.ClimateSettings.People;
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
            AdvancedNpcsMod.Log(instance.Key + " (" + instance.Name + "): spawned.");
            return brain;
        }
    }
}

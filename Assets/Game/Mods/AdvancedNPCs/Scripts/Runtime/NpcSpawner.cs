using System;
using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Utility;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>Spawns Advanced NPCs when their town's GameObject is created.</summary>
    public class NpcSpawner
    {
        readonly AdvancedNpcsMod owner;

        public NpcSpawner(AdvancedNpcsMod owner)
        {
            this.owner = owner;
        }

        public void Enable()
        {
            StreamingWorld.OnCreateLocationGameObject += SpawnFor;
            owner.OnStateRestored += RespawnCurrentLocation;
        }

        public void Disable()
        {
            StreamingWorld.OnCreateLocationGameObject -= SpawnFor;
            owner.OnStateRestored -= RespawnCurrentLocation;
        }

        /// <summary>After a load or new game, rebuild NPCs in the current town from the restored state.</summary>
        public void RespawnCurrentLocation()
        {
            NpcBrain.DespawnAll();
            if (GameManager.Instance == null || GameManager.Instance.StreamingWorld == null)
                return;
            SpawnFor(GameManager.Instance.StreamingWorld.CurrentPlayerLocationObject);
        }

        void SpawnFor(DaggerfallLocation location)
        {
            if (location == null)
                return;

            List<NpcDefinition> defs = owner.Catalog.ForLocation(location.Summary.RegionName, location.Summary.LocationName);
            foreach (NpcDefinition def in defs)
            {
                if (NpcBrain.IsLive(def.Id))
                    continue;
                NpcState state = owner.States.GetOrCreate(def.Id);
                if (state.dead)
                    continue;
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

            go.AddComponent<NpcMover>();
            NpcBrain brain = go.AddComponent<NpcBrain>();
            brain.Init(def, state);
            AdvancedNpcsMod.Log(def.Id + ": spawned in " + def.Place + ".");
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using Wenzil.Console;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>Mod entry point. Owns the catalog and state table and wires DFU events.</summary>
    public class AdvancedNpcsMod : MonoBehaviour
    {
        public const string LogPrefix = "[AdvancedNPCs] ";
        public const string FolderName = "AdvancedNPCs";

        public static AdvancedNpcsMod Instance { get; private set; }

        public DefinitionCatalog Catalog { get; private set; }
        public NpcStateTable States { get; private set; }

        /// <summary>Raised after save data is restored or a new game starts.</summary>
        public event Action OnStateRestored;

        static Mod mod;
        NpcSpawner spawner;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;
            GameObject go = new GameObject("AdvancedNPCs");
            Instance = go.AddComponent<AdvancedNpcsMod>();
            mod.SaveDataInterface = new NpcSaveDataInterface(Instance.States, Instance.RaiseStateRestored);
            mod.IsReady = true;
        }

        void Awake()
        {
            States = new NpcStateTable();
            Catalog = LoadCatalog();
            StartGameBehaviour.OnNewGame += OnNewGame;
            ConsoleCommandsDatabase.RegisterCommand("anpc_pos",
                "Prints your position as Advanced NPC definition JSON.", "anpc_pos", PosCommand);
            spawner = new NpcSpawner(this);
            spawner.Enable();
        }

        void OnDestroy()
        {
            StartGameBehaviour.OnNewGame -= OnNewGame;
            if (spawner != null)
                spawner.Disable();
        }

        void OnNewGame()
        {
            States.Clear();
            RaiseStateRestored();
        }

        void RaiseStateRestored()
        {
            if (OnStateRestored != null)
                OnStateRestored();
        }

        static DefinitionCatalog LoadCatalog()
        {
            string folder = Path.Combine(Application.streamingAssetsPath, FolderName);
            List<KeyValuePair<string, string>> files = new List<KeyValuePair<string, string>>();
            if (!Directory.Exists(folder))
            {
                Log("No definitions folder at " + folder + "; nothing to spawn.");
            }
            else
            {
                foreach (string path in Directory.GetFiles(folder, "*.json"))
                {
                    try
                    {
                        files.Add(new KeyValuePair<string, string>(Path.GetFileName(path), File.ReadAllText(path)));
                    }
                    catch (Exception e)
                    {
                        LogError(Path.GetFileName(path) + ": file: could not read (" + e.Message + ")");
                    }
                }
            }

            DefinitionCatalog catalog = DefinitionCatalog.Build(files);
            foreach (string message in catalog.Messages)
                Log(message);
            return catalog;
        }

        static string PosCommand(params string[] args)
        {
            GameManager gm = GameManager.Instance;
            DaggerfallLocation location = gm.StreamingWorld.CurrentPlayerLocationObject;
            if (gm.PlayerEnterExit.IsPlayerInside || location == null)
                return "Stand outdoors inside a town first.";

            Vector3 local = location.transform.InverseTransformPoint(gm.PlayerObject.transform.position);
            return PositionFormat.ToJsonSnippet(location.Summary.RegionName, location.Summary.LocationName,
                local.x, local.y, local.z);
        }

        public static void Log(string message)
        {
            Debug.Log(LogPrefix + message);
        }

        public static void LogError(string message)
        {
            Debug.LogError(LogPrefix + message);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
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
                "Prints your position as Advanced NPC definition JSON (also written to Player.log).", "anpc_pos", PosCommand);
            ConsoleCommandsDatabase.RegisterCommand("anpc_list",
                "Lists spawned Advanced NPCs with distance, direction and state.", "anpc_list", ListCommand);
            ConsoleCommandsDatabase.RegisterCommand("anpc_place",
                "Saves your current spot as an NPC's home (rewrites its definition file) and moves it here.", "anpc_place <id>", PlaceCommand);
            ConsoleCommandsDatabase.RegisterCommand("anpc_summon",
                "Moves a spawned Advanced NPC in front of you (testing only, not saved).", "anpc_summon <id>", SummonCommand);
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
            string snippet = PositionFormat.ToJsonSnippet(location.Summary.RegionName, location.Summary.LocationName,
                local.x, local.y, local.z);
            Log("anpc_pos\n" + snippet);
            return snippet;
        }

        static string ListCommand(params string[] args)
        {
            List<NpcBrain> brains = NpcBrain.All();
            if (brains.Count == 0)
                return "No Advanced NPCs are spawned nearby (" + Instance.Catalog.Count + " defined).";

            Vector3 player = GameManager.Instance.PlayerObject.transform.position;
            StringBuilder sb = new StringBuilder();
            foreach (NpcBrain b in brains)
            {
                Vector3 d = b.transform.position - player;
                sb.Append(b.Id).Append(": ").Append(Bearing.Describe(d.x, d.z))
                  .Append(", height ").Append(d.y.ToString("0.#", CultureInfo.InvariantCulture))
                  .Append(" (").Append(b.Status).Append(")\n");
            }
            string text = sb.ToString().TrimEnd('\n');
            Log("anpc_list\n" + text);
            return text;
        }

        static string PlaceCommand(params string[] args)
        {
            if (args == null || args.Length == 0)
                return "Usage: anpc_place <id>   (the id from the NPC's definition file)";
            NpcDefinition def;
            if (!Instance.Catalog.ById.TryGetValue(args[0], out def))
                return "No NPC definition with id \"" + args[0] + "\".";

            GameManager gm = GameManager.Instance;
            DaggerfallLocation location = gm.StreamingWorld.CurrentPlayerLocationObject;
            if (gm.PlayerEnterExit.IsPlayerInside || location == null)
                return "Stand outdoors inside a town first.";

            Vector3 world = gm.PlayerObject.transform.position;
            Vector3 local = location.transform.InverseTransformPoint(world);
            string region = location.Summary.RegionName;
            string place = location.Summary.LocationName;
            string path = Path.Combine(Path.Combine(Application.streamingAssetsPath, FolderName), def.SourceFile);
            try
            {
                string edited = DefinitionEditor.SetPlacement(File.ReadAllText(path), region, place, local.x, local.y, local.z);
                File.WriteAllText(path, edited);
            }
            catch (Exception e)
            {
                LogError(def.SourceFile + ": could not save placement (" + e.Message + ")");
                return "Could not update " + def.SourceFile + ": " + e.Message;
            }

            def.Region = region;
            def.Place = place;
            def.X = local.x;
            def.Y = local.y;
            def.Z = local.z;

            NpcBrain brain = NpcBrain.Find(def.Id);
            if (brain != null && brain.transform.parent == location.transform)
            {
                brain.Teleport(world);
            }
            else
            {
                NpcBrain.Discard(brain);
                Instance.spawner.SpawnFor(location);
            }

            string message = def.Id + " placed here and saved to " + def.SourceFile + ".";
            if (Instance.States.GetOrCreate(def.Id).dead)
                message += " (It is dead in this save, so it will not appear.)";
            Log(message + "\n" + PositionFormat.ToJsonSnippet(region, place, local.x, local.y, local.z));
            return message;
        }

        static string SummonCommand(params string[] args)
        {
            if (args == null || args.Length == 0)
                return "Usage: anpc_summon <id>   (ids from anpc_list)";
            NpcBrain b = NpcBrain.Find(args[0]);
            if (b == null)
                return "No spawned NPC with id \"" + args[0] + "\". Try anpc_list.";

            Transform player = GameManager.Instance.PlayerObject.transform;
            b.Teleport(player.position + player.forward * 2f);
            string message = args[0] + " summoned.";
            Log(message);
            return message;
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

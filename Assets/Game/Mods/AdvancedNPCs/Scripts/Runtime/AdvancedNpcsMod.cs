using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Serialization;
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
        public const string AutoRunFile = "selftest-autorun.txt";

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
            AnpcFiles.MigrateLegacy();
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
            ConsoleCommandsDatabase.RegisterCommand("anpc_selftest",
                "Runs the Advanced NPCs in-game self-test next to you (god mode during the run; results in Player.log).", "anpc_selftest", SelfTestCommand);

            string autorun = Path.Combine(AnpcFiles.Root, AutoRunFile);
            if (File.Exists(autorun))
                StartCoroutine(AutoRun(autorun));
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
            DefinitionCatalog catalog = DefinitionCatalog.Build(AnpcFiles.ReadFolders());
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
                return "No ANPCs are spawned nearby (" + Instance.Catalog.Count + " defined).";

            Vector3 player = GameManager.Instance.PlayerObject.transform.position;
            StringBuilder sb = new StringBuilder();
            foreach (NpcBrain b in brains)
            {
                Vector3 d = b.transform.position - player;
                sb.Append(b.Id).Append(" (").Append(b.DisplayName).Append("): ").Append(Bearing.Describe(d.x, d.z))
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
            {
                if (IsGeneric(args[0]))
                    return "\"" + args[0] + "\" is a generic ANPC; generic ANPCs are placed by the spawn rules in their template's npc.json.";
                return "No unique ANPC with id \"" + args[0] + "\".";
            }

            GameManager gm = GameManager.Instance;
            DaggerfallLocation location = gm.StreamingWorld.CurrentPlayerLocationObject;
            if (gm.PlayerEnterExit.IsPlayerInside || location == null)
                return "Stand outdoors inside a town first.";

            Vector3 world = gm.PlayerObject.transform.position;
            Vector3 local = location.transform.InverseTransformPoint(world);
            string region = location.Summary.RegionName;
            string place = location.Summary.LocationName;
            try
            {
                AnpcFiles.SetPlacement(def, region, place, local.x, local.y, local.z);
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

        /// <summary>True for a generic template id or a generic instance key ("commoner@1234#0").</summary>
        static bool IsGeneric(string idOrKey)
        {
            int at = idOrKey.IndexOf('@');
            string template = at >= 0 ? idOrKey.Substring(0, at) : idOrKey;
            foreach (NpcDefinition g in Instance.Catalog.Generics)
            {
                if (g.Id == template)
                    return true;
            }
            return false;
        }

        static string SelfTestCommand(params string[] args)
        {
            if (SelfTest.Running)
                return "The self-test is already running.";
            Instance.StartCoroutine(new SelfTest(Instance, Instance.spawner).Run(null));
            return "Self-test started; results appear on screen and in Player.log (SELFTEST lines).";
        }

        /// <summary>
        /// Unattended test run used by Tools~/selftest.sh: the file's first two lines name a character and a
        /// save; the save is loaded, the self-test runs, and the game quits.
        /// </summary>
        IEnumerator AutoRun(string path)
        {
            string[] lines = File.ReadAllLines(path);
            if (lines.Length < 2)
            {
                LogError(SelfTest.Prefix + "FAIL autorun -- " + AutoRunFile + " needs: character name, then save name");
                Application.Quit();
                yield break;
            }
            string character = lines[0].Trim();
            string save = lines[1].Trim();
            Log(SelfTest.Prefix + "autorun: loading save '" + save + "' of '" + character + "'");
            yield return new WaitForSeconds(3f);
            SaveLoadManager.Instance.Load(character, save);

            float waited = 0f;
            while (waited < 180f && !InTown())
            {
                waited += 1f;
                yield return new WaitForSeconds(1f);
            }
            if (!InTown())
            {
                LogError(SelfTest.Prefix + "FAIL autorun -- save did not load into a town within 180 s");
                Application.Quit();
                yield break;
            }
            yield return new WaitForSeconds(5f); // let the town and its NPCs finish spawning
            yield return StartCoroutine(new SelfTest(this, spawner).Run(null));
            yield return new WaitForSeconds(1f);
            Application.Quit();
        }

        static bool InTown()
        {
            GameManager gm = GameManager.Instance;
            return gm != null && gm.StateManager.CurrentState == StateManager.StateTypes.Game &&
                   !SaveLoadManager.Instance.LoadInProgress && !gm.PlayerEnterExit.IsPlayerInside &&
                   gm.StreamingWorld.CurrentPlayerLocationObject != null;
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

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
        /// <summary>People placed with anpc_spawn in this game (saved with it).</summary>
        public PlacedNpcList Placed { get; private set; }
        public PortraitLibrary Portraits { get; private set; }
        public NameLists Names { get; private set; }
        public SpriteLibrary Sprites { get; private set; }
        public ModConfig Config { get; private set; }
        /// <summary>Dialogue types from ANPCs/_Dialogue.</summary>
        public DialogueLibrary Dialogue { get; private set; }
        /// <summary>Player-wide dialogue flags (saved with the game).</summary>
        public FlagSet Flags { get; private set; }
        /// <summary>Puts dialogue topics into the talk window.</summary>
        public TopicInjector Topics { get; private set; }

        readonly Dictionary<NpcDefinition, ComposedDialogue> composed = new Dictionary<NpcDefinition, ComposedDialogue>();

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
            Instance.Config.Attach(mod);
            mod.MessageReceiver = Instance.OnModMessage;
            mod.SaveDataInterface = new NpcSaveDataInterface(Instance.States, Instance.Placed, Instance.Flags, Instance.RaiseStateRestored);
            mod.IsReady = true;
        }

        void Awake()
        {
            Config = new ModConfig();
            States = new NpcStateTable();
            Placed = new PlacedNpcList();
            AnpcFiles.MigrateLegacy();
            Catalog = LoadCatalog();
            Portraits = PortraitLibrary.Load(AnpcFiles.PortraitsFolder);
            Portraits.WarnMissing(Catalog.ReferencedPortraits());
            Names = NameLists.Load(Path.Combine(AnpcFiles.Root, NameLists.FolderName));
            Names.WarnMissing(Catalog.Generics);
            List<NpcDefinition> all = new List<NpcDefinition>(Catalog.ById.Values);
            all.AddRange(Catalog.Generics);
            Sprites = SpriteLibrary.Load(AnpcFiles.Root, all);
            Flags = new FlagSet();
            Dialogue = DialogueFiles.Load(AnpcFiles.Root);
            DialogueFiles.Check(Dialogue, Catalog);
            Topics = gameObject.AddComponent<TopicInjector>();
            StartGameBehaviour.OnNewGame += OnNewGame;
            ConsoleCommandsDatabase.RegisterCommand("anpc_pos",
                "Prints your position as Advanced NPC definition JSON (also written to Player.log).", "anpc_pos", PosCommand);
            ConsoleCommandsDatabase.RegisterCommand("anpc_list",
                "Lists spawned Advanced NPCs with distance, direction and state.", "anpc_list", ListCommand);
            ConsoleCommandsDatabase.RegisterCommand("anpc_place",
                "Saves your current spot as an NPC's home (rewrites its definition file) and moves it here.", "anpc_place <id>", PlaceCommand);
            ConsoleCommandsDatabase.RegisterCommand("anpc_spawn",
                "Makes a new person from a generic ANPC template in front of you; they stay in this town (saved with your game).",
                "anpc_spawn <template>", SpawnCommand);
            ConsoleCommandsDatabase.RegisterCommand("anpc_remove",
                "Removes a person made with anpc_spawn (from the world and from your save).", "anpc_remove <id>", RemoveCommand);
            ConsoleCommandsDatabase.RegisterCommand("anpc_hostile",
                "Turns an ANPC into an enemy (no crime to fight) or calms it; saved with your game.",
                "anpc_hostile <id or key> [on|off]", HostileCommand);
            ConsoleCommandsDatabase.RegisterCommand("anpc_topics",
                "Shows the dialogue topics of the ANPC in front of you (or <id>): which are shown, and why the others are hidden.",
                "anpc_topics [id]", TopicsCommand);
            ConsoleCommandsDatabase.RegisterCommand("anpc_reload_dialogue",
                "Re-reads ANPCs/_Dialogue/*.json and every dialogue.json (changes to npc.json need a restart).",
                "anpc_reload_dialogue", ReloadDialogueCommand);
            ConsoleCommandsDatabase.RegisterCommand("anpc_flag",
                "Lists dialogue flags, or sets / clears one (saved with your game).", "anpc_flag [name on|off]", FlagCommand);
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
            Placed.Clear();
            Flags.ClearAll();
            RaiseStateRestored();
        }

        /// <summary>The topics and greetings an ANPC definition uses (its types, then its folder's dialogue.json).</summary>
        public ComposedDialogue DialogueFor(NpcDefinition def)
        {
            if (def == null)
                return new ComposedDialogue();
            ComposedDialogue c;
            if (!composed.TryGetValue(def, out c))
            {
                c = Dialogue.Compose(def);
                composed[def] = c;
            }
            return c;
        }

        /// <summary>Adds or replaces a dialogue type (self-test).</summary>
        public void AddDialogueType(DialogueFile file)
        {
            Dialogue.Add(file);
            composed.Clear();
        }

        static string TopicsCommand(params string[] args)
        {
            NpcBrain b = args != null && args.Length > 0 ? NpcBrain.Find(args[0]) : Nearest(5f);
            if (b == null || b.Instance == null)
                return args != null && args.Length > 0 ? "No spawned ANPC \"" + args[0] + "\". Try anpc_list." : "No ANPC within 5 m. Stand in front of one or give an id (anpc_list).";
            NpcDefinition def = b.Instance.Definition;
            ComposedDialogue d = Instance.DialogueFor(def);
            StringBuilder sb = new StringBuilder();
            sb.Append(b.Id).Append(" (").Append(b.DisplayName).Append("): dialogue ")
              .Append(def.Dialogue.Count > 0 ? string.Join(", ", def.Dialogue.ToArray()) : "none")
              .Append(def.OwnDialogue != null ? " + " + def.Folder + "/dialogue.json" : "").Append('\n');
            if (d.Topics.Count == 0)
                sb.Append("  no topics\n");
            GameFacts facts = new GameFacts(b.State, Instance.Flags, b.DisplayName, null);
            foreach (DialogueTopic t in d.Topics)
            {
                string why = Conversation.WhyHidden(t, facts);
                sb.Append("  ").Append(t.Caption).Append(" [").Append(t.Id).Append("]: ").Append(why == null ? "shown" : "hidden, " + why).Append('\n');
            }
            sb.Append("  asked: ").Append(b.State.asked != null && b.State.asked.Count > 0 ? string.Join(", ", b.State.asked.ToArray()) : "nothing").Append('\n');
            sb.Append("  (\"reaction\" uses the last conversation's value: ").Append(facts.Reaction).Append(')');
            string text = sb.ToString();
            Log("anpc_topics\n" + text);
            return text;
        }

        static NpcBrain Nearest(float maxDistance)
        {
            Vector3 player = GameManager.Instance.PlayerObject.transform.position;
            NpcBrain best = null;
            float bestDistance = maxDistance;
            foreach (NpcBrain b in NpcBrain.All())
            {
                float d = Vector3.Distance(b.transform.position, player);
                if (d <= bestDistance)
                {
                    best = b;
                    bestDistance = d;
                }
            }
            return best;
        }

        static string ReloadDialogueCommand(params string[] args)
        {
            Instance.Dialogue = DialogueFiles.Load(AnpcFiles.Root);
            DialogueFiles.ReloadOwn(Instance.Catalog);
            Instance.composed.Clear();
            int problems = DialogueFiles.Check(Instance.Dialogue, Instance.Catalog);
            string message = "Reloaded " + Instance.Dialogue.Count + " dialogue type(s) and the folders' dialogue.json" +
                             (problems > 0 ? "; see Player.log for problems ([AdvancedNPCs] lines)." : "; no problems found by the cross-file checks.") +
                             " Talk to an ANPC again to see the changes.";
            Log(message);
            return message;
        }

        static string FlagCommand(params string[] args)
        {
            if (args == null || args.Length == 0)
            {
                List<string> names = Instance.Flags.Names();
                return names.Count == 0 ? "No dialogue flags are set." : "Dialogue flags: " + string.Join(", ", names.ToArray());
            }
            if (args.Length != 2 || (args[1] != "on" && args[1] != "off"))
                return "Usage: anpc_flag <name> on|off   (or anpc_flag alone to list them)";
            string name = DialogueIds.Normalize(args[0]);
            if (name.Length == 0)
                return "Flag names need letters or digits.";
            if (args[1] == "on")
                Instance.Flags.Set(name);
            else
                Instance.Flags.Clear(name);
            string message = "Flag " + name + (args[1] == "on" ? " set." : " cleared.") + " Saved with your game.";
            Log(message);
            return message;
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
        /// Unattended start used by Tools~/selftest.sh and Tools~/play.sh: the flag file's first two lines name a
        /// character and a save, which is loaded straight from the title screen. A third line "play" stops there
        /// (manual testing; the flag file is removed so the next start is normal); otherwise the self-test runs
        /// and the game quits.
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
            bool play = lines.Length > 2 && lines[2].Trim() == "play";
            if (play)
                File.Delete(path);
            Log(SelfTest.Prefix + "autorun: loading save '" + save + "' of '" + character + "'" + (play ? " to play" : ""));

            // The title screen runs with the game paused (timeScale 0), so only real-time waits finish there.
            float waited = 0f;
            while (waited < 60f && !SaveLoadManager.Instance.IsReady())
            {
                waited += 0.5f;
                yield return new WaitForSecondsRealtime(0.5f);
            }
            yield return new WaitForSecondsRealtime(2f);
            SaveLoadManager.Instance.EnumerateSaves();
            int key = SaveLoadManager.Instance.FindSaveFolderByNames(character, save);
            if (key < 0)
            {
                LogError(SelfTest.Prefix + "FAIL autorun -- no save '" + save + "' of character '" + character + "'");
                if (!play)
                    Application.Quit();
                yield break;
            }
            SaveLoadManager.Instance.Load(key);

            // Like DFU's own Load button, close the title screen windows (intro video, start menu): while they
            // are open the game stays paused. They can appear after the load starts, so keep closing them.
            waited = 0f;
            while (waited < 180f && !InTown())
            {
                DaggerfallUI.Instance.PopToHUD();
                waited += 1f;
                yield return new WaitForSecondsRealtime(1f);
            }
            DaggerfallUI.Instance.PopToHUD();
            if (!InTown())
            {
                LogError(SelfTest.Prefix + "FAIL autorun -- save did not load into a town within 180 s");
                if (!play)
                    Application.Quit();
                yield break;
            }
            if (play)
            {
                Log("Loaded '" + save + "'; have fun.");
                yield break;
            }
            yield return new WaitForSecondsRealtime(5f); // let the town and its NPCs finish spawning
            yield return StartCoroutine(new SelfTest(this, spawner).Run(null));
            yield return new WaitForSecondsRealtime(1f);
            Application.Quit();
        }

        static bool InTown()
        {
            GameManager gm = GameManager.Instance;
            return gm != null && gm.StateManager.CurrentState == StateManager.StateTypes.Game &&
                   !SaveLoadManager.Instance.LoadInProgress && !gm.PlayerEnterExit.IsPlayerInside &&
                   gm.StreamingWorld.CurrentPlayerLocationObject != null;
        }

        static string SpawnCommand(params string[] args)
        {
            List<string> ids = new List<string>();
            foreach (NpcDefinition g in Instance.Catalog.Generics)
                ids.Add(g.Id);
            ids.Sort(StringComparer.Ordinal);
            string known = ids.Count > 0 ? string.Join(", ", ids.ToArray()) : "none";
            if (args == null || args.Length == 0)
                return "Usage: anpc_spawn <template>   (generic templates: " + known + ")";
            NpcDefinition template = Instance.Catalog.Generics.Find(delegate (NpcDefinition g) { return g.Id == args[0]; });
            if (template == null)
            {
                if (Instance.Catalog.ById.ContainsKey(args[0]))
                    return "\"" + args[0] + "\" is a unique ANPC: there is only one. Use anpc_summon or anpc_place.";
                return "No generic template \"" + args[0] + "\" (generic templates: " + known + ").";
            }
            string error;
            NpcBrain brain = Instance.SpawnInFront(template, Instance.Catalog.Generics, out error);
            if (brain == null)
                return error;
            string message = brain.Id + " (" + brain.DisplayName + ") spawned; saved with your game. Undo: anpc_remove " + brain.Id;
            Log(message);
            return message;
        }

        /// <summary>
        /// Adds a person from a generic template two steps in front of the player, facing them, and records it in the
        /// save (anpc_spawn; also the self-test). Null with an error if the player is not outdoors in a town.
        /// </summary>
        public NpcBrain SpawnInFront(NpcDefinition template, IList<NpcDefinition> templates, out string error)
        {
            GameManager gm = GameManager.Instance;
            DaggerfallLocation location = gm.StreamingWorld.CurrentPlayerLocationObject;
            error = null;
            if (gm.PlayerEnterExit.IsPlayerInside || location == null)
            {
                error = "Stand outdoors inside a town first.";
                return null;
            }
            Transform player = gm.PlayerObject.transform;
            Vector3 ahead = player.forward;
            ahead.y = 0;
            ahead = ahead.sqrMagnitude > 0.0001f ? ahead.normalized : Vector3.forward;
            // Two steps ahead, or short of a wall or door in the way.
            float distance = 2f;
            RaycastHit wall;
            if (Physics.Raycast(player.position, ahead, out wall, distance + 0.5f, ~0, QueryTriggerInteraction.Ignore))
                distance = Mathf.Min(distance, wall.distance - 0.5f);
            if (distance < 0.7f)
            {
                error = "No room in front of you; step back or turn around.";
                return null;
            }
            Vector3 local = location.transform.InverseTransformPoint(player.position + ahead * distance);
            PlacedNpc placed = Placed.Add(template.Id, location.Summary.MapID, location.Summary.RegionName, location.Summary.LocationName,
                local.x, local.y, local.z);
            NpcBrain brain = spawner.SpawnPlaced(location, placed, templates);
            if (brain == null)
            {
                Placed.Remove(placed.Key());
                error = "Could not spawn " + template.Id + " here (see Player.log).";
                return null;
            }
            brain.transform.rotation = Quaternion.LookRotation(-ahead);
            return brain;
        }

        static string RemoveCommand(params string[] args)
        {
            if (args == null || args.Length == 0)
                return "Usage: anpc_remove <id>   (ids from anpc_list; only people made with anpc_spawn)";
            if (!Instance.RemovePlaced(args[0]))
                return "\"" + args[0] + "\" was not made with anpc_spawn. Try anpc_list.";
            string message = args[0] + " removed.";
            Log(message);
            return message;
        }

        /// <summary>Forgets a person made with anpc_spawn and takes it out of the world. False if the key is not one.</summary>
        public bool RemovePlaced(string key)
        {
            if (!Placed.Remove(key))
                return false;
            States.Remove(key);
            NpcCorpseSprite.RemoveFor(key);
            NpcBrain brain = NpcBrain.Find(key);
            if (brain != null)
                NpcBrain.Discard(brain);
            return true;
        }

        /// <summary>
        /// Turns an ANPC (by id or key) into an enemy of the player, or calms it (enemies spec §4). The switch is
        /// saved. False if no ANPC has that id or key (spawned now, defined, placed, or remembered in the save).
        /// </summary>
        public bool SetHostile(string key, bool hostile)
        {
            if (string.IsNullOrEmpty(key))
                return false;
            NpcBrain brain = NpcBrain.Find(key);
            if (brain != null)
            {
                brain.SwitchHostile(hostile);
                return true;
            }
            if (!Catalog.ById.ContainsKey(key) && Placed.Find(key) == null && !States.Has(key))
                return false;
            NpcState s = States.GetOrCreate(key);
            s.enemyOn = hostile;
            s.enemyOff = !hostile;
            if (!hostile)
                s.hostile = false;
            return true;
        }

        static string HostileCommand(params string[] args)
        {
            if (args == null || args.Length == 0 || args.Length > 2)
                return "Usage: anpc_hostile <id or key> [on|off]   (ids from anpc_list; without on/off it toggles)";
            bool hostile;
            if (args.Length == 2)
            {
                if (args[1] == "on")
                    hostile = true;
                else if (args[1] == "off")
                    hostile = false;
                else
                    return "Second word must be on or off.";
            }
            else
            {
                NpcBrain b = NpcBrain.Find(args[0]);
                if (b == null)
                    return "No spawned ANPC \"" + args[0] + "\" to toggle; give on or off. Try anpc_list.";
                hostile = !b.IsEnemy;
            }
            if (!Instance.SetHostile(args[0], hostile))
                return "No ANPC \"" + args[0] + "\". Try anpc_list.";
            string message = args[0] + (hostile ? " is now an enemy." : " is calm now.") + " Saved with your game.";
            Log(message);
            return message;
        }

        /// <summary>
        /// Messages from other mods: "SetHostile" with data "key|on" or "key|off"; the callback gets true when the
        /// ANPC was found. "SetFlag" with data "name|on" / "name|off" (or object[] { name, bool }); "HasFlag" with data
        /// "name", the callback gets whether the dialogue flag is set.
        /// </summary>
        void OnModMessage(string message, object data, DFModMessageCallback callBack)
        {
            if (message == "SetFlag" || message == "HasFlag")
            {
                OnFlagMessage(message, data, callBack);
                return;
            }
            if (message != "SetHostile")
                return;
            string text = data as string;
            int bar = text != null ? text.LastIndexOf('|') : -1;
            bool done = false;
            if (bar > 0)
            {
                string mode = text.Substring(bar + 1).Trim();
                if (mode == "on" || mode == "off")
                    done = SetHostile(text.Substring(0, bar).Trim(), mode == "on");
            }
            if (!done)
                Log("SetHostile message not understood or no such ANPC: \"" + text + "\" (use \"<id or key>|on\" or \"|off\").");
            if (callBack != null)
                callBack(message, done);
        }

        void OnFlagMessage(string message, object data, DFModMessageCallback callBack)
        {
            string name = null;
            bool on = true;
            bool understood = false;
            object[] pair = data as object[];
            string text = data as string;
            if (pair != null && pair.Length == 2 && pair[0] is string && pair[1] is bool)
            {
                name = (string)pair[0];
                on = (bool)pair[1];
                understood = true;
            }
            else if (text != null && message == "HasFlag")
            {
                name = text;
                understood = true;
            }
            else if (text != null)
            {
                int bar = text.LastIndexOf('|');
                string mode = bar > 0 ? text.Substring(bar + 1).Trim() : null;
                if (mode == "on" || mode == "off")
                {
                    name = text.Substring(0, bar);
                    on = mode == "on";
                    understood = true;
                }
            }
            understood &= DialogueIds.Normalize(name).Length > 0;
            if (!understood)
            {
                Log(message + " message not understood: use \"name|on\", \"name|off\" or object[] { name, bool } (HasFlag: \"name\").");
                if (callBack != null)
                    callBack(message, false);
                return;
            }
            bool result;
            if (message == "HasFlag")
            {
                result = Flags.Has(name);
            }
            else
            {
                if (on)
                    Flags.Set(name);
                else
                    Flags.Clear(name);
                result = true;
            }
            if (callBack != null)
                callBack(message, result);
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

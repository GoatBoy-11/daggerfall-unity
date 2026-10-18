using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using Wenzil.Console;
using DfuModSettings = DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings.ModSettings;

namespace VanillaFootsteps
{
    /// <summary>
    /// Mod entry point: loads the step sounds, reads the settings and gives every vanilla enemy and wandering
    /// townsperson a footstep component as DFU creates them.
    /// </summary>
    public class VanillaFootstepsMod : MonoBehaviour
    {
        public const string LogPrefix = "[VanillaFootsteps] ";
        public const string Section = "Footsteps";
        /// <summary>StreamingAssets/VanillaFootsteps: loose step_* files there replace the bundled sounds.</summary>
        public const string LooseFolder = "VanillaFootsteps";

        public static VanillaFootstepsMod Instance { get; private set; }

        public bool Townspeople = true;
        public bool ClassEnemies = true;
        public bool Monsters = true;
        public int VolumePercent = 100;
        public float StepDistance = 15f;

        readonly List<AudioClip> clips = new List<AudioClip>();
        string clipSource = "none";
        /// <summary>Steps played since the game started (vfootsteps).</summary>
        public int StepsPlayed;

        static Mod mod;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;
            GameObject go = new GameObject("VanillaFootsteps");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<VanillaFootstepsMod>();
            Instance.LoadClips();
            if (mod.HasSettings)
            {
                mod.LoadSettingsCallback = Instance.ApplySettings;
                mod.LoadSettings();
            }
            GameManager.OnEnemySpawn += OnEnemySpawn;
            PopulationManager.OnMobileNPCCreate += OnMobileNPCCreate;
            ConsoleCommandsDatabase.RegisterCommand("vfootsteps",
                "Vanilla Footsteps: sounds loaded, settings, and how many walkers near you have footsteps.", "vfootsteps", InfoCommand);
            mod.IsReady = true;
        }

        static void OnEnemySpawn(GameObject enemy)
        {
            // ANPCs come through here too; their component sees ANPC's own voice in Start and removes itself.
            if (enemy != null && enemy.GetComponent<EnemyFootsteps>() == null)
                enemy.AddComponent<EnemyFootsteps>();
        }

        static void OnMobileNPCCreate(PopulationManager.PoolItem poolItem)
        {
            if (poolItem.npc != null && poolItem.npc.GetComponent<TownspersonFootsteps>() == null)
                poolItem.npc.gameObject.AddComponent<TownspersonFootsteps>();
        }

        /// <summary>A random step sound; null without any.</summary>
        public AudioClip PickClip()
        {
            return clips.Count == 0 ? null : clips[UnityEngine.Random.Range(0, clips.Count)];
        }

        /// <summary>Whether the player is close enough to hear a step at this position.</summary>
        public bool InEarshot(Vector3 position)
        {
            GameObject player = GameManager.Instance.PlayerObject;
            if (player == null)
                return false;
            float d = Mathf.Min(StepDistance, FootstepRules.MaxAudibleDistance);
            return (player.transform.position - position).sqrMagnitude <= d * d;
        }

        void ApplySettings(DfuModSettings settings, ModSettingsChange change)
        {
            // Each key on its own: settings saved by an older version may lack newer ones.
            Townspeople = Read(delegate { return settings.GetBool(Section, "Townspeople"); }, true);
            ClassEnemies = Read(delegate { return settings.GetBool(Section, "ClassEnemies"); }, true);
            Monsters = Read(delegate { return settings.GetBool(Section, "Monsters"); }, true);
            VolumePercent = Mathf.Clamp(Read(delegate { return settings.GetInt(Section, "Volume"); }, 100), 0, 100);
            StepDistance = Mathf.Clamp(Read(delegate { return settings.GetInt(Section, "Distance"); }, 15), 5, 30);
            Log("Settings: townspeople " + Townspeople + ", class enemies " + ClassEnemies + ", monsters " + Monsters +
                ", volume " + VolumePercent + "%, heard within " + StepDistance + " m.");
        }

        static T Read<T>(Func<T> get, T fallback)
        {
            try
            {
                return get();
            }
            catch (Exception)
            {
                return fallback;
            }
        }

        /// <summary>Loose step_* files in StreamingAssets/VanillaFootsteps if there are any, else the bundled ones.</summary>
        void LoadClips()
        {
            string loose = Path.Combine(Application.streamingAssetsPath, LooseFolder);
            if (Directory.Exists(loose))
            {
                string[] files = Directory.GetFiles(loose);
                Array.Sort(files, StringComparer.Ordinal);
                foreach (string path in files)
                {
                    if (!FootstepRules.IsStepFile(Path.GetFileName(path)))
                        continue;
                    string problem;
                    AudioClip clip = LoadClip(path, out problem);
                    if (clip != null)
                        clips.Add(clip);
                    else
                        LogError(Path.GetFileName(path) + ": " + problem + " (skipped)");
                }
                if (clips.Count > 0)
                {
                    clipSource = clips.Count + " loose file(s) from StreamingAssets/" + LooseFolder;
                    Log("Step sounds: " + clipSource + ".");
                    return;
                }
            }

            if (mod != null && mod.AssetNames != null)
            {
                foreach (string name in mod.AssetNames)
                {
                    string file = Path.GetFileName(name);
                    if (!FootstepRules.IsStepFile(file))
                        continue;
                    AudioClip clip = mod.GetAsset<AudioClip>(file);
                    if (clip != null)
                        clips.Add(clip);
                }
            }
            clipSource = clips.Count > 0 ? clips.Count + " bundled sound(s)" : "none";
            if (clips.Count > 0)
                Log("Step sounds: " + clipSource + ".");
            else
                LogError("No step sounds in the mod or in StreamingAssets/" + LooseFolder + "; nothing will play.");
        }

        /// <summary>A clip from a .wav, .ogg or .mp3 file (blocking, as DFU's own loose-file sounds); null and a reason on failure.</summary>
        static AudioClip LoadClip(string path, out string problem)
        {
            problem = null;
            string ext = Path.GetExtension(path).ToLowerInvariant();
            AudioType type = ext == ".wav" ? AudioType.WAV : ext == ".ogg" ? AudioType.OGGVORBIS : ext == ".mp3" ? AudioType.MPEG : AudioType.UNKNOWN;
            if (type == AudioType.UNKNOWN)
            {
                problem = "not a .wav, .ogg or .mp3 file";
                return null;
            }
            try
            {
                using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(new Uri(Path.GetFullPath(path)).AbsoluteUri, type))
                {
                    DownloadHandlerAudioClip handler = request.downloadHandler as DownloadHandlerAudioClip;
                    if (handler == null)
                    {
                        problem = "no audio handler";
                        return null;
                    }
                    handler.streamAudio = false;
                    request.SendWebRequest();
                    while (!request.isDone) { }
                    if (request.isNetworkError || request.isHttpError)
                    {
                        problem = request.error;
                        return null;
                    }
                    AudioClip clip = handler.audioClip;
                    if (clip == null || clip.length <= 0f)
                    {
                        problem = "could not be decoded (an .ogg with a picture in it does not load: re-export it as audio only)";
                        return null;
                    }
                    clip.name = Path.GetFileName(path);
                    return clip;
                }
            }
            catch (Exception e)
            {
                problem = e.Message;
                return null;
            }
        }

        static string InfoCommand(string[] args)
        {
            VanillaFootstepsMod m = Instance;
            if (m == null)
                return "Vanilla Footsteps is not running.";
            int enemies = 0, enemiesStepping = 0, people = 0, peopleStepping = 0;
            foreach (EnemyFootsteps e in FindObjectsOfType<EnemyFootsteps>())
            {
                enemies++;
                if (e.Audible)
                    enemiesStepping++;
            }
            foreach (TownspersonFootsteps t in FindObjectsOfType<TownspersonFootsteps>())
            {
                people++;
                if (t.Audible)
                    peopleStepping++;
            }
            return "Sounds: " + m.clipSource + ". Townspeople " + m.Townspeople + ", class enemies " + m.ClassEnemies +
                   ", monsters " + m.Monsters + ", volume " + m.VolumePercent + "%, heard within " + m.StepDistance + " m.\n" +
                   "Enemies with footsteps: " + enemies + " (" + enemiesStepping + " walking in earshot); townspeople: " + people +
                   " (" + peopleStepping + " walking in earshot). Steps played: " + m.StepsPlayed + ".";
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

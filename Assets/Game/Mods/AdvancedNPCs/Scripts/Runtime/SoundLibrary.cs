using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>
    /// One ANPC folder's Sounds: the files of each pool and, while some ANPC uses it, their clips (spec sounds §A, §F).
    /// </summary>
    public class LoadedSoundSet
    {
        public string Label;
        public string Dir;
        /// <summary>Pool → file paths, from the folder index.</summary>
        public readonly Dictionary<string, List<string>> Files = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        /// <summary>Pool → loaded clips (filled while loading; empty when unloaded).</summary>
        public readonly Dictionary<string, List<AudioClip>> Clips = new Dictionary<string, List<AudioClip>>(StringComparer.Ordinal);
        /// <summary>Every file has been tried.</summary>
        public bool IsReady;
        /// <summary>Live ANPCs using the set.</summary>
        public int Users;
        internal SoundLibrary Library;
        internal bool Retired;
        internal float UnloadAt = -1f;
        internal Queue<string> Pending;

        /// <summary>The pools the folder has files for.</summary>
        public ICollection<string> Pools
        {
            get { return Files.Keys; }
        }

        public void Acquire()
        {
            if (Library != null)
                Library.Acquire(this);
        }

        public void Release()
        {
            if (Library != null)
                Library.Release(this);
        }

        /// <summary>A random loaded clip of the pool; null when it has none (yet).</summary>
        public AudioClip Pick(string pool)
        {
            List<AudioClip> clips;
            if (pool == null || !Clips.TryGetValue(pool, out clips) || clips.Count == 0)
                return null;
            return clips[UnityEngine.Random.Range(0, clips.Count)];
        }

        /// <summary>"attack 2, step 4" (anpc_info).</summary>
        public string Summary()
        {
            List<string> parts = new List<string>();
            foreach (string pool in SoundPools.All)
            {
                List<string> files;
                if (Files.TryGetValue(pool, out files))
                    parts.Add(pool + " " + files.Count);
            }
            return string.Join(", ", parts.ToArray());
        }
    }

    /// <summary>
    /// Sound sets of every NPC folder (spec sounds §A, §F): ANPCs/&lt;npc&gt;/Sounds. Indexed at start (file names only),
    /// loaded one clip per frame while used, released a while after the last user.
    /// </summary>
    public class SoundLibrary
    {
        public const string FolderName = "Sounds";
        /// <summary>Real seconds a set stays loaded after its last user has gone.</summary>
        public static float UnloadDelaySeconds = 60f;

        readonly Dictionary<string, LoadedSoundSet> byFolder = new Dictionary<string, LoadedSoundSet>(StringComparer.Ordinal);
        readonly List<LoadedSoundSet> loading = new List<LoadedSoundSet>();
        readonly List<LoadedSoundSet> retired = new List<LoadedSoundSet>();

        public int Count
        {
            get { return byFolder.Count; }
        }

        public static SoundLibrary Load(string root, IEnumerable<NpcDefinition> defs)
        {
            SoundLibrary library = new SoundLibrary();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (NpcDefinition def in defs)
            {
                if (!seen.Add(def.Folder))
                    continue;
                LoadedSoundSet set = IndexFolder(def.Folder, Path.Combine(root, def.Folder));
                if (set != null)
                    library.Add(def.Folder, set);
            }
            library.SetDefaultSteps(IndexDefaults(root));
            AdvancedNpcsMod.Log("Indexed " + library.Count + " sound folder(s)" +
                (library.defaultSteps != null ? " and " + library.defaultSteps.Files[SoundPools.Step].Count + " default step sound(s)." : "."));
            return library;
        }

        /// <summary>The shared folder of default sounds: ANPCs/_Sounds.</summary>
        public const string SharedFolder = "_Sounds";
        LoadedSoundSet defaultSteps;

        /// <summary>
        /// The shared footsteps (ANPCs/_Sounds/step_*) for ANPCs without step files of their own (spec default steps);
        /// null when there are none.
        /// </summary>
        public LoadedSoundSet DefaultSteps
        {
            get { return defaultSteps; }
        }

        /// <summary>Replaces the default steps (anpc_reload_assets, self-test); the old set goes once unused.</summary>
        public void SetDefaultSteps(LoadedSoundSet s)
        {
            if (defaultSteps != null && defaultSteps != s)
                Retire(defaultSteps);
            defaultSteps = s;
            if (s != null)
                s.Library = this;
        }

        /// <summary>Re-reads ANPCs/_Sounds (anpc_reload_assets); true when it has default steps.</summary>
        public bool ReloadDefaults(string root)
        {
            SetDefaultSteps(IndexDefaults(root));
            return defaultSteps != null;
        }

        /// <summary>ANPCs/_Sounds indexed, step files only (others are logged and ignored); null without any.</summary>
        static LoadedSoundSet IndexDefaults(string root)
        {
            string dir = Path.Combine(root, SharedFolder);
            if (!Directory.Exists(dir))
                return null;
            LoadedSoundSet set = IndexSet(SharedFolder, dir);
            if (set == null)
                return null;
            foreach (string pool in new List<string>(set.Files.Keys))
            {
                if (pool == SoundPools.Step)
                    continue;
                AdvancedNpcsMod.Log(SharedFolder + ": " + pool + " sounds are not used as defaults (only step); ignored");
                set.Files.Remove(pool);
            }
            if (set.Files.Count == 0)
            {
                AdvancedNpcsMod.Log(SharedFolder + ": no step sounds; ANPCs without their own have no footsteps");
                return null;
            }
            return set;
        }

        /// <summary>The Sounds folder (any case) of an NPC folder, indexed; null when it has none or no usable file.</summary>
        static LoadedSoundSet IndexFolder(string folder, string npcDir)
        {
            if (!Directory.Exists(npcDir))
                return null;
            foreach (string sub in Directory.GetDirectories(npcDir))
            {
                if (string.Equals(Path.GetFileName(sub), FolderName, StringComparison.OrdinalIgnoreCase))
                    return IndexSet(folder + "/" + Path.GetFileName(sub), sub);
            }
            return null;
        }

        /// <summary>Sorts a Sounds folder's files into pools; loads nothing. Null when no file is usable.</summary>
        public static LoadedSoundSet IndexSet(string label, string dir)
        {
            LoadedSoundSet set = new LoadedSoundSet();
            set.Label = label;
            set.Dir = dir;
            string[] files = Directory.GetFiles(dir);
            Array.Sort(files, StringComparer.Ordinal);
            foreach (string path in files)
            {
                string file = Path.GetFileName(path);
                string ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".meta")
                    continue;
                if (ext != ".wav" && ext != ".ogg" && ext != ".mp3")
                {
                    AdvancedNpcsMod.Log(label + "/" + file + ": not a .wav, .ogg or .mp3 file; ignored");
                    continue;
                }
                string pool = SoundPools.PoolOf(Path.GetFileNameWithoutExtension(path));
                if (pool == null)
                {
                    AdvancedNpcsMod.Log(label + "/" + file + ": not a greet, attack, attack_range, cast, hit, death or step sound; ignored");
                    continue;
                }
                List<string> list;
                if (!set.Files.TryGetValue(pool, out list))
                {
                    list = new List<string>();
                    set.Files[pool] = list;
                }
                list.Add(path);
            }
            if (set.Files.Count == 0)
            {
                AdvancedNpcsMod.Log(label + ": no usable sound files; vanilla sounds kept");
                return null;
            }
            return set;
        }

        /// <summary>The folder's sounds; null means none (vanilla voice).</summary>
        public LoadedSoundSet For(string folder)
        {
            LoadedSoundSet set;
            return folder != null && byFolder.TryGetValue(folder, out set) ? set : null;
        }

        /// <summary>Adds a set to a folder (also used by the self-test).</summary>
        public void Add(string folder, LoadedSoundSet set)
        {
            byFolder[folder] = set;
            set.Library = this;
        }

        /// <summary>Takes a folder's set out of the library; its clips go once nothing uses it.</summary>
        public void Remove(string folder)
        {
            LoadedSoundSet old;
            if (!byFolder.TryGetValue(folder, out old))
                return;
            byFolder.Remove(folder);
            Retire(old);
        }

        /// <summary>Re-indexes one NPC folder from disk (anpc_reload_assets); true when it now has sounds.</summary>
        public bool Reload(string folder, string npcDir)
        {
            Remove(folder);
            LoadedSoundSet fresh = IndexFolder(folder, npcDir);
            if (fresh != null)
                Add(folder, fresh);
            return fresh != null;
        }

        /// <summary>
        /// Out of the library for good. Its clips stay a while after the last user lets go: a death cry from an object of
        /// its own (NpcVoice.PlayDeath) may still be playing one of them.
        /// </summary>
        void Retire(LoadedSoundSet s)
        {
            if (s.Pending != null)
            {
                loading.Remove(s);
                s.Pending = null;
            }
            s.Retired = true;
            s.UnloadAt = Time.unscaledTime + SoundRules.RetireDelay(LongestClip(s));
            retired.Add(s);
        }

        static float LongestClip(LoadedSoundSet s)
        {
            float longest = 0f;
            foreach (List<AudioClip> clips in s.Clips.Values)
                foreach (AudioClip c in clips)
                    if (c != null)
                        longest = Math.Max(longest, c.length);
            return longest;
        }

        /// <summary>One more user; starts loading an indexed set.</summary>
        public void Acquire(LoadedSoundSet s)
        {
            s.Users++;
            s.UnloadAt = -1f;
            if (s.IsReady || s.Pending != null)
                return;
            s.Pending = new Queue<string>();
            foreach (string pool in SoundPools.All)
            {
                List<string> paths;
                if (s.Files.TryGetValue(pool, out paths))
                    foreach (string p in paths)
                        s.Pending.Enqueue(p);
            }
            loading.Add(s);
        }

        /// <summary>One user fewer; the last one starts the unload countdown (for a retired set: as long as its longest clip may still play).</summary>
        public void Release(LoadedSoundSet s)
        {
            if (s.Users > 0)
                s.Users--;
            if (s.Users == 0)
                s.UnloadAt = Time.unscaledTime + (s.Retired ? SoundRules.RetireDelay(LongestClip(s)) : UnloadDelaySeconds);
        }

        /// <summary>Every frame (AdvancedNpcsMod.Update): loads one clip, unloads sets whose countdown ran out.</summary>
        public void Tick(float now)
        {
            if (loading.Count > 0)
            {
                LoadedSoundSet s = loading[0];
                if (s.Pending.Count > 0)
                    LoadNext(s);
                if (s.Pending.Count == 0)
                {
                    loading.RemoveAt(0);
                    s.Pending = null;
                    s.IsReady = true;
                    if (s.Users == 0)
                        s.UnloadAt = Time.unscaledTime + UnloadDelaySeconds;
                }
            }
            foreach (LoadedSoundSet s in byFolder.Values)
                UnloadIfUnused(s, now);
            if (defaultSteps != null)
                UnloadIfUnused(defaultSteps, now);
            for (int i = retired.Count - 1; i >= 0; i--)
            {
                if (retired[i].Users > 0 || now < retired[i].UnloadAt)
                    continue;
                Unload(retired[i], false);      // replaced or removed and no longer used: gone for good
                retired.RemoveAt(i);
            }
        }

        static void UnloadIfUnused(LoadedSoundSet s, float now)
        {
            if (s.Users == 0 && s.UnloadAt >= 0f && now >= s.UnloadAt && s.Pending == null && s.IsReady)
                Unload(s, true);
        }

        static void LoadNext(LoadedSoundSet s)
        {
            string path = s.Pending.Dequeue();
            string problem;
            AudioClip clip = LoadClip(path, out problem);
            if (clip == null)
            {
                AdvancedNpcsMod.LogError(s.Label + "/" + Path.GetFileName(path) + ": " + problem + " (sound skipped)");
                return;
            }
            string pool = SoundPools.PoolOf(Path.GetFileNameWithoutExtension(path));
            List<AudioClip> clips;
            if (!s.Clips.TryGetValue(pool, out clips))
            {
                clips = new List<AudioClip>();
                s.Clips[pool] = clips;
            }
            clips.Add(clip);
        }

        static void Unload(LoadedSoundSet s, bool log)
        {
            foreach (List<AudioClip> clips in s.Clips.Values)
                foreach (AudioClip c in clips)
                    UnityEngine.Object.Destroy(c);
            s.Clips.Clear();
            s.IsReady = false;
            s.UnloadAt = -1f;
            if (log)
                AdvancedNpcsMod.Log(s.Label + ": sounds unloaded (no one uses them).");
        }

        /// <summary>A clip from a .wav, .ogg or .mp3 file (blocking, as DFU's own loose-file sounds); null and a reason on failure.</summary>
        public static AudioClip LoadClip(string path, out string problem)
        {
            problem = null;
            AudioType type = TypeOf(Path.GetExtension(path));
            if (type == AudioType.UNKNOWN)
            {
                problem = "not a .wav, .ogg or .mp3 file";
                return null;
            }
            try
            {
                using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip("file://" + path, type))
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
                        problem = "could not be decoded (an .ogg with a picture or video stream does not load: re-export it as audio only)";
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

        static AudioType TypeOf(string extension)
        {
            switch ((extension ?? "").ToLowerInvariant())
            {
                case ".wav": return AudioType.WAV;
                case ".ogg": return AudioType.OGGVORBIS;
                case ".mp3": return AudioType.MPEG;
                default: return AudioType.UNKNOWN;
            }
        }
    }
}

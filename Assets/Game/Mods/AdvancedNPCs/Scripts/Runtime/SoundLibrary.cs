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
            library.IndexPacks(root);
            AdvancedNpcsMod.Log("Indexed " + library.Count + " sound folder(s), " + library.packs.Count + " voice pack(s)" +
                (library.defaultSteps != null ? " and default sounds (" + library.defaultSteps.Summary() + ")." : "."));
            library.WarnMissingPacks(defs);
            return library;
        }

        readonly Dictionary<string, LoadedSoundSet> packs = new Dictionary<string, LoadedSoundSet>(StringComparer.OrdinalIgnoreCase);
        /// <summary>"folder|va_pack" → the folder's own pools over the pack's (an ANPC with both).</summary>
        readonly Dictionary<string, LoadedSoundSet> merged = new Dictionary<string, LoadedSoundSet>(StringComparer.Ordinal);

        /// <summary>Voice pack names found in ANPCs/_Sounds (anpc_info, self-test).</summary>
        public ICollection<string> Packs
        {
            get { return packs.Keys; }
        }

        /// <summary>(Re-)indexes the voice packs ANPCs/_Sounds/va_*; the old ones, and every merged set, go once unused.</summary>
        public void IndexPacks(string root)
        {
            foreach (LoadedSoundSet old in packs.Values)
                Retire(old);
            packs.Clear();
            foreach (string key in new List<string>(merged.Keys))
                DropMerged(key);
            string dir = Path.Combine(root, SharedFolder);
            if (!Directory.Exists(dir))
                return;
            string[] subs = Directory.GetDirectories(dir);
            Array.Sort(subs, StringComparer.Ordinal);
            foreach (string sub in subs)
            {
                string name = Path.GetFileName(sub);
                if (!SoundRules.IsVoicePackName(name))
                {
                    AdvancedNpcsMod.Log(SharedFolder + "/" + name + ": not a voice pack (its name must start with " + SoundRules.VoicePrefix + "); ignored");
                    continue;
                }
                LoadedSoundSet set = IndexSet(SharedFolder + "/" + name, sub);
                if (set == null)
                    continue;
                set.Library = this;
                packs[name] = set;
            }
        }

        /// <summary>Logs every "voice" pack name an npc.json gives that _Sounds does not have.</summary>
        public void WarnMissingPacks(IEnumerable<NpcDefinition> defs)
        {
            foreach (NpcDefinition def in defs)
                foreach (string voice in def.Sounds.Voices)
                    if (!packs.ContainsKey(voice))
                        AdvancedNpcsMod.Log(def.Folder + ": voice pack " + voice + " not found in " + SharedFolder + "; its own and vanilla sounds play instead");
        }

        /// <summary>
        /// The sounds of a folder that uses a voice pack (null: none or not found): the pack itself, or, when the folder has
        /// a Sounds folder too, its own pools over the pack's (an own pool replaces the pack's whole pool).
        /// </summary>
        public LoadedSoundSet For(string folder, string voice)
        {
            LoadedSoundSet own = For(folder);
            LoadedSoundSet pack;
            if (voice == null || !packs.TryGetValue(voice, out pack))
                return own;
            if (own == null)
                return pack;
            // shortcut: such a folder decodes the pack's files again for itself (pack clips shared only by ANPCs without
            // a Sounds folder); share clips per file if many ANPC types with own sounds use one pack.
            string key = folder + "|" + voice;
            LoadedSoundSet m;
            if (!merged.TryGetValue(key, out m))
            {
                m = new LoadedSoundSet();
                m.Label = own.Label + " + " + pack.Label;
                m.Dir = own.Dir;
                m.Library = this;
                foreach (KeyValuePair<string, List<string>> p in pack.Files)
                    m.Files[p.Key] = p.Value;
                foreach (KeyValuePair<string, List<string>> p in own.Files)
                    m.Files[p.Key] = p.Value;
                merged[key] = m;
            }
            return m;
        }

        void DropMerged(string key)
        {
            LoadedSoundSet m;
            if (!merged.TryGetValue(key, out m))
                return;
            merged.Remove(key);
            Retire(m);
        }

        /// <summary>The merged sets of a folder go: its own sounds changed.</summary>
        void DropMergedOf(string folder)
        {
            foreach (string key in new List<string>(merged.Keys))
                if (key.StartsWith(folder + "|", StringComparison.Ordinal))
                    DropMerged(key);
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

        /// <summary>Re-reads ANPCs/_Sounds and its voice packs (anpc_reload_assets); true when it has default steps.</summary>
        public bool ReloadDefaults(string root)
        {
            SetDefaultSteps(IndexDefaults(root));
            IndexPacks(root);
            return defaultSteps != null;
        }

        /// <summary>ANPCs/_Sounds under root indexed: step, eat and drink files only (others are logged and ignored); null without any.</summary>
        public static LoadedSoundSet IndexDefaults(string root)
        {
            string dir = Path.Combine(root, SharedFolder);
            if (!Directory.Exists(dir))
                return null;
            LoadedSoundSet set = IndexSet(SharedFolder, dir);
            if (set == null)
                return null;
            foreach (string pool in new List<string>(set.Files.Keys))
            {
                if (pool == SoundPools.Step || pool == SoundPools.Eat || pool == SoundPools.Drink)
                    continue;
                AdvancedNpcsMod.Log(SharedFolder + ": " + pool + " sounds are not used as defaults (only step, eat, drink); ignored");
                set.Files.Remove(pool);
            }
            if (!set.Files.ContainsKey(SoundPools.Step))
                AdvancedNpcsMod.Log(SharedFolder + ": no step sounds; ANPCs without their own have no footsteps");
            return set.Files.Count == 0 ? null : set;
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
                    AdvancedNpcsMod.Log(label + "/" + file + ": not a greet, halt, attack, attack_range, cast, heal, hit, death, step, talk or supported cm_ companion sound; ignored");
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
            DropMergedOf(folder);
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
            DropMergedOf(folder);
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
            if (s.Retired && !retired.Contains(s))
                retired.Add(s);         // a retired set taken up again (by an ANPC set up before the swap): freed again once unused
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

        /// <summary>
        /// Every frame (AdvancedNpcsMod.Update): keeps one clip decoding in the background (Unity's audio request, not
        /// waited on), unloads sets whose countdown ran out.
        /// </summary>
        public void Tick(float now)
        {
            if (request != null && request.isDone)
                FinishRequest();
            while (request == null && loading.Count > 0)
            {
                LoadedSoundSet s = loading[0];
                if (s.Pending.Count > 0)
                {
                    StartRequest(s, s.Pending.Dequeue());
                    continue;
                }
                loading.RemoveAt(0);
                s.Pending = null;
                s.IsReady = true;
                if (s.Users == 0)
                    s.UnloadAt = Time.unscaledTime + UnloadDelaySeconds;
            }
            foreach (LoadedSoundSet s in byFolder.Values)
                UnloadIfUnused(s, now);
            foreach (LoadedSoundSet s in packs.Values)
                UnloadIfUnused(s, now);
            foreach (LoadedSoundSet s in merged.Values)
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

        UnityWebRequest request;
        LoadedSoundSet requestSet;
        string requestPath;

        void StartRequest(LoadedSoundSet s, string path)
        {
            string problem;
            UnityWebRequest r = Begin(path, out problem);
            if (r == null)
            {
                AdvancedNpcsMod.LogError(s.Label + "/" + Path.GetFileName(path) + ": " + problem + " (sound skipped)");
                return;
            }
            request = r;
            requestSet = s;
            requestPath = path;
        }

        void FinishRequest()
        {
            UnityWebRequest r = request;
            LoadedSoundSet s = requestSet;
            request = null;
            requestSet = null;
            string problem;
            AudioClip clip = End(r, requestPath, out problem);
            if (s.Pending == null)
            {
                if (clip != null)
                    UnityEngine.Object.Destroy(clip);      // the set was removed or reloaded meanwhile
                return;
            }
            if (clip == null)
            {
                AdvancedNpcsMod.LogError(s.Label + "/" + Path.GetFileName(requestPath) + ": " + problem + " (sound skipped)");
                return;
            }
            string pool = SoundPools.PoolOf(Path.GetFileNameWithoutExtension(requestPath));
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
            UnityWebRequest r = Begin(path, out problem);
            if (r == null)
                return null;
            while (!r.isDone) { }
            return End(r, path, out problem);
        }

        /// <summary>Starts decoding a sound file in the background; null and a reason if it cannot be started.</summary>
        static UnityWebRequest Begin(string path, out string problem)
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
                UnityWebRequest r = UnityWebRequestMultimedia.GetAudioClip(new Uri(Path.GetFullPath(path)).AbsoluteUri, type);
                DownloadHandlerAudioClip handler = r.downloadHandler as DownloadHandlerAudioClip;
                if (handler == null)
                {
                    r.Dispose();
                    problem = "no audio handler";
                    return null;
                }
                handler.streamAudio = false;
                r.SendWebRequest();
                return r;
            }
            catch (Exception e)
            {
                problem = e.Message;
                return null;
            }
        }

        /// <summary>The clip of a finished request (disposed here); null and a reason on failure.</summary>
        static AudioClip End(UnityWebRequest r, string path, out string problem)
        {
            problem = null;
            using (r)
            {
                try
                {
                    if (r.isNetworkError || r.isHttpError)
                    {
                        problem = r.error;
                        return null;
                    }
                    AudioClip clip = ((DownloadHandlerAudioClip)r.downloadHandler).audioClip;
                    if (clip == null || clip.length <= 0f)
                    {
                        problem = "could not be decoded (an .ogg with a picture or video stream does not load: re-export it as audio only)";
                        return null;
                    }
                    clip.name = Path.GetFileName(path);
                    return clip;
                }
                catch (Exception e)
                {
                    problem = e.Message;
                    return null;
                }
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

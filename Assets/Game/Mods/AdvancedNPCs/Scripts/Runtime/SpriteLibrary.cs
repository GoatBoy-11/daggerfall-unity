using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>
    /// One sprite set: its sprites.json and, while some ANPC or corpse uses it, its sheet textures and optional
    /// death_static image (spec lazy-sprites §A).
    /// </summary>
    public class LoadedSpriteSet
    {
        public string Label;
        public SpriteSet Set;
        public readonly Dictionary<string, Texture2D> Sheets = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        public Texture2D DeathStatic;
        /// <summary>Height in pixels of the tallest idle frame above the feet row: the "standing height".</summary>
        public float StandingHeightPx;
        /// <summary>Folder the sheets are read from; null for sets built from textures (self-test), which never unload.</summary>
        public string Dir;
        public bool HasDeathStatic;
        /// <summary>Every sheet (and death_static.png) is loaded and uploaded.</summary>
        public bool IsReady;
        /// <summary>Loading left no usable idle sheet: its ANPCs keep the vanilla sprite.</summary>
        public bool Failed;
        /// <summary>Live ANPC sprites and corpses using the set.</summary>
        public int Users;
        /// <summary>sprites.json had walk* or run* sheets.</summary>
        public bool DeclaredMovement;
        /// <summary>Every declared walk* and run* sheet was skipped: its ANPCs stand still, which has been logged.</summary>
        public bool LostMovement;
        internal SpriteLibrary Library;
        internal float UnloadAt = -1f;
        internal Queue<string> Pending;
        internal float IdleHeight;

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
    }

    /// <summary>
    /// Sprite sets of every NPC folder (spec 1b §5, §8): ANPCs/&lt;npc&gt;/sprites or sprites_&lt;n&gt;. Indexed at start
    /// (sprites.json and sheet sizes only), loaded one image per frame while used, unloaded a while after the last user.
    /// </summary>
    public class SpriteLibrary
    {
        public const string SetFile = "sprites.json";
        public const string DeathStaticFile = "death_static.png";
        /// <summary>Real seconds a set stays loaded after its last user has gone.</summary>
        public static float UnloadDelaySeconds = 60f;
        static readonly List<LoadedSpriteSet> None = new List<LoadedSpriteSet>();

        readonly List<LoadedSpriteSet> loading = new List<LoadedSpriteSet>();
        /// <summary>Sets replaced by a reload that a corpse (or a not yet swapped sprite) still uses.</summary>
        readonly List<LoadedSpriteSet> retired = new List<LoadedSpriteSet>();

        readonly Dictionary<string, List<LoadedSpriteSet>> byFolder = new Dictionary<string, List<LoadedSpriteSet>>(StringComparer.Ordinal);

        public int Count
        {
            get
            {
                int n = 0;
                foreach (List<LoadedSpriteSet> sets in byFolder.Values)
                    n += sets.Count;
                return n;
            }
        }

        public static SpriteLibrary Load(string root, IEnumerable<NpcDefinition> defs)
        {
            SpriteLibrary library = new SpriteLibrary();
            System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
            foreach (NpcDefinition def in defs)
            {
                foreach (LoadedSpriteSet set in IndexFolder(def.Folder, Path.Combine(root, def.Folder)))
                    library.Add(def.Folder, set);
            }
            AdvancedNpcsMod.Log("Indexed " + library.Count + " sprite set(s) in " + watch.ElapsedMilliseconds + " ms.");
            return library;
        }

        /// <summary>Every sprites / sprites_&lt;n&gt; set in an NPC folder on disk, in name order (invalid ones skipped and logged).</summary>
        static List<LoadedSpriteSet> IndexFolder(string folder, string dir)
        {
            List<LoadedSpriteSet> sets = new List<LoadedSpriteSet>();
            if (!Directory.Exists(dir))
                return sets;
            List<string> setDirs = new List<string>();
            foreach (string sub in Directory.GetDirectories(dir))
            {
                if (IsSetFolder(Path.GetFileName(sub)))
                    setDirs.Add(sub);
            }
            setDirs.Sort(StringComparer.Ordinal);
            foreach (string setDir in setDirs)
            {
                LoadedSpriteSet set = IndexSet(folder + "/" + Path.GetFileName(setDir), setDir);
                if (set != null)
                    sets.Add(set);
            }
            return sets;
        }

        /// <summary>
        /// Re-indexes one NPC folder from disk (anpc_reload_sprites, spec tools-quality §A3) and returns how many valid
        /// sets it now has. The old sets leave the library; their textures go once nothing uses them.
        /// </summary>
        public int Reload(string folder, string dir)
        {
            Remove(folder);
            List<LoadedSpriteSet> fresh = IndexFolder(folder, dir);
            foreach (LoadedSpriteSet s in fresh)
                Add(folder, s);
            return fresh.Count;
        }

        /// <summary>The sets of an NPC folder; empty means the vanilla class sprite.</summary>
        public List<LoadedSpriteSet> For(string folder)
        {
            List<LoadedSpriteSet> sets;
            return folder != null && byFolder.TryGetValue(folder, out sets) ? sets : None;
        }

        /// <summary>Adds a set to a folder (also used by the self-test).</summary>
        public void Add(string folder, LoadedSpriteSet set)
        {
            List<LoadedSpriteSet> sets;
            if (!byFolder.TryGetValue(folder, out sets))
            {
                sets = new List<LoadedSpriteSet>();
                byFolder[folder] = sets;
            }
            sets.Add(set);
            if (set.Dir != null)
                set.Library = this;
        }

        /// <summary>
        /// Takes a folder's sets out of the library; the textures of those read from disk go once nothing uses them
        /// (sets built from textures belong to whoever built them).
        /// </summary>
        public void Remove(string folder)
        {
            List<LoadedSpriteSet> old;
            if (!byFolder.TryGetValue(folder, out old))
                return;
            byFolder.Remove(folder);
            foreach (LoadedSpriteSet s in old)
            {
                if (s.Library != this)
                    continue;
                if (s.Pending != null)
                {
                    loading.Remove(s);
                    s.Pending = null;
                    s.Failed = true;        // stops waiting sprites; a reloaded set replaces it
                }
                retired.Add(s);
            }
        }

        /// <summary>A set from readable textures already in memory (self-test).</summary>
        public static LoadedSpriteSet FromTextures(string label, SpriteSet set, Dictionary<string, Texture2D> sheets, Texture2D deathStatic)
        {
            LoadedSpriteSet loaded = new LoadedSpriteSet();
            loaded.Label = label;
            loaded.Set = set;
            foreach (KeyValuePair<string, Texture2D> kv in sheets)
                loaded.Sheets[kv.Key] = kv.Value;
            loaded.DeathStatic = deathStatic;
            loaded.StandingHeightPx = StandingHeight(set, loaded.Sheets);
            loaded.IsReady = true;
            return loaded;
        }

        static bool IsSetFolder(string name)
        {
            if (name == "sprites")
                return true;
            if (!name.StartsWith("sprites_", StringComparison.Ordinal) || name.Length == "sprites_".Length)
                return false;
            for (int i = "sprites_".Length; i < name.Length; i++)
            {
                if (name[i] < '0' || name[i] > '9')
                    return false;
            }
            return true;
        }

        /// <summary>Reads sprites.json and checks every sheet's size from its PNG header; loads no image.</summary>
        public static LoadedSpriteSet IndexSet(string label, string dir)
        {
            string jsonPath = Path.Combine(dir, SetFile);
            if (!File.Exists(jsonPath))
            {
                AdvancedNpcsMod.LogError(label + "/" + SetFile + ": file: missing, sprite set skipped");
                return null;
            }
            SpriteSetResult parsed;
            try
            {
                parsed = SpriteSetParser.Parse(label + "/" + SetFile, File.ReadAllText(jsonPath));
            }
            catch (Exception e)
            {
                AdvancedNpcsMod.LogError(label + "/" + SetFile + ": file: could not read (" + e.Message + ")");
                return null;
            }
            if (parsed.Error != null)
            {
                AdvancedNpcsMod.LogError(parsed.Error + " (sprite set skipped)");
                return null;
            }

            SpriteSet set = parsed.Set;
            LoadedSpriteSet loaded = new LoadedSpriteSet();
            loaded.Label = label;
            loaded.Set = set;
            loaded.DeclaredMovement = !SpriteStates.IsStationary(set);
            List<string> skipped = new List<string>();
            foreach (SpriteAnimation a in set.Animations.Values)
            {
                if (SpriteStates.StateOf(a.Name) == null)
                {
                    AdvancedNpcsMod.Log(label + "/" + a.Name + ".png: not an idle*, walk*, run*, hit*, attack*, attack_range*, cast*, greet* or death* sheet; not loaded");
                    skipped.Add(a.Name);
                    continue;
                }
                int w, h;
                string headerProblem = ReadSize(Path.Combine(dir, a.Name + ".png"), out w, out h);
                if (headerProblem != null)
                {
                    AdvancedNpcsMod.LogError(label + "/" + a.Name + ".png: file: " + headerProblem + " (sheet skipped)");
                    skipped.Add(a.Name);
                    continue;
                }
                string sizeError = SpriteSetParser.CheckSheetSize(label, a, set.CellHeight, w, h);
                if (sizeError != null)
                {
                    AdvancedNpcsMod.LogError(sizeError + " (sheet skipped)");
                    skipped.Add(a.Name);
                    continue;
                }
            }
            foreach (string name in skipped)
                set.Animations.Remove(name);
            if (!SpriteStates.IsValid(set))
            {
                AdvancedNpcsMod.LogError(label + ": no usable idle sheet; sprite set skipped");
                return null;
            }
            NoteLostMovement(loaded);
            loaded.Dir = dir;
            loaded.HasDeathStatic = File.Exists(Path.Combine(dir, DeathStaticFile));
            return loaded;
        }

        /// <summary>Null and the size, or what is wrong with the file.</summary>
        static string ReadSize(string path, out int width, out int height)
        {
            width = height = 0;
            if (!File.Exists(path))
                return "missing";
            try
            {
                byte[] head = new byte[PngHeader.Length];
                using (FileStream f = File.OpenRead(path))
                {
                    if (f.Read(head, 0, head.Length) != head.Length)
                        return "not a readable PNG";
                }
                return PngHeader.TryReadSize(head, out width, out height) ? null : "not a readable PNG";
            }
            catch (Exception e)
            {
                return "could not read (" + e.Message + ")";
            }
        }

        /// <summary>One more user; starts loading an indexed set.</summary>
        public void Acquire(LoadedSpriteSet s)
        {
            s.Users++;
            s.UnloadAt = -1f;
            if (s.IsReady || s.Failed || s.Pending != null)
                return;
            s.Pending = new Queue<string>();
            List<string> names = new List<string>(s.Set.Animations.Keys);
            names.Sort(StringComparer.Ordinal);
            foreach (string n in names)
                s.Pending.Enqueue(n);
            if (s.HasDeathStatic)
                s.Pending.Enqueue(DeathStaticFile);
            s.IdleHeight = 0f;
            loading.Add(s);
        }

        /// <summary>One user fewer; the last one starts the unload countdown.</summary>
        public void Release(LoadedSpriteSet s)
        {
            if (s.Users > 0)
                s.Users--;
            if (s.Users == 0 && s.Dir != null)
                s.UnloadAt = Time.unscaledTime + UnloadDelaySeconds;
        }

        /// <summary>Every frame (AdvancedNpcsMod.Update): loads one image, unloads sets whose countdown ran out.</summary>
        public void Tick(float now)
        {
            if (loading.Count > 0)
            {
                LoadedSpriteSet s = loading[0];
                if (s.Pending.Count > 0)
                    LoadNext(s);
                if (s.Pending.Count == 0)
                {
                    loading.RemoveAt(0);
                    FinishLoading(s);
                }
            }
            foreach (List<LoadedSpriteSet> sets in byFolder.Values)
            {
                foreach (LoadedSpriteSet s in sets)
                {
                    if (s.Users == 0 && s.UnloadAt >= 0f && now >= s.UnloadAt && s.Pending == null && s.IsReady)
                        Unload(s);
                }
            }
            for (int i = retired.Count - 1; i >= 0; i--)
            {
                LoadedSpriteSet s = retired[i];
                if (s.Users > 0)
                    continue;
                DestroyTextures(s);         // replaced by a reload and no longer used: gone for good
                s.IsReady = false;
                retired.RemoveAt(i);
            }
        }

        void LoadNext(LoadedSpriteSet s)
        {
            string name = s.Pending.Dequeue();
            if (name == DeathStaticFile)
            {
                s.DeathStatic = LoadPng(s.Label + "/" + DeathStaticFile, Path.Combine(s.Dir, DeathStaticFile), false);
                if (s.DeathStatic != null)
                    Finish(s.DeathStatic);
                return;
            }
            SpriteAnimation a;
            if (!s.Set.Animations.TryGetValue(name, out a))
                return;
            Texture2D t = LoadPng(s.Label + "/" + name + ".png", Path.Combine(s.Dir, name + ".png"), true);
            string sizeError = t != null ? SpriteSetParser.CheckSheetSize(s.Label, a, s.Set.CellHeight, t.width, t.height) : null;
            if (t == null || sizeError != null)
            {
                if (sizeError != null)
                    AdvancedNpcsMod.LogError(sizeError + " (sheet skipped)");
                if (t != null)
                    UnityEngine.Object.Destroy(t);
                s.Set.Animations.Remove(name);
                return;
            }
            if (s.Set.StandingHeight <= 0 && SpriteStates.StateOf(name) == SpriteStates.Idle)
                s.IdleHeight = Math.Max(s.IdleHeight, IdleHeight(s.Set, a, t));
            Finish(t);
            s.Sheets[name] = t;
        }

        void FinishLoading(LoadedSpriteSet s)
        {
            s.Pending = null;
            if (!SpriteStates.IsValid(s.Set))
            {
                AdvancedNpcsMod.LogError(s.Label + ": no usable idle sheet; its ANPCs keep the vanilla sprite");
                DestroyTextures(s);
                s.Failed = true;
                return;
            }
            NoteLostMovement(s);
            if (s.Set.StandingHeight > 0)
                s.StandingHeightPx = s.Set.StandingHeight;
            else
                s.StandingHeightPx = s.IdleHeight > 0 ? s.IdleHeight : s.Set.CellHeight - s.Set.GroundY;
            s.IsReady = true;
            if (s.Users == 0)
                s.UnloadAt = Time.unscaledTime + UnloadDelaySeconds;
        }

        /// <summary>
        /// Logs, once, a set whose walk* and run* sheets were all skipped (at indexing or while loading): its ANPCs will
        /// stand still (spec tools-quality B2.7). A set that never had them is a turret on purpose and is not logged.
        /// </summary>
        static void NoteLostMovement(LoadedSpriteSet s)
        {
            if (s.LostMovement || !SpriteStates.LostMovement(s.DeclaredMovement, s.Set))
                return;
            s.LostMovement = true;
            AdvancedNpcsMod.LogError(s.Label + ": every walk* and run* sheet was skipped; its ANPCs will stand still (turrets)");
        }

        void Unload(LoadedSpriteSet s)
        {
            DestroyTextures(s);
            s.IsReady = false;
            s.UnloadAt = -1f;
            AdvancedNpcsMod.Log(s.Label + ": sprite set unloaded (no one uses it).");
        }

        static void DestroyTextures(LoadedSpriteSet s)
        {
            foreach (Texture2D t in s.Sheets.Values)
                UnityEngine.Object.Destroy(t);
            s.Sheets.Clear();
            if (s.DeathStatic != null)
                UnityEngine.Object.Destroy(s.DeathStatic);
            s.DeathStatic = null;
        }

        static Texture2D LoadPng(string label, string path, bool required)
        {
            if (!File.Exists(path))
            {
                if (required)
                    AdvancedNpcsMod.LogError(label + ": file: missing (sheet skipped)");
                return null;
            }
            try
            {
                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (texture.LoadImage(File.ReadAllBytes(path)))
                {
                    texture.name = label;
                    return texture;
                }
                UnityEngine.Object.Destroy(texture);
                if (required)
                    AdvancedNpcsMod.LogError(label + ": file: not a readable PNG (sheet skipped)");
                else
                    AdvancedNpcsMod.Log(label + ": file: not a readable PNG; using the last death frame");
            }
            catch (Exception e)
            {
                AdvancedNpcsMod.LogError(label + ": file: could not read (" + e.Message + ")");
            }
            return null;
        }

        /// <summary>DFU's filter setting, no wrapping, compressed and released from CPU memory.</summary>
        static void Finish(Texture2D texture)
        {
            NpcSprite.Filtered(texture);
            texture.wrapMode = TextureWrapMode.Clamp;
            if (texture.width % 4 == 0 && texture.height % 4 == 0)
                texture.Compress(true);
            texture.Apply(false, true);
        }

        /// <summary>Tallest opaque pixel above the feet row over every frame and direction of the idle sheets.</summary>
        static float StandingHeight(SpriteSet set, Dictionary<string, Texture2D> sheets)
        {
            if (set.StandingHeight > 0)
                return set.StandingHeight;   // given by the renderer: the body alone, without a raised weapon
            float best = 0f;
            foreach (SpriteAnimation a in SpriteStates.Variants(set, SpriteStates.Idle))
            {
                Texture2D t;
                if (sheets.TryGetValue(a.Name, out t))
                    best = Math.Max(best, IdleHeight(set, a, t));
            }
            return best > 0 ? best : set.CellHeight - set.GroundY;
        }

        /// <summary>Tallest opaque pixel above the feet row in one idle sheet (0 if none); the texture must be readable.</summary>
        static float IdleHeight(SpriteSet set, SpriteAnimation a, Texture2D t)
        {
            int best = 0;
            Color32[] px = t.GetPixels32();
            int w = t.width;
            for (int row = 0; row < SpriteSetParser.Directions.Length; row++)
            {
                int cellBottom = (SpriteSetParser.Directions.Length - 1 - row) * set.CellHeight;   // texture rows start at the bottom
                for (int frame = 0; frame < a.Frames; frame++)
                {
                    int x0 = frame * a.CellWidth;
                    for (int y = set.CellHeight - 1; y > set.GroundY + best; y--)
                    {
                        int line = (cellBottom + y) * w;
                        bool opaque = false;
                        for (int x = x0; x < x0 + a.CellWidth; x++)
                        {
                            if (px[line + x].a > 16)
                            {
                                opaque = true;
                                break;
                            }
                        }
                        if (opaque)
                        {
                            best = Math.Max(best, y + 1 - set.GroundY);
                            break;
                        }
                    }
                }
            }
            return best;
        }
    }
}

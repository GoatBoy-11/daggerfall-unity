using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>One loaded sprite set: its sprites.json, sheet textures and optional death_static image.</summary>
    public class LoadedSpriteSet
    {
        public string Label;
        public SpriteSet Set;
        public readonly Dictionary<string, Texture2D> Sheets = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        public Texture2D DeathStatic;
        /// <summary>Height in pixels of the tallest idle frame above the feet row: the "standing height".</summary>
        public float StandingHeightPx;
    }

    /// <summary>
    /// Sprite sets of every NPC folder (spec 1b §5, §8): ANPCs/&lt;npc&gt;/sprites or sprites_&lt;n&gt;. Loaded once at start and
    /// shared by every person of that folder.
    /// </summary>
    public class SpriteLibrary
    {
        public const string SetFile = "sprites.json";
        public const string DeathStaticFile = "death_static.png";
        static readonly List<LoadedSpriteSet> None = new List<LoadedSpriteSet>();

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
            foreach (NpcDefinition def in defs)
            {
                string dir = Path.Combine(root, def.Folder);
                if (!Directory.Exists(dir))
                    continue;
                List<string> setDirs = new List<string>();
                foreach (string sub in Directory.GetDirectories(dir))
                {
                    if (IsSetFolder(Path.GetFileName(sub)))
                        setDirs.Add(sub);
                }
                setDirs.Sort(StringComparer.Ordinal);
                foreach (string setDir in setDirs)
                {
                    LoadedSpriteSet set = LoadSet(def.Folder + "/" + Path.GetFileName(setDir), setDir);
                    if (set != null)
                        library.Add(def.Folder, set);
                }
            }
            AdvancedNpcsMod.Log("Loaded " + library.Count + " sprite set(s).");
            return library;
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
        }

        public void Remove(string folder)
        {
            byFolder.Remove(folder);
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

        static LoadedSpriteSet LoadSet(string label, string dir)
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
            List<string> skipped = new List<string>();
            foreach (SpriteAnimation a in set.Animations.Values)
            {
                Texture2D texture = LoadPng(label + "/" + a.Name + ".png", Path.Combine(dir, a.Name + ".png"), true);
                if (texture == null)
                {
                    skipped.Add(a.Name);
                    continue;
                }
                string sizeError = SpriteSetParser.CheckSheetSize(label, a, set.CellHeight, texture.width, texture.height);
                if (sizeError != null)
                {
                    AdvancedNpcsMod.LogError(sizeError + " (sheet skipped)");
                    UnityEngine.Object.Destroy(texture);
                    skipped.Add(a.Name);
                    continue;
                }
                loaded.Sheets[a.Name] = texture;
            }
            foreach (string name in skipped)
                set.Animations.Remove(name);
            if (!SpriteStates.IsValid(set))
            {
                AdvancedNpcsMod.LogError(label + ": no usable idle sheet; sprite set skipped");
                foreach (Texture2D t in loaded.Sheets.Values)
                    UnityEngine.Object.Destroy(t);
                return null;
            }

            string staticPath = Path.Combine(dir, DeathStaticFile);
            if (File.Exists(staticPath))
                loaded.DeathStatic = LoadPng(label + "/" + DeathStaticFile, staticPath, false);

            loaded.StandingHeightPx = StandingHeight(set, loaded.Sheets);
            foreach (Texture2D t in loaded.Sheets.Values)
                Finish(t);
            if (loaded.DeathStatic != null)
                Finish(loaded.DeathStatic);
            return loaded;
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
            int best = 0;
            foreach (SpriteAnimation a in SpriteStates.Variants(set, SpriteStates.Idle))
            {
                Texture2D t;
                if (!sheets.TryGetValue(a.Name, out t))
                    continue;
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
            }
            return best > 0 ? best : set.CellHeight - set.GroundY;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>The shared portraits in ANPCs/_Portraits, loaded once at start and looked up by name (spec §10).</summary>
    public class PortraitLibrary
    {
        readonly Dictionary<string, Texture2D> byName = new Dictionary<string, Texture2D>(StringComparer.Ordinal);

        public int Count
        {
            get { return byName.Count; }
        }

        /// <summary>Loaded portrait names (normalised), for pools.</summary>
        public ICollection<string> Names
        {
            get { return byName.Keys; }
        }

        /// <summary>Adds or replaces a portrait (self-test).</summary>
        public void Add(string name, Texture2D texture)
        {
            byName[PortraitNames.Normalize(name)] = texture;
        }

        public void Remove(string name)
        {
            byName.Remove(PortraitNames.Normalize(name));
        }

        public static PortraitLibrary Load(string folder)
        {
            PortraitLibrary library = new PortraitLibrary();
            if (!Directory.Exists(folder))
                return library;
            foreach (string path in AnpcFiles.FilesWithExtension(folder, ".png"))
            {
                string file = AnpcFiles.PortraitsName + "/" + Path.GetFileName(path);
                try
                {
                    Texture2D texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                    if (!texture.LoadImage(File.ReadAllBytes(path)))
                    {
                        AdvancedNpcsMod.LogError(file + ": file: not a readable PNG");
                        continue;
                    }
                    string name = PortraitNames.Normalize(Path.GetFileName(path));
                    texture.filterMode = FilterMode.Point;
                    texture.name = name;
                    library.byName[name] = texture;
                }
                catch (Exception e)
                {
                    AdvancedNpcsMod.LogError(file + ": file: could not read (" + e.Message + ")");
                }
            }
            AdvancedNpcsMod.Log("Loaded " + library.Count + " portrait(s).");
            return library;
        }

        /// <summary>The portrait with this name (any case, with or without .png), or null.</summary>
        public Texture2D Get(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            Texture2D texture;
            return byName.TryGetValue(PortraitNames.Normalize(name), out texture) ? texture : null;
        }

        /// <summary>One warning per referenced portrait that has no PNG; those ANPCs use a vanilla face.</summary>
        public void WarnMissing(IEnumerable<string> referenced)
        {
            foreach (string name in referenced)
            {
                if (PortraitPools.Pool(byName.Keys, name).Count == 0)
                    AdvancedNpcsMod.Log(AnpcFiles.PortraitsName + "/" + name + ".png: file: portrait not found; using a vanilla face");
            }
        }
    }
}

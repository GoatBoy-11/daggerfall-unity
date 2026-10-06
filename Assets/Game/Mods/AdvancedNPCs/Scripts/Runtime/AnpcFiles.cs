using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>All file access for ANPC data under StreamingAssets/ANPCs (spec §6).</summary>
    public static class AnpcFiles
    {
        public const string RootName = "ANPCs";
        public const string PortraitsName = "_Portraits";
        public const string NpcFile = "npc.json";
        public const string DialogueFile = "dialogue.json";

        public static string Root
        {
            get { return Path.Combine(Application.streamingAssetsPath, RootName); }
        }

        public static string PortraitsFolder
        {
            get { return Path.Combine(Root, PortraitsName); }
        }

        public static string NpcJsonPath(string folder)
        {
            return Path.Combine(Path.Combine(Root, folder), NpcFile);
        }

        /// <summary>Every ANPC folder (shared "_" folders excluded) with its npc.json and dialogue.json text.</summary>
        public static List<AnpcFolder> ReadFolders()
        {
            List<AnpcFolder> result = new List<AnpcFolder>();
            if (!Directory.Exists(Root))
            {
                AdvancedNpcsMod.Log("No " + RootName + " folder at " + Root + "; nothing to spawn.");
                return result;
            }
            foreach (string dir in Directory.GetDirectories(Root))
            {
                string name = Path.GetFileName(dir);
                if (name.StartsWith("_", StringComparison.Ordinal))
                    continue;
                result.Add(new AnpcFolder(name, ReadOptional(dir, NpcFile, name), ReadOptional(dir, DialogueFile, name)));
            }
            return result;
        }

        /// <summary>Rewrites location and position in a unique ANPC's npc.json (anpc_place).</summary>
        public static void SetPlacement(NpcDefinition def, string region, string place, float x, float y, float z)
        {
            string path = NpcJsonPath(def.Folder);
            File.WriteAllText(path, DefinitionEditor.SetPlacement(File.ReadAllText(path), region, place, x, y, z));
        }

        static string ReadOptional(string dir, string file, string folder)
        {
            string path = Path.Combine(dir, file);
            if (!File.Exists(path))
                return null;
            try
            {
                return File.ReadAllText(path);
            }
            catch (Exception e)
            {
                AdvancedNpcsMod.LogError(folder + "/" + file + ": file: could not read (" + e.Message + ")");
                return null;
            }
        }
    }
}

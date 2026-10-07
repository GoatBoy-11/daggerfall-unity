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

        /// <summary>The 1a definition folder, read only to migrate it.</summary>
        public const string LegacyName = "AdvancedNPCs";

        public static string Root
        {
            get { return Path.Combine(Application.streamingAssetsPath, RootName); }
        }

        public static string PortraitsFolder
        {
            get { return Path.Combine(Root, PortraitsName); }
        }

        /// <summary>Files of a folder with an extension (".png"), in any case: "*.png" misses ".PNG" on Linux.</summary>
        public static string[] FilesWithExtension(string folder, string extension)
        {
            List<string> found = new List<string>();
            foreach (string path in Directory.GetFiles(folder))
            {
                if (string.Equals(Path.GetExtension(path), extension, StringComparison.OrdinalIgnoreCase))
                    found.Add(path);
            }
            return found.ToArray();
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

        /// <summary>
        /// Moves 1a files StreamingAssets/AdvancedNPCs/*.json into ANPCs/&lt;id&gt;/npc.json (spec §11). Never
        /// overwrites; a migrated file is renamed to *.json.migrated, never deleted.
        /// </summary>
        public static void MigrateLegacy()
        {
            string legacy = Path.Combine(Application.streamingAssetsPath, LegacyName);
            if (!Directory.Exists(legacy))
                return;
            string[] files = FilesWithExtension(legacy, ".json");
            if (files.Length == 0)
                return;
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            int migrated = 0;
            foreach (string path in files)
            {
                string name = Path.GetFileName(path);
                try
                {
                    MigrationResult m = Migration.Convert(name, File.ReadAllText(path));
                    if (!m.Ok)
                    {
                        AdvancedNpcsMod.LogError(LegacyName + "/" + m.Error + " (not migrated)");
                        continue;
                    }
                    string target = Path.Combine(Root, m.Id);
                    if (Directory.Exists(target))
                    {
                        AdvancedNpcsMod.Log(LegacyName + "/" + name + ": not migrated, " + RootName + "/" + m.Id + " already exists");
                        continue;
                    }
                    Directory.CreateDirectory(target);
                    File.WriteAllText(Path.Combine(target, NpcFile), m.NpcJson);
                    File.Move(path, path + ".migrated");
                    migrated++;
                    AdvancedNpcsMod.Log(LegacyName + "/" + name + " -> " + RootName + "/" + m.Id + "/" + NpcFile);
                }
                catch (Exception e)
                {
                    AdvancedNpcsMod.LogError(LegacyName + "/" + name + ": migration failed (" + e.Message + ")");
                }
            }
            AdvancedNpcsMod.Log("Migrated " + migrated + " definition(s) to " + RootName + "/.");
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

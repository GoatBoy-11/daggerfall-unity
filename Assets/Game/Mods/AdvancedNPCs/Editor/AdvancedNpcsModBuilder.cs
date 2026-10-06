using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AdvancedNPCs.EditorTools
{
    /// <summary>
    /// Builds advancednpcs.dfmod the same way Daggerfall Tools > Mod Builder does (C# sources packed as
    /// .cs.txt text assets plus the .dfmod.json manifest, LZ4, Windows), but without the GUI so it can run
    /// in batch mode:
    ///   Unity.exe -batchmode -quit -projectPath &lt;fork&gt; -executeMethod AdvancedNPCs.EditorTools.AdvancedNpcsModBuilder.Build [-modOut &lt;dir&gt;]
    /// Output: &lt;modOut&gt;/StandaloneWindows/advancednpcs.dfmod (default modOut: &lt;project&gt;/Builds/AdvancedNPCs).
    /// </summary>
    public static class AdvancedNpcsModBuilder
    {
        const string ManifestPath = "Assets/Game/Mods/AdvancedNPCs/AdvancedNPCs.dfmod.json";
        const string TempDir = "Assets/Untracked/ModBuilder/AdvancedNPCs";
        const string BundleName = "advancednpcs.dfmod";
        const string LogPrefix = "[AdvancedNPCs build] ";

        [Serializable]
        class ManifestFiles
        {
            public List<string> Files = new List<string>();
        }

        [MenuItem("Daggerfall Tools/Build Advanced NPCs mod")]
        public static void BuildFromMenu()
        {
            Run(DefaultOutDir());
        }

        /// <summary>Batch-mode entry point. Exits Unity with 0 on success, 1 on failure.</summary>
        public static void Build()
        {
            bool ok = false;
            try
            {
                ok = Run(ArgValue("-modOut", DefaultOutDir()));
            }
            catch (Exception e)
            {
                Debug.LogError(LogPrefix + e);
            }
            EditorApplication.Exit(ok ? 0 : 1);
        }

        static bool Run(string outDir)
        {
            ManifestFiles manifest = JsonUtility.FromJson<ManifestFiles>(File.ReadAllText(ManifestPath));
            if (manifest.Files.Count == 0)
            {
                Debug.LogError(LogPrefix + "manifest lists no files");
                return false;
            }

            if (AssetDatabase.IsValidFolder(TempDir))
                AssetDatabase.DeleteAsset(TempDir);
            Directory.CreateDirectory(TempDir);
            AssetDatabase.Refresh();

            List<string> assets = new List<string>();
            foreach (string path in manifest.Files)
            {
                if (!File.Exists(path))
                {
                    Debug.LogError(LogPrefix + "missing file " + path);
                    return false;
                }
                if (path.EndsWith(".cs", StringComparison.Ordinal))
                {
                    // DFU compiles sources it finds as *.cs.txt text assets in the bundle.
                    string copy = TempDir + "/" + Path.GetFileName(path) + ".txt";
                    if (!AssetDatabase.CopyAsset(path, copy))
                    {
                        Debug.LogError(LogPrefix + "could not copy " + path);
                        return false;
                    }
                    assets.Add(copy);
                }
                else
                {
                    assets.Add(path);
                }
            }
            assets.Add(ManifestPath);

            AssetBundleBuild[] map = new AssetBundleBuild[1];
            map[0].assetBundleName = BundleName;
            map[0].assetBundleVariant = "";
            map[0].assetNames = assets.ToArray();

            string targetDir = Path.Combine(outDir, BuildTarget.StandaloneWindows.ToString());
            Directory.CreateDirectory(targetDir);
            AssetBundleManifest result = BuildPipeline.BuildAssetBundles(targetDir, map,
                BuildAssetBundleOptions.ChunkBasedCompression, BuildTarget.StandaloneWindows);
            if (result == null)
            {
                Debug.LogError(LogPrefix + "BuildAssetBundles failed");
                return false;
            }

            Debug.Log(LogPrefix + "built " + Path.Combine(targetDir, BundleName) + " (" + assets.Count + " assets)");
            return true;
        }

        static string DefaultOutDir()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/AdvancedNPCs"));
        }

        static string ArgValue(string name, string fallback)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                    return args[i + 1];
            }
            return fallback;
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Profiling;
using DaggerfallWorkshop;

namespace AdvancedNPCs
{
    /// <summary>
    /// Self-test "spriteload": loads the biggest installed animal sets at once, as a wildlife group spawn does, and logs
    /// how smooth the game stayed (worst frame, slow frames, garbage collections, worst SpriteLibrary.Tick).
    /// </summary>
    public partial class SelfTest
    {
        public const string SpriteLoadSuite = "spriteload";

        static readonly string[] LoadSets = { "an_viper", "an_crocodile", "an_fire_salamander", "an_grey_wolf" };

        IEnumerator SpriteLoadSteps(DaggerfallLocation location, Transform player)
        {
            List<string> folders = new List<string>();
            List<LoadedSpriteSet> sets = new List<LoadedSpriteSet>();
            foreach (string id in LoadSets)
            {
                string dir = Path.Combine(Path.Combine(Path.Combine(Application.streamingAssetsPath, "ANPCs"), id), "sprites");
                LoadedSpriteSet s = Directory.Exists(dir) ? SpriteLibrary.IndexSet(id + "/sprites", dir) : null;
                if (s == null)
                    continue;
                string folder = "selftest_load_" + id;
                mod.Sprites.Add(folder, s);
                folders.Add(folder);
                sets.Add(s);
            }
            if (sets.Count == 0)
            {
                Fail("sprite load timing", "none of the animal sets is installed");
                yield break;
            }

            // Baseline: the same scene without loading.
            float baseWorst = 0f;
            for (int i = 0; i < 120; i++)
            {
                yield return null;
                baseWorst = Math.Max(baseWorst, Time.unscaledDeltaTime);
            }

            int gcBefore = GC.CollectionCount(0);
            long heapBefore = Profiler.GetMonoHeapSizeLong();
            SpriteLibrary.MaxTickMs = 0;
            float start = Time.realtimeSinceStartup;
            foreach (LoadedSpriteSet s in sets)
                s.Acquire();
            float worst = 0f;
            int slow = 0, frames = 0;
            while (Time.realtimeSinceStartup - start < 90f && sets.Exists(delegate (LoadedSpriteSet s) { return !s.IsReady && !s.Failed; }))
            {
                yield return null;
                frames++;
                worst = Math.Max(worst, Time.unscaledDeltaTime);
                if (Time.unscaledDeltaTime > 0.05f)
                    slow++;
            }
            float seconds = Time.realtimeSinceStartup - start;
            int gcs = GC.CollectionCount(0) - gcBefore;
            long heapGrowth = Profiler.GetMonoHeapSizeLong() - heapBefore;
            bool ready = sets.TrueForAll(delegate (LoadedSpriteSet s) { return s.IsReady; });
            int sheets = 0;
            foreach (LoadedSpriteSet s in sets)
                sheets += s.Sheets.Count;

            foreach (LoadedSpriteSet s in sets)
                s.Release();
            foreach (string f in folders)
                mod.Sprites.Remove(f);

            string detail = sets.Count + " sets, " + sheets + " sheets in " + seconds.ToString("0.0") + " s over " + frames + " frames; worst frame " +
                (worst * 1000f).ToString("0") + " ms (baseline " + (baseWorst * 1000f).ToString("0") + " ms); frames over 50 ms: " + slow +
                "; garbage collections: " + gcs + "; mono heap growth " + (heapGrowth >> 20) + " MB; worst sprite Tick " +
                SpriteLibrary.MaxTickMs.ToString("0.0") + " ms";
            AdvancedNpcsMod.Log(Prefix + "TIMING " + detail);
            Check("the biggest animal sets load (timing logged)", ready, detail);
        }
    }
}

using System.Collections;
using System.IO;
using UnityEngine;
using DaggerfallWorkshop;

namespace AdvancedNPCs
{
    /// <summary>Self-test: custom ANPC sounds (spec sounds).</summary>
    public partial class SelfTest
    {
        /// <summary>
        /// Developer probe (only when ANPCs/_probe_sounds exists): which audio formats Unity decodes at runtime here.
        /// </summary>
        IEnumerator SoundProbeSteps()
        {
            string dir = Path.Combine(AnpcFiles.Root, "_probe_sounds");
            if (!Directory.Exists(dir))
                yield break;
            foreach (string file in Directory.GetFiles(dir))
            {
                string problem;
                AudioClip clip = SoundLibrary.LoadClip(file, out problem);
                AdvancedNpcsMod.Log("selftest: sound probe " + Path.GetFileName(file) + ": " +
                    (clip != null ? "loaded, " + clip.length.ToString("0.000") + " s, " + clip.frequency + " Hz" : "FAILED (" + problem + ")"));
                if (clip != null)
                    Object.Destroy(clip);
                yield return null;
            }
        }
    }
}

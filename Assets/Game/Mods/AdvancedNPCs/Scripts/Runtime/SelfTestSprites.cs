using System.Collections;
using System.IO;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>Self-test: sprite sets load while used and unload after (spec lazy-sprites §A).</summary>
    public partial class SelfTest
    {
        IEnumerator LazySpriteSteps(DaggerfallLocation location, Transform player)
        {
            string dir = Path.Combine(Application.temporaryCachePath, "anpc_selftest_lazy/sprites");
            Directory.CreateDirectory(dir);
            foreach (string n in new[] { "idle", "walk", "death" })
                File.WriteAllBytes(Path.Combine(dir, n + ".png"), SolidTexture(32, 32 * 8, new Color32(90, 160, 90, 255)).EncodeToPNG());
            File.WriteAllText(Path.Combine(dir, SpriteLibrary.SetFile),
                "{ \"pixelsPerUnit\": 10, \"cellHeight\": 32, \"groundY\": 4, \"fps\": 8, \"anchorX\": \"centre\", " +
                "\"animations\": { \"idle\": { \"cellWidth\": 16, \"frames\": 2 }, \"walk\": { \"cellWidth\": 16, \"frames\": 2 }, " +
                "\"death\": { \"cellWidth\": 16, \"frames\": 2 } } }");
            LoadedSpriteSet lazy = SpriteLibrary.IndexSet("selftest_lazy/sprites", dir);
            Check("a sprite set is indexed without loading its sheets", lazy != null && !lazy.IsReady && lazy.Sheets.Count == 0,
                lazy == null ? "not indexed" : "ready=" + lazy.IsReady + ", sheets=" + lazy.Sheets.Count);
            if (lazy == null)
                yield break;
            mod.Sprites.Add("selftest_lazy", lazy);
            float oldDelay = SpriteLibrary.UnloadDelaySeconds;
            SpriteLibrary.UnloadDelaySeconds = 0.5f;
            try
            {
                NpcBrain b = SpawnDefinition(TestDefinition("selftest_lazy", Bravery.Normal, location, player, 0f), location);
                NpcSprite sprite = b.GetComponent<NpcSprite>();
                bool pendingAtSpawn = sprite != null && sprite.Pending && !sprite.Showing;
                float until = Time.time + 3f;
                while (Time.time < until && sprite != null && sprite.Pending)
                    yield return null;
                yield return null;
                Check("an ANPC waits invisibly for its set, then shows it", pendingAtSpawn && sprite != null && !sprite.Pending &&
                    sprite.Showing && lazy.IsReady && lazy.Users == 1,
                    "pending at spawn=" + pendingAtSpawn + ", ready=" + lazy.IsReady + ", users=" + lazy.Users);
                // anpc_info (spec tools-quality §A1): the facts an author needs, for one ANPC.
                string info = AdvancedNpcsMod.InfoCommand(b.Id);
                Check("anpc_info names the ANPC, its mode and its loaded sprite set",
                    info.Contains(b.Id) && info.Contains("Calm") && info.Contains("selftest_lazy/sprites") && info.Contains("loaded") &&
                    info.Contains("LOD") && info.Contains("enemy: no") && info.Contains("reaction with opinion:"), info);
                string none = AdvancedNpcsMod.InfoCommand("selftest_nobody_here");
                Check("anpc_info answers for an unknown id", none.Contains("selftest_nobody_here"), none);
                NpcBrain.Discard(b);
                yield return new WaitForSeconds(1.5f);
                Check("a set unloads after its last user", !lazy.IsReady && lazy.Sheets.Count == 0 && lazy.Users == 0,
                    "ready=" + lazy.IsReady + ", sheets=" + lazy.Sheets.Count + ", users=" + lazy.Users);

                // Spawned under a switched-off town and removed without ever being active (Unity skips OnDestroy):
                // it must not hold the set.
                GameObject offTown = new GameObject("selftest_off_town");
                offTown.transform.position = location.transform.position;
                offTown.SetActive(false);
                NpcDefinition offDef = TestDefinition("selftest_lazy_off", Bravery.Normal, location, player, 2f);
                offDef.Folder = "selftest_lazy";
                spawner.SpawnTest(NpcInstance.ForUnique(offDef, "Breton"), offTown.transform);
                UnityEngine.Object.Destroy(offTown);
                yield return null;
                Check("an ANPC that never became active does not hold its set", lazy.Users == 0, "users=" + lazy.Users);
                yield return new WaitForSeconds(1.5f);

                // Gone while its set is still loading: no error, and the set still settles unloaded.
                NpcDefinition quickDef = TestDefinition("selftest_lazy_quick", Bravery.Normal, location, player, 1f);
                quickDef.Folder = "selftest_lazy";
                NpcBrain quick = SpawnDefinition(quickDef, location);
                NpcBrain.Discard(quick);
                yield return new WaitForSeconds(1.5f);
                Check("a set whose user left mid-load settles unloaded", !lazy.IsReady && lazy.Sheets.Count == 0 && lazy.Users == 0,
                    "ready=" + lazy.IsReady + ", sheets=" + lazy.Sheets.Count + ", users=" + lazy.Users);

                // Killed: the living sprite lets go of the set, the corpse holds it (spec tools-quality B2.4).
                NpcDefinition deadDef = TestDefinition("selftest_lazy_dead", Bravery.Normal, location, player, 1f);
                deadDef.Folder = "selftest_lazy";
                NpcBrain dead = SpawnDefinition(deadDef, location);
                NpcSprite deadSprite = dead.GetComponent<NpcSprite>();
                float deadUntil = Time.time + 3f;
                while (Time.time < deadUntil && deadSprite != null && deadSprite.Pending)
                    yield return null;
                int corpsesBefore = NpcCorpseSprite.All.Count;
                PlayerHit(dead, GameManager.Instance.PlayerEntityBehaviour, 100000);
                yield return new WaitForSeconds(1f);
                NpcCorpseSprite deadBody = NpcCorpseSprite.All.Count > corpsesBefore ? NpcCorpseSprite.All[NpcCorpseSprite.All.Count - 1] : null;
                Check("a dead ANPC lets go of its set and its corpse holds it", deadBody != null && lazy.Users == 1,
                    "corpse=" + (deadBody != null) + ", users=" + lazy.Users);
                if (deadBody != null)
                    UnityEngine.Object.Destroy(deadBody.gameObject);
                NpcBrain.Discard(dead);
                yield return null;

                // anpc_reload_sprites (spec tools-quality §A3): a live ANPC swaps to the re-indexed set and shows it.
                NpcDefinition liveDef = TestDefinition("selftest_lazy_live", Bravery.Normal, location, player, 1f);
                liveDef.Folder = "selftest_lazy";
                NpcBrain live = SpawnDefinition(liveDef, location);
                NpcSprite liveSprite = live.GetComponent<NpcSprite>();
                float liveUntil = Time.time + 3f;
                while (Time.time < liveUntil && liveSprite != null && liveSprite.Pending)
                    yield return null;
                int reloaded = mod.Sprites.Reload("selftest_lazy", Path.GetDirectoryName(dir));
                int swapped = AdvancedNpcsMod.SwapSprites(new[] { "selftest_lazy" });
                LoadedSpriteSet fresh = liveSprite != null ? liveSprite.ChosenSet : null;
                liveUntil = Time.time + 3f;
                while (Time.time < liveUntil && liveSprite != null && liveSprite.Pending)
                    yield return null;
                yield return null;
                Check("anpc_reload_sprites swaps a live ANPC to the re-indexed set",
                    reloaded == 1 && swapped >= 1 && fresh != null && fresh != lazy && fresh.Label == lazy.Label && liveSprite.Showing &&
                    fresh.Users == 1 && lazy.Users == 0,
                    "reloaded=" + reloaded + ", swapped=" + swapped + ", new set=" + (fresh != null && fresh != lazy) +
                    ", showing=" + (liveSprite != null && liveSprite.Showing) + ", users new/old=" + (fresh != null ? fresh.Users : -1) + "/" + lazy.Users);
                NpcBrain.Discard(live);
                yield return null;
            }
            finally
            {
                SpriteLibrary.UnloadDelaySeconds = oldDelay;
                mod.Sprites.Remove("selftest_lazy");
            }
        }
    }
}

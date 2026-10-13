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
                string deadInfo = AdvancedNpcsMod.InfoCommand(dead.Id);
                Check("anpc_info on a dead ANPC names the set its corpse shows",
                    deadInfo.Contains("sprite: selftest_lazy/sprites, shown by the corpse") && !deadInfo.Contains("vanilla (no sprite set)"), deadInfo);
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
                int reloaded = mod.Sprites.Reload("selftest_lazy", Path.GetDirectoryName(dir), false);
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

                // Taken out of the library (the self-test's own clean-up): the sheets go once nothing uses the set.
                mod.Sprites.Remove("selftest_lazy");
                yield return null;
                yield return null;
                Check("a sprite set taken out of the library frees its sheets once unused",
                    fresh != null && fresh.Sheets.Count == 0 && !fresh.IsReady, fresh == null ? "no set" : "sheets=" + fresh.Sheets.Count);
            }
            finally
            {
                SpriteLibrary.UnloadDelaySeconds = oldDelay;
                mod.Sprites.Remove("selftest_lazy");
            }

            // Walk sheets declared but none usable (spec tools-quality B2.7): logged once, and the ANPC stands still.
            // One set loses its walk sheet at indexing (wrong size), the other while loading (a PNG that only has a header).
            string lostRoot = Path.Combine(Application.temporaryCachePath, "anpc_selftest_lostwalk");
            string badSize = Path.Combine(lostRoot, "sprites");
            string badData = Path.Combine(lostRoot, "sprites_2");
            Directory.CreateDirectory(badSize);
            Directory.CreateDirectory(badData);
            byte[] idlePng = SolidTexture(32, 32 * 8, new Color32(160, 90, 90, 255)).EncodeToPNG();
            string setJson = "{ \"pixelsPerUnit\": 10, \"cellHeight\": 32, \"groundY\": 4, \"fps\": 8, \"anchorX\": \"centre\", " +
                "\"animations\": { \"idle\": { \"cellWidth\": 16, \"frames\": 2 }, \"walk\": { \"cellWidth\": 16, \"frames\": 2 } } }";
            foreach (string d in new[] { badSize, badData })
            {
                File.WriteAllBytes(Path.Combine(d, "idle.png"), idlePng);
                File.WriteAllText(Path.Combine(d, SpriteLibrary.SetFile), setJson);
            }
            File.WriteAllBytes(Path.Combine(badSize, "walk.png"), SolidTexture(48, 32 * 8, new Color32(160, 90, 90, 255)).EncodeToPNG());
            byte[] headerOnly = new byte[PngHeader.Length];
            System.Array.Copy(idlePng, headerOnly, headerOnly.Length);
            File.WriteAllBytes(Path.Combine(badData, "walk.png"), headerOnly);
            LoadedSpriteSet lostAtIndex = SpriteLibrary.IndexSet("selftest_lostwalk/sprites", badSize);
            LoadedSpriteSet lostAtLoad = SpriteLibrary.IndexSet("selftest_lostwalk/sprites_2", badData);
            Check("a set whose walk sheets were all skipped at indexing is reported as standing still",
                lostAtIndex != null && lostAtIndex.DeclaredMovement && lostAtIndex.LostMovement,
                lostAtIndex == null ? "not indexed" : "declared=" + lostAtIndex.DeclaredMovement + ", lost=" + lostAtIndex.LostMovement);
            if (lostAtLoad != null)
            {
                mod.Sprites.Add("selftest_lostwalk", lostAtLoad);
                try
                {
                    bool lostBeforeLoad = lostAtLoad.LostMovement;
                    NpcBrain walker = SpawnDefinition(TestDefinition("selftest_lostwalk", Bravery.Normal, location, player, 1f), location);
                    NpcGait walkerGait = walker.GetComponent<NpcGait>();
                    bool turretAtSpawn = walkerGait != null && walkerGait.IsTurret;
                    float lostUntil = Time.time + 3f;
                    while (Time.time < lostUntil && !lostAtLoad.IsReady)
                        yield return null;
                    yield return null;
                    yield return null;
                    Check("a walk sheet dropped while loading is reported, and its ANPC becomes a turret",
                        !lostBeforeLoad && !turretAtSpawn && lostAtLoad.LostMovement && walkerGait != null && walkerGait.IsTurret,
                        "lost before/after=" + lostBeforeLoad + "/" + lostAtLoad.LostMovement + ", turret at spawn/now=" + turretAtSpawn + "/" +
                        (walkerGait != null && walkerGait.IsTurret));
                    NpcBrain.Discard(walker);
                    yield return null;
                }
                finally
                {
                    mod.Sprites.Remove("selftest_lostwalk");
                }
            }
            else
            {
                Check("a walk sheet dropped while loading is reported, and its ANPC becomes a turret", false, "not indexed");
            }
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    public partial class SelfTest
    {
        static AiScheduleEntry AnimationEntry(string package, string animation)
        {
            return new AiScheduleEntry { Package = package, Parameters = new Dictionary<string, object> { { "animation", animation } } };
        }

        IEnumerator AnimationPackageSteps(DaggerfallLocation location, Transform player)
        {
            DiscardTestPeople();
            string error;
            AiPackage package = PackageParser.Parse("selftest_visual_package", "{\"type\":\"play_animation\"}", out error);
            mod.Packages.AddForTest(package);
            LoadedSpriteSet synthetic = TestSpriteSet(false, "idle", "walk", "eat_1", "eat_2", "attack", "cast", "greet");
            LoadedSpriteSet stationary = TestSpriteSet(false, "idle", "eat_1");
            mod.Sprites.Add("selftest_visual", synthetic);
            mod.Sprites.Add("selftest_visual_stationary", stationary);
            try
            {
                NpcDefinition definition = TestDefinition("selftest_visual", Bravery.Normal, location, player, OutOfGreeting);
                definition.BaseClass = "Warrior";
                definition.WanderRadius = 8;
                definition.Schedule.Add(AnimationEntry(package.Id, "eat"));
                NpcBrain actor = SpawnDefinition(definition, location);
                yield return Settle;
                NpcSprite sprite = actor.GetComponent<NpcSprite>();
                Vector3 initial = actor.transform.position;
                int frameChanges = sprite != null ? sprite.FrameChanges : 0;
                bool sawFirst = false, sawSecond = false, repeats = false;
                string previous = null;
                int cycles = sprite != null ? sprite.PackageCycles : 0;
                float until = Time.time + 1.5f;
                while (Time.time < until)
                {
                    if (sprite != null)
                    {
                        string current = sprite.CurrentAnimation;
                        sawFirst |= current == "eat_1";
                        sawSecond |= current == "eat_2";
                        if (sprite.PackageCycles != cycles)
                        {
                            repeats |= previous != null && current == previous;
                            cycles = sprite.PackageCycles;
                        }
                        previous = current;
                    }
                    yield return null;
                }
                Check("animation package holds position and animates", sprite != null && sprite.CurrentState == NpcSprite.PackageState &&
                    sprite.FrameChanges > frameChanges && Vector3.Distance(initial, actor.transform.position) < 0.2f, actor.Describe());
                Check("animation variants change at complete cycles without repeats", sawFirst && sawSecond && !repeats,
                    "first=" + sawFirst + ", second=" + sawSecond + ", repeated=" + repeats);

                actor.HoldStill(0.6f);
                yield return new WaitForSeconds(0.1f);
                Check("a movement hold interrupts the visual package", sprite.CurrentState != NpcSprite.PackageState, sprite.CurrentAnimation);
                yield return new WaitForSeconds(0.7f);
                Check("visual package resumes after a hold", sprite.CurrentState == NpcSprite.PackageState, sprite.CurrentAnimation);
                sprite.PlayCast(true);
                actor.HoldStill(0.5f);
                yield return new WaitForSeconds(0.1f);
                Check("healing cast takes priority over prayer", sprite.CurrentState == SpriteStates.Heal || sprite.CurrentState == SpriteStates.Cast,
                    sprite.CurrentState + "/" + sprite.CurrentAnimation);
                yield return new WaitForSeconds(0.6f);

                actor.SwitchHostile(true);
                yield return Settle;
                Check("combat interrupts visual package", actor.PackageAnimation == null && sprite.CurrentState != NpcSprite.PackageState, actor.Status);
                actor.SwitchHostile(false);
                yield return new WaitForSeconds(0.8f);
                Check("visual package resumes after combat", sprite.CurrentState == NpcSprite.PackageState, sprite.CurrentAnimation);

                definition.Schedule[0] = AnimationEntry(package.Id, "eat_1");
                yield return Settle;
                yield return new WaitForSeconds(0.5f);
                Check("exact numbered sheet loops without other variants", sprite.CurrentAnimation == "eat_1", sprite.CurrentAnimation);

                int blows = sprite.BlowsLanded;
                definition.Schedule[0] = AnimationEntry(package.Id, "attack");
                yield return new WaitForSeconds(1.4f);
                Check("attack sheet in a package is visual only", sprite.CurrentState == NpcSprite.PackageState &&
                    sprite.CurrentAnimation == "attack" && sprite.BlowsLanded == blows && !sprite.BlowPending,
                    sprite.CurrentState + ", blows=" + sprite.BlowsLanded + ", pending=" + sprite.BlowPending);

                definition.Schedule.Clear();
                yield return new WaitForSeconds(0.4f);
                Check("ending slot releases animation to current AI", actor.PackageAnimation == null &&
                    sprite.CurrentState != NpcSprite.PackageState, actor.PackageDescription);

                definition.Schedule.Add(AnimationEntry(package.Id, "missing_sheet"));
                yield return Settle;
                Check("missing animation falls back to current AI", actor.PackageAnimation == null &&
                    actor.PackageDescription.Contains("no usable animation"), actor.PackageDescription);
                sprite.Swap(TestSpriteSet(false, "idle", "walk", "missing_sheet"));
                yield return Settle;
                Check("asset reload recovers a previously missing animation", sprite.CurrentState == NpcSprite.PackageState &&
                    sprite.CurrentAnimation == "missing_sheet", actor.PackageDescription);
                NpcBrain.Discard(actor);

                NpcDefinition fixedDefinition = TestDefinition("selftest_visual_stationary", Bravery.Normal, location, player, OutOfGreeting);
                fixedDefinition.BaseClass = "Warrior";
                fixedDefinition.Schedule.Add(AnimationEntry(package.Id, "eat"));
                NpcBrain fixedActor = SpawnDefinition(fixedDefinition, location);
                yield return Settle;
                NpcSprite fixedSprite = fixedActor.GetComponent<NpcSprite>();
                Check("stationary sprite sets can play packages", fixedSprite != null && fixedSprite.CurrentState == NpcSprite.PackageState,
                    fixedActor.PackageDescription);
                NpcBrain.Discard(fixedActor);

                // This goes through the real file index and deferred PNG loading, not just synthetic textures.
                bool indexedPrayer = false;
                foreach (LoadedSpriteSet set in mod.Sprites.For("temple_monk_1"))
                    indexedPrayer |= set.Set.Animations.ContainsKey("pray_1");
                Check("monk schedule loads its custom pray_1 sheet", indexedPrayer, "pray_1 indexed=" + indexedPrayer);
                if (indexedPrayer)
                {
                    NpcDefinition monkDefinition = TestDefinition("selftest_praying_monk", Bravery.Normal, location, player, OutOfGreeting);
                    monkDefinition.Folder = "temple_monk_1";
                    monkDefinition.BaseClass = "Monk";
                    monkDefinition.Schedule.Add(AnimationEntry(package.Id, "pray"));
                    NpcBrain monk = SpawnDefinition(monkDefinition, location);
                    NpcSprite monkSprite = monk.GetComponent<NpcSprite>();
                    until = Time.time + 5f;
                    while (Time.time < until && monkSprite != null && (monkSprite.Pending || monkSprite.CurrentState != NpcSprite.PackageState))
                        yield return null;
                    Check("monk plays the real prayer sheet after lazy loading", monkSprite != null &&
                        monkSprite.CurrentState == NpcSprite.PackageState && monkSprite.CurrentAnimation == "pray_1", monk.Describe());
                    NpcBrain.Discard(monk);
                }
            }
            finally
            {
                mod.Packages.RemoveForTest(package.Id);
                mod.Sprites.Remove("selftest_visual");
                mod.Sprites.Remove("selftest_visual_stationary");
            }
        }
    }
}

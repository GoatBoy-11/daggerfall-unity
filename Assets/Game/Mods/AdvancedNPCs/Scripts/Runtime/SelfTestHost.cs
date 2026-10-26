using System;
using System.Collections;
using System.IO;
using UnityEngine;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Serialization;
using Wenzil.Console;

namespace AdvancedNPCs
{
    /// <summary>
    /// The anpc_selftest console command and the unattended autorun (Tools~/selftest*.sh, play.sh). Lives with the
    /// SelfTest*.cs files so a release build can leave all of them out; AdvancedNpcsMod finds it by name.
    /// </summary>
    public static class SelfTestHost
    {
        public const string AutoRunFile = "selftest-autorun.txt";

        /// <summary>Suites a short run can name (anpc_selftest &lt;suite&gt;, or the autorun file's third line).</summary>
        static readonly string[] Suites =
        {
            SelfTest.SoundSuite, SelfTest.GuardSuite, SelfTest.CompanionEventsSuite, SelfTest.CompanionBarksSuite, SelfTest.AuditSuite,
            SelfTest.CompanionSpellsSuite, SelfTest.CompanionLimitsSuite, SelfTest.CompanionPerformanceSuite, SelfTest.CompanionBowsSuite,
            SelfTest.CompanionEquipmentSuite, SelfTest.CompanionTradingSuite, SelfTest.CompanionPlaytestSuite, SelfTest.CompanionOrdersSuite,
            SelfTest.CompanionCombatSuite, SelfTest.NavigationSuite, SelfTest.CompanionSuite, SelfTest.RangedSuite, SelfTest.ReplaceSuite,
            SelfTest.HealingSuite, SelfTest.PackageSuite, SelfTest.AnimalSuite, SelfTest.BlockSuite, SelfTest.CompanionPotionsSuite,
            SelfTest.SpriteLoadSuite, SelfTest.CompanionSurvivabilitySuite,
        };

        static AdvancedNpcsMod mod;
        static NpcSpawner spawner;

        /// <summary>Called by AdvancedNpcsMod.Start (through reflection) once the spawner exists.</summary>
        public static void Register(AdvancedNpcsMod owner, NpcSpawner npcSpawner)
        {
            mod = owner;
            spawner = npcSpawner;
            ConsoleCommandsDatabase.RegisterCommand("anpc_selftest",
                "Runs the Advanced NPCs in-game self-test next to you (god mode during the run; results in Player.log).", "anpc_selftest", SelfTestCommand);
            string autorun = Path.Combine(AnpcFiles.Root, AutoRunFile);
            if (File.Exists(autorun))
                mod.StartCoroutine(AutoRun(autorun));
        }

        static string SelfTestCommand(params string[] args)
        {
            if (SelfTest.Running)
                return "The self-test is already running.";
            SelfTest test = new SelfTest(mod, spawner);
            if (args.Length > 0 && Array.IndexOf(Suites, args[0]) >= 0)
                test.Suite = args[0];
            mod.StartCoroutine(test.Run(null));
            return "Self-test started; results appear on screen and in Player.log (SELFTEST lines).";
        }

        /// <summary>
        /// Unattended start used by Tools~/selftest.sh and Tools~/play.sh: the flag file's first two lines name a
        /// character and a save, which is loaded straight from the title screen. A third line "play" stops there
        /// (manual testing; the flag file is removed so the next start is normal); "ranged" / "replace" run only the
        /// ranged-attack / flat replacement checks (Tools~/selftest-ranged.sh, selftest-replace.sh); otherwise the whole self-test runs. Then the game quits.
        /// </summary>
        static IEnumerator AutoRun(string path)
        {
            string[] lines = File.ReadAllLines(path);
            if (lines.Length < 2)
            {
                AdvancedNpcsMod.LogError(SelfTest.Prefix + "FAIL autorun -- " + AutoRunFile + " needs: character name, then save name");
                Application.Quit();
                yield break;
            }
            string character = lines[0].Trim();
            string save = lines[1].Trim();
            bool play = lines.Length > 2 && lines[2].Trim() == "play";
            string third = lines.Length > 2 ? lines[2].Trim() : "";
            string suite = Array.IndexOf(Suites, third) >= 0 ? third : null;
            if (play)
                File.Delete(path);
            AdvancedNpcsMod.Log(SelfTest.Prefix + "autorun: loading save '" + save + "' of '" + character + "'" + (play ? " to play" : ""));

            // The title screen runs with the game paused (timeScale 0), so only real-time waits finish there.
            float waited = 0f;
            while (waited < 60f && !SaveLoadManager.Instance.IsReady())
            {
                waited += 0.5f;
                yield return new WaitForSecondsRealtime(0.5f);
            }
            yield return new WaitForSecondsRealtime(2f);
            SaveLoadManager.Instance.EnumerateSaves();
            int key = SaveLoadManager.Instance.FindSaveFolderByNames(character, save);
            if (key < 0)
            {
                AdvancedNpcsMod.LogError(SelfTest.Prefix + "FAIL autorun -- no save '" + save + "' of character '" + character + "'");
                if (!play)
                    Application.Quit();
                yield break;
            }
            SaveLoadManager.Instance.Load(key);

            // Like DFU's own Load button, close the title screen windows (intro video, start menu): while they
            // are open the game stays paused. They can appear after the load starts, so keep closing them.
            waited = 0f;
            while (waited < 180f && !InTown())
            {
                DaggerfallUI.Instance.PopToHUD();
                waited += 1f;
                yield return new WaitForSecondsRealtime(1f);
            }
            DaggerfallUI.Instance.PopToHUD();
            if (!InTown())
            {
                AdvancedNpcsMod.LogError(SelfTest.Prefix + "FAIL autorun -- save did not load into a town within 180 s");
                if (!play)
                    Application.Quit();
                yield break;
            }
            if (play)
            {
                AdvancedNpcsMod.Log("Loaded '" + save + "'; have fun.");
                yield break;
            }
            yield return new WaitForSecondsRealtime(5f); // let the town and its NPCs finish spawning
            SelfTest run = new SelfTest(mod, spawner);
            run.Suite = suite;
            yield return mod.StartCoroutine(run.Run(null));
            yield return new WaitForSecondsRealtime(1f);
            Application.Quit();
        }

        static bool InTown()
        {
            GameManager gm = GameManager.Instance;
            return gm != null && gm.StateManager.CurrentState == StateManager.StateTypes.Game &&
                   !SaveLoadManager.Instance.LoadInProgress && !gm.PlayerEnterExit.IsPlayerInside &&
                   gm.StreamingWorld.CurrentPlayerLocationObject != null;
        }
    }
}

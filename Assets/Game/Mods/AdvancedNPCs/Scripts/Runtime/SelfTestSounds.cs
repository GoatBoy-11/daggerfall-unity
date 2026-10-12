using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.MagicAndEffects;
using DaggerfallWorkshop.Game.MagicAndEffects.MagicEffects;
using AdvancedNPCs.Core;

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
                    UnityEngine.Object.Destroy(clip);
                yield return null;
            }
        }

        IEnumerator SoundSteps(DaggerfallLocation location, Transform player)
        {
            DaggerfallEntityBehaviour playerBehaviour = GameManager.Instance.PlayerEntityBehaviour;
            string root = Path.Combine(Application.temporaryCachePath, "anpc_selftest_voice");
            string dir = Path.Combine(root, "Sounds");
            Directory.CreateDirectory(dir);
            foreach (string n in new[] { "greet_1", "attack_1", "attack_2", "hit_1", "death_1", "step_1", "step_2", "cast_1", "idle_1" })
                File.WriteAllBytes(Path.Combine(dir, n + ".wav"), ToneWav(0.1f));

            LoadedSoundSet voiceSet = SoundLibrary.IndexSet("selftest_voice/Sounds", dir);
            LoadedSoundSet plainSet = SoundLibrary.IndexSet("selftest_voice_plain/Sounds", dir);
            Check("a Sounds folder is sorted into pools by name (suffix ignored, unknown names left out)",
                voiceSet != null && voiceSet.Files.Count == 6 && voiceSet.Files[SoundPools.Attack].Count == 2 &&
                voiceSet.Files[SoundPools.Step].Count == 2 && !voiceSet.Files.ContainsKey("idle"),
                voiceSet == null ? "not indexed" : voiceSet.Summary());
            if (voiceSet == null || plainSet == null)
                yield break;
            mod.Sounds.Add("selftest_voice", voiceSet);
            mod.Sounds.Add("selftest_voice_plain", plainSet);
            mod.Sprites.Add("selftest_voice", TestSpriteSet(false, "idle", "walk", "greet", "attack", "hit", "death"));
            NpcGreeter.ReactionOverride = "likes";
            NpcGreeter.ChanceOverride = 1f;
            DiscardTestPeople();
            Vector3 open = OpenDirection(player, 16f);
            try
            {
                NpcDefinition d = TestDefinition("selftest_voice", Bravery.Brave, location, player, OutOfGreeting);
                d.BaseClass = "Warrior";
                d.Sounds = new SoundSettings { CombatChance = 100, GreetChance = 100 };
                NpcBrain b = SpawnDefinition(d, location);
                yield return Settle;
                NpcVoice voice = b.GetComponent<NpcVoice>();
                EnemySounds enemySounds = b.GetComponent<EnemySounds>();
                float until = Time.time + 3f;
                while (Time.time < until && !voiceSet.IsReady)
                    yield return null;
                Check("an ANPC with a Sounds folder loads them and silences DFU's own voice lookup",
                    voice != null && voiceSet.IsReady && voiceSet.Users == 1 && enemySounds != null &&
                    enemySounds.RaceForSounds == Races.None && voice.VoiceRace != Races.None,
                    "voice=" + (voice != null) + ", ready=" + voiceSet.IsReady + ", users=" + voiceSet.Users +
                    ", race=" + (enemySounds != null ? enemySounds.RaceForSounds.ToString() : "-"));
                if (voice == null)
                    yield break;

                // Greeting: the greet pool plays with the greet sheet.
                b.Teleport(player.position + open * 3f);
                FaceToward(b, player.position);
                until = Time.time + 3f;
                while (Time.time < until && voice.Played(SoundPools.Greet) == 0)
                    yield return null;
                Check("a greeting plays a greet sound", voice.Played(SoundPools.Greet) == 1, "greet sounds=" + voice.Played(SoundPools.Greet));

                // Chasing the player: footfalls on the walk sheet's frames, then attack sounds.
                b.Teleport(player.position + open * 12f);
                FaceToward(b, player.position);
                b.SwitchHostile(true);
                until = Time.time + 8f;
                while (Time.time < until && (voice.Played(SoundPools.Step) == 0 || voice.Played(SoundPools.Attack) == 0))
                    yield return null;
                Check("a walking ANPC plays step sounds on its sheet's frames", voice.Played(SoundPools.Step) > 0,
                    "steps=" + voice.Played(SoundPools.Step) + ", " + b.Status);
                Check("an attack plays an attack sound and no vanilla grunt", voice.Played(SoundPools.Attack) > 0 &&
                    voice.Played("vanilla_attack") == 0, "attacks=" + voice.Played(SoundPools.Attack) + ", vanilla=" + voice.Played("vanilla_attack"));

                int hitsBefore = voice.Played(SoundPools.Hit);
                PlayerHit(b, playerBehaviour, 1);
                yield return null;
                yield return null;
                Check("a hit plays a hit sound", voice.Played(SoundPools.Hit) == hitsBefore + 1,
                    "hit sounds " + hitsBefore + " -> " + voice.Played(SoundPools.Hit));

                // Overlapping one-shots: each on its own audio source, so a new pitch does not bend the one still playing.
                voice.Play(SoundPools.Greet);
                AudioSource firstVoice = voice.LastVoice;
                voice.Play(SoundPools.Greet);
                Check("overlapping voice sounds take turns over separate audio sources",
                    firstVoice != null && voice.LastVoice != null && firstVoice != voice.LastVoice,
                    "first=" + (firstVoice != null ? firstVoice.GetInstanceID().ToString() : "-") + ", second=" +
                    (voice.LastVoice != null ? voice.LastVoice.GetInstanceID().ToString() : "-"));

                PlayerHit(b, playerBehaviour, 100000);
                yield return new WaitForSeconds(0.5f);
                Check("death always plays a death sound and lets go of the sounds",
                    voice.Played(SoundPools.Death) == 1 && voiceSet.Users == 0,
                    "death sounds=" + voice.Played(SoundPools.Death) + ", users=" + voiceSet.Users);
                NpcBrain.Discard(b);

                // Vanilla sprite: footsteps by timer while moving, none beyond stepDistance.
                NpcDefinition pd = TestDefinition("selftest_voice_plain", Bravery.Brave, location, player, 0f);
                pd.BaseClass = "Warrior";
                pd.Sounds = new SoundSettings { CombatChance = 0, GreetChance = 0 };
                NpcBrain plain = SpawnDefinition(pd, location);
                yield return Settle;
                NpcVoice plainVoice = plain.GetComponent<NpcVoice>();
                plain.Teleport(player.position + open * 12f);
                FaceToward(plain, player.position);
                pd.Sounds.StepDistance = 0.5f;      // farther than that: silent
                plain.SwitchHostile(true);
                yield return new WaitForSeconds(1.5f);
                int farSteps = plainVoice != null ? plainVoice.Played(SoundPools.Step) : -1;
                Check("no step sounds beyond stepDistance", farSteps == 0, "steps=" + farSteps + ", " + plain.Status);
                plain.SwitchHostile(false);
                yield return Settle;
                plain.Teleport(player.position + open * 12f);
                FaceToward(plain, player.position);
                pd.Sounds.StepDistance = 30f;
                plain.SwitchHostile(true);
                until = Time.time + 4f;
                while (Time.time < until && plainVoice != null && plainVoice.Played(SoundPools.Step) == 0)
                    yield return null;
                Check("an ANPC with the vanilla sprite steps by timer while moving", plainVoice != null && plainVoice.Played(SoundPools.Step) > 0,
                    "steps=" + (plainVoice != null ? plainVoice.Played(SoundPools.Step) : -1) + ", " + plain.Status);
                plain.SwitchHostile(false);

                string info = AdvancedNpcsMod.InfoCommand(plain.Id);
                Check("anpc_info shows the ANPC's sound pools", info.Contains("sounds: selftest_voice_plain/Sounds") && info.Contains("step 2"), info);
                // anpc_reload_assets: a live ANPC's voice moves to the re-indexed Sounds; the old set loses its user.
                bool reloaded = mod.Sounds.Reload("selftest_voice_plain", root);
                int swapped = AdvancedNpcsMod.SwapSounds(new[] { "selftest_voice_plain" });
                LoadedSoundSet freshSounds = plainVoice != null ? plainVoice.Set : null;
                Check("anpc_reload_assets swaps a live ANPC to the re-read Sounds",
                    reloaded && swapped == 1 && freshSounds != null && freshSounds != plainSet && freshSounds.Users == 1 && plainSet.Users == 0,
                    "reloaded=" + reloaded + ", swapped=" + swapped + ", users new/old=" + (freshSounds != null ? freshSounds.Users : -1) + "/" + plainSet.Users);
                yield return null;
                yield return null;
                bool keptAWhile = plainSet.Clips.Count > 0;
                yield return new WaitForSecondsRealtime(SoundRules.RetireDelay(0.1f) + 0.5f);
                Check("a replaced sound set keeps its clips a while (a death cry may still play), then frees them",
                    keptAWhile && plainSet.Clips.Count == 0, "kept=" + keptAWhile + ", clips later=" + plainSet.Clips.Count);
                NpcBrain.Discard(plain);
                yield return null;
            }
            finally
            {
                NpcGreeter.ReactionOverride = null;
                NpcGreeter.ChanceOverride = -1f;
                mod.Sounds.Remove("selftest_voice");
                mod.Sounds.Remove("selftest_voice_plain");
                mod.Sprites.Remove("selftest_voice");
            }
            DiscardTestPeople();

            // Review fixes: spawned into a switched-off town (EnemySounds.Awake runs only when it turns on), and a
            // creature whose own attack growl must give way to the folder's attack sounds.
            LoadedSoundSet offSet = SoundLibrary.IndexSet("selftest_voice_off/Sounds", dir);
            LoadedSoundSet orcSet = SoundLibrary.IndexSet("selftest_voice_orc/Sounds", dir);
            mod.Sounds.Add("selftest_voice_off", offSet);
            mod.Sounds.Add("selftest_voice_orc", orcSet);
            try
            {
                GameObject offTown = new GameObject("selftest_voice_town");
                offTown.transform.position = location.transform.position;
                offTown.SetActive(false);
                NpcDefinition od = TestDefinition("selftest_voice_off", Bravery.Normal, location, player, 2f);
                NpcBrain off = spawner.SpawnTest(NpcInstance.ForUnique(od, "Breton"), offTown.transform);
                offTown.SetActive(true);
                yield return null;
                yield return null;
                NpcVoice offVoice = off != null ? off.GetComponent<NpcVoice>() : null;
                EnemySounds offSounds = off != null ? off.GetComponent<EnemySounds>() : null;
                Check("an ANPC spawned into a switched-off town still has DFU's voice silenced once it turns on",
                    offVoice != null && offSounds != null && offSounds.RaceForSounds == Races.None &&
                    offVoice.VoiceRace >= Races.Breton && offVoice.VoiceRace <= Races.HighElf,
                    "race now " + (offSounds != null ? offSounds.RaceForSounds.ToString() : "-") + ", kept " +
                    (offVoice != null ? offVoice.VoiceRace.ToString() : "-"));
                UnityEngine.Object.Destroy(offTown);
                yield return null;

                NpcDefinition orcDef = TestDefinition("selftest_voice_orc", Bravery.Brave, location, player, 2f);
                orcDef.Hostile = true;
                orcDef.BaseClass = "Orc";
                orcDef.IsCreature = true;
                NpcBrain orc = SpawnDefinition(orcDef, location);
                yield return Settle;
                float orcUntil = Time.time + 3f;
                while (Time.time < orcUntil && !orcSet.IsReady)
                    yield return null;
                yield return null;
                NpcVoice orcVoice = orc.GetComponent<NpcVoice>();
                Check("a creature with attack sounds no longer plays its own attack growl",
                    orcVoice != null && orcVoice.CreatureAttackSilenced, "silenced=" + (orcVoice != null && orcVoice.CreatureAttackSilenced));
                NpcBrain.Discard(orc);
                yield return null;
            }
            finally
            {
                mod.Sounds.Remove("selftest_voice_off");
                mod.Sounds.Remove("selftest_voice_orc");
            }

            // DFU's grunt stands in only where DFU itself voices one, and for a pool none of whose files could be decoded.
            string gruntDir = Path.Combine(Path.Combine(Application.temporaryCachePath, "anpc_selftest_grunt"), "Sounds");
            Directory.CreateDirectory(gruntDir);
            File.WriteAllBytes(Path.Combine(gruntDir, "greet_1.wav"), ToneWav(0.1f));
            File.WriteAllText(Path.Combine(gruntDir, "hit_1.wav"), "not a sound");
            LoadedSoundSet gruntSet = SoundLibrary.IndexSet("selftest_voice_grunt/Sounds", gruntDir);
            mod.Sounds.Add("selftest_voice_grunt", gruntSet);
            NpcVoice.ForceVanillaVoice = true;
            try
            {
                NpcDefinition gd = TestDefinition("selftest_voice_grunt", Bravery.Normal, location, player, 2f);
                gd.BaseClass = "Warrior";
                gd.Sounds = new SoundSettings { CombatChance = 100, GreetChance = 0 };
                NpcBrain g = SpawnDefinition(gd, location);
                yield return Settle;
                float gruntUntil = Time.time + 3f;
                while (Time.time < gruntUntil && !gruntSet.IsReady)
                    yield return null;
                yield return null;
                NpcVoice gVoice = g.GetComponent<NpcVoice>();
                Check("a pool whose every file failed to decode has no clips", gruntSet.IsReady &&
                    gruntSet.Files.ContainsKey(SoundPools.Hit) && !gruntSet.Clips.ContainsKey(SoundPools.Hit), gruntSet.Summary());

                int gruntsBefore = gVoice != null ? gVoice.Played("vanilla_hit") : -1;
                PlayerHit(g, playerBehaviour, 1);
                yield return null;
                yield return null;
                int gruntsAfterHit = gVoice != null ? gVoice.Played("vanilla_hit") : -1;
                Check("a weapon hit with no playable hit sound gives DFU's own grunt",
                    gruntsAfterHit == gruntsBefore + 1 && gVoice.Played(SoundPools.Hit) == 0,
                    "grunts " + gruntsBefore + " -> " + gruntsAfterHit + ", custom hits " + (gVoice != null ? gVoice.Played(SoundPools.Hit) : -1));

                int healthBefore = g.EntityBehaviour.Entity.CurrentHealth;
                EntityEffectManager gEffects = g.GetComponent<EntityEffectManager>();
                // No caster: no absorption, reflection or resistance roll. The target's saving throw can still cut a small
                // spell to nothing, so cast again (each time watching for a grunt) until one lands.
                for (int cast = 0; cast < 5 && gEffects != null && g.EntityBehaviour.Entity.CurrentHealth >= healthBefore; cast++)
                {
                    gEffects.AssignBundle(DamageSpell(null), AssignBundleFlags.BypassSavingThrows | AssignBundleFlags.BypassChance);
                    yield return null;
                    yield return null;
                }
                int gruntsAfterSpell = gVoice != null ? gVoice.Played("vanilla_hit") : -1;
                Check("spell damage gives no grunt (DFU voices none)",
                    g.EntityBehaviour.Entity.CurrentHealth < healthBefore && gruntsAfterSpell == gruntsAfterHit,
                    "health " + healthBefore + " -> " + g.EntityBehaviour.Entity.CurrentHealth + ", grunts " + gruntsAfterHit + " -> " + gruntsAfterSpell);
                NpcBrain.Discard(g);
                yield return null;
            }
            finally
            {
                NpcVoice.ForceVanillaVoice = false;
                mod.Sounds.Remove("selftest_voice_grunt");
            }

            // The "ANPC sounds" and "Footsteps" settings, switched while ANPCs are about.
            LoadedSoundSet modesSet = SoundLibrary.IndexSet("selftest_voice_modes/Sounds", dir);
            mod.Sounds.Add("selftest_voice_modes", modesSet);
            NpcVoice.ForceVanillaVoice = true;
            try
            {
                NpcDefinition md = TestDefinition("selftest_voice_modes", Bravery.Normal, location, player, 2f);
                md.BaseClass = "Warrior";
                md.Sounds = new SoundSettings { CombatChance = 100, GreetChance = 100 };
                NpcBrain m = SpawnDefinition(md, location);
                NpcBrain bare = SpawnDefinition(TestDefinition("selftest_voice_none", Bravery.Normal, location, player, 3f), location);
                yield return Settle;
                float modesUntil = Time.time + 3f;
                while (Time.time < modesUntil && !modesSet.IsReady)
                    yield return null;
                NpcVoice mVoice = m.GetComponent<NpcVoice>();
                NpcVoice bareVoice = bare.GetComponent<NpcVoice>();
                Check("an ANPC without a Sounds folder has a voice that leaves it to DFU",
                    bareVoice != null && bareVoice.Set == null && bareVoice.DfuVoicing, "voice=" + (bareVoice != null) +
                    ", DFU voicing=" + (bareVoice != null && bareVoice.DfuVoicing));

                mod.Config.VoiceMode = VoiceModes.Vanilla;
                yield return null;
                yield return null;
                int hitsBefore = mVoice.Played(SoundPools.Hit);
                bool greeted = mVoice.Play(SoundPools.Greet);
                PlayerHit(m, playerBehaviour, 1);
                yield return null;
                yield return null;
                Check("ANPC sounds Vanilla: DFU voices an ANPC that has a Sounds folder, its own sounds stay quiet",
                    mVoice.DfuVoicing && !greeted && mVoice.Played(SoundPools.Hit) == hitsBefore,
                    "DFU voicing=" + mVoice.DfuVoicing + ", greet played=" + greeted + ", hit sounds " + hitsBefore + " -> " + mVoice.Played(SoundPools.Hit));

                mod.Config.VoiceMode = VoiceModes.Off;
                yield return null;
                yield return null;
                int gruntsBefore = mVoice.Played("vanilla_hit");
                hitsBefore = mVoice.Played(SoundPools.Hit);
                greeted = mVoice.Play(SoundPools.Greet);
                PlayerHit(m, playerBehaviour, 1);
                yield return null;
                yield return null;
                Check("ANPC sounds Off: no voice at all, from the folder or from DFU",
                    !mVoice.DfuVoicing && !bareVoice.DfuVoicing && !greeted && mVoice.Played(SoundPools.Hit) == hitsBefore &&
                    mVoice.Played("vanilla_hit") == gruntsBefore,
                    "DFU voicing=" + mVoice.DfuVoicing + "/" + bareVoice.DfuVoicing + ", greet played=" + greeted + ", hit sounds " +
                    hitsBefore + " -> " + mVoice.Played(SoundPools.Hit) + ", grunts " + gruntsBefore + " -> " + mVoice.Played("vanilla_hit"));

                mod.Config.VoiceMode = VoiceModes.Anpc;
                yield return null;
                yield return null;
                Check("ANPC sounds back to ANPC: the folder's sounds play again",
                    !mVoice.DfuVoicing && bareVoice.DfuVoicing && mVoice.Play(SoundPools.Greet), "DFU voicing=" + mVoice.DfuVoicing);
                m.SwitchHostile(false);
                NpcBrain.Discard(bare);

                // Footsteps off: none while it walks; on again: they come back.
                mod.Config.Footsteps = false;
                m.Teleport(player.position + open * 12f);
                FaceToward(m, player.position);
                m.SwitchHostile(true);
                int stepsBefore = mVoice.Played(SoundPools.Step);
                yield return new WaitForSeconds(1.5f);
                int stepsOff = mVoice.Played(SoundPools.Step) - stepsBefore;
                mod.Config.Footsteps = true;
                float stepUntil = Time.time + 4f;
                while (Time.time < stepUntil && mVoice.Played(SoundPools.Step) - stepsBefore == stepsOff)
                    yield return null;
                Check("Footsteps off silences steps; on again, they play",
                    stepsOff == 0 && mVoice.Played(SoundPools.Step) - stepsBefore > 0,
                    "steps while off=" + stepsOff + ", after=" + (mVoice.Played(SoundPools.Step) - stepsBefore) + ", " + m.Status);
                m.SwitchHostile(false);
                NpcBrain.Discard(m);
                yield return null;
            }
            finally
            {
                NpcVoice.ForceVanillaVoice = false;
                mod.Config.VoiceMode = VoiceModes.Anpc;
                mod.Config.Footsteps = true;
                mod.Sounds.Remove("selftest_voice_modes");
            }

            // Default steps (ANPCs/_Sounds): for ANPCs without step files of their own, unless "defaultSteps": false.
            string defaultsRoot = Path.Combine(Application.temporaryCachePath, "anpc_selftest_defaults");
            string defaultsDir = Path.Combine(defaultsRoot, SoundLibrary.SharedFolder);
            Directory.CreateDirectory(defaultsDir);
            foreach (string n in new[] { "step_1", "step_2", "greet_1" })
                File.WriteAllBytes(Path.Combine(defaultsDir, n + ".wav"), ToneWav(0.1f));
            // The test's defaults go only to its own three ANPCs (as anpc_reload_assets would hand them out), so the
            // installed defaults of everyone else stay as they are.
            LoadedSoundSet defaults = SoundLibrary.IndexDefaults(defaultsRoot);
            Check("_Sounds gives default steps, and only steps",
                defaults != null && defaults.Files.Count == 1 && defaults.Files.ContainsKey(SoundPools.Step) &&
                defaults.Files[SoundPools.Step].Count == 2, defaults == null ? "none" : defaults.Summary());
            LoadedSoundSet ownSteps = SoundLibrary.IndexSet("selftest_steps_own/Sounds", dir);
            try
            {
                if (defaults != null)
                    mod.Sounds.Add("selftest_steps_defaults", defaults);
                mod.Sounds.Add("selftest_steps_own", ownSteps);
                NpcDefinition ad = TestDefinition("selftest_steps_default", Bravery.Normal, location, player, -6f);
                NpcDefinition od = TestDefinition("selftest_steps_own", Bravery.Normal, location, player, 0f);
                NpcDefinition nd = TestDefinition("selftest_steps_none", Bravery.Normal, location, player, 6f);
                ad.Sounds = new SoundSettings();
                od.Sounds = new SoundSettings { CombatChance = 0, GreetChance = 0 };
                nd.Sounds = new SoundSettings { DefaultSteps = false };
                NpcBrain withDefaults = SpawnDefinition(ad, location);
                NpcBrain withOwn = SpawnDefinition(od, location);
                NpcBrain withNone = SpawnDefinition(nd, location);
                yield return Settle;
                NpcVoice dv = withDefaults.GetComponent<NpcVoice>();
                NpcVoice ov = withOwn.GetComponent<NpcVoice>();
                NpcVoice nv = withNone.GetComponent<NpcVoice>();
                Vector3 side = Vector3.Cross(Vector3.up, open).normalized;
                NpcBrain[] walkers = { withDefaults, withOwn, withNone };
                foreach (NpcBrain w in walkers)
                    w.GetComponent<NpcVoice>().SwapDefaultSteps(defaults);
                for (int i = 0; i < walkers.Length; i++)
                {
                    walkers[i].Teleport(player.position + open * 12f + side * (i - 1) * 2.5f);
                    FaceToward(walkers[i], player.position);
                    walkers[i].SwitchHostile(true);
                }
                float stepsUntil = Time.time + 5f;
                while (Time.time < stepsUntil && (dv.Played("default_step") == 0 || ov.Played(SoundPools.Step) == 0))
                    yield return null;
                yield return new WaitForSeconds(0.5f);
                Check("an ANPC without step files of its own plays the default steps",
                    dv.StepsSource == "default" && dv.Played("default_step") > 0,
                    "source=" + dv.StepsSource + ", default steps=" + dv.Played("default_step") + ", " + withDefaults.Status);
                Check("an ANPC with its own step files never uses the defaults",
                    ov.StepsSource == "own" && ov.Played(SoundPools.Step) > 0 && ov.Played("default_step") == 0,
                    "source=" + ov.StepsSource + ", steps=" + ov.Played(SoundPools.Step) + ", default steps=" + ov.Played("default_step"));
                Check("\"defaultSteps\": false keeps an ANPC without step files silent",
                    nv.StepsSource == "none" && nv.Played(SoundPools.Step) == 0, "source=" + nv.StepsSource + ", steps=" + nv.Played(SoundPools.Step));
                string stepInfo = AdvancedNpcsMod.InfoCommand(withDefaults.Id);
                Check("anpc_info names where the footsteps come from", stepInfo.Contains("steps: default"), stepInfo);
                foreach (NpcBrain w in walkers)
                {
                    w.SwitchHostile(false);
                    NpcBrain.Discard(w);
                }
                yield return null;
                yield return null;
                Check("the default steps are let go when their ANPCs go", defaults != null && defaults.Users == 0,
                    "users=" + (defaults != null ? defaults.Users : -1));
            }
            finally
            {
                mod.Sounds.Remove("selftest_steps_own");
                mod.Sounds.Remove("selftest_steps_defaults");
            }

            // The wench example (when installed with the examples): parses, and anpc_spawn brings her with her sprites and sounds.
            NpcDefinition wench = mod.Catalog.Generics.Find(delegate (NpcDefinition t) { return t.Folder == "wench"; });
            if (wench != null)
            {
                LoadedSoundSet wenchSounds = mod.Sounds.For("wench");
                string wenchWarnings = string.Join("; ", mod.Catalog.Messages.FindAll(delegate (string m) { return m.StartsWith("wench/"); }).ToArray());
                int wenchFiles = 0;
                bool wenchPools = wenchSounds != null;
                foreach (string pool in new[] { SoundPools.Greet, SoundPools.Hit, SoundPools.Death, SoundPools.Step })
                    wenchPools = wenchPools && wenchSounds.Files.ContainsKey(pool);
                if (wenchSounds != null)
                    foreach (List<string> f in wenchSounds.Files.Values)
                        wenchFiles += f.Count;
                List<LoadedSpriteSet> wenchSets = mod.Sprites.For("wench");
                bool wenchSheets = wenchSets.Count == 1;
                foreach (string sheet in new[] { "idle_1", "walk_1", "attack_1", "hit_1", "death" })
                    wenchSheets = wenchSheets && wenchSets[0].Set.Animations.ContainsKey(sheet);
                Check("the wench example parses with her sprites, sounds and settings",
                    wenchSheets && wenchPools && wench.Sounds != null && wench.Sounds.GreetChance == 70 && wenchWarnings.Length == 0,
                    "sets=" + mod.Sprites.For("wench").Count + ", sounds=" + (wenchSounds != null ? wenchSounds.Summary() : "none") +
                    ", warnings: " + wenchWarnings);
                string spawnError;
                NpcBrain w = mod.SpawnInFront(wench, new List<NpcDefinition> { wench }, out spawnError);
                if (w != null)
                {
                    ids.Add(w.Id);
                    mod.Placed.Remove(w.Id);      // a test person: not kept in the save
                    NpcSprite wSprite = w.GetComponent<NpcSprite>();
                    float wenchUntil = Time.time + 5f;
                    while (Time.time < wenchUntil && ((wSprite != null && wSprite.Pending) || (wenchSounds != null && !wenchSounds.IsReady)))
                        yield return null;
                    yield return null;
                    int clips = 0;
                    if (wenchSounds != null)
                        foreach (List<AudioClip> c in wenchSounds.Clips.Values)
                            clips += c.Count;
                    Check("anpc_spawn wench shows her sprite set and loads all her sounds",
                        wSprite != null && wSprite.Showing && wSprite.ChosenSet != null && wSprite.ChosenSet.Label == "wench/sprites" &&
                        clips == wenchFiles && clips > 0,
                        "showing=" + (wSprite != null && wSprite.Showing) + ", set=" + (wSprite != null && wSprite.ChosenSet != null ? wSprite.ChosenSet.Label : "none") +
                        ", clips=" + clips);
                    NpcBrain.Discard(w);
                }
                else
                {
                    Check("anpc_spawn wench shows her sprite set and loads all her sounds", false, "not spawned: " + spawnError);
                }
            }
            // Optional sample check (works when the mod is installed without its examples): the guard's shipped sounds.
            LoadedSoundSet guard = mod.Sounds.For("castle_guard_1");
            if (guard != null)
            {
                string problem = null;
                AudioClip sample = null;
                if (guard.Files.ContainsKey(SoundPools.Greet))
                    sample = SoundLibrary.LoadClip(guard.Files[SoundPools.Greet][0], out problem);
                Check("castle_guard_1 sample sounds index and decode (OGG)",
                    guard.Pools.Count == 6 && guard.Files.ContainsKey(SoundPools.Step) && guard.Files.ContainsKey(SoundPools.Talk) && sample != null,
                    guard.Summary() + (problem != null ? ", " + problem : ""));
                if (sample != null)
                    UnityEngine.Object.Destroy(sample);
            }
            yield return Settle;
        }

        /// <summary>A touch spell of 3-4 health damage by the caster (null: none).</summary>
        static EntityEffectBundle DamageSpell(DaggerfallEntityBehaviour caster)
        {
            EffectSettings settings = BaseEntityEffect.DefaultEffectSettings();
            settings.MagnitudeBaseMin = 3;
            settings.MagnitudeBaseMax = 4;
            settings.MagnitudePlusMin = 0;
            settings.MagnitudePlusMax = 0;
            EffectBundleSettings spell = new EffectBundleSettings
            {
                Version = EntityEffectBroker.CurrentSpellVersion,
                BundleType = BundleTypes.Spell,
                TargetType = TargetTypes.ByTouch,
                ElementType = ElementTypes.Magic,
                Name = "selftest damage",
                Effects = new[] { new EffectEntry(DamageHealth.EffectKey, settings) },
            };
            return new EntityEffectBundle(spell, caster);
        }

        /// <summary>A short 440 Hz tone as a 16-bit mono WAV file.</summary>
        static byte[] ToneWav(float seconds)
        {
            const int rate = 22050;
            int samples = (int)(rate * seconds);
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter w = new BinaryWriter(ms))
            {
                w.Write(new[] { 'R', 'I', 'F', 'F' });
                w.Write(36 + samples * 2);
                w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                w.Write(16);
                w.Write((short)1);
                w.Write((short)1);
                w.Write(rate);
                w.Write(rate * 2);
                w.Write((short)2);
                w.Write((short)16);
                w.Write(new[] { 'd', 'a', 't', 'a' });
                w.Write(samples * 2);
                for (int i = 0; i < samples; i++)
                    w.Write((short)(Math.Sin(2 * Math.PI * 440 * i / rate) * 8000));
                w.Flush();
                return ms.ToArray();
            }
        }
    }
}

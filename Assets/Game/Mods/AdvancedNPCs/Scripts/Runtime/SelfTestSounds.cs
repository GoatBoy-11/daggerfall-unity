using System;
using System.Collections;
using System.IO;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
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
                NpcDefinition d = TestDefinition("selftest_voice", Bravery.Brave, location, player, 0f);
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

            // Optional sample check (works when the mod is installed without its examples): the guard's shipped sounds.
            LoadedSoundSet guard = mod.Sounds.For("castle_guard_1");
            if (guard != null)
            {
                string problem = null;
                AudioClip sample = null;
                if (guard.Files.ContainsKey(SoundPools.Greet))
                    sample = SoundLibrary.LoadClip(guard.Files[SoundPools.Greet][0], out problem);
                Check("castle_guard_1 sample sounds index and decode (OGG)",
                    guard.Pools.Count == 5 && guard.Files.ContainsKey(SoundPools.Step) && sample != null,
                    guard.Summary() + (problem != null ? ", " + problem : ""));
                if (sample != null)
                    UnityEngine.Object.Destroy(sample);
            }
            yield return Settle;
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

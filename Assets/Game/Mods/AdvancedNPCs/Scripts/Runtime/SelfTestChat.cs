using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>Self-test: conversations between ANPCs (spec conversations).</summary>
    public partial class SelfTest
    {
        static readonly string[] ChatFolders = { "selftest_chat_a", "selftest_chat_b", "selftest_chat_c", "selftest_chat_d", "selftest_chat_mute", "selftest_chat_plain", "selftest_chat_quiet", "selftest_chat_ghost", "selftest_chat_enemy_a", "selftest_chat_enemy_b", "selftest_chat_gender" };

        IEnumerator ChatSteps(DaggerfallLocation location, Transform player)
        {
            DaggerfallEntityBehaviour playerBehaviour = GameManager.Instance.PlayerEntityBehaviour;

            // Every installed example converses: "conversation" on and talk sounds to pick from (talk, or talk_male and
            // talk_female for a template whose gender is picked at random).
            string[] talkers = { "argonian_female_acrobat_1", "argonian_staff_fighter_1", "breton_nightblade", "castle_guard_1",
                "dunmer_citizen_1", "dunmer_mercenary", "dunmer_spellblade", "iron_sentinel", "khajiit_citizen_1",
                "nord_barbarian_1", "ohmes_knight_1", "orc_weakling", "wench", "baker", "cooper", "smith" };
            string[] eitherGender = { "bandit", "commoner", "patron", "spellcaster" };
            List<NpcDefinition> examples = new List<NpcDefinition>(mod.Catalog.ById.Values);
            examples.AddRange(mod.Catalog.Generics);
            List<string> silent = new List<string>();
            int found = 0;
            foreach (string folder in talkers)
            {
                NpcDefinition d = examples.Find(delegate (NpcDefinition e) { return e.Folder == folder; });
                if (d == null)
                    continue;       // not installed
                found++;
                if (!d.Conversation.Enabled || TalkFiles(folder, SoundPools.Talk) < 2)
                    silent.Add(folder);
            }
            foreach (string folder in eitherGender)
            {
                NpcDefinition d = examples.Find(delegate (NpcDefinition e) { return e.Folder == folder; });
                if (d == null)
                    continue;
                found++;
                if (!d.Conversation.Enabled || TalkFiles(folder, SoundPools.TalkMale) < 2 || TalkFiles(folder, SoundPools.TalkFemale) < 2)
                    silent.Add(folder);
            }
            Check("every example converses (conversation on, 2+ talk sounds, per gender for random-gender templates)",
                found > 0 && silent.Count == 0, found + " installed; without: " + string.Join(", ", silent.ToArray()));

            // Every sound file of the installed examples decodes (an .ogg with a cover picture, say, does not).
            List<LoadedSoundSet> exampleSets = new List<LoadedSoundSet>();
            List<string> exampleFolders = new List<string>(talkers);
            exampleFolders.AddRange(eitherGender);
            foreach (string folder in exampleFolders)
            {
                LoadedSoundSet sounds = mod.Sounds.For(folder);
                if (sounds == null)
                    continue;
                sounds.Acquire();
                exampleSets.Add(sounds);
            }
            float decodeUntil = Time.time + 120f;
            while (Time.time < decodeUntil && exampleSets.Exists(delegate (LoadedSoundSet s) { return !s.IsReady; }))
                yield return null;
            List<string> undecoded = new List<string>();
            foreach (LoadedSoundSet s in exampleSets)
            {
                foreach (KeyValuePair<string, List<string>> pool in s.Files)
                {
                    List<AudioClip> clips;
                    int loaded = s.Clips.TryGetValue(pool.Key, out clips) ? clips.FindAll(delegate (AudioClip c) { return c != null; }).Count : 0;
                    if (!s.IsReady || loaded != pool.Value.Count)
                        undecoded.Add(s.Label + " " + pool.Key + " " + loaded + "/" + pool.Value.Count);
                }
                s.Release();
            }
            Check("every sound of the installed examples decodes", exampleSets.Count > 0 && undecoded.Count == 0,
                exampleSets.Count + " sets; not decoded: " + string.Join(", ", undecoded.ToArray()));

            string root = Path.Combine(Application.temporaryCachePath, "anpc_selftest_chat");
            string talkDir = Path.Combine(root, "Sounds");
            string muteDir = Path.Combine(Path.Combine(root, "mute"), "Sounds");
            string quietDir = Path.Combine(Path.Combine(root, "quiet"), "Sounds");
            string genderDir = Path.Combine(Path.Combine(root, "gender"), "Sounds");
            Directory.CreateDirectory(genderDir);
            foreach (string n in new[] { "talk_male_1", "talk_female_1", "talk_female_2" })
                File.WriteAllBytes(Path.Combine(genderDir, n + ".wav"), ToneWav(0.3f));
            Directory.CreateDirectory(talkDir);
            Directory.CreateDirectory(muteDir);
            Directory.CreateDirectory(quietDir);
            foreach (string n in new[] { "talk_1", "talk_2" })
                File.WriteAllBytes(Path.Combine(talkDir, n + ".wav"), ToneWav(0.3f));
            File.WriteAllBytes(Path.Combine(talkDir, "greet_1.wav"), ToneWav(0.6f));     // longer than the start delay
            File.WriteAllBytes(Path.Combine(muteDir, "greet_1.wav"), ToneWav(0.1f));
            File.WriteAllBytes(Path.Combine(quietDir, "talk_1.wav"), ToneWav(0.3f));
            foreach (string f in ChatFolders)
                mod.Sounds.Add(f, SoundLibrary.IndexSet(f + "/Sounds", f == "selftest_chat_mute" ? muteDir : f == "selftest_chat_quiet" ? quietDir :
                    f == "selftest_chat_gender" ? genderDir : talkDir));
            mod.Sprites.Add("selftest_chat_a", TestSpriteSet(false, "idle", "walk", "talk"));
            mod.Sprites.Add("selftest_chat_b", TestSpriteSet(false, "idle", "walk"));
            mod.Sprites.Add("selftest_chat_c", TestSpriteSet(false, "idle", "walk", "talk"));
            mod.Sprites.Add("selftest_chat_d", TestSpriteSet(false, "idle", "walk", "talk"));
            mod.Sprites.Add("selftest_chat_mute", TestSpriteSet(false, "idle", "walk"));
            mod.Sprites.Add("selftest_chat_quiet", TestSpriteSet(false, "idle", "walk", "talk"));
            mod.Sprites.Add("selftest_chat_ghost", TestSpriteSet(false, "idle", "walk", "talk"));
            mod.Sprites.Add("selftest_chat_enemy_a", TestSpriteSet(false, "idle", "walk", "talk"));
            mod.Sprites.Add("selftest_chat_enemy_b", TestSpriteSet(false, "idle", "walk", "talk"));
            // selftest_chat_plain: talk sounds but no sprite set (the vanilla class sprite).
            NpcGreeter.ReactionOverride = "neutral";    // nobody greets the player
            NpcGreeter.ChanceOverride = 1f;
            NpcChat.Memory.Clear();
            DiscardTestPeople();
            Vector3 open = OpenDirection(player, 16f);
            Vector3 side = Vector3.Cross(Vector3.up, open).normalized;
            try
            {
                NpcBrain a = SpawnTalker("selftest_chat_a", true, location, player, 100);
                NpcBrain b = SpawnTalker("selftest_chat_b", true, location, player, 100);
                Vector3 spot = player.position + open * 8f;
                a.Teleport(spot);
                b.Teleport(spot + side * 2.5f);
                FaceToward(a, spot + side * 2.5f);
                FaceToward(b, spot);
                NpcChat ca = a.GetComponent<NpcChat>();
                NpcChat cb = b.GetComponent<NpcChat>();

                // Greeting starts a conversation (once both have their sounds and sprites).
                float until = Time.time + 6f;
                while (Time.time < until && !(ca != null && ca.Talking))
                    yield return null;
                bool started = ca != null && cb != null && ca.Talking && cb.Talking && ca.Partner == cb && cb.Partner == ca &&
                               ca.IsInitiator != cb.IsInitiator && ca.Session == cb.Session;
                Check("two ANPCs that greet each other start a conversation", started,
                    "a=" + ChatState(ca) + ", b=" + ChatState(cb) + ", " + a.Status + " / " + b.Status);
                if (!started)
                    yield break;

                NpcGreeter ga = a.GetComponent<NpcGreeter>();
                NpcGreeter gb = b.GetComponent<NpcGreeter>();
                int greetsA = ga.Greetings, greetsB = gb.Greetings;
                int lines = ca.Session.Lines;
                NpcChat first = ca.IsInitiator ? ca : cb;
                NpcSprite sa = a.GetComponent<NpcSprite>();
                NpcSprite sb = b.GetComponent<NpcSprite>();
                NpcVoice va = a.GetComponent<NpcVoice>();
                NpcVoice vb = b.GetComponent<NpcVoice>();
                int talkA = va.Played(SoundPools.Talk), talkB = vb.Played(SoundPools.Talk);
                int greetSounds = va.Played(SoundPools.Greet) + vb.Played(SoundPools.Greet);
                bool firstLineSeen = false, greetOverlap = false;
                bool facedChecked = false, faced = false;
                float facedAt = Time.time + 1f;
                List<string> aAnims = new List<string>(), bAnims = new List<string>();
                until = Time.time + lines * 1.5f + 3f;
                while (Time.time < until && ca.Talking)
                {
                    ChatSession s = ca.Session;
                    if (!firstLineSeen && va.Played(SoundPools.Talk) + vb.Played(SoundPools.Talk) > talkA + talkB)
                    {
                        firstLineSeen = true;
                        greetOverlap = va.PlayingGreet || vb.PlayingGreet;
                    }
                    if (s != null && s.Speaking)
                    {
                        NpcChat speaker = s.Speaker == 0 ? first : first.Partner;
                        if (speaker == ca)
                            aAnims.Add(sa.CurrentAnimation);
                        else if (speaker == cb)
                            bAnims.Add(sb.CurrentAnimation);
                    }
                    if (!facedChecked && Time.time >= facedAt)
                    {
                        facedChecked = true;
                        faced = Facing(a, b.transform.position) < 25f && Facing(b, a.transform.position) < 25f;
                    }
                    yield return null;
                }
                Check("the first line waits for the greeting sound", greetSounds > 0 && firstLineSeen && !greetOverlap,
                    "greet sounds " + greetSounds + ", first line seen " + firstLineSeen + ", overlap " + greetOverlap);
                Check("talkers turn to face each other", faced,
                    "angles " + Facing(a, b.transform.position).ToString("0") + "/" + Facing(b, a.transform.position).ToString("0"));
                Check("the speaker shows its talk sheet, or idle without one",
                    aAnims.Count > 0 && bAnims.Count > 0 && AllStartWith(aAnims, "talk") && AllStartWith(bAnims, "idle"),
                    "a: " + string.Join(",", Distinct(aAnims)) + "; b: " + string.Join(",", Distinct(bAnims)));
                int saidA = va.Played(SoundPools.Talk) - talkA, saidB = vb.Played(SoundPools.Talk) - talkB;
                int saidFirst = first == ca ? saidA : saidB;
                Check("lines alternate and the conversation ends after its lines",
                    !ca.Talking && !cb.Talking && saidA + saidB == lines && saidFirst == (lines + 1) / 2,
                    "lines=" + lines + ", a said " + saidA + ", b said " + saidB + ", talking " + ca.Talking + "/" + cb.Talking);
                Check("a talking ANPC greets no one", ga.Greetings == greetsA && gb.Greetings == greetsB,
                    "greetings a " + greetsA + "->" + ga.Greetings + ", b " + greetsB + "->" + gb.Greetings);

                first.OnGreeted(first == ca ? b : a);
                yield return null;
                Check("the same pair waits 2 game hours", !ca.Talking && !cb.Talking, "talking " + ca.Talking + "/" + cb.Talking);

                // anpc_chat: the ANPC in front of the player and the nearest other within 4 m.
                a.Teleport(player.position + open * 2f);
                b.Teleport(player.position + open * 3.5f);
                yield return Settle;
                string said = AdvancedNpcsMod.ChatCommand();
                Check("anpc_chat starts a conversation at once", said.Contains("are talking") && ca.Talking, said);
                string info = AdvancedNpcsMod.InfoCommand(a.Id);
                Check("anpc_info shows the conversation", info.Contains("conversation: chance 100%") && info.Contains("talking with"), info);
                ca.End(true);

                // A hit cuts the line short and ends it for both.
                yield return Settle;
                string why = ca.TryStart(cb, true);
                until = Time.time + 3f;
                while (Time.time < until && why == null && !(va.TalkPlaying || vb.TalkPlaying))
                    yield return null;
                bool wasTalking = va.TalkPlaying || vb.TalkPlaying;
                PlayerHit(b, playerBehaviour, 1);
                yield return null;
                yield return null;
                Check("a hit ends the conversation and cuts the line",
                    why == null && wasTalking && !ca.Talking && !cb.Talking && !va.TalkPlaying && !vb.TalkPlaying,
                    "start=" + (why ?? "ok") + ", was talking " + wasTalking + ", talking " + ca.Talking + "/" + cb.Talking +
                    ", playing " + va.TalkPlaying + "/" + vb.TalkPlaying);
                NpcBrain.Discard(b);
                NpcBrain.Discard(a);
                yield return Settle;

                NpcBrain c = SpawnTalker("selftest_chat_c", true, location, player);
                NpcBrain d = SpawnTalker("selftest_chat_d", true, location, player);
                c.Teleport(spot);
                d.Teleport(spot + side * 2f);
                NpcChat cc = c.GetComponent<NpcChat>();
                NpcChat cd = d.GetComponent<NpcChat>();
                until = Time.time + 4f;
                while (Time.time < until && (cc.WhyNot() != null || cd.WhyNot() != null) && !cc.Talking && !cd.Talking)
                    yield return null;
                cc.End(true);
                cd.End(true);

                // Moving apart ends it.
                why = cc.TryStart(cd, true);
                yield return null;
                d.Teleport(spot + open * 10f);
                yield return null;
                yield return null;
                Check("moving apart ends the conversation", why == null && !cc.Talking && !cd.Talking,
                    "start=" + (why ?? "ok") + ", talking " + cc.Talking + "/" + cd.Talking);

                // The player talking to one ends it.
                d.Teleport(spot + side * 2f);
                yield return Settle;
                why = cc.TryStart(cd, true);
                yield return null;
                bool opened = c.GetComponent<NpcTalk>().TryTalk();
                Check("talking to one of them ends the conversation", why == null && opened && !cc.Talking && !cd.Talking,
                    "start=" + (why ?? "ok") + ", opened " + opened + ", talking " + cc.Talking + "/" + cd.Talking);
                CloseTalkWindow();
                yield return Settle;

                // Unloading the one who started ends it for the other.
                why = cc.TryStart(cd, true);
                yield return null;
                NpcBrain.Discard(c);
                yield return null;
                yield return null;
                Check("unloading the starter ends the conversation for the other", why == null && !cd.Talking,
                    "start=" + (why ?? "ok") + ", d talking " + cd.Talking);

                // Who may not talk.
                NpcBrain mute = SpawnTalker("selftest_chat_mute", true, location, player);
                NpcBrain plain = SpawnTalker("selftest_chat_plain", true, location, player);
                NpcBrain shy = SpawnTalker("selftest_chat_shy", false, location, player);
                mute.Teleport(spot + side * -2f);
                NpcVoice vp = plain.GetComponent<NpcVoice>();
                until = Time.time + 3f;
                while (Time.time < until && vp.TalkClips == 0)
                    yield return null;
                yield return Settle;
                string muteWhy = cd.TryStart(mute.GetComponent<NpcChat>(), true);
                string plainWhy = plain.GetComponent<NpcChat>().WhyNot();
                // A partner that was never active (an unloaded town's people): no conversation, no exception.
                GameObject holder = new GameObject("selftest_chat_off");
                holder.SetActive(false);
                holder.transform.SetParent(location.transform, false);
                NpcDefinition ghostDef = TestDefinition("selftest_chat_ghost", Bravery.Normal, location, player, OutOfGreeting);
                ghostDef.Conversation = new ConversationSettings { Enabled = true, Chance = 100 };
                NpcBrain ghost = spawner.SpawnTest(NpcInstance.ForUnique(ghostDef, "Breton"), holder.transform);
                string ghostWhy = null, ghostError = null;
                try
                {
                    ghostWhy = cd.TryStart(ghost.GetComponent<NpcChat>(), true);
                }
                catch (System.Exception e)
                {
                    ghostError = e.GetType().Name;
                }
                Check("an ANPC that is not active cannot be talked to",
                    ghostError == null && ghostWhy != null && ghostWhy.Contains("not here") && !cd.Talking,
                    "why=" + ghostWhy + ", error=" + ghostError);
                NpcBrain.Discard(ghost);
                Object.Destroy(holder);

                // Talk files but no greet files: greets other ANPCs silently, never the player.
                NpcBrain quiet = SpawnTalker("selftest_chat_quiet", true, location, player);
                quiet.Teleport(player.position + open * 2.5f);
                FaceToward(quiet, player.position);
                NpcGreeter.ReactionOverride = "loves";
                float quietUntil = Time.time + 2f;
                while (Time.time < quietUntil)
                    yield return null;
                NpcGreeter.ReactionOverride = "neutral";
                NpcGreeter quietGreeter = quiet.GetComponent<NpcGreeter>();
                Check("a talker without greet files never greets the player", quietGreeter != null && quietGreeter.Greetings == 0,
                    quietGreeter == null ? "no greeter" : "greetings " + quietGreeter.Greetings);

                Check("no talk sounds or no opt-in: no conversation",
                    muteWhy != null && muteWhy.Contains("no talk sounds") && shy.GetComponent<NpcChat>() == null,
                    "mute=" + muteWhy + ", shy has NpcChat " + (shy.GetComponent<NpcChat>() != null));

                // The vanilla class sprite talks too (it just shows its idle sprite while it does).
                NpcChat plainChat = plain.GetComponent<NpcChat>();
                quiet.Teleport(plain.transform.position + side * 2f);
                yield return null;
                // It may already be talking with the quiet one (it greeted it): that counts as well.
                string plainStart = plainChat.Talking ? null : plainChat.TryStart(quiet.GetComponent<NpcChat>(), true);
                Check("an ANPC with the vanilla sprite converses", plainWhy == null && plainStart == null && plainChat.Talking &&
                    plain.GetComponent<NpcGreeter>() != null, "why=" + plainWhy + ", start=" + plainStart + ", greeter " +
                    (plain.GetComponent<NpcGreeter>() != null));
                plainChat.End(true);

                // talk_male / talk_female: an ANPC whose gender is picked at random speaks with its own gender's lines.
                NpcDefinition gf = TestDefinition("selftest_chat_gender", Bravery.Normal, location, player, OutOfGreeting);
                gf.Gender = "Female";
                gf.Conversation = new ConversationSettings { Enabled = true, Chance = 100 };
                NpcBrain female = SpawnDefinition(gf, location);
                NpcDefinition gm = TestDefinition("selftest_chat_gender", Bravery.Normal, location, player, OutOfGreeting);
                gm.Gender = "Male";
                gm.Conversation = new ConversationSettings { Enabled = true, Chance = 100 };
                NpcBrain male = SpawnDefinition(gm, location);
                NpcVoice fv = female.GetComponent<NpcVoice>(), mv = male.GetComponent<NpcVoice>();
                until = Time.time + 3f;
                while (Time.time < until && (fv.Set == null || !fv.Set.IsReady || mv.Set == null || !mv.Set.IsReady))
                    yield return null;
                Check("talk_female / talk_male lines go to that gender only",
                    fv.TalkPoolName == SoundPools.TalkFemale && fv.TalkClips == 2 && mv.TalkPoolName == SoundPools.TalkMale && mv.TalkClips == 1,
                    "female " + fv.TalkPoolName + " x" + fv.TalkClips + ", male " + mv.TalkPoolName + " x" + mv.TalkClips);
                NpcBrain.Discard(female);
                NpcBrain.Discard(male);

                // Enemies of the player talk among themselves while DFU's AI has no one for them to fight. Nobody else
                // about: a calm test ANPC would go for them.
                DiscardTestPeople();
                yield return null;
                PlayerEntity playerEntity = GameManager.Instance.PlayerEntity;
                bool noTarget = playerEntity.NoTargetMode;
                playerEntity.NoTargetMode = true;       // the AI ignores the player: they stay idle
                try
                {
                    NpcBrain ea = SpawnEnemyTalker("selftest_chat_enemy_a", location, player);
                    NpcBrain eb = SpawnEnemyTalker("selftest_chat_enemy_b", location, player);
                    Vector3 enemySpot = player.position + open * 12f + side * 4f;
                    ea.Teleport(enemySpot);
                    eb.Teleport(enemySpot + side * 2.5f);
                    FaceToward(ea, enemySpot + side * 2.5f);
                    FaceToward(eb, enemySpot);
                    NpcChat cea = ea.GetComponent<NpcChat>(), ceb = eb.GetComponent<NpcChat>();
                    until = Time.time + 8f;
                    while (Time.time < until && !cea.Talking && !ceb.Talking)
                        yield return null;
                    Check("two enemies with no one to fight greet each other and talk",
                        ea.IsEnemy && eb.IsEnemy && (cea.Talking || ceb.Talking) && cea.Partner == ceb,
                        "enemy " + ea.IsEnemy + "/" + eb.IsEnemy + ", idle " + ea.IdleEnemy + "/" + eb.IdleEnemy + ", a=" + ChatState(cea) +
                        ", b=" + ChatState(ceb) + ", " + ea.Status);
                    cea.End(false);
                    yield return null;

                    // Something to fight (here the player, hitting one): the conversation ends.
                    string again = cea.TryStart(ceb, true);
                    yield return new WaitForSeconds(0.5f);
                    bool enemiesTalked = cea.Talking;
                    playerEntity.NoTargetMode = noTarget;
                    PlayerHit(eb, playerBehaviour, 1);
                    until = Time.time + 2f;
                    while (Time.time < until && cea.Talking)
                        yield return null;
                    Check("an enemy that gets someone to fight ends the conversation",
                        again == null && enemiesTalked && !cea.Talking && !ceb.Talking && !eb.IdleEnemy,
                        "start=" + (again ?? "ok") + ", was talking " + enemiesTalked + ", talking " + cea.Talking + "/" + ceb.Talking +
                        ", idle " + eb.IdleEnemy + ", " + eb.Status);
                    NpcBrain.Discard(ea);
                    NpcBrain.Discard(eb);
                }
                finally
                {
                    playerEntity.NoTargetMode = noTarget;
                }
            }
            finally
            {
                NpcGreeter.ReactionOverride = null;
                NpcGreeter.ChanceOverride = -1f;
                NpcChat.Memory.Clear();
                foreach (string f in ChatFolders)
                {
                    mod.Sounds.Remove(f);
                    mod.Sprites.Remove(f);
                }
            }
            DiscardTestPeople();
        }

        int TalkFiles(string folder, string pool)
        {
            LoadedSoundSet sounds = mod.Sounds.For(folder);
            List<string> files;
            return sounds != null && sounds.Files.TryGetValue(pool, out files) ? files.Count : 0;
        }

        NpcBrain SpawnEnemyTalker(string id, DaggerfallLocation location, Transform player)
        {
            NpcDefinition d = TestDefinition(id, Bravery.Brave, location, player, OutOfGreeting);
            d.Hostile = true;
            d.Conversation = new ConversationSettings { Enabled = true, Chance = 100 };
            d.Sounds = new SoundSettings { GreetChance = 100 };
            return SpawnDefinition(d, location);
        }

        NpcBrain SpawnTalker(string id, bool talks, DaggerfallLocation location, Transform player)
        {
            return SpawnTalker(id, talks, location, player, 50);
        }

        NpcBrain SpawnTalker(string id, bool talks, DaggerfallLocation location, Transform player, int greetChance)
        {
            NpcDefinition d = TestDefinition(id, Bravery.Normal, location, player, OutOfGreeting);
            d.Conversation = new ConversationSettings { Enabled = talks, Chance = 100 };
            d.Sounds = new SoundSettings { GreetChance = greetChance };
            return SpawnDefinition(d, location);
        }

        static string ChatState(NpcChat c)
        {
            return c == null ? "no NpcChat" : (c.Talking ? "talking" : "not talking, " + (c.WhyNot() ?? "could talk"));
        }

        /// <summary>Degrees between where b faces and the direction to target, on the ground.</summary>
        static float Facing(NpcBrain b, Vector3 target)
        {
            Vector3 to = target - b.transform.position;
            to.y = 0;
            Vector3 fwd = b.transform.forward;
            fwd.y = 0;
            return to.sqrMagnitude < 0.0001f ? 0f : Vector3.Angle(fwd, to);
        }

        static bool AllStartWith(List<string> names, string prefix)
        {
            foreach (string n in names)
            {
                if (n == null || !n.StartsWith(prefix, System.StringComparison.Ordinal))
                    return false;
            }
            return true;
        }

        static string[] Distinct(List<string> names)
        {
            List<string> d = new List<string>();
            foreach (string n in names)
            {
                if (!d.Contains(n ?? "null"))
                    d.Add(n ?? "null");
            }
            return d.ToArray();
        }
    }
}

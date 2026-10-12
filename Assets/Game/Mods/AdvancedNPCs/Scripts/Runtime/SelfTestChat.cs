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
        static readonly string[] ChatFolders = { "selftest_chat_a", "selftest_chat_b", "selftest_chat_c", "selftest_chat_d", "selftest_chat_mute", "selftest_chat_plain", "selftest_chat_quiet", "selftest_chat_ghost" };

        IEnumerator ChatSteps(DaggerfallLocation location, Transform player)
        {
            DaggerfallEntityBehaviour playerBehaviour = GameManager.Instance.PlayerEntityBehaviour;
            string root = Path.Combine(Application.temporaryCachePath, "anpc_selftest_chat");
            string talkDir = Path.Combine(root, "Sounds");
            string muteDir = Path.Combine(Path.Combine(root, "mute"), "Sounds");
            string quietDir = Path.Combine(Path.Combine(root, "quiet"), "Sounds");
            Directory.CreateDirectory(talkDir);
            Directory.CreateDirectory(muteDir);
            Directory.CreateDirectory(quietDir);
            foreach (string n in new[] { "talk_1", "talk_2" })
                File.WriteAllBytes(Path.Combine(talkDir, n + ".wav"), ToneWav(0.3f));
            File.WriteAllBytes(Path.Combine(talkDir, "greet_1.wav"), ToneWav(0.6f));     // longer than the start delay
            File.WriteAllBytes(Path.Combine(muteDir, "greet_1.wav"), ToneWav(0.1f));
            File.WriteAllBytes(Path.Combine(quietDir, "talk_1.wav"), ToneWav(0.3f));
            foreach (string f in ChatFolders)
                mod.Sounds.Add(f, SoundLibrary.IndexSet(f + "/Sounds", f == "selftest_chat_mute" ? muteDir : f == "selftest_chat_quiet" ? quietDir : talkDir));
            mod.Sprites.Add("selftest_chat_a", TestSpriteSet(false, "idle", "walk", "talk"));
            mod.Sprites.Add("selftest_chat_b", TestSpriteSet(false, "idle", "walk"));
            mod.Sprites.Add("selftest_chat_c", TestSpriteSet(false, "idle", "walk", "talk"));
            mod.Sprites.Add("selftest_chat_d", TestSpriteSet(false, "idle", "walk", "talk"));
            mod.Sprites.Add("selftest_chat_mute", TestSpriteSet(false, "idle", "walk"));
            mod.Sprites.Add("selftest_chat_quiet", TestSpriteSet(false, "idle", "walk", "talk"));
            mod.Sprites.Add("selftest_chat_ghost", TestSpriteSet(false, "idle", "walk", "talk"));
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

                Check("no talk sounds, a vanilla sprite or no opt-in: no conversation",
                    muteWhy != null && muteWhy.Contains("no talk sounds") && plainWhy == "vanilla sprite" && shy.GetComponent<NpcChat>() == null,
                    "mute=" + muteWhy + ", plain=" + plainWhy + ", shy has NpcChat " + (shy.GetComponent<NpcChat>() != null));
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

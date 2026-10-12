# ANPC Conversations Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Two opted-in ANPCs that greet each other sometimes hold a 4–8 line audio-only conversation, taking turns with `talk` sheets and `talk` sounds.

**Architecture:** Pure Core rules (`ChatRules`, `ChatMemory`, `ChatSession`, settings parsing) unit-tested in the Unity 6 host; a runtime `NpcChat` component per opted-in ANPC, started by `NpcGreeter`, drives the session through new talk APIs on `NpcVoice` and `NpcSprite`. Console `anpc_chat`, `anpc_info` lines, in-game self-test, example content.

**Tech Stack:** C# (DFU runtime compiler subset: no nested enums, no enum fields across files), Unity 2019.4 / DFU 1.9.2, NUnit via `Tools~/run-tests.sh`.

**Spec:** `docs/superpowers/specs/2026-10-12-advanced-npcs-conversations-design.md`

## Global Constraints

- npc.json key `"conversation"`: `true`/`false`/`null` or `{ "enabled": bool (default true inside the object), "chance": 0–100 whole (default 50) }`; missing = off.
- Lines 4–8 per conversation, pause 0.4–1.0 s, pair cooldown 2 game hours (unordered pair, memory only), end when apart > 6 m.
- Sound pool and sprite state name: `talk` (`talk_<n>` variants). Talk sounds never roll a chance; never played outside conversations.
- Talk sound: own voice source, 3D, DFU volume, `soundPitch` ± `soundPitchVariation`; turn = clip length ÷ pitch.
- Eligible: opted in, calm, not hostile/enemy, not already talking, ANPC sounds setting = ANPC, loaded talk clips > 0, custom sprite set loaded.
- A talking ANPC greets no one.
- Nothing saved. Mod version 0.20.0.
- Comment/README style: plain English, short sentences, spec references like `(spec conversations §2)`.

## Review Focus

- Initiator destroyed/disabled mid-conversation (town unload, discard): partner must not stay `Talking` forever → `NpcChat.OnDisable` ends it for both (self-test check 11).
- Both ANPCs greet each other in the same check window: the second `TryStart` must fail cleanly ("already talking"), never two sessions → `Ineligible` unit test + `Talking` guard.
- Sound set still loading when the greeting fires: `TalkClips == 0` → no conversation, no error (eligibility).
- `anpc_reload_assets` replaces the talk set mid-conversation (fewer clips, last index out of range) → `NextClip` ignores `last >= count` (unit test).
- Game time going backwards (an earlier save loaded) with a pair on cooldown → `ChatMemory.CanStart` allows when now < last (unit test).

---

All paths below are under `Assets/Game/Mods/AdvancedNPCs/`. Unity writes `.meta` files for new `.cs` files on import (run-tests/build); commit them.

### Task 1: Core rules — ChatRules, ChatMemory, ChatSession

**Files:** Create `Scripts/Core/ChatRules.cs`; Test `Editor/Tests/ChatRulesTests.cs`.

**Interfaces — produces:** `ChatRules.MinLines/MaxLines/MinPause/MaxPause/CooldownHours/MaxApart/StartDistance`, `ChatRules.Lines(double)`, `ChatRules.Pause(double)`, `ChatRules.PairKey(string,string)`, `ChatRules.NextClip(int count, int last, double roll)`, `ChatRules.Ineligible(bool optedIn, bool alreadyTalking, bool calm, bool customVoice, int talkClips, bool customSprite)`; `ChatMemory.CanStart(string,double)`, `Started(string,double)`, `Clear()`; `ChatSession(int lines)`: `Lines`, `Line`, `Speaker`, `NeedsLine`, `Speaking`, `Over`, `BeginLine(double)`, `Tick(double dt, double pauseRoll)`, `End()`.

- [ ] **Step 1: Failing tests**

```csharp
using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    /// <summary>ANPC conversations (spec conversations).</summary>
    public class ChatRulesTests
    {
        [TestCase(0.0, 4)]
        [TestCase(0.5, 6)]
        [TestCase(0.999, 8)]
        [TestCase(1.0, 8)]
        public void Lines_FourToEight(double roll, int expected)
        {
            Assert.AreEqual(expected, ChatRules.Lines(roll));
        }

        [Test]
        public void Pause_FromPointFourToOne()
        {
            Assert.AreEqual(0.4, ChatRules.Pause(0.0), 1e-9);
            Assert.AreEqual(1.0, ChatRules.Pause(1.0), 1e-9);
        }

        [Test]
        public void PairKey_IgnoresOrder()
        {
            Assert.AreEqual(ChatRules.PairKey("bram", "anna"), ChatRules.PairKey("anna", "bram"));
            Assert.AreNotEqual(ChatRules.PairKey("bram", "anna"), ChatRules.PairKey("bram", "cora"));
        }

        [Test]
        public void NextClip_NeverRepeatsWithTwoOrMore()
        {
            for (int last = 0; last < 3; last++)
                foreach (double roll in new[] { 0.0, 0.3, 0.6, 0.99, 1.0 })
                {
                    int i = ChatRules.NextClip(3, last, roll);
                    Assert.AreNotEqual(last, i);
                    Assert.That(i, Is.InRange(0, 2));
                }
        }

        [Test]
        public void NextClip_OneClipAndNone()
        {
            Assert.AreEqual(0, ChatRules.NextClip(1, 0, 0.7));
            Assert.AreEqual(-1, ChatRules.NextClip(0, -1, 0.7));
        }

        [Test]
        public void NextClip_LastOutOfRange_IsIgnored()
        {
            Assert.That(ChatRules.NextClip(2, 5, 1.0), Is.InRange(0, 1));     // the set was reloaded with fewer clips
        }

        [Test]
        public void Ineligible_Reasons()
        {
            Assert.IsNull(ChatRules.Ineligible(true, false, true, true, 2, true));
            Assert.AreEqual("not opted in", ChatRules.Ineligible(false, false, true, true, 2, true));
            Assert.AreEqual("already talking", ChatRules.Ineligible(true, true, true, true, 2, true));
            Assert.AreEqual("not calm", ChatRules.Ineligible(true, false, false, true, 2, true));
            Assert.AreEqual("voices not ANPC", ChatRules.Ineligible(true, false, true, false, 2, true));
            Assert.AreEqual("no talk sounds", ChatRules.Ineligible(true, false, true, true, 0, true));
            Assert.AreEqual("vanilla sprite", ChatRules.Ineligible(true, false, true, true, 2, false));
        }

        [Test]
        public void Memory_CooldownTwoHours_PerPair()
        {
            ChatMemory m = new ChatMemory();
            string ab = ChatRules.PairKey("a", "b");
            Assert.IsTrue(m.CanStart(ab, 10.0));
            m.Started(ab, 10.0);
            Assert.IsFalse(m.CanStart(ab, 11.9));
            Assert.IsTrue(m.CanStart(ab, 12.0));
            Assert.IsTrue(m.CanStart(ChatRules.PairKey("a", "c"), 10.5));
        }

        [Test]
        public void Memory_TimeWentBack_Allows()
        {
            ChatMemory m = new ChatMemory();
            m.Started("a|b", 50.0);
            Assert.IsTrue(m.CanStart("a|b", 40.0));     // an earlier save was loaded
        }

        [Test]
        public void Session_Alternates_AndEndsAfterLastLine()
        {
            ChatSession s = new ChatSession(3);
            Assert.IsTrue(s.NeedsLine);
            Assert.AreEqual(0, s.Speaker);
            Assert.AreEqual(1, s.Line);
            s.BeginLine(1.0);
            Assert.IsTrue(s.Speaking);
            s.Tick(0.5, 0.0);
            Assert.IsFalse(s.NeedsLine);
            s.Tick(0.6, 0.0);               // the line is over; the 0.4 s pause starts (0.1 s already gone)
            Assert.IsFalse(s.NeedsLine);
            Assert.IsFalse(s.Speaking);
            s.Tick(0.3, 0.0);               // the pause is over
            Assert.IsTrue(s.NeedsLine);
            Assert.AreEqual(1, s.Speaker);
            Assert.AreEqual(2, s.Line);
            s.BeginLine(1.0);
            s.Tick(2.0, 1.0);               // the line and the whole pause in one step
            Assert.IsTrue(s.NeedsLine);
            Assert.AreEqual(0, s.Speaker);
            Assert.AreEqual(3, s.Line);
            s.BeginLine(0.5);
            s.Tick(0.5, 0.0);
            Assert.IsTrue(s.Over);          // no pause after the last line
            Assert.IsFalse(s.NeedsLine);
        }

        [Test]
        public void Session_NothingPlayed_Ends()
        {
            ChatSession s = new ChatSession(4);
            s.BeginLine(0);
            Assert.IsTrue(s.Over);
        }

        [Test]
        public void Session_End_StopsEverything()
        {
            ChatSession s = new ChatSession(4);
            s.BeginLine(1.0);
            s.End();
            Assert.IsTrue(s.Over);
            s.Tick(5.0, 0.0);
            Assert.IsFalse(s.NeedsLine);
            Assert.IsFalse(s.Speaking);
        }

        [Test]
        public void Session_TickWhileWaitingForLine_DoesNothing()
        {
            ChatSession s = new ChatSession(4);
            s.Tick(10.0, 0.0);
            Assert.IsTrue(s.NeedsLine);
            Assert.AreEqual(1, s.Line);
        }
    }
}
```

- [ ] **Step 2:** `bash Tools~/run-tests.sh` → compile error (`ChatRules` missing).
- [ ] **Step 3: Implement** `Scripts/Core/ChatRules.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace AdvancedNPCs.Core
{
    /// <summary>ANPC conversations (spec conversations): limits, rolls and who may talk. No Unity or DFU types.</summary>
    public static class ChatRules
    {
        public const int MinLines = 4, MaxLines = 8;
        public const double MinPause = 0.4, MaxPause = 1.0;
        public const double CooldownHours = 2.0;
        /// <summary>Metres apart at which a conversation ends (knocked back, teleported).</summary>
        public const float MaxApart = 6f;
        /// <summary>anpc_chat: the partner must be this close (the greeting distance).</summary>
        public const float StartDistance = GreetRules.Distance;

        public static int Lines(double roll01)
        {
            return Math.Min(MaxLines, MinLines + (int)(roll01 * (MaxLines - MinLines + 1)));
        }

        public static double Pause(double roll01)
        {
            return MinPause + (MaxPause - MinPause) * Math.Max(0.0, Math.Min(1.0, roll01));
        }

        public static string PairKey(string a, string b)
        {
            return string.CompareOrdinal(a, b) <= 0 ? a + "|" + b : b + "|" + a;
        }

        public static int NextClip(int count, int last, double roll01)
        {
            if (count <= 0)
                return -1;
            if (count == 1)
                return 0;
            if (last < 0 || last >= count)
                return Math.Min(count - 1, (int)(roll01 * count));
            int i = Math.Min(count - 2, (int)(roll01 * (count - 1)));
            return i >= last ? i + 1 : i;
        }

        public static string Ineligible(bool optedIn, bool alreadyTalking, bool calm, bool customVoice, int talkClips, bool customSprite)
        {
            if (!optedIn) return "not opted in";
            if (alreadyTalking) return "already talking";
            if (!calm) return "not calm";
            if (!customVoice) return "voices not ANPC";
            if (talkClips <= 0) return "no talk sounds";
            if (!customSprite) return "vanilla sprite";
            return null;
        }
    }

    public class ChatMemory
    {
        readonly Dictionary<string, double> last = new Dictionary<string, double>(StringComparer.Ordinal);

        public bool CanStart(string pair, double nowHours)
        {
            double at;
            return !last.TryGetValue(pair, out at) || nowHours < at || nowHours - at >= ChatRules.CooldownHours;
        }

        public void Started(string pair, double nowHours) { last[pair] = nowHours; }
        public void Clear() { last.Clear(); }
    }

    public class ChatSession
    {
        readonly int lines;
        int line = 1;
        bool needsLine = true, pausing, over;
        double left;

        public ChatSession(int lines) { this.lines = Math.Max(1, lines); }

        public int Lines { get { return lines; } }
        public int Line { get { return line; } }
        public int Speaker { get { return (line - 1) % 2; } }
        public bool NeedsLine { get { return needsLine && !over; } }
        public bool Speaking { get { return !over && !needsLine && !pausing; } }
        public bool Over { get { return over; } }

        public void BeginLine(double seconds)
        {
            if (!NeedsLine) return;
            if (seconds <= 0) { End(); return; }
            needsLine = false;
            pausing = false;
            left = seconds;
        }

        public void Tick(double dt, double pauseRoll01)
        {
            if (over || needsLine) return;
            left -= dt;
            if (left > 0) return;
            if (!pausing)
            {
                if (line >= lines) { over = true; return; }
                pausing = true;
                left += ChatRules.Pause(pauseRoll01);
                if (left > 0) return;
            }
            pausing = false;
            line++;
            needsLine = true;
        }

        public void End() { over = true; needsLine = false; }
    }
}
```

(Final code formats bodies over several lines with summaries, like the rest of Core.)

- [ ] **Step 4:** `bash Tools~/run-tests.sh` → all pass.
- [ ] **Step 5:** commit "AdvancedNPCs: conversation rules and session".

### Task 2: npc.json "conversation", talk names, schema

**Files:** Modify `Scripts/Core/ChatRules.cs` (`ConversationSettings`, `ChatRules.Read`), `Scripts/Core/NpcDefinition.cs` (`Conversation`), `Scripts/Core/DefinitionParser.cs` (SharedKeys + read after sounds), `Scripts/Core/SoundRules.cs` (`SoundPools.Talk` in `All`), `Scripts/Core/SpriteStates.cs` (`Talk` in `All`, no fallback in `Variants`), `Schemas~/npc.schema.json`; Tests in `ChatRulesTests.cs` and `DefinitionParserTests.cs`.

**Interfaces — produces:** `ConversationSettings { bool Enabled; int Chance = 50; }`, `ChatRules.Read(Dictionary<string,object> root, out string key, out ConversationSettings s, List<string> warnings)`, `NpcDefinition.Conversation`, `SoundPools.Talk`, `SpriteStates.Talk`.

- [ ] **Step 1: Failing tests** (use the JSON parse helper the other Core tests use)

```csharp
        [TestCase("{}", false, 50)]
        [TestCase("{\"conversation\": null}", false, 50)]
        [TestCase("{\"conversation\": false}", false, 50)]
        [TestCase("{\"conversation\": true}", true, 50)]
        [TestCase("{\"conversation\": {}}", true, 50)]
        [TestCase("{\"conversation\": {\"enabled\": false, \"chance\": 20}}", false, 20)]
        [TestCase("{\"conversation\": {\"chance\": 100}}", true, 100)]
        [TestCase("{\"conversation\": {\"enabled\": true, \"chance\": 0}}", true, 0)]
        public void Read_Forms(string json, bool enabled, int chance) { /* Read → null, Enabled/Chance as given, no warnings */ }

        [TestCase("{\"conversation\": {\"chance\": 101}}", "conversation.chance")]
        [TestCase("{\"conversation\": {\"chance\": 2.5}}", "conversation.chance")]
        [TestCase("{\"conversation\": {\"chance\": \"x\"}}", "conversation.chance")]
        [TestCase("{\"conversation\": {\"enabled\": 1}}", "conversation.enabled")]
        [TestCase("{\"conversation\": 3}", "conversation")]
        public void Read_Bad(string json, string expectedKey) { /* Read → problem, key == expectedKey */ }

        [Test] public void Read_UnknownKey_Warns() { /* {"lines": 3} → null, one warning starting "conversation.lines" */ }

        [Test]
        public void TalkNames()
        {
            Assert.AreEqual("talk", SoundPools.PoolOf("talk_3"));
            Assert.AreEqual("talk", SpriteStates.StateOf("talk_2"));
            Assert.IsFalse(SoundPools.IsCombat(SoundPools.Talk));
        }
```

(Bodies written in full in the test file, each three to six lines as in `Read_Forms` comment.) In `DefinitionParserTests.cs`, with its existing minimal-unique helper: `"conversation": { "chance": 30 }` → enabled, chance 30; `"conversation": { "chance": 300 }` → failure naming `conversation.chance`.

- [ ] **Step 2:** run-tests → compile errors.
- [ ] **Step 3: Implement**

```csharp
    /// <summary>npc.json "conversation" (spec conversations §1).</summary>
    public class ConversationSettings
    {
        public bool Enabled;
        public int Chance = 50;
    }

        // in ChatRules
        static readonly string[] Keys = { "enabled", "chance" };

        /// <summary>Missing, null or false = off; true = on; an object = on unless "enabled": false. Unknown keys warn.</summary>
        public static string Read(Dictionary<string, object> root, out string key, out ConversationSettings settings, List<string> warnings)
        {
            settings = new ConversationSettings();
            key = "conversation";
            object raw;
            if (!root.TryGetValue("conversation", out raw) || raw == null)
                return null;
            if (raw is bool)
            {
                settings.Enabled = (bool)raw;
                return null;
            }
            Dictionary<string, object> o = raw as Dictionary<string, object>;
            if (o == null)
                return "must be true, false or an object like { \"enabled\": true, \"chance\": 50 }";
            foreach (string k in FieldReader.UnknownKeys(o, Keys))
                warnings.Add("conversation." + k + ": unknown setting (use " + string.Join(", ", Keys) + "), ignored");
            bool enabled;
            if (FieldReader.Bool(o, "enabled", true, out enabled) != null || (o.ContainsKey("enabled") && o["enabled"] == null))
            {
                key = "conversation.enabled";
                return "must be true or false";
            }
            double chance;
            if (FieldReader.Number(o, "chance", 50, out chance) != null || (o.ContainsKey("chance") && o["chance"] == null) ||
                double.IsNaN(chance) || chance < 0 || chance > 100 || chance != Math.Floor(chance))
            {
                key = "conversation.chance";
                return "must be a whole number from 0 to 100";
            }
            settings.Enabled = enabled;
            settings.Chance = (int)chance;
            return null;
        }
```

`NpcDefinition`: `public ConversationSettings Conversation = new ConversationSettings();` (summary: talks with other ANPCs it greets, off by default). `DefinitionParser`: `"conversation"` in `SharedKeys`; after the sounds block:

```csharp
            string chatKey;
            List<string> chatWarnings = new List<string>();
            if ((problem = ChatRules.Read(o, out chatKey, out d.Conversation, chatWarnings)) != null)
                return Problem(r, file, chatKey, problem);
            foreach (string w in chatWarnings)
                r.Warnings.Add(file + ": " + w);
```

Schema, next to `"sounds"`:

```json
    "conversation": {
      "description": "Talks with other ANPCs it greets (needs talk sounds and a sprite set). true, or an object.",
      "oneOf": [
        { "type": "boolean" },
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "enabled": { "type": "boolean", "default": true },
            "chance": { "type": "integer", "minimum": 0, "maximum": 100, "default": 50, "description": "Percent chance after greeting another ANPC." }
          }
        }
      ]
    },
```

and `talk` in the `"sounds"` description.

- [ ] **Step 4:** run-tests (all pass); `python Tools~/check-schemas.py` (in sync).
- [ ] **Step 5:** commit "AdvancedNPCs: npc.json conversation setting, talk names".

### Task 3: Talk playback on NpcVoice and NpcSprite

**Files:** Modify `Scripts/Runtime/NpcVoice.cs` (beside `Play`), `Scripts/Runtime/NpcSprite.cs` (field, `Update`, `WantedState`, `Draw`, API beside `PlayGreet`).

**Interfaces — produces:** `NpcVoice.TalkClips` (int), `CustomVoiceOn` (bool), `PlayTalk()` → float seconds (0 = nothing), `StopTalk()`, `TalkPlaying`; `NpcSprite.HasSet`, `TalkSheets`, `Greeting`, `Talk(float seconds)`, `StopTalk()`.

- [ ] **Step 1: NpcVoice**

```csharp
        int lastTalk = -1;
        AudioSource talkSource;

        /// <summary>Loaded talk clips (spec conversations §1).</summary>
        public int TalkClips
        {
            get
            {
                List<AudioClip> clips;
                return set != null && set.Clips.TryGetValue(SoundPools.Talk, out clips) ? clips.Count : 0;
            }
        }

        /// <summary>The "ANPC sounds" setting lets the folder's voices play.</summary>
        public bool CustomVoiceOn
        {
            get { return VoiceModes.CustomVoice(Mode); }
        }

        /// <summary>
        /// A talk clip, no chance roll, not the one before when there are more (spec conversations §2); its seconds at
        /// the pitch used, 0 when nothing played.
        /// </summary>
        public float PlayTalk()
        {
            if (voices == null || gone || !CustomVoiceOn || TalkClips == 0)
                return 0f;
            List<AudioClip> clips = set.Clips[SoundPools.Talk];
            lastTalk = ChatRules.NextClip(clips.Count, lastTalk, Random.value);
            AudioClip clip = clips[lastTalk];
            if (clip == null)
                return 0f;
            AudioSource voice = voices[nextVoice];
            nextVoice = SoundRules.NextVoice(nextVoice, voices.Length);
            voice.pitch = SoundRules.Pitch(settings.SoundPitch, settings.SoundPitchVariation, Random.value);
            voice.PlayOneShot(clip, DaggerfallUnity.Settings.SoundVolume);
            lastVoice = voice;
            talkSource = voice;
            Count(SoundPools.Talk);
            return clip.length / Mathf.Max(SoundRules.MinPitch, voice.pitch);
        }

        /// <summary>A talk clip is still playing (self-test).</summary>
        public bool TalkPlaying
        {
            get { return talkSource != null && talkSource.isPlaying; }
        }

        /// <summary>Cuts the talk clip short (a conversation ended early).</summary>
        public void StopTalk()
        {
            if (talkSource != null)
                talkSource.Stop();
            talkSource = null;
        }
```

- [ ] **Step 2: NpcSprite** — field `float talkLeft;`; API:

```csharp
        /// <summary>A custom sprite set is loaded (conversations need one).</summary>
        public bool HasSet { get { return set != null; } }

        /// <summary>talk* sheets of the set (0: talking shows idle).</summary>
        public int TalkSheets { get { return set != null ? SpriteStates.Variants(set.Set, SpriteStates.Talk).Count : 0; } }

        /// <summary>A greet sheet is playing.</summary>
        public bool Greeting { get { return state == SpriteStates.Greet && oneShotLeft > 0; } }

        /// <summary>Loops a random talk* sheet (idle without one) this long while standing; each call picks again.</summary>
        public void Talk(float seconds)
        {
            if (set == null)
                return;
            talkLeft = seconds;
            if (oneShotLeft > 0)
                return;         // a hit, attack or greeting finishes first
            state = null;
            SetState(SpriteStates.Talk);
        }

        public void StopTalk() { talkLeft = 0f; }
```

`Update`: after `if (oneShotLeft > 0) oneShotLeft -= dt;` add `if (talkLeft > 0) talkLeft -= dt;`. `WantedState`: before the idle return, `if (talkLeft > 0 && speed <= WalkSpeed) return SpriteStates.Talk;`. `Draw`: `loop` also true for `SpriteStates.Talk`.

- [ ] **Step 3:** `bash Tools~/compile-check.sh`, `bash Tools~/runtime-check.sh` → OK.
- [ ] **Step 4:** commit "AdvancedNPCs: talk sheets and talk sounds playback".

### Task 4: NpcChat, greeter, spawner, talk window

**Files:** Create `Scripts/Runtime/NpcChat.cs`; modify `NpcGreeter.cs`, `NpcSpawner.cs` (NpcVoice/NpcGreeter lines), `NpcTalk.cs` (`TryTalk`).

**Interfaces — produces:** `NpcChat.Memory`, `NpcChat.Started` (static int), `Init(ConversationSettings)`, `Settings`, `Brain`, `Session`, `Partner`, `IsInitiator`, `Talking`, `WhyNot()`, `OnGreeted(NpcBrain)`, `TryStart(NpcChat other, bool forced)` → null or "Name: reason", `End(bool cutShort)`.

- [ ] **Step 1: NpcChat.cs**

```csharp
using UnityEngine;
using DaggerfallWorkshop;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>
    /// Conversations between ANPCs (spec conversations): after greeting another opted-in ANPC this one may start one;
    /// the one who started drives it (turns, sounds, sheets, facing) and either can end it.
    /// </summary>
    public class NpcChat : MonoBehaviour
    {
        const float TurnDegreesPerSecond = 540f;
        const float StartDelay = 0.3f;
        const float MaxGreetWait = 3f;

        public static readonly ChatMemory Memory = new ChatMemory();
        public static int Started;

        ConversationSettings settings = new ConversationSettings();
        NpcBrain brain;
        NpcSprite sprite;
        NpcVoice voice;
        ChatSession session;
        NpcChat partner;
        bool initiator;
        float delay;
        float greetWait;

        public void Init(ConversationSettings s) { settings = s ?? new ConversationSettings(); }

        void Awake()
        {
            brain = GetComponent<NpcBrain>();
            sprite = GetComponent<NpcSprite>();
            voice = GetComponent<NpcVoice>();
        }

        public ConversationSettings Settings { get { return settings; } }
        public NpcBrain Brain { get { return brain; } }
        public ChatSession Session { get { return session; } }
        public NpcChat Partner { get { return partner; } }
        public bool IsInitiator { get { return initiator; } }
        public bool Talking { get { return session != null && !session.Over; } }

        public string WhyNot()
        {
            if (sprite == null) sprite = GetComponent<NpcSprite>();
            if (voice == null) voice = GetComponent<NpcVoice>();
            bool calm = brain != null && brain.CurrentMode == NpcMode.Calm && !brain.State.hostile && !brain.IsEnemy;
            return ChatRules.Ineligible(settings.Enabled, Talking, calm, voice != null && voice.CustomVoiceOn,
                voice != null ? voice.TalkClips : 0, sprite != null && sprite.HasSet);
        }

        public void OnGreeted(NpcBrain other)
        {
            NpcChat o = other != null ? other.GetComponent<NpcChat>() : null;
            if (o == null || !settings.Enabled || !SoundRules.Rolls(settings.Chance, Random.value))
                return;
            if (!Memory.CanStart(ChatRules.PairKey(brain.Id, other.Id), NowHours()))
                return;
            TryStart(o, false);
        }

        public string TryStart(NpcChat other, bool forced)
        {
            if (other == null || other == this)
                return "no partner";
            string why = WhyNot();
            if (why != null)
                return brain.DisplayName + ": " + why;
            why = other.WhyNot();
            if (why != null)
                return other.brain.DisplayName + ": " + why;
            string pair = ChatRules.PairKey(brain.Id, other.brain.Id);
            if (!forced && !Memory.CanStart(pair, NowHours()))
                return "they talked less than 2 game hours ago";
            Memory.Started(pair, NowHours());
            session = new ChatSession(ChatRules.Lines(Random.value));
            other.session = session;
            partner = other;
            other.partner = this;
            initiator = true;
            other.initiator = false;
            delay = StartDelay;
            greetWait = MaxGreetWait;
            Started++;
            AdvancedNpcsMod.Log(brain.Id + " and " + other.brain.Id + ": conversation of " + session.Lines + " lines.");
            return null;
        }

        void Update()
        {
            if (!initiator || session == null)
                return;
            if (session.Over) { End(false); return; }
            if (ShouldEnd()) { End(true); return; }
            float dt = Time.deltaTime;
            Face(this, partner, dt);
            Face(partner, this, dt);
            brain.HoldStill(0.5f);
            partner.brain.HoldStill(0.5f);
            if (delay > 0f)
            {
                bool greeting = (sprite != null && sprite.Greeting) || (partner.sprite != null && partner.sprite.Greeting);
                greetWait -= dt;
                if (!greeting || greetWait <= 0f)
                    delay -= dt;
                return;
            }
            session.Tick(dt, Random.value);
            if (session.NeedsLine)
            {
                NpcChat speaker = session.Speaker == 0 ? this : partner;
                NpcChat listener = speaker == this ? partner : this;
                if (listener.sprite != null) listener.sprite.StopTalk();
                float seconds = speaker.voice != null ? speaker.voice.PlayTalk() : 0f;
                session.BeginLine(seconds);
                if (seconds > 0f && speaker.sprite != null)
                    speaker.sprite.Talk(seconds);
            }
            if (session.Over)
                End(false);
        }

        bool ShouldEnd()
        {
            if (partner == null || !partner.isActiveAndEnabled || partner.session != session)
                return true;
            if (!StillFit(this) || !StillFit(partner))
                return true;
            Vector3 d = partner.transform.position - transform.position;
            d.y = 0;
            return d.magnitude > ChatRules.MaxApart;
        }

        static bool StillFit(NpcChat c)
        {
            NpcBrain b = c.brain;
            return b != null && b.CurrentMode == NpcMode.Calm && !b.State.hostile && !b.IsEnemy &&
                   c.voice != null && c.voice.CustomVoiceOn && c.voice.TalkClips > 0 && c.sprite != null && c.sprite.HasSet;
        }

        static void Face(NpcChat who, NpcChat toward, float dt)
        {
            Vector3 to = toward.transform.position - who.transform.position;
            to.y = 0;
            if (to.sqrMagnitude > 0.0001f)
                who.transform.rotation = Quaternion.RotateTowards(who.transform.rotation, Quaternion.LookRotation(to), TurnDegreesPerSecond * dt);
        }

        public void End(bool cutShort)
        {
            NpcChat other = partner;
            Leave(cutShort);
            if (other != null && other.partner == this)
                other.Leave(cutShort);
        }

        void Leave(bool cutShort)
        {
            if (session != null) session.End();
            if (cutShort && voice != null) voice.StopTalk();
            if (sprite != null) sprite.StopTalk();
            session = null;
            partner = null;
            initiator = false;
        }

        void OnDisable()
        {
            if (session != null)
                End(true);
        }

        static double NowHours()
        {
            return DaggerfallUnity.Instance.WorldTime.Now.ToSeconds() / 3600.0;
        }
    }
}
```

- [ ] **Step 2: NpcGreeter** — field `NpcChat chat;`, `Start`: `chat = GetComponent<NpcChat>();`; `Update`, after the first guard:

```csharp
            if (chat != null && chat.Talking)
            {
                facingTarget = null;    // busy talking: greets no one (spec conversations §3)
                return;
            }
```

`CheckCandidates`, ANPC branch: after `Greet(other.transform);` add `if (chat != null) chat.OnGreeted(other);`.

- [ ] **Step 3: NpcSpawner** — replace `if (greets) go.AddComponent<NpcGreeter>();` with:

```csharp
            if (def.Conversation.Enabled)
                go.AddComponent<NpcChat>().Init(def.Conversation);
            if (greets || def.Conversation.Enabled)
                go.AddComponent<NpcGreeter>();      // a talker without greet files greets silently, which starts conversations
```

- [ ] **Step 4: NpcTalk.TryTalk**, after the calm check:

```csharp
            NpcChat chat = brain != null ? brain.GetComponent<NpcChat>() : null;
            if (chat != null && chat.Talking)
                chat.End(true);     // the player interrupts (spec conversations §3)
```

- [ ] **Step 5:** compile-check + runtime-check; commit "AdvancedNPCs: ANPC conversations (NpcChat)".

### Task 5: Console — anpc_chat and anpc_info

**Files:** Modify `Scripts/Runtime/AdvancedNpcsMod.cs` (registration + `ChatCommand`), `Scripts/Runtime/NpcBrain.cs` (`Describe`).

- [ ] **Step 1:** register

```csharp
            ConsoleCommandsDatabase.RegisterCommand("anpc_chat",
                "Starts a conversation between the ANPC in front of you and the nearest other ANPC within 4 m (ignores chance and the 2-hour wait).",
                "anpc_chat", ChatCommand);
```

```csharp
        /// <summary>anpc_chat (spec conversations §3); internal for the self-test.</summary>
        internal static string ChatCommand(params string[] args)
        {
            NpcBrain a = Nearest(5f);
            if (a == null)
                return "No ANPC within 5 m. Stand in front of one.";
            NpcBrain b = null;
            float best = ChatRules.StartDistance;
            foreach (NpcBrain other in NpcBrain.All())
            {
                if (other == a || other.CurrentMode == NpcMode.Dead)
                    continue;
                float d = Vector3.Distance(other.transform.position, a.transform.position);
                if (d <= best)
                {
                    best = d;
                    b = other;
                }
            }
            if (b == null)
                return "No other ANPC within " + ChatRules.StartDistance + " m of " + a.DisplayName + ".";
            NpcChat ca = a.GetComponent<NpcChat>();
            NpcChat cb = b.GetComponent<NpcChat>();
            if (ca == null || cb == null)
                return (ca == null ? a : b).DisplayName + " does not converse (no \"conversation\" in its npc.json).";
            string why = ca.TryStart(cb, true);
            string text = why == null ? a.DisplayName + " and " + b.DisplayName + " are talking (" + ca.Session.Lines + " lines)."
                                      : a.DisplayName + " and " + b.DisplayName + " cannot talk: " + why + ".";
            Log("anpc_chat: " + text);
            return text;
        }
```

- [ ] **Step 2:** `Describe`, after the sound lines:

```csharp
            NpcChat chat = GetComponent<NpcChat>();
            if (chat == null)
                sb.Append("  conversation: off\n");
            else
            {
                sb.Append("  conversation: chance ").Append(chat.Settings.Chance).Append("%, talk sounds ")
                  .Append(voice != null ? voice.TalkClips : 0).Append(", talk sheets ").Append(sprite != null ? sprite.TalkSheets : 0);
                if (chat.Talking && chat.Partner != null)
                    sb.Append(", talking with ").Append(chat.Partner.Brain.DisplayName).Append(", line ")
                      .Append(chat.Session.Line).Append(" of ").Append(chat.Session.Lines);
                else if (chat.WhyNot() != null)
                    sb.Append(", cannot talk now: ").Append(chat.WhyNot());
                sb.Append('\n');
            }
```

- [ ] **Step 3:** compile checks; commit "AdvancedNPCs: anpc_chat and conversation info".

### Task 6: In-game self-test

**Files:** Create `Scripts/Runtime/SelfTestChat.cs`; modify `Scripts/Runtime/SelfTest.cs` (run `ChatSteps` after `SoundSteps`, same pattern).

- [ ] **Step 1:** `ChatSteps(DaggerfallLocation location, Transform player)`, modelled on `SoundSteps`:

Setup: temp dirs with `talk_1.wav`, `talk_2.wav` (`ToneWav(0.3f)`), `greet_1.wav`; one indexed `LoadedSoundSet` per test ANPC folder (`selftest_chat_a`, `_b`, `_c`, `_d`), plus `selftest_chat_mute` (greet only). Sprite sets: a, c, d `TestSpriteSet(false, "idle", "walk", "talk")`; b and mute `TestSpriteSet(false, "idle", "walk")`. `NpcGreeter.ReactionOverride = "neutral"`, `NpcGreeter.ChanceOverride = 1f`, `NpcChat.Memory.Clear()`. Definitions from `TestDefinition(id, Bravery.Normal, location, player, OutOfGreeting)` with `d.Conversation = new ConversationSettings { Enabled = true, Chance = 100 }`. a and b 2.5 m apart, 8 m out along `OpenDirection(player, 16f)`, facing each other; wait (≤ 3 s) until sounds `IsReady` and sprites `HasSet`.

Checks:
1. "two ANPCs that greet each other start a conversation" — one of them `Talking` within 4 s; partners point at each other; exactly one initiator.
2. "a talking ANPC greets no one" — both greeters' `Greetings` unchanged from start to end.
3. "talkers face each other" — after 1 s, each forward within 25° of the line to the other.
4. "the speaker shows a talk sheet, or idle without one" — while a speaks its `CurrentAnimation` starts with `talk`; while b speaks, b's starts with `idle`.
5. "lines alternate and it ends after its lines" — within `Lines * 1.5 + 3` s both not talking; `Played("talk")` sum = `Lines`; initiator's = `(Lines + 1) / 2`.
6. "the pair waits 2 game hours" — `OnGreeted` again: not talking.
7. "anpc_chat starts one at once" — a 2 m in front of the player, b 1.5 m beside a; `ChatCommand()` contains "are talking"; `End(true)`.
8. "a hit ends it and cuts the line" — forced start; wait for `TalkPlaying`; hit the listener with `PlayerHit(.., 1)`; within 0.5 s neither talking, no `TalkPlaying`.
9. "moving apart ends it" — c, d forced; teleport d 10 m away; not talking next frames.
10. "talking to one ends it" — c, d forced; `c.GetComponent<NpcTalk>().TryTalk()`; not talking; close the talk window as `SelfTest.cs` does.
11. "unloading the starter ends it for the partner" — forced start; `NpcBrain.Discard(initiator)`; partner not talking next frame.
12. "no talk sounds / not opted in / vanilla sprite" — `TryStart(mute, true)` contains "no talk sounds"; a def with `Conversation.Enabled = false` has no `NpcChat`; an opted-in def without a sprite set gives `WhyNot() == "vanilla sprite"` once its sounds are ready.
13. "anpc_info shows the conversation" — `InfoCommand(id)` contains "conversation: chance 100%".

Cleanup in `finally`: overrides reset, `NpcChat.Memory.Clear()`, `DiscardTestPeople()`, test sets removed as `SoundSteps` does.

- [ ] **Step 2:** close DFU/editor; `bash Tools~/build-mod.sh --no-examples`; `git restore Assets/AddressableAssetsData/`; `bash Tools~/selftest.sh` twice → all pass. Failures: superpowers:systematic-debugging.
- [ ] **Step 3:** commit "AdvancedNPCs: conversation self-test".

### Task 7: Example content, README, version

- [ ] **Step 1: Talk sheets.** For each example with a custom set, take `talk_*.png` from the same character's model folder (wench → `Models/wench_1/extra_sprites`; others via memory `anpc-sprite-characters`) only if `pixelsPerUnit`, `cellHeight`, `groundY`, `fps`, `directions` match and the PNG is `cellWidth × frames` by `cellHeight × 8`; add the entry to the example's `sprites.json`; `.meta` like sibling PNGs (new guid).
- [ ] **Step 2: Voices.** Identify each example's greet actor in the Super Dialogue pack (ffprobe durations of `greet_*.ogg` vs the pack's greeting WAVs). Convert that actor's confirmation 1,2,6,8,9, refusal 1,2,3,8,10, misc 2,3,10,12,13,17 to `talk_1..talk_16.ogg` (`ffmpeg -i in.wav -ac 1 -ar 22050 -c:a libvorbis -q:a 4 -map_metadata -1 -vn out.ogg`).
- [ ] **Step 3:** opt in those examples (`"conversation": { "enabled": true }`); SOUND_CREDITS.txt.
- [ ] **Step 4:** README "Conversations" section after "Sounds" (setting, eligibility, what ends one, `anpc_chat`), `talk*` in Sprites, `talk` in Sounds, console list; `AdvancedNPCs.dfmod.json` 0.20.0 and every new file in its list; ideas doc entry.
- [ ] **Step 5:** run-tests, compile-check, runtime-check, check-schemas, build, selftest ×2; copy changed example folders into `DFU_testing` ANPCs (never overwrite `temple_monk_1`).
- [ ] **Step 6:** commit "AdvancedNPCs 0.20.0: ANPC conversations".

### Task 8: Review and merge

- [ ] Fresh opus review of `git diff master...`; fix Critical/Important; list Minors.
- [ ] Tests + self-test again.
- [ ] `git checkout master && git merge --no-ff feature/anpc-conversations -m "Merge feature/anpc-conversations: ANPC conversations (0.20.0)"`; `git push git@github.com:GoatBoy-11/daggerfall-unity.git master:master`.
- [ ] Memory + report.

# Advanced NPCs — ANPC conversations (design, 2026-10-12)

Two ANPCs that meet and greet each other sometimes stop and chat, taking turns: the speaker plays a `talk` sheet and
a `talk` sound, the other listens. Ambience in the spirit of Gothic 2's smalltalk and Oblivion's NPC chats. Audio
only: no text. Mod version 0.20.0.

Sections 1 and 2 were approved by the user in chat; section 3 was decided while the user was away (marked
**decided**, open to veto), except the greeting rule, which the user asked for.

## 1. Opt-in, eligibility, trigger (approved)

### npc.json

```json
"conversation": { "enabled": true, "chance": 50 }
```

- Missing, `null`, `false` or `"enabled": false`: never converses (the default). `"conversation": true` is short for
  `{ "enabled": true }`. Inside the object `enabled` defaults to true: writing the block opts in.
- `chance`: whole percent 0–100, default 50. Anything else is an error for that key, like other npc.json fields;
  unknown keys inside warn and are ignored.
- Shared key (unique and generic ANPCs). Added to `Schemas~/npc.schema.json`.

### Eligible

Checked for both ANPCs when a conversation would start:

- the ANPC opted in;
- calm (`NpcMode.Calm`), not hostile to the player, not an enemy;
- not already in a conversation;
- its sound set has loaded clips in the `talk` pool, and the **ANPC sounds** setting is *ANPC* (with *Vanilla* or
  *Off* the folder's voices do not play, so no one would hear the conversation);
- it has a custom sprite set (loaded) with a `talk*` sheet or, failing that, an `idle*` sheet. Every loaded set has
  an idle sheet, so in practice: a custom sprite set. An ANPC on the vanilla class sprite never converses.

### Trigger

- When ANPC A's greeter greets ANPC B (the existing greeting: calm, same side, ≤ 4 m, in sight, 25 % per
  encounter, 2 h per target), A rolls its own `chance`. A greeting counts even without a `greet` sheet or sound
  (the silent greeting the greeter already makes), so `greet` files are not required.
- The pair (unordered: A–B is B–A) talks at most once every 2 game hours. The time is noted when a conversation
  starts, so an interrupted one counts too. Kept in memory only (like greetings), shared by all ANPCs.
- Pass, both eligible and the pair not on cooldown: the conversation starts. A speaks first.
- Defaults give about 25 % × 50 % ≈ 12.5 % per meeting.

### New names

- Sprite state `talk` (`talk`, `talk_1`, `talk_2`…): fallback idle. Only conversations play it.
- Sound pool `talk` (`talk.ogg`, `talk_1.ogg`…). Only conversations play it, with no chance roll; `greetChance` and
  `combatChance` do not apply. `talk` files in the shared `_Sounds` folder are ignored (only steps are shared).

## 2. Turn flow (approved)

- Start: both ANPCs hold still and turn to face each other, and keep facing each other for the whole conversation.
- Lines: the total is rolled 4–8 at the start; A speaks first, then they alternate.
- One turn: the speaker picks a random `talk` clip (never the same clip twice in a row when its pool has two or more)
  and plays it from its own voice: 3D, fading out by 30 m, DFU's sound volume, its `soundPitch` and
  `soundPitchVariation`. The turn lasts the clip's length ÷ pitch. The speaker's sprite loops a random `talk*` sheet
  (fallback idle) for that time; the listener shows its idle sheet.
- Pause 0.4–1.0 s (random) after each line, then the other speaks.
- End: after the last line both go back to what calm ANPCs do (wandering).
- Nothing is saved: loading a game, or leaving the area, ends any conversation.

## 3. Interruptions, tools, content (decided)

### Ending early

A conversation ends at once, for both, when either ANPC:

- is no longer calm (attacked, turned hostile, fleeing, fighting a creature), becomes an enemy, or dies;
- is destroyed or disabled (town unloaded, discarded);
- is more than 6 m from the other (knocked back, teleported);
- is talked to by the player (DFU's talk window opens for it);
- loses its talk sounds (`anpc_reload_assets` swapped the set and the new one has none) or its sprite set.

The speaker's clip stops when a conversation ends early (a hit cuts a sentence short). The pair stays on cooldown.

### Greetings during a conversation (user request)

An ANPC in a conversation greets no one (no other ANPC, and not the player either, since a greeting would turn it away
from its partner) until the conversation ends. Others may still greet it; that cannot start a second conversation for
it.

### Player and LOD

- The conversation goes on when the player walks away; its sounds fade out by 30 m like every ANPC sound. The
  greeter only works within 30 m of the player (existing LOD), so new conversations start only near the player.
- The talk sheet is a looping state like idle; the existing sprite LOD (fewer updates far away) applies.

### Console

- `anpc_info` shows the conversation setting, the talk pool size and talk sheet count, and, while chatting, the
  partner and the line (`talking with Bram, line 3 of 6`).
- `anpc_chat` (new, for authors): starts a conversation at once between the ANPC in front of the player and the
  nearest other ANPC within 4 m, ignoring chance and cooldown but not eligibility; prints why not when it cannot.

### Examples

- Talk sheets already rendered in `Models/<character>/…/talk_*.png` are added to the matching example sprite sets
  when their layout (cell height, pixels per unit, ground row, fps, directions) matches the installed set.
- Talk sounds: lines from the Super Dialogue Audio Pack V1 (CC BY 4.0, already credited) for the voice each example
  already uses where it can be identified: confirmations (yeah, okay, definitely, alright, great), refusals (no, no
  way, nah, never, sorry) and murmurs (hmm, huh, wow, laughter, a sigh, clearing the throat). Mono 22 kHz OGG like the
  other example sounds; SOUND_CREDITS.txt updated. The Dan Bosley male voice pack is not used (its licence forbids
  redistribution).
- Examples with a custom sprite set and talk sounds get `"conversation": { "enabled": true }`.

## 4. Architecture

### Core (no Unity/DFU types, unit-tested)

`ChatRules.cs`:

- `ConversationSettings { bool Enabled; int Chance = 50; }` and `ChatRules.Read(root, out key, out settings,
  warnings)` (npc.json, like `SoundRules.Read`). `NpcDefinition.Conversation`.
- Constants: `MinLines = 4`, `MaxLines = 8`, `MinPause = 0.4`, `MaxPause = 1.0`, `CooldownHours = 2`,
  `MaxApart = 6`.
- `Lines(roll01)` → 4..8; `Pause(roll01)` → 0.4..1.0; chance rolls reuse `SoundRules.Rolls`.
- `PairKey(idA, idB)` → order-independent key.
- `NextClip(count, last, roll01)` → index, not `last` when count ≥ 2.
- `Ineligible(...)` → null or a short reason (`"not opted in"`, `"no talk sounds"`, `"vanilla sprite"`,
  `"not calm"`, `"already talking"`, `"voices not ANPC"`), used by the runtime and `anpc_chat`.

`ChatMemory`: pair key → last start hour; `CanStart(key, nowHours)`, `Started(key, nowHours)`.

`ChatSession`: the turn state machine.

- `ChatSession(int lines)`; `Speaker` (0 = initiator, 1 = partner), `Line` (from 1), `Lines`, `Over`.
- `NeedsLine`: the current speaker should start its line now.
- `BeginLine(seconds)`: the speaker started a clip of this length (≤ 0: nothing played → session over).
- `Tick(dt, pauseRoll01)`: counts the line down, then the pause; after the pause the other speaker `NeedsLine`;
  after the last line `Over`.
- `End()`: over now.

### Runtime

- `NpcChat` (new MonoBehaviour, added by the spawner to every ANPC with `conversation.enabled`): `Session`,
  `Partner`, `IsInitiator`, `Talking`. `TryStart(NpcChat other, bool forced)`; the initiator's `Update` drives the
  session (both voices and sprites), checks the end conditions and faces both ANPCs toward each other; `End()` ends
  it for both. Static `ChatMemory` shared by all.
- `NpcGreeter`: skips its checks while its ANPC is talking; after greeting another ANPC, calls `NpcChat.TryStart`.
- `NpcVoice`: `TalkClips` (loaded count), `PlayTalk(out seconds)` (no chance roll, no repeat), `StopTalk()`.
- `NpcSprite`: `TalkSheets` count, `Talk(seconds)` (loop a random talk* sheet, fallback idle, for that long; a new
  call restarts with a new pick), `StopTalk()`. A hit or attack still takes over as now.
- `NpcBrain`: `HoldStill` refreshed by the conversation each frame.
- `NpcTalk.TryTalk` (player opens the talk window): ends the ANPC's conversation.
- `AdvancedNpcsMod`: `anpc_chat` command; `anpc_info` lines.
- `SoundPools.All` and `SpriteStates.All` get `talk`.

## 5. Testing

- Unit tests (Core): settings parsing (shorthand, defaults, bad chance, unknown key warning, `false`/null), lines
  and pause ranges at roll 0 and 1, pair key symmetry, cooldown, clip no-repeat, eligibility reasons, session
  (alternation, line count, pause, BeginLine(0) ends, End, Tick past a line and a pause in one step).
- Self-test in game (`SelfTestChat.cs`): two test ANPCs with tone talk clips of known length and a test sprite set
  with a talk sheet:
  - a forced greeting starts a conversation; both face each other; lines alternate and the speaker shows a talk
    sheet; a set with no talk sheet shows idle; it ends after the rolled number of lines;
  - no talk sounds → no conversation; not opted in → none; vanilla sprite → none;
  - the pair cooldown blocks a second one; a talking ANPC does not greet;
  - a hit ends it at once (clip stopped); the player talking to one ends it; moving one 10 m away ends it;
  - `anpc_chat` starts one and reports a reason when it cannot.
- README (Sounds, Sprites, new Conversations section), schema, check-schemas key sync, version 0.20.0.

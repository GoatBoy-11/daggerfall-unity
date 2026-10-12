# Advanced NPCs custom sounds Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Per-ANPC `Sounds` folders: random pools named after animation events, chances, pitch, footsteps, vanilla
voice replacement.

**Architecture:** Pure rules in `Scripts/Core` (pool names, settings parsing, pitch, step frames, vanilla-voice
decision; unit-tested). Runtime: `SoundLibrary` (index at start, lazy per-frame clip loading, unload after last user,
like `SpriteLibrary`) and `NpcVoice` (component on each ANPC with a sound set: two AudioSources — voice and steps —
event hooks, step timing, vanilla grunt replay).

**Tech Stack:** C# for DFU's runtime mod compiler (no nested enums; every new `.cs` listed in
`AdvancedNPCs.dfmod.json` "Files"); Unity 2019.4 `UnityWebRequestMultimedia.GetAudioClip` (as DFU's
`SoundReplacement`); NUnit; ffmpeg (on PATH) for the MP3/OGG probe files.

**Spec:** `docs/superpowers/specs/2026-10-12-advanced-npcs-sounds-design.md`

## Global Constraints

- Pools: greet, attack, attack_range, cast, hit, death, step. Pool = file name minus extension minus trailing `_<digits>`.
- `attack_range` without files uses `attack`.
- npc.json `"sounds"`: combatChance 0–100 (50), greetChance 0–100 (50), stepDistance > 0 (15), soundPitch 0.25–3 (1),
  soundPitchVariation 0–1 (0), stepPitch 0.25–3 (1), stepPitchVariation 0–1 (0). Unknown keys warn, bad values fail
  the definition like `combat`.
- Pitch per play: `pitch + random(−variation, +variation)`, at least 0.1.
- `sprites.json` `"stepFrames": [..]` only on walk*/run*, 1-based within frames; default `[1, frames/2 + 1]`.
- Vanilla steps timer: 0.5 s walking, 0.33 s running.
- Vanilla voices: ANPC with a Sounds folder gets `EnemySounds.RaceForSounds = Races.None`; the mod replays the vanilla
  grunt (20 % attack, 40 % hurt, `Settings.CombatVoices`, `EnemyClass` only, heavy = damage ≥ max/4) only for events
  without a custom pool.
- Audible max distance 30 m, linear rolloff, volume `DaggerfallUnity.Settings.SoundVolume`.
- Verification: `Tools~/run-tests.sh`, `Tools~/compile-check.sh`, `Tools~/build-mod.sh --no-examples` then
  `git restore Assets/AddressableAssetsData/`, `Tools~/selftest.sh`, `python Tools~/check-schemas.py`.
- ModVersion 0.15.0 → 0.16.0.

## Review Focus

- An ANPC that dies on the frame its clips are still loading: death sound must not throw (plays if loaded, else silent).
- Death sound must be heard although the ANPC's object is deactivated/hidden at death (play from a detached source).
- Many ANPCs sharing one Sounds folder: one load, users counted, unload only after the last.
- A pitch of 0.25 with variation 1 must not reach ≤ 0 (clamped to 0.1).
- A Sounds folder with only unknown names (e.g. `idle_1.wav`): warned, treated as no sounds (vanilla voice kept).

---

### Task 1: Spike — runtime audio formats

- Generate `probe.wav`, `probe.ogg`, `probe.mp3` (0.2 s tone) with ffmpeg into `DFU_testing/ANPCs/_probe_sounds/`.
- Optional self-test step (like the looks pass: runs only when that folder exists) loads each with
  `UnityWebRequestMultimedia.GetAudioClip(file://…, AudioType.WAV / OGGVORBIS / MPEG)` and logs clip length or the error.
- Ledger the result. Ruling: `.mp3` supported iff it loads with length > 0; otherwise `.mp3` files log
  "<file>: MP3 is not supported by Unity here; convert to .ogg" and are skipped.

### Task 2: Core rules (TDD) — `Scripts/Core/SoundRules.cs`, `Editor/Tests/SoundRulesTests.cs`

Produces:
- `public static class SoundPools` — consts `Greet, Attack, AttackRange, Cast, Hit, Death, Step`;
  `string PoolOf(string fileNameWithoutExtension)` (null for unknown); `bool IsCombat(string pool)`;
  `string Resolve(string pool, ICollection<string> available)` (attack_range → attack fallback, null when none).
- `public class SoundSettings` — `CombatChance=50, GreetChance=50, StepDistance=15f, SoundPitch=1f,
  SoundPitchVariation=0f, StepPitch=1f, StepPitchVariation=0f`.
- `public static class SoundRules` — `string Read(Dictionary<string, object> root, out string key, out SoundSettings s,
  List<string> warnings)`; `bool Rolls(int chance, double roll01)`; `float Pitch(float pitch, float variation, double
  roll01)`; `int[] DefaultStepFrames(int frames)`; `bool VanillaVoice(bool hasCustomPool)` (= !hasCustomPool).
- `SpriteAnimation.StepFrames` (int[], null = default) parsed in `SpriteSetParser` (only walk*/run*; 1..frames;
  else the set's error "stepFrames: …").
- Wire `SoundRules.Read` into `DefinitionParser.ReadShared` (`"sounds"` added to SharedKeys); `NpcDefinition.Sounds`.

Tests (RED first): PoolOf (`attack_3`→attack, `attack_range_2`→attack_range, `greet`→greet, `step_10`→step,
`foo_1`→null, `attack_`→null, `Attack_1`→attack (case-insensitive)); Resolve fallback; Read defaults, ranges,
unknown key warning, non-object error; Pitch bounds and clamp; DefaultStepFrames(8)=[1,5], (1)=[1], (3)=[1,2];
stepFrames parse ok/invalid/not on walk; Rolls(0/50/100).

### Task 3: SoundLibrary — `Scripts/Runtime/SoundLibrary.cs`

- `SoundLibrary.Load(root, defs)`: per folder, `<folder>/Sounds` (any case) → `LoadedSoundSet { Label, Dir,
  Dictionary<string, List<string>> Files (pool → paths), Dictionary<string, List<AudioClip>> Clips, Users, IsReady }`.
  Unknown names / unsupported extensions logged once; sets with no known pool are not added.
- `For(folder)`, `Acquire/Release` (Users; unload `UnloadDelaySeconds` after last), `Tick(now)` loads one clip per
  frame (UnityWebRequestMultimedia, blocking like DFU), `Reload(folder, dir)` with a retired list like SpriteLibrary.
- `AdvancedNpcsMod`: `Sounds = SoundLibrary.Load(...)` after sprites; `Update` ticks it.

### Task 4: NpcVoice — `Scripts/Runtime/NpcVoice.cs` + hooks

- Added by `NpcSpawner` when `Sounds.For(def.Folder) != null`: `Init(LoadedSoundSet, SoundSettings, NpcBrain)`;
  acquires in Start, releases in OnDestroy (same never-active rule as NpcSprite); sets `RaceForSounds = None`.
- `Play(string pool)` (chance by group, pitch, voice source); `Step()`; counters `Played(pool)` for the self-test.
- Hooks: NpcGreeter.Greet → `Play(greet)`; NpcVoice.Update watches `mobile.EnemyState` for PrimaryAttack /
  RangedAttack1/2 starts and health drops (hit; vanilla grunt replay when no pool); NpcMagic.TryCast success →
  `Play(cast)`; NpcBrain.OnDeath (first) → `PlayDeath()` via a detached temporary AudioSource at the ANPC's position.
- Steps: NpcSprite calls `voice.OnFrame(animName, frame)` when the shown frame changes on walk/run sheets
  (footfall if frame+1 in StepFrames or default); without a custom set, NpcVoice times steps from NpcGait.Current
  (Walk 0.5 s, Run 0.33 s). Player beyond StepDistance → no step.

### Task 5: Tools, schema, docs

- `anpc_reload_assets` (old `anpc_reload_sprites` kept as alias) reloads sounds too; `anpc_info` adds a "sounds:"
  line (pool counts, loaded/loading).
- npc.schema.json `sounds` block; sprites.schema.json `stepFrames`; check-schemas.py reads `SoundRules` keys.
- README "Sounds" section; version 0.16.0.

### Task 6: Self-test — `Scripts/Runtime/SelfTestSounds.cs`

Generated 0.1 s WAV files (PCM written by code) in a temp Sounds folder for a test ANPC (chances 100):
greet plays on greeting; attack on an attack; hit on damage; death on death; steps counted while walking within
`stepDistance` and none beyond; `RaceForSounds == None`; set Users drop to 0 after discard. Three clean full runs.

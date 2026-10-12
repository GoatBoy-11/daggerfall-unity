# Advanced NPCs tools + quality Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Author tools (`anpc_info`, JSON schemas, `anpc_reload_sprites`), the review-minor fixes, save format
versioning and reliable magic self-tests.

**Architecture:** Pure rules in `Scripts/Core` (unit-tested in the Unity 6 host), runtime wiring in
`Scripts/Runtime` (checked by the in-game self-test), schemas and their checker outside Unity (`Schemas~`, `Tools~`).

**Tech Stack:** C# (DFU runtime mod compiler: no nested enums, new files listed in `AdvancedNPCs.dfmod.json`),
NUnit, Python 3 for the schema check.

**Spec:** `docs/superpowers/specs/2026-10-12-advanced-npcs-tools-quality-design.md`

## Global Constraints

- Every new `.cs` goes into `AdvancedNPCs.dfmod.json` "Files".
- Verification: `Tools~/run-tests.sh` (unit), `Tools~/compile-check.sh`, `Tools~/build-mod.sh --no-examples` then
  `git restore Assets/AddressableAssetsData/`, then `Tools~/selftest.sh` (background).
- ModVersion 0.14.0 → 0.15.0.

## Review Focus

- Reload while a set is still loading (Pending queue of the old set) must not leave a sprite waiting forever.
- Reload of a folder whose sets all became invalid: live ANPCs must show the vanilla sprite, not nothing.
- A corpse of an old set after reload must keep drawing (textures not destroyed under it).
- `anpc_info` with no ANPC in front / unknown id must answer, not throw.
- A save with `Version` > 1 must still load.

---

### Task 1: Core — SaveFormat, `$schema`, opinion partial days

- Tests first (`Editor/Tests/ToolsQualityTests.cs`): `SaveFormat.Check(0/1)` null, `Check(2)` mentions "format 2";
  `FieldReader.UnknownKeys({"$schema":..,"x":..}, [])` == ["x"]; `OpinionRules.ChangedAt(10, 0, 0.9 day)` == 0,
  `ChangedAt(10, 0, 2.5 days)` == 2 days, `ChangedAt(0, 0, 5)` == 5, drifted-to-0 → now.
- Implement `Scripts/Core/SaveFormat.cs`, FieldReader skip, `OpinionRules.ChangedAt`; use it in
  `NpcBrain.ChangeOpinion` and `GameFacts.ChangeOpinion`.
- `NpcSaveData.Version`, written in `GetSaveData`, checked in `RestoreSaveData`.

### Task 2: Review minors (runtime)

B2.1–B2.12 of the spec, each at its site (NpcSpawner, NpcBrain.FindFoe/Patrol, NpcSprite.Hide/Busy/greet,
NpcGreeter, NpcGait). Self-test additions: dead ANPC's set users drop; hostile-without-hit drops greet pose;
patrol ignores an inactive foe.

### Task 3: `anpc_info`

Command in `AdvancedNpcsMod`; text from `NpcBrain.Describe()` lines. Self-test: describe a test ANPC, check id,
mode and sprite state appear.

### Task 4: `anpc_reload_sprites`

`SpriteLibrary.Reload(root, folder)` returns the replaced old sets; retired sets go to a `retired` list unloaded by
`Tick` once unused. `NpcSprite.Swap(LoadedSpriteSet next)` releases the held set and begins the new one (null shows
vanilla). Self-test: a live test ANPC swaps to the reindexed set.

### Task 5: Schemas + checker + README

`Schemas~/*.schema.json`, `Tools~/check-schemas.py`, README sections, version bump.

### Task 6: Flaky magic tests

Placement/diagnostics already in `SelfTestMagic.cs`; diagnose with the printed senses; three clean full runs.

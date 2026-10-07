# Advanced NPCs 1b (Custom Sprites) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Draw an ANPC's own 8-direction sprite sheets (from `ANPCs/<npc>/sprites/`) instead of the vanilla class sprite, including a death animation that stays as the corpse.

**Architecture:** Pure parts (sprites.json parsing and sheet checks, sheet name → state, direction row, set choice, `spriteHeight`) in `Scripts/Core`, unit-tested. Runtime: `SpriteLibrary` loads sets per NPC folder; `NpcSprite` is a camera-facing quad on the NPC that follows DFU's `MobileUnit.EnemyState` while the vanilla billboard keeps running hidden (combat timing stays vanilla); `NpcCorpseSprite` plays the death sheet and stays as the corpse, hiding the picture of the loot pile DFU creates (`DaggerfallEntityBehaviour.CorpseLootContainer`).

**Spec:** `docs/superpowers/specs/2026-10-07-advanced-npcs-1b-sprites-design.md`

## Global Constraints

As in `docs/superpowers/plans/2026-10-06-advanced-npcs-v2.md` (C# subset, no enum-typed fields across files, manifest entries, `git restore Assets/AddressableAssetsData/` after builds, branch `feature/advanced-npcs-v2`, no push, verification commands). Build with `build-mod.sh --no-examples`. The user's sprite art never goes into the repository.

- Direction rows (spec §5.1): 0 front, 1 front_right, 2 right, 3 back_right, 4 back, 5 back_left, 6 left, 7 front_left. With `f` the NPC's forward and `c` the direction to the camera (both flat), `θ = atan2(dot(c, right(f)), dot(c, f))` in degrees, `right(f) = (f.z, −f.x)`; row = `round(θ / 45)` wrapped to 0..7 (camera on the NPC's right → row 2).
- Sheet layout: row 0 at the top of the image; Unity UV origin is bottom-left, so row r uses `offset.y = (7 − r) / 8`.
- Feet point: the bottom of the NPC's `CharacterController` (`bounds.min.y`), horizontally the root position.

## Review Focus

1. **A sheet whose size does not match `sprites.json`** (e.g. edited in an image editor) must be rejected with the expected size, never drawn misaligned. Pinned by `SpriteSetTests.WrongSheetSize_IsRejected` (Task 1).
2. **Camera exactly on a 22.5° boundary** must give a stable row. Pinned by `SpriteDirectionsTests` boundary cases (Task 2).
3. **Only `idle` present** must still animate walking (falls back to idle). Pinned by `SpriteStatesTests.Fallbacks` (Task 2).
4. **NPC killed while no corpse loot pile is created** (e.g. quest foe) must not leave a stray corpse sprite searching forever. Pinned by the `NpcCorpseSprite` timeout (Task 5).
5. **Two people of the same template** must share one loaded texture set (memory). Pinned by the `SpriteLibrary` cache (Task 3) and self-test (Task 4).

---

### Task 1: Sprite sets (Core)
Files: create `Scripts/Core/SpriteSet.cs` (`SpriteAnimation`, `SpriteSet`, `SpriteSetResult`, `SpriteSetParser`); test `Editor/Tests/SpriteSetTests.cs`; manifest.
Interfaces: `SpriteSetParser.Parse(string label, string json) -> SpriteSetResult {Set, Error}`; `SpriteSet {float PixelsPerUnit; int CellHeight; int GroundY; int Fps; int DeathStaticGroundY; Dictionary<string, SpriteAnimation> Animations}`; `SpriteAnimation {string Name; int CellWidth; int Frames}`; `SpriteSetParser.CheckSheetSize(string label, SpriteAnimation a, int cellHeight, int width, int height) -> string` (null = ok).
Tests: parse the real wench `sprites.json` shape; invalid fields (`pixelsPerUnit` ≤ 0, `cellHeight` ≤ 0, `fps` ≤ 0, `frames` < 1, `cellWidth` < 1, directions not the 8 known names in order) → error text `<label>: <field>: <problem>`; `deathStatic.groundY` optional; `WrongSheetSize_IsRejected` → `<label>/death.png: file: must be 2064 x 2048 (6 frames of 344 x 256, 8 directions), got 2000 x 2048`.

### Task 2: States, directions, set choice, spriteHeight (Core)
Files: create `Scripts/Core/SpriteStates.cs` (`SpriteStates`, `SpriteDirections`); modify `NpcDefinition.cs` (`float SpriteHeight`), `DefinitionParser.cs` (`spriteHeight` shared key: positive number or error); tests `SpriteStatesTests.cs`, `SpriteDirectionsTests.cs`, `FolderParserTests` (spriteHeight).
Interfaces: `SpriteStates.Idle/Walk/Hit/Attack/Death` (strings); `SpriteStates.StateOf(string sheetName) -> string or null` (name, or name + `_<number>`); `SpriteStates.Variants(SpriteSet set, string state) -> List<SpriteAnimation>` with fallbacks walk→idle, hit→idle, attack→idle (death: none); `SpriteStates.IsValid(SpriteSet)` (has idle); `SpriteStates.PickSet(int count, uint seed) -> int`; `SpriteDirections.Row(float camX, float camZ, float fwdX, float fwdZ) -> int`.
Tests: all 8 sectors, wrap-around, exact boundaries stable, zero vectors → 0; names `idle`, `idle_2`, `idle_x` (→ null), `walk_1`, `death`, `deathstatic` (→ null); fallbacks; PickSet repeatable and spread; spriteHeight positive/negative/text.

### Task 3: SpriteLibrary (Runtime)
Files: create `Scripts/Runtime/SpriteLibrary.cs` (`LoadedSpriteSet`, `SpriteLibrary`); modify `AdvancedNpcsMod.cs` (`public SpriteLibrary Sprites`, loaded after the catalog), manifest.
Interfaces: `LoadedSpriteSet {SpriteSet Set; Dictionary<string, Texture2D> Sheets; Texture2D DeathStatic; float StandingHeightPx}`; `SpriteLibrary.Load(string root, IEnumerable<NpcDefinition> defs)`; `List<LoadedSpriteSet> For(string folder)` (empty = vanilla); `void Add(string folder, LoadedSpriteSet set)` (self-test); `static LoadedSpriteSet FromTextures(SpriteSet set, Dictionary<string, Texture2D> sheets, Texture2D deathStatic)`.
Rules: set folders `sprites` and `sprites_<n>` (sorted ordinal); only sheets named in `sprites.json` and `death_static.png`; textures point-filtered, clamped, no mipmaps; `StandingHeightPx` = highest opaque pixel above `GroundY` over all idle frames (computed before compression); then `Compress(true)` and `Apply(false, true)`. Errors per spec §7. Log `Loaded N sprite set(s)`.

### Task 4: NpcSprite (Runtime)
Files: create `Scripts/Runtime/NpcSprite.cs`; modify `NpcSpawner.cs` (after `NpcTalk`: choose set with `SpriteStates.PickSet(sets.Count, instance.Seed)`, add `NpcSprite`), `SelfTest.cs`.
Interfaces: `NpcSprite.Init(LoadedSpriteSet set, float worldHeight)`; read-only `CurrentAnimation`, `CurrentRow`, `CurrentFrame`, `Set`, `WorldPerPixel`; `static float DefaultHeight(GameObject npc)` = `MobileUnit.GetSize().y`.
Rules: world per pixel = `worldHeight / StandingHeightPx`; child quad (`GameObject.CreatePrimitive(PrimitiveType.Quad)`, collider removed) with a copy of the vanilla billboard's material, texture swapped per sheet, `mainTextureScale = (1/frames, 1/8)`; vanilla `MeshRenderer.enabled = false` (re-applied in `LateUpdate`); quad turns to the camera around Y; quad centre `y = feetY + (cellHeight/2 − groundY) * k`; state: `Hurt` → hit, `PrimaryAttack`/`RangedAttack1`/`RangedAttack2`/`Spell` → attack, otherwise walk if the root moved > 0.15 units/s, else idle; variant re-picked when the state changes; idle/walk loop, hit/attack hold the last frame.
Self-test (first, RED by compile): register a generated set for folder `selftest_sprite` (2 frames of 16×32 per sheet: idle, walk, death), spawn an NPC with that folder: "sprite NPC hides the vanilla billboard and shows its own", "sprite direction follows the camera", "sprite frames advance", "sprite walks while moving".

### Task 5: NpcCorpseSprite (Runtime)
Files: create `Scripts/Runtime/NpcCorpseSprite.cs`; modify `NpcBrain.cs` (`OnDeath`: if the NPC has an `NpcSprite` with a death sheet → `NpcCorpseSprite.Spawn(...)` before DFU deactivates it), `SelfTest.cs`.
Interfaces: `static NpcCorpseSprite Spawn(NpcSprite from, DaggerfallEntityBehaviour behaviour)`; read-only `ShowingStatic`, `Finished`, `HidLoot`.
Rules: new object under the NPC's parent (town) at the feet point; plays a death variant once (rows follow the camera against the NPC's forward at death), then shows `DeathStatic` (camera-facing, bottom at ground + `DeathStaticGroundY`) or holds the last death frame; until found, reads `behaviour.CorpseLootContainer` each frame: when set, disables every `MeshRenderer` under the loot object; afterwards, if the loot object is gone → destroy self; if no loot appears within 10 s → keep the body but stop looking.
Self-test (first): kill the sprite NPC → "death leaves a sprite corpse and hides the loot picture"; second NPC whose set has `death_static` → "death_static is used as the corpse".

### Task 6: Docs and the user's test template
Files: `README.md` (Sprites section); test install only: `ANPCs/wench/npc.json` (generic, female) + copy of `Models/wench_1/sprites` as `ANPCs/wench/sprites` without `*.backup`, `*.psd`, `_cells`.
Verification: full loop; Player.log `Loaded 1 sprite set(s)`, wench people spawn; hand-off checklist.

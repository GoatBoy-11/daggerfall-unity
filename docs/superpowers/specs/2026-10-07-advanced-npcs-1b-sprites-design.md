# Advanced NPCs 1b — Custom Sprites

- **Date:** 2026-10-07
- **Status:** Draft, awaiting review
- **Builds on:** v2 and v2.1 designs (`2026-10-06-advanced-npcs-v2-design.md`, `2026-10-07-advanced-npcs-v2.1-design.md`).

## 1. Context

ANPCs still look like vanilla class sprites. The user renders their own 8-direction sprite sheets from Blender (first model: `Models/wench_1`, sheets made by the mod's render script: `idle_1`, `idle_2`, `walk_1`, `hit_1`, `death`, `attack_1`, `attack_2`, plus `sprites.json`). 1b makes the game draw them.

## 2. Goals and success criteria

1. An NPC folder with a `sprites/` folder shows those sheets instead of the vanilla class sprite; without it nothing changes.
2. The right direction (8) is shown for where the camera stands, and the right animation for what the NPC does (idle, walking, hurt, attacking, dying).
3. Animation variants (`idle_1`, `idle_2`) are picked at random each time that state starts.
4. Several sprite sets per NPC (`sprites_1/`, `sprites_2/`, …): each person gets one, the same one every visit in *Same people every visit* mode.
5. The NPC stands as tall as a vanilla townsperson unless `npc.json` says otherwise, and does not change size between animations.
6. On death the death sheet plays and the body stays as the lootable corpse; an optional `death_static.png` replaces the last frame as a single-direction corpse image.
7. Combat, AI, sounds, crime and saving behave exactly as before.

## 3. Non-goals

- Re-dressing the corpse after the area is left or the game reloaded (DFU then recreates the loot pile with its vanilla corpse picture).
- Changing attack timing to match custom attack frames (DFU's own timing stays; §6.3).
- Real-time 3D models; sprites for monsters; per-state sounds.
- Shipping the user's sprite art in the repository.

## 4. Engine facts

- `DaggerfallEnemy.FindMobileUnit` lets a mod replace the billboard of **every** enemy (global asset `DaggerfallMobileUnit`), so it cannot be used per NPC.
- `DaggerfallMobileUnit` turns its quad to the camera each frame and picks an orientation from the angle between the camera and its parent's forward direction (`UpdateOrientation`, 8 steps of 45°, vanilla sheets mirror 3 of them).
- `EnemyAttack` / `EnemyMotor` read `MobileUnit.EnemyState`, `IsPlayingOneShot()` and `DoMeleeDamage`, which `DaggerfallMobileUnit` drives from its own animation frames. Hiding only its `MeshRenderer` keeps all of that working.
- `EnemyMotor` (fighting) and the mod's `NpcMover` (calm, fleeing) both rotate the NPC root to face its movement, so the root's forward is the facing.
- `MobileUnit.GetSize()` is the vanilla sprite's world size (record 0).
- On death `EnemyDeath` creates a lootable corpse marker (`GameObjectHelper.CreateLootableCorpseMarker`) with the vanilla corpse texture and removes the enemy.

## 5. Files

```
ANPCs/<npc>/
  npc.json
  sprites/                 one sprite set (or sprites_1/, sprites_2/, … for several)
    sprites.json           written by the render script
    idle_1.png  idle_2.png  walk_1.png  hit_1.png  attack_1.png  attack_2.png  death.png
    death_static.png       optional corpse image (one direction)
```

- `sprites/` alone, or `sprites_<number>/` folders; if both exist, all are variants.
- Other files in the folder (`*.backup`, `_cells/`) are ignored; only sheets named in `sprites.json` and `death_static.png` are read.

### 5.1 `sprites.json` (as written by the render script)

| Field | Meaning |
|---|---|
| `pixelsPerUnit` | Pixels per model unit, the same for every sheet of the set. |
| `cellHeight` | Frame height in pixels (all sheets). |
| `groundY` | Pixel row of the feet, counted from the bottom of a frame. |
| `anchorX` | `"centre"`: feet at the horizontal centre of a frame. |
| `fps` | Playback speed. |
| `directions` | Row order of every sheet: `front, front_right, right, back_right, back, back_left, left, front_left`. |
| `animations` | Per sheet name: `cellWidth` (frame width in pixels) and `frames`. A sheet is `frames × cellWidth` wide and `8 × cellHeight` high. |

Optional, for `death_static.png`: `"deathStatic": { "groundY": <row from bottom> }` (default 0: the image's bottom edge is the ground). It is drawn at the same `pixelsPerUnit`, centred.

### 5.2 `npc.json` addition

| Field | Default | Meaning |
|---|---|---|
| `spriteHeight` | the vanilla class sprite's height (`MobileUnit.GetSize().y`) | World height, in DFU units, of the standing pose (the tallest frame of the idle sheets). |

The world scale is `spriteHeight / (standing height in pixels / pixelsPerUnit)`; every sheet of the set uses it, so the NPC never changes size between animations.

## 6. Behaviour

### 6.1 States

| Sheet names | Played while |
|---|---|
| `idle*` | standing (calm, cowering, waiting to attack) |
| `walk*` | moving (wandering, fleeing, chasing) |
| `hit*` | DFU `Hurt` state (one shot) |
| `attack*` | DFU `PrimaryAttack` / `RangedAttack` / `Spell` (one shot) |
| `death*` | after death (one shot, then corpse) |

- The state comes from `MobileUnit.EnemyState` plus whether the NPC actually moves this frame.
- A name with a number suffix is a variant of its state; a variant is picked at random each time the state starts (looping states keep the same variant until the state changes).
- Missing sheets fall back: `walk` → `idle`; `hit`, `attack` → `idle`; no `idle` → that sprite set is invalid (vanilla sprite, error). No `death` → vanilla death handling.

### 6.2 Drawing

- A child quad on the NPC root turns to the camera around the vertical axis (like vanilla billboards) with the feet point on the ground.
- Direction: angle between the camera direction and the root's forward, in 45° steps, mapped to the sheet rows in `directions` order.
- Frames advance at `fps`; one-shot states hold their last frame until DFU leaves the state.
- One material per sheet; frames are chosen by texture offset (no per-frame textures). The vanilla billboard's `MeshRenderer` is disabled; its component keeps running.
- The quad uses DFU's billboard shader/material settings so it is lit and darkened like vanilla billboards; filter mode follows DFU's global setting.

### 6.3 Combat timing

DFU's hidden billboard keeps deciding when a swing deals damage, so custom attack sheets may look slightly early or late against the hit. Accepted for 1b (non-goal §3).

### 6.4 Death and corpse

- When the NPC dies, a separate corpse sprite object is placed where it stood (child of the town, so it unloads with it) and plays the death sheet in the direction the NPC faced relative to the camera.
- Afterwards it shows `death_static.png` (single direction, camera-facing) if present, otherwise the last death frame (direction still follows the camera).
- The loot pile DFU creates there stays clickable; its own picture is hidden. If the loot pile disappears, the corpse sprite is removed.
- After leaving the area or reloading, DFU recreates its loot pile with its vanilla corpse picture (non-goal §3).

### 6.5 Sprite sets per person

Each person picks one set from its seed (`StableHash`-based, like portraits): repeatable in same-people mode, re-rolled with the person in random mode. Nothing is saved.

## 7. Error handling

| Problem | Result |
|---|---|
| `sprites/` without valid `sprites.json` | Error; that set skipped (vanilla sprite if no set is left). |
| Sheet missing, unreadable or of the wrong size | Error naming the file and expected size; that sheet skipped (fallbacks §6.1). |
| No `idle*` sheet | Set invalid, error. |
| `death_static.png` unreadable | Warning; last death frame used. |
| `spriteHeight` not a positive number | NPC skipped, error (parser). |

## 8. Performance

Sheets are loaded once per folder and shared by every person using them. A full set is large (the wench set is about 7 sheets of up to 2064×2048 RGBA); textures are loaded without mipmaps and compressed after loading to keep memory down. Loading happens at start-up with the definitions.

## 9. Architecture

| Unit | Job |
|---|---|
| `SpriteSetParser` (Core) | `sprites.json` text → `SpriteSet` (scale, ground, fps, animations) or error; validates sheet sizes given image sizes. |
| `SpriteStates` (Core) | Sheet name → state + variant; state fallbacks; variant pick. |
| `SpriteDirections` (Core) | Camera/forward angle → direction row (the DFU formula). |
| `SpriteLibrary` (Runtime) | Loads every NPC folder's sets, textures and `death_static.png`; caches per folder. |
| `NpcSprite` (Runtime) | The quad on a live NPC: state, direction, frame, variant; hides the vanilla renderer. |
| `NpcCorpseSprite` (Runtime) | Death animation, corpse frame or `death_static`, hides the loot pile's picture, follows its lifetime. |
| `DefinitionParser` | `spriteHeight`. |
| `NpcSpawner` | Picks a set per person, adds `NpcSprite`. |
| `NpcBrain` | Hands over to `NpcCorpseSprite` on death. |

## 10. Testing

- Core: `sprites.json` parsing and every error row; sheet-size validation; name → state/variant mapping and fallbacks; direction formula for all 8 sectors and the wrap-around; set choice repeatable per seed.
- In-game self-test (with a small generated test set): an ANPC with sprites hides the vanilla renderer and shows its quad; the direction row changes as the camera moves around it; the frame advances; state switches to walk while moving; after death a corpse sprite exists at the spot and the loot pile's picture is hidden; with `death_static.png` the corpse shows that image.
- Manual (user): the wench set in a test template in their install — size next to vanilla townsfolk, lighting at day/night, directions, death and corpse.

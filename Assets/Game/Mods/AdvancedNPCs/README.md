# Advanced NPCs

Persistent, data-defined NPCs ("ANPCs") for Daggerfall Unity 1.9.2. Each ANPC is a folder with an
`npc.json`. ANPCs stay calm, only fight whoever attacks them, fight or flee by bravery, report crimes,
calm down over in-game hours, and keep their state (alive/dead, hostile/calm, health) through visits
and save/load.

## Folder layout

```
DaggerfallUnity_Data/StreamingAssets/ANPCs/
  _Portraits/        shared portrait PNGs
  _Namelists/        custom name lists
  commoner/          one folder per ANPC; the folder name is its id
    npc.json
```

- Folder names use lowercase letters, digits and `_`. Never rename a folder after release: the name keys save data.
- Folders starting with `_` hold shared data and are not ANPCs.
- Unknown fields in `npc.json` are reported in `Player.log` and ignored (catches typos).
- Every ANPC is a **generic** template (people that add life to towns) unless its `npc.json` says `"kind": "unique"`.

## Making a unique ANPC

1. Install the mod and enable **Advanced NPCs** in the DFU mod list.
2. In game, stand outdoors where the ANPC should live, open the console (`~`) and type `anpc_pos`.
3. Create `StreamingAssets/ANPCs/my_npc/npc.json` with a `name` and the two lines `anpc_pos` printed:

```json
{
  "kind": "unique",
  "name": "My NPC",
  "location": { "region": "Daggerfall", "place": "Daggerfall" },
  "position": [12.5, 1, -3.25]
}
```

4. Restart the game. Check `Player.log` for lines starting with `[AdvancedNPCs]` — every problem names the folder, file and field.
5. To move it later, stand at the new spot and type `anpc_place my_npc`.

## Fields (unique ANPCs)

| Field | Required | Default | Allowed values |
|---|---|---|---|
| `kind` | yes | `generic` | must be `unique` for a unique ANPC |
| `name` | yes | — | any text |
| `location` | yes | — | from `anpc_pos` |
| `position` | yes | — | from `anpc_pos` (relative to the town's origin corner) |
| `race` | no | the region's people | `Breton`, `Redguard`, `Nord` |
| `portrait` | no | vanilla face | portrait name, see [Portraits](#portraits) |
| `baseClass` | no | `Spellsword` | Mage, Spellsword, Battlemage, Sorcerer, Healer, Nightblade, Bard, Burglar, Rogue, Acrobat, Thief, Assassin, Monk, Archer, Ranger, Barbarian, Warrior, Knight |
| `gender` | no | fixed random per ANPC | `Male`, `Female` |
| `bravery` | no | `Normal` | `Coward` (always flees), `Normal` (flees at low health), `Brave` (fights to the death) |
| `fleeHealthPercent` | no | `25` | 1–99, used by `Normal` |
| `calmDownHours` | no | `[6, 48]` | `[min, max]` in-game hours before a hostile ANPC forgives the player |
| `crimeOnAttack` | no | `true` | `true`: attacking is assault, killing is murder |
| `wanderRadius` | no | `8` | metres around the spawn point; `0` stands still |


## Generic townsfolk

A generic template makes several different people per town, alongside vanilla citizens:

```json
{
  "kind": "generic",
  "portraits": ["commoner_1", "commoner_2"],
  "spawn": {
    "locationTypes": ["TownCity", "TownHamlet", "TownVillage"],
    "count": [1, 3]
  },
  "baseClass": "Bard",
  "bravery": "Coward"
}
```

| Field | Default | Meaning |
|---|---|---|
| `name` / `names` | generated | every instance has `name`, or one of `names` is picked; neither: a DFU-style name for its race and gender |
| `portrait` / `portraits` | vanilla face | one portrait name, or several whose pools are joined, see [Portraits](#portraits) |
| `nameList` | `default_<race>` | name list for generated names, see [Name lists](#name-lists) |
| `spawn.locationTypes` | `TownCity`, `TownHamlet`, `TownVillage` | location types the template appears in |
| `spawn.places` | all matching towns | list of `{ "region", "place" }`; only those towns. Add `"positions": [[x, y, z], …]` (from `anpc_pos`) for fixed spots |
| `spawn.count` | `[1, 3]` | instances per town, `0 <= min <= max <= 20` |

All behaviour fields of unique ANPCs (`race`, `baseClass`, `gender`, `bravery`, …) work the same. `location`,
`position` and `dialogue.json` are for unique ANPCs only. Instances are named `<template>@<map id>#<n>` in
`anpc_list` and in save data.

## Settings

In DFU's mod list, select Advanced NPCs → Settings → **Population**:

- **Generic people** — *Same people every visit* (default): each town keeps the same generic people, and what happens to them (killed, angry, hurt) is saved. *Random each visit*: re-rolled whenever the town loads; nothing about them is saved.
- **Max generic per town** — 0 to 30 (default 12); 0 turns generic ANPCs off.

Changes apply the next time a town loads.

## Upgrading from 0.1

On the first start, every valid `StreamingAssets/AdvancedNPCs/<name>.json` is copied to
`StreamingAssets/ANPCs/<id>/npc.json` (its `id` line becomes `"kind": "unique"`) and the old file is renamed to
`<name>.json.migrated`. Existing folders are never overwritten. Saves keep each NPC's state because the
folder name is the old id.

## Console commands

- `anpc_pos` — your position as definition JSON (also written to `Player.log`).
- `anpc_list` — spawned ANPCs (unique and generic) with key, name, distance, direction, height and state.
- `anpc_place <id>` — makes your current spot that unique ANPC's home: rewrites `location` and `position` in its `npc.json` (nothing else) and moves it here.
- `anpc_summon <id or key>` — moves a spawned ANPC in front of you (testing only; not saved).
- `anpc_selftest` — runs the behaviour checks with temporary ANPCs next to you (god mode on, crimes recorded instead of punished, game clock untouched); results on screen and in `Player.log`.

## Behaviour

- Calm ANPCs never stop you resting or travelling.
- They only fight whoever attacked them; hitting one ANPC does not anger the others.
- Killed ANPCs stay dead in that save. A new game brings them back.


## Talking and portraits

- Click a calm ANPC in Talk or Grab mode to open DFU's citizen talk window with its name and portrait; vanilla topics work as for any citizen.
- Hostile or fleeing ANPCs refuse to talk. Info mode shows "You see <name>.".

## Portraits

- PNG files in `ANPCs/_Portraits/`, shared by all ANPCs; 64 × 64 pixels recommended.
- `"portrait": "bram"` uses `bram.png` and every `bram_<number>.png` (`bram_1.png`, `bram_2.png`, …) as a pool: each person gets one of them. A lone `bram_1.png` works too.
- The face a person shows the first time you talk to them is kept for the rest of that game, even if you add or remove files later (*Random each visit* people are re-rolled anyway). This is only saved when the pool has more than one picture.
- Without a portrait, or if no file matches, the ANPC shows a vanilla face matching its race and gender.

## Sprites

An ANPC folder can have its own 8-direction sprite sheets instead of the vanilla class sprite:

```
ANPCs/wench/
  npc.json
  sprites/            one sprite set; several as sprites_1/, sprites_2/, … (each person gets one)
    sprites.json
    idle_1.png  idle_2.png  walk_1.png  hit_1.png  attack_1.png  death.png
    death_static.png  (optional)
```

- Make them with `Tools~/render-sprites.py` from a rigged, animated `.blend`: one sheet per Action, rows = directions (front, front_right, right, back_right, back, back_left, left, front_left), columns = frames, plus `sprites.json` (scale, frame sizes, feet row, fps). Run with `test` first to check light and size:
  `blender -b character.blend --python render-sprites.py -- <out_dir> [test] [--rig NAME] [--mesh NAME]`
- Sheet names pick the animation: `idle*` (standing), `walk*` (moving), `hit*` (hurt), `attack*` (attacking), `death*` (dying). Numbered names (`idle_1`, `idle_2`) are variants picked at random. Missing walk/hit/attack sheets use idle; a set needs at least one idle sheet.
- Every hit (any health loss) plays a hit sheet from its first frame to its last, also when the person is hit again while it plays.
- Size: the standing pose is as tall as the vanilla class sprite; set `"spriteHeight"` (world units) in `npc.json` to change it. Every animation uses the same scale.
- Death: the death sheet plays and the body stays as the corpse. `death_static.png` (one picture, always facing you) replaces the last death frame; `"deathStatic": { "groundY": n }` in `sprites.json` sets its ground row. The loot pile stays clickable. After leaving the area or reloading, DFU shows its own corpse picture again.
- Sheets may be edited in an image editor, but must keep the size `sprites.json` describes (checked at start; wrong sizes are reported in `Player.log`). Other files in the folder (`.psd`, backups) are ignored.
- Combat timing (when a swing hits) stays DFU's own, so custom attack frames can look slightly early or late.

## Name lists

- Generic people without `name`/`names` get generated names. `"nameList"` chooses the list: a custom file in `ANPCs/_Namelists/` (without `.json`) or a vanilla one: `default_breton`, `default_redguard`, `default_nord`, `default_darkelf`, `default_highelf`, `default_woodelf`, `default_khajiit`, `default_imperial`. Without `nameList` the person's own race is used.
- Custom lists come in two formats, described in `Examples/ANPCs/_Namelists/README.txt`: a bank copied from DFU's `NameGen.txt` plus a `"style"`, or plain `male`/`female`/`surnames` lists. Simple lists can put a gendered prefix before the surname (`maleSurnamePrefix`/`femaleSurnamePrefix`, e.g. Orsimer `gro-`/`gra-`). Two ready-made lists ship: `orsimer` and `argonian`.
- A missing or broken list falls back to the race's vanilla list (one warning in `Player.log`).

## Examples

`Examples/ANPCs/` holds four generic templates for cities, towns and villages: `commoner` (one to three
cowardly commoners per town) and `cooper` (Normal, uses portrait `bram`), `baker` (Coward) and `smith` (Brave),
each at most once per town. Use `anpc_list` to find them.

## Building (developers)

Sources live in `Scripts/Core` (pure logic, unit-tested) and `Scripts/Runtime` (DFU glue).
`Tools~` (ignored by Unity) holds:

- `run-tests.sh` — runs the Core NUnit tests in a small Unity host project.
- `compile-check.sh` — compiles every mod source against a DFU install's own assemblies.
- `runtime-check.sh` — compiles the sources with DFU's own in-memory runtime compiler and loads every type, catching problems only the game's compiler has (e.g. nested enums cause TypeLoadException there).
- `build-mod.sh` — builds `advancednpcs.dfmod` with Unity 2019.4.41f2 (a windowed editor that closes itself) and installs it, plus `Examples/ANPCs`, into a DFU install. In the editor the same build is under **Daggerfall Tools → Build Advanced NPCs mod**.
- `selftest.sh [character] [save]` — fully unattended in-game test: starts DFU, loads a save standing outdoors in a town, runs `anpc_selftest`, quits and prints the results. Temporarily skips the startup options screen (settings.ini is restored).
- `play.sh [character] [save]` — starts DFU straight into a save (default `Testo` / `AtDaggerfall`) for manual testing: no options screen, no title menu, no self-test.

When adding a `.cs` file under `Scripts/`, also add it to `Files` in `AdvancedNPCs.dfmod.json`.

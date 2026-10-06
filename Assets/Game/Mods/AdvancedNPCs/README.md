# Advanced NPCs

Persistent, data-defined NPCs ("ANPCs") for Daggerfall Unity 1.9.2. Each ANPC is a folder with an
`npc.json`. ANPCs stay calm, only fight whoever attacks them, fight or flee by bravery, report crimes,
calm down over in-game hours, and keep their state (alive/dead, hostile/calm, health) through visits
and save/load.

## Folder layout

```
DaggerfallUnity_Data/StreamingAssets/ANPCs/
  _Portraits/        shared portrait PNGs
  bram/              one folder per ANPC; the folder name is its id
    npc.json
```

- Folder names use lowercase letters, digits and `_`. Never rename a folder after release: the name keys save data.
- Folders starting with `_` hold shared data and are not ANPCs.
- Unknown fields in `npc.json` are reported in `Player.log` and ignored (catches typos).

## Making a unique ANPC

1. Install the mod and enable **Advanced NPCs** in the DFU mod list.
2. In game, stand outdoors where the ANPC should live, open the console (`~`) and type `anpc_pos`.
3. Create `StreamingAssets/ANPCs/my_npc/npc.json` with a `name` and the two lines `anpc_pos` printed:

```json
{
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
| `kind` | no | `unique` | `unique` (one fixed person) |
| `name` | yes | — | any text |
| `location` | yes | — | from `anpc_pos` |
| `position` | yes | — | from `anpc_pos` (relative to the town's origin corner) |
| `race` | no | the region's people | `Breton`, `Redguard`, `Nord` |
| `portrait` | no | vanilla face | name of a PNG in `ANPCs/_Portraits/` without `.png` (any case) |
| `baseClass` | no | `Spellsword` | Mage, Spellsword, Battlemage, Sorcerer, Healer, Nightblade, Bard, Burglar, Rogue, Acrobat, Thief, Assassin, Monk, Archer, Ranger, Barbarian, Warrior, Knight |
| `gender` | no | fixed random per ANPC | `Male`, `Female` |
| `bravery` | no | `Normal` | `Coward` (always flees), `Normal` (flees at low health), `Brave` (fights to the death) |
| `fleeHealthPercent` | no | `25` | 1–99, used by `Normal` |
| `calmDownHours` | no | `[6, 48]` | `[min, max]` in-game hours before a hostile ANPC forgives the player |
| `crimeOnAttack` | no | `true` | `true`: attacking is assault, killing is murder |
| `wanderRadius` | no | `8` | metres around the spawn point; `0` stands still |

## Upgrading from 0.1

On the first start, every valid `StreamingAssets/AdvancedNPCs/<name>.json` is copied to
`StreamingAssets/ANPCs/<id>/npc.json` (without its `id` line) and the old file is renamed to
`<name>.json.migrated`. Existing folders are never overwritten. Saves keep each NPC's state because the
folder name is the old id.

## Console commands

- `anpc_pos` — your position as definition JSON (also written to `Player.log`).
- `anpc_list` — spawned ANPCs with distance, direction, height and state.
- `anpc_place <id>` — makes your current spot that unique ANPC's home: rewrites `location` and `position` in its `npc.json` (nothing else) and moves it here.
- `anpc_summon <id>` — moves a spawned ANPC in front of you (testing only; not saved).
- `anpc_selftest` — runs the behaviour checks with temporary ANPCs next to you (god mode on, crimes recorded instead of punished, game clock untouched); results on screen and in `Player.log`.

## Behaviour

- Calm ANPCs never stop you resting or travelling.
- They only fight whoever attacked them; hitting one ANPC does not anger the others.
- Killed ANPCs stay dead in that save. A new game brings them back.


## Talking and portraits

- Click a calm ANPC in Talk or Grab mode to open DFU's citizen talk window with its name and portrait; vanilla topics work as for any citizen.
- Hostile or fleeing ANPCs refuse to talk. Info mode shows "You see <name>.".
- Portraits are PNG files in `ANPCs/_Portraits/`, shared by all ANPCs; 64 × 64 pixels recommended. Without one (or if the file is missing) the ANPC shows a vanilla face matching its race and gender.

## Examples

`Examples/ANPCs/` holds three Daggerfall city ANPCs: Bram (Normal), Cora (Coward) and Bors (Brave). They
stand next to each other at Daggerfall's north-west gate; use `anpc_list` or `anpc_summon` if they are not
in view.

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

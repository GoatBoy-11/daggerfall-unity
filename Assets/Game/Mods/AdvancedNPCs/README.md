# Advanced NPCs

Persistent, named town NPCs for Daggerfall Unity 1.9.2. Each NPC is one JSON file. NPCs stay calm,
only fight whoever attacks them, fight or flee by bravery, report crimes, calm down over in-game
hours, and keep their state (alive/dead, hostile/calm, health) through visits and save/load.

## Making your own NPC

1. Install the mod and enable **Advanced NPCs** in the DFU mod list.
2. Create the folder `DaggerfallUnity_Data/StreamingAssets/AdvancedNPCs/` in your game folder if it does not exist.
3. In game, stand outdoors where the NPC should live, open the console (`~`) and type `anpc_pos` (or, for an NPC that already exists, `anpc_place <id>` to move it here and save the spot).
4. Make a new file `my_npc.json` in that folder with an `id`, a `name`, and the two lines `anpc_pos` printed:

```json
{
  "id": "my_npc",
  "name": "My NPC",
  "location": { "region": "Daggerfall", "place": "Daggerfall" },
  "position": [12.5, 1, -3.25]
}
```

5. Restart the game. Check `Player.log` for lines starting with `[AdvancedNPCs]` — every problem names the file and field.

## Fields

| Field | Required | Default | Allowed values |
|---|---|---|---|
| `id` | yes | — | lowercase letters, digits, `_`; unique; never change it after release (it keys save data) |
| `name` | yes | — | any text |
| `location` | yes | — | from `anpc_pos` |
| `position` | yes | — | from `anpc_pos` (relative to the town's origin corner) |
| `baseClass` | no | `Spellsword` | Mage, Spellsword, Battlemage, Sorcerer, Healer, Nightblade, Bard, Burglar, Rogue, Acrobat, Thief, Assassin, Monk, Archer, Ranger, Barbarian, Warrior, Knight |
| `gender` | no | random | `Male`, `Female` |
| `bravery` | no | `Normal` | `Coward` (always flees), `Normal` (flees at low health), `Brave` (fights to the death) |
| `fleeHealthPercent` | no | `25` | 1–99, used by `Normal` |
| `calmDownHours` | no | `[6, 48]` | `[min, max]` in-game hours before a hostile NPC forgives the player |
| `crimeOnAttack` | no | `true` | `true`: attacking is assault, killing is murder |
| `wanderRadius` | no | `8` | metres around the spawn point; `0` stands still |

## Console commands

- `anpc_pos` — your position as definition JSON (also written to `Player.log`).
- `anpc_list` — spawned NPCs with distance, direction, height and state.
- `anpc_place <id>` — makes your current spot that NPC's home: rewrites `location` and `position` in its definition file (nothing else) and moves it here.
- `anpc_summon <id>` — moves a spawned NPC in front of you (testing only; not saved).

## Behaviour

- Calm NPCs never stop you resting or travelling.
- They only fight whoever attacked them; hitting one NPC does not anger the others.
- Killed NPCs stay dead in that save. A new game brings them back.

## Examples

`Examples/` holds three Daggerfall city NPCs: Bram (Normal), Cora (Coward) and Bors (Brave). They stand
next to each other at Daggerfall's north-west gate; use `anpc_list` or `anpc_summon` if
they are not in view, and `anpc_pos` to move them.

## Building (developers)

Sources live in `Scripts/Core` (pure logic, unit-tested) and `Scripts/Runtime` (DFU glue).
`Tools~` (ignored by Unity) holds:

- `run-tests.sh` — runs the Core NUnit tests in a small Unity host project.
- `compile-check.sh` — compiles every mod source against a DFU install's own assemblies.
- `runtime-check.sh` — compiles the sources with DFU's own in-memory runtime compiler and loads every type, catching problems only the game's compiler has (e.g. nested enums cause TypeLoadException there).
- `build-mod.sh` — builds `advancednpcs.dfmod` with Unity 2019.4.41f2 in batch mode and installs it (plus the examples) into a DFU install. Needs an activated Unity license. In the editor the same build is under **Daggerfall Tools → Build Advanced NPCs mod**.

When adding a `.cs` file under `Scripts/`, also add it to `Files` in `AdvancedNPCs.dfmod.json`.

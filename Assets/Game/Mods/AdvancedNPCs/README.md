# Advanced NPCs

Persistent, data-defined NPCs ("ANPCs") for Daggerfall Unity 1.9.2. Each ANPC is a folder with an
`npc.json`. ANPCs stay calm, fight whoever attacks them (even when the attack misses; brave ones also attack enemies they see), fight or flee by bravery, report crimes,
calm down over in-game hours, and keep their state (alive/dead, hostile/calm, health) through visits
and save/load.

- Allies take sides: when an ANPC is attacked (or killed outright), calm ANPCs of the same building (a guild hall's
  members, a shop's staff, a tavern's crowd), of the same spawn group, or standing in for the same vanilla faction join
  in if they are within 20 m and see it (within 8 m they hear it). All but cowards fight the attacker; cowards run.
  Companions, guards and enemies keep out of it.
- An ANPC spawned inside another person (a shown vanilla flat NPC or another ANPC on the same spot) steps to a free
  spot close by, which becomes its home. Indoors ANPCs start facing the most open way, into the room.
- Clicking an ANPC to talk turns it to face you.

## Folder layout

```
DaggerfallUnity_Data/StreamingAssets/ANPCs/
  _Portraits/        shared portrait PNGs
  _Namelists/        custom name lists
  _Dialogue/         dialogue types: topics many ANPCs can share
  commoner/          one folder per ANPC; the folder name is its id
    npc.json
    dialogue.json    optional: topics only this ANPC (or template) has
```

- Folder names use lowercase letters, digits and `_`. Never rename a folder after release: the name keys save data.
- Folders starting with `_` hold shared data and are not ANPCs.
- Unknown fields in `npc.json` are reported in `Player.log` and ignored (catches typos).
- Every JSON file the mod reads accepts `//` line and `/* */` block comments. The commented `example_*` folders (manual spawn only) and `_Examples/example_unique` (never loaded) document each field.
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
| `race` | no | the region's people | `Breton`, `Redguard`, `Nord`, `DarkElf` (or `Dunmer`), `Khajiit`, `Argonian` |
| `portrait` | no | vanilla face | portrait name, see [Portraits](#portraits) |
| `baseClass` | no | `Spellsword` | Mage, Spellsword, Battlemage, Sorcerer, Healer, Nightblade, Bard, Burglar, Rogue, Acrobat, Thief, Assassin, Monk, Archer, Ranger, Barbarian, Warrior, Knight; for enemies also a creature, see [Enemies](#enemies) |
| `attitude` | no | `calm` | `calm` (townsfolk) or `hostile` (an enemy), see [Enemies](#enemies) |
| `hostileHours` | no | always | with `hostile`: `[from, to]` whole hours 0–23, e.g. `[20, 6]` |
| `gender` | no | fixed random per ANPC | `Male`, `Female` |
| `bravery` | no | `Normal` | `Coward` (always flees), `Prudent` (flees below `fleeHealthPercent`), `Normal` (once below half of it, a `fleeChance` % chance to flee, rolled once), `Brave` (never flees; when calm, attacks enemies it sees within `aggroRange`) |
| `fleeHealthPercent` | no | `25` | 1–99, used by `Prudent` (`Normal` uses half of it) |
| `fleeChance` | no | `30` | 0–100: `Normal`'s chance to flee once badly hurt |
| `aggroRange` | no | `15` | 1–50 m: how far a calm `Brave` ANPC looks for enemies (enemy ANPCs and hostile vanilla enemies, never the city watch) |
| `calmDownHours` | no | `[6, 48]` | `[min, max]` in-game hours before a hostile ANPC forgives the player |
| `crimeOnAttack` | no | `true` | `true`: attacking is assault, killing is murder |
| `wanderRadius` | no | `8` | metres around the spawn point; `0` stands still |
| `no_companion_slot` | no | `false` | Recruitable without consuming a limited party slot; recruitment still requires `recruitCost` |
| `combat` | no | vanilla, no bow, runs | `{ "health": 0.7, "attackSpeed": 1.3, "rangedAtDistance": true, "runSpeed": 1.2, "canRun": true }`, see [Combat tuning](#combat-tuning) |
| `dialogue` | no | none | dialogue type(s) from `_Dialogue/`, e.g. `"tavern_wench"` or `["gossip", "tavern_wench"]`, see [Dialogue topics](#dialogue-topics) |

Before 0.12, `"Normal"` meant "always flees at low health"; use `"Prudent"` for that now.


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

All behaviour fields of unique ANPCs (`race`, `baseClass`, `gender`, `bravery`, `dialogue`, …) work the same, and
a template's `dialogue.json` gives its topics to every person made from it. `location` and `position` are for
unique ANPCs only. Instances are named `<template>@<map id>#<n>` in
`anpc_list` and in save data.

## Settings

In DFU's mod list, select Advanced NPCs → Settings → **Population**:

- **Generic people** — *Same people every visit* (default): each town keeps the same generic people, and what happens to them (killed, angry, hurt) is saved. *Random each visit*: re-rolled whenever the town loads; nothing about them is saved.
- **Max generic per town** — 0 to 30 (default 12); 0 turns generic ANPCs off. Also the most generic ANPCs in one building.
- **Dungeons / Interiors / Wilderness** — on (default) or off: generic ANPCs with a `dungeons`, `interiors` or `wilderness` spawn block, see [Spawning outside towns](#spawning-outside-towns).
- **Max wilderness ANPCs around you** — 0 to 10 (default 4).

These apply the next time a town loads.

**Companions** (apply to new recruitment immediately):

- **Max followers** — 3 (default), 4, 5, 6, 7, 8, 9, 10, or **Unlimited (Not Recommended)**.
  Living hired companions count whether following or waiting, including those in unloaded areas.
  Dismissed or dead companions do not count. A full party gets **Too many followers!** on a hire attempt,
  with no gold charged and no contract change. Lowering the limit keeps existing companions and
  their orders; ordinary recruits can join again once there is room. Unlimited removes this cap.
  NPCs with `no_companion_slot: true` do not consume a slot and can join a full party.

**Corpses** (ANPCs with their own sprite sets; apply at once):

- **Death animation** — on (default): the death sheet plays, then the body lies still. Off: the body appears at once, as in vanilla Daggerfall.
- **Directional corpses** — off (default): a body on its last death frame shows its front or back (chosen at random when it dies) and always faces you, like vanilla corpses and `death_static.png`. On: it shows the side you see it from, as it turns while you walk around.

**Sounds** (apply at once):

- **ANPC sounds** — *ANPC* (default): each ANPC's own [Sounds](#sounds) folder, Daggerfall's voice where it has none. *Vanilla*: Daggerfall's voice for every ANPC; Sounds folders are not used for voices. *Off*: ANPCs make no voice sounds at all (weapon and creature idle sounds stay).
- **Footsteps** — on (default) or off: ANPC footsteps (own and default), whichever **ANPC sounds** is set to.
- **Steps volume** — 0 to 100 (default 100): footstep volume in percent of Daggerfall's sound volume.

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
- `anpc_spawn <template>` — makes a new person from a generic template (e.g. `anpc_spawn wench`) in front of you, rolled like the template's other people (name, portrait, sprite set). They stay at that spot for the rest of this game, saved with it: outdoors in a town (key `<template>@<map id>+<n>`), inside a building (`<template>@<map id>:b<building>+<n>`) or inside a dungeon (`<template>@<map id>:dungeon+<n>`). Not in the wilderness. This works in either GenericPeople mode.
- `anpc_remove <key>` — removes a person made with `anpc_spawn` from the world and from your save.
- `anpc_hostile <id or key> [on|off]` — turns an ANPC into an enemy or calms it (without on/off it toggles); saved with your game. See [Enemies](#enemies).
- `anpc_opinion <id or key> [value]` — shows or sets an ANPC's personal opinion of you (−100 to 100); saved with your game.

- `anpc_selftest healing` — runs the focused support-healing checks (eligibility, cost, AI, animation and audio); details in `Player.log`.
- `anpc_info [id or key]` — the ANPC in front of you (within 5 m) or that ANPC: id, key, kind, folder, bravery, mode, health, opinion, sprite set (loaded, loading, vanilla, or shown by a dead ANPC's corpse) and height, sound pools and where its footsteps come from (`steps: own`, `default` or `none`), its conversation setting (and partner and line while talking, or why it cannot talk now), its current target, distance and update rate.
- `anpc_schedule [id or key]` — explains every schedule entry at the current game time: selected, outside hours, first unmet condition, or overridden by another slot. Includes the current activity and reports combat/fleeing/death or companion overrides.
- `anpc_reload_schedules [folder]` — reloads only schedules from existing loaded definitions, optionally one folder. Invalid JSON, overlapping ordinary slots, bad conditions, missing packages and invalid package parameters retain that NPC's previous schedule. Other valid NPC schedules still reload. Other `npc.json` fields require a restart.
- `anpc_route start <id or key>` / `add` / `finish` / `cancel` — records patrol waypoints while you walk and prints package JSON; see [Recording a patrol route](#recording-a-patrol-route).
- `anpc_chat` — starts a conversation at once between the ANPC in front of you (within 5 m) and the nearest other ANPC within 4 m of it, ignoring the chance and the 2-hour wait; says why not when one of them cannot talk. See [Conversations](#conversations).
- `anpc_topics [id or key]` — the dialogue topics of the ANPC in front of you (within 5 m) or of that ANPC: which are shown, and for the others the condition that hides them. See [Dialogue topics](#dialogue-topics).
- `anpc_reload_dialogue` — re-reads `_Dialogue/*.json` and every `dialogue.json` without restarting. Use `anpc_reload_schedules` for schedule edits; other `npc.json` changes still need a restart.
- `anpc_reload_assets [id]` — re-reads the sprite sets and [Sounds](#sounds) of that ANPC folder (or of all, with the default steps in `_Sounds`; `anpc_reload_assets _Sounds` re-reads only those) from disk; spawned ANPCs using them switch to the new files (also ANPCs that had no Sounds). ANPCs that had the vanilla sprite, and changes to walk/run/greet sheets or `npc.json`, take effect when they next spawn; only whether they can move at all changes at once (a set left without walk/run sheets makes them stand still, see turrets). (`anpc_reload_sprites` is the old name.)
- `anpc_flag` — lists the dialogue flags that are set; `anpc_flag <name> on|off` sets or clears one (saved with your game).
- `anpc_here` — in a dungeon, building or the wilderness: which templates can appear there and what happened (filter, `when`, the chance roll, how many came). See [Spawning outside towns](#spawning-outside-towns).
- `anpc_selftest` — runs the behaviour checks with temporary ANPCs next to you (god mode on, crimes recorded instead of punished, game clock untouched); results on screen and in `Player.log`.

## Behaviour

- Calm ANPCs never stop you resting or travelling.
- They only fight whoever attacked them; hitting one ANPC does not anger the others.
- Killed ANPCs stay dead in that save. A new game brings them back.

Each ANPC has its own opinion of you (−100 to 100, saved), added to the region's reaction for greetings and the dialogue `reaction` condition. Hitting it costs 25, being seen hitting or killing another ANPC costs 10 with calm onlookers within 15 m, and helping it in a fight (its enemy dies with you within 20 m) gains 10; dialogue replies can change it with `"opinion"`. It drifts back toward 0 by 2 points a day.

Performance: ANPCs far from you update their animation and calm decisions less often (every 3rd frame beyond 40 m, every 8th beyond 100 m; always every frame while fighting, fleeing or acting). Nothing changes up close.

## Editor support (JSON schemas)

`Schemas~/` holds JSON schemas for `npc.json`, `sprites.json` and dialogue files: editors such as VS Code then autocomplete field names and flag typos as you type. Copy the folder to `ANPCs/_Schemas/` (folders starting with `_` are not ANPCs) and add a line at the top of a file:

```json
{
  "$schema": "../_Schemas/npc.schema.json",
  "race": "Breton"
}
```

Use `../../_Schemas/sprites.schema.json` in a `sprites` folder and `../_Schemas/dialogue.schema.json` in `_Dialogue/` or an ANPC folder. The mod ignores the `"$schema"` member. `Tools~/check-schemas.py` keeps the schemas in step with the mod's own field lists.

## Guard authority and enforcement

Set `"guard": true` in a peaceful human ANPC's `npc.json` to give it guard authority.
The supplied `castle_guard_1` example enables it. Its normal class, sprites, combat tuning,
and schedule stay in use; `guard_spot` alone still only holds a post. Restart DFU after
changing the definition.

Guards detect wanted targets within `aggroRange` with a clear line of sight in the same
loaded area. Pursuit interrupts their routine, uses the existing navigation without
teleporting, and gives up when the target escapes or becomes unreachable. They resume
normal behaviour afterwards. Guards use melee while enforcing the law; configured
ranged attacks and spells resume outside enforcement. Stationary sprite sets remain
stationary.

Player guilt comes from DFU's active `CrimeCommitted`, including crimes reported by other
mods. On reaching the player, guards offer the native surrender dialogue. Accepting uses
DFU's court and punishment (including its health reduction); refusing permits combat.
A potentially fatal melee hit uses DFU's forced-surrender rules, which can reject surrender.
ANPC guards share one offer and reputation charge, and respect an existing native guard
offer. Vanilla guard spawning remains enabled. The guard keeps its ordinary class because
DFU automatically deletes native `Knight_CityWatch` entities when the crime clears.

NPC guilt is registered explicitly, independently of hostility or team membership:

```text
anpc_crime <spawned id or key> Assault
anpc_crime <spawned id or key> None
```

Other ANPC runtime code can call `NpcCrime.ReportNpc(key, PlayerEntity.Crimes.Assault)`;
other mods can send `ReportCrime` with data `"key|Assault"` (or `"key|None"`), receiving a
boolean callback. Offences are scoped to the current region and saved for persistent
NPC identities. Unknown actors, guards, companions and random-per-visit identities are
rejected. Register offences from an event that knows the actual offender; ordinary
hostility is not automatically treated as evidence of crime.

Guards attack offending NPCs and try to kill them, regardless of bravery or remaining
health. NPCs do not surrender to guards and are not taken into custody. Death uses the
normal corpse and saved-death behaviour. Old saves' custody timers are ignored, so
previously detained NPCs may spawn again on their next area entry. Pending offences
still survive saving/loading. Pursuit targets and paths are recalculated after loading.
`anpc_info` shows guard and wanted status. `anpc_selftest guards` runs the focused
runtime checks outdoors in a town.

Guards shout when starting a pursuit, then every 15-30 seconds at random while pursuing
(vanilla guards repeat every 3-9 seconds). The timer stops while paused, AI is disabled,
or the guard is paralyzed. Add `Sounds/halt.wav`, `halt.ogg` or `halt.mp3`
to the ANPC's folder to replace the vanilla "Halt!"; numbered variants such as
`halt_1.wav` and `halt_2.wav` are picked randomly. Halt ignores `greetChance` and
`combatChance`, but follows the ANPC voice mode, pitch and volume settings. In ANPC
voice mode it waits for a configured clip to load, using the vanilla shout if none
can be decoded; without a halt file the vanilla shout plays immediately. Vanilla
voice mode always uses the default shout, and Off mutes both.

## AI packages and schedules

Packages are optional. Without a schedule, ANPCs use their current AI. Schedules select routine
behaviour; combat, flight, greetings, conversations and healing can interrupt it.

Put reusable packages in `ANPCs/_Packages/<name>.json`:

```json
{
  "type": "wander",
  "parameters": { "radius": 6, "pause": 5 }
}
```

Reference that filename (without `.json`) in any unique or generic NPC:

```json
"schedule": [
  { "hours": ["08:00", "18:00"], "package": "local_patrol" },
  { "hours": ["18:00", "23:00"], "package": "local_wander", "parameters": { "radius": 3 } },
  { "package": "stand" }
]
```

- Times use the in-game clock, exactly `HH:mm`, 00:00–23:59. Start included, end excluded.
  `["23:00", "07:00"]` crosses midnight. Equal endpoints are invalid; omit hours for all day.
- One unconditional entry may omit `hours`: it is the all-day fallback. Unconditional timed entries
  take precedence over that fallback. Conditional entries (`when`) take priority over both:
  the first matching conditional entry in list order wins, provided its optional hours also match.
  Conditional entries may overlap or omit hours. With no eligible entry, original AI fills the gap.
- At most 64 entries. Overlapping unconditional timed slots, multiple unconditional fallbacks and malformed schedules disable
  the whole schedule with a warning; the NPC still loads normally. A missing or invalid selected
  package falls back to current AI for that slot, including when an all-day fallback exists.
- A slot's optional `parameters` override the package defaults for that NPC only. Unknown or invalid
  parameters disable that slot's package. Nothing edits the shared package or NPC definition.
- While running, a package replaces `wanderRadius` and `stayNear`, including the normal indoor
  wandering cap. Keep routes inside the current area. Stationary sprite sets still cannot move.
- `wander`, `wander_civilian`, `travel_to` and `patrol` use positions relative to the NPC's original home (spawn/replacement position),
  as `[x,y,z]` metre offsets in that area's axes, each component between -100 and 100.
  They remain portable across generic instances. Summoning/repositioning an NPC resets its home.
- Routes use direct collision-aware movement along authored waypoints. Place points around obstacles.
  Automatic pathfinding, opening doors, changing interiors and C# extension handlers are unsupported.
  Named spots and sequences compose routines within the loaded area.
- After 10 active seconds without progress, a coordinate-based package falls back to current AI and
  retries the same target after 15 active seconds, at most twice per package step. Patrol waypoint and
  sequence progress are retained; sequence timing pauses during fallback. After both retries fail,
  current AI runs until the next occurrence, a time jump or a reload. Paused/interrupted time does not count.
  Named spots keep their existing alternative-post/retry behaviour; `wander` and `wander_civilian` pick a new random point instead.
- On save load or area entry, the current schedule is selected and navigation starts fresh.
  Resting/travel/time jumps do not replay missed activities. Package waits use in-game seconds.

| Type | Parameters |
|---|---|
| `stand` | None; stand where the NPC is when this package takes control. |
| `work_at` | Optional `position` or `spot`, `animation` (default `idle`), `facing` (0-360 degrees), `run` (false). Walk to the post, face it, and loop a visual animation. |
| `sequence` | Required `steps`, optional `loop` (false). Reuse packages in order; step durations use active gameplay seconds. |
| `follow` | `target` (default `player`, otherwise exact spawned ANPC id/key), `distance` (0.5-20 m, default 2), `runDistance` (1-100 m, default 6; at least `distance + 0.5`). Follow within the loaded area, walk nearby, run farther away. |
| `guard_spot` | Optional `position: [x,y,z]` in current-area coordinates from `anpc_pos`, or named `spot`; omitted means current position at slot start. `idleInterval: [min,max]` defaults to `[6,15]` active gameplay seconds. Temporary Brave behaviour. |
| `play_animation` | `animation` (default `idle`), `only_interrupt_at_combat` (default false). Hold position and repeat full cycles; numbered names are exact, base names randomly select numbered variants without immediate repeats. |
| `wander` | `center` (default `[0,0,0]`), `radius` (0–50, default 8), `pause` (0–3600 game seconds, default 3), `run` (default false). |
| `wander_civilian` | `center` (default `[0,0,0]`), `radius` (0–50 m, default 40). Walk around town using vanilla road/surface preferences; occasionally stop for 1–4 full animation cycles. |
| `seek_shelter` | `radius` (1–24 m, default 16), `spot` (preferred named spot ids/tags; default `shelter`), `run` (false). Find reachable outdoor cover and wait beneath it. |
| `travel_to` | Required `target: [x,y,z]`; `run` (default false). Hold at the target, returning after displacement. |
| `patrol` | Required `points: [[x,y,z], ...]` (2–32); `pauseAtStops` (0–3600 game seconds, default 10), `run` (default false). Loop through the points. |
| `eat` | `mealCycles` (`[min,max]` 1–50, default `[2,5]`), `breakCycles` (`[min,max]` 0–50, default `[1,3]`). Stay put and eat/drink, see [Eating and drinking](#eating-and-drinking). |
| `chore` | `center`, `radius` (0–50, default 4), `animation` (family/sheet or list, default `sweep`, `chop`, `scrub`, `work`), `cycles` (`[min,max]` 1–50, default `[2,4]`), `run`. Work around home, see [Chores, hawking and socializing](#chores-hawking-and-socializing). |
| `hawk` | `range` (1–30 m, default 6), `interval` (`[min,max]` seconds, default `[10,20]`), `animation` (default `hawk`). Call out to the passing player. |
| `socialize` | `radius` (2–50 m, default 15), `interval` (`[min,max]` seconds, default `[20,40]`). Walk over to another ANPC and start a conversation. |

`wander_civilian` chooses its animation pool at each stop: 50% `idle*`, 50% available
`pray*`, `play*`, `emote*` or `dance*`. Names use the normal bare name or `_N` suffix
(e.g. `idle`, `idle_2`, `dance_10`); unrelated names such as `playful` do not match.
A new variant is chosen at each complete cycle, avoiding immediate repeats when possible.
Missing activity sheets fall back to idle. Stops last 1–4 complete cycles, independent of the
game clock; hurt, combat, greetings, talking and other ordinary interruptions take priority.
Town movement favours roads and keeps its heading like vanilla citizens; each reached tile
has a 25% chance to start a stop. Outside a town grid it uses ordinary radius wandering and
stops after each leg. Blocked civilian targets are retried rather than disabling
the package. ANPCs using vanilla sprites stand idle for 2–6 seconds instead. It stays within
the loaded area and does not open doors or change interiors.

To use the supplied `_Packages/wander_civilian.json`, add an all-day schedule to an NPC:

```json
"schedule": [{ "package": "wander_civilian" }]
```

An entry can override `parameters`, for example `"parameters": { "radius": 25 }`.
No existing NPC schedules are changed automatically.

Running respects `combat.canRun` and uses `combat.runSpeed` as its multiplier.
Use `Schemas~/package.schema.json` for package autocomplete and the updated NPC schema for schedules.

- `anpc_selftest packages`: focused in-game checks of takeover, holds, fallback and combat interruption.
- `anpc_reload_packages`: re-read package JSON files and restart the current activities.
- `anpc_reload_schedules [folder]`: validate and replace schedules from `npc.json`, then restart affected
  activities and refresh requested animation families. Existing generic instances and NPCs previously
  without a schedule update too. The previous schedule remains running when a replacement is invalid;
  rejection details appear in the console and `Player.log`. Checks use the currently loaded package library
  and variable definitions, so reload new packages first; adding/changing variable declarations needs a restart.
  An empty array, `null`, or removing `schedule` clears the routine. Nothing is written to definitions or saves.
- `anpc_schedule [id or key]`: inspect selection without changing the clock, conditions or priority rules.
  A selected entry may still be interrupted or fail at runtime; the current-activity line reports that separately.
- `anpc_info [id or key]`: includes selected slot/package, fallback reason and interruption status.
- `anpc_spawn package_tester`: opt-in example with day patrol, evening wander and night stand.
  From 07:00 to 08:00 it uses its original AI. It does not spawn automatically.

### Recording a patrol route

Stand at the first waypoint near a spawned ANPC and run `anpc_route start <id or key>`.
This captures the first point relative to that NPC's original home, rather than its current moving position.
Walk around obstacles, running `anpc_route add` at each waypoint, then `anpc_route finish`.
The console and `Player.log` receive a complete patrol package. Save it under `_Packages/<name>.json`,
run `anpc_reload_packages`, and reference `<name>` in the NPC's schedule. Use `anpc_reload_schedules`
after editing that schedule. The recorder never writes files or changes the NPC's routine.

Routes require 2-32 points, each within 100 metres of home on each axis. Record within the same loaded
town, building or dungeon; changing area or teleporting/repositioning the NPC prevents further capture
until you return or start again. `anpc_route cancel` discards an unfinished route. An unfinished route
cannot be replaced by another `start`. This records authored waypoints, without automatic pathfinding.

### Seeking outdoor shelter

Use the supplied `_Packages/seek_shelter.json` and add a conditional entry above the ordinary routine:

```json
"schedule": [
  { "package": "seek_shelter", "when": { "outdoors": true, "bad_weather": true } },
  { "package": "wander_civilian" }
]
```

`bad_weather` is a shared boolean condition for schedules, dialogue and spawn filters. `true` means
rain, thunderstorms or snow; `false` means none of those. Overcast and fog alone do not count.
It describes the weather independently of whether the NPC is indoors, so combine it with `outdoors`
for shelter routines. Clearing weather selects the normal scheduled package again.

An NPC already under usable cover stays there. Otherwise it tries up to 16 nearby matching named
spots, respecting their capacity, then samples up to 64 surrounding points within `radius`. Named
spots are preferred destinations, but must also have genuine solid cover, floor, standing clearance
and a directly accessible walking route. Create outdoor spots with `anpc_spot_pos`, tag them
`shelter`, and run `anpc_reload_spots`. A custom `spot` selector changes which authored spots are
preferred; automatic cover remains available if none is usable.

Cover is tested across the NPC's width, up to eight metres above its head. Trees represented by
billboards, triggers, people, walls, low ceilings and gaps in the walking surface do not qualify.
Automatic destinations reserve a small local cell to limit crowding. Routes stay within the loaded
outdoor area and use clear straight approaches; the NPC does not open doors, enter buildings,
teleport or navigate around corners. Narrow cover can be missed by the bounded sampling; authored
spots make such destinations discoverable.

With no usable cover, ordinary wander/leash AI takes over and the search retries after ten active
seconds. This fallback is the original AI, rather than the next schedule entry. Cover and access are
rechecked every two active seconds; blocked routes are abandoned after ten seconds without progress.
Combat, slot changes, death, despawn and reload release reservations. A sequence can use the package
with a duration, which starts on arrival. Stationary sprite sets can shelter where they already stand.
No sprite sheets, save fields or automatic changes to existing NPCs are required.

`anpc_info` reports seeking, sheltered, or why ordinary AI is being used. `shelter_tester` is a manual-only
example (`anpc_spawn shelter_tester` after installing the example files); it never spawns automatically.

### Repeated animation packages

`play_animation` holds the NPC's current position and loops a sheet declared in its own `sprites.json`.
Set the requested name on a schedule entry:

```json
"schedule": [
  { "hours": ["06:00", "08:00"], "package": "play_animation", "parameters": { "animation": "pray" } }
]
```

`pray` matches `pray`, `pray_1`, `pray_2`, etc.; `pray_1` matches only that exact sheet.
Names are case-insensitive. Each complete cycle uses the set's FPS and the selected sheet's frame count.
With multiple variants, the next cycle picks a different one at random. No pose changes mid-cycle.
Only standard sheets and custom sheets referenced by valid package schedules are loaded.
Changing a package's animation then running `anpc_reload_packages` also refreshes affected sprite sets.
New frames/sheets require `anpc_reload_assets`; use `anpc_reload_schedules` for NPC schedule changes.

Greetings, conversations, healing, combat and fleeing interrupt playback; resuming restarts at the first
frame. Ending the slot releases the animation immediately. These are visual loops: selecting an attack
or cast sheet never causes damage, projectiles or spell effects. Missing usable sheets (or a vanilla-only
NPC) fall back to the current AI, with a diagnostic; a still-loading custom set is allowed to finish.
Stationary sprite sets can use this package too.

### Personal NPC variables and conditional routines

Declare optional boolean/integer defaults in `npc.json`:

```json
"variables": { "following": false, "jobs_completed": 0 },
"schedule": [
  { "package": "follow", "when": { "variables": { "following": true } } },
  { "package": "stand" }
]
```

Each spawned instance has independent values, even when several people use the same folder. These
are separate from player-wide dialogue flags and personal opinion. Persistent instances save explicit
variable overrides under their instance key; random-each-visit people follow the existing rule of
forgetting their personal state between visits. Old saves without variables read the declared defaults.
Unchanged defaults need no saved entry; explicitly setting false or zero is still saved. Changed saved
values take priority over definition defaults on subsequent visits. Removed declarations retain their
saved data, but unavailable names never satisfy a condition. Changing a declaration's type logs a warning
and prevents its conditions matching until the saved value is corrected through the API or console.

Declare at most 64 variables. Names are case-sensitive, lowercase letters/digits/underscores, letter
first, at most 64 characters. Values are booleans or signed 32-bit integers (-2147483648 to 2147483647).
Unknown names, type changes and arithmetic overflow are rejected without changing the stored value.

Use the same `when.variables` syntax in dialogue topics, answers, replies and schedule entries.
All clauses must match. A literal value means equality; comparison objects support `eq`, `ne`, `gt`,
`gte`, `lt`, `lte`. Booleans support `eq` and `ne`; ordered comparisons require integers. For example:

```json
"when": { "variables": { "following": false, "jobs_completed": { "gte": 3, "lt": 10 } } }
```

Schedule `when` and dialogue share the supported condition syntax, including nested `any`. Unknown variables, incompatible types and invalid conditions never unlock an
activity. Invalid schedules warn and use current AI, as before. Matching conditional entries have
priority in list order, followed by the normal timed entry and unconditional fallback. Setting a value
refreshes the eligible activity on the next brain update; changing an unrelated value keeps the current
package's progress. NPCs without variables or conditions retain their existing behaviour.

Dialogue actions belong to the NPC being addressed and run when the topic/response is selected:

```json
{
  "caption": "Follow me",
  "when": { "variables": { "following": false } },
  "answers": [{
    "text": "Of course. Lead the way.",
    "setVariables": { "following": true },
    "addVariables": { "jobs_completed": 1 }
  }]
}
```

`setVariables` accepts declared boolean/integer values; `addVariables` accepts signed integer amounts.
For each part of the selected line (topic/reply first, answer second), sets run before adds. Merely showing
or opening a topic does not change its variables. Composed dialogue checks names and types against each
NPC's declarations, including nested replies. Greetings support conditions, not actions.

- `anpc_var <id or key>`: list that person's values.
- `anpc_var <id or key> <name>`: read one value.
- `anpc_var <id or key> <name> <true|false|integer>`: set a value.
- `anpc_add_var <id or key> <name> <amount>`: add/subtract an integer.
- `anpc_info`: includes personal variables and the selected package.
- C# API on `NpcBrain`: `SetVariable(name, bool/int)`, `AddVariable(name, amount)`,
  `TryGetVariable(name, out NpcVariableValue)`, `TrySetVariable(..., out error)` and
  `TryAddVariable(..., out error)`. Public reads return a copy; writes validate the declaration.

### Following

`follow` defaults to the player, stopping about 2 metres away and running when at least 6 metres away.
The NPC's normal gait speeds, `combat.canRun` and `combat.runSpeed` apply. A half-metre resume margin
prevents constant shuffling around the stopping distance; running has a one-metre stop margin to
avoid gait flicker around `runDistance`. Configure defaults in `_Packages/follow.json`
or override `target`, `distance` and `runDistance` on a schedule entry. `runDistance` must be at least
`distance + 0.5`. A named target is its exact spawned id/key from `anpc_list`, including generic keys,
and must be a living, active, different ANPC under the same area parent.

The `follow` package stays in the currently loaded area and now uses local obstacle steering, recent
route samples and ordinary unlocked door opening (see below). It does not transfer between areas or
teleport by itself; hired mercenaries add those capabilities through the companion system. Missing/out-of-area targets
cause the follower to stand and retry; a returned target is reacquired. After 10 active seconds without
movement, it rests for 2 active seconds before retrying. Pausing, talk, greetings, healing and combat
interrupt routine movement. When calm it resumes whichever package is eligible. Following does not
change bravery. Stationary sprite sets cannot follow and fall back to current AI with a diagnostic.

For an opt-in example, restart DFU and run `anpc_spawn follower_tester`. Talk to the Follower Tester
and choose **Follow me** or **Wait here**. Recruitment sets that individual's `following` boolean and
increments their `times_recruited` integer; dismissal selects `stand` at the current position. It has no
automatic spawns and uses vanilla Warrior sprites. Each additional spawned tester has its own values.

### Persistent mercenary companions

Add `"recruitCost": 100` to an NPC definition to offer built-in hiring dialogue. Omitting the field
keeps normal ANPC behaviour. The price is a whole number from 0 to 1,000,000 gold.

Hiring charges the addressed person's fee once, promotes even a temporary/random-each-visit person
to persistent state, and selects player following. **Wait here** leaves the paid companion at their
saved position; **Follow me** resumes for free. **You're dismissed** ends the contract and leaves them
there. Hiring a dismissed person starts a new paid contract. Without enough gold, **Your hiring fee**
explains the price. Commands are unavailable while dead, hostile, fighting or fleeing.

A hired person retains their instance key, generated name, race/gender, portrait selection, sprite seed,
maximum health, health fraction, opinion, dialogue history, personal variables and configured magic state.
They transfer through building/dungeon entry and exit and world rebuilds such as fast travel. A save/load
restores one copy, including indoors; their original population/placed spawn is reserved so revisiting
home cannot duplicate them. Death remains permanent. Format 3 reads older saves with no recruited people.
Missing NPC definitions leave companion records in the save and log a diagnostic.

Following companions appear on checked floor near the player when entering a different area. Ground,
wall and person-clearance checks retry if arrival is crowded. Waiting/dismissed people return near their
saved spot when that area is loaded; outdoor positions use terrain-local coordinates to survive floating
origin shifts. Recruitment overrides routine packages with follow/stand, while combat and interaction
interruptions retain their existing priority. Area changes do not refill a companion's magic pool.
The navigation extension below adds local obstacle/door handling and restricted catch-up. Full
pathfinding remains separate work. Defensive combat, belongings and manual equipment are described below.

`ironclad_rogue_1` is the mercenary example (100 gold). Her custom sprite uses `spriteScale: 0.94`,
6% smaller in both dimensions than the vanilla Rogue base-class height. Existing spawn rules are retained.
Restart DFU, find her in a town/tavern or run `anpc_spawn ironclad_rogue_1`, then talk to hire her.
`anpc_info` includes companion status and area. `anpc_selftest companions` runs the focused live suite
with the installed rogue pack (from outdoors in a town); it uses the current game's gold temporarily and
creates the separate `ANPC follower verification 20261015` test save for the real save/load check.

### Following around obstacles and through doors

Following now probes for walkable floor, steps and clearance, steers around nearby obstacles and people,
and can use up to 128 recent target positions to follow a route around a corner. Route decisions are
bounded, with short cached steps between route probes. This is local steering with a recent route, not a
full dungeon or city pathfinder. A missing/out-of-area target still waits safely.

A follower whose class can open doors opens an ordinary unlocked hinged door within walking range.
It never toggles an open door shut, picks locks or bashes. Locked/magically held doors, special doors
and doors carrying linked actions are left for the player. A short door posture lowers the collision
capsule to fit common low lintels, keeps the feet anchored, and restores height when there is clearance.

Only a hired companion actively following the player can use teleport catch-up. After two active seconds
at least `CatchUpDistance` behind (mod setting, Companions section, 8-50 m, default 15 m), or five seconds
farther than 6 m without closing in by at least 1 m (sliding along a wall or circling a corner counts as
stuck), it reappears on checked floor near the player: behind the player's view first, else beside or
around the player. The destination must be out of sight; the companion itself may be in view when it
leaves. The same living entity is moved, preserving health and contract; no respawn or healing occurs.
Only a closed locked or magically held door on the straight line between companion and player blocks
recovery. Waiting, dismissed, fighting/fleeing or deliberately held companions do not catch up.
Ordinary follow packages can steer/open ordinary doors but do not teleport.

Catch-up uses the existing floor, wall and person-clearance checks and retries each second when there is
no safe hidden spot. Trail/recovery clocks are temporary: menus and pauses freeze them, target/area
changes and interruptions (combat, talk, holds) reset them, and they add no save-format fields. `anpc_info` reports clear route, recent route, detouring, opening a door or
waiting at a protected door. `anpc_selftest navigation` exercises the rogue on temporary collision fixtures.

### Companion combat and orders

A friendly recruited companion (`following` or `waiting`) defaults to Defensive stance. Following
companions assist against visible hostile enemies currently threatening the player or another hired
companion. Their own immediate attacker has first priority, then the player's attacker, then another
companion's attacker. A current enemy is retained over equally important alternatives. Neutral ANPCs
angry only because the player assaulted them are excluded, as are the player, friendly allies, city
watch and protected quest actors. A hostile quest foe actively attacking the party can be defended
against even when DFU's default `IsAttackableByAI` flag is false (for example, Daggerfall's night ghosts).
Unrelated, hidden or restrained quest foes remain protected, and the mod never changes the shared
quest AI flag. Defensive stance does not initiate fights with unrelated hostiles.

Following combat stays within 30 m of the player. A waiting companion only defends herself within
8 m of her waiting point, and walks back there afterward. Her saved waiting position stays anchored
during combat. A dead, inactive, pacified or out-of-area enemy is released; six active seconds without
sight or ten seconds without approach progress also release the enemy, with an eight-second retry
delay. Normal following resumes after combat. Combat/retreat never invokes teleport catch-up.

Below 25% health a companion retreats towards the player or waiting point, and stays cautious until
40%; configured bravery can also cause retreat earlier. Health, damage, equipment and actual melee
attacks still use DFU's normal rules. Recruitment and area transfers do not heal the person.

An isolated damaging player hit gives a warning and -5 opinion, preserving the contract and the
damage taken. Three damaging player hits with no gap longer than ten active seconds end the contract
without a refund and invoke ordinary ANPC assault/hostility behaviour. Hit counters, combat targets,
pursuit clocks and retry delays are temporary; save loads reconstruct safe targeting. All behaviour is implemented inside the mod.

`anpc_info` includes defensive combat, retreat and return status. `anpc_selftest companioncombat`
tests these decisions with the real Ironclad Rogue and temporary opponents. The mod controls the
hired actor's perception while reusing DFU's attack and movement components; its compatibility adapter
caches four read-only EnemySenses fields. Melee approach uses the motor's public action callback and
the same floor/obstacle checks as following, including the player as an obstacle when approaching an
enemy. The original motor callback and automatic senses are restored when the contract ends.
The rogue retains her 100-gold recruitment fee and reduced sprite scale.

Interact with a hired companion within normal activation distance to open the native **Orders** picker,
including during combat. Keyboard selection, mouse and Escape use DFU's normal picker controls.
The header shows stance and health condition; the movement row shows Following or Waiting.
Choose **Talk** for normal conversation while calm, or use the **Companion orders** dialogue topic
to return to this menu. Info activation shows her stance/condition; Steal keeps its normal behaviour.

- **Defensive** protects the party using the rules above.
- **Aggressive** also engages visible eligible hostiles within the same pursuit limits. Civilians,
  guards, allies and uninvolved protected quest actors remain excluded. Waiting still permits self-defence only.
- **Passive** suppresses physical and offensive spell attacks and retreats towards the player or
  waiting point when a party threat is seen. It does not initiate fights.
- **Disengage and regroup** cancels pursuit and pending attacks, walks back to the player, and
  suppresses reacquisition for at least eight active seconds and until nearby. A blocked return
  expires after twenty active seconds. No combat catch-up teleport is used. The saved stance stays
  unchanged. A waiting companion temporarily comes to the player, then returns to her original point.
- Walking into a calm companion continuously for about **0.75 seconds** also triggers Push. Simply
  standing nearby does not. Automatic pushes have a six-second cooldown; combat and paused UI stop
  contact detection. Both following and waiting companions can make room.
- **Push** (when calm) makes the companion walk a few steps away from you, then pause briefly to let
  you pass. They avoid walls and unsafe drops. Followers then resume following; waiting companions
  stay at their new spot. Combat and new movement orders interrupt the push.
- **Follow / Wait** work during combat and deliberately replace the movement order. Dismiss and Talk
  are offered when calm. Changing stance preserves the waiting point, health, gold and hit warnings.

Each person's stance is saved independently (save format 4). Older/missing/invalid stances default to
Defensive. Targets, animation signals and regroup timers are transient and reset on load/area transfer.
Orders recheck the exact living friendly contract at selection time, so stale menus cannot change a
replacement actor after loading. No engine source or engine binaries are changed.
`anpc_selftest orders` checks stances, regroup, native UI, old-save defaults and restoration in game.
Fresh recruitment immediately creates the follow routine even for NPCs without a schedule; no area
change is required. Choosing Talk waits for the picker selection input to finish before opening the
vanilla topic controls, preventing that press from selecting a topic. `anpc_selftest companionbugs`
covers fresh hiring inside an inn, repeated Return-driven Talk transitions and quest ghost defence.
Bow tactics and manual companion equipment are described below. Companion spell tactics remain later work.

### Party slot exemptions and performance

An optional top-level boolean in `npc.json` exempts an NPC from the party limit:

```json
{
  "kind": "generic",
  "recruitCost": 0,
  "no_companion_slot": true
}
```

`no_companion_slot` defaults to `false` when omitted. It only changes slot accounting; recruitment
still requires `recruitCost`, and any authored fee is still charged. This is useful for escort NPCs.
The installed definition controls the flag after restarting; its last known value is retained in the
saved contract if the definition is temporarily missing. Old records default to ordinary slots.
Exempt companions still perform normal movement, combat and saving work.

Inventory snapshots are reused by in-memory callers until a known change invalidates them. Firing
an arrow marks the inventory dirty rather than serializing the entire pack. Actual saves and actor
teardown force fresh native item records, so external item changes and native durability/enchantment
updates are included even without a notification. Save format 5 is unchanged. Each snapshot replaces
the previous one; repeated fights or area transfers do not append inventory history.

The roster reuses its quarter-second membership buffer. Perception shares a line-of-sight result only
within the same rendered frame for the same actor positions; actual arrow launch checks always query
physics again. Ammo selection and ordinary projectile collision checks avoid temporary result lists;
saturated projectile queries fall back to a complete hit list to preserve collision safety.
`anpc_selftest limits` checks recruitment, settings, dialogue and save compatibility.
`anpc_selftest performance` records controlled party frame timings and native inventory/save sizes,
and checks snapshot reuse, external item updates and fresh launch safety. Measurements are specific
to the test machine and scene; they are not an FPS guarantee for crowded or heavily modded games.

### Companion bow combat

Give a short or long bow and arrows through Belongings, then select the bow and Equip. If she is
already holding a melee weapon and shield, these exact items become her close-combat fallback.
Equip the intended melee kit before selecting the bow; manually selecting a different melee weapon
or unequipping the bow ends bow preference. Her class and material restrictions still apply.

With a clear shot she holds position at 6–25 metres, starting with her bow at 8 metres or farther.
Below 6 metres, with no usable arrows, or with a broken bow she uses the remembered melee kit,
or fists if that kit is gone. The separate switching distances prevent repeated swapping at the
same boundary. She closes distance when out of shooting range or when a shot is blocked; normal
navigation, pursuit limits, stances, retreat and regroup still apply. Shots are at least two active
seconds apart. She does not kite backwards or pick a different weapon from her pack.

Each released arrow consumes one actual item from her pack. A hit puts that arrow on the original
enemy, preserving its native/custom item metadata; misses and blocked impacts spend it. Bow damage,
hit rolls, material immunity, enchantment strikes and retaliation use DFU's native rules and formula
overrides. Each arrow retains its launch weapon and target, so changing to melee while it flies
cannot turn it into a sword hit. A bystander or ally blocks a shot and never receives its damage.
Shots are cancelled when their contract, stance, target or loaded area is no longer valid.

The selected bow and fallback item UIDs persist with belongings through saves and area transfers;
older format-5 inventories need no conversion. Player bow-hand settings are left intact. The authored
sprite uses `attack_range` or `attack_ranged` (including numbered variants) when attacking with an
equipped bow, then its ordinary attack sheet fallback if none exists. This selection also applies to
ordinary ANPCs with a bow during their primary attack animation, while native damage signals are retained.
Spells keep their casting sheets; idle/walk animations are unaffected. This does
not generate new bow artwork. No engine edits, global AI hooks or inventory replacements are used.
`anpc_selftest bows` exercises live shots, finite ammunition, hand settings, range switching,
save restoration, blocked shots, passive/regroup orders and missing/broken gear.

### Companion belongings and trading

Choose **Give / Take belongings** in the calm companion Orders menu. Select an item in either pane,
then use Give or Take (double-click / Return also works). Stacks and wallet gold ask for a quantity;
Escape cancels without moving anything. Each pane shows current load / maximum in kilograms.
Select an item to see its full name, condition and the quantity that currently fits.

Capacity uses the companion's native live Strength and DFU's MaxEncumbrance getter: normally
floor(Strength * 1.5) kg, with native increased allowance effects and formula overrides respected.
All carried items, including worn gear, count using native effective weights. The player's wallet gold
also counts when receiving items. Each confirmation checks the exact living contract, distance, calm
state, item ownership, quantity and recipient capacity again. Losing Strength never deletes belongings;
an overloaded owner can give items back and receive weightless items, but cannot add positive weight.

Worn equipment must be unequipped before transferring it. Unequip the player's gear in
the usual inventory before giving it. Giving a new weapon or armor stores it in the pack; select it on
the companion's side and use Equip to wear it. Quest-bound items, summoned items and transportation cannot be traded. Coin transfers
move real gold between the player's wallet and the companion's native inventory without charging a fee.

Use **Equip / Unequip** below the companion's list to change selected gear while calm and nearby.
The button also uses DFU's configured Inventory Equip shortcut.
Weapons replace the primary hand; a two-handed weapon returns both held items to the pack, and a
shield replaces an incompatible two-handed weapon. Displaced gear retains its identity and condition.
Native equipment slots support armor, shields, compatible clothing, jewelry and gems. Broken items,
quest-bound/summoned items, class-forbidden equipment and incompatible clothing are rejected with
an explanation. Clothing fit uses the saved ANPC identity rather than the generic enemy gender field.
Bows can be selected manually; arrows are ammunition in the pack and cannot be equipped as a melee
weapon. Giving gear still does not select it automatically.

The native damage formula uses the selected weapon and its material (for example, silver versus
a ghost). The first manual equipment change switches this companion's armor to protection calculated
from the gear actually worn, including boots and shield coverage, without DFU's hidden class-enemy
armor bonus. Creature companions retain their native natural-armor baseline. Existing companions
keep their previous armor calculation until manually changing gear. Native held/enchantment callbacks
run on the companion's effect manager and are resumed once on save/area restoration. On death, all
slots (including jewelry) are unequipped before the native corpse receives the items. Equipment
changes never move an item through the player's inventory, including when splitting a custom stack.

The authored ANPC sprite keeps its drawn outfit; this feature changes actual equipment and combat
values, without generating new armor sprites. The optional managedEquipment flag fits save format 5;
existing inventory snapshots load without conversion or rehire. `anpc_selftest equipment` checks hand
conflicts, native armor, restrictions, native/custom stacks, failed-change rollback, held effects, save/load,
UI input, weapon material damage, contract validation and corpse ownership.

Save format 5 stores native item records, equipment UIDs, armor values and permanent base stats in
each companion contract. Old contracts initialize their existing native kit once. Belongings survive
save/load, area teardown, waiting, dismissal and rehire; a death transfers the live items to DFU's native
corpse inventory. Native item identity, condition, enchantments and registered custom item classes are
retained. Split stacks use full native records and remain distinct to avoid merging different metadata.
Missing custom item classes or damaged inventory snapshots preserve the saved data and disable
trading for that person, with a diagnostic in the log.

This UI is specific to ANPC companions and does not replace DFU's inventory window or override global
formulas. Compatibility with mods using native item registration/serialization is supported through those
APIs; mods with their own follower/UI/save systems may still require integration. `anpc_selftest trading`
checks native/custom item transfers, weight limits, quantity confirmation/cancellation, gold, restrictions,
save restoration, stale actors and corpse ownership. `anpc_selftest companions` additionally checks an
actual disk save/load, building and dungeon transitions and world rebuilds with persistent belongings.

### Guarding a spot

`guard_spot` walks to its post, holds it, and returns there after combat or displacement. Without
`position`, it captures the NPC's current position once when the schedule occurrence starts.
A reload, time jump, area entry or new occurrence starts fresh. Its effective bravery is `Brave`,
including during combat; ending the slot immediately restores the configured bravery. The shared
NPC definition is never changed. Greetings, talk and healing still interrupt the routine.

```json
"schedule": [
  { "hours": ["20:00", "06:00"], "package": "guard_spot",
    "parameters": { "position": [12.5, 1, -3.25], "idleInterval": [6, 15] } }
]
```

Both parameters are optional. `position` uses absolute coordinates relative to the current area's
origin (each component -1000000 to 1000000); outdoors, copy the position line from `anpc_pos`.
Direct coordinates have no city/location field. Use a named area spot to scope a post to a town or building. A generic NPC uses direct coordinates in every area where it spawns,
so use the existing `spawn.places` setting when a fixed outdoor post belongs to one particular town.
Movement is direct and collision-aware; unreachable posts fall back to current AI. Stationary sprite
sets can guard their current position but cannot walk to a distant post.

At the post, the sprite holds frame zero of `idle_1`, otherwise `idle`, otherwise the lowest numbered
idle sheet. After a random interval it plays any idle sheet once using that sheet's frame count and
the set's FPS, then returns to the frozen pose and starts another interval. `idleInterval` is an ordered
`[min,max]` pair, each 0.1-3600 seconds of active gameplay. Pausing and interruptions do not advance
this timer; resuming an interrupted pose starts a fresh interval. Combat, healing, greetings and talk
take animation priority. NPCs using vanilla sprites retain their normal vanilla rendering.

### Working at a post

`work_at` walks to an optional `position: [x,y,z]` in the current area's axes, or reserves a named `spot`.
If neither is given, it captures the current position when the activity starts. At the post it loops the
configured animation family/exact sheet and optionally faces a direction. `facing: 0` points along the
area's +Z axis and `90` along +X. A package's facing overrides the spot's facing. Movement and animations
use the existing collision, gait and sprite systems. `run` respects `combat.canRun` and `combat.runSpeed`.

```json
{
  "type": "work_at",
  "parameters": { "spot": "forge", "animation": "hammer", "facing": 90 }
}
```

Animations must exist in that NPC's sprite set. Family variants switch after full cycles, as with
`play_animation`; they are visual and do not cause attack/spell effects. Missing usable sheets fall back
to normal AI and release reservations. Lazy-loaded sheets may finish loading. Greetings, conversation,
healing and combat can interrupt work. Once calm, the NPC returns to its post and resumes. Stationary
sprite sets may work at their current position but cannot walk to a distant workstation.

### Reusable named spots and occupancy

Put each named spot in `ANPCs/_Spots/<id>.json`. Two coordinate modes are supported:

```json
{
  "relativeTo": "home",
  "position": [1, 0, 0],
  "tags": ["prayer"],
  "facing": 180,
  "capacity": 1
}
```

Home-relative coordinates are offsets from each instance's original home, so generic NPCs can use
portable routines in different towns/interiors. Each translated post is a separate resource in that area.
An optional location restricts where a home-relative post is available.

```json
{
  "relativeTo": "area",
  "location": { "region": "Daggerfall", "place": "Daggerfall", "context": "outdoors" },
  "position": [12.5, 1, -3.25],
  "tags": ["forge"],
  "capacity": 1
}
```

Area-specific spots require region/place, with context `outdoors` (default), `dungeon`, or
`b<buildingKey>` for one particular building. `anpc_spot_pos` prints a ready-to-save definition at your
position in a loaded town, building or dungeon. Save it under `_Spots`, add tags/facing as needed, then
run `anpc_reload_spots`. Use `Schemas~/spot.schema.json` for autocomplete. An absent `_Spots` folder is
normal and affects only packages that request a named spot.

`work_at`, `guard_spot` and `travel_to` accept `spot` as a filename id, tag, or list of 1-16 ids/tags.
It is mutually exclusive with a coordinate `position`/`target`. Matching is local to the current area;
no NPC travels to another town or enters a building to use a spot. Matching free spots are tried nearest
first. Default capacity is one NPC; configurable capacity is 1-16. An NPC reserves its post while
approaching/working. It tries another matching free post if the nearest is occupied. When none is free,
it uses its normal wander/leash AI and retries once per active second. Sequence timing pauses during
that fallback. Unreachable named posts are skipped temporarily and retried after a bounded interval.

Reservations are transient and are released on step/slot changes, completion, combat, death, despawn,
disabling the brain or spot reload. Brief greetings/conversations retain the reservation while the worker
is still using that post. Returning from combat reacquires a free post, which may be a different match.
The bundled `home_prayer` demonstrates portable offsets; `example_daggerfall_post` demonstrates
a fixed outdoor position in Daggerfall, selected explicitly by id or `example_workstation` tag.

Reload/area entry/save load reconstruct reservations; they are never written into save data. Diagnose
which post was selected, or why current AI is being used, with `anpc_info`.

### Sequences of packages

A sequence references reusable leaf package files with optional per-step parameters:

```json
{
  "type": "sequence",
  "parameters": {
    "loop": true,
    "steps": [
      { "package": "return_home" },
      { "package": "work_at", "parameters": { "spot": "prayer", "animation": "pray" }, "duration": 15 },
      { "package": "stand", "duration": 5 }
    ]
  }
}
```

The filename `return_home` is a `travel_to` package, so it completes automatically on arrival when no
`duration` is given. Every other leaf type requires a duration (0.1-86400 active gameplay seconds).
For work/guard/travel steps, a duration starts at the destination; for wandering/patrol/follow it includes
active movement. Interruptions, loading and unavailable named spots do not consume step time.
A sequence resumes its current step after interruptions. `loop` restarts from the first step; a non-looping
sequence completes and holds the current position until the schedule changes. Each NPC owns its own
step index, elapsed time and movement state. Save load, area entry, time jumps or package/spot reload
restart the current sequence. No missed steps are replayed.

At most 32 steps. This version supports references to leaf packages; nested sequences/cycles are
rejected with a diagnostic. Missing/invalid referenced packages release to current AI. Sequence
animations are included in sprite metadata indexing and refresh when packages reload.

### Indoor and outdoor schedules

Schedules already accept conditional `when` clauses. `indoors` and `outdoors` take JSON booleans:

```json
"schedule": [
  { "package": "wander_civilian", "when": { "outdoors": true } },
  { "package": "local_wander", "when": { "indoors": true }, "parameters": { "radius": 2 } }
]
```

Put these entries in the NPC's `npc.json`; the package files in `_Packages` stay reusable.
Buildings (taverns, houses, shops, guilds, temples, etc.) and dungeon interiors count as indoors.
Town/city exteriors and wilderness count as outdoors. The check uses the NPC's current area,
so an outdoor NPC does not become indoor just because the player enters a building.
`"outdoors": false` means indoors, and `"indoors": false` means outdoors. All clauses must match;
using both keys with `true` never matches. These conditions also work in dialogue `when` objects.
Without an NPC context (e.g. spawn filters), the current player area is used.

Omit the indoor entry to use the NPC's ordinary AI indoors, or replace it with another package
such as `stand`. Conditions may be combined with hours or weather; first matching conditional
entry wins. With no matching entry, the NPC uses its ordinary AI. Use `anpc_reload_schedules`
after editing schedules in `npc.json`.

### Broader schedule conditions

Schedule `when` now uses the shared condition syntax, including weather, season, region/town, time,
player level/race/gender, gold/items/guild membership, global flags, asked topics, quest globals,
reaction and nested `any`, alongside personal `variables`. `tone` is dialogue-answer-only.
Schedules use live regional reaction plus personal opinion rather than a cached conversation reaction.

New personal conditions, also available in dialogue, are `minOpinion`/`maxOpinion` (-100 to 100)
and `minHealth`/`maxHealth` (0-100 health percentage). Bounds are inclusive and require an individual
NPC context. These can be combined with existing clauses:

```json
"when": {
  "weather": "rain",
  "minOpinion": 20,
  "maxHealth": 60,
  "variables": { "hired": true }
}
```

Every clause must hold; `any` is an OR of condition objects. Conditions are reevaluated on brain updates,
and the first eligible conditional schedule entry wins. Optional outer `hours` still use exact HH:mm;
`when.hours` follows the existing whole-hour condition format. Invalid conditions warn and cannot
activate a package. NPCs with old schedules retain their existing selection rules.

### Eating and drinking

The `eat` package (supplied as `_Packages/eat.json`) holds the NPC where it is and plays its `eat*` and `drink*`
sheets: a bout of `mealCycles` full cycles, each cycle picking eat or drink at random (only one family if the set has
only one), then a break of `breakCycles` idle cycles, repeated with new random counts. Variants of a family avoid
immediate repeats. A set without eat or drink sheets plays only idle; without idle sheets the bouts follow each other;
`breakCycles: [0, 0]` also removes the breaks. NPCs with vanilla sprites simply stand.

```json
"schedule": [{ "hours": ["12:00", "13:00"], "package": "eat" }]
```

Each eat or drink cycle starts an `eat` / `drink` sound (no chance roll): the ANPC's own `Sounds/eat_*` / `drink_*`
files, else the shared `_Sounds` ones. A new one is skipped while the previous bite or gulp still plays, so they never
overlap; keep these clips shorter than one cycle. They follow the pitch settings and are muted only by
**ANPC sounds: Off**. Greetings, talk, combat and fleeing interrupt the routine; calm again, it starts a fresh bout.
`eat*` and `drink*` sheets load only for NPCs whose schedule uses the package.

### Chores, hawking and socializing

**`chore`** (`_Packages/chore.json`): walks to a random point within `radius` of `center` (home), plays 2–4 (`cycles`)
full cycles of a chore sheet, then picks the next point. Chore sheets are any of `sweep*`, `chop*`, `scrub*` and
`work*` the set has (variants picked at random), or the families/sheets listed in `animation`
(`"animation": "work"`, `["sweep", "dust_2"]`). A set with none of them idles at each stop; vanilla sprites stand
2–6 seconds. `radius: 0` works on the spot. `man_with_a_broom` sweeps with it by day.

**`hawk`** (`_Packages/hawk.json`): stays put. When the player is within `range` and in sight, it turns to face them,
plays a random `Sounds/hawk_*` line (no chance roll; ANPC voice mode only) and one cycle of its `hawk*` sheet (or the
`animation` named), falling back to a `greet*` sheet, then a `talk*` sheet; with none of them it only turns. Then it
waits `interval` active seconds before the next call. Without hawk sound files the call is silent.

**`socialize`** (`_Packages/socialize.json`): every `interval` active seconds it looks for the nearest ANPC within
`radius` (same town, building or dungeon) that could talk right now, walks up to it and starts a
[conversation](#conversations). Both need `"conversation"` on and `talk` sounds, and the usual 2-game-hour wait per
pair applies; a partner not reached within 15 seconds is dropped until the next try. Between conversations it stands.
`anpc_info` shows what it is doing (walking over, talking, nobody to talk to, why it cannot talk).

### Protecting an animation from social interruptions

`play_animation` accepts `only_interrupt_at_combat`, default false, in its package parameters or the
NPC's schedule parameters:

```json
"parameters": { "animation": "pray", "only_interrupt_at_combat": true }
```

False retains the normal interruption behaviour. True suppresses greetings, NPC chat, movement holds,
talk poses and calm healing casts while this animation is selected. Player dialogue may still open;
game pause freezes the current animation without switching its pose. Being attacked (including fleeing)
or a Brave NPC spotting an enemy lets combat take over immediately. Normal playback resumes when calm.
Death, paralysis and the game clock retain their normal authority. Ending/changing the schedule or
finishing a sequence step still selects the next activity. This setting does not change bravery.

### Example NPC routines

The installed examples now include these optional schedules:

| NPC | Routine |
|---|---|
| `castle_guard_1` | 06:00-20:00: patrol/watch sequence (45/25 active seconds); rain or snow selects a named home post instead. Overnight: guard the home post. |
| `wench` | 07:00-23:00, health at least 60%: work/roam sequence (20/15 active seconds). At 30% health or less: return home. Other daytime health: tavern roam. Overnight: return home. |
| `cowled_old_man_1` | 07:00-08:00, health at least 50%: protected idle animation. 18:00-20:00, opinion at least 20 and health at least 50%: work at a home study post using existing idle poses. Original AI in gaps. |
| `smith` | 18:00-23:00: `local_wander` within 3 m, with 10 game-second pauses. Otherwise `return_home` holds his original spot. |
| `follower_tester` | Manual spawn only. Dialogue enables `follow` or returns to `stand`; personal variables saved for that spawned person. |
| `temple_monk_1` | 06:00-08:00: work/stand sequence at a home-relative prayer spot when health is at least 50%. 12:00-13:00: protected prayer. 18:00-20:00: work at a prayer-tagged spot in clear weather. Original AI in gaps; manual spawn only. |

These are local routines around each instance's spawn. They do not locate a particular castle,
tavern or forge, change areas, or sleep. Prayer uses the selected sheet directly. Blocked routes fall back
to the NPC's original AI. Restart DFU after changing these NPC schedules.

## Enemies

`"attitude": "hostile"` (unique or generic) makes an enemy: it attacks you on sight with DFU's own enemy AI, never
calms down on its own, and fighting or killing it is no crime. It will not talk, and it blocks resting nearby like vanilla
enemies. Bravery still applies (a Coward enemy runs).

- `"hostileHours": [20, 6]` — an enemy only from 8 pm to 6 am (hours may wrap past midnight); calm townsfolk the
  rest of the day. When its hours end it calms down, unless you fought it — then it stays angry like provoked
  townsfolk until its `calmDownHours` pass.
- Language pacification: the first time it sees you on a visit, DFU's own roll runs (Etiquette, or Streetwise
  for Burglar/Rogue/Acrobat/Thief/Assassin/Nightblade, a creature's language such as Orcish; a sheathed weapon
  helps). On success it calms down for good ("Pacified ... using ... skill."), saved with your game, and you can
  talk to it. Hit it afterwards and it is an enemy again for good, with no crime and no second roll. Only enemies
  by `"attitude": "hostile"` roll, not ones switched on with `anpc_hostile` or a `becomeEnemy` reply.
- Creatures: an enemy may use any DFU creature as `baseClass` — Rat, Imp, Spriggan, GiantBat, GrizzlyBear,
  SabertoothTiger, Spider, Orc, Centaur, Werewolf, Nymph, Slaughterfish, OrcSergeant, Harpy, Wereboar,
  SkeletalWarrior, Giant, Zombie, Ghost, Mummy, GiantScorpion, OrcShaman, Gargoyle, Wraith, OrcWarlord,
  FrostDaedra, FireDaedra, Daedroth, Vampire, DaedraSeducer, VampireAncient, DaedraLord, Lich, AncientLich,
  Dragonling, FireAtronach, IronAtronach, FleshAtronach, IceAtronach, Dragonling_Alternate, Dreugh, Lamia. It
  gets that creature's stats, attacks and sounds (and its picture, unless the folder has sprites). Creatures are
  always hostile (no `hostileHours`).
- On the go: `anpc_hostile <id or key> on|off` turns any ANPC into an enemy or calms it (also a hostile one); the
  switch is saved with your game. Other mods can do the same with `AdvancedNpcsMod.Instance.SetHostile(key, true)`
  or the mod message `"SetHostile"` with data `"<key>|on"` / `"<key>|off"`.
- Enemies spawn where any ANPC spawns: unique ones at their place, generic ones in towns, and with `anpc_spawn`.

### Combat tuning

`"combat"` (unique or generic, optional) scales DFU's own numbers for this ANPC; `health`, `damage` and
`attackSpeed` are multipliers and 1 (or leaving them out) keeps the vanilla value:

```json
"combat": { "health": 0.7, "damage": 0.7, "attackSpeed": 1.3 }
```

- `health` (0.05–10): max health is rolled by DFU as usual, then multiplied (at least 1).
- `damage` (0.05–10): a creature's melee damage ranges, e.g. an Orc's 1–6 becomes 1–4 at 0.7. Creature
  `baseClass` only: human classes take their damage from weapons.
- `attackSpeed` (0.25–4): how often it swings. DFU waits 1.5–3 s (adjusted by your level and the reflexes
  setting) after each melee attack; 1.3 waits 1/1.3 as long.
- `rangedAtDistance` (true/false, default false): DFU gives every human class a bow and shoots it at random when
  the target is far away. ANPCs fight in melee only unless this is `true`. Mage, Healer and other casters without
  a bow are not affected. A creature `baseClass` has no bow in DFU: with `true` it throws instead, now and then,
  when its target is 6–51 m away, in sight and in front of it (DFU's own bow rules), and keeps closing in to melee.
- `rangedAnimation` (optional): the sheet a bow shot or throw plays, e.g. `"attack_range_1"`. Without it, a random
  `attack_range*` (or `attack_ranged*`) sheet plays, or an `attack*` sheet if the set has none. A name the sprite
  set lacks is noted in `Player.log` and the same fallback is used. Its `actionFrame` is when the shot leaves.
- `projectileDamage` (optional, needs `rangedAtDistance`): what a ranged hit does, `[min, max]` or one number
  (whole numbers 1–1000), instead of DFU's own bow (or creature melee) damage. DFU's to-hit roll still decides
  whether it hits; the amount is then rolled from min to max.
- Projectiles: a `projectile.png` (or several, `projectile_1.png`, `projectile_2.png`, ..., one picked at random
  per shot) in the sprite set folder is what flies, at the person's scale, instead of DFU's arrow; without one, DFU's
  arrow flies. See [Projectile images](#projectile-images).

```json
"combat": { "rangedAtDistance": true, "rangedAnimation": "attack_ranged_1", "projectileDamage": [2, 4] }
```

- `runSpeed` (1–2, default 1.2): how much faster than walking the ANPC runs. A fighting ANPC runs when it sees its
  target more than 6 m away and walks again under 4 m; a fleeing ANPC runs. DFU's walking speed for enemies is
  about the player's, so the default lets a running player escape or catch up.
- `canRun` (true/false, default true): `false` never runs; it chases and flees at walking speed with walk sheets.

`Player.log` shows the result when it spawns, e.g. `orc_weakling@1276329198#0: combat health 24->17, damage 1-6->1-4, attack speed x1.3.`


## Spellcasting

Add an optional `magic` block to any unique or generic ANPC:

```json
"magic": {
  "spells": ["Fireball", "Heal", "Shield"],
  "usesMagicka": true,
  "maxMagicka": 150,
  "recovery": "onAreaEntry",
  "castCooldown": [4, 7]
}
```

| Field | Default | Meaning |
|---|---|---|
| `spells` | required | Standard DFU spell names, case-insensitive (use the original English names). Replaces the class's inherited spells. `[]` disables spellcasting. |
| `usesMagicka` | `true` | Require and spend the full spell cost. `false` permits casting even at zero magicka; cooldown, silence and paralysis still apply. |
| `maxMagicka` | `150` | Maximum and initial magicka, whole number 0–1,000,000. |
| `castCooldown` | `[4, 7]` | Random gameplay seconds between casts; both numbers must be at least 1, in ascending order, at most 3600. Shared by all spells. |
| `recovery` | `onAreaEntry` | Fully replenish when the player returns to the NPC's town, building or dungeon. The outdoors around a town counts as the town: walking past its edge (or being chased out of the gate) is the same visit, and town casters keep casting there. A new visit starts after a building or dungeon, in another town, or once the town is far behind (unloaded) or after fast travel. Loading a save in the same visit preserves magicka and cooldown. |
| `restHours` | `8` | With `"recovery": "rest"`, game hours to recover an empty pool. Recovery starts after one game minute without detecting a combat target, includes time away, and caps at maximum. Area entry does not instantly refill this mode. Above 0, at most 720. |
| `skill` | `80` | The ANPC's proficiency in each of the six magic schools (1–100). DFU uses these skills to calculate casting costs; the player's skills do not affect them. |

Omitting `magic` keeps the existing vanilla class behaviour. Existing saves initialize a newly configured caster at full magicka.
Each instance has its own pool and cooldown. Rest recovery also applies after time passes while the player rests or travels.
Random-each-visit and wilderness people are temporary, so their replacements start fresh as usual.
Saves only keep a caster's magic while it matters: one at full magicka with no cooldown adds nothing, and an
`onAreaEntry` caster's magic is dropped once you are in another area (it will start full there anyway).

Configured ANPCs cast normal spells only while fighting, with a short random initial delay. They choose affordable, useful spells, heal
below 60% health (prioritizing healing below 40%), and avoid reapplying active buffs or effects already on their target.
Offensive spells require sight of a target; ranged spells additionally require a clear projectile path within 25 metres.
Touch spells require melee distance. With no affordable/useful spells they continue their normal physical combat.
Bravery still takes precedence for ordinary ANPCs; hired companions may use support magic while retreating (see below).

Effects, projectiles, resistance, reflection and spell costs come from DFU's spell system. Supported effects include damage
to health/fatigue/magicka, continuous damage, attribute drains, paralysis and silence; self spells include healing,
regeneration, shield, spell resistance/reflection/absorption, elemental resistance, invisibility and shadow. Player utility spells such as Recall,
levitation and door manipulation are not supported. Unknown names or unsupported spells are skipped with a warning naming
the `npc.json` and spell in `Player.log`; other valid spells still work. This list uses standard spells, not custom spells
from the current player's spellbook. Casting releases the spell immediately through DFU and plays its spell animation; custom sprites use a `cast*`
sheet when available, falling back to an `attack*` sheet; with neither, the spell is released without an animation.
Melee action frames do not apply to spells. Only the Mage, Spellsword, Battlemage, Healer and Nightblade classes have a
vanilla spell animation; other classes without a custom sprite still cast (with the cast sound and sparkles) but show no
casting pose.

### Companion spellcasting

Hired, living, friendly companions use the existing `NpcMagic` controller for healing, protection and offence.
Configured `magic.spells` remains their private book; `[]` still disables casting. Without a `magic` block, a hired
class caster uses the supported combat spells in its inherited native book, with its native magic skills and maximum
magicka. Dismissal returns that complete native book to normal class behaviour. Recruiting a martial ANPC does not
grant spells: `ironclad_rogue_1` remains the martial reference.

All hired casters recover magicka gradually as with `"recovery": "rest"`, even if their JSON says
`"recovery": "onAreaEntry"`. The definition is never edited. `restHours` controls the rate (8 game hours by default);
combat blocks recovery until one game minute after the last threat. Following and waiting both qualify, including
elapsed rest/travel time. Hiring preserves the current pool, and entry/load preserves depletion, fractional recovery,
recovery timestamps and remaining cast cooldown. A new hire cannot claim time from before its contract.
`usesMagicka: false` retains its authored unlimited-casting behaviour. The timed companion override ends on dismissal.

Priority is emergency self healing below 40%, then the most wounded eligible nearby party member, self care,
useful protection, and finally offence. A known pure `Heal-Health` spell can help self, player or another living hired
ANPC within 2.5 metres and clear sight; default health threshold is 60%. Self regeneration stays self-only. Party
membership supplies consent even if the player's town reaction is neutral. Existing `magic.healing` options still
apply: `player: "none"`, `allies: false`, `outsideCombat` and `healthThreshold` remain meaningful. When that block is
absent, hired casters use its normal defaults. Outside a contract the existing opt-in friendly-healing rules are unchanged.
There is one cooldown and one mana pool, so friendly healing cannot double-cast alongside companion healing.

During an encounter, supported caster-only protection spells can protect the caster and nearby player/party members.
The controller checks native active effects to avoid recasting an existing protection. Passive and retreating companions
can heal and protect, while regroup suppresses casting. Calm travel does not continually spend mana on protective buffs.
Support uses native effect magnitude and the actual caster; helping another person costs the touch variant of the spell.

Offence follows the current stance, legal target, area and pursuit leash. Casters hold a clear distance of 3–25 metres
when they have useful affordable ranged spells, and use ordinary weapon combat when empty, silenced or touch-only.
A usable equipped bow takes priority for ranged offence; healing and protection remain available. Empty ammunition
or a broken bow lets an otherwise capable caster use its spells.
DFU supplies actual missile visuals, collision, saving throws, resistance, reflection and effects. An ANPC-only missile
guard rechecks permission at impact: single spells affect only the selected foe; area spells may also affect other threats
allowed by the current stance. Player, hired party, civilians, guards and protected quest actors never receive that payload.
Bystanders can block shots, and becoming passive, regrouping, dismissal or loss of the legal target cancels in-flight harm.
No DFU engine files or player spellbooks are changed.

Run `anpc_selftest companionspells` on a disposable outdoor test save for live casting checks, and `anpc_selftest healing`
for the original friendly-healing regression checks. These fixtures create their own caster; no changes to Ironclad's
authored class, artwork, price or spell list are required.

### Healing allies and a friendly player

For ordinary ANPCs, add `healing` inside `magic` to opt in (omitting it keeps self healing only; hired party members use the defaults above):

```json
"magic": {
  "spells": ["Heal", "Shield"],
  "healing": {
    "allies": true,
    "player": "friendly",
    "outsideCombat": true,
    "healthThreshold": 0.6
  }
},
"sounds": { "combatChance": 40 }
```

The `temple_monk_1` and `cowled_old_man_1` examples enable support healing during and outside combat.
`dunmer_spellblade` and `spellcaster` enable it while fighting only. All four already know Heal.

Writing `"healing": {}` uses the defaults above. `player` accepts `friendly` or `none`.
The player qualifies when the ANPC is not an enemy or angry at them, and its reaction (regional reaction plus
personal opinion) is `likes` or `loves`. ANPC allies share the same player allegiance and must not be fighting
each other; angry civilians do not count as friendly allies. Peaceful healers can also help non-hostile vanilla
City Watch and Player Ally entities. Unrelated creatures and strangers do not qualify.

The healer selects the eligible living recipient with the lowest health fraction below `healthThreshold`
(above 0, at most 1), breaking ties by distance. They must be within 2.5 metres and in sight, in the same
building/dungeon/outdoors. The healer does not walk to reach them. With `outsideCombat: false`, it helps
others only while fighting. Fleeing healers never cast. Emergency self healing below 40% still takes priority
while fighting. Support healing shares the existing cooldown, magicka pool, silence and paralysis restrictions.

Only configured pure `Heal-Health` spells can heal others. The self spell is preserved; a separate touch
variant uses DFU's touch casting cost and its effect system, including the actual healer's caster level.
Willing recipients do not resist or reflect this beneficial healing. The healer keeps its combat target
throughout; healing does not provoke an attack.

Health healing (self or others, and self regeneration) uses a random `heal`, `heal_1`, `heal_2`, etc. sheet,
declared in `sprites.json` like other animations. Without a heal sheet it uses `cast*`, then `attack*`, then
no animation. Falling back to an attack sheet does not apply a physical hit or use its action frame.
Vanilla sprites use the class's casting pose when available.

Optional `Sounds/heal.wav`, `heal_1.ogg`, `heal_2.wav`, etc. form a random heal voice pool.
They use **`sounds.combatChance`**, even outside combat: 40 means a 40% chance per healing cast.
Without loaded heal clips, no custom voice plays; it never falls back to cast or attack clips.
The usual voice setting, volume and pitch controls still apply. Ordinary spell effect sounds remain DFU's.

`anpc_spells` lists all supported standard spell names (also written to `Player.log`); `anpc_spells fire` filters them.
`anpc_list` shows each configured caster's magicka or unlimited mode. `Examples/ANPCs/spellcaster/npc.json` is a calm,
manual-spawn example: use `anpc_spawn spellcaster`, then `anpc_hostile <key> on` to fight it.

## Spawning outside towns

A generic template can also appear in dungeons, in buildings and in the wilderness. Add a block for each place to
its `spawn`:

```json
"spawn": {
  "dungeons":   { "chance": 25, "count": [2, 4], "dungeonTypes": ["HumanStronghold", "Prison"] },
  "interiors":  { "buildings": ["Tavern", "Fighters Guild"], "chance": 60, "count": [1, 3], "when": { "time": "night" } },
  "wilderness": { "chance": 5, "max": 2, "when": { "time": "night" } }
}
```

- **`chance`** (0–100): on each visit, how likely the template is there at all. **`count`** `[min, max]` (1–10):
  how many come when it is. A group stands together.
- **`dungeonTypes`** (optional): Crypt, OrcStronghold, HumanStronghold, Prison, DesecratedTemple, Mine, NaturalCave,
  Coven, VampireHaunt, Laboratory, HarpyNest, RuinedCastle, SpiderNest, GiantStronghold, DragonsDen,
  BarbarianStronghold, VolcanicCaves, ScorpionNest, Cemetery. Without it, every dungeon.
- **`buildings`**: Alchemist, Armorer, Bank, Bookseller, ClothingStore, FurnitureStore, GemStore, GeneralStore,
  Library, PawnShop, WeaponSmith, Temple, Tavern, Palace, House (any house), GuildHall (any guild), or a guild:
  Fighters Guild, Mages Guild, Thieves Guild, Dark Brotherhood, Knightly Order.
- **Wilderness**: every 10 in-game minutes outside any town or dungeon (not while resting or travelling), the
  chance is rolled; one person appears out of sight 40–80 m away, on dry land. **`max`** (1–10) caps how many of the
  template are around you at once. They leave when far behind you, when you enter a town or building, or travel.
- **`when`** (optional): the [dialogue conditions](#conditions-when) (time, weather, season, place, player, flags,
  quest globals), except `asked`, `notAsked`, `tone` and `reaction`.
- People stand on DFU's own monster, treasure and furniture spots: never within 2 m of a door, never within 20 m of
  a dungeon's entrance. A building holds at most "Max generic per town" of them.
- With **Same people every visit**, a dungeon or building always has the same people (or none) in the same spots,
  and a killed one stays dead. With **Random each visit**, every entry rolls again. Wilderness people are never kept.
- A template with only these blocks never appears in towns. To have it in towns too, add `locationTypes`.
- Attitude, bravery, sprites, portraits and dialogue work as in towns: a hostile bandit attacks on sight, a calm
  patron can be talked to. Indoors people wander at most 3 m.
- Not sure why someone is (or isn't) there? Type `anpc_here`.

## Replacing vanilla NPCs

A generic ANPC can stand in for Daggerfall's own flat (non-moving) NPCs: shopkeepers, innkeepers, guild members,
quest givers. Name the vanilla NPC in `"replaces"`:

```json
"replaces": ["177.2", "faction:1003"],
"replacePortrait": true,
"replaceName": false,
"stayNear": 3
```

- **`replaces`**: one id or a list. A sprite id is the flat's texture archive and record (`"177.2"`; `"177 2"` and
  `"177_2"` work too); `"faction:<id>"` matches every flat of that faction (named NPCs have their own faction).
- The flat stays in the world but is hidden, and the ANPC appears on its spot with all its own sprites, AI, sounds
  and combat. Clicking it does exactly what clicking the vanilla NPC does: the same dialogue, quest offers, quest
  people, shop, bank, inn rooms and guild services (vanilla's own click is used, so other mods' services work too).
- **`replacePortrait`** (default `true`): the talk window shows the ANPC's own portrait (a PNG portrait; with a
  vanilla face the vanilla NPC's face stays). **`replaceName`** (default `false`): show the ANPC's own name instead
  of the vanilla NPC's (quest text always uses the vanilla name).
- **`stayNear`** (metres, default `0` = off): keeps a calm stand-in within this distance of its spot, so a
  shopkeeper stays by the counter; after a fight or flight it walks back. Indoors they wander at most 3 m anyway.
- When several ANPCs replace the same vanilla NPC, each vanilla person gets one of them at random, and keeps that
  one for the whole game (seeded by the flat itself, not saved).
- Stand-ins can be fought and can die. A dead stand-in stays dead, and so does the vanilla NPC it replaced: that
  shop or guild has no one at the counter any more.
- A quest that hides or shows the vanilla NPC hides or shows its stand-in. NPCs with their own scripted action (the
  Daggerfall castle gate guard, Sheogorath) are never replaced.
- Without a `spawn` block the ANPC only appears as a stand-in; with one it also spawns as usual.
- Works in buildings, dungeons (castles) and on town streets. Only generic ANPCs can replace.

## Talking and portraits

- Click a calm ANPC in Talk or Grab mode to open DFU's citizen talk window with its name and portrait; vanilla topics work as for any citizen.
- Hostile or fleeing ANPCs refuse to talk. Info mode shows "You see <name>.".

## Dialogue topics

ANPCs can have their own "Tell Me About" topics. They are listed right after "Where am I?", next to the vanilla
topics, and answered in the talk window like any other topic.

### Your first topic in two minutes

1. Create `ANPCs/_Dialogue/tavern_wench.json` (the file name is the **dialogue type**):

   ```json
   {
     "topics": [
       { "caption": "The house ale", "answers": ["Two coppers a mug. Three if you want it cold."] }
     ]
   }
   ```

2. Add one line to the ANPC's `npc.json`: `"dialogue": "tavern_wench"`.
3. Start the game and talk to her: **Tell Me About → The house ale**.
4. Later edits: change the file, type `anpc_reload_dialogue` in the console, talk to her again. No restart.

Every ANPC (unique or generic) with `"dialogue": "tavern_wench"` shares these topics. A list mixes types:
`"dialogue": ["gossip", "tavern_wench"]`. A folder's own `dialogue.json` (same format) adds topics only that
ANPC (or every person of that template) has. If two files have a topic with the same `id`, the later one wins:
types in the order listed, then the folder's own file.

### Topics

```json
{
  "caption": "The missing sailor",
  "id": "sailor",
  "when": { "asked": "Any rumours?" },
  "question": { "polite": "Could you tell me about the sailor?", "blunt": "The sailor. Talk." },
  "answers": [
    { "when": { "reaction": "dislikes" }, "text": "Why should I tell you?" },
    "Jory drank here every night, then nothing.",
    "Poor Jory. Last seen near the old warehouse."
  ],
  "sets": ["heard_sailor"],
  "once": false
}
```

| Field | Needed | Meaning |
|---|---|---|
| `caption` | yes | The text in the topic list. Keep it under 24 characters. |
| `answers` | yes | What the ANPC says: one text, or a list. One is picked at random each time. |
| `when` | no | Only list the topic while these conditions hold (see below). |
| `question` | no | What the player says. Default by tone: "Could you tell me about…?", "Tell me about….", "What do you know about…?". One text for every tone, or `{ "polite", "normal", "blunt" }`. |
| `id` | no | The name other topics use for this one. Default: the caption in lowercase with `_` for spaces and punctuation ("Any rumours?" → `any_rumours`). |
| `sets` / `clears` | no | Flags to set / clear when the topic is asked (see Flags). |
| `once` | no | `true`: the topic disappears for this person after it was asked. |

**Answers that depend on something:** an answer can be `{ "when": {…}, "text": "…" }` (it may also have its own
`sets` / `clears`). Answers whose `when` holds win over plain answers; among the winners one is picked at random.
A topic whose answers all have a `when` and none holds is not listed. `"when": { "tone": "blunt" }` in an answer
reacts to the tone the player chose.

**Follow-up topics:** `"when": { "asked": "Any rumours?" }` lists the topic once "Any rumours?" was asked of this
person, straight away in the same conversation. Name the topic by its caption or its `id`.

**Greetings:** a file may have `"greetings": [ … ]` with the same answer format (no `tone`, `sets` or `clears`).
When one qualifies it replaces the vanilla greeting; otherwise the vanilla greeting stays.

**Placeholders:** `{player}`, `{npc}`, `{town}` and `{region}` in any text; `{topic}` (the caption) in questions.

### Conditions (`when`)

Every condition in a `when` must hold. For "or", use `"any": [ { … }, { … } ]`.

| Condition | Example | Holds when |
|---|---|---|
| `hours` | `[20, 6]` | the hour is from 20:00 up to (not including) 6:00 |
| `time` | `"night"` | it is `day` or `night` |
| `indoors` | `true` | the NPC is inside a building or dungeon (`false`: outside) |
| `outdoors` | `true` | the NPC is outside, in town or wilderness (`false`: inside) |
| `season` | `["autumn", "winter"]` | it is one of these seasons (`spring`, `summer`, `autumn`/`fall`, `winter`) |
| `weather` | `"rain"` | the weather is one of `clear`, `overcast`, `rain` (storms count), `storm`, `snow` |
| `bad_weather` | `true` | rain, thunderstorms or snow; `false` requires none of these, regardless of indoors/outdoors |
| `region` / `town` | `"Daggerfall"` | the player is in one of these regions / towns |
| `minLevel` / `maxLevel` | `5` | the player's level is at least / at most this |
| `playerRace` | `"Dark Elf"` | the player is one of these races |
| `playerGender` | `"Female"` | the player is `Male` / `Female` |
| `minGold` | `500` | the player carries at least this much gold |
| `hasItem` | `"Ruby"` | the player carries an item with one of these names |
| `guild` (+ `minGuildRank`) | `"Fighters Guild"` | the player is a member (of rank 0–10 at least): `Fighters Guild`, `Mages Guild`, `Thieves Guild`, `Dark Brotherhood`, `Temple`, `Knightly Order` |
| `reaction` | `["likes", "loves"]` | how people here regard the player: `dislikes`, `neutral`, `likes`, `loves`. This is DFU's reaction, from your standing with the region's people, so it is the same for every ANPC in a region (below about -20 DFU refuses to talk at all) |
| `asked` / `notAsked` | `"Any rumours?"` | all / none of these topics were asked of this person |
| `flags` / `notFlags` | `"heard_sailor"` | all / none of these flags are set |
| `questGlobal` / `notQuestGlobal` | `"LiftedCurse"` | all / none of these DFU quest globals (names from `Quests-GlobalVars`, or numbers 0–63) are true |
| `tone` | `"blunt"` | (answers only) the player's tone is one of `polite`, `normal`, `blunt` |
| `any` | `[{ "time": "night" }, { "flags": "x" }]` | at least one of these condition groups holds |

Values like seasons, races and names may be one text or a list of "any of these".

### Flags

Flags are named switches for the whole game, saved with it. A topic sets one (`"sets": ["heard_sailor"]`), and
any topic of any ANPC can react to it (`"when": { "flags": "heard_sailor" }`). That's how one person's answer
unlocks a topic with someone else. `anpc_flag` lists them and sets or clears one for testing. Other mods can send
the messages `SetFlag` (`"name|on"` / `"name|off"`) and `HasFlag` (`"name"`, the callback gets true or false).

### Replies and actions

After an answer, the player can be given **replies** to choose from (a list over the talk window, always with
"(Say nothing.)" last). A reply adds the player's line and the ANPC's answer to the conversation, and can lead to
more replies, so small dialogue trees are easy:

```json
{
  "caption": "The house ale",
  "answers": ["Two coppers a mug. Want one?"],
  "replies": [
    { "text": "A mug, please. (2 gold)", "takeGold": 2, "reputation": 1,
      "answers": ["Here you go, love."],
      "replies": [ { "text": "Another round!", "takeGold": 2, "answers": ["Steady now."] } ] },
    { "text": "Not today.", "answers": ["Suit yourself."] }
  ]
}
```

- A reply has `text` (what the player says) and `answers` (same as a topic's: texts, or `{ "when", "text" }`).
  Optional: `when` (the conditions above; `tone` works here), `replies`, `sets` / `clears`, `id`, and actions.
- `replies` can also sit on one answer object; then that answer's replies are offered instead of the topic's.
- Replies nest up to 8 deep. `"asked": "The house ale/A mug, please. (2 gold)"` checks whether a reply was chosen.

**Actions** work on topics, answers and replies and happen when that line is said:

| Action | Example | Effect |
|---|---|---|
| `giveGold` / `takeGold` | `5` | the player gets / pays that much gold (1–100000) |
| `giveItem` / `takeItem` | `"Ruby"` | the player gets / gives up one item by its name |
| `reputation` | `-2` | how this region's people regard the player (−20 to 20); part of what `reaction` reads |
| `opinion` | `10` | this ANPC's own opinion of the player (−50 to 50); `reaction` reads the region's reaction plus this ANPC's opinion |
| `startQuest` | `"A0C00Y00"` | starts a DFU quest by its file name |
| `becomeEnemy` | `true` | the talk window closes and the ANPC becomes an enemy (like `anpc_hostile <id> on`) |
| `endConversation` | `true` | the talk window closes after the line |

A reply or topic that takes gold or an item is only offered when the player has it. An unknown `giveItem` name is
reported in `Player.log` when the game starts.

### When something doesn't show up

- Look in `Player.log` for `[AdvancedNPCs]` lines: every problem names the file, the topic and the field, and
  misspelt field names get a suggestion (`when: tme: unknown condition (did you mean "time"?)`). A broken topic is
  skipped; the rest of the file still works. A condition that cannot be read never holds, so a typo hides the
  topic (or answer) instead of showing what it was meant to lock away.
- Stand in front of the ANPC and type `anpc_topics`: it lists each topic as `shown` or with the condition that
  hides it (`hidden, when: time`, `hidden, once (already asked)`).
- `Examples/ANPCs/_Dialogue/tavern_wench.json` uses every feature: greetings, tone answers, follow-ups, flags,
  `once`, time, weather, season, gold and guild topics, replies and actions.
- Two topics or reply keys repeated in one JSON object, or a topic replaced by a later file with the same id, are
  noted in `Player.log` too.

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

- Make them with `Tools~/render-sprites.py` from a rigged, animated `.blend`: one sheet per Action, rows = directions (front, front_right, right, back_right, back, back_left, left, front_left), columns = frames, plus `sprites.json` (scale, frame sizes, feet row, fps). The feet row is the lowest point of the idle poses, so the character may stand at any height. Run with `test` first to check light and size:
  `blender -b character.blend --python render-sprites.py -- <out_dir> [test] [--rig NAME] [--mesh NAME]`
- For characters with a separate weapon mesh, `Tools~/render-character-sprites.py` includes both meshes in rendering and bounds: `--mesh body,sword`. `--keep-last death_1` keeps the final corpse pose. The Dunmer example documents a complete command.
- A character whose idle holds a weapon above its head (a guard's upright spear) would look smaller in game, because the standing height is measured from the tallest idle pixel. Add `--standing-mesh body` (the body mesh alone) and the renderer writes `"standingHeight"` (pixels above the feet row, without the weapon) into `sprites.json`; the game then sizes the person by the body.
- Sprite sets load when an ANPC using them appears (it may show up a moment after spawning) and unload a minute after the last one is gone; at startup only the files and their sizes are checked.
  Sheets are decoded and compressed on background threads, so loading does not stall the game; only 8-bit, non-interlaced
  PNGs take that path (other PNGs still load, on the main thread, with a short stall each). Big sheets still cost video
  memory while shown: every 1000x1000 pixels is about 1 MB.
- Sheet names pick the animation: `idle*` (standing), `walk*` (moving), `run*` (running), `hit*` (hurt), `attack*` (attacking), `attack_range*` or `attack_ranged*` (shooting a bow or throwing, see `rangedAtDistance`), `cast*` (spellcasting), `greet*` (greeting), `talk*` (speaking in a [conversation](#conversations)), `death*` (dying). Numbered names (`idle_1`, `idle_2`) are variants picked at random. Missing cast sheets use attack sheets (no attack sheet either: spells play no animation); missing attack_range sheets use attack sheets; missing run sheets use walk sheets and missing walk sheets use run sheets, then idle; missing hit/attack sheets use idle; greet sheets have no fallback; missing talk sheets use idle; a set needs at least one idle sheet.
- Turret: a set with no `walk*` and no `run*` sheet never moves: it does not wander, chase, flee or get knocked
  back, but still turns to its target and attacks, shoots or casts from where it stands. Like any DFU enemy it only
  notices a quiet player in front of it, so place turrets facing where trouble comes from. When `sprites.json` lists
  walk or run sheets but none of them can be used (wrong size, unreadable), `Player.log` says so: that set's people
  stand still too.
- Greeting: a calm ANPC with a `greet*` sheet sometimes (25 % per encounter) stops, turns and greets the player
  within 4 m and in sight when its people like you (`likes` or `loves`, the reaction dialogue uses), or another
  calm ANPC that is not an enemy. It greets the same person at most once every 2 game hours (not saved).
- Every hit (any health loss) plays a hit sheet from its first frame to its last, also when the person is hit again while it plays. A hit during an attack does not interrupt it (no stun-lock, as in vanilla): the hit sheet plays right after the attack sheet. Paralysis freezes the sprite.
- Size: the standing pose is as tall as the vanilla class sprite; set `"spriteHeight"` (world units) in `npc.json` to change it. Every animation uses the same scale.
- Overall size: `"spriteScale"` (0.2–2, default `1`) in `npc.json` shrinks or grows the whole sprite evenly (width with height), on top of the default size or `spriteHeight`. Rule of thumb for the samples: no taller than the vanilla class size minus about one of their own heads, but never shorter than Daggerfall's own Warrior (whose figure is about 2.03 m: the sample people stand 2.08 m). The two Dunmer on the larger Spellsword frame use `0.85`; the orc weakling is goblin-sized (`0.72`, 1.44 m). The spawn line in `Player.log` shows each sprite's final height.
- Proportions: `"spriteHeightScale"` defaults to `1`. Set it to `0.94` for a sprite that is 6% shorter with exactly the same width. It scales only the vertical dimension of all animations and the corpse, anchored at the feet; sprite sheets and timing remain unchanged.
- Death: the death sheet plays (shown from the side you see it from) and the body stays as the corpse, on the last frame of its front or back row, always facing you; see the **Corpses** [settings](#settings) to skip the animation or keep the body showing the side you see it from. `death_static.png` (one picture, always facing you) replaces the last death frame; `"deathStatic": { "groundY": n }` in `sprites.json` sets its ground row. The loot pile stays clickable. After leaving the area or reloading, DFU shows its own corpse picture again.
- Sheets may be edited in an image editor, but must keep the size `sprites.json` describes (checked at start; wrong sizes are reported in `Player.log`). Other files in the folder (`.psd`, backups) are ignored.
- Every attack plays its attack sheet from the first frame to the last.
- Action frame (optional): `"actionFrame": n` on an attack or attack_range (attack_ranged) sheet in `sprites.json` makes the blow (melee damage, or the arrow for archers) land when the sheet shows frame `n`, counted from 1 like the sheet's columns:
  `"attack_1": { "cellWidth": 272, "frames": 6, "actionFrame": 4 }`

#### Projectile images

`projectile.png` / `projectile_<n>.png` in a sprite set folder are flown by that ANPC's ranged attack (see
`rangedAtDistance`). A lone image needs no `sprites.json` entry: it is one frame, always facing you. An entry
describes a sheet of frames:

```json
"projectile": { "cellWidth": 52, "cellHeight": 52, "frames": 8, "pixelsPerUnit": 214.819, "fps": 12 }
```

- One row of frames is a billboard (the same from every side, e.g. a tumbling rock). Eight rows, in the usual
  direction order, are directional: row `front` shows it flying at you, `right` flying past to your left, and so on.
- `cellHeight` (default: the image height, one row), `pixelsPerUnit` (default: the set's, so it is drawn at the
  person's scale; a larger value draws a small object in more pixels) and `fps` (default: the set's) are optional.
- Frames loop while it flies. It flies straight, as fast as DFU's arrow, and hits like one (only the shooter's
  target is hurt). A thrown rock leaves no arrow behind.
  Without it, DFU's own timing is kept, which can look slightly early or late. Spells keep DFU's timing. The render script writes `actionFrame` itself from a pose marker named `action` on the attack Action, so it survives re-rendering.

## Sounds

An ANPC folder can have its own sounds in a `Sounds` folder (`.wav`, `.ogg` or `.mp3`; an `.ogg` must be audio only — one with embedded cover art does not load):

```
ANPCs/wench/
  npc.json
  Sounds/
    greet_1.ogg   greet_2.ogg
    attack_1.wav  attack_2.wav  attack_3.wav
    hit_1.wav
    death_1.ogg
    step_1.ogg    step_2.ogg   step_3.ogg
```

- A file's name says when it plays: `eat` / `drink` (each eat/drink cycle of the [eat package](#eating-and-drinking)), `hawk` (each call of the [hawk package](#chores-hawking-and-socializing)), `greet`, `halt` (guard pursuit warning; always plays, vanilla fallback), `attack`, `attack_range` (bow shots; uses `attack` sounds if it has none), `cast`, `heal` (self or support healing; no voice fallback), `hit` (hurt but alive), `death`, `step` and `talk` (a line in a [conversation](#conversations); always plays there, no chance roll, and nowhere else; `talk_male` / `talk_female` for one gender only). A trailing `_<number>` only makes a pool to pick from at random, and it has nothing to do with the sprite sheets' numbers: any `attack*` sheet plays a random `attack_*` sound. Other names are ignored (logged).
- Sounds follow what happens, not the sprite, so an ANPC with the vanilla sprite can have them too.
- Default footsteps: `step_*` files in the shared `ANPCs/_Sounds` folder play for every ANPC whose own `Sounds` folder has no `step` files (the examples ship four dirt-ground steps there). They play 15% quieter than an ANPC's own steps. An ANPC with step files of its own uses only those; `"defaultSteps": false` turns the defaults off for one ANPC.
- Default eat and drink sounds: `eat_*` / `drink_*` in `_Sounds` (CC0 crunches and swallows ship there) play for the eat package where the ANPC's own folder has none of that pool. Other names in `_Sounds` are ignored (logged).
- `death` always plays. `step` plays on every footfall while walking or running, but only when you are within `stepDistance`. The others play by chance.
- With a walk/run sheet, footsteps fall on its first and middle frame; `"stepFrames": [2, 6]` on a `walk*`/`run*` animation in `sprites.json` picks the frames (counted from 1). Without one, a step every 0.5 s walking, 0.33 s running.
- With **ANPC sounds** set to *ANPC* (the default, see [Settings](#settings)): DFU's own voice grunts (attack, pain) are replaced for the events the folder has sounds for, and kept for the others where DFU plays them: a melee attack, and a hit by a weapon (never for bow shots, spells or damage over time). While the folder's sounds are still loading, or when none of an event's files could be read, DFU's grunt stands in. A creature base class (an orc, say) with `attack` sounds also stops its own attack growl. Weapon swing, impact and parry sounds, and a creature's idle barks, are never changed.
- Sounds come from the ANPC in 3D, fade out by 30 m, follow DFU's sound volume, load when the first ANPC using them appears and unload a while after the last one goes.

Settings in `npc.json`, all optional:

```json
"sounds": {
  "combatChance": 50,
  "greetChance": 50,
  "stepDistance": 15,
  "soundPitch": 1.0,
  "soundPitchVariation": 0.05,
  "stepPitch": 1.0,
  "stepPitchVariation": 0.1
}
```

| Setting | Default | Meaning |
|---|---|---|
| `combatChance` | 50 | percent chance of an attack, attack_range, cast, heal or hit sound (heal also outside combat) |
| `greetChance` | 50 | percent chance of a greet sound |
| `stepDistance` | 15 | metres; no footsteps when you are farther |
| `soundPitch` | 1 | pitch of every sound except steps (0.25–3) |
| `soundPitchVariation` | 0 | each play changes that pitch by up to ± this (0–1) |
| `stepPitch` | 1 | pitch of footsteps (0.25–3) |
| `stepPitchVariation` | 0 | each footstep changes that pitch by up to ± this (0–1) |
| `defaultSteps` | true | without step files of its own, play the shared `_Sounds` steps |

After changing files, `anpc_reload_assets` re-reads them; `anpc_info` lists an ANPC's sound pools.

## Conversations

Two ANPCs that greet each other sometimes stop and chat, in turns: the one speaking plays a `talk` sound and its
`talk*` sheet, the other listens. No text: you only hear them, like Gothic's townsfolk.

```json
"conversation": { "enabled": true, "chance": 50 }
```

- Off unless `npc.json` has `"conversation"`. `"conversation": true` is the same as `{ "enabled": true }`; the
  object turns it on unless it says `"enabled": false`. `chance` (0–100, default 50) is the percent chance after
  this ANPC greets another one (the greeting itself happens 25 % of the time per encounter, see
  [Sprites](#sprites)).
- Both ANPCs need it on, `talk` files in their `Sounds` folder (see [Sounds](#sounds)) and the **ANPC sounds**
  setting on *ANPC*. An ANPC with the vanilla class sprite talks too (it shows its standing sprite). An ANPC
  without `greet` files still greets other ANPCs silently (it neither turns nor greets you), which is how
  conversations start. `talk*` sheets load only for ANPCs with `"conversation"` on.
- Who talks with whom: two calm ANPCs, or two enemies of yours (hostile ANPCs) while they have no one to fight,
  like bandits waiting in a dungeon. A calm ANPC never talks with an enemy. Once an enemy spots you (or anyone to
  fight), its conversation ends. `"conversation": false` (or no `"conversation"`) keeps an ANPC out of it.
- `talk_male_*` and `talk_female_*` files are lines for one gender only, for an ANPC whose gender is picked at
  random (no `"gender"` in `npc.json`): it uses its own gender's files, else plain `talk` files, never the other
  gender's.
- A conversation has 4 to 8 lines. The one who greeted speaks first, then they take turns; each line lasts as long
  as its sound (a different sound from the speaker's last one when it has two or more), with a short pause
  (0.4–1 s) after it; the first line waits for a greeting wave or hello to finish. The speaker loops a random `talk*` sheet, or its idle sheet without one.
- They stop and face each other while talking, and greet no one else.
- The same two talk at most once every 2 game hours (not saved).
- It ends at once when either is attacked, turns hostile or dies, when they end up more than 6 m apart, when you
  talk to one of them (the talk window opens as usual), or when one leaves (the town unloads). A line in progress
  is cut off. Leaving and loading a game ends every conversation.
- `anpc_chat` starts one right away for testing; `anpc_info` shows an ANPC's conversation setting and state.
- Every example has talk sounds and `"conversation"` on; `bandit`, `commoner`, `patron` and `spellcaster` (random
  gender) have `talk_male` and `talk_female` files.

## Name lists

- Generic people without `name`/`names` get generated names. `"nameList"` chooses the list: a custom file in `ANPCs/_Namelists/` (without `.json`) or a vanilla one: `default_breton`, `default_redguard`, `default_nord`, `default_darkelf`, `default_highelf`, `default_woodelf`, `default_khajiit`, `default_imperial`. Without `nameList` the person's own race is used.
- Custom lists come in two formats, described in `Examples/ANPCs/_Namelists/README.txt`: a bank copied from DFU's `NameGen.txt` plus a `"style"`, or plain `male`/`female`/`surnames` lists. Simple lists can put a gendered prefix before the surname (`maleSurnamePrefix`/`femaleSurnamePrefix`, e.g. Orsimer `gro-`/`gra-`). Ready-made lists include `orsimer`, `argonian`, `dunmer_morrowind` (Morrowind-style Dunmer given names and family names), and `khajiit_skyrim` (original Skyrim-style Khajiit whole names without surnames).
- A missing or broken list falls back to the race's vanilla list (one warning in `Player.log`).

## Examples

`Examples/ANPCs/` holds four generic templates for cities, towns and villages: `commoner` (one to three
cowardly commoners per town) and `cooper` (Normal, uses portrait `bram`), `baker` (Coward) and `smith` (Brave),
each at most once per town. Use `anpc_list` to find them. `Examples/ANPCs/_Dialogue/tavern_wench.json` is a
complete dialogue type; add `"dialogue": "tavern_wench"` to any ANPC to try it. `bandit` (hostile; human
strongholds, prisons, barbarian strongholds and ruined castles, and the wilderness at night) and `patron` (calm;
taverns) show spawning outside towns. `dunmer_spellblade` is a manual-spawn Dunmer Spellsword with eight-direction sprites, a casting sheet, a custom portrait, and the shared `dunmer_morrowind` namelist. Use `anpc_spawn dunmer_spellblade` to try him. `dunmer_mercenary` uses the iron-armoured model and axe, Fireball, Wizard's Fire and Resist Fire, with 80 magicka. Spawn him with `anpc_spawn dunmer_mercenary`.

`wench` is a cowardly tavern wench (one or two per town, often inside taverns) with idle, walk, attack, hit and death
sheets, her own portrait, the `tavern_wench` dialogue and her own greet, hit, death and step sounds (see
`SOUND_CREDITS.txt`). Try `anpc_spawn wench`.

Nine fully animated characters (custom portraits; hit/death/cast effects; the two mages have 10-11 sheets, the rest 14):

- `iron_sentinel`: hostile Breton knight with a two-handed claymore. Dungeons only (ruined castles, human strongholds, prisons).
- `orc_weakling`: a lower-tier orc with a crude club, on the `Orc` creature, made weaker and quicker with `"combat"`
  (70% health and damage, 1.3x attack speed). Throws rocks from afar (`attack_ranged_1`, a tumbling `projectile.png`,
  2–4 damage a hit). Orc strongholds, caves and mines.
- `nord_barbarian_1`: hostile Nord raider with a two-handed battle axe and 1.2x health. Barbarian and human strongholds,
  rarely in the wilderness.
- `castle_guard_1`: a friendly town guard with a spear, after Daggerfall's city guard. His idle holds the spear above his
  head, so his `sprites.json` carries `"standingHeight"`. One or two per town.
- `ohmes_knight_1`: a calm Khajiit knight-errant with a longsword, in cities, taverns and Knightly Order halls and
  rarely on the road by day. His tail is rigged and animated; `sprites.json` carries `"standingHeight"`.
- `argonian_female_acrobat_1`: a calm Argonian acrobat who fights barehanded (karate chop and front kick), with a
  rigged tail; towns and sometimes taverns. Uses `"race": "Argonian"`.
- `mages_guild_bald_man_1`: Daggerfall's bald Mages Guild mage as an ANPC. `"replaces": ["177.2"]` puts him in
  place of every vanilla bald-man flat (talk, quests and guild services stay the vanilla NPC's). Reads a floating
  spell book with his arms folded, smashes you with it up close, hurls fireballs (`projectile.png`) and casts
  Fireball and Wizard's Fire. No spawn block: he appears only as that stand-in.
- `cowled_old_man_1`: Daggerfall's cowled old man (`"replaces": ["177.0"]`), with his dragon-wing staff planted
  at arm's length as in the vanilla sprite, its head pulsing with light. Walks with the staff as a walking stick,
  strikes with it two-handed, looses blue fireballs from it and casts Shock, Paralysis, Heal and Shield.
- `argonian_staff_fighter_1`: the same Argonian in a red top, fighting with a two-handed quarterstaff (overhead
  strike and lunging thrust); towns, temples and Fighters Guild halls.

## Building (developers)

Sources live in `Scripts/Core` (pure logic, unit-tested) and `Scripts/Runtime` (DFU glue).
`Tools~` (ignored by Unity) holds:

- `run-tests.sh` — runs the Core NUnit tests in a small Unity host project.
- `run-core-tests.ps1` — runs the same Core NUnit suite with cached NUnit and Mono, without opening the Unity Editor.
- `compile-check.sh` — compiles every mod source against a DFU install's own assemblies.
- `runtime-check.sh` — compiles the sources with DFU's own in-memory runtime compiler and loads every type, catching problems only the game's compiler has (e.g. nested enums cause TypeLoadException there).
- `build-mod.sh` — builds `advancednpcs.dfmod` with Unity 2019.4.41f2 (a windowed editor that closes itself) and installs it, plus `Examples/ANPCs`, into a DFU install. In the editor the same build is under **Daggerfall Tools → Build Advanced NPCs mod**. Sounds you may not share (bought packs) can live outside git in `ANPC_LOCAL_SOUNDS` (default `F:/_Projects/Dagerfall/anpc_local_sounds`): `<anpc>/Sounds/<pool>_N.ogg` there replaces that ANPC's whole pool in the install.
- `selftest.sh [character] [save]` — fully unattended in-game test: starts DFU, loads a save standing outdoors in a town, runs `anpc_selftest`, quits and prints the results. Temporarily skips the startup options screen (settings.ini is restored).
  With an `ANPCs/selftest-looks.txt` file present, it also stands every installed sprite set in front of the
  camera and saves front and side screenshots in `anpc-looks/` next to `Player.log`.
- `play.sh [character] [save]` — starts DFU straight into a save (default `Testo` / `AtDaggerfall`) for manual testing: no options screen, no title menu, no self-test.

When adding a `.cs` file under `Scripts/`, also add it to `Files` in `AdvancedNPCs.dfmod.json`.


## Companion speech and barks

Only living, friendly hired companions use `cm_` sound pools. Put optional `.wav`, `.ogg` or `.mp3`
files in that ANPC's `Sounds` folder, using the usual numbered suffix: `cm_bark_1.wav`,
`cm_inn_2.ogg`, etc. Companion speech uses the existing pitch, sound volume and spatial attenuation.

| Pool | Trigger |
|---|---|
| `cm_bark` | Random ordinary chatter while calm. |
| `cm_dungeon` | Inside a dungeon, two thirds of scheduled bark choices use this pool; one third use `cm_bark`. |
| `cm_inn` | Inside a tavern/inn, the same two-thirds contextual choice. |
| `cm_hurt` | Below 25% health, replaces scheduled ordinary/contextual barks, including during combat. |
| `cm_no_magicka` | Once when magicka goes from a positive amount to zero. Regaining some magicka re-arms it; staying at zero never repeats it. |

Missing/undecodable pools are silent: there is no vanilla grunt or alternate pool substituted for a
missing `cm_` clip. Thus a missing contextual clip leaves that two-thirds choice silent; the standard
one-third choice still works. These pools ignore `combatChance` and `greetChance`.

Configure active real-time seconds in `npc.json` (not game hours):

```json
"sounds": {
  "barkMinSeconds": 45,
  "barkMaxSeconds": 90
}
```

These are also the defaults. Values must be from 1 to 86400, with maximum at least minimum. Equal
bounds give a fixed interval. Timers stop while paused or AI is disabled. Loading/area transfer,
re-enabling, or returning within the 30 m audible distance starts a fresh interval; no backlog of
missed barks is replayed. A fresh/reloaded companion already at zero magicka does not announce a new
depletion. Resource events that happen while muted are not queued for later.

All speech of one hired companion interrupts their previous speech, including ordinary greeting,
talk, combat and healing clips and fallback combat grunts. Different companions can speak together.
Footsteps and native weapon/spell effects keep separate sources and are not interrupted by speech.
Periodic healthy chatter is suppressed during combat. The existing ANPC/Vanilla/Off voice setting
still applies; custom barks require ANPC voices.

The companion menu has **Turn barks off/on**, saved per contract through waiting, transfers and
save/load. The mod's Companions settings have **Disable follower barks**, which disables every `cm_`
pool and hides that individual menu option. It preserves the individual preference, ordinary combat
voices and footsteps. Older saves default to barks enabled; no save conversion or rehire is needed.

Ironclad's example includes ten supplied CVR SALTY voice samples; `Sounds/VOICE-SOURCES.md` records
the original paths. No spells or no-magicka voice line are granted to this martial Rogue.
`anpc_selftest barks` checks live speech, timing, pause/mute, depletion edges and native menu/save
behavior. The companion/full suites also exercise real inn/load restoration and dungeon context.


### Optional companion event speech

These additional numbered pools use the same Sounds folder, volume/pitch, speech interruption and
both bark-disable controls. They are event acknowledgements, separate from the random bark timer:

| Pool | Trigger |
|---|---|
| `cm_recruit` | A successful recruitment, after the hiring conversation closes. |
| `cm_wait` | A successful Wait here order. |
| `cm_follow` | Resuming following after waiting. |
| `cm_dismiss` | One goodbye after an explicit successful dismissal; the contract ends immediately. |
| `cm_regroup` | A successful disengage/regroup order. |
| `cm_combat` | Entering a combat episode; changing targets does not repeat it. |
| `cm_victory` | Returning to calm after the last observed combat target is confirmed dead. |

Example names: `cm_wait_1.wav`, `cm_combat_2.ogg`. All are optional; missing pools remain silent,
without substitution. Command acknowledgements wait until gameplay resumes and the menu has closed.
Only the latest queued command is spoken, and it must still match this exact actor/contract/status.
Failed commands, muted events, loading, area reconstruction and betrayal do not queue these replies.
The single authorized dismissal goodbye may finish while the NPC is dismissed; that NPC cannot issue
further companion barks. It uses the same interruptible voice sources and stops on mute or teardown.

Victory means a confirmed dead combat target, not a kill-credit claim. Losing a living enemy, escaping,
regrouping, or loading a save cannot produce victory speech. These additions add no saved fields or
new per-frame world scans. `anpc_selftest barkevents` exercises commands, native menus, combat
transitions, dismissal, speech interruption and mute/pause handling.

Ironclad's supplied SALTY set includes 11 additional matching event clips (21 total); her provenance
document lists the original paths. The regroup line is a generic acknowledgement rather than a
specific retreat instruction.

Push voices use the optional companion sound pool **cm_push**, also named **push**: place files such as
`Sounds/cm_push.wav`, `Sounds/cm_push_1.ogg` or `Sounds/cm_push_12.wav` in the ANPC's folder.
Names such as `push.wav` and `push_7.ogg` work as well; both names join the same random pool.
A random variant plays on 50% of menu pushes and 15% of automatic contact pushes. Missing clips are
silent. These reactions respect the global follower bark setting, per-companion mute, and voice mode.

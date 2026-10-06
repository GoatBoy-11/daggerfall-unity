# Advanced NPCs v2 — Folders, Generic Townsfolk, Talk and Topics

- **Date:** 2026-10-06
- **Status:** Draft, awaiting review
- **Target:** Daggerfall Unity 1.9.2, Unity 2019.4.41f2
- **Branch:** `feature/advanced-npcs-v2`, branched from `feature/advanced-npcs-1a` (1a is complete, not merged or pushed)
- **Builds on:** `docs/superpowers/specs/2026-10-06-advanced-npcs-1a-design.md` (behaviour, hostility, crime and saving stay as specified there unless this document says otherwise)

## 1. Context

Step 1a delivered persistent, named NPCs ("ANPCs") defined as one flat JSON file each in `StreamingAssets/AdvancedNPCs/`. Before moving on to custom sprites (1b), the user redirected the design so that ANPCs can serve as everyday townsfolk and are easy for players to author:

- one folder per ANPC instead of one flat file,
- two kinds: **unique** (one fixed person, as in 1a) and **generic** (a template that populates towns),
- a mod setting that decides whether generic townsfolk are the same people on every visit,
- talking to any calm ANPC through DFU's own citizen talk window, with a custom name and portrait,
- extra dialogue topics for unique ANPCs.

The talk mechanism was proven in a throwaway spike (branch `spike/talk-window`, commit `49cf001`): the player clicked an ANPC in game and DFU's talk window opened with working vanilla topics. This spec reimplements it properly; the spike code is not merged.

### Order of work (each phase playable on its own)

1. **Phase A** — folder layout, migration of 1a files, portraits, talking (the spike approach, done properly).
2. **Phase B** — generic templates, town population, persistence setting.
3. **Phase C** — extra dialogue topics for unique ANPCs (starts with a mechanism spike, §9.3).

Custom sprites (1b) and schedules follow in later specs.

## 2. Goals and success criteria

1. An author creates `ANPCs/<folder>/npc.json` (and optionally `dialogue.json`) and a portrait PNG; no Unity install needed.
2. Existing 1a definition files are migrated automatically on first start; existing saves keep the NPCs' state (dead stays dead).
3. Clicking a calm ANPC (unique or generic) opens DFU's talk window showing its name and its PNG portrait, or a vanilla face when it has none. The vanilla topics (news, where is, work…) work as for a vanilla citizen.
4. A hostile or fleeing ANPC refuses to talk. Clicking in Info mode shows "You see <name>." instead of vanilla's "You see a <class>."
5. With the shipped generic template, Daggerfall city (and every other city, town and village) gets a handful of generic ANPCs on its streets in addition to vanilla citizens.
6. With the setting **Same people every visit**: leaving and re-entering a town, fast travel and save/load produce the same generic people (names, faces, positions) and keep their state (a killed one stays dead, an angry one stays angry until it calms down).
7. With the setting **Random each visit**: each time a town is built its generic ANPCs are re-rolled, and nothing about them is saved.
8. A unique ANPC with `dialogue.json` shows its extra topics in the talk window's "Tell Me About" list; choosing one shows the authored answer. Other NPCs do not show those topics.
9. Every data problem produces a single `[AdvancedNPCs]` log line naming the folder, file and field; one bad ANPC never stops the others.

## 3. Non-goals

- Custom sprites or 3D graphics (1b and later). The `sprites/` subfolder is reserved and ignored.
- Schedules, interiors, homes at night.
- Branching dialogue trees; topic conditions and actions (the format leaves room, §9.1).
- Extra topics for generic ANPCs.
- Replacing or reducing vanilla citizens. Generic ANPCs are added alongside them.
- Text macros (`%pcn` etc.) in authored answers.
- ANPC folders shipped inside other `.dfmod` packages. Loose files only.
- Changes to DFU core code. The mod must work on stock DFU 1.9.2.

## 4. Key decisions

| Decision | Choice | Reason |
|---|---|---|
| Data root | `StreamingAssets/ANPCs/` | Short; separate from the 1a folder so migration is unambiguous. |
| Identity | Folder name = id | One place to look; renaming a folder is visibly a new NPC. |
| Shared portraits | `ANPCs/_Portraits/*.png` | Shared between NPCs; the leading `_` sorts it first and marks it as "not an NPC". |
| Kinds | `kind: "unique"` (default) or `"generic"` in `npc.json` | One file format, two behaviours; old files are unique. |
| Generic placement | Walkable street cells from the town's own `CityNavigation` grid, picked by a seeded random | The same grid vanilla citizens spawn on; deterministic per town when seeded. |
| Generic persistence | Mod setting, default **Same people every visit** | User decision. Testing default is same-people. |
| Seeding | Own stable hash (FNV-1a) and own small PRNG in Core | `string.GetHashCode` and `UnityEngine.Random` are not stable or seedable per town; the hash must not change between runs or versions. |
| Talk | Hidden child `MobilePersonNPC` "talk proxy" + `TalkManager.TalkToMobileNPC`, portrait applied to the talk window by reflection | Proven in the spike; no `UIWindowFactory` override, so it coexists with talk-window replacement mods. |
| Extra topics | Mechanism decided by a spike at the start of Phase C, preferred approach first (§9.3) | `TalkManager` has no public hook for custom answers. |
| Save data | Same `NpcStateTable`, keyed by unique id or generic instance key; default states not saved | Old saves stay valid; save size does not grow with every town visited. |

## 5. Engine facts this design relies on

Verified in the DFU source at the fork's `master` (2026-10-06), in addition to 1a spec §5:

- `PlayerActivate` (`PlayerActivate.cs:409-443`) on a click first runs `ActivateMobileEnemy` (which sets the "You see a/an <class>" mid-screen text, lines 800-817) and then calls `IPlayerActivable.Activate(hit)` on the clicked transform. A component on the ANPC therefore runs last and can overwrite that text.
- `TalkManager.TalkToMobileNPC(MobilePersonNPC)` (`TalkManager.cs:727`) uses the "People of" faction of the current region and opens the talk window. A `MobilePersonNPC` added to a hidden child object only registers itself in `ActiveGameObjectDatabase` in `Awake`; nothing else iterates it (spike finding).
- `DaggerfallTalkWindow` keeps the portrait in protected fields `texturePortrait` and `panelPortrait`. Setting both after the window opens shows a PNG portrait (spike finding).
- `MobilePersonNPC.RandomiseNPC` calls its billboard `Asset.SetPerson` (`MobilePersonNPC.cs:224`), which a billboard-less proxy does not have. The proxy's face record therefore has to be set directly; the per race/gender face record tables are in `MobilePersonNPC.cs:32-39`.
- Each town `DaggerfallLocation` has a `CityNavigation` component (used by `PopulationManager`, `PopulationManager.cs:89,151`) with `GetNavGridWeightLocal`, `NavGridToWorldPosition` and `WorldToScenePosition`. Cells with weight > 0 are walkable streets.
- `DFRegion.LocationTypes` (`DFRegion.cs:66`) names location types: `TownCity`, `TownHamlet`, `TownVillage`, `HomeFarms`, `Tavern`, `ReligionTemple`, etc. `DaggerfallLocation.Summary` provides `MapID`, `RegionName`, `LocationName` and `LocationType`.
- Mod settings: a mod with a `modsettings.json` asset gets a settings window in DFU's mod list. `Mod.LoadSettingsCallback` is invoked on start (via `Mod.LoadSettings()`) and whenever the player changes settings (`Mod.cs:159,502`). Values are read with `ModSettings.GetInt/GetBool/GetValue` (`ModSettings.cs:68-139`).
- `TalkManager.ListTopicTellMeAbout` (`TalkManager.cs:457`) returns the live list the talk window displays. Answers come from `TalkManager.GetAnswerText(listItem)`, which only knows built-in question types (`TalkManager.cs:1992`); `DaggerfallTalkWindow.SelectTopicFromTopicList` (`DaggerfallTalkWindow.cs:1286`) calls it and adds the answer to the conversation list box.

## 6. Data layout

```
DaggerfallUnity_Data/StreamingAssets/ANPCs/
  _Portraits/                 shared portrait PNGs (any name)
    bram.png
    commoner_1.png
  bram/                       one folder per ANPC; folder name = id
    npc.json                  required
    dialogue.json             optional, unique ANPCs only
    sprites/                  reserved for 1b, ignored now
  commoner/
    npc.json                  a generic template
```

Rules:

- Folder names (ids) use lowercase letters, digits and `_`, and must not start with `_`. Folders starting with `_` are reserved for shared data and never loaded as NPCs. Other invalid names are skipped with an error.
- A folder without `npc.json` is skipped with a warning.
- `npc.json` must not contain `id`; if it does and it differs from the folder name, a warning is logged and the folder name wins.
- Unknown fields produce a warning naming the field (catches typos) and are otherwise ignored, so files written for later versions still load.
- The game's `selftest-autorun.txt` flag file (1a testing) moves to `ANPCs/`.

## 7. `npc.json`

### 7.1 Shared fields (both kinds)

All 1a behaviour fields keep their meaning, defaults and validation (1a spec §8): `baseClass`, `gender`, `bravery`, `fleeHealthPercent`, `calmDownHours`, `crimeOnAttack`, `wanderRadius`. New:

| Field | Required | Default | Rules |
|---|---|---|---|
| `kind` | no | `unique` | `unique` or `generic`. |
| `race` | no | the region's people (`ClimateSettings.People`) | `Breton`, `Redguard`, `Nord`. Used for the vanilla fallback face and generated names. |

### 7.2 Unique

```json
{
  "kind": "unique",
  "name": "Bram the Cooper",
  "location": { "region": "Daggerfall", "place": "Daggerfall" },
  "position": [73, 0.96, 645.89],
  "portrait": "bram",
  "baseClass": "Spellsword",
  "gender": "Male",
  "bravery": "Normal"
}
```

| Field | Required | Default | Rules |
|---|---|---|---|
| `name` | yes | — | Display name (talk window, HUD, logs). |
| `location`, `position` | yes | — | As 1a; written by `anpc_place`. |
| `portrait` | no | vanilla face | Name of a PNG in `_Portraits/` without `.png`. |
| `names`, `portraits`, `spawn` | not allowed | — | Error: these belong to generic templates. |

### 7.3 Generic

```json
{
  "kind": "generic",
  "names": [],
  "portraits": ["commoner_1", "commoner_2"],
  "spawn": {
    "locationTypes": ["TownCity", "TownHamlet", "TownVillage"],
    "places": [],
    "count": [1, 3]
  },
  "baseClass": "Bard",
  "bravery": "Coward",
  "wanderRadius": 12
}
```

| Field | Required | Default | Rules |
|---|---|---|---|
| `name` / `names` | no | generated | `name`: every instance has this name. `names`: one picked per instance. Neither: a DFU-style person name generated for the instance's race and gender. |
| `portrait` / `portraits` | no | vanilla face | One name, or a list from which each instance picks one. |
| `gender` | no | random per instance | As 1a. |
| `spawn.locationTypes` | no | `TownCity`, `TownHamlet`, `TownVillage` | `DFRegion.LocationTypes` names. Ignored when `spawn.places` is given. |
| `spawn.places` | no | empty (all matching towns) | List of `{ "region", "place" }` (case and spaces ignored, as 1a). When given, the template only spawns there. An entry may add `"positions": [[x,y,z], …]` (from `anpc_pos`); instances in that place use those spots in order instead of random street cells. |
| `spawn.count` | no | `[1, 3]` | `[min, max]` instances per town, whole numbers, `0 <= min <= max <= 20`. |
| `location`, `position` | not allowed | — | Error: these belong to unique ANPCs. |
| `dialogue.json` | ignored | — | Warning: topics are for unique ANPCs only (non-goal). |

### 7.4 Generic instances

When a town's GameObject is built, every generic template that matches the town rolls `count` instances. Each instance gets:

- **Key**: `<template>@<mapId>#<n>`, e.g. `commoner@1234#2` (`mapId` = `DaggerfallLocation.Summary.MapID`, unique per location; `n` from 0). The key is the instance's save-data id and appears in `anpc_list`.
- **Seed**: `StableHash(key)` in same-people mode; a fresh random seed per town build in random mode.
- From the seed, in a fixed order: gender (if not set), name, portrait (or vanilla face variant), spawn cell.

Spawn cells: all `CityNavigation` cells with weight > 0, sorted by grid coordinate, picked by the seeded PRNG without repeats. Positions from `spawn.places[].positions` take precedence. A town with no walkable cells and no positions gets no generic instances (logged once per town).

The setting **Max generic ANPCs per town** caps the total. When the rolls exceed it, instances are kept in template-id order, then by `n`, so the kept set is deterministic.

## 8. Persistence setting

`modsettings.json` (DFU mod settings window), section **Population**:

| Key | Type | Default | Meaning |
|---|---|---|---|
| `GenericPeople` | multiple choice: `Same people every visit`, `Random each visit` | `Same people every visit` | See below. |
| `MaxGenericPerTown` | slider 0–30 | `12` | 0 turns generic ANPCs off. |

- **Same people every visit**: instance keys and seeds as §7.4. State (dead, hostile, calm-down deadline, health) is kept in the `NpcStateTable` under the instance key and saved like a unique ANPC's.
- **Random each visit**: instances are rolled with a fresh seed and get a standalone `NpcState` that is never put in the table, so nothing is saved. Crimes against them still count.
- Changing the setting during play applies the next time a town is built; already spawned ANPCs stay as they are.
- Saved entries only exist for states that differ from fresh (alive, calm, full health); `NpcStateTable.Snapshot` drops default entries. Entries for keys that no longer match any definition are still kept (1a rule).

## 9. Talking

### 9.1 `dialogue.json` (unique only)

```json
{
  "topics": [
    { "caption": "Barrels", "answers": ["Best barrels in Daggerfall, and I make every one myself."] },
    { "caption": "The north gate", "answers": ["Guards there take bribes.", "Quiet lately."] }
  ]
}
```

| Field | Required | Rules |
|---|---|---|
| `topics` | yes | List, at most 20 entries. |
| `topics[].caption` | yes | Shown in the topic list. Unique within the file (case ignored). Longer than 24 characters → warning (the list is narrow). |
| `topics[].answers` | yes | One or more non-empty texts; one is picked at random each time the topic is chosen. |

An invalid `dialogue.json` is skipped with an error; the NPC still loads without extra topics. Later versions will add optional `id`, `conditions` and `actions` per topic; unknown fields already only warn (§6).

### 9.2 Opening the talk window (`NpcTalk`)

A component on every spawned ANPC implementing `IPlayerActivable`:

- Creates a hidden child object with a `MobilePersonNPC` talk proxy: `NameNPC` = instance name, `Race`, `Gender`, and `PersonFaceRecordId` from the vanilla face tables (§5) using the instance seed (unique: `StableHash(id)`).
- `Activate(hit)`, by `PlayerActivate` mode:
  - **Steal**: nothing (vanilla enemy pickpocketing runs).
  - **Info**: mid-screen text "You see <name>." (overwrites vanilla's class text).
  - **Grab / Talk**: if farther than `PlayerActivate.MobileNPCActivationDistance`, nothing. If the brain is not Calm: "<name> will not talk to you now." Otherwise `TalkManager.Instance.TalkToMobileNPC(proxy)`, then apply the portrait (if any) and the extra topics (§9.3).
- Portrait: `texturePortrait` and `panelPortrait.BackgroundTexture` set by reflection, filter mode from `DaggerfallUI.Instance.GlobalFilterMode`. If either field is missing (a replacement talk window without them), the vanilla face stays and one warning is logged per session.

### 9.3 Extra topics

Wanted behaviour: while the player talks to a unique ANPC with topics, the "Tell Me About" list shows one entry per topic after the vanilla entries; choosing one adds the question and the authored answer to the conversation. Closing the window removes them, so no other NPC shows them and nothing reaches DFU's conversation save data.

Phase C starts with a spike to choose the mechanism (throwaway, like the talk spike):

1. **Preferred — list injection.** Append `TalkManager.ListItem`s (type `Item`, question type `NoQuestion`, recognisable `key`) to `TalkManager.Instance.ListTopicTellMeAbout` after the window opens and refresh the list box. Hook the window's topic list box selection so that, after the window has handled the click, the mod replaces the answer line it added with the authored answer (and the question line with "Tell me about <caption>."). Remove the items when the window closes.
2. **Fallback — window subclass.** Register a `DaggerfallTalkWindow` subclass with `UIWindowFactory` that overrides `SelectTopicFromTopicList` for marked items. Works reliably but conflicts with other mods that replace the talk window; documented in the README if chosen.

Spike exit criteria: in game, a topic is listed, its answer appears, vanilla topics still answer correctly, and the topic is absent when talking to a vanilla citizen afterwards. The chosen mechanism and its findings are recorded in the Phase C plan before implementation.

## 10. Portraits

- `_Portraits/*.png` are loaded once at start into a name → `Texture2D` cache (point filtering by default; the talk window applies the global filter mode).
- Recommended: 64×64 pixels (the talk window's portrait panel size); other sizes are scaled.
- A referenced portrait that does not exist → one warning per name at load; that NPC uses the vanilla face.
- Unreadable PNG → error for that file; same fallback.

## 11. Migration from 1a

On start, if `StreamingAssets/AdvancedNPCs/` contains `*.json` files:

1. Each file is parsed with the 1a rules. Invalid files are left untouched and logged.
2. For a valid file with id `X`: if `ANPCs/X/` does not exist, create it and write `npc.json` with the original text minus the `id` field (other fields, order and formatting kept). Then rename the old file to `<name>.json.migrated`.
3. If `ANPCs/X/` already exists, nothing is written; the old file stays and a warning says so.
4. One summary line: "Migrated N definition(s) to ANPCs/."

Because the folder name equals the old id, saved states keep matching. `anpc_place` now edits `ANPCs/<id>/npc.json`. The save-data class stays `v1` (same shape; generic keys are just more entries).

## 12. Architecture

Pure logic stays in `AdvancedNPCs.Core` (no Unity or DFU references, NUnit-tested); DFU glue stays in `Scripts/Runtime`. New and changed units:

### 12.1 Core

| Unit | Job |
|---|---|
| `FieldReader` | The typed field readers now private to `DefinitionParser` (text, number, bool, number list, plus string list and object list), and unknown-field warnings. Split out so the three parsers share them. |
| `NpcDefinition` | Gains `Kind`, `Race`, `Portraits` (list; unique has 0 or 1), `Generic` (spawn rules, names), `Topics`, `Folder`. |
| `DefinitionParser` | Parses one folder: folder name + `npc.json` text (+ optional `dialogue.json` text) → definition or error, plus warnings. Unique and generic sections in separate methods. |
| `DialogueParser` | `dialogue.json` text → topic list or error. |
| `DefinitionCatalog` | Built from folders. `Uniques` by id (as 1a `ById`), `Generics` list. `ForLocation(region, place)` → uniques; `GenericsFor(region, place, locationType)` → matching templates. |
| `StableHash` | FNV-1a 32-bit over UTF-8 bytes. Fixed test vectors pin it forever. |
| `SeededRandom` | Small deterministic PRNG (xorshift32) with `Next(max)`, `Range(min, max)`, `Pick(list)`. |
| `NpcInstance` | What gets spawned: `Key`, `Definition`, `Name`, `Gender`, `Race`, `PortraitName`, `FaceVariant`, `Seed`, local position (set later for random cells), `Persistent`. Unique ANPCs become instances with `Key = id`. |
| `PopulationPlanner` | `Plan(templates, town, mode, cap, cellCount, nameSource)` → ordered `NpcInstance` list with chosen cell indices or fixed positions. `town` carries map id, region, place, location type and the region's default race. Pure and deterministic; Runtime turns cell indices into positions. |
| `NameSource` (interface) | `Generate(race, gender, random)`; Core ships a fixed-list implementation for tests, Runtime wraps DFU's name generator. |
| `VanillaFaces` | Copy of the face record tables (§5) → `FaceRecord(race, gender, variant)`. |
| `Migration` | 1a file text → `(id, npc.json text)` or error. Removes the `id` member textually so formatting survives. |
| `NpcStateTable` | `Snapshot` omits default states. |

### 12.2 Runtime

| Unit | Job |
|---|---|
| `AnpcFiles` | Reads the `ANPCs` folder tree into Core inputs; runs migration first; writes `npc.json` for `anpc_place`. All file I/O lives here. |
| `PortraitLibrary` | Loads `_Portraits/*.png`, caches by name, logs missing/unreadable. |
| `ModConfig` | Reads `GenericPeople` and `MaxGenericPerTown` through `LoadSettingsCallback`. |
| `NpcSpawner` | As 1a for uniques (via `NpcInstance`); additionally plans and spawns generic instances per town from its `CityNavigation`. |
| `NpcBrain` | `Init(instance, state)` instead of `(definition, state)`; uses `instance.Key` and `instance.Name`. Behaviour unchanged. |
| `NpcTalk` | §9.2. |
| `TopicInjector` | §9.3, mechanism chosen by the spike. |
| `AdvancedNpcsMod` | Wiring; console commands updated (§13). |

Data flow: files → `AnpcFiles` → `DefinitionCatalog` + `PortraitLibrary` → (town built) → `NpcSpawner` asks `PopulationPlanner` → `NpcInstance`s + states → `NpcBrain` + `NpcTalk` per spawned object → state changes back into `NpcStateTable`.

`NpcSpawner` keeps the 1a rules for stale copies and respawn after load (`SpawnRules`), now keyed by instance key.

## 13. Console commands

- `anpc_pos` — unchanged.
- `anpc_list` — also lists generic instances (key, name, distance, state).
- `anpc_place <id>` — unique only; rewrites `ANPCs/<id>/npc.json`. For a generic key it answers that generic ANPCs are placed by `spawn` rules.
- `anpc_summon <key>` — any spawned ANPC.
- `anpc_selftest` — extended (§15).

## 14. Error handling

Every message starts with `[AdvancedNPCs] ` and names `<folder>/<file>: <field>: <problem>`.

| Problem | Result |
|---|---|
| Invalid folder name | Folder skipped, error. |
| Folder without `npc.json` | Folder skipped, warning. |
| Invalid `npc.json` (JSON, required field, bad value, wrong kind field) | ANPC skipped, error. |
| Unknown field | Warning, field ignored. |
| `id` in `npc.json` differs from folder | Warning, folder name used. |
| Invalid `dialogue.json` | Topics skipped, error; ANPC still loads. |
| `dialogue.json` on a generic template | Warning, ignored. |
| Missing or unreadable portrait | Warning/error once per portrait; vanilla face. |
| Talk window lacks the portrait fields | Warning once per session; vanilla face. |
| Town without walkable cells | Info once per town; no generic instances there. |
| Migration target folder exists | Warning; old file untouched. |
| Spawn failure | Error; that instance skipped. |
| No `ANPCs` folder and nothing to migrate | Info line; mod idles. |

## 15. Testing

1. **Core unit tests** (existing harness `Tools~/run-tests.sh`): folder/`npc.json` parsing for both kinds and every error row in §14 that Core produces; `dialogue.json` parsing; `StableHash` test vectors; `SeededRandom` sequence pinned; `PopulationPlanner` — same inputs give identical plans, cap trimming order, fixed positions take precedence, `count` bounds, place and location-type matching; `VanillaFaces` lookups; `Migration` keeps formatting and removes only `id`; `NpcStateTable.Snapshot` omits defaults and keeps unknown keys.
2. **Compile checks**: `compile-check.sh` and `runtime-check.sh` (DFU's runtime compiler) after each task touching Runtime.
3. **In-game self-test** (`selftest.sh`, unattended), new checks added to the 1a list:
   - calm ANPC opens the talk window with its name and PNG portrait; ANPC without portrait opens it with a vanilla face;
   - hostile ANPC refuses to talk;
   - Info-mode click shows "You see <name>.";
   - same-people mode: building the same town twice yields identical generic keys, names and positions; a generic instance's damage survives a save/load;
   - random mode: two builds differ and no generic keys are saved;
   - `MaxGenericPerTown` = 0 spawns no generics;
   - Phase C: a unique ANPC's topic is listed and answered; it is absent for the next NPC talked to.
4. **Manual** (user): look and feel of portraits and townsfolk density in Daggerfall city; talk to Bram, a generic commoner and a vanilla citizen in a row.

## 16. Shipped examples

`Examples/ANPCs/` mirrors the game layout and is installed by `build-mod.sh`:

- `bram/`, `coward_cora/`, `brave_bors/` — the three 1a examples migrated; `bram/dialogue.json` with two topics (Phase C).
- `commoner/` — generic template for cities, towns and villages (Phase B).
- `_Portraits/` — empty except a `README.txt` with the portrait spec; the user is producing art. Self-tests generate their own textures.

## 17. Risks and items to verify early

| Risk | Mitigation |
|---|---|
| `CityNavigation` grid not ready when `OnCreateLocationGameObject` fires | First Phase B task checks it in game; fallback: plan generics one frame later. |
| Many full-AI ANPCs across several loaded towns cost frame time | `MaxGenericPerTown` cap (default 12); measure in Daggerfall city during Phase B; spawning only towns near the player is a later option. |
| DFU name generator not seedable per instance | Verify early in Phase B; fallback: draw from a built-in name list via `SeededRandom`. |
| Topic injection breaks vanilla talk behaviour or leaks into conversation save data | Phase C spike with explicit exit criteria; items removed on window close; fallback mechanism defined. |
| Talk-window replacement mods lack the portrait fields | Reflection null-check, vanilla face, one warning. |
| Migration writes into the user's game folder | Never overwrites; old files renamed, not deleted; every action logged. |
| Generic keys depend on `MapID` | `MapID` is fixed by the game data; covered by the same-people self-test. |

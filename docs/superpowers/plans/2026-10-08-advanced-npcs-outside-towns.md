# Advanced NPCs — spawning outside towns: Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Generic templates appear in dungeons, interiors and the wilderness (chance, count, filters, `when`),
`anpc_spawn` works indoors, and `anpc_here` explains every spawn decision.

**Architecture:** Rules in Core (pure C#, NUnit): spawn-block parsing, a planner deciding who appears in a place
(chance, count, keys, seeds, explanation lines), a spot picker over candidate points, encounter rules for the
wilderness. Runtime: three spawners hooked to DFU's transition events and a wilderness timer, collecting candidate
points from DFU's markers and reusing the existing person-spawning code.

**Tech Stack:** C# for DFU 1.9.2's runtime compiler (no nested enums, no enum-typed fields across files),
Unity 2019.4, NUnit in the Unity 6 test host, the mod's own `Json.cs`.

**Spec:** `docs/superpowers/specs/2026-10-08-advanced-npcs-outside-towns-design.md`

**Execution:** native (the user's standing workflow: spec → plan → native execution → fresh reviewer).

## Global Constraints

- Stock DFU 1.9.2 only: always confirm APIs with `Tools~/compile-check.sh` (the repo's master is newer than 1.9.2).
- A template with only the old town fields behaves exactly as before; old saves load unchanged; town keys stay
  `template@mapId#n` and `template@mapId+n`.
- `chance` whole 0–100; `count` `[min, max]` with `1 <= min <= max <= 10`; `wilderness.max` 1–10 (default 1).
- Dungeon spots never within 20 m of the enter marker; interior spots never within 2 m of a door; wilderness ring
  40–80 m, out of view, slope < 35°, not water, 8 tries; despawn > 150 m and out of view.
- Wilderness roll every 10 in-game minutes; never while resting, travelling or fast travelling.
- `when` in spawn blocks: C1 conditions without `asked`, `notAsked`, `tone`, `reaction` (warn there).
- Messages: `<file>: <field>: <problem>`, one line each; a bad block never stops the template's other blocks.
- Settings: Dungeons / Interiors / Wilderness switches (default on); "Max wilderness ANPCs around you" (default 4);
  "Max generic ANPCs per town" also caps each interior.
- Indoor `wanderRadius` capped at 3 m.

## Review Focus

1. Re-entering the same dungeon/building in same-people mode → same people at the same spots; killed ones absent.
2. Saving and loading **inside** a dungeon or building → people come back once (no duplicates, none lost).
3. A dungeon with fewer usable spots than people → the extra people are skipped quietly (no stacking in one spot).
4. Wilderness while the time is accelerated (rest/travel) → no rolls, no spawns.
5. Leaving the wilderness into a town or fast travelling → every wilderness person despawns.

Covered by: 1, 3 in `OutOfTownPlannerTests`/`SpotPickerTests` plus a self-test re-entry check (Task 8); 2 by a
self-test save/load check inside a building (Task 8); 4, 5 by `EncounterRulesTests` and self-test (Task 8).

---

### Task 0: Spike (throwaway, branch `spike/outside`)

Answer, in `docs/superpowers/specs/2026-10-08-advanced-npcs-outside-spike-findings.md`:
1. Marker spots: in a crypt, a human stronghold, a tavern, a Fighters Guild hall and a temple, how many candidate
   points exist (random/fixed enemies, random treasure, interior markers, DFU's people) and do people placed 1.5 m
   from them stand on the floor inside the room? (Screenshots + positions in Player.log.)
2. When are those objects present relative to `OnTransitionDungeonInterior` / `OnTransitionInterior` (same frame,
   next frame)? Are enemies still at their markers then?
3. Can the self-test enter a building and a dungeon by script (`PlayerEnterExit.TransitionInterior` with a door
   from the town's `StaticDoors`, `TransitionDungeonInterior`) and leave again reliably? If not, list the test
   saves needed (inside a tavern, inside a dungeon, in the wilderness) and how to make them unattended.
4. Dungeon type and guild group: confirm the reads (`PlayerGPS.CurrentLocation.MapTableData.DungeonType`,
   `BuildingDiscoveryData.factionID` → `GuildManager.GetGuildGroup`).
5. Wilderness: confirm "outside every location" (`PlayerGPS.IsPlayerInLocationRect`), time acceleration
   detection, terrain ray layer and water test.
Decision recorded: markers vs grid fallback; scripted entry vs test saves. Update Tasks 5–8 with the findings.

### Task 1: Spawn blocks (parsing)

**Files:** Modify `Scripts/Core/GenericSpawn.cs`, `Scripts/Core/DefinitionParser.cs`; Create
`Scripts/Core/OutsideSpawn.cs`; Tests `Editor/Tests/OutsideSpawnParserTests.cs`, cases in `GenericTemplateTests.cs`.

**Produces:**
- `DungeonSpawn { int Chance; int CountMin = 1, CountMax = 1; List<string> DungeonTypes; Condition When; }`
- `InteriorSpawn { int Chance; int CountMin = 1, CountMax = 1; List<string> Buildings; Condition When; }`
  (Buildings are canonical keys: building type names, `house`, `guildhall`, guild keys from `Guilds.Keys`)
- `WildernessSpawn { int Chance; int Max = 1; Condition When; }`
- `GenericSpawn.Dungeons / Interiors / Wilderness` (null when absent); `GenericSpawn.HasTownRules` (false when
  only context blocks were given and no `locationTypes`/`places`).
- `DungeonTypeNames.Canonical(string)`, `BuildingNames.Canonical(string)` (case/spaces ignored).
- `ConditionParser.Parse(..., bool allowTone, bool allowConversation)` overload: `asked`, `notAsked`, `reaction`
  rejected with "only allowed in dialogue" when `allowConversation` is false.

**Tests:** each block parsed with defaults; chance out of range / not whole → block skipped with message; count
bounds; unknown dungeon type / building → message lists allowed names; unknown field hint; `when` with `asked` →
message; template with only `dungeons` does not match towns; old template unchanged (existing tests stay green).

### Task 2: Places, planner, explanation

**Files:** Create `Scripts/Core/OutOfTownPlanner.cs`; Test `Editor/Tests/OutOfTownPlannerTests.cs`.

**Produces:**
- `PlaceInfo { string Kind /* "dungeon" | "interior" */; int MapId; string Region, Place, DefaultRace; string DungeonType;
  string Building /* canonical building key */; string Guild /* guild key or null */; int BuildingKey;
  string PlaceKey /* "<mapId>:dungeon" | "<mapId>:b<key>" */ }`
- `OutOfTownPlanner.Plan(IList<NpcDefinition> templates, PlaceInfo place, GenericMode mode, int cap, uint visitSeed,
  INameSource names, IDialogueFacts facts, List<string> explain) -> List<NpcInstance>` — per template (id order):
  filter, `when`, chance roll (`StableHash(template@PlaceKey)` in same mode), count, keys
  `template@<PlaceKey>#n`, seeds, `Persistent = same`; each instance gets `CellIndex = -1` and a group index.
  `explain` gets one line per template: `bandit: dungeon type Crypt not in [HumanStronghold]`,
  `bandit: when: time fails`, `bandit: chance 30, rolled 72: not here`, `bandit: chance 30, rolled 12: 3 spawned`.
- `PlacedNpc.context` + `PlacedNpc.Key()` → `template@mapId+n` (town), `template@mapId:dungeon+n`,
  `template@mapId:b5+n`; `PlacedNpcList.Add(..., string context)`, `ForPlace(int mapId, string context)`.

**Tests:** same mode repeats exactly (who, count, keys, seeds); random mode varies with visitSeed; chance 0 never,
100 always; filters (dungeon type, building, guild hall by guild, `house`, `guildhall`); cap; explanation lines;
placed keys for the three contexts; old `PlacedNpc` without context = town key unchanged.

### Task 3: Spot picker

**Files:** Create `Scripts/Core/SpotPicker.cs`; Test `Editor/Tests/SpotPickerTests.cs`.

**Produces:** `SpotPicker.Pick(IList<float[]> candidates /* x,y,z sorted */, float[] avoid /* entrance or null */,
float avoidRadius, int groups, IList<int> groupSizes, uint seed) -> List<int>` (candidate index per group, distinct,
−1 when none left); `SpotPicker.GroupOffsets(int size, uint seed) -> List<float[]>` (first at 0,0; rest 2–3 m
around, ≥ 1 m apart); `SpotPicker.Sort(List<float[]>)` stable order.

**Tests:** avoid radius respected; distinct spots; same seed same picks; fewer spots than groups → −1 for the rest;
offsets spacing and range.

### Task 4: Encounter rules (wilderness)

**Files:** Create `Scripts/Core/EncounterRules.cs`; Test `Editor/Tests/EncounterRulesTests.cs`.

**Produces:** `EncounterRules.DueRolls(ulong lastRollMinute, ulong nowMinute) -> int` (whole 10-minute steps,
capped at 1 so time jumps never burst-spawn); `EncounterRules.RingPoint(float px, float pz, float viewX, float viewZ,
double r1, double r2) -> float[]` (40–80 m, > 70° from view direction); `EncounterRules.MayRoll(bool inLocation,
bool timeAccelerated, bool travelling)`; `EncounterRules.CanSpawn(int aliveOfTemplate, int max, int aliveTotal,
int totalCap)`; `EncounterRules.ShouldDespawn(float distance, bool inView)`.

**Tests:** steps and burst cap; ring distance and angle for many seeds; rolls blocked when in a location /
accelerated / travelling; caps; despawn rule.

### Task 5: Interiors and dungeons at runtime

**Files:** Create `Scripts/Runtime/IndoorSpawner.cs`, `Scripts/Runtime/SpotFinder.cs`; Modify
`Scripts/Runtime/NpcSpawner.cs` (expose `Spawn(NpcInstance, NpcState, Transform)` as `SpawnUnder`),
`Scripts/Runtime/ModConfig.cs`, `modsettings.json`, `AdvancedNpcsMod.cs`.

- Hook `OnTransitionInterior` / `OnTransitionDungeonInterior` (timing per spike); build `PlaceInfo`; planner;
  `SpotFinder.Candidates(...)` from markers (per spike); `SpotPicker`; floor ray + capsule clearance per person;
  spawn with `SpawnUnder(instance, state, interiorOrDungeonTransform)`; dead skipped via `NpcState.dead`.
- Despawn: people are children of the interior/dungeon object; clear brains on exit events too.
- Load inside: `OnStateRestored` → respawn for the current interior/dungeon (Review Focus 2).
- Settings: `Dungeons`, `Interiors`, `Wilderness` (bool), `MaxWildernessAround` (int 0–10, default 4).
- Indoor `wanderRadius` min(def, 3).

### Task 6: Wilderness at runtime

**Files:** Create `Scripts/Runtime/WildernessSpawner.cs`; Modify `AdvancedNpcsMod.cs`.

- MonoBehaviour tick: `EncounterRules.MayRoll`, `DueRolls` on DFU's game minutes; per template with `wilderness`:
  `when`, chance (fresh random), caps; `RingPoint` up to 8 tries; terrain ray, water, slope; spawn under the
  StreamingWorld's world parent (floating origin safe); key `template@wild#<counter>`, `Persistent = false`.
- Despawn rule each second; all on entering a location, any transition, fast travel (`OnClearStreamingWorld`).

### Task 7: anpc_spawn indoors, anpc_here

**Files:** Modify `AdvancedNpcsMod.cs` (`SpawnInFront` by context), `NpcSpawner.cs`/`IndoorSpawner.cs`
(spawn placed people of the current place), `Scripts/Core/PlacedNpc.cs`.

- `anpc_spawn` inside: context `dungeon` / `b<buildingKey>`, position relative to the dungeon/interior transform;
  wilderness: "Wilderness encounters are not kept; stand in a town, building or dungeon."
- `anpc_here`: kind and names (dungeon type / building / guild / wilderness), then the planner's explanation
  lines (wilderness: each template's chance, `when`, alive/max and last roll).

### Task 8: Self-test, example, README

**Files:** Modify `Scripts/Runtime/SelfTest.cs`, `README.md`; Create `Examples/ANPCs/bandit/npc.json` (hostile
dungeon + wilderness template, vanilla sprites), `Examples/ANPCs/patron/npc.json` (calm tavern template).

Self-test checks (per spike's entry method): interior people spawned, count within range, none within 2 m of a door;
re-entry same people (same keys and spots); dead stays dead; save/load inside → same people once; dungeon group
together, none within 20 m of the entrance; wilderness spawn out of view at 40–80 m; despawn when far; no rolls
while resting; `anpc_spawn` inside a building saved and restored; `anpc_here` lines. LOOK screenshots: dungeon
group, tavern patron. README: "Spawning outside towns" section + `anpc_here`.

### Task 9: Verify and review

`run-tests.sh`, `compile-check.sh`, `runtime-check.sh`, `build-mod.sh` (then `git restore
Assets/AddressableAssetsData/`), `selftest.sh` ×3; fresh reviewer on the whole branch; fix findings; mod 0.5.0.

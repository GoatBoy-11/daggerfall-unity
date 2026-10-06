# Advanced NPCs — Step 1a Design

- **Date:** 2026-10-06
- **Status:** Draft, awaiting review
- **Target:** Daggerfall Unity 1.9.2, Unity 2019.4.41f2
- **Branch:** `feature/advanced-npcs-1a` (fork: GoatBoy-11/daggerfall-unity)

## 1. Context

The long-term goal is a new kind of NPC for Daggerfall Unity, closer to modern Elder Scrolls games: persistent named individuals with schedules, richer animations, rigged 3D models rendered as billboards, and the ability to turn hostile and calm down again. It must be easy for other mod users to create their own NPCs.

The full roadmap, each step playable on its own:

1. **One persistent NPC outdoors** — this document covers part **1a** (behaviour with vanilla sprites). Part **1b** adds custom baked 8-angle sprites.
2. Off-screen and on-screen schedule simulation, outdoors.
3. Interiors (tavern, home) with sleep/eat animations at markers.
4. Data-driven authoring kit (expanded definitions, templates, Blender bake script).
5. Real-time 3D-to-billboard renderer.

## 2. Goals for 1a

A user can drop a JSON file into a folder and get a named NPC standing in a town that:

- Exists alongside vanilla civilians (does not replace them).
- Is calm by default and does not block resting or fast travel while calm.
- Becomes hostile **only toward whoever attacked it** (player or another creature).
- Reacts according to a bravery level: Coward, Normal, Brave.
- Calms down after a configurable number of in-game hours.
- Treats player attacks as crimes (assault, murder) with guard response.
- Keeps its state (alive/dead, hostile/calm, calm-down deadline, health) across town visits and save/load.
- Stays dead permanently once killed.

### Success criteria

1. NPC appears at its defined position in its town.
2. Resting and fast travel work normally near the calm NPC.
3. Hitting it: it turns hostile to the player, an assault crime is raised, guards spawn.
4. Other Advanced NPCs nearby stay calm.
5. Coward flees immediately; Normal fights then flees below its health threshold; Brave fights to the death.
6. After resting elsewhere past its calm-down deadline, returning finds it calm.
7. Save and reload preserve its state.
8. Killing it raises a murder crime; it never respawns, including after reload.

## 3. Non-goals for 1a

- Custom sprites or 3D models (step 1b and later).
- Schedules, interiors, dialogue, gestures, sleep/eat animations.
- Pathfinding (movement is direct; may snag on walls).
- Healing while fleeing.
- Per-NPC crime categories beyond on/off (the `crimeOnAttack` flag is the hook for this later; user-facing option "C").
- NPC definitions inside other `.dfmod` packages (loose files only for 1a).
- Changes to DFU core code. The mod must work on stock DFU.

## 4. Key decisions

| Decision | Choice | Reason |
|---|---|---|
| Base components | Vanilla enemy stack (`EnemyMotor`, `EnemySenses`, `EnemyAttack`, `EnemyHealth`) created via `GameObjectHelper.CreateEnemy(..., MobileReactions.Passive)` | Combat, damage, death, loot, sounds, and rest/travel checks come for free. A fully custom AI would rewrite all of this and vanilla monsters would ignore the NPC. |
| Engine side effects | Mod-side "hostility guard", no core patch | Must run on stock DFU. Upstream PR not expected to be accepted. |
| Who it fights | Attacker only | Matches modern TES behaviour; avoids punishing the player for a monster's attack. |
| Crime | On by default (vanilla civilian rules), per-NPC flag | User chose eventual per-NPC control; flag present from day one so it is a data change later. |
| Visuals for 1a | Vanilla class sprites (`baseClass`) | Isolates behaviour risk from rendering risk. |
| Definition storage | Loose JSON in `StreamingAssets/AdvancedNPCs/` | Authors need no Unity install for 1a NPCs. |

## 5. Engine facts this design relies on

Verified in the DFU source at the fork's `master` (2026-10-06):

- `GameManager.AreEnemiesNearby()` (`GameManager.cs:684`) only counts enemies where `EnemyMotor.IsHostile` is true (line 710). Calm NPCs therefore never block rest or travel.
- `EnemyMotor.IsHostile` (`EnemyMotor.cs:90`) is a public get/set property.
- When the player attacks a non-hostile enemy, `DaggerfallEntityBehaviour.HandleAttackFromSource` (`DaggerfallEntityBehaviour.cs:256-258`) calls `GameManager.MakeEnemiesHostile()`, which sets `IsHostile = true` on **every** active enemy (`GameManager.cs:793`). This is the side effect the hostility guard must undo.
- `EnemyMotor.MakeEnemyHostileToAttacker(attacker)` (`EnemyMotor.cs:186`) sets `senses.Target` to the attacker only if the enemy has no target in sight within 2 units, and sets `IsHostile = true` only when the attacker is the player. It is called for player attacks (several call sites) and creature attacks (`EnemyAttack.cs:389`).
- Vanilla civilian crime handling (`DaggerfallEntityBehaviour.cs:207-235`) requires `EntityTypes.CivilianNPC` and a `MobilePersonNPC` component, so it never fires for our NPCs. The mod raises crimes itself using the public `PlayerEntity.CrimeCommitted` and `PlayerEntity.SpawnCityGuards(bool)`.
- Vanilla `EnemyMotor` retreat (`EnemyMotor.cs:42-48`) is tactical back-off in private fields, not real fleeing. Flee needs its own movement.
- Other creatures only target our NPCs when the player has the Enemy Infighting setting enabled (`EnemySenses.cs:794`). Our NPC still retaliates against anything that damages it.
- `StreamingWorld.OnCreateLocationGameObject(DaggerfallLocation)` (`StreamingWorld.cs:1825`) fires when a town's GameObject is built.
- `IHasModSaveData` (`ModTypes.cs:260`) provides mod save data with FullSerializer versioning.

## 6. Architecture

Five units plus one pure-logic helper, one job each. Data flows one way: definition + stored state → spawner → brain → state store.

### 6.1 `NpcDefinition` (data)
The JSON shape and its defaults. No logic beyond defaults.

### 6.2 `DefinitionLoader`
Reads every `*.json` in `StreamingAssets/AdvancedNPCs/` at mod startup, validates, and produces a dictionary keyed by `id`. Never throws to callers; problems are logged (see §10).

### 6.3 `NpcStateStore` (implements `IHasModSaveData`)
Holds `Dictionary<string, NpcState>` keyed by NPC `id`. Provides get-or-default and update. Entries for definitions that no longer exist are kept but ignored.

### 6.4 `NpcSpawner`
Subscribes to `StreamingWorld.OnCreateLocationGameObject`. For each definition whose location matches the town's region name and location name, and whose state is not dead, spawns the NPC parented under the `DaggerfallLocation` transform. Parenting handles floating-origin shifts and automatic despawn when the town unloads.

### 6.5 `NpcBrain` (MonoBehaviour on each spawned NPC)
Owns the authoritative state machine (§7), the hostility guard, wandering and fleeing movement, the calm-down timer, and crime reporting. Writes changes to `NpcStateStore` as they happen and on `OnDestroy`.

### 6.6 `HostilityRules` (pure C#, no Unity dependencies)
Decision functions used by the brain so they can be unit-tested:
- `Decide(bravery, healthFraction, fleeThreshold) → Fight | Flee`
- `NewCalmDeadline(now, minHours, maxHours, random) → deadline`
- `IsCalmDue(now, deadline) → bool`
- `ClassifyHostileFlip(wasHostile, isHostile, healthDropped, targetIsPlayer) → PlayerAttack | EngineSweep | None`

## 7. Behaviour

### 7.1 States
`Calm`, `Fighting(target)`, `Fleeing(threat)`, `Dead`.

### 7.2 Calm
- `IsHostile = false`.
- `EnemyMotor` disabled; the brain's simple mover wanders to random points within `wanderRadius` of the spawn position, pausing between moves. The mover applies gravity and uses the `CharacterController`.
- Hostility guard: each frame the brain compares `IsHostile` against its own state. If `IsHostile` turned true while the brain says Calm, it is classified with `ClassifyHostileFlip`:
  - health dropped this frame, **or** `senses.Target` is the player → **player attack** (§7.3).
  - otherwise → **engine sweep** → reset `IsHostile = false` and clear any target.

### 7.3 Attacked
- **By the player:** `IsHostile = true`; set calm deadline; raise assault if `crimeOnAttack` and the NPC was Calm (§7.7); choose Fight or Flee by bravery.
- **By another creature** (health dropped, `IsHostile` unchanged): target is `senses.Target`; `IsHostile` stays false; choose Fight or Flee by bravery. No crime.
- **Bravery:**
  - Coward → Flee.
  - Normal → Fight; switch to Flee when health fraction falls below `fleeHealthPercent`.
  - Brave → Fight until death.

### 7.4 Fighting
`EnemyMotor` and `EnemyAttack` enabled; vanilla combat runs. The brain keeps checking bravery each frame (Normal NPCs may switch to Flee). If the target is another creature and it dies or leaves sensing range, return to Calm.

### 7.5 Fleeing
- `EnemyMotor` and `EnemyAttack` disabled.
- The simple mover runs directly away from the threat.
- When at least 30 units away and out of the threat's line of sight, stop and hold (cower). If the threat closes within that range again, resume fleeing.
- No healing.
- Fleeing from another creature ends (→ Calm) when that creature dies or leaves sensing range.

### 7.6 Calming down (player hostility)
- Deadline stored as in-game time: `now + random(minHours, maxHours)` from `calmDownHours`.
- Every new player hit resets the deadline.
- Checked on spawn and every few seconds while active. Because it is in-game time, resting or travelling elsewhere counts.
- On calm: `IsHostile = false`, clear target, return to Calm and wander.

### 7.7 Crime (`crimeOnAttack == true`, attacker is the player)
- First player hit while the NPC is Calm → `CrimeCommitted = Crimes.Assault` + `SpawnCityGuards(true)` (same calls vanilla uses for civilians).
- Further hits while already hostile → no additional assault.
- Killed by the player → `CrimeCommitted = Crimes.Murder` + `SpawnCityGuards(true)`.

### 7.8 Dead
Vanilla corpse and loot behaviour. State store marks `dead = true`; the spawner never spawns it again.

## 8. NPC definition format

Location: `<DFU>/DaggerfallUnity_Data/StreamingAssets/AdvancedNPCs/*.json`, one NPC per file.

```json
{
  "id": "daggerfall_city_bram",
  "name": "Bram the Cooper",
  "location": { "region": "Daggerfall", "place": "Daggerfall" },
  "position": [123.4, 2.1, -56.7],
  "baseClass": "Spellsword",
  "gender": "Male",
  "bravery": "Normal",
  "fleeHealthPercent": 25,
  "calmDownHours": [6, 48],
  "crimeOnAttack": true,
  "wanderRadius": 8
}
```

| Field | Required | Default | Rules |
|---|---|---|---|
| `id` | yes | — | Unique across all files. Lowercase letters, digits, underscore. Key for save data. |
| `name` | yes | — | Display name. |
| `location.region` | yes | — | Region name as DFU reports it. |
| `location.place` | yes | — | Location name as DFU reports it. |
| `position` | yes | — | `[x, y, z]` relative to the town's origin. Produced by `anpc_pos`. |
| `baseClass` | no | `Spellsword` | A human class `MobileTypes` name (stats and sprites). Monsters rejected. |
| `gender` | no | random | `Male` or `Female`. |
| `bravery` | no | `Normal` | `Coward`, `Normal`, `Brave`. |
| `fleeHealthPercent` | no | `25` | 1–99. Used by `Normal` only. |
| `calmDownHours` | no | `[6, 48]` | Two numbers, `0 < min <= max`. |
| `crimeOnAttack` | no | `true` | |
| `wanderRadius` | no | `8` | 0 means stand still. |

This format is expected to grow (user note); new fields must be optional with defaults so old files keep working.

## 9. Spawning and saving

### 9.1 Spawning
1. On `OnCreateLocationGameObject`, read the location's region and name, look up matching definitions.
2. For each alive NPC: `CreateEnemy(name, baseClass, position, gender, parent: location transform, MobileReactions.Passive)`, then add `NpcBrain` and initialise it from definition + stored state (including restoring health and hostility).
3. The NPC must be excluded from vanilla enemy serialization so a save/load does not create a duplicate. The mechanism (removing the `SerializableEnemy` component, or clearing its load ID) is verified as an early plan task.
4. Ground alignment risk: terrain may not be ready at creation. Fallback: use the stored `y` from the definition, then re-run ground alignment one frame later.

### 9.2 Saving
- `NpcState`: `{ dead: bool, hostile: bool, hostileUntil: in-game seconds, healthFraction: float 0-1 }` (fraction because DFU re-rolls max health, scaled by player level, on every spawn; full health restored on calm-down).
- Save data class is versioned with FullSerializer's `fsObject` attribute.
- No stored entry → fresh: alive, calm, full health.
- On load, live NPCs in the current town are re-initialised from the restored store.

### 9.3 Authoring aid
Console command `anpc_pos` prints the player's current region, location name, and position relative to the town origin, formatted as JSON ready to paste into a definition.

## 10. Error handling

The mod never stops because of bad data. Every problem produces one `Player.log` line prefixed `[AdvancedNPCs]`, naming file, field, and problem.

| Problem | Result |
|---|---|
| Invalid JSON or missing required field | That file skipped, error logged. |
| Invalid enum or out-of-range value | That file skipped, error logged with the bad value. |
| Duplicate `id` | First loaded wins (alphabetical file order), duplicate logged. |
| Location never matched during play | Warning logged once per definition, the first time the player enters that region. |
| Spawn failure at runtime | Logged; that NPC skipped; others unaffected. |
| Missing `AdvancedNPCs` folder | Info line, mod idles. |

## 11. Project layout

```
Assets/Game/Mods/AdvancedNPCs/
  AdvancedNPCs.dfmod.json        mod manifest (Mod Builder)
  Scripts/
    AdvancedNpcsMod.cs           entry point: [Invoke] startup, wiring, console command
    Data/NpcDefinition.cs
    Data/DefinitionLoader.cs
    Data/NpcStateStore.cs
    Runtime/NpcSpawner.cs
    Runtime/NpcBrain.cs
    Logic/HostilityRules.cs
  Editor/Tests/                  NUnit edit-mode tests, not shipped
  Examples/bram.json             sample NPC
```

Mod scripts ship as source in the `.dfmod` and are compiled by DFU at load. `Logic/` and the data classes avoid Unity types where possible so they can be unit-tested.

## 12. Testing

1. **Edit-mode unit tests** (Unity Test Framework 1.1.31, already in `Packages/manifest.json`):
   - Definition parsing, defaults, each validation error in §10.
   - `HostilityRules.Decide` for each bravery at several health fractions.
   - Calm deadline creation, reset on re-hit, `IsCalmDue` across long gaps.
   - `ClassifyHostileFlip` truth table.
   - State store save-data round trip.
   - **Risk:** the project has no assembly definitions, so test access to mod code must be confirmed first. This is the first plan task.
2. **Smoke run** in `F:\_Projects\Dagerfall\DFU_testing`: build `.dfmod`, install with `bram.json`, launch, check `Player.log` for `[AdvancedNPCs]` lines and errors.
3. **Manual checklist:** the eight success criteria in §2, plus Coward and Brave variants of the sample NPC.

## 13. Risks and items to verify early

| Risk | Mitigation |
|---|---|
| Tests can't reference mod code without asmdefs | First plan task: prove one test compiles and runs; adjust layout if needed. |
| Vanilla save system duplicates spawned NPCs | Early task: confirm and disable vanilla serialization for our NPCs. |
| Ground not ready at spawn | Stored `y` + delayed re-alignment. |
| Disabling `EnemyMotor` breaks something (gravity, senses) | Simple mover applies gravity; early in-game test of Calm wander. |
| Attacker classification wrong in edge cases (e.g. player hits NPC already in melee with a creature) | Rule: `IsHostile` only flips true for player attacks; any flip with a health drop is the player. Covered by unit tests and manual check. |
| Engine sweep and a real player attack in the same frame on different NPCs | Each NPC classifies independently from its own health drop. |
| Flee movement snags on walls | Accepted for 1a; pathfinding in a later step. |

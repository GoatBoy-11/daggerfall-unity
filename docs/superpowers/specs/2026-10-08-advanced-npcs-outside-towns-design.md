# Advanced NPCs — spawning outside towns (design)

Date: 2026-10-08. Approved in conversation section by section (format, placement, the rest), with one change:
`anpc_spawn` also works inside dungeons and buildings. Builds on the v2 spec (generic templates, same-people /
random modes, keys) and the C1 dialogue spec (`when` conditions).

## 1. Summary

Generic templates can appear in **dungeons**, in **interiors** (taverns, guild halls, temples, shops, …) and in the
**wilderness**, besides towns. Each context has its own block under `spawn` in `npc.json` with a chance out of 100,
a count, filters and an optional `when` (the dialogue conditions). Any template can go anywhere; its attitude
decides how it behaves (a hostile bandit attacks, a calm pilgrim talks). Dungeons and interiors follow the existing
"Same people every visit" / "Random each visit" setting; wilderness encounters are never saved. `anpc_spawn` can
place permanent people inside dungeons and buildings. `anpc_here` explains what spawned where and why.

## 2. Goals and success criteria

1. A template with only the old town fields behaves exactly as before; old saves load unchanged.
2. `"dungeons": { "chance": 30, "count": [1, 3], "dungeonTypes": ["HumanStronghold"] }`: on entering a matching
   dungeon, the template appears with 30 % chance, as a group of 1–3 standing together, never within 20 m of the
   entrance, on walkable floor, not in walls.
3. `"interiors": { "buildings": ["Tavern", "Fighters Guild"], "chance": 50, "count": [1, 2] }`: on entering a
   matching building, likewise, never within 2 m of a door. Guild halls are told apart by their guild.
4. `"wilderness": { "chance": 10, "max": 2 }`: while the player is outside every location and not resting or
   travelling, every 10 in-game minutes the chance is rolled; on success one person appears out of view 40–80 m
   away on dry ground. At most `max` of the template (and the setting's total) are alive around the player; they
   despawn when far behind and out of view, on entering a location, or on fast travel.
5. `when` (any spawn block) uses the C1 dialogue conditions; `asked`, `notAsked`, `tone`, `reaction` are
   conversation-only and warn there.
6. Same-people mode: the same dungeon / building always has the same people (or none) at the same spots; a killed
   one stays dead. Random mode: re-rolled on every entry, nothing saved.
7. `anpc_spawn <template>` inside a dungeon or building places a person there for the rest of the game (saved);
   in the wilderness it refuses with a message.
8. `anpc_here` lists, for the current dungeon / building / wilderness, each template that could appear and the
   outcome (filter mismatch, failed `when` key, chance roll, spawned count). Every data problem is one
   `[AdvancedNPCs]` line naming file and field.
9. Settings: Dungeons / Interiors / Wilderness switches (default on); "Max wilderness ANPCs around you" (default 4).

## 3. File format

```json
"spawn": {
  "locationTypes": ["TownCity"],
  "count": [0, 1],
  "dungeons":   { "chance": 30, "count": [1, 3], "dungeonTypes": ["HumanStronghold", "Prison"], "when": { … } },
  "interiors":  { "buildings": ["Tavern", "Fighters Guild", "Temple"], "chance": 50, "count": [1, 2], "when": { "time": "night" } },
  "wilderness": { "chance": 10, "max": 2, "when": { "time": "night" } }
}
```

| Field | Required | Default | Rules |
|---|---|---|---|
| `dungeons.chance`, `interiors.chance`, `wilderness.chance` | yes (in its block) | — | whole number 0–100 |
| `dungeons.count`, `interiors.count` | no | `[1, 1]` | `[min, max]`, `1 <= min <= max <= 10` |
| `dungeons.dungeonTypes` | no | all | DFU `DFRegion.DungeonTypes` names: Crypt, OrcStronghold, HumanStronghold, Prison, DesecratedTemple, Mine, NaturalCave, Coven, VampireHaunt, Laboratory, HarpyNest, RuinedCastle, SpiderNest, GiantStronghold, DragonsDen, BarbarianStronghold, VolcanicCaves, ScorpionNest, Cemetery (case and spaces ignored) |
| `interiors.buildings` | yes | — | building types `Alchemist`, `Armorer`, `Bank`, `Bookseller`, `ClothingStore`, `FurnitureStore`, `GemStore`, `GeneralStore`, `Library`, `PawnShop`, `WeaponSmith`, `Temple`, `Tavern`, `Palace`, `House` (any house), `GuildHall` (any guild) and guild names `Fighters Guild`, `Mages Guild`, `Thieves Guild`, `Dark Brotherhood`, `Knightly Order` (case and spaces ignored) |
| `wilderness.max` | no | `1` | 1–10 alive around the player at once |
| `when` | no | always | C1 conditions without `asked`, `notAsked`, `tone`, `reaction` |

- `locationTypes` is optional when a context block exists: a template with only `dungeons` never appears in towns
  (`locationTypes` defaults to the three town types only when no context block is given — the old behaviour).
- A block with problems is skipped with an error naming file and field; the template's other blocks still work.
- Unknown fields warn with a "did you mean" hint (as in C1).
- **Chance and count:** `chance` decides whether the template appears in this place at all (one roll per visit);
  `count` decides how many then. Wilderness rolls once per 10 in-game minutes and spawns one person per success.

## 4. Engine facts (verified in DFU source, to be confirmed by the spike)

- `PlayerEnterExit.OnTransitionInterior` / `OnTransitionDungeonInterior` (static events, `TransitionEventArgs`),
  `OnTransitionExterior` / `OnTransitionDungeonExterior`; `PlayerEnterExit.Interior` (`DaggerfallInterior`),
  `.Dungeon` (`DaggerfallDungeon`), `.BuildingType` (`DFLocation.BuildingTypes`), `.BuildingDiscoveryData`
  (`factionID`, `buildingKey`).
- Guild halls: `BuildingTypes.GuildHall` + faction id → `GuildManager` guild group (Fighters/Mages/Thieves/Dark
  Brotherhood/knightly orders). Temples: `BuildingTypes.Temple`.
- Dungeons: each RDB block has `Random Enemies` / `Fixed Enemies` nodes (enemies at DFU's monster markers) and
  random treasure (`DaggerfallLoot`, `LootContainerTypes.RandomTreasure`); `DaggerfallDungeon.EnterMarker` /
  `StartMarker`; dungeon type from the location's `DFLocation.Dungeon` / region data.
- Interiors: `DaggerfallInterior.Markers` (`Rest`, `Enter`, `Treasure`, ladders), the `Interior Flats` node with
  DFU's people (`StaticNPC`).
- Wilderness: `PlayerGPS` knows whether the player is inside a location's rect; terrain is hit by a downward ray
  on the terrain layer; water from the terrain tile data or the ray hitting below the water line.

## 5. Placement

**Spots** come from the context's markers; a person stands about 1.5 m (dungeons) or 1–2 m (interiors) from the
marker's position, at a point found by a downward floor ray with a capsule clearance check (the `anpc_spawn` wall
check). If no offset works, the marker point itself is used; if it is blocked too, the spot is skipped.

- **Dungeons:** spots = positions of DFU's random/fixed enemies and random treasure, collected after layout, sorted
  by block and position (stable across visits). Spots within 20 m of the enter marker are dropped. A group stands
  together: first person on the chosen spot, the rest within 2–3 m.
- **Interiors:** spots = `Rest`, `Treasure` and `Enter` markers plus DFU's people in the building; spots within 2 m
  of a door (enter marker) are dropped. The existing "Max generic ANPCs per town" setting also caps each interior.
- **Wilderness:** 8 tries for a point 40–80 m from the player, outside the camera's view cone (angle > 70° from the
  view direction or behind cover), terrain hit, not water, slope < 35°; no spot → no spawn this roll.
- Seeds: in same-people mode, spot choice comes from the place key's seed (stable); in random mode from a fresh seed.

**Fallback (if the spike shows marker spots are unreliable):** sample the floor on a 2 m grid inside the
interior/dungeon block bounds with downward rays and clearance checks, then pick from those.

## 6. Identity, state, saving

| Context | Place key | Person key | Saved |
|---|---|---|---|
| Dungeon | `<mapId>:dungeon` | `template@<mapId>:dungeon#n` | same-people mode |
| Interior | `<mapId>:b<buildingKey>` | `template@<mapId>:b<buildingKey>#n` | same-people mode |
| Wilderness | — | `template@wild#<counter>` | never |
| `anpc_spawn` indoors | as above | `template@<mapId>:dungeon+n` / `template@<mapId>:b<key>+n` | always |

- Same-people mode: the chance roll, count, names, faces and spots come from `StableHash(person or place key)`;
  `NpcState` (dead, hostile, asked, …) is saved as for town people.
- Random mode: a fresh seed per entry; `Persistent = false`.
- `PlacedNpc` gets `context` (`""` = town outdoors, `"dungeon"`, `"b<buildingKey>"`); missing in old saves = town,
  so existing keys stay `template@mapId+n`. Its position is relative to the dungeon's / interior's transform.
- People are parented to the dungeon / interior object and despawn when it is destroyed (exit, load, fast travel).

## 7. Behaviour

Unchanged from towns: attitude, hostile hours, bravery, calm-down, crimes, sprites, portraits, dialogue. Crimes
inside a dungeon follow DFU's rules (no guards). A hostile template in a dungeon or the wilderness is an enemy from
the start. Wandering: `wanderRadius` applies around the spawn spot; indoors it is capped at 3 m so people stay in
their room.

## 8. Code structure

Core (NUnit): `SpawnBlocks` (model + parsing of `dungeons`/`interiors`/`wilderness` inside `DefinitionParser`),
`PlaceInfo` (kind, map id, region, place, dungeon type, building type, guild, building key),
`OutOfTownPlanner` (eligible templates, chance and count by seed, keys, `Explain` lines for `anpc_here`),
`SpotPicker` (seeded choice, entrance distance, grouping, spacing), `EncounterRules` (ring point, view test,
roll timing, caps). `PlacedNpc.context`.

Runtime: `InteriorSpawner`, `DungeonSpawner` (enter/exit events, spot collection, spawning through the existing
person-spawning code), `WildernessSpawner` (MonoBehaviour timer, terrain ray, despawn), `GameFacts` reused for
`when` (no conversation), settings, `anpc_here`, `anpc_spawn` indoors.

## 9. Testing

Core NUnit for parsing, planning, spot picking and encounter rules. In-game self-test: enter a building and a
dungeon through DFU's own transitions where scripted entry is reliable (decided by the spike; otherwise extra test
saves made once: inside a tavern, inside a dungeon, in the wilderness), check counts, spacing, entrance distance,
same-people repeat, wilderness ring and despawn. LOOK screenshots: a group in a dungeon, a patron in a tavern.

## 10. Plan order

1. **Spike (throwaway):** marker spots in a crypt, a stronghold, a tavern, a guild hall, a temple; scripted entry
   for the self-test. Decide markers vs grid fallback and test-save needs.
2. Core: parsing, planner, spot picker, encounter rules.
3. Runtime: interiors, dungeons, wilderness, `anpc_spawn` indoors, `anpc_here`, settings.
4. Self-test, example template, README; fresh review.

## 11. Non-goals

Wilderness "placed on the land" mode; quest-dungeon special handling; ANPCs following the player between places;
interior schedules (who is in the tavern at which hour, beyond `when`); dungeon spawns tied to specific rooms.

# Advanced NPCs — outside-towns spike: findings

Date: 2026-10-08. Plan Task 0. Throwaway code on branch `spike/outside` (commit 4006bea77,
`Scripts/Runtime/OutsideSpike.cs`, run instead of the self-test from the AtDaggerfall save).

## Results

| Place | Candidate points | Usable (1.5 m offset, floor ray, capsule, no wall between) | People stood on floor |
|---|---|---|---|
| Tavern (Daggerfall) | 3 (named markers only) | 2 | 2/2 |
| Temple | 3 | 2 | 2/2 |
| Fighters Guild hall | 3 | 2 | 2/2 |
| Mages Guild hall | 1 | 0 | — |
| Crypt (Castle Woodcroft) | 43 (39 random enemies, 4 treasure) | 34 of 34 beyond 20 m | 3/3 |
| Human stronghold (Privateer's Hold) | 50 (17 random, 25 fixed enemies, 8 treasure) | 47 of 48 beyond 20 m | 3/3 |

## Answers

1. **Timing:** `OnTransitionInterior` and `OnTransitionDungeonInterior` fire synchronously at the end of the
   transition call, in the same frame; markers, enemies and treasure all exist then. Spawn in the event handler.
2. **Interior points are too few with only Rest/Treasure/Enter.** Interiors carry many more editor markers whose
   types DFU's enum does not name (records 2, 3, 11, 17, 18, 20 …; the Mages Guild hall had 25 markers). **Use every
   editor marker except LadderBottom/LadderTop**, plus DFU's people (`StaticNPC`, **including inactive ones**: DFU
   deactivates them when the building counts as closed, which is why the spike counted 0).
3. **Building type and guild:** `PlayerEnterExit.BuildingType` is right; `BuildingDiscoveryData.factionID` was 0 for
   a Fighters Guild hall at event time. Read the faction from the town's `BuildingDirectory.GetBuildingSummary(
   door.buildingKey)` (the town object is still there at the event) and map it with
   `GuildManager.GetGuildGroup(factionId)`; fall back to the discovery data.
4. **Dungeon entrance:** the crypt had no enter marker; use the player's position at the event (start or enter
   marker) as the entrance for the 20 m rule. Dungeon identity: `DaggerfallDungeon.Summary` (`ID` = map id,
   `DungeonType`, `RegionName`, `LocationName`).
5. **Scripted entry works** — no extra test saves needed: interiors with `PlayerEnterExit.TransitionInterior(
   doorCollection.transform, door)` using the town's `StaticDoorCollections` (doorType Building) and back with
   `TransitionExterior()`; dungeons with `StartDungeonInterior(DFLocation)` (any dungeon of the region, from
   `PlayerGPS.CurrentRegion.MapTable` + `ContentReader.GetLocation`) and back with `TransitionDungeonExterior()`,
   which returns to the town the player came from.
6. **Wilderness:** `PlayerGPS.IsPlayerInLocationRect` and `GameManager.IsPlayerOnHUD` exist (rest, travel and other
   windows make it false: a simple "no rolls" test). A downward ray from the player hits the player's own collider
   (layer `Player`, 13): exclude that layer. Water detection is settled in Task 6.
7. **Screenshots:** setting the player's transform directly put the camera inside geometry; LOOK shots in Task 8
   must move the player through the CharacterController (disable, move, enable) or look from the entrance.

## Decision

Markers (not the grid fallback), with the widened interior set. Scripted entry for the self-test.

# Advanced NPCs: tools for authors, quality and maintenance — design

Date: 2026-10-12. Status: decided autonomously (user AFK: "After that you can work on the Tools for Authors and
Quality and Maintenance entries"); every call below is listed in the final report for the user to veto.

## Intent

The "Tools for authors" and "Quality and maintenance" sections of the ideas list
(`docs/superpowers/ideas/2026-10-12-advanced-npcs-ideas.md`). Success: an author can inspect any ANPC in game, get
editor autocompletion and validation for their JSON, and see sprite edits without restarting; the self-test passes
reliably; the known review minors are fixed; saves carry a format version.

## A. Tools for authors

### A1. `anpc_info [id or key]`

The ANPC in front of you (same pick as `anpc_topics`), or the one named. Prints one line per fact:

- id, key, display name, kind (unique / generic), folder
- mode (calm / fighting / fleeing / dead), enemy or not, hostile until (if hostile), bravery, health
- opinion (current, after drift) and DFU's regional reaction
- sprite set: label, `loaded` / `loading` / `vanilla` (no set), height in metres, current animation
- DFU target (none / player / name) and whether in sight, distance to you
- LOD: update interval (1, 3 or 8)

No ANPC found: "No Advanced NPC in front of you (or give an id)."

### A2. JSON schemas

- `Schemas~/npc.schema.json`, `Schemas~/sprites.schema.json`, `Schemas~/dialogue.schema.json` (JSON Schema
  draft-07). `~` keeps Unity from importing them; the README says to copy them into `ANPCs/_Schemas/` (underscore
  folders are already skipped) and add `"$schema": "../_Schemas/npc.schema.json"` to a file.
- Every parser accepts a `"$schema"` member without the "unknown field" warning (in `FieldReader.UnknownKeys`).
- `Tools~/check-schemas.py`: each schema is valid JSON; its top-level properties are exactly the parser's known keys
  (read from `DefinitionParser.cs`, `DialogueParser.cs`, `SpriteSet.cs` source); every example file validates when the
  `jsonschema` module is installed (skipped with a note otherwise).

### A3. `anpc_reload_sprites [id]`

Re-indexes the sprite sets of one ANPC folder (or every folder) from disk and swaps them in:

- Live ANPCs of that folder that wear custom sprites re-pick by seed (`SpriteStates.PickSet`) from the new sets and
  show the new set as soon as it has loaded (the vanilla picture stays hidden meanwhile, as at spawn).
- A folder that now has no valid set: its live ANPCs show the vanilla sprite.
- ANPCs that wore the vanilla sprite keep it until they next spawn; walk/run/greet abilities and `spriteScale` /
  npc.json changes also need a respawn (stated in the command's reply).
- Old sets leave the library; corpses keep them until the corpse goes (their textures are then destroyed).
- Reply: "Reloaded N sprite set(s) in M folder(s); K ANPC(s) updated."

## B. Quality and maintenance

### B1. Flaky self-tests

The magic AI caster and the town-edge caster are placed 8 m from the player along `OpenDirection` (clear line,
ground under), every leftover self-test ANPC is discarded first, and the failure detail prints the DFU senses
(target, in sight, distance). Goal: three consecutive full self-test runs with no failures.

### B2. Review minors fixed

1. The "no X.png for combat.rangedAnimation" log checks the set's indexed animations, not the loaded sheets (false
   message at every spawn of a still-loading set).
2. Patrol: inactive ANPCs (switched-off towns) are no foes; the collider buffer grows (64 → up to 512) when full.
3. Patrol primes DFU's senses: `senses.Target` is the foe when the attack starts, so the ANPC does not stand
   waiting for DFU to notice it.
4. A dead ANPC releases its sprite set when the corpse takes over (`NpcSprite.Hide`); the corpse holds its own.
5. A greeting ANPC that turns hostile without being hit drops the greet pose at once.
6. The greeter holds still for the length of the greet sheet actually played, not the first one; a greet that did
   not start does not hold still.
7. Walk/run sheets present in sprites.json but all skipped (bad size, unreadable) log that the ANPC will stand still
   (turret), instead of silently becoming one.
8. Sprite LOD "busy" also covers fighting and fleeing (the opinion-lod spec says so).
9. The greeter forgets who it greeted when the player is beyond 30 m (it no longer runs there, so it never reset).
10. "Helped" (+10 opinion) also counts when the ANPC was fleeing from the creature that died.
11. Opinion drift keeps partial days: a change moves `opinionAt` forward only by the whole days already drifted
    (frequent small changes no longer stop the drift); drifting to 0 stores 0.
12. Gait: the first frame after a skipped LOD gap does not count the gap as one frame's movement (no run burst).
13. README: the opinion/LOD paragraphs move out of the console command list; `anpc_info`,
    `anpc_reload_sprites` and schemas are documented. `GameFacts` loses its stacked doc comment.

### B3. Save format version

- `NpcSaveData.Version` (int). Current = 1; old saves load 0 and are read exactly as today.
- `SaveFormat` (Core, pure): `Current = 1`, `Check(int version) : string` — null when known (0 or 1), else a warning
  "save written by a newer Advanced NPCs (format N); unknown data is ignored".
- `GetSaveData` writes `Current`; `RestoreSaveData` logs `Check`'s warning when it is not null.

## C. Testing

- Core unit tests: `SaveFormat.Check`; `"$schema"` is not unknown; opinion drift partial days (`OpinionRules`).
- In-game self-test: `anpc_info` on a test ANPC names its id, mode and sprite state; `anpc_reload_sprites` on the
  self-test sprite folder swaps a live ANPC to the re-indexed set; a dead ANPC's sprite set has one user fewer;
  hostile-without-hit drops the greet pose; patrol ignores an inactive foe.
- `Tools~/check-schemas.py` passes.

## Deferred (rulings)

- A set removed from the library while loading never unloads (only the self-test removes sets).
- `NpcBrain.All()` allocates per greeter check (small; LOD already cut the calls).
- A possible lost motor tick on slopes (not reproduced).
- Hit then kill costs a witness −20 (two separate acts seen; stands).
- `anpc_opinion` only reaches spawned ANPCs (the state of an unspawned one is keyed by instance, not id).
- Pursuit timer after a creature fight: already zeroed every calm frame, and a dead target cannot re-trigger.

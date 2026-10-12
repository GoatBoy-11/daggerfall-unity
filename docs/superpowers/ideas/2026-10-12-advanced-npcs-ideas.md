# Advanced NPCs — ideas and improvements (brainstorm, 2026-10-12)

Written autonomously after the run/greet/turret, on-demand sprite, combat and height work. Grounded in what the mod
already has (unique/generic ANPCs, custom 8-direction sprites, dialogue topics + replies with actions, spellcasting,
combat tuning, bravery, patrols, greetings, spawning in towns/interiors/dungeons/wilderness) and in the gaps seen
while building it. Rough size: **S** (an evening), **M** (a feature like run/greet/turret), **L** (multi-part).

## Top five (my recommendation)

1. **Daily routines (M–L).** `"schedule"` in npc.json: where an ANPC is and what it does by hour (`"8-18": work at
   the smithy door`, `"18-23": tavern`, `"23-8": home (despawn)`). Towns would feel alive: the market fills in the
   morning, the tavern at night. Builds on the existing spawner (interiors, building kinds) and wander code.
2. **Companions (M).** A reply action `"hire"` (gold per day) that turns an ANPC into a follower: follows the player,
   fights on the player's side (DFU's `PlayerAlly` team), waits/leaves on command through dialogue. Uses the
   existing reply/action system, gait (runs to catch up) and bravery (a Prudent companion flees!).
3. **Personal opinion of the player (M).** A per-ANPC opinion (saved) changed by gifts, witnessed crimes, quests,
   being hit, conversation tone. Drives greetings (today reaction-based), dialogue conditions, prices, willingness
   to help. Today "opinion" is the region's reputation only; this makes each person remember you.
4. **Barks (S–M).** Short lines near the ANPC (or in the HUD message line) at moments the code already knows:
   greeting, being hit, fleeing, a brave guard spotting an enemy, calming down. Authored in the existing `_Dialogue`
   files (`"barks": { "greet": [...], "flee": [...] }`).
5. **Distance LOD for updates (S).** Brains, gait and greeters of ANPCs far from the player update every few frames
   instead of every frame. Cheap, and keeps big towns smooth as more ANPCs are installed.

## Behaviour and AI

- **Archer kiting (M).** With `rangedAtDistance`, keep 6–12 m from the target, back off when it closes in, then
  shoot (DFU's motor only approaches). The `attack_range` sheet already exists.
- **Blocking (S–M).** A `block*` sheet played when DFU's parry roll succeeds, for sword-and-shield ANPCs.
- **Surrender (M).** A badly hurt Normal ANPC sometimes yields instead of fleeing (`yield*` sheet); talking to it can
  spare it (reputation +) or rob it (crime).
- **Morale spreading (S).** When an ally flees or dies nearby, Normal/Prudent ANPCs get a higher flee chance for a
  while; a Brave leader nearby lowers it.
- **Patrol routes (M).** Guards walk a loop of points instead of wandering; brave patrols already attack on sight.
- **Witnesses (M).** Calm ANPCs who see the player commit a crime remember it (opinion −, may fetch a guard).
- **Reactions to the player's state (S–M).** Fear of a known vampire/werewolf, comments on a bloodied or naked
  player, rank-aware greetings for guild members.
- **Sitting and leaning (M).** `sit*`/`lean*` sheets with spots found near benches, bars and walls.

## Content and world

- **Merchants (M–L).** An ANPC with a small stock (`"shop"`) opens DFU's trade window from a reply.
- **ANPC quests (M).** Let an ANPC be a quest giver/target with its own lines, beyond `startQuest`.
- **Weather and night (S).** ANPCs head indoors in heavy rain/snow or late at night unless their schedule says not.
- **Groups (M).** Generic spawn of a small party (leader + followers) that moves together; bandit camps.

## Sprites and the art pipeline

- **Crop empty margins (S).** Trim each cell to the character (keeping the feet row and centre): less memory/disk.
- **Async PNG decode (M).** Decode sheets on a worker thread, upload on the main thread: no per-sheet hitch.
- **One-command re-render (S).** render → anchors → effects → install per character, with absolute paths built in
  (a relative output path makes Blender write images under `C:\` while sprites.json lands in the project).
- **`death_static` generator (S).** Make the billboard corpse picture from the last death frame automatically.
- **Height check in the pipeline (S).** Measure the body against Daggerfall's own sprites (the vanilla Warrior's
  figure fills ~97% of its frame) and suggest `spriteScale`, so new characters match without manual measuring.

## Tools for authors

- **`anpc_info` console command (S).** The ANPC in front of you: id, state, bravery, sprite set, height, target.
- **JSON schemas (S).** For npc.json / sprites.json / dialogue files: editor autocompletion and validation.
- **Hot reload of sprites (S–M).** Re-index and reload changed sheets without restarting the game.

## Quality and maintenance

- **Fix the flaky self-tests (S).** The "magic town caster past the town edge" pair (and sometimes "magic AI casts")
  still fail intermittently; likely placement/line-of-sight like the other fixed tests.
- **Deferred minors from reviews (S each).** False `rangedAnimation` "no X.png" log at spawn; patrol scan buffer of
  64 colliders; patrol not priming DFU senses; pursuit timer left set after a creature fight; a dead ANPC keeping its
  sprite set until its town unloads; patrols seeing ANPCs in inactive towns; greet pose lingering ~1 s when turned
  hostile without a hit; greet hold time using the first greet sheet; broken walk/run sheets silently making a turret.
- **Save format versioning (S).** A version number in the mod's save data so later changes can migrate old saves.

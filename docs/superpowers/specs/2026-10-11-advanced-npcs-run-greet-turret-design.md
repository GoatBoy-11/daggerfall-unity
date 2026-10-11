# Advanced NPCs: running, greetings and turrets — design

Date: 2026-10-11. Status: approved approach (1: boost on top of DFU's motor), awaiting spec review.

## Intent

The user's playtests show ANPCs that walk everywhere and stand blank-faced next to friends. Three additions make
them feel alive without making combat unfair:

1. **Run.** A fighting ANPC that sees its target far away runs until it is close; a fleeing ANPC runs away.
   Running is only slightly faster than walking, so the player can still escape or catch up.
2. **Greet.** An ANPC with a `greet*` sheet now and then greets a player or ANPC it likes, when close and in sight.
3. **Turret.** A sprite set with neither walk nor run sheets never moves: a static guard, archer or caster.

Success: in game, a chasing barbarian visibly switches from walk to run at range and back to walk near you, a
fleeing citizen runs, a friendly guard sometimes stops, turns and greets you, and an idle-only archer never leaves
its spot. Existing behaviour of ANPCs without these sheets is unchanged except chase/flee speed.

## User decisions (2026-10-11)

- "Positive opinion": toward the **player**, the ANPC is calm (not hostile, not an enemy) and DFU's reaction to the
  player is `likes` or `loves` (the same value dialogue `"reaction"` conditions use: the region's people faction,
  `TalkManager.GetReactionToPlayer`, banded by `ConditionParser.ReactionBand`). Toward **another ANPC**: both are
  calm and on the same side (neither hostile nor an enemy, neither fighting nor fleeing).
- Run speed: **1.2 × the ANPC's own walk speed** by default; npc.json `"combat": { "runSpeed": x }` overrides it
  per ANPC, x from 1 to 2.
- Can run: npc.json `"combat": { "canRun": false }` turns running off for one ANPC: it chases and flees at its
  walk speed and shows walk sheets. Default `true` (leaving it out lets the ANPC run). (User asked for a "can_run"
  setting; camelCase inside `combat` matches the other keys.)
- Greeting: within about 4 m and in sight, a **25 % chance per encounter**; the ANPC stops wandering, turns to face
  the target, plays `greet` once, then carries on; it will not greet the same target again for **2 game hours**.
  The other party may greet back on its own roll.
- Approach 1: DFU's `EnemyMotor` keeps driving the chase; the mod adds the extra speed on top of it.

## Definitions

- **Walk speed** of an ANPC: DFU's enemy formula `(Stats.LiveSpeed + 150) / 40` m/s (≈ 5 m/s), the speed
  `EnemyMotor` chases at.
- **Run speed**: walk speed × `runSpeed` (default 1.2).
- **Gait**: what the ANPC's legs do this frame: `Still`, `Walk` or `Run`.
- **Turret**: an ANPC whose sprite set has no `walk*` and no `run*` sheet. ANPCs without a custom sprite set are
  never turrets.

## 1. Sprite sheets

- New states, recognised by `SpriteStates.StateOf`: `run` (`run`, `run_1`, ...) and `greet` (`greet`, `greet_1`, ...).
  Today these sheets are skipped at load; afterwards they load like the others. (`talk*`, `pray*` stay skipped.)
- Fallbacks (`SpriteStates.Variants`):
  - `run` → `walk` → `idle`.
  - `walk` → `run` → `idle` (a set with run but no walk walks with run).
  - `greet` has **no** fallback: no greet sheet, no greeting.
- `SpriteStates.IsStationary(set)`: true when the set has no `walk*` and no `run*` sheet.

## 2. Gait rules (Core, pure: `GaitRules`)

- `Decide(mode, targetInSight, distance, wasRunning, turret, canRun) → Gait`:
  - turret → `Still` (the sprite shows idle, attack, cast, hit as usual).
  - `canRun` false → never `Run`; every case below that would run gives `Walk` instead.
  - Fighting: `Run` when the target is in sight and `distance > RunStart (6 m)`; keeps running until
    `distance < RunStop (4 m)` or the target is out of sight (hysteresis so it does not flicker); otherwise
    `Walk`, and the sprite still shows idle when the measured speed says it stands (as today).
  - Fleeing: `Run` while it moves away; `Still` when cowering.
  - Calm: `Walk` while wandering, `Still` otherwise (unchanged).
- `RunSpeed(walkSpeed, runSpeed)` = walkSpeed × runSpeed.
- `RunSpeed(walkSpeed, runSpeed, canRun)` = walkSpeed when `canRun` is false.
- `"combat.runSpeed"` parsed by `CombatRules.Read`: number, 1–2, default 1.2; anything else is a load problem
  with key `combat.runSpeed`.
- `"combat.canRun"`: true or false, default true; anything else is a load problem with key `combat.canRun`.
  `runSpeed` with `canRun: false` is allowed (ignored).

## 3. Movement (Runtime)

New component `NpcGait` on every ANPC (added by the spawner next to `NpcMover`):

- Each frame (LateUpdate, after DFU's motor moved the controller) it asks `GaitRules` for the gait using the
  brain's mode, `EnemySenses.TargetInSight` / `DistanceToTarget`, and the sprite set.
- **Chase boost**: while Fighting with gait `Run`, it measures the horizontal distance the controller moved since
  last frame (in the town's local space, so floating-origin shifts do not count, as `NpcSprite` already does) and
  moves the `CharacterController` a further `(runSpeed − 1) ×` that distance in the same direction. Walls and
  slopes still stop it (CharacterController.Move). No boost while DFU's knockback is active
  (`EnemyMotor.KnockbackSpeed > 0`) or while an attack/spell one-shot plays.
- **Turret pin**: for a turret, it remembers its spawn spot (town-local) and moves the controller back to it every
  frame, horizontally only (gravity still works). DFU's motor still turns it, shoots, casts and swings when the
  target comes within reach; knockback does not move it.
- `Gait` is public for `NpcSprite` and the self-test.

Brain changes (`NpcBrain`):

- Fleeing uses run speed (`GaitRules.RunSpeed(walk speed, runSpeed, canRun)`, so walk speed when it cannot run) instead of the fixed `NpcMover.RunSpeed`
  (5 m/s). A turret never flees: it cowers (stops) in place, as a fleeing ANPC does when safe.
- Calm wandering is skipped for turrets (same as `wanderRadius` 0).

## 4. Animation (NpcSprite)

- `WantedState` uses the gait: `Run` → `run` sheets; `Walk` → `walk` when the measured speed says it moves, else
  `idle`; `Still` → `idle`. Attack, cast, hit and greet one-shots keep priority as today. Without an `NpcGait`
  (safety only) the old speed test stays.

## 5. Greetings

New component `NpcGreeter` on every ANPC whose sprite set has a `greet*` sheet (no sheet: no component).

- Runs only while the brain is **Calm**, the ANPC is not already greeting, and the game is not paused (a DFU window
  pauses `Update`).
- About every 0.5 s it looks for candidates: the player and every live ANPC (`NpcBrain.All()`), skipping itself
  and dead ones.
- `GreetRules` (Core, pure) decides per candidate:
  - in range: flat distance ≤ 4 m;
  - in sight: a clear line between chests (same test as the brain's `CanSee`);
  - opinion: player → this ANPC calm and reaction band `likes`/`loves`; ANPC → both calm and neither an enemy;
  - cooldown: not greeted this target within the last 2 game hours;
  - encounter: the target has just come into range (it was out of range at the previous check). The 25 % roll
    happens once per encounter, so standing next to someone does not roll again until they leave and return.
- On success: the greeter tells the brain to pause wandering (`NpcBrain.HoldStill(seconds)`, new), turns the ANPC
  to face the target (flat yaw, over ~0.25 s), and asks the sprite for a one-shot `greet` (`NpcSprite.PlayGreet()`),
  which plays from first to last frame like a hit sheet. The cooldown time is recorded per target key
  (player = `"player"`, ANPC = its key) in memory only (not saved; a reload may greet again).
- If the ANPC becomes hostile or is hit mid-greeting, the greet stops (combat sheets take over as today).

## 6. Data and docs

- npc.json: `"combat": { "runSpeed": 1.2, "canRun": true }` (new keys). Unknown-key message lists them. Not stat
  changes: `HasCombatTuning` stays false for them alone.
- sprites.json: no change; `run*` and `greet*` sheets just load now. `actionFrame` stays attack-only.
- README: sheet list (`run*`, `greet*`, fallbacks, turret rule), combat section (`runSpeed`, `canRun`), behaviour notes
  (chase run 6 m/4 m, flee runs, greetings, turrets). Mod version 0.11.0.

## 7. Testing

- Core unit tests: `StateOf` for run/greet; fallbacks (run→walk→idle, walk→run, greet none); `IsStationary`;
  `GaitRules.Decide` table (fight far/near/hysteresis/out of sight, flee, calm, turret); `RunSpeed`;
  `canRun` false never runs; `combat.runSpeed` and `combat.canRun` parse (default, range, type); `GreetRules` (distance, sight, each opinion case, cooldown,
  once per encounter, chance with a seeded random).
- In-game self-test (`anpc_selftest`):
  - a hostile ANPC with run sheets far from the player shows gait Run and the run sheet, and moves faster than
    its walk speed (measured over 1 s); near the player it is not running;
  - a fleeing ANPC runs at run speed;
  - an ANPC with `canRun: false` far from the player keeps gait Walk and its walk speed;
  - a turret (idle + attack only) does not move while hostile, yet faces and attacks;
  - a calm ANPC with a greet sheet, reaction forced to `likes`, chance forced to 1, greets the player when
    placed within 4 m, faces the player, and does not greet again within the cooldown; a `dislikes` reaction
    never greets.
- Smoke: the samples spawn; `run`/`greet` sheets load without size errors.

## Out of scope

- Talk animations (`talk*`), `pray*` and other idle extras.
- Saving greet cooldowns.
- Changing DFU's chase pathing, strafing or ranged logic.
- Running during calm wandering.

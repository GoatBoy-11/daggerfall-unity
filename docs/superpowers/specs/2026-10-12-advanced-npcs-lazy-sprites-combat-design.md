# Advanced NPCs: on-demand sprite loading, retaliation, bravery, brave patrols — design

Date: 2026-10-12. Status: approved in conversation, awaiting spec review.

## Intent

1. **Memory and startup.** Today every installed sprite set is decoded and uploaded at game start: about 11 s of
   loading and about 390 MB of GPU memory for the 14 installed sets (sheets are already DXT-compressed and released
   from CPU memory), growing with every new character. Load a set only while an ANPC (or its corpse) uses it.
2. **Retaliation on a miss.** A calm ANPC fights back when a hostile ANPC or vanilla enemy swings at it, even when
   the swing misses (today only lost health triggers it).
3. **Bravery.** New `"Prudent"` (always flees at low health: today's Normal). `"Normal"` becomes a chance to flee at
   a lower threshold.
4. **Brave patrols.** A calm `"Brave"` ANPC attacks enemies it sees nearby: vanilla DFU enemies and enemy ANPCs.

Success: the game starts without the sprite-loading wait; memory follows the ANPCs around you; a guard swung at
and missed fights back; a Normal townsperson sometimes stands its ground at low health; a brave guard engages a
bandit or an enemy ANPC that comes near.

## User decisions (2026-10-12)

- Lazy loading: spread over frames, the ANPC stays invisible until its set is ready; a set unloads 60 s after its
  last ANPC/corpse is gone. (The "drop CPU copy" fix was already in place.)
- Normal: rolls once when health first drops below **half of `fleeHealthPercent`**; npc.json `"fleeChance"` 0–100,
  **default 30** (% chance to flee). Healing back above the line allows a new roll.
- Brave patrol: npc.json `"aggroRange"`, **default 15 m**; enemy within range and in clear sight.
- Enemies for patrols: vanilla DFU enemies hostile to the player (not the city watch, not player allies) and ANPCs
  with a hostile attitude (`IsEnemy`); not ANPCs merely angry at the player.

## A. On-demand sprite loading

### Startup (index only)

- `SpriteLibrary.Load` keeps finding set folders and parsing `sprites.json`, but instead of decoding each PNG it reads
  the PNG header (signature + IHDR width/height) and runs the same size check (`SpriteSetParser.CheckSheetSize`).
  Missing, unreadable or wrong-size sheets are dropped from the set and logged as today; a set without a usable idle
  sheet is skipped as today. Non-state sheets (`talk*`, `pray*`) are skipped as today.
- The startup log line becomes `Indexed N sprite set(s) in X ms.`
- Everything that needs only names (state lookups, `IsStationary`, greet/ranged sheet checks, cell sizes) works on
  the indexed set before any texture exists.

### Loading and unloading

- `LoadedSpriteSet` gains: `IsReady` (all sheet textures and the optional `death_static.png` loaded),
  `Users` (count), and an unload deadline.
- `SpriteLibrary.Acquire(set)` adds a user; if the set is not loaded and not loading, it queues it. A loader
  (coroutine on the mod object) loads **one image per frame** (read, `LoadImage`, `Finish` = filter, clamp, compress,
  release CPU copy), then computes `StandingHeightPx` (needs the idle sheets' pixels: computed from each idle sheet
  right after `LoadImage` and before `Finish` releases the pixels), then marks the set ready.
- `SpriteLibrary.Release(set)` removes a user. When `Users` reaches 0 the set gets an unload deadline 60 s later
  (real time); a new `Acquire` cancels it. When the deadline passes, every texture is destroyed and the set returns
  to the indexed state. A set whose last user leaves while it is still loading finishes loading, then waits out its
  deadline (simple, avoids half-loaded states).
- Sets made by `SpriteLibrary.FromTextures` (self-test) are ready from the start and never unload.
- A sheet that fails to decode at load time (file changed since startup) is logged and dropped from that set's
  animations; if that leaves no idle sheet, the set is marked failed: its ANPCs fall back to the vanilla sprite.

### Users

- `NpcSprite`: the spawner acquires the chosen set. Until it is ready the sprite is **pending**: the vanilla billboard
  stays hidden (`forceRenderingOff`) and no quad is drawn; the ANPC exists, moves and fights as usual. When the set is
  ready the sprite runs today's `Init` (size, material, first frame). `OnDestroy` releases the set. A failed set:
  pending ends, vanilla billboard shown again.
- `NpcCorpseSprite`: acquires the set when the body is created (the dying ANPC still holds it, so it is ready) and
  releases it in `OnDestroy`.
- Self-test "looks" screenshots wait until their set is ready.

### Cost per frame

One sheet per frame: decode + compress of one sheet (about 1300 × 2048 px) can take tens of ms; this replaces the
single 11 s block at start. Loading starts when a town/area spawns its ANPCs, usually while DFU is loading anyway.

## B. Retaliation when missed

- DFU's `EnemyAttack.ApplyDamageToNonPlayer` calls `targetMotor.MakeEnemyHostileToAttacker(attacker)` on every
  melee swing that reaches a non-player target, hit or miss; that sets the victim's `senses.Target` to the attacker
  and `EnemyMotor.GiveUpTimer = 200`. While an ANPC is calm its motor is off and the brain zeroes `GiveUpTimer` every
  frame, so a non-zero `GiveUpTimer` with a non-player target means "something just attacked me".
- `NpcBrain.Update`, calm mode, before zeroing `GiveUpTimer`: if `HostilityRules.AttackedWhileCalm(...)` (calm,
  timer set, target not the player, target alive) → `OnAttackedByCreature(senses.Target)`. Bravery decides as
  usual (a Coward flees).
- Player attacks are unchanged (they already have their own crime/hostility path).

## C. Bravery

- `Bravery` enum gains `Prudent` (appended at the end; definitions are not saved, but appending keeps any numeric
  use stable).
- `HostilityRules.Decide(bravery, healthFraction, fleeHealthPercent, fleeChance, ref FleeRoll roll, double random01)`:
  - Coward → Flee. Brave → Fight.
  - Prudent → Flee when health% < `fleeHealthPercent` (today's Normal).
  - Normal → when health% < `fleeHealthPercent / 2`: the first time below the line, roll once
    (`random01 * 100 < fleeChance` → flee) and keep that answer while below; at or above the line → Fight and forget
    the roll (so healing and dropping again rolls again).
- `FleeRoll` is a small struct the brain keeps per ANPC (not saved: a reload may roll again).
- npc.json `"fleeChance"`: whole number 0–100, default 30; any bravery may carry it, only Normal uses it.
- Parser message: `must be Coward, Normal, Prudent or Brave`.
- Defaults unchanged: `bravery` defaults to `Normal`, so ANPCs without a `bravery` key get the new Normal. README
  tells authors to use `"Prudent"` for the old behaviour.

## D. Brave patrols

- npc.json `"aggroRange"`: number 1–50 (metres), default 15; only Brave uses it.
- `NpcBrain`, calm mode, Brave, every 0.5 s (staggered per ANPC): look for the nearest foe within `aggroRange` that
  it can see (`CanSee`):
  - ANPCs: `NpcBrain.All()`, other than itself, not dead, `IsEnemy`.
  - Vanilla: `Physics.OverlapSphere(position, aggroRange)` → `DaggerfallEntityBehaviour` with an `EnemyMotor`, no
    `NpcBrain`, `EnemyMotor.IsHostile`, alive, team not `CityWatch` and not `PlayerAlly`.
  - Rule `AggroRules.IsFoe(isAnpc, anpcIsEnemy, vanillaHostile, watchOrAlly, alive)` (Core, pure).
- On a foe: `threat = foe; EnterCombat()` (Brave → Fight). The fight ends with today's creature rules (foe dead or
  over 40 m away) → calm, wanders back home. No crime, no player hostility.
- Turrets patrol too (they turn and attack in place).

## E. Data, docs, version

- npc.json keys: `fleeChance`, `aggroRange` (known-key list, unknown-key messages).
- README: bravery table (Coward / Prudent / Normal / Brave), `fleeChance`, `aggroRange`, retaliation note, sprite
  loading note (sets load when needed; an ANPC may appear a moment after spawning).
- Mod version 0.12.0.

## F. Testing

- Core unit tests: PNG header reader (valid, too short, not a PNG); `HostilityRules.Decide` table for all four
  braveries including Normal's single roll, re-roll after healing, fleeChance 0/100; `AttackedWhileCalm` table;
  `AggroRules.IsFoe` table; parsing of `fleeChance` / `aggroRange` (default, range, type) and `Prudent`.
- In-game self-test:
  - lazy load: a set added unloaded is not ready at spawn, becomes ready within a few seconds, the sprite then
    draws; after its last ANPC is removed and a (test-shortened) deadline passes, its textures are gone;
  - a calm ANPC whose motor gets `MakeEnemyHostileToAttacker(hostileAnpc)` with no damage starts fighting it;
  - Normal with fleeChance 100 flees below half the threshold, with 0 keeps fighting; Prudent flees below the
    threshold;
  - a calm Brave ANPC engages an enemy ANPC in range and sight; ignores one beyond its `aggroRange`; ignores an ANPC
    only hostile to the player (hit by the player, not an enemy).
  - Existing checks that stand calm Brave ANPCs next to test enemies are adjusted (distance or bravery) where the new
    patrol would change what they measure.
- Full self-test and sample smoke as before.

## Out of scope

- Texture cropping (render pipeline) and optional extra compression settings.
- Saving flee rolls.
- Brave ANPCs defending the player against the city watch.

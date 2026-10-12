# Advanced NPCs: personal opinion of the player, and update LOD — design

Date: 2026-10-12. Status: decided autonomously (user AFK, "can I leave you to it?"); every call below is listed in
the final report for the user to veto.

## Intent

From the ideas list, top-five items 3 and 5:

- **Opinion (3).** Each ANPC remembers how it feels about the player. Today "opinion" is the region's reputation
  only, so every townsperson reacts the same.
- **LOD (5).** ANPCs far from the player do less work per frame, so towns stay smooth as more ANPCs are installed.

Success: hitting a guard makes it (and people who saw it) like you less for a while; helping someone in a fight makes
them like you more; dialogue authors can raise or lower opinion; far-away ANPCs cost less per frame with no visible
or behavioural change near the player.

## A. Opinion

### Data

- `NpcState.opinion` (int, −100..100, default 0) and `NpcState.opinionAt` (ulong game seconds of the last change).
  Saved with the rest of the state (DFU's serializer); old saves load 0. `IsDefault` requires opinion 0.
- Opinion drifts back toward 0 by **2 points per game day**, applied lazily when read or changed
  (`OpinionRules.Current(opinion, at, now)`).

### What changes it (decided)

| Event | Change |
|---|---|
| The player attacks this ANPC (start of an assault, `OnAttackedByPlayer`) | −25 |
| The player attacks or kills another ANPC while this one is calm, not an enemy, within 15 m and can see the victim | −10 |
| This ANPC's creature/enemy threat dies while the player is within 20 m (you helped) | +10 |
| Dialogue reply action `"opinion": n` (−50..50, not 0) | n |

Every change clamps to −100..100.

### What it changes

- **Greetings:** the player band becomes `ReactionBand(DFU reaction + opinion)` (likes ≥ 10, loves ≥ 30).
- **Dialogue:** the `"reaction"` condition uses DFU's reaction + this ANPC's opinion.
- **Console:** `anpc_opinion <id> [value]` shows or sets it; the status line shows a non-zero opinion.

### Core

`OpinionRules` (pure): `Min = -100`, `Max = 100`, `DecayPerDay = 2`, `Assault = -25`, `Witness = -10`,
`Helped = 10`, `WitnessRange = 15f`, `HelpRange = 20f`, `Current(int opinion, ulong at, ulong now) : int`,
`Change(int opinion, ulong at, ulong now, int delta) : int` (decayed, then changed, clamped), `Band(int reaction,
int opinion) : string`.

## B. LOD

- `NpcLod.Interval(float distanceToPlayer, bool busy) : int` (pure): busy → 1; < 40 m → 1; < 100 m → 3; else 8.
  "Busy": fighting, fleeing, a one-shot sheet (attack, cast, hit, greet) playing, or the sprite still loading.
- Each ANPC gets a phase (0..7) so far ANPCs spread over frames. A component runs its work on frames where
  `(frameCount + phase) % interval == 0` and uses the accumulated delta time since its last run.
- Applied to: `NpcSprite` animation/drawing; `NpcGait` for calm ANPCs (fighting/fleeing/turrets every frame);
  brave patrol scans (interval × 0.5 s); `NpcGreeter` (no checks at all while the player is more than 30 m away).
- Not applied to: `NpcBrain`'s per-frame hostility guard and movement (`NpcMover`), DFU's own components.

## C. Testing

- Core: `OpinionRules` (decay, clamp, change, band), `NpcLod.Interval` table, parser for the `"opinion"` action.
- In-game self-test: assault −25 on the victim and −10 on a calm witness; reply action changes opinion; a high
  opinion makes a calm ANPC greet without the reaction override; a far ANPC's sprite changes frames less often than a
  near one but still animates.

## Out of scope

Prices, quests, gifts other than the `"opinion"` action, saving witnesses' memories of specific crimes.

# Advanced NPCs — Enemy ANPCs (design)

Date: 2026-10-07. Status: written while the user was away ("see if you could come up with a NPC system to make
them hostile on the go, specifically for new types of enemies"); every decision below is mine and open to change.

## 1. Goal

Let a mod author make **enemies** with the ANPC tools they already use (folder, `npc.json`, generic templates,
custom sprites, `anpc_spawn`): people or creatures that attack the player on sight, always or only at certain
hours, and let anything (console, other mods) turn any ANPC hostile or calm **on the go**.

Not in this step: spawning in the wilderness or in dungeons (towns only, as now; `anpc_spawn` works anywhere in a
town), group/faction behaviour, enemies that talk before attacking (that waits for Phase C topics).

## 2. `npc.json`

| key | default | meaning |
|---|---|---|
| `attitude` | `"calm"` | `"calm"`: today's townsfolk. `"hostile"`: an enemy — attacks the player on sight. |
| `hostileHours` | (always) | Only with `"hostile"`: `[from, to]` whole hours 0–23, e.g. `[20, 6]` = 8 pm to 6 am (may wrap midnight). Outside those hours the person is calm townsfolk; when the hours start it turns on the player, when they end it calms down (unless the player attacked it — then the normal calm-down rules apply). |
| `baseClass` | `"Spellsword"` | As now (the 18 human classes), **plus** any DFU creature (`Orc`, `Spider`, `SkeletalWarrior`, `Werewolf`, …) for hostile ANPCs. A creature needs `"attitude": "hostile"` without `hostileHours` (it cannot be talked to). |

Errors follow the usual `file: key: problem` format; a bad value skips the definition.

## 3. Behaviour of a hostile ANPC

- It fights: DFU's own enemy AI (EnemyMotor/EnemySenses/EnemyAttack) is on and hostile to the player, so it hunts
  the player once its senses find them, like any vanilla enemy. Bravery and `fleeHealthPercent` still apply
  exactly as for provoked townsfolk (a Coward runs instead of fighting).
- It never calms down while it is an enemy (no calm-down deadline).
- Attacking or killing it is **no crime** (it is an enemy, like a vanilla bandit). `crimeOnAttack` is ignored.
- It refuses to talk (as hostile ANPCs already do); Info mode still names it.
- Team: while an enemy it keeps DFU's own team for its class or creature (it behaves like a vanilla enemy with
  Enemy Infighting). While calm (outside `hostileHours`, or switched off) it is on the City Watch team like other
  townsfolk.
- It blocks resting nearby, like vanilla enemies (calm ANPCs do not).
- Health, death, corpse, loot, sprites, action frames and `anpc_spawn` work as for any ANPC.

## 4. On the go

- `anpc_hostile <id or key> [on|off]` turns a spawned ANPC into an enemy (no crime) or calms it. Without on/off it
  toggles. The switch is saved: the person stays that way through reloads until switched again, and it overrides
  the definition (`off` calms even a `"hostile"` enemy).
- Other mods: `AdvancedNpcsMod.SetHostile(string key, bool hostile)` (C#), or the mod message `"SetHostile"` with
  data `"<key>|on"` / `"<key>|off"` (DFU's `Mod.MessageReceiver`; the callback gets true if the key was found).
- Saved per person in the existing state table: `enemyOn` / `enemyOff` (bools). Missing in older saves = false.

## 5. Rules (pure code, unit-tested)

`EnemyRules.IsEnemyNow(hostileAttitude, fromHour, toHour, hour, forcedOn, forcedOff)`: `forcedOff` → false;
`forcedOn` → true; calm attitude → false; hostile without hours → true; with hours → hour in [from, to) with
wrap-around past midnight; equal from and to means all day.

## 6. Tests

- Core: parser (`attitude`, `hostileHours`, creature `baseClass` rules and errors), `EnemyRules`.
- Self-test: a hostile ANPC spawns fighting and hostile to the player; hitting it is no assault; killing it is no
  murder; a creature-based enemy (Orc) spawns and fights; `anpc_hostile` turns a calm ANPC into an enemy and back,
  and the switch survives a save round trip; the hour rule decides a `hostileHours` person.

# Advanced NPCs: custom sounds — design

Date: 2026-10-12. Status: design approved in chat by the user ("Yes that looks great").

## Intent

Authors give an ANPC its own voice and footsteps: sound files in its folder, picked at random, played when the
matching animation event happens. Success: a `Sounds` folder with `greet_1.ogg`, `attack_1.wav`, `attack_2.wav`,
`death_1.ogg`, `step_1.ogg`, `step_2.ogg` makes that ANPC greet, grunt, die and walk audibly with those sounds, and
nothing else about it changes.

## A. Files

- `ANPCs/<id>/Sounds/` (folder name matched case-insensitively). Formats: `.wav` and `.ogg`; `.mp3` when the
  implementation spike shows Unity 2019 decodes it at runtime on Windows, else an `.mp3` logs "convert to .ogg".
- The pool name is the file name without extension and without a trailing `_<digits>`: `attack_3.wav` → `attack`,
  `attack_range_2.ogg` → `attack_range`, `greet.ogg` → `greet`. Animation and sound suffixes are independent: any
  `attack*` sheet triggers the `attack` pool.
- Pools: `greet`, `attack`, `attack_range`, `cast`, `hit`, `death`, `step`. Any other name is logged once and ignored.
- `attack_range` with no files of its own uses the `attack` pool.
- Each trigger plays one file chosen at random from its pool.

## B. Triggers

Sounds follow the game event, not the sprite, so an ANPC with the vanilla sprite can have custom sounds too.

| Pool | Event | Chance |
|---|---|---|
| greet | the greeter greets (NpcGreeter) | greetChance |
| attack | a melee attack starts (DFU state PrimaryAttack begins) | combatChance |
| attack_range | a bow attack starts (RangedAttack1/2 begins) | combatChance |
| cast | a spell is cast (NpcMagic.TryCast succeeds) | combatChance |
| hit | health drops and the ANPC lives | combatChance |
| death | the ANPC dies | always |
| step | a footfall while walking or running (§D) | always, within stepDistance |

## C. Settings (npc.json)

```json
"sounds": {
  "combatChance": 50, "greetChance": 50, "stepDistance": 15,
  "soundPitch": 1.0, "soundPitchVariation": 0.05,
  "stepPitch": 1.0, "stepPitchVariation": 0.1
}
```

All optional. Unknown keys warn like other blocks. Schema and `check-schemas.py` cover the block.

| Key | Range | Default | Applies to |
|---|---|---|---|
| combatChance | 0–100 | 50 | attack, attack_range, cast, hit |
| greetChance | 0–100 | 50 | greet |
| stepDistance | > 0 m | 15 | step (no steps farther from the player) |
| soundPitch | 0.25–3 | 1 (normal pitch) | every pool except step |
| soundPitchVariation | 0–1 | 0 (none) | every pool except step |
| stepPitch | 0.25–3 | 1 | step |
| stepPitchVariation | 0–1 | 0 | step |

Each time a sound plays, its pitch is `pitch + random(−variation, +variation)`, at least 0.1. Requested by the
user as sound_pitch / step_pitch / pitch_variation; camelCase like the other npc.json keys. The vanilla grunts the
mod replays (§E) keep DFU's own pitch.

## D. Steps

- With a custom walk/run sheet: footfalls on the sheet's frames — by default frame 1 and the middle frame
  (`frames / 2 + 1`, counted from 1), i.e. two per loop. Optional `"stepFrames": [2, 6]` on a `walk*` / `run*`
  animation in `sprites.json` sets the frames (1-based, within the sheet).
- Without a custom sheet (vanilla sprite): a timer while moving — every 0.5 s walking, 0.33 s running.
- No step plays while the player is farther than `stepDistance`, or while the ANPC stands still, is dead or paralysed.

## E. Vanilla voices

- DFU plays race/gender voice grunts for human enemies on their attacks (20 %) and when hurt (40 %), when the
  "combat voices" setting is on. An ANPC with a custom pool for that event (`attack` / `attack_range` for attacks,
  `hit` for hurt) gets no vanilla grunt for it; an event without a custom pool keeps the vanilla grunt.
- Mechanism: the ANPC's `EnemySounds.RaceForSounds` is set to `Races.None`, which makes DFU's voice lookup return
  `SoundClips.None` (silence); the mod then plays the vanilla grunt itself (same chances and setting, same race
  and gender) for events that have no custom pool. Weapon swing, impact, miss and parry sounds are never touched.
- Creatures (e.g. an orc base class) growl through `EnemySounds`' attack clip on half their attacks; with an `attack`
  pool that cached clip is replaced by a silent one, so the folder's sound is the attack's voice (review fix). Idle
  barks stay.
- An ANPC with no `Sounds` folder is unchanged.

## F. Playback and loading

- 3D sound from the ANPC (an AudioSource on it: spatial blend 1, linear rolloff like DFU), volume from DFU's
  sound volume setting. Max audible distance 30 m (steps also limited by `stepDistance`).
- Sound folders are indexed at start (names only). Clips load when the first ANPC using the folder appears and are
  released a while after the last one goes, like sprite sets.
- `anpc_reload_sprites` becomes `anpc_reload_assets` (old name kept) and re-reads sounds too; `anpc_info` lists the
  ANPC's pools and counts.

## G. Testing

- Core (unit): pool name parsing (`attack_3` → attack, `attack_range_2` → attack_range, `greet` → greet,
  `foo_1` → unknown, `step_10` → step); pool choice incl. the `attack_range` → `attack` fallback; chance roll;
  default step frames and `stepFrames` parsing/validation; `sounds` block parsing (defaults, ranges, unknown keys);
  pitch with variation stays within `pitch ± variation` and never below 0.1;
  the vanilla-voice decision table.
- In-game self-test: a test ANPC with generated clips plays `greet` on greeting, `attack` on an attack, `hit` on
  damage, `death` on death (chance 100); no step beyond `stepDistance`, steps within it while walking; vanilla
  voice silenced (`RaceForSounds == None`) for an ANPC with a Sounds folder.

## Out of scope

Idle/ambient barks, per-file volume or pitch, lip sync, music.

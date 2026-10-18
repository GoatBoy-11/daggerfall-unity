# Step Sounds

Footstep sounds for Daggerfall's own enemies and the townspeople who wander outside, and soft wing beats for flying
enemies. The footsteps are those of the [Advanced NPCs](../AdvancedNPCs/README.md) mod: the four shared dirt-ground
steps from `ANPCs/_Sounds`.

- **Townspeople**: the people and city watch walking the streets step on the first and middle frame of their walk
  animation, and only while they really walk. They are silent while they stand facing you.
- **Enemies**: every enemy that walks on the ground, whether from a dungeon, a random encounter, a quest or a loaded
  save. Steps follow the walk animation's frames, so a slowed enemy steps slower and a paralysed one not at all.
  An enemy that only turns on the spot makes no sound. Swimmers (slaughterfish, dreugh, lamia), ghosts and
  wraiths, and levitating or falling enemies are silent.
- **Wings**: flyers (giant bats, imps, harpies, dragonlings, a transformed Daedra Seducer) beat their wings softly,
  once per loop of their flying animation, about twice a second, while flying and while hovering. Bats are the
  softest and highest; dragonlings the loudest and deepest. The table is `FootstepRules.ForFlyer`.
- **Size**: monsters sound like their size. Rats are quiet and high-pitched, spiders and scorpions a little less so,
  bears, centaurs, giants, Daedroths, Daedra Lords, gargoyles and atronachs are louder and deeper. Knights and the
  city watch sound slightly heavier than other classes. The table is `FootstepRules.ForEnemy`.
- **Loudness**: people play at the same loudness as ANPC's default steps (85%). Sounds come from the walker in 3D,
  are heard within the **Distance** setting (15 m by default) and fade out by 30 m, and follow DFU's sound volume.
- **Advanced NPCs**: ANPCs keep their own footsteps. This mod leaves them alone, so nothing plays twice.
- **Other mods**: if another mod replaces the enemy or townsperson sprites with its own models, those walkers
  step every half second while they move instead of on animation frames.

## Settings

| Setting | Default | |
|---|---|---|
| Townspeople | on | footsteps of the people and city watch in the streets |
| Class enemies | on | mages, thieves, knights, guards and the other classes |
| Monsters | on | monsters that walk |
| Wings | on | wing beats of flying enemies |
| Volume | 100 | footsteps and wings, in percent of DFU's sound volume |
| Distance | 15 | metres within which you hear footsteps and wings (5-30) |

Settings take effect at once.

## Console

`step_sounds` prints where the sounds came from, the settings, how many enemies and townspeople have footsteps
(and how many are walking or flying within earshot now), and how many steps and wing beats have played.

## Other sounds

Put `step.ogg` or `step_1.ogg`, `step_2.wav`, ... (`.ogg`, `.wav` or `.mp3`; an `.ogg` must be audio only) in
`StreamingAssets/Sound/step_sounds/`. If that folder has any, they replace the bundled footsteps the next time the
game starts. `wing_1.ogg`, `wing_2.ogg`, ... there replace the bundled wing beats the same way. Each kind is replaced
on its own, and you don't need to rebuild the mod. DFU itself only looks in `Sound` for files named after its own
sounds, so this subfolder never clashes with sound replacements. A single flap works best; trim a recording of several flaps
into one file per flap.

## Other mods' enemies

Enemies added by other mods (Daggerfall Enemy Expansion, for example) get sounds too, as long as they are created
through DFU's usual enemy spawning, which raises `GameManager.OnEnemySpawn`. Their behaviour decides what plays:
ground walkers step, flyers beat their wings, swimmers and ghosts are silent. Enemies with human affinity follow
the **Class enemies** setting; all others follow **Monsters**. Volume and pitch are the defaults, since only
vanilla ids have entries in the tables.

## Building

You need the Unity editor DFU uses (2019.4) and this fork of the project.

1. Open the project in Unity and wait for it to import.
2. **Daggerfall Tools > Build Step Sounds mod**. When it is done, the editor opens the folder that holds
   `Builds/StepSounds/StandaloneWindows/step_sounds.dfmod`.
3. Copy `step_sounds.dfmod` to `DaggerfallUnity_Data/StreamingAssets/Mods/` in the game's folder, start
   DFU and turn the mod on in the mod list.

From a command line (Unity closed):
`Unity.exe -quit -projectPath <project> -executeMethod StepSounds.EditorTools.StepSoundsModBuilder.Build -modOut <dir>`.

DFU compiles the mod's scripts when the game starts, as C# 4: no string interpolation, `?.` or `=>` members.
`Tools~/test-rules.sh` tests `FootstepRules` with plain Mono (`mcs`/`mono`).

## Credits

- The four step sounds come from NOX_SOUND Footsteps Essentials by Nox Sound (CC0), the same files as Advanced
  NPCs' `ANPCs/_Sounds` (see its `SOUND_CREDITS.txt`).
- The four wing beats were synthesized for this mod with `Tools~/make-wings.py` (filtered noise: a low downstroke
  plus a feathery air swish), then turned down 18 dB and saved as mono 22 kHz OGG. They are original and free of
  any license; regenerate or tune them with `python3 Tools~/make-wings.py <dir>`.

# Vanilla Footsteps

Footstep sounds for Daggerfall's own enemies and the townspeople who wander outside. It uses the footsteps of the
[Advanced NPCs](../AdvancedNPCs/README.md) mod: the four shared dirt-ground steps from `ANPCs/_Sounds`.

- **Townspeople**: the people and city watch walking the streets step on the first and middle frame of their walk
  animation, and only while they really walk. They are silent while they stand facing you.
- **Enemies**: every enemy that walks on the ground, whether from a dungeon, a random encounter, a quest or a loaded
  save. Steps follow the walk animation's frames, so a slowed enemy steps slower and a paralysed one not at all.
  An enemy that only turns on the spot makes no sound. Flyers (imps, bats, harpies, dragonlings, a transformed
  Daedra Seducer), swimmers (slaughterfish, dreugh, lamia), ghosts and wraiths, and levitating or falling enemies
  are silent.
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
| Volume | 100 | percent of DFU's sound volume |
| Distance | 15 | metres within which you hear footsteps (5-30) |

Settings take effect at once.

## Console

`vfootsteps` prints where the sounds came from, the settings, how many enemies and townspeople have footsteps
(and how many are walking within earshot now), and how many steps have played.

## Other step sounds

Put `step.ogg` or `step_1.ogg`, `step_2.wav`, ... (`.ogg`, `.wav` or `.mp3`; an `.ogg` must be audio only) in
`StreamingAssets/VanillaFootsteps/`. If that folder has any, they replace the bundled sounds the next time the game
starts. You don't need to rebuild the mod.

## Building

You need the Unity editor DFU uses (2019.4) and this fork of the project.

1. Open the project in Unity and wait for it to import.
2. **Daggerfall Tools > Build Vanilla Footsteps mod**. When it is done, the editor opens the folder that holds
   `Builds/VanillaFootsteps/StandaloneWindows/vanillafootsteps.dfmod`.
3. Copy `vanillafootsteps.dfmod` to `DaggerfallUnity_Data/StreamingAssets/Mods/` in the game's folder, start
   DFU and turn the mod on in the mod list.

From a command line (Unity closed):
`Unity.exe -quit -projectPath <project> -executeMethod VanillaFootsteps.EditorTools.VanillaFootstepsModBuilder.Build -modOut <dir>`.

DFU compiles the mod's scripts when the game starts, as C# 4: no string interpolation, `?.` or `=>` members.
`Tools~/test-rules.sh` tests `FootstepRules` with plain Mono (`mcs`/`mono`).

## Credits

The four step sounds come from NOX_SOUND Footsteps Essentials by Nox Sound (CC0), the same files as Advanced NPCs'
`ANPCs/_Sounds` (see its `SOUND_CREDITS.txt`).

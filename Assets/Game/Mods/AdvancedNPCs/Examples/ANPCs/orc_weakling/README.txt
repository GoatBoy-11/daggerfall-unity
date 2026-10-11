Orc weakling / lower-tier orc with a crude club

Manual spawn: anpc_spawn orc_weakling
Hostile, Brave, male, wander radius 6. Base class Orc (DFU creature), tuned with
"combat": about 70% of an Orc's health, 70% of its damage (1-6 becomes 1-4) and
1.3x attack speed (shorter wait between swings).
Spawns on its own only in dungeons: 25% chance of 1-3 in orc strongholds,
natural caves and mines. No town or wilderness spawns.
Orsimer names (gro-/gra-) from the shared orsimer list; custom 64x64 portrait.

14 sheets, 8 directions, 8 fps, 256-pixel cell height, 87 animation frames.
Both club attacks land on frame 3 (actionFrame). Includes hit blood, the
growing death pool and yellow casting effects. death_1 ends flat on his back,
the club lying beside him.

Needs the Advanced NPCs build with "combat" support (branch
feature/anpc-combat-tuning). Source: Models/orc_weakling (README there).
Model: Meshy "Crimson Eyed Goblin"; the club is original (procedural).

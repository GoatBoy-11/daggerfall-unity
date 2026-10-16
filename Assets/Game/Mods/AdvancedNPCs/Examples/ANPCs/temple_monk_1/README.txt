Temple monk 1

Manual spawn: anpc_spawn temple_monk_1
Male Breton, Monk base class, Normal bravery, wander radius 5.
Uses the existing breton_oblivion_skyrim namelist. Automatic spawning is
initially disabled with spawn.count [0, 0].

Heal supports nearby allies and a friendly player, during and outside combat.
Shield remains a self-directed combat spell. Healing uses cast sheets unless heal
sheets are added; optional Sounds/heal clips use sounds.combatChance.
120 magicka, recovery on area entry, skill 80, cast cooldown 5-8 seconds.
Recipients must be in sight within 2.5 m and below 60% health; player reaction
must be likes or loves. This is automatic support healing, not a paid service.

15 actions at 8 fps; 79 poses and 632 directional cells. Punches land on
frame 3. Yellow casting charge on frame 2 and release on frame 3. Blood
flecks cover both hit variants; pools grow across death frames 4 and 5.

Prayer schedule:
06:00-08:00, health >=50%: monk_work_cycle uses the home_prayer named spot
(1 metre beside the original home), prays for 15 active seconds, stands for 5,
then repeats. The spot sets facing to 180 degrees.
12:00-13:00: play_animation pray with only_interrupt_at_combat true; greetings,
chat and calm healing do not interrupt. Attacks/combat still take precedence.
18:00-20:00, clear weather: work_at selects a free prayer-tagged spot and prays.
Other hours/conditions use original AI. Each home-relative post belongs to its
translated position in that area. Edit ANPCs/_Spots/home_prayer.json to move it.
Adding pray_2, pray_3, etc. enables random variants between completed cycles.
Manual-spawn only: anpc_spawn temple_monk_1.

Editable Blender files, clean renders and transparent effect layers live
under Models/temple_monk_1 and output/temple_monk_1.

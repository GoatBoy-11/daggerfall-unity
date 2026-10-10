Dunmer Mercenary

A male Dunmer in iron armour carrying an iron axe, rendered from dunmer_1_4.blend.
Names come from the shared dunmer_morrowind namelist.
Manual spawn: anpc_spawn dunmer_mercenary
To fight him: anpc_hostile <spawned key> on

Fireball (area missile), Wizard's Fire (single-target missile), Resist Fire (self).
80 magicka, compared with the Spellblade's 150. Casting delay 4-7 seconds;
magicka refills when the player enters a new area. Edit npc.json to change these.
No ambient spawning is enabled by default.

12 sheets, 8 directions, 256-pixel cell height, 8 fps, consistent scale and ground row.
Impact frames counted from 1: attack_1 = 3; attack_2 = 2.
The final death pose is retained. run and talk_1 are exported for future use;
current ANPC states use idle, walk, hit, attack, cast and death.
The marked working copy is Models/dunmer_1/dunmer_mercenary_marked.blend.
The original dunmer_1_4.blend is unchanged.

Re-render from F:/_Projects/Dagerfall:
& 'F:/Blender 5.2/blender.exe' --factory-startup -b 'Models/dunmer_1/dunmer_mercenary_marked.blend' --python 'daggerfall-unity/Assets/Game/Mods/AdvancedNPCs/Tools~/render-character-sprites.py' -- 'Models/dunmer_1/mercenary_sprites' --rig rig --mesh 'dunmer_2,iron_axe' --keep-last death_1 --samples 48

Uses the same wench/Spellblade camera and upper-left lighting setup.
Both body and axe are included in the rendered animation bounds.

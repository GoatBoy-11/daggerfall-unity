Dunmer Spellblade

A male Dunmer with a generated Morrowind-style name, chitin armour and sword, using the saved dunmer_1_2.blend.
Manual spawn: anpc_spawn dunmer_spellblade
To fight him: anpc_hostile <spawned key> on

Fireball, Heal and Shield; 150 magicka; casting delay 4-7 seconds; refill on area entry.
All settings are editable in npc.json. No ambient spawns are enabled by default.

12 sheets, 8 directions per sheet, 256-pixel cell height, 8 fps, consistent scale and ground row.
Attack impact frames (counted from 1): attack_1 = 3, attack_2 = 2.
Death retains its final resting pose. run and talk_1 are exported for future use;
current in-game states use idle, walk, hit, attack, cast and death.
The working Blender copy has action pose markers; the original file is unchanged.

Re-render from F:/_Projects/Dagerfall:
& 'F:/Blender 5.2/blender.exe' --factory-startup -b 'Models/dunmer_1/dunmer_spellblade_marked.blend' --python 'daggerfall-unity/Assets/Game/Mods/AdvancedNPCs/Tools~/render-character-sprites.py' -- 'Models/dunmer_1/sprites' --rig rig --mesh 'dunmer_1,chitin_sword' --keep-last death_1 --samples 48

Uses the wench baker's orthographic camera (7 degrees down), viewer-relative upper-left
key light and weak fill, with transparent backgrounds. Both body and sword are included
in animation bounds so weapon swings fit without changing the character's scale.

Shared namelist: ANPCs/_Namelists/dunmer_morrowind.json.
Use "nameList": "dunmer_morrowind" on any generic Dunmer to generate a given name and family name.
Male and female given names are included. Fixed "name" or "names" fields override generated names.

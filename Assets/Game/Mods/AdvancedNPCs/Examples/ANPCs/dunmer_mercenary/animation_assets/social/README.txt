Mercenary prayer and greeting animation assets

Blender source: Models/dunmer_1/dunmer_mercenary_social.blend
pray_1: 32 exported frames / 4 seconds, plus a closing repeat at Blender frame 33.
The mercenary crouches to put the axe down, stands with joined hands and bowed head,
then crouches to pick the axe up and returns to his original idle pose.
greet_1: 12 exported frames / 1.5 seconds, plus a closing repeat at Blender frame 13.
He raises and waves his free hand with a small nod; the axe remains in his right hand.

Both actions use 8 fps and stepped keys. Sheets contain all eight directions and
share cell height, scale and ground row with the existing mercenary sprite set.
No action markers are needed for these gestures. Existing combat markers are preserved.

These are animation assets for future triggers, per the requested scope. The current
ANPC runtime does not automatically play pray_1 or greet_1; the live sprites.json is unchanged.

Re-render from F:/_Projects/Dagerfall:
& 'F:/Blender 5.2/blender.exe' --factory-startup -b 'Models/dunmer_1/dunmer_mercenary_social.blend' --python 'daggerfall-unity/Assets/Game/Mods/AdvancedNPCs/Tools~/render-character-sprites.py' -- 'Models/dunmer_1/social_sprites' --rig rig --mesh 'dunmer_2,iron_axe' --actions 'pray_1,greet_1' --layout 'Models/dunmer_1/mercenary_sprites/sprites.json' --samples 48

A c_anpc_weapon.r prop control parks the axe while the hand is free. All original
actions key this control at identity; their original weapon and body movement is retained.
Geometry checks cover every authored frame for unwanted axe/body intersections and
hand reach. Original movement was compared across 105 sampled frames.

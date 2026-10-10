Dunmer Spellblade prayer and greeting animation assets

Blender source: Models/dunmer_1/dunmer_spellblade_social.blend
Based on the edited dunmer_mercenary_social.blend1 with dunmer_1 and chitin_sword visible.
The input file and the existing spellcaster sprite set are preserved.

pray_1: 32 exported frames / 4 seconds, plus a closing repeat at Blender frame 33.
He places his chitin sword flat beside him, stands with joined hands and bowed head,
then picks the sword up and returns to idle. The lowering path and crouch are adapted
for the longer blade. The right-hand IK stretch is enabled only during prayer for contact.
greet_1: 12 exported frames / 1.5 seconds, plus a closing repeat at Blender frame 13.
He waves his free left hand and nods, holding the sword in his right hand.

Both actions use 8 fps with stepped keys. Sheets contain all eight directions and
share cell height, scale, ground row and direction order with the existing Spellblade sprites.
The c_anpc_weapon.r prop control holds the released sword. Original actions reset it
at identity. Existing combat markers are retained. No gesture action markers are needed.

These sheets are supplied as animation assets for future triggers, matching the requested
asset-only scope. The current ANPC runtime does not automatically play these actions;
the live sprites.json and NPC behaviour are unchanged.

Re-render from F:/_Projects/Dagerfall:
& 'F:/Blender 5.2/blender.exe' --factory-startup -b 'Models/dunmer_1/dunmer_spellblade_social.blend' --python 'daggerfall-unity/Assets/Game/Mods/AdvancedNPCs/Tools~/render-character-sprites.py' -- 'F:/_Projects/Dagerfall/Models/dunmer_1/spellblade_social_sprites' --rig rig --mesh 'dunmer_1,chitin_sword' --actions 'pray_1,greet_1' --layout 'F:/_Projects/Dagerfall/Models/dunmer_1/sprites/sprites.json' --samples 48

Validation: 352 sprite cells checked for transparency bounds and shared layout.
Every action frame checked for unwanted sword/body intersections and hand reach.
105 existing animation frames compared to the edited input source; motion is retained.

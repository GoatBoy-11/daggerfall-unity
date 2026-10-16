# Elegantly Dressed Lady - animation revision 3

Source: Models/elegantly_dressed_lady/elegantly_dressed_lady_3.blend.
12 animation sheets, eight direction rows, 256-pixel cells, 8 fps.
The last source frame of EVERY action is excluded, including death.

Frames: attack_1 6, attack_range_1 6, cast_1 6, death_1 7, greet_1 7,
hit_1 4, idle_1 10, idle_2 10, run_1 6, talk_1 12, talk_2 12, walk_1 8.
Blender hurt_1 is exported as hit_1 for the ANPC hit reaction.
Existing blood spray and death pool are reprojected to the revised poses.
No projectile sheet is supplied; ranged/casting actions are included as authored.

The existing npc.json and portrait are retained. Replaces vanilla flat 182.27.
Clean renders, effect layers, previews, frame audit, and previous-sheet backups:
output/elegantly_dressed_lady/revision_3.

Validation: 752 nonempty directional cells, sheet dimensions, clipping, excluded
final frames, unchanged source blend, and the ANPC SpriteSetParser/SpriteStates.
A live gameplay check has not been performed.

Install: copy the ANPCs folder into DaggerfallUnity_Data/StreamingAssets.

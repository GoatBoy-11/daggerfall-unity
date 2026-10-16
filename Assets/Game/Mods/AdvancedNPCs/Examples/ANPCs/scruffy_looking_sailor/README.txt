# Scruffy Looking Sailor — 182_25 replacement

This is the renamed ANPC formerly published as `crimson_wanderer_1`.
The sprites are re-rendered directly from the user's corrected
`Models/crimson_wanderer_1/crimson_wanderer_1_animated.blend`.
No animation authoring or rig fitting is rerun, and that source file is not saved
or modified by this pipeline. Its interpolation, poses, frame ranges and action
markers are retained. Rendering samples at the scene's 8 fps.

`scruffy_looking_sailor_animated.blend` is a byte-identical copy of the corrected
source, supplied under the new preset name for convenience. Internal Blender
mesh names remain compatible with the original project.

The ANPC folder ID and custom portrait are `scruffy_looking_sailor`; its display
name is **Scruffy Looking Sailor**, the original flat's name. It replaces exactly
archive 182, record 25, keeps the 1.95 m standing height, and has no additional
random spawning rules. Existing flat names and dialogue portraits are retained
on replacements. The preset remains a male Breton Warrior with melee combat
and conversation gestures. Direct preview:

`anpc_spawn scruffy_looking_sailor`

All eleven actions are re-rendered in eight directions. The user's added idle
frames and the frame-0 attack pose are included. Looping actions omit their
final closing key under the existing sprite-export convention; death keeps
its settled final pose. `sprites/sprites.json` records the resulting counts
and impact frames rather than reusing the previous metadata.

The portrait is refreshed. Hit sprays and larger death blood pools are
recomposited against the new pose projections and render scale. The pool width
is 70% of standing pixel height and its height is 30% of that width. Clean
sheets and editable transparent effect layers are retained in the output folder.

The previous active ANPC folders are archived outside the game's ANPC roots,
so the old and new IDs cannot both replace flat 182_25. Other ANPCs are preserved.
The old Blender project remains in its original folder. Render scripts, frame
mapping, source checksum, validation, previews, prior pack backups and the ZIP
are in `output/scruffy_looking_sailor/`.

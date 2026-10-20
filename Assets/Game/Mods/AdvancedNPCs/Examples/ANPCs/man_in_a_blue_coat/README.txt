# Man in a Blue Coat - replacement for 182_23

ANPC id `man_in_a_blue_coat`; replaces Daggerfall flat archive 182, record 23.
Source: `../_models_for_use/Meshy_AI_Stern_Rogue_1016025818_texture.glb` (unchanged). Editable files:
`man_in_a_blue_coat_rigged.blend` (clean Auto-Rig Pro rig) and `man_in_a_blue_coat_animated.blend` (actions).

The real mesh was merged by distance at 0.0001 units before ARP matching and binding
(12075 -> 7973 vertices). Arm weights were stripped from the torso, shirt and coat
panels (heat weights leaked into the open coat front); the coat hem below the hips blends
into four sway bones, and the hair follows the head rigidly.

He wears a low ponytail (added mesh, hair colour sampled from his texture): tied at the nape under
his bob and lying over the coat collar; the top follows the head, the tip the upper back.
A small orange blotch in the Meshy texture on the coat back is painted out in the packed copy
(the source GLB is unchanged).

Idle follows the flat: both hands hooked over the belt front, elbows flared out, wide
turned-out stance, head turned to his left with the chin raised, as in the flat.
One action of each kind, 8 fps, Constant interpolation; looping actions omit their closing key:

| Action | Frames | Motion |
| --- | ---: | --- |
| idle_1 | 4 | Flat pose with breathing |
| walk / run | 8 / 8 | Natural arm swing |
| greet_1 | 6 | Raised right hand, left hand stays on the belt |
| talk_1 | 6 | Right-hand gestures and nods |
| attack_1 | 6 | Overhead diagonal cut; impact marker frame 3. The short sword exists only in this action. Body turns with the swing, head stays on the target |
| hit_1 | 4 | Recoil and recovery |
| death_1 | 7 | Buckle, kneel, fall face down (keeps the settled last pose) |

Hit sprays and death blood pools are composited (clean sheets and effect layers kept in
`effect_layers/`). Standing height 1.9 m = the flat's size
((pixels + trunc(pixels * -100 / 256)) * 0.025). Male Breton Warrior, melee,
conversation on; original flat names and portraits are kept on replacements; the custom
portrait is used when spawned directly: `anpc_spawn man_in_a_blue_coat`.

Scripts: `output/coat_men/` (chars.py, rig.py, author.py, review.py, render_sprites.py,
extract_anchors.py, build_effects.py, render_portrait.py, publish.py, smoke.ps1).

# Man in a Green Vest - replacement for 182_17

ANPC id `man_in_a_green_vest`; replaces Daggerfall flat archive 182, record 17.
Source: `../_models_for_use/Meshy_AI_Cedric_the_Green_1016031248_texture.glb` (unchanged). Editable: `man_in_a_green_vest_rigged.blend`, `man_in_a_green_vest_animated.blend`.

Rig: mesh merged by distance (0.0001) before Auto-Rig Pro matching; bound with **Adaptive Auto Skinning**
(output/coat_men/rig_glb.py; Voxel Heat Diffuse is the fallback), then the usual torso/sleeve leak cleanup and
cloth sway bones from output/coat_men/rigcfg.py.

Idle follows the flat: right fist on the hip, a coin pouch held out in the left hand. Weapon: short sword, attack only; the pouch is dropped on death.
One action of each kind, 8 fps, Constant keys: idle_1, walk, run, greet_1, talk_1, attack_1 (impact frame 3, body
turns with the blow, head on the target), hit_1, death_1. Hit sprays and death pools composited.
Standing height 1.925 m from the flat's size and scale. Male Nord Warrior; original flat names and portraits
are kept on replacements; `anpc_spawn man_in_a_green_vest` previews it with its own portrait.

Scripts: output/coat_men (chars.py, rigcfg.py, rig_glb.py, author.py, review.py, render_sprites.py, extract_anchors.py,
build_effects.py, render_portrait.py, publish.py, smoke.ps1) and output/sas (chain_glb.sh, final.sh, run_blender.ps1).

# Man with a Broom - replacement for 182_39

ANPC id `man_with_a_broom`; replaces Daggerfall flat archive 182, record 39.
Source: `../_models_for_use/Meshy_AI_The_Stern_Villager_1016025628_texture.glb` (unchanged). Editable: `man_with_a_broom_rigged.blend`, `man_with_a_broom_animated.blend`.

Rig: mesh merged by distance (0.0001) before Auto-Rig Pro matching; bound with **Adaptive Auto Skinning**
(output/coat_men/rig_glb.py; Voxel Heat Diffuse is the fallback), then the usual torso/sleeve leak cleanup and
cloth sway bones from output/coat_men/rigcfg.py.

Idle follows the flat: broom planted beside him in the right hand. Weapon: the broom (always held: swept along when walking, swung as a club).
One action of each kind, 8 fps, Constant keys: idle_1, walk, run, greet_1, talk_1, attack_1 (impact frame 3, body
turns with the blow, head on the target), hit_1, death_1, and sweep_1 (8 frames: two-handed strokes across the floor, used by the chore package from 07:00 to 19:00). Hit sprays and death pools composited.
Standing height 1.925 m from the flat's size and scale. Male Breton Warrior; original flat names and portraits
are kept on replacements; `anpc_spawn man_with_a_broom` previews it with its own portrait.

Scripts: output/coat_men (chars.py, rigcfg.py, rig_glb.py, author.py, review.py, render_sprites.py, extract_anchors.py,
build_effects.py, render_portrait.py, publish.py, smoke.ps1) and output/sas (chain_glb.sh, final.sh, run_blender.ps1).

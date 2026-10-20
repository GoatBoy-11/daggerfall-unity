# Bald Man with a Jug - replacement for 182_1

ANPC id `bald_man_with_a_jug`; replaces Daggerfall flat archive 182, record 1.
Source: `../_models_for_use/Meshy_AI_The_Stern_Blacksmith_1016031502_texture.glb` (unchanged). Editable: `bald_man_with_a_jug_rigged.blend`, `bald_man_with_a_jug_animated.blend`.

Rig: mesh merged by distance (0.0001) before Auto-Rig Pro matching; bound with **Adaptive Auto Skinning**
(output/coat_men/rig_glb.py; Voxel Heat Diffuse is the fallback), then the usual torso/sleeve leak cleanup and
cloth sway bones from output/coat_men/rigcfg.py.

Idle follows the flat: a clay jug raised in the right hand, left fist on the hip. Weapon: unarmed: he swings with the jug still in hand.
One action of each kind, 8 fps, Constant keys: idle_1, walk, run, greet_1, talk_1, attack_1 (impact frame 3, body
turns with the blow, head on the target), hit_1, death_1. Hit sprays and death pools composited.
Standing height 2.05 m from the flat's size and scale. Male Redguard Warrior; original flat names and portraits
are kept on replacements; `anpc_spawn bald_man_with_a_jug` previews it with its own portrait.

Scripts: output/coat_men (chars.py, rigcfg.py, rig_glb.py, author.py, review.py, render_sprites.py, extract_anchors.py,
build_effects.py, render_portrait.py, publish.py, smoke.ps1) and output/sas (chain_glb.sh, final.sh, run_blender.ps1).

# Woman in a Purple Skirt - replacement for 184_33

ANPC id `woman_in_a_purple_skirt`; replaces Daggerfall flat archive 184, record 33.
Source: `../_models_for_use/Meshy_AI_Ember_Violette_1013202141_texture.glb` (unchanged). Editable: `woman_in_a_purple_skirt_rigged.blend`, `woman_in_a_purple_skirt_animated.blend`.

Rig: mesh merged by distance (0.0001) before Auto-Rig Pro matching; bound with **Adaptive Auto Skinning**
(output/coat_men/rig_glb.py; Voxel Heat Diffuse is the fallback), then the usual torso/sleeve leak cleanup and
cloth sway bones from output/coat_men/rigcfg.py.

Idle follows the flat: both hands on her hips. Weapon: small sword (75%), attack only.
One action of each kind, 8 fps, Constant keys: idle_1, walk, run, greet_1, talk_1, attack_1 (impact frame 3, body
turns with the blow, head on the target), hit_1, death_1. Hit sprays and death pools composited.
Standing height 2.025 m from the flat's size and scale. Female Redguard Bard; original flat names and portraits
are kept on replacements; `anpc_spawn woman_in_a_purple_skirt` previews it with its own portrait.

Scripts: output/coat_men (chars.py, rigcfg.py, rig_glb.py, author.py, review.py, render_sprites.py, extract_anchors.py,
build_effects.py, render_portrait.py, publish.py, smoke.ps1) and output/sas (chain_glb.sh, final.sh, run_blender.ps1).

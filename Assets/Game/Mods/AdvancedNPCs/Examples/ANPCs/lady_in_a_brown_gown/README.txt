# Lady in a Brown Gown - replacement for 183_18

ANPC id `lady_in_a_brown_gown`; replaces Daggerfall flat archive 183, record 18.
Source: `../_models_for_use/Meshy_AI_Amethyst_Desert_Empre_1015164407_texture.glb` (unchanged). Editable files:
`lady_in_a_brown_gown_rigged.blend` (clean Auto-Rig Pro rig) and `lady_in_a_brown_gown_animated.blend` (actions).

The real mesh was merged by distance at 0.0001 units before ARP matching and binding
(11344 -> 8022 vertices). Arm weights were stripped from the torso volume and
torso/leg weights from the sleeves (heat weights leak both ways). The floor-length gown is 65% hip-rigid with sway bones. Her long bell sleeves hang from two "sleeve" bones that author.py points at the floor every frame (except once lying), so they drape whatever the forearm does. She keeps straight legs when lying so the gown lies flat.

Idle follows the flat: left hand on her hip with the hip cocked, right arm hanging, head turned to her right.
Weapon: small sword (75%), attack only.
One action of each kind, 8 fps, Constant interpolation; looping actions omit their closing key:
idle_1 (4), walk (8), run (8), greet_1 (6), talk_1 (6), attack_1 (6, impact marker frame 3: the
body turns with the swing and the head stays on the target), hit_1 (4), death_1 (7, keeps the last pose).

Hit sprays and death blood pools are composited (clean sheets and effect layers in `effect_layers/`).
Standing height 2.075 m from the flat's size and scale. Female Redguard Bard, melee,
conversation on; original flat names and portraits are kept on replacements; the custom portrait is
used when spawned directly: `anpc_spawn lady_in_a_brown_gown`.

Scripts: `output/coat_men/` (chars.py, rigcfg.py, rig.py, author.py, review.py, render_sprites.py,
extract_anchors.py, build_effects.py, render_portrait.py, publish.py, smoke.ps1).

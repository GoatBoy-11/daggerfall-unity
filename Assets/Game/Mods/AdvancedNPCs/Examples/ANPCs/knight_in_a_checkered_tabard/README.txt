# Knight in a Checkered Tabard - replacement for 183_4

ANPC id `knight_in_a_checkered_tabard`; replaces Daggerfall flat archive 183, record 4.
Source: `../_models_for_use/Meshy_AI_Goldensteel_Warden_1015160841_texture.glb` (unchanged). Editable files:
`knight_in_a_checkered_tabard_rigged.blend` (clean Auto-Rig Pro rig) and `knight_in_a_checkered_tabard_animated.blend` (actions).

The real mesh was merged by distance at 0.0001 units before ARP matching and binding
(14999 -> 8060 vertices). Arm weights were stripped from the torso volume and
torso/leg weights from the sleeves (heat weights leak both ways). The knee-length tabard below the belt is half hip-rigid, half thigh-driven, with four sway bones.

Idle follows the flat: both fists on his hips, elbows out, head turned to his right.
Weapon: short sword, attack only.
One action of each kind, 8 fps, Constant interpolation; looping actions omit their closing key:
idle_1 (4), walk (8), run (8), greet_1 (6), talk_1 (6), attack_1 (6, impact marker frame 3: the
body turns with the swing and the head stays on the target), hit_1 (4), death_1 (7, keeps the last pose).

Hit sprays and death blood pools are composited (clean sheets and effect layers in `effect_layers/`).
Standing height 2.075 m from the flat's size and scale. Male Breton Knight, melee,
conversation on; original flat names and portraits are kept on replacements; the custom portrait is
used when spawned directly: `anpc_spawn knight_in_a_checkered_tabard`.

Scripts: `output/coat_men/` (chars.py, rigcfg.py, rig.py, author.py, review.py, render_sprites.py,
extract_anchors.py, build_effects.py, render_portrait.py, publish.py, smoke.ps1).

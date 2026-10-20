# Old Man in a Green Tunic - replacement for 184_0

ANPC id `old_man_in_a_green_tunic`; replaces Daggerfall flat archive 184, record 0.
Source: `../_models_for_use/Meshy_AI_Regal_Elder_Knight_1016033041_texture.glb` (unchanged). Editable files:
`old_man_in_a_green_tunic_rigged.blend` (clean Auto-Rig Pro rig) and `old_man_in_a_green_tunic_animated.blend` (actions).

The real mesh was merged by distance at 0.0001 units before ARP matching and binding
(11891 -> 8009 vertices). Arm weights were stripped from the torso volume and
torso/leg weights from the sleeves (heat weights leak both ways). The purple cape (picked by colour) hangs from the chest and blends into four sway bones by the hem.

Idle follows the flat: arms hanging loosely at his sides.
Weapon: short sword, attack only.
One action of each kind, 8 fps, Constant interpolation; looping actions omit their closing key:
idle_1 (4), walk (8), run (8), greet_1 (6), talk_1 (6), attack_1 (6, impact marker frame 3: the
body turns with the swing and the head stays on the target), hit_1 (4), death_1 (7, keeps the last pose).

Hit sprays and death blood pools are composited (clean sheets and effect layers in `effect_layers/`).
Standing height 1.975 m from the flat's size and scale. Male Redguard Monk, melee,
conversation on; original flat names and portraits are kept on replacements; the custom portrait is
used when spawned directly: `anpc_spawn old_man_in_a_green_tunic`.

Scripts: `output/coat_men/` (chars.py, rigcfg.py, rig.py, author.py, review.py, render_sprites.py,
extract_anchors.py, build_effects.py, render_portrait.py, publish.py, smoke.ps1).

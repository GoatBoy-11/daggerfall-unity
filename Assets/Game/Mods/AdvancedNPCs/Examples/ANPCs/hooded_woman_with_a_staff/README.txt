# Hooded Woman with a Staff - replacement for 184_1

ANPC id `hooded_woman_with_a_staff`; replaces Daggerfall flat archive 184, record 1.
Source: `../_models_for_use/Meshy_AI_Hooded_Woman_1016033836_texture.glb` (unchanged). Editable files:
`hooded_woman_with_a_staff_rigged.blend` (clean Auto-Rig Pro rig) and `hooded_woman_with_a_staff_animated.blend` (actions).

The real mesh was merged by distance at 0.0001 units before ARP matching and binding
(11088 -> 6338 vertices). Arm weights were stripped from the torso volume and
torso/leg weights from the sleeves (heat weights leak both ways). The green skirt is 65% hip-rigid with sway bones; the purple cape hangs from the chest; the hood follows the head. The staff (added mesh, 1.85 units, dark wood with a gold knob) is a bone-parented prop.

Idle follows the flat: right hand holding a tall gold-topped staff planted beside her, left arm hanging.
Weapon: her staff (always held: walking stick, strike, dropped on death).
One action of each kind, 8 fps, Constant interpolation; looping actions omit their closing key:
idle_1 (4), walk (8), run (8), greet_1 (6), talk_1 (6), attack_1 (6, impact marker frame 3: the
body turns with the swing and the head stays on the target), hit_1 (4), death_1 (7, keeps the last pose).

Hit sprays and death blood pools are composited (clean sheets and effect layers in `effect_layers/`).
Standing height 2.075 m from the flat's size and scale. Female Redguard Mage, melee,
conversation on; original flat names and portraits are kept on replacements; the custom portrait is
used when spawned directly: `anpc_spawn hooded_woman_with_a_staff`.

Scripts: `output/coat_men/` (chars.py, rigcfg.py, rig.py, author.py, review.py, render_sprites.py,
extract_anchors.py, build_effects.py, render_portrait.py, publish.py, smoke.ps1).

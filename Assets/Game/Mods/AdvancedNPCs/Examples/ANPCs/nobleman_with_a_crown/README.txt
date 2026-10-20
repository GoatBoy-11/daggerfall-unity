# Nobleman with a Crown - replacement for 183_13

ANPC id `nobleman_with_a_crown`; replaces Daggerfall flat archive 183, record 13.
Source: `../_models_for_use/Meshy_AI_Dragon_Crown_Sovereig_1015162230_texture.glb` (unchanged). Editable files:
`nobleman_with_a_crown_rigged.blend` (clean Auto-Rig Pro rig) and `nobleman_with_a_crown_animated.blend` (actions).

The real mesh was merged by distance at 0.0001 units before ARP matching and binding
(12192 -> 8125 vertices). Arm weights were stripped from the torso volume and
torso/leg weights from the sleeves (heat weights leak both ways). The ankle-length robe is 60% hip-rigid with four sway bones; heat weights that leaked from the spine into the puffy sleeves were removed. The crown follows the head rigidly.

Idle follows the flat: left fist on his hip, right forearm held forward, head turned to his right.
Weapon: short sword, attack only.
One action of each kind, 8 fps, Constant interpolation; looping actions omit their closing key:
idle_1 (4), walk (8), run (8), greet_1 (6), talk_1 (6), attack_1 (6, impact marker frame 3: the
body turns with the swing and the head stays on the target), hit_1 (4), death_1 (7, keeps the last pose).

Hit sprays and death blood pools are composited (clean sheets and effect layers in `effect_layers/`).
Standing height 2.025 m from the flat's size and scale. Male Breton Knight, melee,
conversation on; original flat names and portraits are kept on replacements; the custom portrait is
used when spawned directly: `anpc_spawn nobleman_with_a_crown`.

Scripts: `output/coat_men/` (chars.py, rigcfg.py, rig.py, author.py, review.py, render_sprites.py,
extract_anchors.py, build_effects.py, render_portrait.py, publish.py, smoke.ps1).

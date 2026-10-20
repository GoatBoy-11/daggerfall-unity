# Man in a Red Bandana - replacement for 186_36

ANPC id `man_in_a_red_bandana`; replaces Daggerfall flat archive 186, record 36.
Source: `../_models_for_use/Meshy_AI_Red_Bandana_Buccaneer_1015191332_texture.glb` (unchanged). Editable files:
`man_in_a_red_bandana_rigged.blend` (clean Auto-Rig Pro rig) and `man_in_a_red_bandana_animated.blend` (actions).

The real mesh was merged by distance at 0.0001 units before ARP matching and binding
(16331 -> 11476 vertices). Arm weights were stripped from the torso volume and
torso/leg weights from the sleeves (heat weights leak both ways). The bandana and its tails follow the head rigidly (picked by colour).

Idle follows the flat: both arms hanging loose with fists at his sides, wide stance.
Weapon: short sword, attack only.
One action of each kind, 8 fps, Constant interpolation; looping actions omit their closing key:
idle_1 (4), walk (8), run (8), greet_1 (6), talk_1 (6), attack_1 (6, impact marker frame 3: the
body turns with the swing and the head stays on the target), hit_1 (4), death_1 (7, keeps the last pose).

Hit sprays and death blood pools are composited (clean sheets and effect layers in `effect_layers/`).
Standing height 2.05 m from the flat's size and scale. Male Breton Warrior, melee,
conversation on; original flat names and portraits are kept on replacements; the custom portrait is
used when spawned directly: `anpc_spawn man_in_a_red_bandana`.

Scripts: `output/coat_men/` (chars.py, rigcfg.py, rig.py, author.py, review.py, render_sprites.py,
extract_anchors.py, build_effects.py, render_portrait.py, publish.py, smoke.ps1).

# Imperial Knight - imperial_knight

Source: Swords and Sorcery RPG Maker pack, `D:/_Game_Assets/RPG_Maker/SaS/Knight_A` (Daz3D assets, purchased; no attribution needed).
Not a flat replacer: spawns in towns and in Palace, Fighters Guild, Tavern interiors.

Rig: the Daz mesh was baked in its rest pose (output/sas/extract.py) and the Auto-Rig Pro human template was fitted
to the source skeleton's joints (output/sas/rig_sas.py). The artist's Daz skin weights were transferred onto the
ARP bones by name and position (no automatic skinning); fingers keep their own weights. Imperial is not an engine race: runtime race Breton, Imperial names.

Idle: arms down, sword in his right hand, shield on the left arm. One action of each kind at 8 fps, Constant keys: idle_1, walk, run, greet_1, talk_1,
attack_1 (impact frame 3; the body turns with the blow, head on the target), hit_1, death_1 (keeps the last pose).
Eat/drink (Oblivion bread + bottle) from output/arp_meals_batch. Hit sprays and death pools composited (clean sheets +
effect layers kept). Standing height 2.08 m (not shorter than a vanilla Warrior). Male Breton Knight.
Portrait: Nano Banana from the 3D portrait render, Daggerfall face style, snapped to ART_PAL.COL (Models/_portrait_tools).
Direct preview: `anpc_spawn imperial_knight`.

Scripts: output/sas (extract.py, rig_sas.py, skin_tint.py, chain.sh, final.sh, run_blender.ps1) and output/coat_men (author.py,
review.py, render_sprites.py, extract_anchors.py, build_effects.py, render_portrait.py, publish.py, smoke.ps1).

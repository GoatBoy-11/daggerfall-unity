# Dancer - tavern_dancer

Source: Swords and Sorcery RPG Maker pack, `D:/_Game_Assets/RPG_Maker/SaS/Dancer` (Daz3D assets, purchased; no attribution needed).
Not a flat replacer: spawns in towns and taverns.

Rig: the Daz mesh was baked in its rest pose (output/sas/extract.py) and the Auto-Rig Pro human template was fitted
to the source skeleton's joints (output/sas/rig_sas.py). The artist's Daz skin weights were transferred onto the
ARP bones by name and position (no automatic skinning); fingers keep their own weights. 

Idle: hands on her hips. One action of each kind at 8 fps, Constant keys: idle_1, walk, run, greet_1, talk_1,
attack_1 (impact frame 3; the body turns with the blow, head on the target), hit_1, death_1 (keeps the last pose).
Hit sprays and death pools composited (clean sheets + effect layers kept). Standing height 2.03 m (not shorter
than a vanilla Warrior). Female Redguard Bard. Direct preview: `anpc_spawn tavern_dancer`.

Scripts: output/sas (extract.py, rig_sas.py, chain.sh, final.sh, run_blender.ps1) and output/coat_men (author.py, review.py,
render_sprites.py, extract_anchors.py, build_effects.py, render_portrait.py, publish.py, smoke.ps1).

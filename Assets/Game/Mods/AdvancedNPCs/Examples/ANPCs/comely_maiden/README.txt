# Comely Maiden

Replaces vanilla flat 182.11 only. Vanilla name and conversation portrait are preserved; no random spawning rules are added.

Carries the platter, teal bottle, goblet, tankard and green bottle in every living motion. Props fall separately during death. No attack or run.

Eight directions at 8 fps, one action per motion. Actions: idle (6), walk (8), greet (6), talk (6), hit (4), death (7), re-authored 2026-10-17 with `output/coat_men/author_maiden.py` (Constant keys): breathing idle with hip drift and a level tray, the bottle arm hanging naturally as in flat 182.11, a real short stride with hip and skirt sway, a curtsy-nod greet, talk lifting the bottle, the tray tipping on hit, and on death the tray, the items on it (teal bottle, goblet, tankard - split onto their own bones) and her green bottle each fly off separately and land knocked over on the floor (straight legs, the skirt lies flat). Previous blend and sheets: output/comely_maiden/backup_20261017. Welded by distance (0.0001 m) before Auto-Rig Pro matching/binding, then edited shoulder, arm, garment and boot weights.

Blood uses the shared larger defaults: pool width 70% of standing height, height 30% of pool width, penultimate scale 62%. Clean sheets and separate effect layers remain in output/comely_maiden.

Editable source: Models/comely_maiden/comely_maiden_animated.blend. Scripts and reference diagnostics: output/flat_replacements_oct15. Original source GLB is preserved: Meshy_AI_Moonlit_Matron_1013212236_texture.glb.

Validation: front/side key-pose renders, sampled deformation/weight audits, and the actual ANPC definition/sprite parsers. A live gameplay check has not been performed.

Install: copy the character folder and optional _Portraits file into DaggerfallUnity_Data/StreamingAssets/ANPCs. AdvancedNPCs must be enabled.

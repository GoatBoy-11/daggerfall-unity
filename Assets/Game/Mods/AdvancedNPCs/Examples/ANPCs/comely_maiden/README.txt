# Comely Maiden

Replaces vanilla flat 182.11 only. Vanilla name and conversation portrait are preserved; no random spawning rules are added.

Carries the platter, teal bottle, goblet, tankard and green bottle in every living motion. Props fall separately during death. No attack or run.

Eight directions at 8 fps, 256-pixel cell height, one action per motion. Actions: idle, walk, greet, talk, hit, death. Bezier interpolation with clamped handles. Welded by distance (0.0001 m) before Auto-Rig Pro matching/binding, then edited shoulder, arm, garment and boot weights.

Blood uses the shared larger defaults: pool width 70% of standing height, height 30% of pool width, penultimate scale 62%. Clean sheets and separate effect layers remain in output/comely_maiden.

Editable source: Models/comely_maiden/comely_maiden_animated.blend. Scripts and reference diagnostics: output/flat_replacements_oct15. Original source GLB is preserved: Meshy_AI_Moonlit_Matron_1013212236_texture.glb.

Validation: front/side key-pose renders, sampled deformation/weight audits, and the actual ANPC definition/sprite parsers. A live gameplay check has not been performed.

Install: copy the character folder and optional _Portraits file into DaggerfallUnity_Data/StreamingAssets/ANPCs. AdvancedNPCs must be enabled.

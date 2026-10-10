Dunmer citizen / villager

Manual spawn: anpc_spawn dunmer_citizen_1
Male Dunmer, Bard base class, Coward bravery, wander radius 8. Passive townsperson
who flees danger. No ambient spawns enabled initially; edit spawn.count to enable.
Uses the existing dunmer_morrowind name pool and a custom portrait.

No spells. The explicit empty spellbook also disables any inherited class spells;
maxMagicka is zero. cast_1 is included solely for future configuration/use.

14 sheets, 8 directions, 8 fps, 256-pixel cells, 75 animation frames / 600 cells.
idle_1: 4; idle_2: 6; idle_3: 6; walk: 8; run: 6; greet_1: 6;
attack_1: 5; attack_2: 5; hit_1: 3; hit_2: 3; talk_1: 6; talk_2: 6;
death_1: 7; cast_1: 4. Both punches land on frame 3.
Loop-closing duplicate poses are excluded from sheets. Death retains its resting
pose. run, greet and talk are prepared assets for future behaviour triggers;
the current runtime uses idle, walk, attack, hit, cast and death states.

Animation source: Models/dunmer_villager_1/dunmer_citizen_animations.blend.
The original dunmer_villager_1_1.blend is preserved. New animations adapt the rig's
unused locomotion/fall poses and add citizen gestures and unarmed punches.
Native mesh/materials/skinning are preserved. Wrist orientation and controller
reach were corrected for the rig's Child Of constraints. Every pose uses stepped
keys at 8 fps. Feet use the measured boot floor and share one sprite ground row.
Walk/run arm swings are constructed from a shoulder swing and elbow hinge, then
matched to the IK hand and c_arms_pole controllers. This avoids the earlier axial
twist on the backward swing. Hip turns, knee flexion, staggered feet and weight
shifts make the gestures and punches less rigid without increasing frame counts.
Lighting matches the existing ANPC renderer: upper-left key, weak fill,
orthographic camera tilted down 7 degrees, transparent PNG sheets.

Review images and animated previews: output/dunmer_citizen/.

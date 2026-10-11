Khajiit citizen / villager

Manual spawn: anpc_spawn khajiit_citizen_1
Male Khajiit, Bard base class, Coward bravery, wander radius 8.
No automatic spawns enabled initially; edit spawn.count to enable them.
Uses the shared khajiit_skyrim name pool and a custom 64x64 portrait.

No spells. The explicit empty spellbook disables inherited class spells;
maxMagicka is zero. cast_1 remains available for future configuration.

14 sheets, 8 directions, 8 fps, 256-pixel cell height, 75 animation frames
and 600 directional cells. Both punches land on frame 3. Includes the
existing hit blood, growing death pool and yellow casting effects.
run, greet and talk are prepared assets for future behaviour triggers;
the current runtime uses idle, walk, attack, hit, cast and death states.

Source: Models/khajiit_citizen_1/khajiit_citizen_1.blend, khajiit_villager
mesh on the citizen rig. Effects and clean sheets are kept in the source
pack. The installed sheets do not include the individual clean renders.

Tail added: eight-bone skinned mesh with fur-matched grey stripes and stepped
sway in the existing actions. Death settles on the ground beside the body.
Source includes khajiit_villager and khajiit_tail; render both meshes.

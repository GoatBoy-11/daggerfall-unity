# Wood Elf Ranger — wood_elf_ranger_f_1

An original female elven ranger, built from the user's Elven Warrior in Green
model. `wood_elf_ranger_f_1_animated.blend` contains the complete editable ARP
rig, weapons and animations. `wood_elf_ranger_f_1_rigged.blend` is the clean rig.

The source GLB is unchanged. The actual character mesh was merged by distance
at 0.0001 Blender units **before** ARP matching and heat-map binding: 11,886
vertices became 5,121. UV loops and materials remain on the merged mesh.
All vertices have normalized deform weights. Separate hair and tunic bones
handle the loose surfaces. The body retains its original proportions, with a
normalized Blender height of 1.72 units and a game standing height of 2.10 m.

The wooden recurve bow and arrow come from `_models_for_use/_weapons_and_accessories`.
The bow's original static string is replaced with an editable drawn string;
its limbs also flex. A short elven sword and leather quiver are procedural.
All accessory source GLBs are preserved. Weapon controllers keep the bow
attached to the left-hand grip; the arrow follows the drawn string until release.

| Action | Exported frames | Behaviour |
| --- | ---: | --- |
| idle_1 / idle_2 | 4 / 6 | Breathing / scanning the surroundings |
| walk / run | 8 / 8 | Contact, passing, recovery and run flight poses |
| greet_1 | 6 | Friendly raised-hand acknowledgement |
| talk_1 / talk_2 | 6 / 6 | Hand gestures, nods and weight shifts |
| attack_range_1 | 8 | Nock, raise, draw, aim, release, lower |
| attack_1 / attack_2 | 6 / 6 | Diagonal sword cut / horizontal slash |
| hit_1 | 4 | Recoil and recovery |
| death_1 | 7 | Recoil, buckle, kneel, fall, settle face down |

Every action is authored for **8 fps**, with Constant interpolation for the
exported poses. Looping actions have an extra closing key that is excluded
from the sheets. Death retains its last pose. Bow release is on frame 6;
both sword impacts are on frame 3. `sprites/` contains twelve sheets, 600
directional cells, and an eight-direction arrow projectile, with metadata.

References studied: the supplied female front/side walk and side run GIFs,
Daggerfall TEXTURE.481 Light Fighter locomotion, sword and bow poses, the
additional bow-fighter sheet, and Might and Magic VIII's skeleton archer.
The reference contact sheets and structured motion checks are in
`output/wood_elf_ranger_f_1/`.

Hit and death effects are composited from the project's existing transparent
blood assets. New pools use the current larger default: width 70% of standing
pixel height, height 30% of pool width, with a smaller penultimate pool.
Clean sheets and editable transparent effect layers are retained. Older
characters' death sheets are not modified.

The ANPC is a calm, brave female Ranger, using a bow at distance and sword
attacks nearby. It may appear occasionally in cities, taverns and daytime
wilderness encounters. Conversation gestures are enabled; spellcasting is
disabled. No existing flat is replaced. To spawn it manually:

`anpc_spawn wood_elf_ranger_f_1`

The current ANPC parser supports no Wood Elf race token. Its runtime race is
**Breton** as a compatible fallback; the character's model, portrait, display
name and preset identity are Wood Elf Ranger. This does not extend engine race
support.

Production scripts are `rig_wood_elf_ranger_f_1.py`, `ranger_assets.py`,
`anpc_lib.py` and `author_wood_elf_ranger_f_1.py`. Run the rig script in Blender
5.2 with ARP enabled against `output/feline_knight/arp-template.blend`, then
run the author script against the new rigged scene. Rendering, effects,
publication, parser validation and game smoke-test scripts are in the output
folder. The final pack is copied into the mod's examples and DFU testing install.

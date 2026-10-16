# ANPC routines extension

Approved by Oliver: implement work_at, sequence, named spots/occupancy, broader schedule conditions, and play_animation.only_interrupt_at_combat. Follow-up choices: support both area-specific and home-relative spots; try another free matching spot, otherwise use normal AI until a spot becomes free. Full autonomy granted to complete implementation and validation.

work_at: optional current-area position or named spot, animation, facing and run. Existing visual loops/collision movement, interrupted by social/casting/combat, returns to post.

sequence: 1-32 references to reusable leaf packages, per-step overrides, active-time durations; travel_to may complete on arrival. Loop optional, non-loop completion holds. Resume interrupted step; time/load/reload rebuild transient progress. Nested/cyclic sequences rejected in this first version.

Spots: _Spots/<id>.json with position, relativeTo home/area, optional scoped location (region/place/context), tags, capacity and facing. Area mode requires location. Context outdoors/dungeon/b<buildingKey>. Physical post reservations keyed by loaded area plus spot id and translated coordinates. Nearest free match, current-AI fallback while busy, bounded retries for blocked posts. Release on completion/slot changes/combat/death/despawn/disable/reload, no saved leases.

Conditions: shared existing ConditionParser plus NPC opinion/health bounds. Preserve old schedule selection and variable-only API calls. Live schedule facts independent from conversation reaction calculation. Invalid conditions cannot unlock activities.

only_interrupt_at_combat: default false. True keeps selected play-animation through social holds/talk/greetings/calm casts. Player dialogue remains available; pause retains frozen pose. Attacks/flight and Brave threat detection interrupt; death/paralysis and activity selection remain authoritative.

Verify parsing/schema alignment, per-NPC state/progress isolation, motion/facing, reservation capacity/release/retry, interruption/resume, actual dialogue and package regression. Build with local Unity 2019.4, test in DFU_testing, back up changed install files, install the tested bundle and authoring data. Preserve pre-existing local work; no remote push or merge requested.

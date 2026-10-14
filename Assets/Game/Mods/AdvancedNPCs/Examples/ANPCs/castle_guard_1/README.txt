Verdant guard / friendly town guard with a spear (after the classic Daggerfall city guard)

Manual spawn: anpc_spawn castle_guard_1
Male Breton, Knight class, calm (fights back when attacked; attacking him is a
crime), Brave, wander radius 4. No spells. Default Breton names; custom portrait.
Spawns in cities, hamlets and villages: 1-2 per town.

Idle is the Daggerfall city-guard pose: upright, spear planted at his right foot,
tip above his head, left arm relaxed. sprites.json carries "standingHeight" (body
only), so the tall spear does not make him look small (needs Advanced NPCs with
standingHeight support). Attacks: overhand thrust and underhand thrust, both on
frame 3. Includes hit blood, the growing death pool and casting effects.
death_1: his knees give, he kneels, then falls face down flat; the spear lies beside.

Source: Models/verdant_guard. Model: Meshy "Verdant Sentinel"; the spear is original.

AI schedule: 06:00-20:00 normally uses guard_watch_cycle: patrol for 45 active
seconds, then guard the named home_guard_post for 25 seconds, repeating.
Rain (including storms) or snow selects guard_spot at home_guard_post instead.
20:00-06:00: guard the same home-relative post. This is the original spawn
position, so generic guards retain their own posts in every town.
Bravery stays Brave; idle_1 frame zero is held, with a random idle cycle every
6-15 seconds. Combat still interrupts; calm guards return to their post.
Occupied posts use original AI while waiting for a matching free spot.
Wander radius 4 remains the fallback if a package cannot run.

# ANPC personal variables and follow

Approved in chat: boolean/integer defaults per NPC definition, independent per spawned entity; dialogue sets/adds; optional variable conditions on schedules; implement the foundation, then follow.

Storage: additive NpcState.variables dictionary with typed serializable values, deep snapshot/restore. Definition defaults are read without allocating state; explicit false/zero writes are retained. Existing instance persistence rules apply, including random-each-visit state reset. Save format 2 reads versions 0/1.

Dialogue: optional INpcVariableFacts and INpcVariableActions keep existing dialogue interfaces compatible. setVariables/addVariables validate declarations and types; checked integer addition rejects overflow. Condition variables use literals or eq/ne/gt/gte/lt/lte; missing/wrong types never match. Composed dialogue validates nested references per NPC.

Schedules: first eligible conditional entry takes priority; optional time and variables must both match. Otherwise existing unconditional timed/fallback selection applies. Conditional overlap is allowed; old unconditional validation remains. Selection updates on the next brain frame without resetting unrelated active package progress.

Follow: target defaults player, otherwise exact living active ANPC instance id/key in same area. Default distance 2, runDistance 6; arrival and run hysteresis. Existing gait speed/canRun/runSpeed apply. Direct collision movement, interruptions keep brain authority, target loss holds/retries, blocked movement retries after a bounded pause. No doors, area transfer, teleporting or new pathfinder. Does not change bravery.

Opt-in follower_tester: no automatic spawns, vanilla Warrior sprites, Follow me/Wait here dialogue, independent following and times_recruited values. Add schemas, console commands, API documentation, core tests and focused DFU integration tests. Preserve earlier unrelated local work; no commit/push requested.

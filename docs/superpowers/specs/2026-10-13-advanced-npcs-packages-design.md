# Optional AI packages

The user approved building the discussed optional JSON package system on a new branch. Preserve existing uncommitted healing/sprite work.

## First release
Load ANPCs/_Packages/<id>.json, containing type and parameters. Support stand, wander, travel_to and patrol. Each NPC can have schedule entries with a package, optional HH:mm hours and parameter overrides. Timed entries beat one optional all-day fallback. Starts are inclusive; ends exclusive; midnight wrapping is supported. Reject overlaps, duplicate fallbacks and malformed schedules with warnings, retaining the NPC and its original AI.

Package movement overrides wanderRadius/stayNear temporarily, without changing shared definitions or the original home. Combat, flight, greetings, conversations, healing and paused UI take precedence. Select the present schedule after interruptions/time skips. Each NPC has independent transient movement state. On load/area entry, reconstruct the current activity; these built-ins have no persistent side effects.

Positions are metre offsets from the NPC's original home in its area, portable across generic instances. Wander has center/radius/pause, travel_to has target, patrol has points/pauseAtStops. Movement can run. Routes use authored local waypoints and existing collision movement; blocked movement falls back. Automatic pathfinding, area transitions, named anchors, sequences and public C# registration are later stages.

Reload through anpc_reload_packages, diagnose through anpc_info, ship schemas/examples/documentation. Missing packages or folder are ordinary fallback cases. C# 4 compatible. Add every source to dfmod manifest.

## Verification
NUnit: boundaries, midnight, fallback, malformed data, overrides without mutation, independent progress, movement timeout. Run full Core suite, schema checks, installed DFU assembly compilation and DFU runtime compiler/type loading. Runtime smoke checks when feasible.

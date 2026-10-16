Tavern wench

Manual spawn: anpc_spawn wench
07:00-23:00, at least 60% health: tavern_service_cycle repeats 20 active
seconds at home_service_post using existing idle sheets, then 15 active
seconds of tavern_roam. It does not invent a serving animation.
At 30% health or less: return_home takes priority at any hour.
At other daytime health values: tavern_roam. Overnight: return_home.
The named post is the original spawn position, indoors or outdoors.
An occupied post uses original AI until a matching post is free.
Existing town/interior spawn rules, dialogue, sounds and cowardly flight stay active.

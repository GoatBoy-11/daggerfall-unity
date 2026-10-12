Default sounds shared by every ANPC.

step_*.wav / .ogg / .mp3 are the default footsteps: they play for any ANPC whose own Sounds folder has no
step files (with a sprite set on its walk/run sheet frames, otherwise every 0.5 s walking, 0.33 s running).
An ANPC with step files of its own uses only those. "sounds": { "defaultSteps": false } in an npc.json
turns the defaults off for that ANPC. Other sound names here are ignored (logged).

The four shipped here are dirt-ground walking steps by Nox Sound (CC0), see SOUND_CREDITS.txt.

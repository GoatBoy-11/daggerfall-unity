Default sounds shared by every ANPC.

step_*.wav / .ogg / .mp3 are the default footsteps: they play for any ANPC whose own Sounds folder has no
step files (with a sprite set on its walk/run sheet frames, otherwise every 0.5 s walking, 0.33 s running).
They play 15% quieter than an ANPC's own steps.
An ANPC with step files of its own uses only those. "sounds": { "defaultSteps": false } in an npc.json
turns the defaults off for that ANPC.

eat_* and drink_* play while an ANPC runs the "eat" package, once at the start of each eat / drink animation cycle
(never over the previous one). An ANPC's own Sounds/eat_* or drink_* files replace these for that pool.
Other sound names here are ignored (logged).

va_<name>/ folders are voice packs: pool files (greet_1.ogg, attack_2.ogg, cm_bark_1.ogg, ...) shared by every ANPC
whose npc.json names them: "sounds": { "voice": "va_warrior_male" }, or a list to pick one per person. An ANPC's own
Sounds pool replaces the pack's pool. Tools~/voice_packs.py builds the packs here from the downloaded originals.

The four steps are dirt-ground walking steps by Nox Sound (CC0); eat and drink are CC0 clips from OpenGameArt.
See SOUND_CREDITS.txt.

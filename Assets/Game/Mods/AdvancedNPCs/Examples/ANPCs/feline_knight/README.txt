Feline Knight / Khajiit knight-errant with a longsword

Manual spawn: anpc_spawn feline_knight
Male Khajiit, Knight class, calm (fights back when attacked), Brave, wander
radius 6, 1.1x health ("combat": { "health": 1.1 }). No spells. Khajiit names
(khajiit_skyrim list); custom portrait.
Spawns in cities (0-1 each), sometimes in taverns and Knightly Order halls (15%),
and rarely on the road by day (3% per wilderness check, at most 1).

14 sheets, 8 directions, 8 fps, 256-pixel cells. sprites.json carries
"standingHeight" (body only), so the raised sword never makes him look small.
His tail is rigged: it sways at idle, swings while walking, streams back on the
run, bristles when hit and drapes to the floor when he dies. Attacks: overhead
diagonal cut and horizontal slash, both landing on frame 3. Greet: a knight's
sword salute. Includes hit blood, the growing death pool and casting effects.
death_1: his knees buckle, he kneels, pitches forward and lies face down flat;
the sword lies beside his right hand.

Source: Models/feline_knight (README there). Model: Meshy "Feline Knight";
sword: Oblivion HD steel longsword geometry and texture (personal use only).

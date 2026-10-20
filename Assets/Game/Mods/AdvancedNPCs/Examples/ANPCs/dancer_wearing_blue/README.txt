Dancer Wearing Blue — replacement ANPC 181.5 (181_5)

Dance schedule: 06:00–23:00 using play_animation, animation dance. Stand overnight.
Social interruptions remain enabled: she can greet and talk, then resume dancing.
Normal bravery; melee attacks only. Her sole spell is Heal for herself during combat.
100 magicka, 6–10 second casting cooldown, recovery over 8 game hours of rest.
Vanilla names and conversation portraits remain in use. No random spawning rules.

11 transparent sheets, 8 directions, 256-pixel cells, 8 fps: attack_1, attack_2,
cast_1, dance, death_1, hit_1, idle_1, run_1, walk_1, talk, greet.
Fourteen discrete dance frames (1.75 seconds), exported from the source dance_1.
Walk/run omit the repeated closing frame, giving nine frames each.
Talk and greet use the user-polished actions from dancer_wearing_blue_anpc.blend.
Both retain all ten authored frames at 8 fps, rendered in eight directions.
cast_1 is used for self healing, with a glow following the raised left hand.
Hit blood spray and expanding death blood pools are composited into game sheets.

Install the ANPCs folder into DaggerfallUnity_Data/StreamingAssets/. Requires
Advanced NPCs with play_animation and configured spellcasting support enabled.
The shared play_animation/stand packages are included, matching the project defaults.
Restart the game to pick up the new NPC definition and schedule.

Editable blend: Models/dancer_wearing_blue/dancer_wearing_blue_anpc.blend.
The requested dancer_wearing_blue_dance_3.blend and its nine actions are preserved.
Clean sheets, effect layers, manifests, previews and rebuild scripts are retained
in output/dancer_wearing_blue; generated effect layers also accompany the model.
Validation covers all source poses, raised-hand timing, all 896 directional cells,
the real ANPC/sprite/package parsers, and every minute of the daily schedule.

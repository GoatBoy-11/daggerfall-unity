Mages Guild bald mage / stand-in for Daggerfall's own bald mage (flat 177.2)

"replaces": ["177.2"]: he takes the place of every vanilla bald-man flat (Mages Guild
halls and elsewhere). Clicking him opens that NPC's own talk, quests and guild
services; the vanilla face and name are kept (replacePortrait and replaceName false). He stays
within 2 m of the flat's spot when calm. No spawn block: he never appears otherwise.
Calm Redguard Mage, Prudent: fights back when attacked. Reads a floating spell book.

Sheets (8 directions, 8 fps): idle_1 (arms folded, reading, a page turns),
idle_2 (looks up and around), walk (arms folded, the book floating beside him),
run (book in his left hand), greet_1 (looks up, nods, raises a hand), talk_1,
attack_1 (one frame: an overhead smash with the closed book, actionFrame 1),
attack_range_1 (hurls a fireball, actionFrame 3), cast_1, hit_1, death_1 (the book
drops; he ends face down). projectile.png: an 8-frame fireball (4-10 damage a hit).
Spells: Fireball, Wizard's Fire, Resist Fire (100 magicka).

Source: Models/mages_guild_bald_man (README there). Model: the user's
mages_guild_bald_man.glb; the book and the fireball are original (procedural).

# Advanced NPCs Framework [ANPC] — Creator Manual

The framework adds no people by itself. It reads **ANPCs** (Advanced NPCs) from folders you or bundle
authors provide: persistent, data-defined people with their own sprites, sounds, dialogue, routines and
combat. This manual shows how to make one and lists every setting.

Requires Daggerfall Unity 1.9.2.

---

## 1. Quick start

1. Install the framework and enable **Advanced NPCs** in DFU's mod list.
2. Create `DaggerfallUnity_Data/StreamingAssets/ANPCs/town_drunk/npc.json`:

   ```json
   {
     "names": ["Old Bertram", "Halvard the Sot"],
     "baseClass": "Bard",
     "bravery": "Coward",
     "spawn": { "locationTypes": ["TownCity", "TownVillage"], "count": [1, 2] }
   }
   ```

3. Start the game, walk into a town, open the console (`~`) and type `anpc_list`. Your drunk is there,
   using the vanilla Bard sprite and a vanilla face.
4. Problems? Open `Player.log` and search for `[AdvancedNPCs]`. Every problem names the folder, file and
   field, and misspelt fields get a suggestion.

Everything else (sprites, sounds, dialogue, schedules) is optional and added to the same folder.

**Commented examples** ship with the framework. They never spawn on their own; try them with
`anpc_spawn <name>` and copy one as a starting point:

| Example | Shows |
|---|---|
| `example_townsfolk` | calm generic citizen: names, spawning, bravery, dialogue, conversation, sounds, schedule |
| `example_bandit` | hostile human: combat tuning, bow, outside-town spawn blocks (commented) |
| `example_creature` | creature enemy (Orc) made weaker and quicker, throwing |
| `example_mage` | spellcaster with healing |
| `example_mercenary` | hireable companion |
| `example_guard` | guard holding a post |
| `_Dialogue/example_dialogue.json` | greetings, conditional answers, flags, replies, actions |
| `_Examples/example_unique/` | unique ANPC and a `sprites.json`; inside a `_` folder, so never loaded — copy it out to use |

A generic ANPC with `"spawn": { "count": [0, 0] }` (and no dungeon/interior/wilderness blocks) only appears
through `anpc_spawn`. Unique ANPCs always spawn at their spot.

---

## 2. Folder layout

```
StreamingAssets/ANPCs/
  _Portraits/     shared portrait PNGs
  _Namelists/     custom name lists
  _Dialogue/      shared dialogue types
  _Packages/      shared AI routines
  _Spots/         named places for routines
  _Sounds/        default footsteps, eat/drink sounds and va_* voice packs
  town_drunk/     one folder per ANPC; the folder name is its id
    npc.json        required
    dialogue.json   optional: topics only this ANPC has
    sprites/        optional: sprite set (sprites_1/, sprites_2/ … for several)
    Sounds/         optional: voice and step sounds
```

- Folder names: lowercase letters, digits and `_`. **Never rename a folder after release** — the name keys
  save data.
- Folders starting with `_` are shared data, not ANPCs.
- Unknown fields are reported in `Player.log` and ignored.
- Comments are allowed in every ANPC JSON file: `// to the end of the line` and `/* blocks */`.
- Editor autocomplete: copy `Schemas~/` to `ANPCs/_Schemas/` and add
  `"$schema": "../_Schemas/npc.schema.json"` at the top of an `npc.json`
  (`../../_Schemas/sprites.schema.json` inside a sprites folder).

---

## 3. Two kinds of ANPC

**Generic** (default): a template that makes several different people per town, alongside vanilla
citizens. Name, portrait, sprite set and gender are rolled per person.

**Unique**: one named person living at one exact spot.

1. Stand where they should live, open the console and type `anpc_pos`.
2. Paste the two lines it prints:

   ```json
   {
     "kind": "unique",
     "name": "Sir Aldric",
     "location": { "region": "Daggerfall", "place": "Daggerfall" },
     "position": [12.5, 1, -3.25]
   }
   ```

3. To move them later, stand at the new spot and type `anpc_place <id>`.

All other fields work for both kinds.

---

## 4. `npc.json` reference

Every field is optional unless marked.

### Identity

| Field | Default | Values |
|---|---|---|
| `kind` | `generic` | `generic`, `unique` |
| `name` | generated | any text (**required** for unique) |
| `names` | — | generic: list, one picked per person |
| `nameList` | the race's | list name in `_Namelists/`, or `default_breton`, `default_redguard`, `default_nord`, `default_darkelf`, `default_highelf`, `default_woodelf`, `default_khajiit`, `default_imperial` |
| `race` | the region's | `Breton`, `Redguard`, `Nord`, `DarkElf`, `Khajiit`, `Argonian` |
| `gender` | random per person | `Male`, `Female` |
| `portrait` / `portraits` | vanilla face | portrait name(s) from `_Portraits/` (see §7) |
| `baseClass` | `Spellsword` | Mage, Spellsword, Battlemage, Sorcerer, Healer, Nightblade, Bard, Burglar, Rogue, Acrobat, Thief, Assassin, Monk, Archer, Ranger, Barbarian, Warrior, Knight — or a creature (hostile only, see Enemies) |

The base class decides stats, skills, the vanilla sprite and its default height.

### Spawning (generic)

| Field | Default | Meaning |
|---|---|---|
| `spawn.locationTypes` | `TownCity`, `TownHamlet`, `TownVillage` | town types they appear in |
| `spawn.count` | `[1, 3]` | people per town (0–20) |
| `spawn.places` | all towns | only these: `[{ "region": "…", "place": "…" }]`; add `"positions": [[x,y,z], …]` from `anpc_pos` for fixed spots |
| `spawn.dungeons` | — | `{ "chance": 25, "count": [2, 4], "dungeonTypes": [...], "when": {...} }` |
| `spawn.interiors` | — | `{ "buildings": ["Tavern"], "chance": 60, "count": [1, 2], "when": {...} }` |
| `spawn.wilderness` | — | `{ "chance": 5, "max": 2, "group": [1, 1], "terrain": "dry", "when": {...} }` — rolled every 10 game minutes, appears 40–80 m away out of sight; `max` 1–20; `group` [min, max] 1–8 appear together; `terrain` `dry`, `grass`, `rocky`, `nearWater` |

- `chance` 0–100 per visit; `count` 1–10 outside towns.
- **dungeonTypes**: Crypt, OrcStronghold, HumanStronghold, Prison, DesecratedTemple, Mine, NaturalCave, Coven,
  VampireHaunt, Laboratory, HarpyNest, RuinedCastle, SpiderNest, GiantStronghold, DragonsDen,
  BarbarianStronghold, VolcanicCaves, ScorpionNest, Cemetery (omit for all).
- **buildings**: Alchemist, Armorer, Bank, Bookseller, ClothingStore, FurnitureStore, GemStore, GeneralStore,
  Library, PawnShop, WeaponSmith, Temple, Tavern, Palace, House, GuildHall, Fighters Guild, Mages Guild,
  Thieves Guild, Dark Brotherhood, Knightly Order.
- `when` takes the conditions in §9 (not `asked`, `notAsked`, `tone`, `reaction`).
- A template with only dungeon/interior/wilderness blocks never appears in towns; add `locationTypes` too.
- Indoors, people wander at most 3 m. `anpc_here` explains who can appear where you stand and why.

### Behaviour

| Field | Default | Meaning |
|---|---|---|
| `attitude` | `calm` | `calm` townsfolk, `hostile` enemy |
| `bravery` | `Normal` | `Coward` always flees · `Prudent` flees below `fleeHealthPercent` · `Normal` below half of that, rolls `fleeChance` once · `Brave` never flees and attacks enemies it sees |
| `fleeHealthPercent` | `25` | 1–99 |
| `fleeChance` | `30` | 0–100 |
| `aggroRange` | `15` | 1–50 m; how far Brave ANPCs and guards look for targets |
| `calmDownHours` | `[6, 48]` | game hours before an angered ANPC forgives you |
| `crimeOnAttack` | `true` | attacking is assault, killing is murder |
| `wanderRadius` | `8` | metres around home; `0` stands still |
| `conversation` | off | `true` or `{ "enabled": true, "chance": 50 }` — chats with other ANPCs (see §8) |
| `guard` | `false` | `true`: arrests wanted players (DFU surrender/court) and hunts NPCs reported for crimes |

Calm ANPCs fight only whoever attacked them. Nearby allies (same building, spawn group or replaced
faction) join in within 20 m if they see it, 8 m if they hear it. Each ANPC keeps a personal opinion of
you (−100 to 100) that drifts back to 0.

### Enemies

`"attitude": "hostile"` attacks on sight with DFU's enemy AI, never calms down on its own, killing it is no crime.

- Language skill: when it first sees you, DFU's pacification roll (Etiquette, Streetwise for thieves,
  creature languages such as Orcish) may calm it for good. Hit it later and it is an enemy again, no crime.

- `"hostileHours": [20, 6]` — hostile only from 20:00 to 06:00, calm townsfolk otherwise.
- A hostile ANPC may use a **creature** `baseClass` (Rat, Imp, Spriggan, GiantBat, GrizzlyBear,
  SabertoothTiger, Spider, Orc, Centaur, Werewolf, Nymph, Slaughterfish, OrcSergeant, Harpy, Wereboar,
  SkeletalWarrior, Giant, Zombie, Ghost, Mummy, GiantScorpion, OrcShaman, Gargoyle, Wraith, OrcWarlord,
  FrostDaedra, FireDaedra, Daedroth, Vampire, DaedraSeducer, VampireAncient, DaedraLord, Lich, AncientLich,
  Dragonling, FireAtronach, IronAtronach, FleshAtronach, IceAtronach, Dragonling_Alternate, Dreugh, Lamia).
  It gets that creature's stats, attacks and sounds.

### Combat tuning

```json
"combat": { "health": 0.7, "damage": 0.7, "attackSpeed": 1.3, "rangedAtDistance": true,
            "rangedAnimation": "attack_ranged_1", "projectileDamage": [2, 4], "runSpeed": 1.2, "canRun": true }
```

| Field | Default | Meaning |
|---|---|---|
| `health` | `1` | multiplier on DFU's rolled health (0.05–10) |
| `damage` | `1` | multiplier on melee damage, creatures only (0.05–10) |
| `attackSpeed` | `1` | swing rate multiplier (0.25–4) |
| `rangedAtDistance` | `false` | humans shoot a bow, creatures throw, when the target is far |
| `rangedAnimation` | random `attack_range*` | sheet a shot plays |
| `projectileDamage` | DFU's | `[min, max]` or one number, 1–1000 |
| `runSpeed` | `1.2` | run speed vs walk (1–2) |
| `canRun` | `true` | `false`: always walks |

### Magic

```json
"magic": { "spells": ["Fireball", "Heal", "Shield"], "maxMagicka": 150, "castCooldown": [4, 7] }
```

| Field | Default | Meaning |
|---|---|---|
| `spells` | **required** | standard DFU spell names; replaces the class's spells; `[]` disables. `anpc_spells` lists them |
| `usesMagicka` | `true` | `false`: unlimited casting |
| `maxMagicka` | `150` | 0–1,000,000 |
| `castCooldown` | `[4, 7]` | seconds between casts (1–3600) |
| `recovery` | `onAreaEntry` | refill on each new visit, or `rest` (refill over `restHours`) |
| `restHours` | `8` | with `rest`: hours to refill an empty pool |
| `skill` | `80` | magic school skill 1–100 (affects cost) |
| `healing` | self only | `{ "allies": true, "player": "friendly", "outsideCombat": true, "healthThreshold": 0.6 }` — heal allies / a liked player within 2.5 m |

Casters cast while fighting and heal themselves below 60 % health. With `healing.outsideCombat` they also
heal others when calm.

### Companions

| Field | Default | Meaning |
|---|---|---|
| `recruitCost` | — | gold to hire (0–1,000,000); adds hiring dialogue. Hired people follow, wait, fight by stance, trade belongings and travel with you |
| `no_companion_slot` | `false` | does not count toward the **Max followers** setting |

Companion barks come from `cm_*` sound pools (see §6).

### Replacing vanilla NPCs (generic only)

| Field | Default | Meaning |
|---|---|---|
| `replaces` | — | `"177.2"` (flat texture archive.record) or `"faction:1003"`, one or a list |
| `replacesFlat` | — | one vanilla person by the id `anpc_flat` prints (`"1234/b5678/-90"`), one or a list; wins over `replaces` |
| `replacePortrait` | `true` | show the ANPC's own PNG portrait |
| `replaceName` | `false` | show the ANPC's name (quest text keeps vanilla) |
| `stayNear` | `0` | keep a calm stand-in within this many metres of its spot |

The vanilla flat is hidden and the ANPC stands on its spot. Clicking it does exactly what the vanilla NPC
did: shops, quests, guild services. Killing the stand-in kills that shopkeeper too. Without a `spawn`
block the ANPC appears only as a stand-in.

### Size

| Field | Default | Meaning |
|---|---|---|
| `spriteHeight` | vanilla class height | standing height in world units |
| `spriteScale` | `1` | scale width and height together (0.2–2) |
| `spriteHeightScale` | `1` | scale height only, e.g. `0.94` = 6 % shorter |

Rule of thumb: keep people no shorter than DFU's Warrior (~2.03 m). The spawn line in `Player.log` shows the
final height.

### Animals

`"animal": { "temperament": "prey", "activity": "crepuscular" }` makes an animal: no talk window ("*Name* ignores
you."), no hiring, guard duty or conversations; bravery from the temperament (fighters Prudent, others Coward).

| Field | Default | Meaning |
|---|---|---|
| `temperament` | `grazer` | `prey` (flees), `grazer` (looks, shies away), `territorial` (warns, then attacks), `predator` (attacks close, or on `boldChance`), `ambusher` (strikes close), `ambient` (scatters) |
| `activity` | `always` | `diurnal` 06–20, `nocturnal` 20–06, `crepuscular` 04–09 + 17–22; rests otherwise (`sleep*`/`rest*`/`idle`) |
| `senses` | by temperament | `{ "alert": 25, "react": 15 }` metres; sneaking halves, running ×1.5, Stealth up to half off |
| `territory` | by temperament | metres from home a chase may go (0 = until the player is far) |
| `warnSeconds` | `3` | territorial warning before the attack |
| `boldChance` | `10` predators | % chance to attack a player it just noticed |
| `calls` | `[45, 150]` s | a `call` sound now and then within 30 m; `false` = never |
| `hunts` | — | temperaments, ids or `id_*` prefixes it hunts when hungry (`aggroRange` = search range) |
| `hungerHours` | `[4, 10]` | game hours between hunts |
| `herd` | `true` | group members wander around their leader |
| `attacks` | `true` | `false`: never attacks unprovoked (keeps warning / watching), bites back when hit |

- Calm animals may use the creature classes `Rat`, `GiantBat`, `GrizzlyBear`, `SabertoothTiger`, `Spider`,
  `GiantScorpion`; no `"attitude": "hostile"`.
- `crimeOnAttack: true` = livestock: hit no crime, kill = Theft unless it attacked first.
- Farm and town animals have their own count under the per-town limit.
- `"disease": { "chance": 5, "diseases": ["Plague"] }` (any ANPC): its melee hits may pass on a DFU disease.

### Other sections

- `dialogue` — dialogue type(s) from `_Dialogue/`: `"tavern_wench"` or `["gossip", "tavern_wench"]` (§9).
- `sounds` — sound settings (§6).
- `schedule`, `variables` — routines and per-person values (§10).

---

## 5. Sprites

Without a `sprites/` folder the ANPC uses its base class's vanilla sprite.

### Sheet format

One PNG per animation. **Columns = frames** (left to right), **rows = 8 directions** (top to bottom):
front, front_right, right, back_right, back, back_left, left, front_left. Each cell is
`cellWidth × cellHeight`, so a sheet is `cellWidth × frames` wide and `cellHeight × 8` tall.

```
ANPCs/my_npc/sprites/
  sprites.json
  idle_1.png  idle_2.png  walk_1.png  attack_1.png  hit_1.png  death.png
```

```json
{
  "pixelsPerUnit": 64.6,
  "cellHeight": 256,
  "groundY": 32,
  "fps": 8,
  "animations": {
    "idle_1":   { "cellWidth": 104, "frames": 10 },
    "walk_1":   { "cellWidth": 120, "frames": 8, "stepFrames": [2, 6] },
    "attack_1": { "cellWidth": 168, "frames": 4, "actionFrame": 3 },
    "hit_1":    { "cellWidth": 120, "frames": 3 },
    "death":    { "cellWidth": 344, "frames": 6 }
  }
}
```

| Field | Meaning |
|---|---|
| `cellHeight` | pixel height of one cell (all sheets) |
| `groundY` | pixel row of the feet, counted from the cell's bottom |
| `fps` | animation speed |
| `pixelsPerUnit` | source scale; in-game size comes from §4 Size, this mainly sets projectile scale |
| `standingHeight` | optional: body height in pixels above `groundY`, ignoring a raised weapon |
| `deathStatic.groundY` | optional: ground row of `death_static.png` |
| `cellWidth`, `frames` | per animation, **required** |
| `actionFrame` | attack sheets: frame (from 1) the blow lands / arrow leaves |
| `stepFrames` | walk/run sheets: frames (from 1) with a footstep (default first and middle) |

Sheet sizes must match `sprites.json` (checked at startup). PNGs should be 8-bit, non-interlaced for
stutter-free background loading.

### Animation names

Numbered names (`idle_1`, `idle_2`) are variants picked at random. At least one `idle` sheet is required.

| Name | Used for | Missing → |
|---|---|---|
| `idle*` | standing | **required** |
| `walk*` / `run*` | moving | each other, then idle |
| `attack*` | melee | idle |
| `attack_range*` / `attack_ranged*` | bow shot / throw | attack |
| `cast*` | spellcasting | attack, then none |
| `heal*` | healing | cast, attack |
| `hit*` | hurt | idle |
| `block*` | an attack on it did no damage (miss/parry, or a full shield block with Physical Combat and Armor Overhaul) | no block |
| `greet*` | greeting the player/ANPCs | no greeting |
| `talk*` | ANPC conversations | idle |
| `death*` | dying; last frame stays as the corpse | — |
| `death_static.png` | optional single-picture corpse | last death frame |
| `projectile*.png` | what a ranged attack flies | DFU arrow |
| any other name | used by `play_animation` / `work_at` routines | idle |

- **Turret**: a set with no `walk*` or `run*` never moves but still turns, attacks, shoots and casts.
- **Several looks**: `sprites_1/`, `sprites_2/` … — each person gets one.
- **Projectiles**: `projectile.png` (one frame) needs no entry; a sheet needs
  `"projectile": { "cellWidth": 52, "cellHeight": 52, "frames": 8 }`. One row = billboard, eight rows =
  directional.
- **Rendering from Blender**: `Tools~/render-sprites.py` renders a rigged, animated `.blend` (one Action
  per sheet) and writes `sprites.json`, including `actionFrame` from a pose marker named `action`:
  `blender -b character.blend --python render-sprites.py -- <out_dir> [test]`.

---

## 6. Sounds

Put `.wav`, `.ogg` or `.mp3` files in `ANPCs/<id>/Sounds/`. The file name says when it plays; a trailing
`_<number>` adds it to a random pool (`attack_1.wav`, `attack_2.wav`). Sounds work with vanilla sprites too.

| Pool | Plays |
|---|---|
| `greet` | greeting (`greetChance`) |
| `attack`, `attack_range`, `cast`, `heal`, `hit` | by `combatChance` |
| `death` | always |
| `step` | every footfall within `stepDistance` |
| `eat`, `drink` | each eat / drink cycle of the `eat` package; never over the previous one |
| `hawk` | each call of the `hawk` package |
| `alert`, `warn`, `call` | animals: noticing the player, a territorial warning (every 1.5 s), now and then; no chance roll; else `greet` |
| `talk` (`talk_male` / `talk_female`) | conversation lines (required for conversations) |
| `halt` | guard starting a pursuit |
| `cm_bark`, `cm_dungeon`, `cm_inn`, `cm_hurt`, `cm_no_magicka` | hired companion chatter |
| `cm_recruit`, `cm_wait`, `cm_follow`, `cm_dismiss`, `cm_regroup`, `cm_combat`, `cm_victory`, `cm_push` | hired companion event lines |

Without its own `step`, `eat` or `drink` files, an ANPC uses the shared `_Sounds` ones (`step_*`, `eat_*`, `drink_*`).

**Voice packs**: a folder `_Sounds/va_<name>/` holds pool files like a `Sounds` folder (any pool). ANPCs pick one with
`"voice"` in `"sounds"`, so many ANPCs share one voice without copies. A list picks one per person (same person, same
voice); packs ending in the other gender's `_male` / `_female` are skipped for that person. Order: the ANPC's own `Sounds` pool, else the pack's pool, else the `_Sounds` defaults, else vanilla. An own
pool replaces the pack's whole pool (own `attack_1.ogg` means none of the pack's attacks). Unknown pack names are
logged; the ANPC keeps its own and vanilla sounds.

```json
"sounds": { "voice": "va_warrior_male" }
"sounds": { "voice": ["va_commoner_female", "va_townswoman_female", "va_commoner_male"] }
```

```json
"sounds": { "combatChance": 50, "greetChance": 50, "stepDistance": 15 }
```

| Field | Default | Meaning |
|---|---|---|
| `combatChance` | `50` | % chance of attack/cast/heal/hit sounds |
| `greetChance` | `50` | % chance of a greet sound |
| `stepDistance` | `15` | metres; no footsteps beyond |
| `soundPitch` / `soundPitchVariation` | `1` / `0` | voice pitch (0.25–3) and random ± (0–1) |
| `stepPitch` / `stepPitchVariation` | `1` / `0` | footstep pitch and random ± |
| `defaultSteps` | `true` | use `_Sounds` steps when the ANPC has none |
| `voice` | none | voice pack name, or a list (one picked per person) |
| `barkMinSeconds` / `barkMaxSeconds` | `45` / `90` | companion chatter interval |

`.ogg` files must be audio only (no embedded cover art).

---

## 7. Portraits and names

**Portraits**: PNGs in `_Portraits/`, 64 × 64 recommended. `"portrait": "bram"` uses `bram.png` and every
`bram_<n>.png` as a pool; each person keeps the face they first showed. No portrait = vanilla face for the
race and gender.

**Name lists**: `_Namelists/<name>.json`, used with `"nameList": "<name>"`. Simple format:

```json
{ "male": ["Ragnar", "Ulf"], "female": ["Astrid", "Sigrid"], "surnames": ["Stormborn", "Ironhand"] }
```

Optional `maleSurnamePrefix` / `femaleSurnamePrefix` (e.g. `gro-` / `gra-`). A bank copied from DFU's
`NameGen.txt` plus a `"style"` also works (see `_Namelists/README.txt`). Names starting with `default_`
are reserved.

---

## 8. Conversations

With `"conversation": true` on both, two calm ANPCs (or two idle enemies) that greet each other sometimes
stop and chat in turns, playing `talk` sounds and `talk*` sheets — no text, like Gothic's townsfolk.
`chance` (default 50) is the chance after a greeting. Needs `talk` sound files and the **ANPC sounds**
setting on *ANPC*. Test with `anpc_chat`.

---

## 9. Dialogue

ANPCs add their own **Tell Me About** topics to DFU's talk window.

1. Create `_Dialogue/tavern_wench.json` (the file name is the dialogue type):

   ```json
   { "topics": [ { "caption": "The house ale", "answers": ["Two coppers a mug."] } ] }
   ```

2. Add `"dialogue": "tavern_wench"` to any `npc.json`.
3. Edit, then type `anpc_reload_dialogue` — no restart needed.

A folder's own `dialogue.json` (same format) adds topics only that ANPC has.

### Topic fields

| Field | Meaning |
|---|---|
| `caption` | **required**; list text, under 24 characters |
| `answers` | **required**; one text, a list (random pick), or `{ "when": {...}, "text": "..." }` objects (matching ones win) |
| `when` | conditions to list the topic |
| `question` | player's line: one text or `{ "polite", "normal", "blunt" }` |
| `id` | name for references (default: caption in lowercase, `_` for spaces) |
| `sets` / `clears` | game-wide flags to set / clear |
| `once` | `true`: disappears after asking |
| `replies` | player choices after the answer: `{ "text", "answers", "when", "replies", …actions }`, nested up to 8 deep |

`"greetings": [ ... ]` (answer format) replaces the vanilla greeting when one qualifies.
Placeholders: `{player}`, `{npc}`, `{town}`, `{region}`, `{topic}`.

### Actions (on topics, answers, replies)

| Action | Effect |
|---|---|
| `giveGold` / `takeGold` | 1–100,000 gold (take-lines only show if the player can pay) |
| `giveItem` / `takeItem` | one item by name |
| `reputation` | regional reputation −20 to 20 |
| `opinion` | this ANPC's opinion −50 to 50 |
| `startQuest` | DFU quest file name, e.g. `"A0C00Y00"` |
| `becomeEnemy` | closes talk; ANPC turns hostile |
| `endConversation` | closes talk after the line |
| `setVariables` / `addVariables` | change personal variables (§10) |

### Conditions (`when`)

All must hold; use `"any": [ {...}, {...} ]` for OR. Values may be one item or a list.

| Condition | Example |
|---|---|
| `hours` | `[20, 6]` (whole hours, end excluded) |
| `time` | `"day"` / `"night"` |
| `indoors` / `outdoors` | `true` |
| `season` | `["autumn", "winter"]` |
| `weather` | `clear`, `overcast`, `rain`, `storm`, `snow` |
| `bad_weather` | `true` (rain, storm or snow) |
| `region` / `town` | `"Daggerfall"` |
| `climate` | `["woodlands", "mountainwoods"]` (`ocean`, `desert`, `desert2`, `mountain`, `rainforest`, `swamp`, `subtropical`, `hauntedwoodlands`) |
| `minLevel` / `maxLevel` | `5` |
| `playerRace` / `playerGender` | `"Dark Elf"` / `"Female"` |
| `minGold` / `hasItem` | `500` / `"Ruby"` |
| `guild` + `minGuildRank` | `"Fighters Guild"`, rank 0–10 |
| `reaction` | `dislikes`, `neutral`, `likes`, `loves` (region + personal opinion) |
| `minOpinion` / `maxOpinion` | −100 to 100 |
| `minHealth` / `maxHealth` | 0–100 % |
| `asked` / `notAsked` | topic caption or id (`"Topic/Reply"` for replies) |
| `flags` / `notFlags` | `"heard_sailor"` |
| `questGlobal` / `notQuestGlobal` | `"LiftedCurse"` |
| `variables` | `{ "jobs": { "gte": 3 } }` (`eq`, `ne`, `gt`, `gte`, `lt`, `lte`) |
| `tone` | answers only: `polite`, `normal`, `blunt` |

A condition with a typo never holds, so it hides the topic. `anpc_topics` shows why each topic is hidden.

---

## 10. Schedules and routines

Without a schedule an ANPC wanders around home. A schedule picks a **package** (routine) by time and
conditions; combat, greetings and talk still interrupt it.

```json
"schedule": [
  { "package": "seek_shelter", "when": { "outdoors": true, "bad_weather": true } },
  { "hours": ["08:00", "18:00"], "package": "local_patrol" },
  { "hours": ["18:00", "23:00"], "package": "local_wander", "parameters": { "radius": 3 } },
  { "package": "stand" }
]
```

- `hours`: exact `HH:mm`, start included, end excluded; may cross midnight.
- Priority: first matching `when` entry → timed entry → the one entry without hours (all-day fallback).
  No match = normal wandering. Timed entries without `when` must not overlap.
- `parameters` override the package's defaults for this ANPC only.
- Edit, then `anpc_reload_schedules`. `anpc_schedule` explains which entry is active and why.

### Packages

A package is a file `_Packages/<name>.json`; schedules reference it by file name (`"package": "stand"`
needs `_Packages/stand.json`):

```json
{ "type": "wander", "parameters": { "radius": 6, "pause": 5 } }
```

Positions are `[x, y, z]` metre offsets from the ANPC's home unless noted.

| Type | Parameters |
|---|---|
| `stand` | — |
| `wander` | `center`, `radius` (0–50, def 8), `pause` (game s, def 3), `run` |
| `wander_civilian` | `center`, `radius` (def 40): roams town roads like vanilla citizens, stops for idle/pray/dance/emote sheets |
| `travel_to` | `target` **required**, `run` |
| `patrol` | `points` **required** (2–32), `pauseAtStops` (def 10), `run` |
| `guard_spot` | `position` (area coordinates from `anpc_pos`) or `spot`, `idleInterval` (def `[6, 15]`); acts Brave |
| `work_at` | `position` or `spot`, `animation` (def `idle`), `facing` (0–360), `run` |
| `play_animation` | `animation` (def `idle`), `only_interrupt_at_combat` |
| `follow` | `target` (def `player`, or an ANPC key), `distance` (def 2), `runDistance` (def 6) |
| `seek_shelter` | `radius` (def 16), `spot` (def `shelter`), `run` |
| `eat` | `mealCycles` (def `[2, 5]`), `breakCycles` (def `[1, 3]`): stays put, plays bouts of random `eat*`/`drink*` cycles with `idle` breaks; only idle without eat/drink sheets |
| `chore` | `center`, `radius` (def 4), `animation` (def `sweep`, `chop`, `scrub`, `work`), `cycles` (def `[2, 4]`), `run`: walks to random nearby spots and plays chore sheets a few cycles at each |
| `hawk` | `range` (def 6), `interval` (def `[10, 20]` s), `animation` (def `hawk`): stays put, faces the passing player and calls out (`hawk_*` sounds; `hawk*` sheet, else `greet*`, else `talk*`) |
| `socialize` | `radius` (def 15), `interval` (def `[20, 40]` s): walks up to a nearby ANPC that can talk and starts a conversation (both need conversation on and talk sounds) |
| `sequence` | `steps` **required** (≤ 32: `{ "package", "parameters", "duration" }`), `loop` |

Record patrol routes by walking them: `anpc_route start <key>`, `anpc_route add` at each point,
`anpc_route finish` prints the package JSON.

### Named spots

`_Spots/<id>.json` marks places that routines can reserve (`"spot": "<id or tag>"`):

```json
{ "relativeTo": "home", "position": [1, 0, 0], "tags": ["prayer"], "facing": 180, "capacity": 1 }
```

`"relativeTo": "area"` with a `location` (`region`, `place`, `context`: `outdoors`, `dungeon` or
`b<buildingKey>`) pins a spot to one place. `anpc_spot_pos` prints one where you stand; `anpc_reload_spots`
reloads them.

### Personal variables

Per-person values that dialogue can change and schedules/dialogue can test:

```json
"variables": { "following": false, "jobs_completed": 0 },
"schedule": [
  { "package": "follow", "when": { "variables": { "following": true } } },
  { "package": "stand" }
]
```

Booleans or whole numbers, up to 64. Set them from dialogue with `setVariables` / `addVariables`, or the
console `anpc_var`.

---

## 11. Mod settings (for players)

DFU mod list → Advanced NPCs → Settings:

- **Population**: same people every visit / random each visit; max generic per town (default 12; 0 = off);
  dungeons, interiors, wilderness on/off; max wilderness ANPCs around you.
- **Wildlife**: wild animals on/off; max wild animals around you (default 10); density Low / Normal / High.
- **Companions**: max followers (default 3); catch-up distance; disable follower barks.
- **Corpses**: death animation; directional corpses.
- **Sounds**: ANPC / Vanilla / Off voices; footsteps; step volume.

---

## 12. Console commands

| Command | Does |
|---|---|
| `anpc_pos` | prints your position as JSON |
| `anpc_place <id>` | moves a unique ANPC's home to you (rewrites its `npc.json`) |
| `anpc_list` | spawned ANPCs with keys, distance, state |
| `anpc_info [key]` | everything about the ANPC in front of you |
| `anpc_spawn <template>` | makes a person from a generic template in front of you (saved) |
| `anpc_remove <key>` | removes one made with `anpc_spawn` |
| `anpc_summon <key>` | moves an ANPC to you (testing, not saved) |
| `anpc_here` | who can spawn here and why / why not |
| `anpc_hostile <key> [on\|off]` | make an ANPC an enemy or calm it |
| `anpc_opinion <key> [value]` | read / set opinion |
| `anpc_topics [key]` | shown and hidden topics |
| `anpc_flag [name on\|off]` | list / set dialogue flags |
| `anpc_var <key> [name] [value]` | read / set personal variables |
| `anpc_schedule [key]` | explain the active schedule entry |
| `anpc_chat` | start an ANPC conversation now |
| `anpc_spells [filter]` | list usable spell names |
| `anpc_route start\|add\|finish\|cancel` | record a patrol |
| `anpc_spot_pos` | print a named spot at your position |
| `anpc_reload_dialogue` | reload dialogue files |
| `anpc_reload_schedules [id]` | reload schedules |
| `anpc_reload_packages` / `anpc_reload_spots` | reload packages / spots |
| `anpc_reload_assets [id]` | reload sprites and sounds |

Any other `npc.json` change needs a game restart.

---

## 13. Publishing a bundle

A bundle is a set of ANPC folders (plus any `_Portraits`, `_Dialogue`, `_Packages`, `_Spots`,
`_Namelists` files they use) that players extract into `StreamingAssets/ANPCs/`.

- List the framework as a requirement.
- Pick folder ids that won't clash with other bundles (e.g. prefix with your bundle name). Two bundles
  with the same folder overwrite each other.
- Name shared files uniquely too (`_Dialogue/mybundle_gossip.json`), unless you deliberately reuse
  ones the framework ships.
- Never rename a folder after release; saves are keyed by it.
- Only ship sounds and art you may redistribute.
- Before release: start a game, check `Player.log` for `[AdvancedNPCs]` warnings, and `anpc_spawn` each
  template once.

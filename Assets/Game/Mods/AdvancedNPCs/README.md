# Advanced NPCs

Persistent, data-defined NPCs ("ANPCs") for Daggerfall Unity 1.9.2. Each ANPC is a folder with an
`npc.json`. ANPCs stay calm, only fight whoever attacks them, fight or flee by bravery, report crimes,
calm down over in-game hours, and keep their state (alive/dead, hostile/calm, health) through visits
and save/load.

## Folder layout

```
DaggerfallUnity_Data/StreamingAssets/ANPCs/
  _Portraits/        shared portrait PNGs
  _Namelists/        custom name lists
  _Dialogue/         dialogue types: topics many ANPCs can share
  commoner/          one folder per ANPC; the folder name is its id
    npc.json
    dialogue.json    optional: topics only this ANPC (or template) has
```

- Folder names use lowercase letters, digits and `_`. Never rename a folder after release: the name keys save data.
- Folders starting with `_` hold shared data and are not ANPCs.
- Unknown fields in `npc.json` are reported in `Player.log` and ignored (catches typos).
- Every ANPC is a **generic** template (people that add life to towns) unless its `npc.json` says `"kind": "unique"`.

## Making a unique ANPC

1. Install the mod and enable **Advanced NPCs** in the DFU mod list.
2. In game, stand outdoors where the ANPC should live, open the console (`~`) and type `anpc_pos`.
3. Create `StreamingAssets/ANPCs/my_npc/npc.json` with a `name` and the two lines `anpc_pos` printed:

```json
{
  "kind": "unique",
  "name": "My NPC",
  "location": { "region": "Daggerfall", "place": "Daggerfall" },
  "position": [12.5, 1, -3.25]
}
```

4. Restart the game. Check `Player.log` for lines starting with `[AdvancedNPCs]` — every problem names the folder, file and field.
5. To move it later, stand at the new spot and type `anpc_place my_npc`.

## Fields (unique ANPCs)

| Field | Required | Default | Allowed values |
|---|---|---|---|
| `kind` | yes | `generic` | must be `unique` for a unique ANPC |
| `name` | yes | — | any text |
| `location` | yes | — | from `anpc_pos` |
| `position` | yes | — | from `anpc_pos` (relative to the town's origin corner) |
| `race` | no | the region's people | `Breton`, `Redguard`, `Nord`, `DarkElf` (or `Dunmer`), `Khajiit` |
| `portrait` | no | vanilla face | portrait name, see [Portraits](#portraits) |
| `baseClass` | no | `Spellsword` | Mage, Spellsword, Battlemage, Sorcerer, Healer, Nightblade, Bard, Burglar, Rogue, Acrobat, Thief, Assassin, Monk, Archer, Ranger, Barbarian, Warrior, Knight; for enemies also a creature, see [Enemies](#enemies) |
| `attitude` | no | `calm` | `calm` (townsfolk) or `hostile` (an enemy), see [Enemies](#enemies) |
| `hostileHours` | no | always | with `hostile`: `[from, to]` whole hours 0–23, e.g. `[20, 6]` |
| `gender` | no | fixed random per ANPC | `Male`, `Female` |
| `bravery` | no | `Normal` | `Coward` (always flees), `Normal` (flees at low health), `Brave` (fights to the death) |
| `fleeHealthPercent` | no | `25` | 1–99, used by `Normal` |
| `calmDownHours` | no | `[6, 48]` | `[min, max]` in-game hours before a hostile ANPC forgives the player |
| `crimeOnAttack` | no | `true` | `true`: attacking is assault, killing is murder |
| `wanderRadius` | no | `8` | metres around the spawn point; `0` stands still |
| `combat` | no | vanilla | `{ "health": 0.7, "damage": 0.7, "attackSpeed": 1.3 }` multipliers, see [Combat tuning](#combat-tuning) |
| `dialogue` | no | none | dialogue type(s) from `_Dialogue/`, e.g. `"tavern_wench"` or `["gossip", "tavern_wench"]`, see [Dialogue topics](#dialogue-topics) |


## Generic townsfolk

A generic template makes several different people per town, alongside vanilla citizens:

```json
{
  "kind": "generic",
  "portraits": ["commoner_1", "commoner_2"],
  "spawn": {
    "locationTypes": ["TownCity", "TownHamlet", "TownVillage"],
    "count": [1, 3]
  },
  "baseClass": "Bard",
  "bravery": "Coward"
}
```

| Field | Default | Meaning |
|---|---|---|
| `name` / `names` | generated | every instance has `name`, or one of `names` is picked; neither: a DFU-style name for its race and gender |
| `portrait` / `portraits` | vanilla face | one portrait name, or several whose pools are joined, see [Portraits](#portraits) |
| `nameList` | `default_<race>` | name list for generated names, see [Name lists](#name-lists) |
| `spawn.locationTypes` | `TownCity`, `TownHamlet`, `TownVillage` | location types the template appears in |
| `spawn.places` | all matching towns | list of `{ "region", "place" }`; only those towns. Add `"positions": [[x, y, z], …]` (from `anpc_pos`) for fixed spots |
| `spawn.count` | `[1, 3]` | instances per town, `0 <= min <= max <= 20` |

All behaviour fields of unique ANPCs (`race`, `baseClass`, `gender`, `bravery`, `dialogue`, …) work the same, and
a template's `dialogue.json` gives its topics to every person made from it. `location` and `position` are for
unique ANPCs only. Instances are named `<template>@<map id>#<n>` in
`anpc_list` and in save data.

## Settings

In DFU's mod list, select Advanced NPCs → Settings → **Population**:

- **Generic people** — *Same people every visit* (default): each town keeps the same generic people, and what happens to them (killed, angry, hurt) is saved. *Random each visit*: re-rolled whenever the town loads; nothing about them is saved.
- **Max generic per town** — 0 to 30 (default 12); 0 turns generic ANPCs off. Also the most generic ANPCs in one building.
- **Dungeons / Interiors / Wilderness** — on (default) or off: generic ANPCs with a `dungeons`, `interiors` or `wilderness` spawn block, see [Spawning outside towns](#spawning-outside-towns).
- **Max wilderness ANPCs around you** — 0 to 10 (default 4).

Changes apply the next time a town loads.

## Upgrading from 0.1

On the first start, every valid `StreamingAssets/AdvancedNPCs/<name>.json` is copied to
`StreamingAssets/ANPCs/<id>/npc.json` (its `id` line becomes `"kind": "unique"`) and the old file is renamed to
`<name>.json.migrated`. Existing folders are never overwritten. Saves keep each NPC's state because the
folder name is the old id.

## Console commands

- `anpc_pos` — your position as definition JSON (also written to `Player.log`).
- `anpc_list` — spawned ANPCs (unique and generic) with key, name, distance, direction, height and state.
- `anpc_place <id>` — makes your current spot that unique ANPC's home: rewrites `location` and `position` in its `npc.json` (nothing else) and moves it here.
- `anpc_summon <id or key>` — moves a spawned ANPC in front of you (testing only; not saved).
- `anpc_spawn <template>` — makes a new person from a generic template (e.g. `anpc_spawn wench`) in front of you, rolled like the template's other people (name, portrait, sprite set). They stay at that spot for the rest of this game, saved with it: outdoors in a town (key `<template>@<map id>+<n>`), inside a building (`<template>@<map id>:b<building>+<n>`) or inside a dungeon (`<template>@<map id>:dungeon+<n>`). Not in the wilderness. This works in either GenericPeople mode.
- `anpc_remove <key>` — removes a person made with `anpc_spawn` from the world and from your save.
- `anpc_hostile <id or key> [on|off]` — turns an ANPC into an enemy or calms it (without on/off it toggles); saved with your game. See [Enemies](#enemies).
- `anpc_topics [id or key]` — the dialogue topics of the ANPC in front of you (within 5 m) or of that ANPC: which are shown, and for the others the condition that hides them. See [Dialogue topics](#dialogue-topics).
- `anpc_reload_dialogue` — re-reads `_Dialogue/*.json` and every `dialogue.json` without restarting (a change to `npc.json` still needs a restart).
- `anpc_flag` — lists the dialogue flags that are set; `anpc_flag <name> on|off` sets or clears one (saved with your game).
- `anpc_here` — in a dungeon, building or the wilderness: which templates can appear there and what happened (filter, `when`, the chance roll, how many came). See [Spawning outside towns](#spawning-outside-towns).
- `anpc_selftest` — runs the behaviour checks with temporary ANPCs next to you (god mode on, crimes recorded instead of punished, game clock untouched); results on screen and in `Player.log`.

## Behaviour

- Calm ANPCs never stop you resting or travelling.
- They only fight whoever attacked them; hitting one ANPC does not anger the others.
- Killed ANPCs stay dead in that save. A new game brings them back.

## Enemies

`"attitude": "hostile"` (unique or generic) makes an enemy: it attacks you on sight with DFU's own enemy AI, never
calms down, and fighting or killing it is no crime. It will not talk, and it blocks resting nearby like vanilla
enemies. Bravery still applies (a Coward enemy runs).

- `"hostileHours": [20, 6]` — an enemy only from 8 pm to 6 am (hours may wrap past midnight); calm townsfolk the
  rest of the day. When its hours end it calms down, unless you fought it — then it stays angry like provoked
  townsfolk until its `calmDownHours` pass.
- Creatures: an enemy may use any DFU creature as `baseClass` — Rat, Imp, Spriggan, GiantBat, GrizzlyBear,
  SabertoothTiger, Spider, Orc, Centaur, Werewolf, Nymph, Slaughterfish, OrcSergeant, Harpy, Wereboar,
  SkeletalWarrior, Giant, Zombie, Ghost, Mummy, GiantScorpion, OrcShaman, Gargoyle, Wraith, OrcWarlord,
  FrostDaedra, FireDaedra, Daedroth, Vampire, DaedraSeducer, VampireAncient, DaedraLord, Lich, AncientLich,
  Dragonling, FireAtronach, IronAtronach, FleshAtronach, IceAtronach, Dragonling_Alternate, Dreugh, Lamia. It
  gets that creature's stats, attacks and sounds (and its picture, unless the folder has sprites). Creatures are
  always hostile (no `hostileHours`).
- On the go: `anpc_hostile <id or key> on|off` turns any ANPC into an enemy or calms it (also a hostile one); the
  switch is saved with your game. Other mods can do the same with `AdvancedNpcsMod.Instance.SetHostile(key, true)`
  or the mod message `"SetHostile"` with data `"<key>|on"` / `"<key>|off"`.
- Enemies spawn where any ANPC spawns: unique ones at their place, generic ones in towns, and with `anpc_spawn`.

### Combat tuning

`"combat"` (unique or generic, optional) scales DFU's own numbers for this ANPC; every value is a multiplier and
1 (or leaving it out) keeps the vanilla value:

```json
"combat": { "health": 0.7, "damage": 0.7, "attackSpeed": 1.3 }
```

- `health` (0.05–10): max health is rolled by DFU as usual, then multiplied (at least 1).
- `damage` (0.05–10): a creature's melee damage ranges, e.g. an Orc's 1–6 becomes 1–4 at 0.7. Creature
  `baseClass` only: human classes take their damage from weapons.
- `attackSpeed` (0.25–4): how often it swings. DFU waits 1.5–3 s (adjusted by your level and the reflexes
  setting) after each melee attack; 1.3 waits 1/1.3 as long.

`Player.log` shows the result when it spawns, e.g. `orc_weakling@1276329198#0: combat health 24->17, damage 1-6->1-4, attack speed x1.3.`


## Spellcasting

Add an optional `magic` block to any unique or generic ANPC:

```json
"magic": {
  "spells": ["Fireball", "Heal", "Shield"],
  "usesMagicka": true,
  "maxMagicka": 150,
  "recovery": "onAreaEntry",
  "castCooldown": [4, 7]
}
```

| Field | Default | Meaning |
|---|---|---|
| `spells` | required | Standard DFU spell names, case-insensitive (use the original English names). Replaces the class's inherited spells. `[]` disables spellcasting. |
| `usesMagicka` | `true` | Require and spend the full spell cost. `false` permits casting even at zero magicka; cooldown, silence and paralysis still apply. |
| `maxMagicka` | `150` | Maximum and initial magicka, whole number 0–1,000,000. |
| `castCooldown` | `[4, 7]` | Random gameplay seconds between casts; both numbers must be at least 1, in ascending order, at most 3600. Shared by all spells. |
| `recovery` | `onAreaEntry` | Fully replenish when the player returns to the NPC's town, building or dungeon. The outdoors around a town counts as the town: walking past its edge (or being chased out of the gate) is the same visit, and town casters keep casting there. A new visit starts after a building or dungeon, in another town, or once the town is far behind (unloaded) or after fast travel. Loading a save in the same visit preserves magicka and cooldown. |
| `restHours` | `8` | With `"recovery": "rest"`, game hours to recover an empty pool. Recovery starts after one game minute without detecting a combat target, includes time away, and caps at maximum. Area entry does not instantly refill this mode. Above 0, at most 720. |
| `skill` | `80` | The ANPC's proficiency in each of the six magic schools (1–100). DFU uses these skills to calculate casting costs; the player's skills do not affect them. |

Omitting `magic` keeps the existing vanilla class behaviour. Existing saves initialize a newly configured caster at full magicka.
Each instance has its own pool and cooldown. Rest recovery also applies after time passes while the player rests or travels.
Random-each-visit and wilderness people are temporary, so their replacements start fresh as usual.
Saves only keep a caster's magic while it matters: one at full magicka with no cooldown adds nothing, and an
`onAreaEntry` caster's magic is dropped once you are in another area (it will start full there anyway).

Configured ANPCs cast only while fighting, with a short random initial delay. They choose affordable, useful spells, heal
below 60% health (prioritizing healing below 40%), and avoid reapplying active buffs or effects already on their target.
Offensive spells require sight of a target; ranged spells additionally require a clear projectile path within 25 metres.
Touch spells require melee distance. With no affordable/useful spells they continue their normal physical combat.
Bravery still takes precedence: a fleeing ANPC does not cast.

Effects, projectiles, resistance, reflection and spell costs come from DFU's spell system. Supported effects include damage
to health/fatigue/magicka, continuous damage, attribute drains, paralysis and silence; self spells include healing,
regeneration, shield, spell resistance/reflection/absorption, elemental resistance, invisibility and shadow. Player utility spells such as Recall,
levitation and door manipulation are not supported. Unknown names or unsupported spells are skipped with a warning naming
the `npc.json` and spell in `Player.log`; other valid spells still work. This list uses standard spells, not custom spells
from the current player's spellbook. Casting releases the spell immediately through DFU and plays its spell animation; custom sprites use a `cast*`
sheet when available, falling back to an `attack*` sheet; with neither, the spell is released without an animation.
Melee action frames do not apply to spells. Only the Mage, Spellsword, Battlemage, Healer and Nightblade classes have a
vanilla spell animation; other classes without a custom sprite still cast (with the cast sound and sparkles) but show no
casting pose.

`anpc_spells` lists all supported standard spell names (also written to `Player.log`); `anpc_spells fire` filters them.
`anpc_list` shows each configured caster's magicka or unlimited mode. `Examples/ANPCs/spellcaster/npc.json` is a calm,
manual-spawn example: use `anpc_spawn spellcaster`, then `anpc_hostile <key> on` to fight it.

## Spawning outside towns

A generic template can also appear in dungeons, in buildings and in the wilderness. Add a block for each place to
its `spawn`:

```json
"spawn": {
  "dungeons":   { "chance": 25, "count": [2, 4], "dungeonTypes": ["HumanStronghold", "Prison"] },
  "interiors":  { "buildings": ["Tavern", "Fighters Guild"], "chance": 60, "count": [1, 3], "when": { "time": "night" } },
  "wilderness": { "chance": 5, "max": 2, "when": { "time": "night" } }
}
```

- **`chance`** (0–100): on each visit, how likely the template is there at all. **`count`** `[min, max]` (1–10):
  how many come when it is. A group stands together.
- **`dungeonTypes`** (optional): Crypt, OrcStronghold, HumanStronghold, Prison, DesecratedTemple, Mine, NaturalCave,
  Coven, VampireHaunt, Laboratory, HarpyNest, RuinedCastle, SpiderNest, GiantStronghold, DragonsDen,
  BarbarianStronghold, VolcanicCaves, ScorpionNest, Cemetery. Without it, every dungeon.
- **`buildings`**: Alchemist, Armorer, Bank, Bookseller, ClothingStore, FurnitureStore, GemStore, GeneralStore,
  Library, PawnShop, WeaponSmith, Temple, Tavern, Palace, House (any house), GuildHall (any guild), or a guild:
  Fighters Guild, Mages Guild, Thieves Guild, Dark Brotherhood, Knightly Order.
- **Wilderness**: every 10 in-game minutes outside any town or dungeon (not while resting or travelling), the
  chance is rolled; one person appears out of sight 40–80 m away, on dry land. **`max`** (1–10) caps how many of the
  template are around you at once. They leave when far behind you, when you enter a town or building, or travel.
- **`when`** (optional): the [dialogue conditions](#conditions-when) (time, weather, season, place, player, flags,
  quest globals), except `asked`, `notAsked`, `tone` and `reaction`.
- People stand on DFU's own monster, treasure and furniture spots: never within 2 m of a door, never within 20 m of
  a dungeon's entrance. A building holds at most "Max generic per town" of them.
- With **Same people every visit**, a dungeon or building always has the same people (or none) in the same spots,
  and a killed one stays dead. With **Random each visit**, every entry rolls again. Wilderness people are never kept.
- A template with only these blocks never appears in towns. To have it in towns too, add `locationTypes`.
- Attitude, bravery, sprites, portraits and dialogue work as in towns: a hostile bandit attacks on sight, a calm
  patron can be talked to. Indoors people wander at most 3 m.
- Not sure why someone is (or isn't) there? Type `anpc_here`.

## Talking and portraits

- Click a calm ANPC in Talk or Grab mode to open DFU's citizen talk window with its name and portrait; vanilla topics work as for any citizen.
- Hostile or fleeing ANPCs refuse to talk. Info mode shows "You see <name>.".

## Dialogue topics

ANPCs can have their own "Tell Me About" topics. They are listed right after "Where am I?", next to the vanilla
topics, and answered in the talk window like any other topic.

### Your first topic in two minutes

1. Create `ANPCs/_Dialogue/tavern_wench.json` (the file name is the **dialogue type**):

   ```json
   {
     "topics": [
       { "caption": "The house ale", "answers": ["Two coppers a mug. Three if you want it cold."] }
     ]
   }
   ```

2. Add one line to the ANPC's `npc.json`: `"dialogue": "tavern_wench"`.
3. Start the game and talk to her: **Tell Me About → The house ale**.
4. Later edits: change the file, type `anpc_reload_dialogue` in the console, talk to her again. No restart.

Every ANPC (unique or generic) with `"dialogue": "tavern_wench"` shares these topics. A list mixes types:
`"dialogue": ["gossip", "tavern_wench"]`. A folder's own `dialogue.json` (same format) adds topics only that
ANPC (or every person of that template) has. If two files have a topic with the same `id`, the later one wins:
types in the order listed, then the folder's own file.

### Topics

```json
{
  "caption": "The missing sailor",
  "id": "sailor",
  "when": { "asked": "Any rumours?" },
  "question": { "polite": "Could you tell me about the sailor?", "blunt": "The sailor. Talk." },
  "answers": [
    { "when": { "reaction": "dislikes" }, "text": "Why should I tell you?" },
    "Jory drank here every night, then nothing.",
    "Poor Jory. Last seen near the old warehouse."
  ],
  "sets": ["heard_sailor"],
  "once": false
}
```

| Field | Needed | Meaning |
|---|---|---|
| `caption` | yes | The text in the topic list. Keep it under 24 characters. |
| `answers` | yes | What the ANPC says: one text, or a list. One is picked at random each time. |
| `when` | no | Only list the topic while these conditions hold (see below). |
| `question` | no | What the player says. Default by tone: "Could you tell me about…?", "Tell me about….", "What do you know about…?". One text for every tone, or `{ "polite", "normal", "blunt" }`. |
| `id` | no | The name other topics use for this one. Default: the caption in lowercase with `_` for spaces and punctuation ("Any rumours?" → `any_rumours`). |
| `sets` / `clears` | no | Flags to set / clear when the topic is asked (see Flags). |
| `once` | no | `true`: the topic disappears for this person after it was asked. |

**Answers that depend on something:** an answer can be `{ "when": {…}, "text": "…" }` (it may also have its own
`sets` / `clears`). Answers whose `when` holds win over plain answers; among the winners one is picked at random.
A topic whose answers all have a `when` and none holds is not listed. `"when": { "tone": "blunt" }` in an answer
reacts to the tone the player chose.

**Follow-up topics:** `"when": { "asked": "Any rumours?" }` lists the topic once "Any rumours?" was asked of this
person, straight away in the same conversation. Name the topic by its caption or its `id`.

**Greetings:** a file may have `"greetings": [ … ]` with the same answer format (no `tone`, `sets` or `clears`).
When one qualifies it replaces the vanilla greeting; otherwise the vanilla greeting stays.

**Placeholders:** `{player}`, `{npc}`, `{town}` and `{region}` in any text; `{topic}` (the caption) in questions.

### Conditions (`when`)

Every condition in a `when` must hold. For "or", use `"any": [ { … }, { … } ]`.

| Condition | Example | Holds when |
|---|---|---|
| `hours` | `[20, 6]` | the hour is from 20:00 up to (not including) 6:00 |
| `time` | `"night"` | it is `day` or `night` |
| `season` | `["autumn", "winter"]` | it is one of these seasons (`spring`, `summer`, `autumn`/`fall`, `winter`) |
| `weather` | `"rain"` | the weather is one of `clear`, `overcast`, `rain` (storms count), `storm`, `snow` |
| `region` / `town` | `"Daggerfall"` | the player is in one of these regions / towns |
| `minLevel` / `maxLevel` | `5` | the player's level is at least / at most this |
| `playerRace` | `"Dark Elf"` | the player is one of these races |
| `playerGender` | `"Female"` | the player is `Male` / `Female` |
| `minGold` | `500` | the player carries at least this much gold |
| `hasItem` | `"Ruby"` | the player carries an item with one of these names |
| `guild` (+ `minGuildRank`) | `"Fighters Guild"` | the player is a member (of rank 0–10 at least): `Fighters Guild`, `Mages Guild`, `Thieves Guild`, `Dark Brotherhood`, `Temple`, `Knightly Order` |
| `reaction` | `["likes", "loves"]` | how people here regard the player: `dislikes`, `neutral`, `likes`, `loves`. This is DFU's reaction, from your standing with the region's people, so it is the same for every ANPC in a region (below about -20 DFU refuses to talk at all) |
| `asked` / `notAsked` | `"Any rumours?"` | all / none of these topics were asked of this person |
| `flags` / `notFlags` | `"heard_sailor"` | all / none of these flags are set |
| `questGlobal` / `notQuestGlobal` | `"LiftedCurse"` | all / none of these DFU quest globals (names from `Quests-GlobalVars`, or numbers 0–63) are true |
| `tone` | `"blunt"` | (answers only) the player's tone is one of `polite`, `normal`, `blunt` |
| `any` | `[{ "time": "night" }, { "flags": "x" }]` | at least one of these condition groups holds |

Values like seasons, races and names may be one text or a list of "any of these".

### Flags

Flags are named switches for the whole game, saved with it. A topic sets one (`"sets": ["heard_sailor"]`), and
any topic of any ANPC can react to it (`"when": { "flags": "heard_sailor" }`). That's how one person's answer
unlocks a topic with someone else. `anpc_flag` lists them and sets or clears one for testing. Other mods can send
the messages `SetFlag` (`"name|on"` / `"name|off"`) and `HasFlag` (`"name"`, the callback gets true or false).

### Replies and actions

After an answer, the player can be given **replies** to choose from (a list over the talk window, always with
"(Say nothing.)" last). A reply adds the player's line and the ANPC's answer to the conversation, and can lead to
more replies, so small dialogue trees are easy:

```json
{
  "caption": "The house ale",
  "answers": ["Two coppers a mug. Want one?"],
  "replies": [
    { "text": "A mug, please. (2 gold)", "takeGold": 2, "reputation": 1,
      "answers": ["Here you go, love."],
      "replies": [ { "text": "Another round!", "takeGold": 2, "answers": ["Steady now."] } ] },
    { "text": "Not today.", "answers": ["Suit yourself."] }
  ]
}
```

- A reply has `text` (what the player says) and `answers` (same as a topic's: texts, or `{ "when", "text" }`).
  Optional: `when` (the conditions above; `tone` works here), `replies`, `sets` / `clears`, `id`, and actions.
- `replies` can also sit on one answer object; then that answer's replies are offered instead of the topic's.
- Replies nest up to 8 deep. `"asked": "The house ale/A mug, please. (2 gold)"` checks whether a reply was chosen.

**Actions** work on topics, answers and replies and happen when that line is said:

| Action | Example | Effect |
|---|---|---|
| `giveGold` / `takeGold` | `5` | the player gets / pays that much gold (1–100000) |
| `giveItem` / `takeItem` | `"Ruby"` | the player gets / gives up one item by its name |
| `reputation` | `-2` | how this region's people regard the player (−20 to 20); this is what `reaction` reads |
| `startQuest` | `"A0C00Y00"` | starts a DFU quest by its file name |
| `becomeEnemy` | `true` | the talk window closes and the ANPC becomes an enemy (like `anpc_hostile <id> on`) |
| `endConversation` | `true` | the talk window closes after the line |

A reply or topic that takes gold or an item is only offered when the player has it. An unknown `giveItem` name is
reported in `Player.log` when the game starts.

### When something doesn't show up

- Look in `Player.log` for `[AdvancedNPCs]` lines: every problem names the file, the topic and the field, and
  misspelt field names get a suggestion (`when: tme: unknown condition (did you mean "time"?)`). A broken topic is
  skipped; the rest of the file still works. A condition that cannot be read never holds, so a typo hides the
  topic (or answer) instead of showing what it was meant to lock away.
- Stand in front of the ANPC and type `anpc_topics`: it lists each topic as `shown` or with the condition that
  hides it (`hidden, when: time`, `hidden, once (already asked)`).
- `Examples/ANPCs/_Dialogue/tavern_wench.json` uses every feature: greetings, tone answers, follow-ups, flags,
  `once`, time, weather, season, gold and guild topics, replies and actions.
- Two topics or reply keys repeated in one JSON object, or a topic replaced by a later file with the same id, are
  noted in `Player.log` too.

## Portraits

- PNG files in `ANPCs/_Portraits/`, shared by all ANPCs; 64 × 64 pixels recommended.
- `"portrait": "bram"` uses `bram.png` and every `bram_<number>.png` (`bram_1.png`, `bram_2.png`, …) as a pool: each person gets one of them. A lone `bram_1.png` works too.
- The face a person shows the first time you talk to them is kept for the rest of that game, even if you add or remove files later (*Random each visit* people are re-rolled anyway). This is only saved when the pool has more than one picture.
- Without a portrait, or if no file matches, the ANPC shows a vanilla face matching its race and gender.

## Sprites

An ANPC folder can have its own 8-direction sprite sheets instead of the vanilla class sprite:

```
ANPCs/wench/
  npc.json
  sprites/            one sprite set; several as sprites_1/, sprites_2/, … (each person gets one)
    sprites.json
    idle_1.png  idle_2.png  walk_1.png  hit_1.png  attack_1.png  death.png
    death_static.png  (optional)
```

- Make them with `Tools~/render-sprites.py` from a rigged, animated `.blend`: one sheet per Action, rows = directions (front, front_right, right, back_right, back, back_left, left, front_left), columns = frames, plus `sprites.json` (scale, frame sizes, feet row, fps). The feet row is the lowest point of the idle poses, so the character may stand at any height. Run with `test` first to check light and size:
  `blender -b character.blend --python render-sprites.py -- <out_dir> [test] [--rig NAME] [--mesh NAME]`
- For characters with a separate weapon mesh, `Tools~/render-character-sprites.py` includes both meshes in rendering and bounds: `--mesh body,sword`. `--keep-last death_1` keeps the final corpse pose. The Dunmer example documents a complete command.
- Sheet names pick the animation: `idle*` (standing), `walk*` (moving), `hit*` (hurt), `attack*` (attacking), `cast*` (spellcasting), `death*` (dying). Numbered names (`idle_1`, `idle_2`) are variants picked at random. Missing cast sheets use attack sheets (no attack sheet either: spells play no animation); missing walk/hit/attack sheets use idle; a set needs at least one idle sheet.
- Every hit (any health loss) plays a hit sheet from its first frame to its last, also when the person is hit again while it plays. A hit during an attack does not interrupt it (no stun-lock, as in vanilla): the hit sheet plays right after the attack sheet. Paralysis freezes the sprite.
- Size: the standing pose is as tall as the vanilla class sprite; set `"spriteHeight"` (world units) in `npc.json` to change it. Every animation uses the same scale.
- Proportions: `"spriteHeightScale"` defaults to `1`. Set it to `0.94` for a sprite that is 6% shorter with exactly the same width. It scales only the vertical dimension of all animations and the corpse, anchored at the feet; sprite sheets and timing remain unchanged.
- Death: the death sheet plays and the body stays as the corpse. `death_static.png` (one picture, always facing you) replaces the last death frame; `"deathStatic": { "groundY": n }` in `sprites.json` sets its ground row. The loot pile stays clickable. After leaving the area or reloading, DFU shows its own corpse picture again.
- Sheets may be edited in an image editor, but must keep the size `sprites.json` describes (checked at start; wrong sizes are reported in `Player.log`). Other files in the folder (`.psd`, backups) are ignored.
- Every attack plays its attack sheet from the first frame to the last.
- Action frame (optional): `"actionFrame": n` on an attack sheet in `sprites.json` makes the blow (melee damage, or the arrow for archers) land when the sheet shows frame `n`, counted from 1 like the sheet's columns:
  `"attack_1": { "cellWidth": 272, "frames": 6, "actionFrame": 4 }`
  Without it, DFU's own timing is kept, which can look slightly early or late. Spells keep DFU's timing. The render script writes `actionFrame` itself from a pose marker named `action` on the attack Action, so it survives re-rendering.

## Name lists

- Generic people without `name`/`names` get generated names. `"nameList"` chooses the list: a custom file in `ANPCs/_Namelists/` (without `.json`) or a vanilla one: `default_breton`, `default_redguard`, `default_nord`, `default_darkelf`, `default_highelf`, `default_woodelf`, `default_khajiit`, `default_imperial`. Without `nameList` the person's own race is used.
- Custom lists come in two formats, described in `Examples/ANPCs/_Namelists/README.txt`: a bank copied from DFU's `NameGen.txt` plus a `"style"`, or plain `male`/`female`/`surnames` lists. Simple lists can put a gendered prefix before the surname (`maleSurnamePrefix`/`femaleSurnamePrefix`, e.g. Orsimer `gro-`/`gra-`). Ready-made lists include `orsimer`, `argonian`, `dunmer_morrowind` (Morrowind-style Dunmer given names and family names), and `khajiit_skyrim` (original Skyrim-style Khajiit whole names without surnames).
- A missing or broken list falls back to the race's vanilla list (one warning in `Player.log`).

## Examples

`Examples/ANPCs/` holds four generic templates for cities, towns and villages: `commoner` (one to three
cowardly commoners per town) and `cooper` (Normal, uses portrait `bram`), `baker` (Coward) and `smith` (Brave),
each at most once per town. Use `anpc_list` to find them. `Examples/ANPCs/_Dialogue/tavern_wench.json` is a
complete dialogue type; add `"dialogue": "tavern_wench"` to any ANPC to try it. `bandit` (hostile; human
strongholds, prisons, barbarian strongholds and ruined castles, and the wilderness at night) and `patron` (calm;
taverns) show spawning outside towns. `dunmer_spellblade` is a manual-spawn Dunmer Spellsword with eight-direction sprites, a casting sheet, a custom portrait, and the shared `dunmer_morrowind` namelist. Use `anpc_spawn dunmer_spellblade` to try him. `dunmer_mercenary` uses the iron-armoured model and axe, Fireball, Wizard's Fire and Resist Fire, with 80 magicka. Spawn him with `anpc_spawn dunmer_mercenary`.

## Building (developers)

Sources live in `Scripts/Core` (pure logic, unit-tested) and `Scripts/Runtime` (DFU glue).
`Tools~` (ignored by Unity) holds:

- `run-tests.sh` — runs the Core NUnit tests in a small Unity host project.
- `compile-check.sh` — compiles every mod source against a DFU install's own assemblies.
- `runtime-check.sh` — compiles the sources with DFU's own in-memory runtime compiler and loads every type, catching problems only the game's compiler has (e.g. nested enums cause TypeLoadException there).
- `build-mod.sh` — builds `advancednpcs.dfmod` with Unity 2019.4.41f2 (a windowed editor that closes itself) and installs it, plus `Examples/ANPCs`, into a DFU install. In the editor the same build is under **Daggerfall Tools → Build Advanced NPCs mod**.
- `selftest.sh [character] [save]` — fully unattended in-game test: starts DFU, loads a save standing outdoors in a town, runs `anpc_selftest`, quits and prints the results. Temporarily skips the startup options screen (settings.ini is restored).
- `play.sh [character] [save]` — starts DFU straight into a save (default `Testo` / `AtDaggerfall`) for manual testing: no options screen, no title menu, no self-test.

When adding a `.cs` file under `Scripts/`, also add it to `Files` in `AdvancedNPCs.dfmod.json`.

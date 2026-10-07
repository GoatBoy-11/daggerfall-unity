# Advanced NPCs v2.1 — Generic by Default, Name Lists, Portrait Pools

- **Date:** 2026-10-07
- **Status:** Draft, awaiting review
- **Builds on:** `2026-10-06-advanced-npcs-v2-design.md` (v2, Phases A and B implemented on `feature/advanced-npcs-v2`). Everything there stays unless this document changes it.

## 1. Context

After testing v2 in game the user set the direction for the next step: ANPCs exist mainly to add life to towns, so **generic is the normal kind** and unique NPCs are the exception. Authors also need their own name lists and several portraits per NPC to pick from. The persistence setting stays as it is (default *Same people every visit*, optional *Random each visit*).

## 2. Goals and success criteria

1. An `npc.json` without `"kind"` is a generic template. A unique NPC must say `"kind": "unique"`.
2. Old 0.1 files are still migrated and still work as unique NPCs.
3. The shipped examples are four generic templates: commoner, cooper, baker, smith.
4. An author can put name lists in `ANPCs/_Namelists/` in either of two formats, and choose per NPC between a custom list and any vanilla list.
5. An author can give an NPC a pool of portraits (`bram_1.png`, `bram_2.png`, …); each person gets one of them, and once the player has talked to that person it never changes for the rest of that game.

## 3. Non-goals

- Sprites (variant scheme noted in §8, designed with step 1b).
- Changing the persistence setting.
- Editing DFU's own `NameGen.txt`.

## 4. Generic by default

| Rule | Detail |
|---|---|
| Default `kind` | `generic` (was `unique`). |
| Unique NPCs | Need `"kind": "unique"`. |
| `location` / `position` without `kind` | Error: `<folder>/npc.json: location: belongs to unique ANPCs — add "kind": "unique"`. |
| Migration of 0.1 files | Writes `"kind": "unique"` in place of the removed `id` line; saves keep working. |

Shipped examples (`Examples/ANPCs/`), all generic with generated names; the 0.1 folders `daggerfall_city_*` are removed:

| Folder | From | `baseClass` | `bravery` | `spawn.count` | `portrait` |
|---|---|---|---|---|---|
| `commoner` | unchanged | Bard | Coward | `[1, 3]` | — |
| `cooper` | Bram | Spellsword | Normal | `[0, 1]` | `bram` |
| `baker` | Cora | Spellsword | Coward | `[0, 1]` | — |
| `smith` | Bors | Spellsword | Brave | `[0, 1]` | — |

Saved states under the old `daggerfall_city_*` keys stay in saves and are ignored (1a rule).

## 5. Name lists

### 5.1 Folder and reference

```
ANPCs/_Namelists/
  pirates.json
  my_nords.json
```

In `npc.json`:

| Field | Default | Meaning |
|---|---|---|
| `nameList` | `default_<race>` of the person | `default_breton`, `default_redguard`, `default_nord`, `default_darkelf`, `default_highelf`, `default_woodelf`, `default_khajiit`, `default_imperial` use DFU's own banks from `NameGen.txt`; any other value names a file in `_Namelists` (without `.json`, any case). |

Name choice order for a generic person: `names` (fixed list in `npc.json`) → `name` (one fixed name) → `nameList`. A unique NPC keeps its required `name`.

Names starting with `default_` are reserved; a custom file called `default_*.json` is ignored with a warning.

### 5.2 Two file formats

The mod tells them apart by their keys.

**Vanilla format** — exactly one bank as it appears in DFU's `NameGen.txt`, plus the style that glues the parts:

```json
{
  "style": "nord",
  "setCount": 6,
  "sets": [
    { "setIndex": 0, "parts": ["Bjorn", "Ulf"] },
    { "setIndex": 1, "parts": ["ar", "rik"] }
  ]
}
```

- `style` (required): one of the eight vanilla bank names above without `default_` (`breton` … `imperial`). Names are built with that bank's DFU rules (same rules as `NameHelper.FullName`), so the file must have as many sets as that style uses; too few sets → error naming the file and the needed count.
- An author can copy a bank out of `NameGen.txt` and edit the parts.

**Simple format** — complete names:

```json
{
  "male": ["Ragnar", "Ulf"],
  "female": ["Astrid", "Sigrid"],
  "surnames": ["Stormborn", "Ironhand"]
}
```

- Name = random first name for the person's gender + `" "` + random surname (no surname when `surnames` is missing or empty).
- If the person's gender list is missing, the other gender's list is used. At least one of `male`/`female` must have a name.

A file with neither `sets` nor `male`/`female` → error. Bad files are skipped; NPCs that refer to them fall back to `default_<race>` with one warning per file.

### 5.3 Generation

- One generator in Core (pure, unit-tested) implements both formats and the eight vanilla styles, using the person's seed (`SeededRandom`), so same-people mode gives the same name every visit without touching DFU's global random state.
- Vanilla banks are read from DFU's `NameGen.txt` (the `Resources` copy, or the replacement in `StreamingAssets/Text` that DFU itself honours).
- This replaces `DfuNameSource` (v2), which seeded DFU's own generator.

## 6. Portrait pools

### 6.1 Pool rule

For a portrait name `bram` (from `portrait` or one entry of `portraits`), the pool is every file in `_Portraits` named `bram.png` or `bram_<number>.png` (any case):

| Files | Pool |
|---|---|
| `bram.png` | `bram` |
| `bram_1.png`, `bram_2.png`, `bram_3.png` | the three |
| `bram_1.png` only | `bram_1` |
| `bram.png`, `bram_1.png` | both |

A name with no matching file → warning, vanilla face (as v2). With `portraits: ["a", "b"]` the pools of `a` and `b` are joined.

### 6.2 Choice and lock

- Each person picks one file from the pool with its seed at spawn (same-people mode: the same file every visit).
- The **first time the player talks** to a persistent person, the chosen file name is stored in that person's saved state (`NpcState.portrait`). From then on that file is used for the rest of the game, even if files are later added to or removed from the pool. If the stored file has disappeared, a new pick is made and stored.
- *Random each visit* people are not saved, so their portrait lasts only until the town reloads (consistent with everything else about them).
- `NpcState.portrait` is a new optional field of save data `v1` (absent in old saves = not locked); a state with a locked portrait is not "default" and is saved.

## 7. Error handling (additions to v2 §14)

| Problem | Result |
|---|---|
| `location`/`position` without `"kind": "unique"` | NPC skipped, error with the hint. |
| Name list file invalid (JSON, unknown `style`, too few sets, no names) | File skipped, error; users fall back to `default_<race>`, one warning per file. |
| `nameList` names a missing file or unknown `default_*` | Warning once per name; `default_<race>`. |
| `_Namelists/default_*.json` | Ignored, warning. |
| Locked portrait file missing | New pick from the pool, stored. |

## 8. Sprites (noted for step 1b, not built now)

- Sprite sheets live in the NPC's own folder: `ANPCs/<npc>/sprites/` (`idle.png`, `walk.png`, …).
- Variants are numbered folders `sprites_1/`, `sprites_2/`, … (a lone `sprites/` or `sprites_1/` is the only variant). A variant is a complete set, so all animations of one person match.
- A person's variant is picked from its seed and locked the same way as its portrait (stored on first meeting rather than first talk).

## 9. Architecture changes

| Unit | Change |
|---|---|
| `DefinitionParser` | Default `kind` generic; hint error; `nameList` field (both kinds). |
| `Migration` | Writes `"kind": "unique"`. |
| `NameListParser` (Core, new) | `_Namelists/*.json` text → `NameList` (vanilla bank + style, or simple lists) or error. |
| `NameGenerator` (Core, new) | `Generate(NameList, gender, SeededRandom)`; the eight vanilla style rules ported from `NameHelper`. |
| `NameLists` (Runtime, new) | Loads DFU's `NameGen.txt` banks as `default_*` lists and `_Namelists/*.json`; implements `INameSource` by list name. |
| `INameSource` | Gains the list name: `Generate(string listName, string race, string gender, uint seed)`. |
| `PortraitPools` (Core, new) | File names → pools by base name (`bram`, `bram_1`, …). |
| `PortraitLibrary` | Exposes pools; `Get` by exact file name. |
| `PopulationPlanner` / `NpcInstance.ForUnique` | Pick a portrait file from the joined pool by seed. |
| `NpcState` | `portrait` (string, optional); `IsDefault` false when set. |
| `NpcTalk` | On first talk with a persistent person, store the shown portrait file in its state; prefer the stored file when present. |
| Examples, README | §4; `_Namelists/README.txt` describing both formats. |

## 10. Testing

- Core: default kind generic; hint error; migration adds `"kind": "unique"`; both name-list formats parse and every error row; generator for every vanilla style against hand-checked parts (fixed seeds); simple-format gender fallback and no-surname case; portrait pools for every row of §6.1; planner picks from pools and is repeatable; `NpcState` with a portrait is saved.
- In-game self-test additions: a generic person named from a custom list in each format; a pooled portrait stays the same across a respawn and after files are added to the pool once talked to.
- Manual: the user's own name lists and portraits.

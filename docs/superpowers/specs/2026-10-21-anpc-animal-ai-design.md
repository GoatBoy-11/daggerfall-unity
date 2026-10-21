# Animal AI and wildlife spawning (design)

Date: 2026-10-21. Status: approved by the user (answers below), all three phases built together as mod 0.26.0.

User decisions (2026-10-21):
1. Predators attack unprovoked only on a small random chance, but always when the player comes too close (react range).
   They hunt prey ANPCs at random intervals (simulated hunger).
2. Rat disease is fine; authors can give any ANPC a disease it may pass on (`disease` block, not animal-only).
3. Wildlife cap default 10 (the user's PC is high-end); check performance.
4. Build all phases; the user is away, design choices are mine.

## Why

There are about 50 `an_` animal ANPCs (deluxe pack, vanilla-animal batch). Today every one of them is
`spawn.count [0, 0]`, so none appear on their own. They are calm `Bard` + `Coward` with one looping routine
(`an_deluxe_routine_feed_look`). That gives them human stats, makes every one flee, and lets a click open the talk
window with a wolf. The goal: animals that live in Daggerfall's wilderness and on its farms, and react to the player
and to each other in a way that fits the species.

## Goals and non-goals

Goals:
- Animals spawn in the wilderness by climate, region, season, time and ground type, in herds and packs.
- Livestock lives on farms.
- Each animal has a temperament: it notices the player at a range that depends on stealth, then flees, warns,
  attacks or ignores.
- Phase 3: packs follow a leader, predators hunt prey ANPCs, one alarmed animal alarms the others.

Non-goals: taming, riding, hunger or need simulation, saving wild animals, new animations (everything falls back to
the sheets the animals already have: `idle*`, `walk`, `run`, `eat*`, `attack*`, `death*`), hunting loot, and any
Oblivion audio (CC0 or licensed only).

## What already exists (reuse, do not rebuild)

| Need | Existing piece |
|---|---|
| Wilderness spawns | `WildernessSpawner`: a roll every 10 game min per template, one person 40–80 m away out of view, never saved, despawned when far, inside or on rebuild; `MaxWildernessAround` cap |
| Ground check | `WildernessSpawner.DryLand`: terrain tilemap tile `& 0x3F`, 0 = water, 1 dirt, 2 grass, 3 stone |
| Region, season, time, weather filters | `when` conditions (`Condition.cs`) |
| Farms | `spawn.locationTypes` already accepts `HomeFarms` (`GenericSpawn.cs:83`) |
| Daily rhythm | `schedule` + packages (`wander`, `eat`, `play_animation`, `sequence`, `follow`, `travel_to`) |
| Group defence | `HostilityRules.Allied`: same parent + same spawn group, 20 m seen / 8 m heard |
| Fleeing | `bravery` / `fleeHealthPercent` / `HostilityRules` flee decision |
| ANPC vs ANPC fights | `AggroRules.IsFoe` + Brave look-around |
| Stats from creatures | creature `baseClass` (hostile only today, `DefinitionParser.cs:419`) |
| Cheap far updates | `NpcLod` |
| Crime for killing | `crimeOnAttack` |

## Phase 1 — wildlife spawning (smallest useful release)

### 1.1 `climate` condition

A new `when` key, so it works in spawn blocks, schedules and dialogue alike:

```json
"when": { "climate": ["Woodlands", "MountainWoods"] }
```

Values are the DFU `MapsFile.Climates` names: `Ocean`, `Desert`, `Desert2` (the hot desert seen in Dak'fron),
`Mountain`, `Rainforest`, `Swamp`, `Subtropical`, `MountainWoods`, `Woodlands`, `HauntedWoodlands`. The fact comes
from `PlayerGPS.CurrentClimateIndex` (added to `IDialogueFacts` as `Climate`). An unknown name is a load problem,
like other typos. Regions still use the existing `region` condition.

### 1.2 Groups

```json
"wilderness": { "chance": 15, "max": 6, "group": [3, 6], "terrain": "grass",
                "when": { "climate": "Woodlands", "season": ["spring", "summer", "autumn"] } }
```

- `group` `[min, max]` (1–8, default `[1, 1]`): one successful roll spawns that many around a single ground point, each
  2–6 m from it on ground that passes the same checks. The first is the group's leader.
- All members share one spawn-group id, so the existing ally rule makes them defend each other.
- `max` still counts individuals (unchanged meaning). The group size is cut to the room left under `max` and under
  the wildlife cap. Nothing spawns when there is no room for `group[0]`.

### 1.3 Ground type

`terrain` picks the tile test for the anchor point (members only need dry ground):

| Value | Accepts |
|---|---|
| `dry` (default, today's behaviour) | tiles 1–3 |
| `grass` | tile 2 |
| `rocky` | tile 3 |
| `nearWater` | a dry tile with a water tile (0) within 3 tilemap samples |

`nearWater` tries 16 ring points instead of 8, because shores are rare. Coast animals (crab) use `nearWater` + climate
`Ocean`, plus their own land climate.

### 1.4 Wildlife cap and settings

A template with an `animal` block counts toward a separate cap, so animals never crowd out bandits:

| Setting | Default | Tooltip (rows of 80 chars or fewer) |
|---|---|---|
| `Wildlife` | on | "Animals appear in the wilderness." |
| `MaxWildlifeAround` | 10 (0–30) | "Most wild animals alive near you at once." |
| `WildlifeDensity` | Normal (Low ×0.5, Normal ×1, High ×2 on `chance`) | "How often animals turn up in the wilderness." |

Edit `Editor/ModSettingsWriter.cs`, not `modsettings.json`.

### 1.5 Animals are not people

When an `animal` block is present (`"animal": {}` is enough in phase 1):
- A Talk click shows "<Name> ignores you." The Info click keeps "You see <Name>.". No talk window, no dialogue file
  is read, no portrait screen.
- Forced off: `conversation`, `guard`, companion hiring, `socialize`/`hawk` packages, the talk-face turn.
  `greetChance` stays: a bleat or growl as you pass is good.
- **Calm creature classes are allowed**, so wolves and bears get creature stats and natural attacks. They are allowed
  only for an animal-safe list: `Rat`, `GiantBat`, `GrizzlyBear`, `SabertoothTiger`, `Spider`, `GiantScorpion`. No
  were-creatures, so no lycanthropy from a wolf bite. Rat keeps DFU's disease chance. `combat.health` / `damage` scale
  them down (a wolf = SabertoothTiger at about 0.5 health, 0.4 damage). Prey and livestock may keep a human class.
  `hostileHours` stays forbidden for creatures.
- Spike first: does a calm creature run on the ANPC calm motor without DFU's enemy AI taking over? Check movement,
  the anger-on-hit change, the corpse, and its sounds (own `Sounds` pool first, vanilla creature sounds otherwise).
  If the spike fails, fall back to a human class + an `animal.stats` override (health and hand-to-hand damage)
  applied after DFU sets up the entity.

### 1.6 Farms (authoring only)

Livestock gets `"locationTypes": ["HomeFarms"]`, `"count": [2, 5]`, `"crimeOnAttack": true`. Wild animals keep
`crimeOnAttack: false`. No code; the self-test checks that a farm spawn appears.

### 1.7 Explaining

`anpc_here` adds the climate, the tile under the player and the wildlife count against its cap. Wilderness lines show
the group and terrain rule and why the last roll failed ("no shore found", "room for 1, group needs 3").

## Phase 2 — the `animal` block (behaviour)

```json
"animal": {
  "temperament": "prey",
  "senses": { "alert": 25, "react": 15 },
  "activity": "crepuscular",
  "territory": 30,
  "warnSeconds": 3,
  "call": { "interval": [40, 120] }
}
```

Every field except `temperament` has a default from the temperament:

| Temperament | alert / react (m) | Unprovoked reaction | When hit | territory |
|---|---|---|---|---|
| `prey` | 25 / 15 | the group flees | flees | — |
| `grazer` | 10 / 4 | looks, then goes on grazing | the group flees | — |
| `territorial` | 18 / 8 | warns, then charges if you stay | fights | 25 |
| `predator` | 30 / 12 | stalks (keeps its distance, watches); attacks only if bold (below) | fights | 40 |
| `ambusher` | 6 / 3 | none until react, then strikes | fights | 8 |
| `ambient` | 6 / 3 | scatters 5–10 m | flees | — |

### 2.1 State machine

`Unaware → Alert → React (Flee | Warn | Attack) → Return → Unaware`. The pure rules live in Core `AnimalRules.cs` (no
Unity types, NUnit tested). A Runtime `NpcAnimal` component sits on the brain and drives the existing modes.

- **Alert**: the player is within the effective alert distance and in line of sight (`SpotFinder.Mask` raycast), or
  within 40 % of it without sight (heard). The animal stops, faces the player and plays `alert*`, else `look*`, else
  `idle`, and the `alert` sound. The schedule pauses. After 6 s with the player still outside react range it goes
  back to Unaware.
- **Effective distance** = base × sneak (crouch or slow walk ×0.5, run ×1.5) × (1 − Stealth/200) × time (×0.7 at
  night for `diurnal` animals, ×0.5 while resting). Minimum 3 m. One formula, one table of tests.
- **Flee**: run straight away from the threat, 30–50 m (`ambient` 5–10 m), then Return. Reuses the existing flee
  movement with a distance stop.
- **Warn** (territorial): face the player, play `warn*` (else the first frame of `attack*`, else `idle`) and the
  `warn` sound, for `warnSeconds`. If the player stays in react range or closes to half of it: Attack. If the player
  backs out of alert range: Return.
- **Attack**: the existing fight against the player, no crime for wild animals. A predator attacks without a hit on
  `boldChance` % (default 10) when it first notices the player, and always once the player is inside react range.
- **Leash / Return**: once the animal is more than `territory` metres from home, the fight ends, it `travel_to`s home
  and it is calm at once. Wolves never follow you across a region.
- **Group alarm**: one member entering Flee or Attack sends same-group members within 30 m into the same reaction.

### 2.2 Activity

`diurnal` (active 06–20), `nocturnal` (20–06), `crepuscular` (04–09 and 17–22), `always` (default). Outside its
active hours the animal rests: it stays put and plays `sleep*`, else `rest*`, else `idle`, with alert ×0.5. Resting is
one schedule entry put in front of the ANPC's own `schedule`, so existing routines keep working during active hours.

### 2.3 Sounds and sheets

New pools: `alert`, `warn`, `call`. `call` plays every `call.interval` game seconds while active and the player is
within 40 m (moo, cluck, howl). A pack's leader howls once at the start of a crepuscular evening. Same lookup order as
today (own pool, voice pack, `_Sounds`, vanilla). Missing sheets always fall back as listed; nothing is required.

## Phase 3 — ecosystem

- **Herds and packs**: members get the `follow` package with the leader's key (distance 3–6 m) during active hours.
  When the leader dies or despawns, the next member leads.
- **Hunting**: `"hunts": ["prey"]` or ids (`"an_hind"`, `"an_rabbit_*"`). An active predator treats a matching
  ANPC within `aggroRange` as a foe (`AggroRules.IsFoe` gains a prey test). At most one hunt per predator per game
  hour. The kill leaves the normal corpse; the predator plays `eat` on it for 2–4 cycles, then goes on.
- **Cross-species alarm**: a fleeing prey alerts prey and grazers of any species within 20 m.
- **Seasons**: hibernation and seasonal absence are authoring only (`when.season`).

## Authoring table (first pass, tune in game)

| Animals | Temperament | Activity | Group | Where |
|---|---|---|---|---|
| hind, doe, hart, stag | prey | crepuscular | 2–5 (one stag/hart leads) | Woodlands, MountainWoods, Subtropical; grass |
| rabbits | prey | crepuscular | 1–3 | Woodlands, Subtropical, Mountain; grass |
| wild boar, boar | territorial | crepuscular | 1–4 | Woodlands, MountainWoods, HauntedWoodlands |
| grey, timber, plain wolf | predator, hunts prey | nocturnal | 3–5 | MountainWoods, Mountain, Woodlands (rare), HauntedWoodlands |
| brown bear | territorial (predator at night) | diurnal | 1 | MountainWoods, Mountain; not in winter |
| ibex, goat (wild) | prey | diurnal | 2–5 | Mountain; rocky |
| crocodile | ambusher | always | 1–2 | Swamp, Rainforest; nearWater |
| viper | ambusher | diurnal | 1 | Desert, Desert2, Subtropical, Rainforest; rocky; not in winter |
| scorpion, yellow scorpion | ambusher | nocturnal | 1–2 | Desert, Desert2; rocky |
| fire salamander | ambient | nocturnal | 1 | Rainforest, Swamp, MountainWoods; nearWater |
| frogs | ambient | nocturnal | 2–4 | Swamp, Rainforest, Woodlands; nearWater; not in winter |
| crab | ambient | always | 1–3 | Ocean, Subtropical; nearWater |
| rat | ambient | nocturnal | 2–4 | HauntedWoodlands, plus town and dungeon blocks |
| goose | territorial | diurnal | 3–6 | Woodlands; nearWater; farms |
| cows, ox, calf, sheep, rams, pigs, horses, donkey | grazer (bull, ram, black ram = territorial) | diurnal | farm counts | HomeFarms; horses also near roadside taverns |
| hens, rooster, chick | ambient | diurnal | farm counts | HomeFarms |
| dog | territorial (warns, rarely bites) | always | 1 | HomeFarms |

## Verification

- Core NUnit (Editor/Tests): `climate` condition parse and match; group cut to the room left; terrain tile
  classification incl. shore search; alert distance table; state transitions for each temperament; leash; bold
  predator rule; activity hours across midnight; animal-safe creature list; `animal` forcing talk and companion off.
- Self-tests, short and run with `ANPC_BACKGROUND=1`: a group spawn shares one group and stays within 6 m; a prey group
  flees when the player is put within react range; territorial warns, then attacks, then leashes home; a calm
  `GrizzlyBear` animal spawns calm and fights when hit; a Talk click gives the line and no window; a farm spawn
  appears. Existing suites must still pass.
- Manual playtest checklist for the user: walk the Woodlands at dusk, a Swamp shore, the Alik'r, a farmstead.
- MANUAL.md: new `animal` section, the `climate` condition, the `wilderness.group` / `terrain` fields, the settings;
  README changelog.

## Risks

- Calm creature classes are the biggest unknown (the 1.5 spike decides it before phase 1 is built).
- Performance: 8 animals plus wilderness people. Groups spawn out of view; `NpcLod` covers far ones. Check frame time
  with 8 wolves in view.
- Shore search can fail often in dry climates; the explain line makes that visible.
- Animals despawn when you look away far behind (existing rule). A fleeing herd may vanish at 80 m+, which is fine.
- Several earlier animal batches still use Oblivion sounds (see the no-Oblivion-audio rule); the new `alert`, `warn`
  and `call` pools must use CC0 or licensed files from `tools/sounds.py`.

## Disease (any ANPC)

```json
"disease": { "chance": 5, "diseases": ["Plague", "StomachRot"] }
```

Each melee hit on the player rolls `chance` %; a success passes one of the listed DFU diseases through DFU's own
`FormulaHelper.InflictDisease` (level 1 immunity and the disease saving throw apply). A creature class keeps its own DFU
effects on top (a Rat's 5 % plague, a GiantScorpion's paralysis).

## Build notes (decided while building)

- Warn plays `warn*`, else `alert*`, else `idle`, with the `warn` sound every 1.5 s (an attack sheet as a warning
  looked like a swing at the air).
- Resting is done by the animal component, not a schedule entry: outside its active hours the animal stays put with
  `sleep*`/`rest*`/`idle`; the ANPC's own schedule runs in active hours.
- Herds: followers wander around the leader (their home moves with it every few seconds) instead of the `follow`
  package, so their own routine keeps playing.
- An animal's bravery comes from its temperament (fighters Prudent, the rest Coward); `fleeHealthPercent` still applies.
- Livestock (`crimeOnAttack: true`): hitting one is no crime, killing one is Theft (not Murder), unless it attacked first.
- Wild animals' deaths and hits are not witnessed by people (no opinion change).

## Results (2026-10-21, built while the user was away)

- Core unit tests 1410/1410. compile-check and runtime-check OK.
- Self-test suite `animals` (Tools~/selftest-animals.sh) 26/26: climate fact, group spawn and leader, wildlife cap,
  floating-origin home shift, prey flight and group alarm, flight end, no talk window, no crime for wild animals,
  calm GrizzlyBear warns / attacks / gives up, hunt and feeding, disease hook, livestock assault/theft, cost check.
- Frame time with 10 animals within 20 m: 13.47 ms vs 13.48 ms without (minimized test window, so CPU side only).
- Full self-test 459/461. Both failures are old and unrelated: the Dunmer samples now have 14 sheets (eat/drink were
  added in the 2026-10-19 meals batch), but the test still expects 12.
- Found and fixed while testing: DFU's floating origin moves wilderness bodies one by one, so a wilderness ANPC's
  stored home jumped 819.2 m whenever the player crossed a map pixel. This also affected wilderness people's wandering
  before this change.
- 49 an_ animals authored with tools/author_animals.py (originals in output/an_animal_ai_oct21/originals).
- Not checked in game by a person: visuals, how often animals turn up, farm cells (HomeFarms nav grids), sound feel.

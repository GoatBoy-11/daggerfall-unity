# Advanced NPCs v2.1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make generic the default ANPC kind (examples become generic templates), add custom and vanilla name lists, and portrait pools whose pick is locked on first talk.

**Architecture:** Pure parts (kind default, migration, name-list parsing and generation, portrait pools) in `Scripts/Core`, unit-tested. Runtime loads `_Namelists` and DFU's `NameGen.txt` (through DFU's own deserializer, as `NameHelper` does — the file is not strict JSON), resolves portrait pools at spawn and stores the shown portrait in `NpcState` on first talk.

**Tech Stack:** as v2 (Unity 2019.4.41f2 build, Unity 6 Core test host, DFU 1.9.2, NUnit, FullSerializer).

**Spec:** `docs/superpowers/specs/2026-10-07-advanced-npcs-v2.1-design.md` (on top of `2026-10-06-advanced-npcs-v2-design.md`).

## Global Constraints

All Global Constraints and Verification commands of `docs/superpowers/plans/2026-10-06-advanced-npcs-v2.md` apply unchanged (C# subset, no enum-typed fields of enums declared in another file, manifest entries for every `.cs`, `git restore Assets/AddressableAssetsData/` after builds, branch `feature/advanced-npcs-v2`, no push). Additionally:

- Build with `build-mod.sh --no-examples` except in Task 5 (the user's test install has hand edits).
- Name-list names and portrait names: trimmed, lowercase; `.json` / `.png` stripped.
- Vanilla list names: `default_breton`, `default_redguard`, `default_nord`, `default_darkelf`, `default_highelf`, `default_woodelf`, `default_khajiit`, `default_imperial`. A missing list falls back with a warning to `default_<race>`, then `default_breton`.
- Vanilla style rules (DFU `NameHelper`): breton, darkelf, highelf, woodelf, khajiit, imperial — first name male sets 0+1, female 2+3, surname 4+5 (6 sets needed); nord — first as above, surname 0+1+`nordSurnameImmutableSuffix` ("sen"), 4 sets; redguard — single name, male 0+1+2 then 75% +3, female 0+1+2+4, 5 sets.

## Review Focus

1. **A 0.1 file migrated after this change** must load as a unique NPC (`"kind": "unique"` written). Pinned by `MigrationTests` expectations (Task 1).
2. **A v2 install whose `npc.json` has `location` but no `kind`** must say how to fix it. Pinned by `FolderParserTests.LocationWithoutKind_HintsUnique` (Task 1).
3. **A name list file with too few sets for its style** must be rejected with the needed count, not crash at name time. Pinned by `NameListTests.TooFewSets_IsRejected` (Task 2).
4. **Portrait files added after the player talked to someone** must not change that person's face. Pinned by self-test "talked-to person keeps its portrait" (Task 4).
5. **Only `bram_1.png` present** must still be used for `"portrait": "bram"`. Pinned by `PortraitPoolsTests` (Task 4).

---

### Task 1: Generic by default

**Files:** Modify `Scripts/Core/DefinitionParser.cs`, `Scripts/Core/Migration.cs`; tests `Editor/Tests/FolderParserTests.cs`, `DefinitionCatalogTests.cs`, `MigrationTests.cs`.

**Interfaces:** Produces: `ParseFolder` default kind generic; error `<file>: location: belongs to unique ANPCs — add "kind": "unique"` (same for `position`) when `kind` is absent; `Migration.Convert` output contains `"kind": "unique"` where `"id": "<id>"` was.

- [ ] **Step 1: Update tests (RED)**
  - `FolderParserTests`: constant `Unique` gets `"kind": "unique", ` as first member; add:
    ```csharp
        [Test]
        public void MissingKind_IsGeneric()
        {
            ParseResult r = DefinitionParser.ParseFolder("commoner", "{ \"baseClass\": \"Bard\" }");
            Assert.IsTrue(r.Ok, r.Error);
            Assert.AreEqual(NpcKind.Generic, r.Definition.Kind);
        }

        [Test]
        public void LocationWithoutKind_HintsUnique()
        {
            ParseResult r = DefinitionParser.ParseFolder("bram",
                "{ \"name\": \"Bram\", \"location\": { \"region\": \"R\", \"place\": \"P\" }, \"position\": [0,0,0] }");
            Assert.AreEqual("bram/npc.json: location: belongs to unique ANPCs — add \"kind\": \"unique\"", r.Error);
        }
    ```
    `MissingName_ReportsFolderPath` gets `"kind": "unique", ` in its JSON.
  - `DefinitionCatalogTests`: helpers `Unique(...)`, `UniqueWithPortrait(...)` and the literal in `ParserWarnings_AreCollected` start with `"kind": "unique", `.
  - `MigrationTests`: expected outputs contain `"kind": "unique"` in place of the id member; rename `PrettyFile_RemovesIdLineOnly` → `PrettyFile_ReplacesIdWithKind`, `IdLast_RemovesPrecedingComma` → `IdLast_ReplacedInPlace`, `OneLineFile_RemovesIdInline` → `OneLineFile_ReplacedInline`.
- [ ] **Step 2:** `run-tests.sh` → Expected: failures in the changed tests.
- [ ] **Step 3: Implement**
  - `DefinitionParser.ParseFolder`: default for `kind` becomes `"generic"`; pass `bool kindGiven = o.ContainsKey("kind")` to `ReadGeneric`; location/position errors become `kindGiven ? "belongs to unique ANPCs; generic templates use spawn" : "belongs to unique ANPCs — add \"kind\": \"unique\""`.
  - `Migration`: replace `RemoveId` with `ReplaceIdWithKind(json)` = text before the `"id": "<id>"` match + `"kind": "unique"` + text after. `Convert` uses it.
- [ ] **Step 4:** `run-tests.sh` → all pass; `compile-check.sh`, `runtime-check.sh` clean.
- [ ] **Step 5:** commit `feat: make generic the default ANPC kind; migration marks 0.1 NPCs unique`.

### Task 2: Name lists (Core)

**Files:** Create `Scripts/Core/NameList.cs` (classes `NameList`, `NameListResult`, `NameListParser`, `NameGenerator`); test `Editor/Tests/NameListTests.cs`; manifest.

**Interfaces:**
- `class NameList { string Name; string Style /* null = simple */; readonly List<string[]> Sets; readonly List<string> Male, Female, Surnames; bool IsSimple }`
- `NameListParser.Parse(string name, string json) -> NameListResult { NameList List; string Error }` (error text `_Namelists/<name>.json: <field>: <problem>`)
- `NameListParser.RequiredSets(string style) -> int` (6; nord 4; redguard 5; unknown 0)
- `NameListParser.Styles` = breton, redguard, nord, darkelf, highelf, woodelf, khajiit, imperial
- `NameListParser.Normalize(string) -> string` (trim, lowercase, strip `.json`)
- `NameGenerator.Generate(NameList list, string gender, SeededRandom rng, string nordSuffix) -> string`

- [ ] **Step 1: Tests (RED)** — `NameListTests`:
  - `Vanilla_Parses`: `{ "style": "Nord", "sets": [ {"parts":["A"]}, {"parts":["b"]}, {"parts":["C"]}, {"parts":["d"]} ] }` → Style `nord`, 4 sets.
  - `UnknownStyle_IsRejected` → `_Namelists/x.json: style: must be one of breton, redguard, nord, darkelf, highelf, woodelf, khajiit, imperial (got "orc")`.
  - `TooFewSets_IsRejected` (breton with 2 sets) → `_Namelists/x.json: sets: style breton needs 6 sets (got 2)`.
  - `EmptySet_IsRejected` → `_Namelists/x.json: sets[1]: needs at least one part`.
  - `Simple_Parses`; `Simple_OneGenderEnough`; `Simple_NoNames_IsRejected` → `_Namelists/x.json: male: needs at least one name in male or female`.
  - `Neither_IsRejected` → `_Namelists/x.json: file: needs "sets" (vanilla format) or "male"/"female" (simple format)`.
  - `InvalidJson_IsRejected` (starts with `_Namelists/x.json: file: invalid JSON`).
  - Generator with one part per set: breton A,b,C,d,E,f → male `Ab Ef`, female `Cd Ef`; nord A,b,C,d + `sen` → male `Ab Absen`; redguard A,b,c,D,E → female `AbcE`, male `Abc` or `AbcD`; simple male-only list for a female → male name; simple without surnames → one word.
  - `Normalize`: `" Pirates.JSON "` → `pirates`.
- [ ] **Step 2:** `run-tests.sh` → compile error (`NameListParser` missing).
- [ ] **Step 3:** Implement per Interfaces (`sets` in array order, each `parts` a non-empty text list; `setIndex`/`setCount` ignored; `male`/`female`/`surnames` via `FieldReader.Texts`; `sets` key → vanilla, else `male`/`female` → simple). Generator per Global Constraints rules, drawing with `rng.Pick`, redguard 75% via `rng.Next(100) < 75`. Manifest entry.
- [ ] **Step 4:** tests pass; runtime-check clean. **Step 5:** commit `feat: parse name lists (vanilla bank or simple) and generate names`.

### Task 3: Use name lists

**Files:** Modify `Scripts/Core/NpcDefinition.cs` (`string NameList = ""`), `DefinitionParser.cs` (`nameList` in SharedKeys, normalized), `PopulationPlanner.cs` (`INameSource.Generate(string listName, string race, string gender, uint seed)`; list = `NameList` or `"default_" + race.ToLowerInvariant()`); create `Scripts/Runtime/NameLists.cs`; delete `Scripts/Runtime/DfuNameSource.cs` (+ manifest); modify `NpcSpawner.cs` (`names` = `owner.Names`), `AdvancedNpcsMod.cs` (`public NameLists Names`, loaded in `Awake`), `SelfTest.cs`; tests `PopulationPlannerTests.cs` (FakeNames signature, `Names_UseListOrDefaultForRace`), `FolderParserTests` (`nameList` read and normalized).

**Interfaces:** `NameLists : INameSource` with `static NameLists Load(string folder)`, `void Add(NameList list)` (self-test), `Generate(...)` — unknown list → one warning per name, fall back to `default_<race>`, then `default_breton`, then `"Stranger"`. Vanilla banks: `Resources.Load<TextAsset>("NameGen")`, replaced by `StreamingAssets/Text/NameGen.txt` if present, deserialized with `SaveLoadManager.Deserialize(typeof(Dictionary<NameHelper.BankTypes, NameHelper.NameBank>), text)`; each bank whose style is known becomes `default_<lowercase bank name>`. Custom: `ANPCs/_Namelists/*.json`; names starting `default_` ignored with a warning. Nord suffix: `TextManager.Instance.GetLocalizedText("nordSurnameImmutableSuffix")`.

- [ ] Steps: Core tests RED → implement → GREEN; self-test checks first (RED by compile): lists `selftest_simple` (`Testmale`/`Testfemale`) and `selftest_vanilla` (breton A,b,C,d,E,f) via `mod.Names.Add`, 1-person generic template each; names `Testmale|Testfemale` and `Ab Ef|Cd Ef`; implement runtime; checks; build `--no-examples`; self-test 36/36; commit `feat: generic names from custom or vanilla name lists`.

### Task 4: Portrait pools and lock

**Files:** Create `Scripts/Core/PortraitPools.cs`; modify `NpcState.cs` (`public string portrait;` in `Clone`, `IsDefault` requires it empty), `PortraitLibrary.cs` (`Names`, `Add`, `Remove`, `WarnMissing` via pools), `NpcSpawner.cs` (`ResolvePortrait`), `NpcTalk.cs` (`Init(instance, texture, portraitFile)`, `PortraitFile`; on successful talk with a persistent person store file in `brain.State.portrait` if empty), `SelfTest.cs`; tests `PortraitPoolsTests.cs`, `NpcStateTableTests`.

**Interfaces:** `PortraitPools.Pool(ICollection<string> files, string baseName) -> List<string>` (base and `base_<digits>`, sorted ordinal); `PortraitPools.Joined(ICollection<string> files, IList<string> bases) -> List<string>`; `PortraitPools.Choose(List<string> pool, string key) -> string` (null for empty; index from `new SeededRandom(StableHash.Of(key + "#portrait"))`). Spawn: stored `state.portrait` wins if the library has it; else `Choose(Joined(library.Names, def.Portraits), instance.Key)`; `instance.PortraitName` = chosen file.

- [ ] Steps: Core tests RED (every row of spec §6.1; `bram` not matching `bramble`/`bram_x`; Choose stable per key; state with portrait saved) → implement → GREEN; self-test check first: library gets `selftest_face_1`, `selftest_face_2`; 1-person same-people template with portrait `selftest_face`; talk → `State.portrait` set; add `selftest_face_3`, discard, respawn → same file; implement runtime; checks; build `--no-examples`; self-test 37/37; commit `feat: portrait pools; the face shown at first talk is kept`.

### Task 5: Examples, docs, release

**Files:** `Examples/ANPCs/` — delete `daggerfall_city_*` (+ metas), add generic `cooper`, `baker`, `smith` per spec §4, `_Namelists/README.txt`; `README.md`; manifest version `0.3.0`; test install: remove `ANPCs/daggerfall_city_*`.

- [ ] Steps: write files; full verification loop; `build-mod.sh` (with examples); self-test 37/37; Player.log shows cooper/baker/smith; commit `feat: ship generic cooper, baker and smith; document name lists and portrait pools`; hand-off checklist to the user.

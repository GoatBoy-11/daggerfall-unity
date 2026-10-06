# Advanced NPCs v2 (Phases A and B) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move ANPC definitions to one folder per NPC (with automatic migration of 1a files), let the player talk to any calm ANPC through DFU's citizen talk window with a PNG portrait, and populate towns with generic ANPCs whose persistence is a mod setting.

**Architecture:** Pure logic (parsing, migration, seeding, population planning, save pruning) goes into the existing `AdvancedNPCs.Core` assembly and is NUnit-tested in the Unity 6 test host. DFU glue (file access, portraits, talk proxy, settings, spawning) goes into `Scripts/Runtime` and is verified with the compile checks and the unattended in-game self-test. A new Core type `NpcInstance` is what gets spawned: unique ANPCs become one instance keyed by id, generic templates become planned instances keyed `<template>@<mapId>#<n>`.

**Tech Stack:** Unity 2019.4.41f2 (mod build), Unity 6000.6.4f1 (Core test host only), C# (conservative subset), Daggerfall Unity 1.9.2 mod API, NUnit (Unity Test Framework), DFU FullSerializer save data and mod settings.

**Spec:** `docs/superpowers/specs/2026-10-06-advanced-npcs-v2-design.md`

**Scope:** Phases A and B of the spec. Phase C (extra dialogue topics, spec §9.1 and §9.3) starts with a mechanism spike whose findings the spec requires before its plan is written; it gets its own plan after this one. `dialogue.json` is only touched here where the spec requires it for Phase B (a warning when a generic template has one).

## Global Constraints

- Unity Editor for the DFU project: **2019.4.41f2 only** — `C:\Program Files\Unity\Hub\Editor\2019.4.41f2\Editor\Unity.exe`. Never open the project with any Unity 6 editor. (The Core test host `F:\_Projects\Dagerfall\anpc_testhost` is a separate Unity 6 project; `Tools~/run-tests.sh` handles it.)
- Target: Daggerfall Unity **1.9.2** test install at `F:\_Projects\Dagerfall\DFU_testing`.
- Branch: `feature/advanced-npcs-v2`. Never commit to `master`. Do not push unless the user asks.
- C# subset (DFU compiles mod sources at runtime with an old compiler): **no** string interpolation `$""`, **no** `?.` / `??=`, **no** expression-bodied members `=>`, **no** `nameof`, **no** `out var`, **no** tuples, **no** pattern matching (`is X x`), **no nested enums** (TypeLoadException in DFU's compiler). `var`, generics, LINQ, `delegate (...) { }`, plain `is` type checks are fine. Match the surrounding style: explicit types.
- `UnityEngine.JsonUtility` cannot fill nested classes from the mod assembly; Core parses JSON with its own `Json.Parse`.
- Do not name a type like a UnityEngine or DFU type (checked: none of the new names in this plan clash).
- All log lines start with `[AdvancedNPCs] ` (use `AdvancedNpcsMod.Log` / `AdvancedNpcsMod.LogError`).
- Data root: `<DFU>/DaggerfallUnity_Data/StreamingAssets/ANPCs/`; shared portraits in `ANPCs/_Portraits/`; the 1a folder `StreamingAssets/AdvancedNPCs/` is only read for migration.
- Message format: `<folder>/<file>: <field>: <problem>` (spec §14).
- Generic instance key: `<template>@<mapId>#<n>` with `mapId = DaggerfallLocation.Summary.MapID`, `n` from 0 (spec §7.4).
- Setting keys: section `Population`, keys `GenericPeople` (0 = `Same people every visit`, 1 = `Random each visit`, default 0) and `MaxGenericPerTown` (slider 0–30, default 12) (spec §8).
- Generic defaults: `spawn.locationTypes` `TownCity, TownHamlet, TownVillage`; `spawn.count` `[1, 3]`, bounds `0 <= min <= max <= 20` (spec §7.3).
- Every new `.cs` under `Scripts/` must be added to `Files` in `AdvancedNPCs.dfmod.json` in the same task. Unity creates `.meta` files for new files and folders under `Assets/` when the 2019.4 editor next opens the project (`build-mod.sh` does this); commit them in the task that runs the build.
- Do not modify files outside `Assets/Game/Mods/AdvancedNPCs/` and `docs/`. After every `build-mod.sh` run: `git restore Assets/AddressableAssetsData/` (Unity import churn).

### Verification commands (run from the repo root `F:\_Projects\Dagerfall\daggerfall-unity` in Git Bash)

```bash
"Assets/Game/Mods/AdvancedNPCs/Tools~/run-tests.sh"
```
Core NUnit tests (Unity 6 host). Prints `result="Passed" total=… passed=… failed=…`; on a compile error prints `NO RESULTS FILE (compile error?)` and the `error CS…` lines. Exit 0 only if all passed.

```bash
"Assets/Game/Mods/AdvancedNPCs/Tools~/compile-check.sh"
```
Compiles all mod sources against DFU_testing's assemblies. Expected: `compile-check: OK (N source files)`.

```bash
"Assets/Game/Mods/AdvancedNPCs/Tools~/runtime-check.sh"
```
Compiles with DFU's own runtime compiler and loads every type. Expected: no errors, exit 0.

```bash
cd Assets/Game/Mods/AdvancedNPCs && for f in $(find Scripts -name '*.cs' | sort); do grep -q "\"Assets/Game/Mods/AdvancedNPCs/$f\"" AdvancedNPCs.dfmod.json || echo "MISSING in manifest: $f"; done; cd - > /dev/null
```
Manifest check. Expected: no output.

```bash
"Assets/Game/Mods/AdvancedNPCs/Tools~/build-mod.sh"
git restore Assets/AddressableAssetsData/
```
Builds `advancednpcs.dfmod` with a windowed Unity 2019.4 that closes itself and installs it (plus examples) into DFU_testing. The Unity editor and the game must be closed. `--no-examples` skips copying examples.

```bash
"Assets/Game/Mods/AdvancedNPCs/Tools~/selftest.sh"
```
Starts DFU_testing, loads save `AtDaggerfall` of character `Testo`, runs `anpc_selftest`, quits, prints `SELFTEST` lines. Exit 0 only if every check passed. Expected last line: `SELFTEST DONE <n>/<n> passed`.

Player.log (Git Bash): `"$USERPROFILE/AppData/LocalLow/Daggerfall Workshop/Daggerfall Unity/Player.log"`.

## Review Focus

1. **A 1a file whose `id` is the last field, or that has Windows line endings** — migration must still produce valid `npc.json` (no dangling comma, CRLF kept). Pinned by `MigrationTests.IdLast_RemovesPrecedingComma` and `MigrationTests.CrlfFile_KeepsLineEndings` (Task 3).
2. **Portrait referenced as `"Bram.PNG"` or `" bram "` while the file is `bram.png`** — must find the same portrait. Pinned by `PortraitNamesTests.Normalize` (Task 1).
3. **A folder named `Bram` or `my npc` (typical on Windows)** — must be reported by name while the other ANPCs still load. Pinned by `DefinitionCatalogTests.InvalidFolderName_IsReported_OthersStillLoad` (Task 2).
4. **Two towns with the same place name in different regions** — generic people and their saved state must not be shared. Pinned by `PopulationPlannerTests.SamePlaceNameDifferentMap_DifferentKeysAndPeople` (Task 9).
5. **A town with fewer walkable cells than planned instances, or none** — planning must not hang; instances share cells or are marked unplaced and skipped. Pinned by `PopulationPlannerTests.FewCells_ReusesCells` and `PopulationPlannerTests.NoCells_MarksInstancesUnplaced` (Task 9).

---

## File Structure

```
Assets/Game/Mods/AdvancedNPCs/
  AdvancedNPCs.dfmod.json          manifest (Files list grows per task; version 0.2.0 in Task 11)
  modsettings.json                 generated by the builder (Task 10)
  README.md                        author guide (Tasks 3, 7, 11)
  Scripts/Core/                    (assembly AdvancedNPCs.Core — pure, unit-tested)
    FieldReader.cs        NEW  typed JSON field readers + unknown-field detection (Task 1)
    PortraitNames.cs      NEW  portrait name normalisation (Task 1)
    NpcDefinition.cs      MOD  NpcKind enum; Kind, Race, Portraits, Names, Folder (Task 1); Spawn (Task 8)
    DefinitionParser.cs   MOD  ParseFolder (unique: Task 1, generic: Task 8); legacy Parse kept for migration
    DefinitionCatalog.cs  MOD  built from AnpcFolder list; uniques + generics (Task 2)
    Migration.cs          NEW  1a file -> npc.json text (Task 3)
    StableHash.cs         NEW  FNV-1a 32 (Task 4)
    SeededRandom.cs       NEW  xorshift32 (Task 4)
    VanillaFaces.cs       NEW  vanilla face record tables (Task 4)
    NpcInstance.cs        NEW  what gets spawned (Task 4)
    NpcState.cs           MOD  IsDefault; Snapshot omits defaults (Task 5)
    GenericSpawn.cs       NEW  SpawnPlace, GenericSpawn, TownInfo, LocationTypeNames (Task 8)
    PopulationPlanner.cs  NEW  GenericMode, INameSource, Plan (Task 9)
  Scripts/Runtime/                 (Assembly-CSharp — DFU glue)
    AnpcFiles.cs          NEW  all ANPC file I/O, migration run (Tasks 2, 3)
    AdvancedNpcsMod.cs    MOD  wiring, commands (Tasks 2, 3, 6, 7, 10)
    NpcSpawner.cs         MOD  instances (Task 6), talk (Task 7), generics (Task 11)
    NpcBrain.cs           MOD  Init(NpcInstance, NpcState), Key/DisplayName (Task 6), CurrentHealthFraction (Task 11)
    PortraitLibrary.cs    NEW  _Portraits loading (Task 7)
    NpcTalk.cs            NEW  talk proxy, Info text, portrait (Task 7)
    ModConfig.cs          NEW  mod settings (Task 10)
    DfuNameSource.cs      NEW  seeded DFU person names (Task 11)
    SelfTest.cs           MOD  new checks (Tasks 6, 7, 10, 11)
  Editor/
    AdvancedNpcsModBuilder.cs MOD  writes modsettings.json before building (Task 10)
    ModSettingsWriter.cs  NEW  modsettings.json via DFU's own serializer (Task 10)
    Tests/                       new and changed NUnit tests per task
  Examples/ANPCs/                  mirrors the game layout (Task 2; commoner in Task 11)
  Tools~/build-mod.sh, selftest.sh MOD  ANPCs paths (Task 2)
```

---

## Phase A — folders, migration, portraits, talking

### Task 1: Folder-aware definition parsing

**Files:**
- Create: `Assets/Game/Mods/AdvancedNPCs/Scripts/Core/FieldReader.cs`
- Create: `Assets/Game/Mods/AdvancedNPCs/Scripts/Core/PortraitNames.cs`
- Modify: `Assets/Game/Mods/AdvancedNPCs/Scripts/Core/NpcDefinition.cs` (whole file)
- Modify: `Assets/Game/Mods/AdvancedNPCs/Scripts/Core/DefinitionParser.cs` (whole file)
- Modify: `Assets/Game/Mods/AdvancedNPCs/AdvancedNPCs.dfmod.json`
- Test: `Assets/Game/Mods/AdvancedNPCs/Editor/Tests/FolderParserTests.cs`, `Assets/Game/Mods/AdvancedNPCs/Editor/Tests/PortraitNamesTests.cs`

**Interfaces:**
- Consumes: `Json.Parse(string) -> object` (Dictionary<string, object> / List<object> / double / bool / string / null), `JsonException`, `HumanClasses.Canonical(string) -> string or null` (all existing).
- Produces:
  - `enum NpcKind { Unique, Generic }` (top level in `AdvancedNPCs.Core`).
  - `NpcDefinition` new members: `NpcKind Kind = NpcKind.Unique`, `string Name = ""`, `string Race = ""`, `readonly List<string> Portraits`, `readonly List<string> Names`, `string Folder = ""`. (Task 8 adds `GenericSpawn Spawn`.)
  - `ParseResult.Warnings : List<string>`.
  - `DefinitionParser.ParseFolder(string folder, string json) -> ParseResult`, `DefinitionParser.IsValidFolderName(string) -> bool`; legacy `DefinitionParser.Parse(string fileName, string json) -> ParseResult` behaves as in 1a.
  - `FieldReader.Text/Number/Bool/Numbers/Texts/Object/Objects/UnknownKeys/Num` (signatures in Step 3).
  - `PortraitNames.Normalize(string) -> string`.

- [ ] **Step 1: Write the failing tests**

`Assets/Game/Mods/AdvancedNPCs/Editor/Tests/PortraitNamesTests.cs`:

```csharp
using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class PortraitNamesTests
    {
        [TestCase("bram", "bram")]
        [TestCase("Bram.PNG", "bram")]
        [TestCase("  bram.png ", "bram")]
        [TestCase("commoner_1", "commoner_1")]
        [TestCase("", "")]
        public void Normalize(string raw, string expected)
        {
            Assert.AreEqual(expected, PortraitNames.Normalize(raw));
        }

        [Test]
        public void Null_IsEmpty()
        {
            Assert.AreEqual("", PortraitNames.Normalize(null));
        }
    }
}
```

`Assets/Game/Mods/AdvancedNPCs/Editor/Tests/FolderParserTests.cs`:

```csharp
using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class FolderParserTests
    {
        const string Unique =
            "{ \"name\": \"Bram\", " +
            "\"location\": { \"region\": \"Daggerfall\", \"place\": \"Daggerfall\" }, " +
            "\"position\": [1.5, 2, -3] }";

        static string With(string extraFields)
        {
            return Unique.Substring(0, Unique.Length - 1) + ", " + extraFields + " }";
        }

        [Test]
        public void Unique_FolderNameIsId_DefaultsApply()
        {
            ParseResult r = DefinitionParser.ParseFolder("bram", Unique);
            Assert.IsTrue(r.Ok, r.Error);
            NpcDefinition d = r.Definition;
            Assert.AreEqual("bram", d.Id);
            Assert.AreEqual("bram", d.Folder);
            Assert.AreEqual("bram/npc.json", d.SourceFile);
            Assert.AreEqual(NpcKind.Unique, d.Kind);
            Assert.AreEqual("Bram", d.Name);
            Assert.AreEqual(-3f, d.Z);
            Assert.AreEqual("", d.Race);
            Assert.AreEqual(0, d.Portraits.Count);
            Assert.AreEqual("Spellsword", d.BaseClass);
            Assert.AreEqual(8f, d.WanderRadius);
            Assert.AreEqual(0, r.Warnings.Count);
        }

        [Test]
        public void Unique_KindPortraitAndRace_AreRead()
        {
            ParseResult r = DefinitionParser.ParseFolder("bram",
                With("\"kind\": \"Unique\", \"portrait\": \"Bram.png\", \"race\": \"nord\""));
            Assert.IsTrue(r.Ok, r.Error);
            CollectionAssert.AreEqual(new[] { "bram" }, r.Definition.Portraits);
            Assert.AreEqual("Nord", r.Definition.Race);
        }

        [Test]
        public void BadRace_IsRejected()
        {
            ParseResult r = DefinitionParser.ParseFolder("bram", With("\"race\": \"Khajiit\""));
            Assert.AreEqual("bram/npc.json: race: must be Breton, Redguard or Nord (got \"Khajiit\")", r.Error);
        }

        [Test]
        public void BadKind_IsRejected()
        {
            ParseResult r = DefinitionParser.ParseFolder("bram", With("\"kind\": \"boss\""));
            Assert.AreEqual("bram/npc.json: kind: must be unique or generic (got \"boss\")", r.Error);
        }

        [Test]
        public void IdDifferentFromFolder_WarnsAndUsesFolder()
        {
            ParseResult r = DefinitionParser.ParseFolder("bram", With("\"id\": \"old_bram\""));
            Assert.IsTrue(r.Ok, r.Error);
            Assert.AreEqual("bram", r.Definition.Id);
            CollectionAssert.Contains(r.Warnings, "bram/npc.json: id: \"old_bram\" differs from the folder name; using \"bram\"");
        }

        [Test]
        public void IdSameAsFolder_NoWarning()
        {
            ParseResult r = DefinitionParser.ParseFolder("bram", With("\"id\": \"bram\""));
            Assert.IsTrue(r.Ok, r.Error);
            Assert.AreEqual(0, r.Warnings.Count);
        }

        [Test]
        public void UnknownField_Warns()
        {
            ParseResult r = DefinitionParser.ParseFolder("bram", With("\"bravry\": \"Brave\""));
            Assert.IsTrue(r.Ok, r.Error);
            CollectionAssert.Contains(r.Warnings, "bram/npc.json: bravry: unknown field, ignored");
        }

        [Test]
        public void GenericOnlyField_OnUnique_IsRejected()
        {
            ParseResult r = DefinitionParser.ParseFolder("bram", With("\"spawn\": {}"));
            Assert.AreEqual("bram/npc.json: spawn: belongs to generic templates (\"kind\": \"generic\")", r.Error);
        }

        [TestCase("Bram")]
        [TestCase("my npc")]
        [TestCase("_portraits")]
        [TestCase("")]
        public void InvalidFolderName_IsRejected(string folder)
        {
            ParseResult r = DefinitionParser.ParseFolder(folder, Unique);
            Assert.IsFalse(r.Ok);
            StringAssert.StartsWith(folder + ": folder: use only lowercase letters, digits and underscore", r.Error);
        }

        [Test]
        public void MissingName_ReportsFolderPath()
        {
            ParseResult r = DefinitionParser.ParseFolder("bram",
                "{ \"location\": { \"region\": \"R\", \"place\": \"P\" }, \"position\": [0,0,0] }");
            Assert.AreEqual("bram/npc.json: name: required", r.Error);
        }

        [Test]
        public void InvalidJson_ReportsFolderPath()
        {
            ParseResult r = DefinitionParser.ParseFolder("bram", "{ nope");
            StringAssert.StartsWith("bram/npc.json: file: invalid JSON", r.Error);
        }

        [Test]
        public void LegacyParse_StillReadsIdFromFile()
        {
            ParseResult r = DefinitionParser.Parse("bram.json",
                "{ \"id\": \"bram\", \"name\": \"Bram\", \"location\": { \"region\": \"R\", \"place\": \"P\" }, \"position\": [0,0,0] }");
            Assert.IsTrue(r.Ok, r.Error);
            Assert.AreEqual("bram", r.Definition.Id);
            Assert.AreEqual(NpcKind.Unique, r.Definition.Kind);
            Assert.AreEqual("bram.json", r.Definition.SourceFile);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `"Assets/Game/Mods/AdvancedNPCs/Tools~/run-tests.sh"`
Expected: `NO RESULTS FILE (compile error?)` with errors such as `error CS0103: The name 'PortraitNames' does not exist in the current context` and `error CS0117: 'DefinitionParser' does not contain a definition for 'ParseFolder'`.

- [ ] **Step 3: Implement**

`Assets/Game/Mods/AdvancedNPCs/Scripts/Core/PortraitNames.cs`:

```csharp
using System;

namespace AdvancedNPCs.Core
{
    /// <summary>Portrait names as used in npc.json and as keys of _Portraits/*.png: trimmed, lowercase, no ".png".</summary>
    public static class PortraitNames
    {
        public static string Normalize(string name)
        {
            if (name == null)
                return "";
            string n = name.Trim().ToLowerInvariant();
            if (n.EndsWith(".png", StringComparison.Ordinal))
                n = n.Substring(0, n.Length - 4);
            return n;
        }
    }
}
```

`Assets/Game/Mods/AdvancedNPCs/Scripts/Core/FieldReader.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;

namespace AdvancedNPCs.Core
{
    /// <summary>
    /// Typed readers for objects produced by Json.Parse. A missing or null key yields the fallback (or null);
    /// a value of the wrong type yields a problem text, otherwise the readers return null.
    /// </summary>
    public static class FieldReader
    {
        public static string Text(Dictionary<string, object> o, string key, string fallback, out string value)
        {
            object raw;
            value = fallback;
            if (!o.TryGetValue(key, out raw) || raw == null)
                return null;
            value = raw as string;
            return value == null ? "must be text" : null;
        }

        public static string Number(Dictionary<string, object> o, string key, double fallback, out double value)
        {
            object raw;
            value = fallback;
            if (!o.TryGetValue(key, out raw) || raw == null)
                return null;
            if (!(raw is double))
                return "must be a number";
            value = (double)raw;
            return null;
        }

        public static string Bool(Dictionary<string, object> o, string key, bool fallback, out bool value)
        {
            object raw;
            value = fallback;
            if (!o.TryGetValue(key, out raw) || raw == null)
                return null;
            if (!(raw is bool))
                return "must be true or false";
            value = (bool)raw;
            return null;
        }

        /// <summary>False if present but not an array of numbers; values is null when the key is missing.</summary>
        public static bool Numbers(Dictionary<string, object> o, string key, out double[] values)
        {
            object raw;
            values = null;
            if (!o.TryGetValue(key, out raw) || raw == null)
                return true;
            List<object> list = raw as List<object>;
            if (list == null)
                return false;
            values = new double[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                if (!(list[i] is double))
                {
                    values = null;
                    return false;
                }
                values[i] = (double)list[i];
            }
            return true;
        }

        /// <summary>A list of non-empty texts; values is null when the key is missing.</summary>
        public static string Texts(Dictionary<string, object> o, string key, out List<string> values)
        {
            object raw;
            values = null;
            if (!o.TryGetValue(key, out raw) || raw == null)
                return null;
            List<object> list = raw as List<object>;
            if (list == null)
                return "must be a list of texts";
            List<string> texts = new List<string>();
            foreach (object item in list)
            {
                string s = item as string;
                if (s == null || s.Trim().Length == 0)
                    return "must be a list of non-empty texts";
                texts.Add(s);
            }
            values = texts;
            return null;
        }

        /// <summary>An object; value is null when the key is missing.</summary>
        public static string Object(Dictionary<string, object> o, string key, out Dictionary<string, object> value)
        {
            object raw;
            value = null;
            if (!o.TryGetValue(key, out raw) || raw == null)
                return null;
            value = raw as Dictionary<string, object>;
            return value == null ? "must be an object" : null;
        }

        /// <summary>A list of objects; values is null when the key is missing.</summary>
        public static string Objects(Dictionary<string, object> o, string key, out List<Dictionary<string, object>> values)
        {
            object raw;
            values = null;
            if (!o.TryGetValue(key, out raw) || raw == null)
                return null;
            List<object> list = raw as List<object>;
            if (list == null)
                return "must be a list of objects";
            List<Dictionary<string, object>> objects = new List<Dictionary<string, object>>();
            foreach (object item in list)
            {
                Dictionary<string, object> d = item as Dictionary<string, object>;
                if (d == null)
                    return "must be a list of objects";
                objects.Add(d);
            }
            values = objects;
            return null;
        }

        /// <summary>Keys of o that are not in known, sorted.</summary>
        public static List<string> UnknownKeys(Dictionary<string, object> o, ICollection<string> known)
        {
            List<string> unknown = new List<string>();
            foreach (string key in o.Keys)
            {
                if (!known.Contains(key))
                    unknown.Add(key);
            }
            unknown.Sort(StringComparer.Ordinal);
            return unknown;
        }

        public static string Num(double f)
        {
            return f.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
```

`Assets/Game/Mods/AdvancedNPCs/Scripts/Core/NpcDefinition.cs` (whole file):

```csharp
using System.Collections.Generic;

namespace AdvancedNPCs.Core
{
    public enum Bravery
    {
        Coward,
        Normal,
        Brave,
    }

    public enum NpcKind
    {
        Unique,
        Generic,
    }

    /// <summary>A validated ANPC definition (one folder's npc.json) used at runtime.</summary>
    public class NpcDefinition
    {
        public string Id;
        public NpcKind Kind = NpcKind.Unique;
        public string Name = "";
        public string Region;
        public string Place;
        public float X;
        public float Y;
        public float Z;
        public string BaseClass;
        public string Gender;
        /// <summary>"Breton", "Redguard", "Nord", or "" for the region's people.</summary>
        public string Race = "";
        /// <summary>Normalised portrait names (PortraitNames); a unique ANPC has 0 or 1.</summary>
        public readonly List<string> Portraits = new List<string>();
        /// <summary>Generic templates: names to pick from (empty: fixed Name or generated).</summary>
        public readonly List<string> Names = new List<string>();
        public Bravery Bravery;
        public int FleeHealthPercent;
        public float CalmDownMinHours;
        public float CalmDownMaxHours;
        public bool CrimeOnAttack;
        public float WanderRadius;
        /// <summary>Folder name under ANPCs (equals Id).</summary>
        public string Folder = "";
        /// <summary>Path used in messages, e.g. "bram/npc.json".</summary>
        public string SourceFile;
    }
}
```

`Assets/Game/Mods/AdvancedNPCs/Scripts/Core/DefinitionParser.cs` (whole file):

```csharp
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AdvancedNPCs.Core
{
    public class ParseResult
    {
        public NpcDefinition Definition;
        public string Error;
        public readonly List<string> Warnings = new List<string>();

        public bool Ok
        {
            get { return Definition != null; }
        }
    }

    /// <summary>
    /// Turns one ANPC folder's npc.json (or, for migration, a 1a flat definition file) into a validated
    /// NpcDefinition or a single error line, plus warnings.
    /// </summary>
    public static class DefinitionParser
    {
        static readonly Regex IdPattern = new Regex("^[a-z0-9_]+$");

        static readonly string[] SharedKeys =
        {
            "id", "kind", "race", "baseClass", "gender", "bravery", "fleeHealthPercent", "calmDownHours",
            "crimeOnAttack", "wanderRadius",
        };
        static readonly string[] UniqueKeys = { "name", "location", "position", "portrait" };
        static readonly string[] GenericKeys = { "name", "names", "portrait", "portraits", "spawn" };
        static readonly string[] GenericOnlyKeys = { "names", "portraits", "spawn" };
        static readonly string[] RaceNames = { "Breton", "Redguard", "Nord" };

        /// <summary>A 1a flat file from StreamingAssets/AdvancedNPCs (the id is inside the file). Used by migration.</summary>
        public static ParseResult Parse(string fileName, string json)
        {
            ParseResult r = new ParseResult();
            Dictionary<string, object> o = Root(fileName, json, r);
            if (o == null)
                return r;

            string problem;
            string id;
            if ((problem = FieldReader.Text(o, "id", null, out id)) != null)
                return Fail(r, fileName, "id", problem);
            if (string.IsNullOrEmpty(id))
                return Fail(r, fileName, "id", "required");
            if (!IdPattern.IsMatch(id))
                return Fail(r, fileName, "id", "use only lowercase letters, digits and underscore (got \"" + id + "\")");

            NpcDefinition d = new NpcDefinition();
            d.Id = id;
            d.Folder = id;
            d.SourceFile = fileName;
            if (!ReadUnique(fileName, o, d, r) || !ReadShared(fileName, o, d, r))
                return r;
            r.Definition = d;
            return r;
        }

        /// <summary>True for names usable as an ANPC folder (and so as an id).</summary>
        public static bool IsValidFolderName(string folder)
        {
            return !string.IsNullOrEmpty(folder) && IdPattern.IsMatch(folder) && folder[0] != '_';
        }

        /// <summary>One ANPC folder's npc.json; the folder name is the id (spec §6, §7).</summary>
        public static ParseResult ParseFolder(string folder, string json)
        {
            ParseResult r = new ParseResult();
            if (!IsValidFolderName(folder))
                return Fail(r, folder, "folder",
                    "use only lowercase letters, digits and underscore, not starting with _ (got \"" + folder + "\")");

            string file = folder + "/npc.json";
            Dictionary<string, object> o = Root(file, json, r);
            if (o == null)
                return r;

            string problem;
            string jsonId;
            if (FieldReader.Text(o, "id", null, out jsonId) == null && !string.IsNullOrEmpty(jsonId) && jsonId != folder)
                r.Warnings.Add(file + ": id: \"" + jsonId + "\" differs from the folder name; using \"" + folder + "\"");

            string rawKind;
            if ((problem = FieldReader.Text(o, "kind", "unique", out rawKind)) != null)
                return Fail(r, file, "kind", problem);
            NpcDefinition d = new NpcDefinition();
            if (string.Equals(rawKind.Trim(), "unique", StringComparison.OrdinalIgnoreCase))
                d.Kind = NpcKind.Unique;
            else if (string.Equals(rawKind.Trim(), "generic", StringComparison.OrdinalIgnoreCase))
                d.Kind = NpcKind.Generic;
            else
                return Fail(r, file, "kind", "must be unique or generic (got \"" + rawKind + "\")");

            d.Id = folder;
            d.Folder = folder;
            d.SourceFile = file;
            bool ok = d.Kind == NpcKind.Unique ? ReadUnique(file, o, d, r) : ReadGeneric(file, o, d, r);
            if (!ok || !ReadShared(file, o, d, r))
                return r;

            List<string> known = new List<string>(SharedKeys);
            known.AddRange(d.Kind == NpcKind.Unique ? UniqueKeys : GenericKeys);
            foreach (string key in FieldReader.UnknownKeys(o, known))
                r.Warnings.Add(file + ": " + key + ": unknown field, ignored");

            r.Definition = d;
            return r;
        }

        static Dictionary<string, object> Root(string file, string json, ParseResult r)
        {
            if (string.IsNullOrEmpty(json) || json.Trim().Length == 0)
            {
                Fail(r, file, "file", "empty");
                return null;
            }
            object root;
            try
            {
                root = Json.Parse(json);
            }
            catch (JsonException e)
            {
                Fail(r, file, "file", "invalid JSON (" + e.Message + ")");
                return null;
            }
            Dictionary<string, object> o = root as Dictionary<string, object>;
            if (o == null)
                Fail(r, file, "file", "invalid JSON (top level must be an object)");
            return o;
        }

        static bool ReadUnique(string file, Dictionary<string, object> o, NpcDefinition d, ParseResult r)
        {
            foreach (string key in GenericOnlyKeys)
            {
                if (o.ContainsKey(key))
                    return Problem(r, file, key, "belongs to generic templates (\"kind\": \"generic\")");
            }

            string problem;
            string name;
            if ((problem = FieldReader.Text(o, "name", null, out name)) != null)
                return Problem(r, file, "name", problem);
            if (string.IsNullOrEmpty(name))
                return Problem(r, file, "name", "required");
            d.Name = name;

            object locationValue;
            o.TryGetValue("location", out locationValue);
            Dictionary<string, object> location = locationValue as Dictionary<string, object>;
            if (locationValue != null && location == null)
                return Problem(r, file, "location", "must be an object with region and place");
            if (location == null)
                location = new Dictionary<string, object>();

            string region;
            if ((problem = FieldReader.Text(location, "region", null, out region)) != null)
                return Problem(r, file, "location.region", problem);
            if (string.IsNullOrEmpty(region))
                return Problem(r, file, "location.region", "required");
            string place;
            if ((problem = FieldReader.Text(location, "place", null, out place)) != null)
                return Problem(r, file, "location.place", problem);
            if (string.IsNullOrEmpty(place))
                return Problem(r, file, "location.place", "required");

            double[] position;
            if (!FieldReader.Numbers(o, "position", out position) || position == null || position.Length != 3)
                return Problem(r, file, "position", "required, must be [x, y, z]");

            string portrait;
            if ((problem = FieldReader.Text(o, "portrait", "", out portrait)) != null)
                return Problem(r, file, "portrait", problem);
            string normalised = PortraitNames.Normalize(portrait);
            if (normalised.Length > 0)
                d.Portraits.Add(normalised);

            d.Region = region;
            d.Place = place;
            d.X = (float)position[0];
            d.Y = (float)position[1];
            d.Z = (float)position[2];
            return true;
        }

        // Generic templates are read from Task 8 on (spec §7.3).
        static bool ReadGeneric(string file, Dictionary<string, object> o, NpcDefinition d, ParseResult r)
        {
            return Problem(r, file, "kind", "generic templates are not supported yet");
        }

        static bool ReadShared(string file, Dictionary<string, object> o, NpcDefinition d, ParseResult r)
        {
            string problem;

            string rawClass;
            if ((problem = FieldReader.Text(o, "baseClass", "Spellsword", out rawClass)) != null)
                return Problem(r, file, "baseClass", problem);
            string baseClass = HumanClasses.Canonical(rawClass);
            if (baseClass == null)
                return Problem(r, file, "baseClass", "unknown class \"" + rawClass + "\"");

            string rawGender;
            if ((problem = FieldReader.Text(o, "gender", "", out rawGender)) != null)
                return Problem(r, file, "gender", problem);
            string gender;
            if (string.IsNullOrEmpty(rawGender))
                gender = "";
            else if (string.Equals(rawGender, "Male", StringComparison.OrdinalIgnoreCase))
                gender = "Male";
            else if (string.Equals(rawGender, "Female", StringComparison.OrdinalIgnoreCase))
                gender = "Female";
            else
                return Problem(r, file, "gender", "must be Male or Female (got \"" + rawGender + "\")");

            string rawBravery;
            if ((problem = FieldReader.Text(o, "bravery", "Normal", out rawBravery)) != null)
                return Problem(r, file, "bravery", problem);
            Bravery bravery;
            if (!TryParseBravery(rawBravery, out bravery))
                return Problem(r, file, "bravery", "must be Coward, Normal or Brave (got \"" + rawBravery + "\")");

            double flee;
            if ((problem = FieldReader.Number(o, "fleeHealthPercent", 25, out flee)) != null || flee != Math.Floor(flee))
                return Problem(r, file, "fleeHealthPercent", "must be a whole number");
            if (flee < 1 || flee > 99)
                return Problem(r, file, "fleeHealthPercent", "must be 1-99 (got " + FieldReader.Num(flee) + ")");

            double[] calm;
            if (!FieldReader.Numbers(o, "calmDownHours", out calm) || (calm != null && calm.Length != 2))
                return Problem(r, file, "calmDownHours", "must be [min, max]");
            if (calm == null)
                calm = new double[] { 6, 48 };
            if (!(calm[0] > 0) || calm[0] > calm[1])
                return Problem(r, file, "calmDownHours",
                    "need 0 < min <= max (got [" + FieldReader.Num(calm[0]) + ", " + FieldReader.Num(calm[1]) + "])");

            double wander;
            if ((problem = FieldReader.Number(o, "wanderRadius", 8, out wander)) != null)
                return Problem(r, file, "wanderRadius", problem);
            if (wander < 0)
                return Problem(r, file, "wanderRadius", "must be 0 or more (got " + FieldReader.Num(wander) + ")");

            bool crime;
            if ((problem = FieldReader.Bool(o, "crimeOnAttack", true, out crime)) != null)
                return Problem(r, file, "crimeOnAttack", problem);

            string rawRace;
            if ((problem = FieldReader.Text(o, "race", "", out rawRace)) != null)
                return Problem(r, file, "race", problem);
            string race = "";
            if (rawRace.Trim().Length > 0)
            {
                race = CanonicalRace(rawRace);
                if (race == null)
                    return Problem(r, file, "race", "must be Breton, Redguard or Nord (got \"" + rawRace + "\")");
            }

            d.BaseClass = baseClass;
            d.Gender = gender;
            d.Bravery = bravery;
            d.FleeHealthPercent = (int)flee;
            d.CalmDownMinHours = (float)calm[0];
            d.CalmDownMaxHours = (float)calm[1];
            d.CrimeOnAttack = crime;
            d.WanderRadius = (float)wander;
            d.Race = race;
            return true;
        }

        static string CanonicalRace(string raw)
        {
            foreach (string race in RaceNames)
            {
                if (string.Equals(race, raw.Trim(), StringComparison.OrdinalIgnoreCase))
                    return race;
            }
            return null;
        }

        static bool TryParseBravery(string value, out Bravery bravery)
        {
            bravery = Bravery.Normal;
            if (value == null)
                return false;
            foreach (Bravery b in (Bravery[])Enum.GetValues(typeof(Bravery)))
            {
                if (string.Equals(b.ToString(), value.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    bravery = b;
                    return true;
                }
            }
            return false;
        }

        static bool Problem(ParseResult r, string file, string field, string problem)
        {
            r.Error = file + ": " + field + ": " + problem;
            r.Definition = null;
            return false;
        }

        static ParseResult Fail(ParseResult r, string file, string field, string problem)
        {
            Problem(r, file, field, problem);
            return r;
        }
    }
}
```

In `Assets/Game/Mods/AdvancedNPCs/AdvancedNPCs.dfmod.json`, add after the line `"Assets/Game/Mods/AdvancedNPCs/Scripts/Core/DefinitionParser.cs",`:

```json
        "Assets/Game/Mods/AdvancedNPCs/Scripts/Core/FieldReader.cs",
```

and after the line `"Assets/Game/Mods/AdvancedNPCs/Scripts/Core/NpcState.cs",`:

```json
        "Assets/Game/Mods/AdvancedNPCs/Scripts/Core/PortraitNames.cs",
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `"Assets/Game/Mods/AdvancedNPCs/Tools~/run-tests.sh"`
Expected: `result="Passed"`, `failed="0"`. All existing `DefinitionParserTests` (legacy `Parse`) still pass unchanged.

Run: `"Assets/Game/Mods/AdvancedNPCs/Tools~/compile-check.sh"`
Expected: `compile-check: OK (…)` (Runtime still only uses the unchanged legacy API).

- [ ] **Step 5: Commit**

```bash
git add Assets/Game/Mods/AdvancedNPCs/Scripts/Core/FieldReader.cs Assets/Game/Mods/AdvancedNPCs/Scripts/Core/PortraitNames.cs Assets/Game/Mods/AdvancedNPCs/Scripts/Core/NpcDefinition.cs Assets/Game/Mods/AdvancedNPCs/Scripts/Core/DefinitionParser.cs Assets/Game/Mods/AdvancedNPCs/Editor/Tests/FolderParserTests.cs Assets/Game/Mods/AdvancedNPCs/Editor/Tests/PortraitNamesTests.cs Assets/Game/Mods/AdvancedNPCs/AdvancedNPCs.dfmod.json
git commit -m "feat: parse ANPC folders (npc.json, kind, race, portrait, unknown-field warnings)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Catalog from folders, ANPCs root and example layout

**Files:**
- Modify: `Assets/Game/Mods/AdvancedNPCs/Scripts/Core/DefinitionCatalog.cs` (whole file)
- Create: `Assets/Game/Mods/AdvancedNPCs/Scripts/Runtime/AnpcFiles.cs`
- Modify: `Assets/Game/Mods/AdvancedNPCs/Scripts/Runtime/AdvancedNpcsMod.cs` (`FolderName`, `Awake`, `LoadCatalog`, `PlaceCommand`)
- Modify: `Assets/Game/Mods/AdvancedNPCs/Tools~/build-mod.sh` (examples block), `Assets/Game/Mods/AdvancedNPCs/Tools~/selftest.sh` (`FLAG`)
- Move: `Assets/Game/Mods/AdvancedNPCs/Examples/*.json` (+ `.meta`) → `Examples/ANPCs/<id>/npc.json`
- Modify: `Assets/Game/Mods/AdvancedNPCs/AdvancedNPCs.dfmod.json`
- Test: `Assets/Game/Mods/AdvancedNPCs/Editor/Tests/DefinitionCatalogTests.cs` (whole file)

**Interfaces:**
- Consumes: `DefinitionParser.ParseFolder`, `ParseResult.Warnings`, `NpcKind`, `NpcDefinition.Portraits/Folder/SourceFile` (Task 1); `DefinitionEditor.SetPlacement(string json, string region, string place, float x, float y, float z) -> string` (existing).
- Produces:
  - `class AnpcFolder { readonly string Name, NpcJson, DialogueJson; AnpcFolder(string name, string npcJson, string dialogueJson) }` (null json = file missing).
  - `DefinitionCatalog.Build(IEnumerable<AnpcFolder>) -> DefinitionCatalog`; `ById : Dictionary<string, NpcDefinition>` (unique only); `Generics : List<NpcDefinition>`; `Messages`; `Count` (uniques + generics); `ForLocation(region, place) -> List<NpcDefinition>`; `ReferencedPortraits() -> List<string>`; `static SamePlaceName(string a, string b) -> bool`.
  - `AnpcFiles.RootName = "ANPCs"`, `AnpcFiles.PortraitsName = "_Portraits"`, `AnpcFiles.Root`, `AnpcFiles.PortraitsFolder`, `AnpcFiles.NpcJsonPath(string folder)`, `AnpcFiles.ReadFolders() -> List<AnpcFolder>`, `AnpcFiles.SetPlacement(NpcDefinition def, string region, string place, float x, float y, float z)`.

- [ ] **Step 1: Write the failing tests**

Replace `Assets/Game/Mods/AdvancedNPCs/Editor/Tests/DefinitionCatalogTests.cs` with:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class DefinitionCatalogTests
    {
        static string Unique(string region, string place)
        {
            return "{ \"name\": \"N\", \"location\": { \"region\": \"" + region +
                   "\", \"place\": \"" + place + "\" }, \"position\": [0,0,0] }";
        }

        static string UniqueWithPortrait(string portrait)
        {
            return "{ \"name\": \"N\", \"location\": { \"region\": \"R\", \"place\": \"P\" }, \"position\": [0,0,0], " +
                   "\"portrait\": \"" + portrait + "\" }";
        }

        static AnpcFolder Folder(string name, string npcJson)
        {
            return new AnpcFolder(name, npcJson, null);
        }

        [Test]
        public void ValidFolders_AreIndexedById()
        {
            DefinitionCatalog c = DefinitionCatalog.Build(new[]
            {
                Folder("a", Unique("R", "P")),
                Folder("b", Unique("R", "P")),
            });
            Assert.AreEqual(2, c.Count);
            Assert.IsTrue(c.ById.ContainsKey("a"));
            Assert.IsTrue(c.ById.ContainsKey("b"));
            Assert.AreEqual(0, c.Generics.Count);
            CollectionAssert.Contains(c.Messages, "Loaded 2 unique ANPC(s) and 0 generic template(s).");
        }

        [Test]
        public void BadFolder_IsSkippedAndLogged_OthersStillLoad()
        {
            DefinitionCatalog c = DefinitionCatalog.Build(new[]
            {
                Folder("bad", "{ nope"),
                Folder("good", Unique("R", "P")),
            });
            Assert.AreEqual(1, c.Count);
            Assert.IsTrue(c.Messages.Exists(delegate (string m) { return m.StartsWith("bad/npc.json: file: invalid JSON"); }));
        }

        [Test]
        public void InvalidFolderName_IsReported_OthersStillLoad()
        {
            DefinitionCatalog c = DefinitionCatalog.Build(new[]
            {
                Folder("My NPC", Unique("R", "P")),
                Folder("good", Unique("R", "P")),
            });
            Assert.AreEqual(1, c.Count);
            Assert.IsTrue(c.ById.ContainsKey("good"));
            Assert.IsTrue(c.Messages.Exists(delegate (string m) { return m.StartsWith("My NPC: folder: use only lowercase letters"); }));
        }

        [Test]
        public void UnderscoreFolders_AreSharedDataNotNpcs()
        {
            DefinitionCatalog c = DefinitionCatalog.Build(new[] { Folder("_Portraits", null) });
            Assert.AreEqual(0, c.Count);
            CollectionAssert.AreEqual(new[] { "Loaded 0 unique ANPC(s) and 0 generic template(s)." }, c.Messages);
        }

        [Test]
        public void FolderWithoutNpcJson_IsSkippedWithWarning()
        {
            DefinitionCatalog c = DefinitionCatalog.Build(new[] { Folder("empty", null) });
            Assert.AreEqual(0, c.Count);
            CollectionAssert.Contains(c.Messages, "empty/npc.json: file: missing, folder skipped");
        }

        [Test]
        public void ParserWarnings_AreCollected()
        {
            string json = "{ \"name\": \"N\", \"location\": { \"region\": \"R\", \"place\": \"P\" }, \"position\": [0,0,0], \"colour\": \"red\" }";
            DefinitionCatalog c = DefinitionCatalog.Build(new[] { Folder("a", json) });
            Assert.AreEqual(1, c.Count);
            CollectionAssert.Contains(c.Messages, "a/npc.json: colour: unknown field, ignored");
        }

        [Test]
        public void ReferencedPortraits_AreDistinctAndSorted()
        {
            DefinitionCatalog c = DefinitionCatalog.Build(new[]
            {
                Folder("a", UniqueWithPortrait("z")),
                Folder("b", UniqueWithPortrait("A.png")),
                Folder("c", UniqueWithPortrait("z")),
                Folder("d", Unique("R", "P")),
            });
            CollectionAssert.AreEqual(new[] { "a", "z" }, c.ReferencedPortraits());
        }

        [Test]
        public void ForLocation_IgnoresCaseAndSpaces()
        {
            DefinitionCatalog c = DefinitionCatalog.Build(new[]
            {
                Folder("a", Unique("Daggerfall", "Daggerfall")),
                Folder("b", Unique("Wayrest", "Wayrest")),
            });
            List<NpcDefinition> found = c.ForLocation("daggerfall ", " DAGGERFALL");
            Assert.AreEqual(1, found.Count);
            Assert.AreEqual("a", found[0].Id);
        }

        [Test]
        public void ForLocation_NullNames_ReturnEmpty()
        {
            DefinitionCatalog c = DefinitionCatalog.Build(new[] { Folder("a", Unique("R", "P")) });
            Assert.AreEqual(0, c.ForLocation(null, null).Count);
        }

        [Test]
        public void NoFolders_IsEmptyNotError()
        {
            DefinitionCatalog c = DefinitionCatalog.Build(new AnpcFolder[0]);
            Assert.AreEqual(0, c.Count);
            CollectionAssert.Contains(c.Messages, "Loaded 0 unique ANPC(s) and 0 generic template(s).");
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `"Assets/Game/Mods/AdvancedNPCs/Tools~/run-tests.sh"`
Expected: `NO RESULTS FILE (compile error?)` with `error CS0246: The type or namespace name 'AnpcFolder' could not be found`.

- [ ] **Step 3: Implement the Core catalog**

Replace `Assets/Game/Mods/AdvancedNPCs/Scripts/Core/DefinitionCatalog.cs` with:

```csharp
using System;
using System.Collections.Generic;

namespace AdvancedNPCs.Core
{
    /// <summary>The files of one ANPC folder as read from disk; a null text means the file is missing.</summary>
    public class AnpcFolder
    {
        public readonly string Name;
        public readonly string NpcJson;
        public readonly string DialogueJson;

        public AnpcFolder(string name, string npcJson, string dialogueJson)
        {
            Name = name;
            NpcJson = npcJson;
            DialogueJson = dialogueJson;
        }
    }

    /// <summary>All loaded ANPC definitions plus the messages produced while loading them (spec §6, §14).</summary>
    public class DefinitionCatalog
    {
        /// <summary>Unique ANPCs by id.</summary>
        public readonly Dictionary<string, NpcDefinition> ById = new Dictionary<string, NpcDefinition>(StringComparer.Ordinal);
        /// <summary>Generic templates, in folder order.</summary>
        public readonly List<NpcDefinition> Generics = new List<NpcDefinition>();
        public readonly List<string> Messages = new List<string>();

        public int Count
        {
            get { return ById.Count + Generics.Count; }
        }

        public static DefinitionCatalog Build(IEnumerable<AnpcFolder> folders)
        {
            DefinitionCatalog catalog = new DefinitionCatalog();
            List<AnpcFolder> ordered = new List<AnpcFolder>(folders);
            ordered.Sort(delegate (AnpcFolder a, AnpcFolder b) { return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase); });

            foreach (AnpcFolder folder in ordered)
            {
                if (folder.Name != null && folder.Name.StartsWith("_", StringComparison.Ordinal))
                    continue; // shared data such as _Portraits
                if (folder.NpcJson == null)
                {
                    catalog.Messages.Add(folder.Name + "/npc.json: file: missing, folder skipped");
                    continue;
                }

                ParseResult result = DefinitionParser.ParseFolder(folder.Name, folder.NpcJson);
                catalog.Messages.AddRange(result.Warnings);
                if (!result.Ok)
                {
                    catalog.Messages.Add(result.Error);
                    continue;
                }

                NpcDefinition d = result.Definition;
                if (d.Kind == NpcKind.Generic)
                {
                    if (folder.DialogueJson != null)
                        catalog.Messages.Add(folder.Name + "/dialogue.json: file: topics are for unique ANPCs only, ignored");
                    catalog.Generics.Add(d);
                }
                else if (catalog.ById.ContainsKey(d.Id))
                {
                    catalog.Messages.Add(d.SourceFile + ": folder: duplicate id \"" + d.Id + "\", skipped");
                }
                else
                {
                    catalog.ById.Add(d.Id, d);
                }
            }

            catalog.Messages.Add("Loaded " + catalog.ById.Count + " unique ANPC(s) and " + catalog.Generics.Count + " generic template(s).");
            return catalog;
        }

        /// <summary>Unique ANPCs that live in this town.</summary>
        public List<NpcDefinition> ForLocation(string region, string place)
        {
            List<NpcDefinition> found = new List<NpcDefinition>();
            if (region == null || place == null)
                return found;
            foreach (NpcDefinition d in ById.Values)
            {
                if (SamePlaceName(d.Region, region) && SamePlaceName(d.Place, place))
                    found.Add(d);
            }
            return found;
        }

        /// <summary>Every portrait name any definition refers to, distinct and sorted.</summary>
        public List<string> ReferencedPortraits()
        {
            List<string> names = new List<string>();
            List<NpcDefinition> all = new List<NpcDefinition>(ById.Values);
            all.AddRange(Generics);
            foreach (NpcDefinition d in all)
            {
                foreach (string p in d.Portraits)
                {
                    if (!names.Contains(p))
                        names.Add(p);
                }
            }
            names.Sort(StringComparer.Ordinal);
            return names;
        }

        /// <summary>Region and place names match ignoring case and surrounding spaces.</summary>
        public static bool SamePlaceName(string a, string b)
        {
            return a != null && b != null && string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `"Assets/Game/Mods/AdvancedNPCs/Tools~/run-tests.sh"`
Expected: `result="Passed"`, `failed="0"`.

- [ ] **Step 5: Add the runtime file access**

Create `Assets/Game/Mods/AdvancedNPCs/Scripts/Runtime/AnpcFiles.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>All file access for ANPC data under StreamingAssets/ANPCs (spec §6).</summary>
    public static class AnpcFiles
    {
        public const string RootName = "ANPCs";
        public const string PortraitsName = "_Portraits";
        public const string NpcFile = "npc.json";
        public const string DialogueFile = "dialogue.json";

        public static string Root
        {
            get { return Path.Combine(Application.streamingAssetsPath, RootName); }
        }

        public static string PortraitsFolder
        {
            get { return Path.Combine(Root, PortraitsName); }
        }

        public static string NpcJsonPath(string folder)
        {
            return Path.Combine(Path.Combine(Root, folder), NpcFile);
        }

        /// <summary>Every ANPC folder (shared "_" folders excluded) with its npc.json and dialogue.json text.</summary>
        public static List<AnpcFolder> ReadFolders()
        {
            List<AnpcFolder> result = new List<AnpcFolder>();
            if (!Directory.Exists(Root))
            {
                AdvancedNpcsMod.Log("No " + RootName + " folder at " + Root + "; nothing to spawn.");
                return result;
            }
            foreach (string dir in Directory.GetDirectories(Root))
            {
                string name = Path.GetFileName(dir);
                if (name.StartsWith("_", StringComparison.Ordinal))
                    continue;
                result.Add(new AnpcFolder(name, ReadOptional(dir, NpcFile, name), ReadOptional(dir, DialogueFile, name)));
            }
            return result;
        }

        /// <summary>Rewrites location and position in a unique ANPC's npc.json (anpc_place).</summary>
        public static void SetPlacement(NpcDefinition def, string region, string place, float x, float y, float z)
        {
            string path = NpcJsonPath(def.Folder);
            File.WriteAllText(path, DefinitionEditor.SetPlacement(File.ReadAllText(path), region, place, x, y, z));
        }

        static string ReadOptional(string dir, string file, string folder)
        {
            string path = Path.Combine(dir, file);
            if (!File.Exists(path))
                return null;
            try
            {
                return File.ReadAllText(path);
            }
            catch (Exception e)
            {
                AdvancedNpcsMod.LogError(folder + "/" + file + ": file: could not read (" + e.Message + ")");
                return null;
            }
        }
    }
}
```

- [ ] **Step 6: Rewire `AdvancedNpcsMod`**

In `Assets/Game/Mods/AdvancedNPCs/Scripts/Runtime/AdvancedNpcsMod.cs`:

1. Delete the line `public const string FolderName = "AdvancedNPCs";`.
2. In `Awake`, replace
   ```csharp
   string autorun = Path.Combine(Path.Combine(Application.streamingAssetsPath, FolderName), AutoRunFile);
   ```
   with
   ```csharp
   string autorun = Path.Combine(AnpcFiles.Root, AutoRunFile);
   ```
3. Replace the whole `LoadCatalog` method with:
   ```csharp
   static DefinitionCatalog LoadCatalog()
   {
       DefinitionCatalog catalog = DefinitionCatalog.Build(AnpcFiles.ReadFolders());
       foreach (string message in catalog.Messages)
           Log(message);
       return catalog;
   }
   ```
4. In `PlaceCommand`, replace
   ```csharp
   NpcDefinition def;
   if (!Instance.Catalog.ById.TryGetValue(args[0], out def))
       return "No NPC definition with id \"" + args[0] + "\".";
   ```
   with
   ```csharp
   NpcDefinition def;
   if (!Instance.Catalog.ById.TryGetValue(args[0], out def))
   {
       if (IsGeneric(args[0]))
           return "\"" + args[0] + "\" is a generic ANPC; generic ANPCs are placed by the spawn rules in their template's npc.json.";
       return "No unique ANPC with id \"" + args[0] + "\".";
   }
   ```
   and replace
   ```csharp
   string path = Path.Combine(Path.Combine(Application.streamingAssetsPath, FolderName), def.SourceFile);
   try
   {
       string edited = DefinitionEditor.SetPlacement(File.ReadAllText(path), region, place, local.x, local.y, local.z);
       File.WriteAllText(path, edited);
   }
   ```
   with
   ```csharp
   try
   {
       AnpcFiles.SetPlacement(def, region, place, local.x, local.y, local.z);
   }
   ```
5. Add this method below `PlaceCommand`:
   ```csharp
   /// <summary>True for a generic template id or a generic instance key ("commoner@1234#0").</summary>
   static bool IsGeneric(string idOrKey)
   {
       int at = idOrKey.IndexOf('@');
       string template = at >= 0 ? idOrKey.Substring(0, at) : idOrKey;
       foreach (NpcDefinition g in Instance.Catalog.Generics)
       {
           if (g.Id == template)
               return true;
       }
       return false;
   }
   ```

- [ ] **Step 7: Move the examples into the folder layout**

```bash
cd Assets/Game/Mods/AdvancedNPCs/Examples
for id in bram brave_bors coward_cora; do
  mkdir -p "ANPCs/$id"
  git mv "$id.json" "ANPCs/$id/npc.json"
  git mv "$id.json.meta" "ANPCs/$id/npc.json.meta"
  sed -i '/^[[:space:]]*"id":/d' "ANPCs/$id/npc.json"
done
mkdir -p ANPCs/_Portraits
cd - > /dev/null
grep -L '"id"' Assets/Game/Mods/AdvancedNPCs/Examples/ANPCs/*/npc.json
```
Expected: the last command prints all three `npc.json` paths (none contains `"id"`). `ANPCs/bram/npc.json` now reads:

```json
{
  "name": "Bram the Cooper",
  "location": { "region": "Daggerfall", "place": "Daggerfall" },
  "position": [73, 0.96, 645.89],
  "baseClass": "Spellsword",
  "gender": "Male",
  "bravery": "Normal",
  "fleeHealthPercent": 25,
  "calmDownHours": [6, 48],
  "crimeOnAttack": true,
  "wanderRadius": 8
}
```

Create `Assets/Game/Mods/AdvancedNPCs/Examples/ANPCs/_Portraits/README.txt`:

```text
Shared ANPC portraits. Put PNG files here and refer to them by name (without .png) from npc.json:
  "portrait": "bram"          -> _Portraits/bram.png
Recommended size: 64 x 64 pixels (the talk window's portrait size); other sizes are scaled.
Names are not case sensitive. A missing portrait falls back to a vanilla face.
```

- [ ] **Step 8: Update the tool scripts**

In `Assets/Game/Mods/AdvancedNPCs/Tools~/build-mod.sh`, replace the block

```bash
if [ "$EXAMPLES" = 1 ]; then
  DEFS="$DFU/DaggerfallUnity_Data/StreamingAssets/AdvancedNPCs"
  mkdir -p "$DEFS"
  cp "$HERE/../Examples/"*.json "$DEFS/"
  echo "Copied example NPCs into $DEFS"
fi
```

with

```bash
if [ "$EXAMPLES" = 1 ]; then
  DEFS="$DFU/DaggerfallUnity_Data/StreamingAssets/ANPCs"
  mkdir -p "$DEFS"
  (cd "$HERE/../Examples/ANPCs" && find . -name '*.meta' -prune -o -type f -print | while read -r f; do
    mkdir -p "$DEFS/$(dirname "$f")"
    cp "$f" "$DEFS/$f"
  done)
  echo "Copied example ANPCs into $DEFS"
fi
```

and in the header comment replace `NPC definitions` with `ANPC folders`.

In `Assets/Game/Mods/AdvancedNPCs/Tools~/selftest.sh`, replace

```bash
FLAG="$DFU/DaggerfallUnity_Data/StreamingAssets/AdvancedNPCs/selftest-autorun.txt"
```

with

```bash
FLAG="$DFU/DaggerfallUnity_Data/StreamingAssets/ANPCs/selftest-autorun.txt"
```

and in the header comment replace `StreamingAssets/AdvancedNPCs` with `StreamingAssets/ANPCs`.

- [ ] **Step 9: Manifest and compile checks**

In `AdvancedNPCs.dfmod.json`, add before the line `"Assets/Game/Mods/AdvancedNPCs/Scripts/Runtime/AdvancedNpcsMod.cs",`:

```json
        "Assets/Game/Mods/AdvancedNPCs/Scripts/Runtime/AnpcFiles.cs",
```

and change `ModDescription` to:

```json
    "ModDescription": "Persistent, data-defined NPCs that stay calm, fight or flee by bravery when attacked, report crimes, and calm down over in-game time. One folder per NPC in StreamingAssets/ANPCs.",
```

Run: `"Assets/Game/Mods/AdvancedNPCs/Tools~/compile-check.sh"` — Expected: `compile-check: OK (…)`.
Run: `"Assets/Game/Mods/AdvancedNPCs/Tools~/runtime-check.sh"` — Expected: exit 0, no errors.
Run the manifest check from Global Constraints — Expected: no output.

- [ ] **Step 10: Commit**

```bash
git add -A Assets/Game/Mods/AdvancedNPCs/Scripts Assets/Game/Mods/AdvancedNPCs/Editor/Tests/DefinitionCatalogTests.cs Assets/Game/Mods/AdvancedNPCs/Examples Assets/Game/Mods/AdvancedNPCs/Tools~ Assets/Game/Mods/AdvancedNPCs/AdvancedNPCs.dfmod.json
git commit -m "feat: load ANPCs from one folder per NPC under StreamingAssets/ANPCs" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Migrate 1a definition files

**Files:**
- Create: `Assets/Game/Mods/AdvancedNPCs/Scripts/Core/Migration.cs`
- Modify: `Assets/Game/Mods/AdvancedNPCs/Scripts/Runtime/AnpcFiles.cs` (add `LegacyName`, `MigrateLegacy`)
- Modify: `Assets/Game/Mods/AdvancedNPCs/Scripts/Runtime/AdvancedNpcsMod.cs` (`Awake`)
- Modify: `Assets/Game/Mods/AdvancedNPCs/README.md` (whole file)
- Modify: `Assets/Game/Mods/AdvancedNPCs/AdvancedNPCs.dfmod.json`
- Test: `Assets/Game/Mods/AdvancedNPCs/Editor/Tests/MigrationTests.cs`

**Interfaces:**
- Consumes: `DefinitionParser.Parse` (legacy), `DefinitionParser.ParseFolder` (Task 1); `AnpcFiles.Root`, `AnpcFiles.NpcFile` (Task 2).
- Produces: `class MigrationResult { string Id; string NpcJson; string Error; bool Ok }`; `Migration.Convert(string fileName, string json) -> MigrationResult`; `Migration.RemoveId(string json) -> string`; `AnpcFiles.LegacyName = "AdvancedNPCs"`; `AnpcFiles.MigrateLegacy()`.

- [ ] **Step 1: Write the failing tests**

`Assets/Game/Mods/AdvancedNPCs/Editor/Tests/MigrationTests.cs`:

```csharp
using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class MigrationTests
    {
        const string Pretty =
            "{\n  \"id\": \"bram\",\n  \"name\": \"Bram\",\n  \"location\": { \"region\": \"R\", \"place\": \"P\" },\n  \"position\": [1, 2, 3]\n}\n";
        const string PrettyWithoutId =
            "{\n  \"name\": \"Bram\",\n  \"location\": { \"region\": \"R\", \"place\": \"P\" },\n  \"position\": [1, 2, 3]\n}\n";

        [Test]
        public void PrettyFile_RemovesIdLineOnly()
        {
            MigrationResult m = Migration.Convert("bram.json", Pretty);
            Assert.IsTrue(m.Ok, m.Error);
            Assert.AreEqual("bram", m.Id);
            Assert.AreEqual(PrettyWithoutId, m.NpcJson);
        }

        [Test]
        public void CrlfFile_KeepsLineEndings()
        {
            MigrationResult m = Migration.Convert("bram.json", Pretty.Replace("\n", "\r\n"));
            Assert.IsTrue(m.Ok, m.Error);
            Assert.AreEqual(PrettyWithoutId.Replace("\n", "\r\n"), m.NpcJson);
        }

        [Test]
        public void IdLast_RemovesPrecedingComma()
        {
            string json = "{\n  \"name\": \"Bram\",\n  \"location\": { \"region\": \"R\", \"place\": \"P\" },\n  \"position\": [1, 2, 3],\n  \"id\": \"bram\"\n}";
            MigrationResult m = Migration.Convert("bram.json", json);
            Assert.IsTrue(m.Ok, m.Error);
            Assert.AreEqual("{\n  \"name\": \"Bram\",\n  \"location\": { \"region\": \"R\", \"place\": \"P\" },\n  \"position\": [1, 2, 3]\n}", m.NpcJson);
        }

        [Test]
        public void OneLineFile_RemovesIdInline()
        {
            MigrationResult m = Migration.Convert("bram.json",
                "{ \"id\": \"bram\", \"name\": \"Bram\", \"location\": { \"region\": \"R\", \"place\": \"P\" }, \"position\": [0,0,0] }");
            Assert.IsTrue(m.Ok, m.Error);
            Assert.AreEqual("{ \"name\": \"Bram\", \"location\": { \"region\": \"R\", \"place\": \"P\" }, \"position\": [0,0,0] }", m.NpcJson);
        }

        [Test]
        public void InvalidFile_IsNotMigrated()
        {
            MigrationResult m = Migration.Convert("bad.json", "{ \"id\": \"bad\" }");
            Assert.IsFalse(m.Ok);
            Assert.AreEqual("bad.json: name: required", m.Error);
        }

        [Test]
        public void Result_ParsesAsFolderWithoutWarnings()
        {
            MigrationResult m = Migration.Convert("bram.json", Pretty);
            ParseResult r = DefinitionParser.ParseFolder(m.Id, m.NpcJson);
            Assert.IsTrue(r.Ok, r.Error);
            Assert.AreEqual(0, r.Warnings.Count);
            Assert.AreEqual("bram", r.Definition.Id);
            Assert.AreEqual(3f, r.Definition.Z);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `"Assets/Game/Mods/AdvancedNPCs/Tools~/run-tests.sh"`
Expected: `NO RESULTS FILE (compile error?)` with `error CS0246: The type or namespace name 'MigrationResult' could not be found`.

- [ ] **Step 3: Implement `Migration`**

`Assets/Game/Mods/AdvancedNPCs/Scripts/Core/Migration.cs`:

```csharp
using System;
using System.Text.RegularExpressions;

namespace AdvancedNPCs.Core
{
    public class MigrationResult
    {
        public string Id;
        public string NpcJson;
        public string Error;

        public bool Ok
        {
            get { return Error == null; }
        }
    }

    /// <summary>
    /// Converts a 1a definition file into the text of ANPCs/&lt;id&gt;/npc.json: the same text without the "id"
    /// member, so the author's other fields, order and formatting survive (spec §11).
    /// </summary>
    public static class Migration
    {
        // Ids only contain [a-z0-9_], so the value has no escapes.
        static readonly Regex IdMember = new Regex("\"id\"\\s*:\\s*\"[^\"]*\"");

        public static MigrationResult Convert(string fileName, string json)
        {
            MigrationResult m = new MigrationResult();
            ParseResult old = DefinitionParser.Parse(fileName, json);
            if (!old.Ok)
            {
                m.Error = old.Error;
                return m;
            }

            string body = RemoveId(json);
            ParseResult check = DefinitionParser.ParseFolder(old.Definition.Id, body);
            if (!check.Ok)
            {
                m.Error = fileName + ": could not convert (" + check.Error + ")";
                return m;
            }
            m.Id = old.Definition.Id;
            m.NpcJson = body;
            return m;
        }

        public static string RemoveId(string json)
        {
            Match match = IdMember.Match(json);
            if (!match.Success)
                return json;

            int start = match.Index;
            int end = match.Index + match.Length;
            int after = end;
            while (after < json.Length && (json[after] == ' ' || json[after] == '\t'))
                after++;
            if (after < json.Length && json[after] == ',')
            {
                // "id": "x", other... -> remove the member, its comma and the spaces after it.
                end = after + 1;
                while (end < json.Length && (json[end] == ' ' || json[end] == '\t'))
                    end++;
            }
            else
            {
                // Last member: remove the comma that precedes it instead.
                int before = start - 1;
                while (before >= 0 && char.IsWhiteSpace(json[before]))
                    before--;
                if (before >= 0 && json[before] == ',')
                    start = before;
            }

            string result = json.Substring(0, start) + json.Substring(end);

            // If the member had its own line, that line is now blank: drop it (keeps \r\n or \n endings intact).
            int lineEnd = result.IndexOf('\n', start);
            if (lineEnd >= 0)
            {
                int lineStart = start > 0 ? result.LastIndexOf('\n', start - 1) + 1 : 0;
                if (result.Substring(lineStart, lineEnd - lineStart).Trim().Length == 0)
                    result = result.Remove(lineStart, lineEnd - lineStart + 1);
            }
            return result;
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `"Assets/Game/Mods/AdvancedNPCs/Tools~/run-tests.sh"`
Expected: `result="Passed"`, `failed="0"`.

- [ ] **Step 5: Run the migration at startup**

In `Assets/Game/Mods/AdvancedNPCs/Scripts/Runtime/AnpcFiles.cs`, add below `public const string DialogueFile = "dialogue.json";`:

```csharp
        /// <summary>The 1a definition folder, read only to migrate it.</summary>
        public const string LegacyName = "AdvancedNPCs";
```

and add this method below `ReadFolders`:

```csharp
        /// <summary>
        /// Moves 1a files StreamingAssets/AdvancedNPCs/*.json into ANPCs/&lt;id&gt;/npc.json (spec §11). Never
        /// overwrites; a migrated file is renamed to *.json.migrated, never deleted.
        /// </summary>
        public static void MigrateLegacy()
        {
            string legacy = Path.Combine(Application.streamingAssetsPath, LegacyName);
            if (!Directory.Exists(legacy))
                return;
            string[] files = Directory.GetFiles(legacy, "*.json");
            if (files.Length == 0)
                return;
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            int migrated = 0;
            foreach (string path in files)
            {
                string name = Path.GetFileName(path);
                try
                {
                    MigrationResult m = Migration.Convert(name, File.ReadAllText(path));
                    if (!m.Ok)
                    {
                        AdvancedNpcsMod.LogError(LegacyName + "/" + m.Error + " (not migrated)");
                        continue;
                    }
                    string target = Path.Combine(Root, m.Id);
                    if (Directory.Exists(target))
                    {
                        AdvancedNpcsMod.Log(LegacyName + "/" + name + ": not migrated, " + RootName + "/" + m.Id + " already exists");
                        continue;
                    }
                    Directory.CreateDirectory(target);
                    File.WriteAllText(Path.Combine(target, NpcFile), m.NpcJson);
                    File.Move(path, path + ".migrated");
                    migrated++;
                    AdvancedNpcsMod.Log(LegacyName + "/" + name + " -> " + RootName + "/" + m.Id + "/" + NpcFile);
                }
                catch (Exception e)
                {
                    AdvancedNpcsMod.LogError(LegacyName + "/" + name + ": migration failed (" + e.Message + ")");
                }
            }
            AdvancedNpcsMod.Log("Migrated " + migrated + " definition(s) to " + RootName + "/.");
        }
```

In `AdvancedNpcsMod.Awake`, replace `Catalog = LoadCatalog();` with:

```csharp
            AnpcFiles.MigrateLegacy();
            Catalog = LoadCatalog();
```

In `AdvancedNPCs.dfmod.json`, add after `"Assets/Game/Mods/AdvancedNPCs/Scripts/Core/Json.cs",`:

```json
        "Assets/Game/Mods/AdvancedNPCs/Scripts/Core/Migration.cs",
```

- [ ] **Step 6: Rewrite the README for the folder layout**

Replace `Assets/Game/Mods/AdvancedNPCs/README.md` with:

````markdown
# Advanced NPCs

Persistent, data-defined NPCs ("ANPCs") for Daggerfall Unity 1.9.2. Each ANPC is a folder with an
`npc.json`. ANPCs stay calm, only fight whoever attacks them, fight or flee by bravery, report crimes,
calm down over in-game hours, and keep their state (alive/dead, hostile/calm, health) through visits
and save/load.

## Folder layout

```
DaggerfallUnity_Data/StreamingAssets/ANPCs/
  _Portraits/        shared portrait PNGs
  bram/              one folder per ANPC; the folder name is its id
    npc.json
```

- Folder names use lowercase letters, digits and `_`. Never rename a folder after release: the name keys save data.
- Folders starting with `_` hold shared data and are not ANPCs.
- Unknown fields in `npc.json` are reported in `Player.log` and ignored (catches typos).

## Making a unique ANPC

1. Install the mod and enable **Advanced NPCs** in the DFU mod list.
2. In game, stand outdoors where the ANPC should live, open the console (`~`) and type `anpc_pos`.
3. Create `StreamingAssets/ANPCs/my_npc/npc.json` with a `name` and the two lines `anpc_pos` printed:

```json
{
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
| `kind` | no | `unique` | `unique` (one fixed person) |
| `name` | yes | — | any text |
| `location` | yes | — | from `anpc_pos` |
| `position` | yes | — | from `anpc_pos` (relative to the town's origin corner) |
| `race` | no | the region's people | `Breton`, `Redguard`, `Nord` |
| `baseClass` | no | `Spellsword` | Mage, Spellsword, Battlemage, Sorcerer, Healer, Nightblade, Bard, Burglar, Rogue, Acrobat, Thief, Assassin, Monk, Archer, Ranger, Barbarian, Warrior, Knight |
| `gender` | no | fixed random per ANPC | `Male`, `Female` |
| `bravery` | no | `Normal` | `Coward` (always flees), `Normal` (flees at low health), `Brave` (fights to the death) |
| `fleeHealthPercent` | no | `25` | 1–99, used by `Normal` |
| `calmDownHours` | no | `[6, 48]` | `[min, max]` in-game hours before a hostile ANPC forgives the player |
| `crimeOnAttack` | no | `true` | `true`: attacking is assault, killing is murder |
| `wanderRadius` | no | `8` | metres around the spawn point; `0` stands still |

## Upgrading from 0.1

On the first start, every valid `StreamingAssets/AdvancedNPCs/<name>.json` is copied to
`StreamingAssets/ANPCs/<id>/npc.json` (without its `id` line) and the old file is renamed to
`<name>.json.migrated`. Existing folders are never overwritten. Saves keep each NPC's state because the
folder name is the old id.

## Console commands

- `anpc_pos` — your position as definition JSON (also written to `Player.log`).
- `anpc_list` — spawned ANPCs with distance, direction, height and state.
- `anpc_place <id>` — makes your current spot that unique ANPC's home: rewrites `location` and `position` in its `npc.json` (nothing else) and moves it here.
- `anpc_summon <id>` — moves a spawned ANPC in front of you (testing only; not saved).
- `anpc_selftest` — runs the behaviour checks with temporary ANPCs next to you (god mode on, crimes recorded instead of punished, game clock untouched); results on screen and in `Player.log`.

## Behaviour

- Calm ANPCs never stop you resting or travelling.
- They only fight whoever attacked them; hitting one ANPC does not anger the others.
- Killed ANPCs stay dead in that save. A new game brings them back.

## Examples

`Examples/ANPCs/` holds three Daggerfall city ANPCs: Bram (Normal), Cora (Coward) and Bors (Brave). They
stand next to each other at Daggerfall's north-west gate; use `anpc_list` or `anpc_summon` if they are not
in view.

## Building (developers)

Sources live in `Scripts/Core` (pure logic, unit-tested) and `Scripts/Runtime` (DFU glue).
`Tools~` (ignored by Unity) holds:

- `run-tests.sh` — runs the Core NUnit tests in a small Unity host project.
- `compile-check.sh` — compiles every mod source against a DFU install's own assemblies.
- `runtime-check.sh` — compiles the sources with DFU's own in-memory runtime compiler and loads every type, catching problems only the game's compiler has (e.g. nested enums cause TypeLoadException there).
- `build-mod.sh` — builds `advancednpcs.dfmod` with Unity 2019.4.41f2 (a windowed editor that closes itself) and installs it, plus `Examples/ANPCs`, into a DFU install. In the editor the same build is under **Daggerfall Tools → Build Advanced NPCs mod**.
- `selftest.sh [character] [save]` — fully unattended in-game test: starts DFU, loads a save standing outdoors in a town, runs `anpc_selftest`, quits and prints the results. Temporarily skips the startup options screen (settings.ini is restored).

When adding a `.cs` file under `Scripts/`, also add it to `Files` in `AdvancedNPCs.dfmod.json`.
````

- [ ] **Step 7: Compile checks**

Run: `"Assets/Game/Mods/AdvancedNPCs/Tools~/compile-check.sh"` — Expected: `compile-check: OK (…)`.
Run: `"Assets/Game/Mods/AdvancedNPCs/Tools~/runtime-check.sh"` — Expected: exit 0.
Run the manifest check — Expected: no output.

- [ ] **Step 8: Prepare a 1a install to migrate**

```bash
LEGACY="/f/_Projects/Dagerfall/DFU_testing/DaggerfallUnity_Data/StreamingAssets/AdvancedNPCs"
ANPCS="/f/_Projects/Dagerfall/DFU_testing/DaggerfallUnity_Data/StreamingAssets/ANPCs"
ls "$LEGACY" "$ANPCS" 2>&1
```

Expected: `$LEGACY` lists `bram.json`, `brave_bors.json`, `coward_cora.json` (installed by 1a), and `$ANPCS` does not exist. If `$LEGACY` has no `.json` files, restore them from the 1a branch:

```bash
mkdir -p "$LEGACY"
for id in bram brave_bors coward_cora; do git show "feature/advanced-npcs-1a:Assets/Game/Mods/AdvancedNPCs/Examples/$id.json" > "$LEGACY/$id.json"; done
```

If `$ANPCS` exists from an earlier attempt, move it aside first: `mv "$ANPCS" "$ANPCS.bak"` (do not delete it).

- [ ] **Step 9: Build without examples and run the self-test (migration happens on this start)**

Close the Unity editor and the game, then:

```bash
"Assets/Game/Mods/AdvancedNPCs/Tools~/build-mod.sh" --no-examples
git restore Assets/AddressableAssetsData/
"Assets/Game/Mods/AdvancedNPCs/Tools~/selftest.sh"
grep -E "\[AdvancedNPCs\] (AdvancedNPCs/|Migrated|Loaded|bram: spawned)" "$USERPROFILE/AppData/LocalLow/Daggerfall Workshop/Daggerfall Unity/Player.log"
ls "$LEGACY" "$ANPCS"
```

Expected:
- self-test ends with `SELFTEST DONE 18/18 passed`;
- log lines `AdvancedNPCs/bram.json -> ANPCs/bram/npc.json` (and the two others), `Migrated 3 definition(s) to ANPCs/.`, `Loaded 3 unique ANPC(s) and 0 generic template(s).`;
- `$LEGACY` now holds `*.json.migrated`, `$ANPCS` holds `bram/`, `brave_bors/`, `coward_cora/`.

Then run `build-mod.sh` once more **with** examples (copies `Examples/ANPCs` incl. `_Portraits/README.txt`), `git restore Assets/AddressableAssetsData/`, and run `selftest.sh` again. Expected: `SELFTEST DONE 18/18 passed`, no new `Migrated` line (nothing left to migrate).

- [ ] **Step 10: Commit (including the `.meta` files Unity created)**

```bash
git status --short Assets/Game/Mods/AdvancedNPCs
git add -A Assets/Game/Mods/AdvancedNPCs
git commit -m "feat: migrate 1a definition files into ANPC folders on startup" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

Expected `git status` before adding: the task's files plus new `.meta` files (e.g. `Scripts/Core/FieldReader.cs.meta`, `Examples/ANPCs.meta`, `Examples/ANPCs/bram.meta`). Nothing outside `Assets/Game/Mods/AdvancedNPCs`.

---

### Task 4: Stable seeding, vanilla faces and `NpcInstance`

**Files:**
- Create: `Assets/Game/Mods/AdvancedNPCs/Scripts/Core/StableHash.cs`, `SeededRandom.cs`, `VanillaFaces.cs`, `NpcInstance.cs` (all in `Scripts/Core/`)
- Modify: `Assets/Game/Mods/AdvancedNPCs/AdvancedNPCs.dfmod.json`
- Test: `Assets/Game/Mods/AdvancedNPCs/Editor/Tests/SeedingTests.cs`, `Assets/Game/Mods/AdvancedNPCs/Editor/Tests/NpcInstanceTests.cs`

**Interfaces:**
- Consumes: `NpcDefinition` (Task 1).
- Produces:
  - `StableHash.Of(string) -> uint` (FNV-1a 32 over UTF-8; null hashes like "").
  - `class SeededRandom { SeededRandom(uint seed); uint NextUInt(); int Next(int max); int Range(int min, int max); T Pick<T>(IList<T> list) }` — xorshift32; seed 0 is replaced by `0x9E3779B9`; `Next(max <= 0) == 0`; `Range` inclusive, `Range(min, max <= min) == min`.
  - `VanillaFaces.OutfitVariants = 4`, `VanillaFaces.FaceVariants = 24`, `VanillaFaces.FaceRecord(string race, string gender, int outfit, int face) -> int`.
  - `class NpcInstance { string Key; NpcDefinition Definition; string Name; string Gender ("Male"/"Female"); string Race; string PortraitName (null = vanilla face); int FaceOutfit; int FaceVariant; uint Seed; bool HasFixedPosition; float X, Y, Z; int CellIndex = -1; bool Persistent = true; int FaceRecord(); static NpcInstance ForUnique(NpcDefinition def, string defaultRace); static string PickGender(string fixedGender, SeededRandom rng) }`.

- [ ] **Step 1: Write the failing tests**

`Assets/Game/Mods/AdvancedNPCs/Editor/Tests/SeedingTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class SeedingTests
    {
        // FNV-1a 32 reference vectors; these values must never change (they key generic people in saves).
        [TestCase("", 2166136261u)]
        [TestCase("a", 3826002220u)]
        [TestCase("foobar", 3214735720u)]
        [TestCase("bram", 1802036227u)]
        [TestCase("commoner@1234#2", 3591463060u)]
        public void StableHash_KnownVectors(string text, uint expected)
        {
            Assert.AreEqual(expected, StableHash.Of(text));
        }

        [Test]
        public void StableHash_Null_HashesLikeEmpty()
        {
            Assert.AreEqual(StableHash.Of(""), StableHash.Of(null));
        }

        [Test]
        public void SeededRandom_Seed1_MatchesXorshift32()
        {
            SeededRandom r = new SeededRandom(1);
            Assert.AreEqual(270369u, r.NextUInt());
            Assert.AreEqual(67634689u, r.NextUInt());
            Assert.AreEqual(2647435461u, r.NextUInt());
        }

        [Test]
        public void SeededRandom_SeedZero_UsesFixedNonZeroSeed()
        {
            Assert.AreEqual(1359758873u, new SeededRandom(0).NextUInt());
        }

        [Test]
        public void SeededRandom_SameSeed_SameSequence()
        {
            SeededRandom a = new SeededRandom(77);
            SeededRandom b = new SeededRandom(77);
            for (int i = 0; i < 20; i++)
                Assert.AreEqual(a.NextUInt(), b.NextUInt());
        }

        [Test]
        public void SeededRandom_Next_StaysInRange()
        {
            SeededRandom r = new SeededRandom(42);
            for (int i = 0; i < 1000; i++)
            {
                int v = r.Next(7);
                Assert.IsTrue(v >= 0 && v < 7, "got " + v);
            }
            Assert.AreEqual(0, r.Next(0));
            Assert.AreEqual(0, r.Next(-3));
        }

        [Test]
        public void SeededRandom_Range_IsInclusive()
        {
            SeededRandom r = new SeededRandom(5);
            HashSet<int> seen = new HashSet<int>();
            for (int i = 0; i < 300; i++)
            {
                int v = r.Range(1, 3);
                Assert.IsTrue(v >= 1 && v <= 3, "got " + v);
                seen.Add(v);
            }
            CollectionAssert.AreEquivalent(new[] { 1, 2, 3 }, seen);
            Assert.AreEqual(5, r.Range(5, 5));
            Assert.AreEqual(5, r.Range(5, 2));
        }

        [Test]
        public void SeededRandom_Pick_UsesNext()
        {
            // First draw for seed 1 is 270369, and 270369 % 3 == 0.
            Assert.AreEqual("a", new SeededRandom(1).Pick(new[] { "a", "b", "c" }));
        }

        [TestCase("Breton", "Male", 0, 0, 192)]
        [TestCase("Breton", "Female", 2, 3, 27)]
        [TestCase("Redguard", "Male", 1, 23, 335)]
        [TestCase("Nord", "Female", 2, 5, 53)]
        [TestCase("Khajiit", "Male", 0, 0, 192)]
        [TestCase("Nord", "Male", 9, 99, 215)]
        public void VanillaFaces_FaceRecord(string race, string gender, int outfit, int face, int expected)
        {
            Assert.AreEqual(expected, VanillaFaces.FaceRecord(race, gender, outfit, face));
        }
    }
}
```

`Assets/Game/Mods/AdvancedNPCs/Editor/Tests/NpcInstanceTests.cs`:

```csharp
using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class NpcInstanceTests
    {
        static NpcDefinition Def(string id, string gender, string race, string portrait)
        {
            NpcDefinition d = new NpcDefinition();
            d.Id = id;
            d.Folder = id;
            d.Name = "Name of " + id;
            d.Gender = gender;
            d.Race = race;
            if (portrait != null)
                d.Portraits.Add(portrait);
            d.X = 1f;
            d.Y = 2f;
            d.Z = 3f;
            return d;
        }

        [Test]
        public void ForUnique_CopiesIdentityAndPosition()
        {
            NpcInstance i = NpcInstance.ForUnique(Def("bram", "Male", "", null), "Breton");
            Assert.AreEqual("bram", i.Key);
            Assert.AreEqual("Name of bram", i.Name);
            Assert.AreEqual(StableHash.Of("bram"), i.Seed);
            Assert.IsTrue(i.HasFixedPosition);
            Assert.AreEqual(1f, i.X);
            Assert.AreEqual(2f, i.Y);
            Assert.AreEqual(3f, i.Z);
            Assert.AreEqual(-1, i.CellIndex);
            Assert.IsTrue(i.Persistent);
            Assert.IsNull(i.PortraitName);
        }

        [Test]
        public void ForUnique_KeepsFixedGender()
        {
            Assert.AreEqual("Female", NpcInstance.ForUnique(Def("a", "Female", "", null), "Breton").Gender);
        }

        [Test]
        public void ForUnique_UnsetGender_IsStablePerId()
        {
            string first = NpcInstance.ForUnique(Def("cora", "", "", null), "Breton").Gender;
            string again = NpcInstance.ForUnique(Def("cora", "", "", null), "Breton").Gender;
            Assert.AreEqual(first, again);
            Assert.IsTrue(first == "Male" || first == "Female", first);
        }

        [Test]
        public void ForUnique_RaceFallsBackToDefault()
        {
            Assert.AreEqual("Redguard", NpcInstance.ForUnique(Def("a", "Male", "", null), "Redguard").Race);
            Assert.AreEqual("Nord", NpcInstance.ForUnique(Def("a", "Male", "Nord", null), "Redguard").Race);
        }

        [Test]
        public void ForUnique_UsesFirstPortrait()
        {
            Assert.AreEqual("bram", NpcInstance.ForUnique(Def("a", "Male", "", "bram"), "Breton").PortraitName);
        }

        [Test]
        public void ForUnique_FaceIsStableAndValid()
        {
            NpcInstance a = NpcInstance.ForUnique(Def("bors", "Male", "Nord", null), "Breton");
            NpcInstance b = NpcInstance.ForUnique(Def("bors", "Male", "Nord", null), "Breton");
            Assert.IsTrue(a.FaceOutfit >= 0 && a.FaceOutfit < VanillaFaces.OutfitVariants);
            Assert.IsTrue(a.FaceVariant >= 0 && a.FaceVariant < VanillaFaces.FaceVariants);
            Assert.AreEqual(a.FaceOutfit, b.FaceOutfit);
            Assert.AreEqual(a.FaceVariant, b.FaceVariant);
            Assert.AreEqual(VanillaFaces.FaceRecord("Nord", "Male", a.FaceOutfit, a.FaceVariant), a.FaceRecord());
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `"Assets/Game/Mods/AdvancedNPCs/Tools~/run-tests.sh"`
Expected: `NO RESULTS FILE (compile error?)` with `error CS0103: The name 'StableHash' does not exist in the current context`.

- [ ] **Step 3: Implement**

`Assets/Game/Mods/AdvancedNPCs/Scripts/Core/StableHash.cs`:

```csharp
using System.Text;

namespace AdvancedNPCs.Core
{
    /// <summary>
    /// FNV-1a 32-bit hash of the UTF-8 bytes. Unlike string.GetHashCode it never changes between runs, runtimes
    /// or versions, so it can seed generic people that must look the same on every visit (spec §4, §7.4).
    /// </summary>
    public static class StableHash
    {
        public static uint Of(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text == null ? "" : text);
            uint hash = 2166136261;
            unchecked
            {
                for (int i = 0; i < bytes.Length; i++)
                {
                    hash ^= bytes[i];
                    hash *= 16777619;
                }
            }
            return hash;
        }
    }
}
```

`Assets/Game/Mods/AdvancedNPCs/Scripts/Core/SeededRandom.cs`:

```csharp
using System.Collections.Generic;

namespace AdvancedNPCs.Core
{
    /// <summary>Small deterministic PRNG (xorshift32). The same seed always gives the same sequence.</summary>
    public class SeededRandom
    {
        uint state;

        public SeededRandom(uint seed)
        {
            state = seed == 0 ? 0x9E3779B9u : seed; // xorshift never leaves 0
        }

        public uint NextUInt()
        {
            uint x = state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            state = x;
            return x;
        }

        /// <summary>0 &lt;= result &lt; max; 0 when max &lt;= 0.</summary>
        public int Next(int max)
        {
            if (max <= 0)
                return 0;
            return (int)(NextUInt() % (uint)max);
        }

        /// <summary>min &lt;= result &lt;= max; min when max &lt;= min.</summary>
        public int Range(int min, int max)
        {
            if (max <= min)
                return min;
            return min + Next(max - min + 1);
        }

        /// <summary>One element of a non-empty list.</summary>
        public T Pick<T>(IList<T> list)
        {
            return list[Next(list.Count)];
        }
    }
}
```

`Assets/Game/Mods/AdvancedNPCs/Scripts/Core/VanillaFaces.cs`:

```csharp
namespace AdvancedNPCs.Core
{
    /// <summary>
    /// Vanilla citizen face records (copy of MobilePersonNPC's tables, MobilePersonNPC.cs:32-39): record =
    /// table[outfit] + face, with 4 outfit variants and 24 faces each. Used for the talk proxy's portrait.
    /// </summary>
    public static class VanillaFaces
    {
        public const int OutfitVariants = 4;
        public const int FaceVariants = 24;

        static readonly int[] MaleRedguard = { 336, 312, 336, 312 };
        static readonly int[] FemaleRedguard = { 144, 144, 120, 96 };
        static readonly int[] MaleNord = { 240, 264, 168, 192 };
        static readonly int[] FemaleNord = { 72, 0, 48, 0 };
        static readonly int[] MaleBreton = { 192, 216, 288, 240 };
        static readonly int[] FemaleBreton = { 72, 72, 24, 72 };

        /// <summary>Unknown races use the Breton table; out-of-range variants are clamped.</summary>
        public static int FaceRecord(string race, string gender, int outfit, int face)
        {
            bool female = gender == "Female";
            int[] table;
            if (race == "Redguard")
                table = female ? FemaleRedguard : MaleRedguard;
            else if (race == "Nord")
                table = female ? FemaleNord : MaleNord;
            else
                table = female ? FemaleBreton : MaleBreton;

            int o = outfit < 0 ? 0 : (outfit >= OutfitVariants ? OutfitVariants - 1 : outfit);
            int f = face < 0 ? 0 : (face >= FaceVariants ? FaceVariants - 1 : face);
            return table[o] + f;
        }
    }
}
```

`Assets/Game/Mods/AdvancedNPCs/Scripts/Core/NpcInstance.cs`:

```csharp
namespace AdvancedNPCs.Core
{
    /// <summary>
    /// One ANPC as it is spawned: a unique ANPC (Key = id) or one generic person planned from a template
    /// (Key = template@mapId#n). Everything random about it comes from Seed, in a fixed order.
    /// </summary>
    public class NpcInstance
    {
        public string Key;
        public NpcDefinition Definition;
        public string Name;
        /// <summary>"Male" or "Female".</summary>
        public string Gender;
        /// <summary>"Breton", "Redguard" or "Nord".</summary>
        public string Race;
        /// <summary>Normalised portrait name, or null for a vanilla face.</summary>
        public string PortraitName;
        public int FaceOutfit;
        public int FaceVariant;
        public uint Seed;
        /// <summary>True when X, Y, Z are known (unique ANPCs, spawn.places positions).</summary>
        public bool HasFixedPosition;
        /// <summary>Position relative to the town's origin.</summary>
        public float X;
        public float Y;
        public float Z;
        /// <summary>Index into the town's walkable cell list, or -1.</summary>
        public int CellIndex = -1;
        /// <summary>False for "Random each visit" generic people: their state is never saved.</summary>
        public bool Persistent = true;

        public int FaceRecord()
        {
            return VanillaFaces.FaceRecord(Race, Gender, FaceOutfit, FaceVariant);
        }

        public static NpcInstance ForUnique(NpcDefinition def, string defaultRace)
        {
            NpcInstance i = new NpcInstance();
            i.Key = def.Id;
            i.Definition = def;
            i.Name = def.Name;
            i.Seed = StableHash.Of(def.Id);
            SeededRandom rng = new SeededRandom(i.Seed);
            i.Gender = PickGender(def.Gender, rng);
            i.Race = string.IsNullOrEmpty(def.Race) ? defaultRace : def.Race;
            i.PortraitName = def.Portraits.Count > 0 ? def.Portraits[0] : null;
            i.FaceOutfit = rng.Next(VanillaFaces.OutfitVariants);
            i.FaceVariant = rng.Next(VanillaFaces.FaceVariants);
            i.HasFixedPosition = true;
            i.X = def.X;
            i.Y = def.Y;
            i.Z = def.Z;
            i.Persistent = true;
            return i;
        }

        public static string PickGender(string fixedGender, SeededRandom rng)
        {
            if (!string.IsNullOrEmpty(fixedGender))
                return fixedGender;
            return rng.Next(2) == 0 ? "Male" : "Female";
        }
    }
}
```

In `AdvancedNPCs.dfmod.json`, add after `"Assets/Game/Mods/AdvancedNPCs/Scripts/Core/NpcDefinition.cs",`:

```json
        "Assets/Game/Mods/AdvancedNPCs/Scripts/Core/NpcInstance.cs",
```

after `"Assets/Game/Mods/AdvancedNPCs/Scripts/Core/PositionFormat.cs",`:

```json
        "Assets/Game/Mods/AdvancedNPCs/Scripts/Core/SeededRandom.cs",
```

after `"Assets/Game/Mods/AdvancedNPCs/Scripts/Core/SpawnRules.cs",`:

```json
        "Assets/Game/Mods/AdvancedNPCs/Scripts/Core/StableHash.cs",
        "Assets/Game/Mods/AdvancedNPCs/Scripts/Core/VanillaFaces.cs",
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `"Assets/Game/Mods/AdvancedNPCs/Tools~/run-tests.sh"` — Expected: `result="Passed"`, `failed="0"`.
Run: `"Assets/Game/Mods/AdvancedNPCs/Tools~/runtime-check.sh"` — Expected: exit 0.
Run the manifest check — Expected: no output.

- [ ] **Step 5: Commit**

```bash
git add Assets/Game/Mods/AdvancedNPCs/Scripts/Core/StableHash.cs Assets/Game/Mods/AdvancedNPCs/Scripts/Core/SeededRandom.cs Assets/Game/Mods/AdvancedNPCs/Scripts/Core/VanillaFaces.cs Assets/Game/Mods/AdvancedNPCs/Scripts/Core/NpcInstance.cs Assets/Game/Mods/AdvancedNPCs/Editor/Tests/SeedingTests.cs Assets/Game/Mods/AdvancedNPCs/Editor/Tests/NpcInstanceTests.cs Assets/Game/Mods/AdvancedNPCs/AdvancedNPCs.dfmod.json
git commit -m "feat: add stable seeding, vanilla face tables and NpcInstance" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Save only states that differ from fresh

**Files:**
- Modify: `Assets/Game/Mods/AdvancedNPCs/Scripts/Core/NpcState.cs` (`NpcState.IsDefault`, `NpcStateTable.Snapshot`)
- Test: `Assets/Game/Mods/AdvancedNPCs/Editor/Tests/NpcStateTableTests.cs` (add two tests)

**Interfaces:**
- Consumes: existing `NpcState`, `NpcStateTable`.
- Produces: `NpcState.IsDefault() -> bool` (alive, calm, health ≥ 0.999); `NpcStateTable.Snapshot()` omits default states. `Restore` unchanged (keeps whatever it is given).

- [ ] **Step 1: Write the failing tests**

Add to `NpcStateTableTests` (inside the class, after `Snapshot_IsIndependentCopy`):

```csharp
        [Test]
        public void Snapshot_OmitsFreshStates()
        {
            NpcStateTable t = new NpcStateTable();
            t.GetOrCreate("fresh");
            t.GetOrCreate("hurt").healthFraction = 0.5f;
            t.GetOrCreate("dead").dead = true;
            t.GetOrCreate("angry").hostile = true;
            Dictionary<string, NpcState> snap = t.Snapshot();
            Assert.IsFalse(snap.ContainsKey("fresh"));
            Assert.IsTrue(snap.ContainsKey("hurt"));
            Assert.IsTrue(snap.ContainsKey("dead"));
            Assert.IsTrue(snap.ContainsKey("angry"));
            Assert.IsTrue(t.Has("fresh")); // only the snapshot drops it
        }

        [Test]
        public void IsDefault_IgnoresOldCalmDeadline()
        {
            NpcState s = new NpcState();
            s.hostileUntil = 500UL;
            Assert.IsTrue(s.IsDefault());
            s.hostile = true;
            Assert.IsFalse(s.IsDefault());
        }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `"Assets/Game/Mods/AdvancedNPCs/Tools~/run-tests.sh"`
Expected: `NO RESULTS FILE (compile error?)` with `error CS1061: 'NpcState' does not contain a definition for 'IsDefault'`.

- [ ] **Step 3: Implement**

In `NpcState` (after `Clone()`), add:

```csharp
        /// <summary>Alive, calm and at full health: nothing worth saving (spec §8).</summary>
        public bool IsDefault()
        {
            return !dead && !hostile && healthFraction >= 0.999f;
        }
```

In `NpcStateTable.Snapshot`, replace the loop

```csharp
            foreach (KeyValuePair<string, NpcState> kv in states)
                copy.Add(kv.Key, kv.Value.Clone());
```

with

```csharp
            foreach (KeyValuePair<string, NpcState> kv in states)
            {
                if (!kv.Value.IsDefault())
                    copy.Add(kv.Key, kv.Value.Clone());
            }
```

and change its comment line above the method (add one if missing) to `/// <summary>Copy of every state that differs from fresh; fresh ones are not saved.</summary>`.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `"Assets/Game/Mods/AdvancedNPCs/Tools~/run-tests.sh"` — Expected: `result="Passed"`, `failed="0"` (`Restore_ReplacesContentAndKeepsUnknownIds` still passes: `Restore` keeps every entry it receives).

- [ ] **Step 5: Commit**

```bash
git add Assets/Game/Mods/AdvancedNPCs/Scripts/Core/NpcState.cs Assets/Game/Mods/AdvancedNPCs/Editor/Tests/NpcStateTableTests.cs
git commit -m "feat: save only NPC states that differ from fresh" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Spawn `NpcInstance`s

No behaviour change except that a unique ANPC without `gender` now gets a fixed, seeded gender instead of a random one per spawn. Verified by the compile checks here and by the full self-test in Task 7.

**Files:**
- Modify: `Assets/Game/Mods/AdvancedNPCs/Scripts/Runtime/NpcSpawner.cs` (whole file)
- Modify: `Assets/Game/Mods/AdvancedNPCs/Scripts/Runtime/NpcBrain.cs` (identity members)
- Modify: `Assets/Game/Mods/AdvancedNPCs/Scripts/Runtime/SelfTest.cs` (`Make`)
- Modify: `Assets/Game/Mods/AdvancedNPCs/Scripts/Runtime/AdvancedNpcsMod.cs` (`ListCommand`)

**Interfaces:**
- Consumes: `NpcInstance.ForUnique`, `NpcInstance` fields (Task 4); `DefinitionCatalog.ForLocation` (Task 2); `SpawnRules.Decide`, `NpcState`, `NpcStateTable` (existing).
- Produces:
  - `NpcBrain.Init(NpcInstance instance, NpcState npcState)`; `NpcBrain.Id` (= instance key); `NpcBrain.DisplayName`; `NpcBrain.Instance` (the `NpcInstance`).
  - `NpcSpawner.SpawnFor(DaggerfallLocation)`, `NpcSpawner.SpawnTest(NpcInstance instance, Transform parent) -> NpcBrain`, private `NpcBrain SpawnChecked(NpcInstance, NpcState, DaggerfallLocation)`, private `NpcBrain Spawn(NpcInstance, NpcState, Transform)`, `static string NpcSpawner.DefaultRace()`.

- [ ] **Step 1: Replace `NpcSpawner.cs`**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Utility;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>Spawns ANPCs when their town's GameObject is created.</summary>
    public class NpcSpawner
    {
        readonly AdvancedNpcsMod owner;

        // Towns currently built by StreamingWorld (several are loaded around the player at once).
        readonly List<DaggerfallLocation> knownLocations = new List<DaggerfallLocation>();

        public NpcSpawner(AdvancedNpcsMod owner)
        {
            this.owner = owner;
        }

        public void Enable()
        {
            StreamingWorld.OnCreateLocationGameObject += SpawnFor;
            StreamingWorld.OnClearStreamingWorld += OnWorldCleared;
            owner.OnStateRestored += RespawnLoadedLocations;
        }

        public void Disable()
        {
            StreamingWorld.OnCreateLocationGameObject -= SpawnFor;
            StreamingWorld.OnClearStreamingWorld -= OnWorldCleared;
            owner.OnStateRestored -= RespawnLoadedLocations;
        }

        /// <summary>After a load or new game, rebuild ANPCs in every loaded town from the restored state.</summary>
        public void RespawnLoadedLocations()
        {
            NpcBrain.DespawnAll();
            List<DaggerfallLocation> towns = LoadedLocations();
            if (towns.Count == 0 && GameManager.Instance != null && GameManager.Instance.StreamingWorld != null)
            {
                DaggerfallLocation current = GameManager.Instance.StreamingWorld.CurrentPlayerLocationObject;
                if (current != null)
                    towns.Add(current);
            }
            foreach (DaggerfallLocation town in towns)
                SpawnFor(town);
        }

        // Fast travel, teleport and load tear the world down; old towns are destroyed over many frames.
        void OnWorldCleared()
        {
            NpcBrain.DespawnAll();
            knownLocations.Clear();
        }

        List<DaggerfallLocation> LoadedLocations()
        {
            // StreamingWorld deactivates a town object before destroying it, so inactive ones are on their way out.
            knownLocations.RemoveAll(delegate (DaggerfallLocation l) { return l == null || !l.gameObject.activeSelf; });
            return new List<DaggerfallLocation>(knownLocations);
        }

        public void SpawnFor(DaggerfallLocation location)
        {
            if (location == null)
                return;
            if (!knownLocations.Contains(location))
                knownLocations.Add(location);

            string defaultRace = DefaultRace();
            foreach (NpcDefinition def in owner.Catalog.ForLocation(location.Summary.RegionName, location.Summary.LocationName))
            {
                NpcInstance instance = NpcInstance.ForUnique(def, defaultRace);
                SpawnChecked(instance, owner.States.GetOrCreate(instance.Key), location);
            }
        }

        /// <summary>Spawns an ANPC that is not in the catalog (self-test). Its state lives in the normal table.</summary>
        public NpcBrain SpawnTest(NpcInstance instance, Transform parent)
        {
            return Spawn(instance, owner.States.GetOrCreate(instance.Key), parent);
        }

        /// <summary>The region's people, used when an ANPC does not set its race.</summary>
        public static string DefaultRace()
        {
            FactionFile.FactionRaces people = GameManager.Instance.PlayerGPS.ClimateSettings.People;
            if (people == FactionFile.FactionRaces.Redguard)
                return "Redguard";
            if (people == FactionFile.FactionRaces.Nord)
                return "Nord";
            return "Breton";
        }

        /// <summary>Spawns unless dead or already spawned in this town (SpawnRules). Returns the new brain or null.</summary>
        NpcBrain SpawnChecked(NpcInstance instance, NpcState state, DaggerfallLocation location)
        {
            NpcBrain existing = NpcBrain.Find(instance.Key);
            bool existingHere = existing != null && existing.transform.parent == location.transform;
            SpawnAction action = SpawnRules.Decide(state.dead, existing != null, existingHere);
            if (action == SpawnAction.Skip)
                return null;
            if (action == SpawnAction.ReplaceStale)
                NpcBrain.Discard(existing);
            try
            {
                return Spawn(instance, state, location.transform);
            }
            catch (Exception e)
            {
                AdvancedNpcsMod.LogError(instance.Key + ": spawn failed (" + e.Message + ")");
                return null;
            }
        }

        NpcBrain Spawn(NpcInstance instance, NpcState state, Transform parent)
        {
            NpcDefinition def = instance.Definition;
            MobileTypes type = (MobileTypes)Enum.Parse(typeof(MobileTypes), def.BaseClass);
            MobileGender gender = instance.Gender == "Female" ? MobileGender.Female : MobileGender.Male;

            GameObject go = GameObjectHelper.CreateEnemy(instance.Name, type, new Vector3(instance.X, instance.Y, instance.Z),
                gender, parent, MobileReactions.Passive);

            // SerializableEnemy only registers with the vanilla save system when LoadID != 0.
            // Keeping it 0 means our own state table is the only save, so loads never duplicate ANPCs.
            DaggerfallEnemy enemy = go.GetComponent<DaggerfallEnemy>();
            if (enemy != null)
                enemy.LoadID = 0;

            // Townsfolk side: with Enemy Infighting on, enemies attack anything on another team, and class
            // enemies default to KnightsAndMages/Criminals. On the CityWatch team, guards leave them alone
            // while monsters (other teams) can still attack them.
            DaggerfallEntityBehaviour behaviour = go.GetComponent<DaggerfallEntityBehaviour>();
            if (behaviour != null && behaviour.Entity != null)
                behaviour.Entity.Team = MobileTeams.CityWatch;

            go.AddComponent<NpcMover>();
            NpcBrain brain = go.AddComponent<NpcBrain>();
            brain.Init(instance, state);
            AdvancedNpcsMod.Log(instance.Key + " (" + instance.Name + "): spawned.");
            return brain;
        }
    }
}
```

- [ ] **Step 2: Give `NpcBrain` an instance identity**

In `Assets/Game/Mods/AdvancedNPCs/Scripts/Runtime/NpcBrain.cs`:

1. Below the field `NpcDefinition def;` add:
   ```csharp
           NpcInstance instance;
           string key;
   ```
2. In `Discard`, replace
   ```csharp
   if (brain.def != null && live.TryGetValue(brain.def.Id, out current) && current == brain)
       live.Remove(brain.def.Id);
   ```
   with
   ```csharp
   if (brain.key != null && live.TryGetValue(brain.key, out current) && current == brain)
       live.Remove(brain.key);
   ```
3. Replace the `Id` property with:
   ```csharp
           /// <summary>Unique id or generic instance key.</summary>
           public string Id
           {
               get { return key != null ? key : "?"; }
           }

           public string DisplayName
           {
               get { return instance != null ? instance.Name : "?"; }
           }

           public NpcInstance Instance
           {
               get { return instance; }
           }
   ```
4. Replace `Init` with:
   ```csharp
           public void Init(NpcInstance npcInstance, NpcState npcState)
           {
               instance = npcInstance;
               def = npcInstance.Definition;
               key = npcInstance.Key;
               state = npcState;
               rng = new System.Random((int)npcInstance.Seed ^ System.Environment.TickCount);
               live[key] = this;
           }
   ```
5. In `OnDestroy`, replace
   ```csharp
   if (def != null && live.TryGetValue(def.Id, out current) && current == this)
       live.Remove(def.Id);
   ```
   with
   ```csharp
   if (key != null && live.TryGetValue(key, out current) && current == this)
       live.Remove(key);
   ```
6. In `OnAttackedByPlayer` replace `AdvancedNpcsMod.Log(def.Id + ": assaulted by player.");` with `AdvancedNpcsMod.Log(key + ": assaulted by player.");`; in `UpdateCalmDown` replace `AdvancedNpcsMod.Log(def.Id + ": calmed down.");` with `AdvancedNpcsMod.Log(key + ": calmed down.");`.
7. In `OnDeath` replace `ResolveKiller(def, motor, wasHostile, fightingCreature)` with `ResolveKiller(def, key, motor, wasHostile, fightingCreature)`; change the signature to `static IEnumerator ResolveKiller(NpcDefinition def, string key, EnemyMotor motor, bool wasHostile, bool fightingCreature)` and its log line to `AdvancedNpcsMod.Log(key + ": died" + (byPlayer ? " (player)." : " (creature)."));`.

Then confirm no `def.Id` is left:

```bash
grep -n "def\.Id" Assets/Game/Mods/AdvancedNPCs/Scripts/Runtime/NpcBrain.cs
```
Expected: no output.

- [ ] **Step 3: Update `SelfTest.Make` and `anpc_list`**

In `SelfTest.Make`, replace the last two lines

```csharp
            d.SourceFile = "(selftest)";
            return spawner.SpawnTest(d, location.transform);
```

with

```csharp
            d.Folder = id;
            d.SourceFile = "(selftest)";
            return spawner.SpawnTest(NpcInstance.ForUnique(d, "Breton"), location.transform);
```

In `AdvancedNpcsMod.ListCommand`, replace `sb.Append(b.Id).Append(": ")` with `sb.Append(b.Id).Append(" (").Append(b.DisplayName).Append("): ")`, and in its first `return` replace the text `"No Advanced NPCs are spawned nearby ("` with `"No ANPCs are spawned nearby ("` (the rest of that line stays).

- [ ] **Step 4: Compile checks**

Run: `"Assets/Game/Mods/AdvancedNPCs/Tools~/compile-check.sh"` — Expected: `compile-check: OK (…)`.
Run: `"Assets/Game/Mods/AdvancedNPCs/Tools~/runtime-check.sh"` — Expected: exit 0.

- [ ] **Step 5: Commit**

```bash
git add Assets/Game/Mods/AdvancedNPCs/Scripts/Runtime/NpcSpawner.cs Assets/Game/Mods/AdvancedNPCs/Scripts/Runtime/NpcBrain.cs Assets/Game/Mods/AdvancedNPCs/Scripts/Runtime/SelfTest.cs Assets/Game/Mods/AdvancedNPCs/Scripts/Runtime/AdvancedNpcsMod.cs
git commit -m "refactor: spawn ANPCs from NpcInstance (key, name, seeded gender)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: Talking and portraits

**Files:**
- Create: `Assets/Game/Mods/AdvancedNPCs/Scripts/Runtime/PortraitLibrary.cs`
- Create: `Assets/Game/Mods/AdvancedNPCs/Scripts/Runtime/NpcTalk.cs`
- Modify: `Assets/Game/Mods/AdvancedNPCs/Scripts/Runtime/NpcSpawner.cs` (`Spawn`)
- Modify: `Assets/Game/Mods/AdvancedNPCs/Scripts/Runtime/AdvancedNpcsMod.cs` (`Portraits` property, `Awake`)
- Modify: `Assets/Game/Mods/AdvancedNPCs/Scripts/Runtime/SelfTest.cs` (talk checks)
- Modify: `Assets/Game/Mods/AdvancedNPCs/README.md`, `Assets/Game/Mods/AdvancedNPCs/AdvancedNPCs.dfmod.json`

**Interfaces:**
- Consumes: `NpcInstance` (`Name`, `Race`, `Gender`, `PortraitName`, `FaceRecord()`) (Task 4); `NpcBrain.CurrentMode`, `NpcMode` (existing); `AnpcFiles.PortraitsFolder`, `AnpcFiles.PortraitsName` (Task 2); `DefinitionCatalog.ReferencedPortraits()` (Task 2); `PortraitNames.Normalize` (Task 1).
- Produces:
  - `PortraitLibrary.Load(string folder) -> PortraitLibrary`; `Get(string name) -> Texture2D or null`; `WarnMissing(IEnumerable<string> referenced)`; `Count`.
  - `NpcTalk : MonoBehaviour, IPlayerActivable` with `Init(NpcInstance instance, Texture2D portrait)`, `Activate(RaycastHit hit)`, `HandleActivate(PlayerActivateModes mode, float distance)`, `TryTalk() -> bool`, `LastMessage`, `Proxy`, `Portrait` (get/set), `static CurrentTalkPortrait() -> Texture2D`.
  - `AdvancedNpcsMod.Portraits : PortraitLibrary`.

- [ ] **Step 1: Create `PortraitLibrary.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>The shared portraits in ANPCs/_Portraits, loaded once at start and looked up by name (spec §10).</summary>
    public class PortraitLibrary
    {
        readonly Dictionary<string, Texture2D> byName = new Dictionary<string, Texture2D>(StringComparer.Ordinal);

        public int Count
        {
            get { return byName.Count; }
        }

        public static PortraitLibrary Load(string folder)
        {
            PortraitLibrary library = new PortraitLibrary();
            if (!Directory.Exists(folder))
                return library;
            foreach (string path in Directory.GetFiles(folder, "*.png"))
            {
                string file = AnpcFiles.PortraitsName + "/" + Path.GetFileName(path);
                try
                {
                    Texture2D texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                    if (!texture.LoadImage(File.ReadAllBytes(path)))
                    {
                        AdvancedNpcsMod.LogError(file + ": file: not a readable PNG");
                        continue;
                    }
                    string name = PortraitNames.Normalize(Path.GetFileName(path));
                    texture.filterMode = FilterMode.Point;
                    texture.name = name;
                    library.byName[name] = texture;
                }
                catch (Exception e)
                {
                    AdvancedNpcsMod.LogError(file + ": file: could not read (" + e.Message + ")");
                }
            }
            AdvancedNpcsMod.Log("Loaded " + library.Count + " portrait(s).");
            return library;
        }

        /// <summary>The portrait with this name (any case, with or without .png), or null.</summary>
        public Texture2D Get(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            Texture2D texture;
            return byName.TryGetValue(PortraitNames.Normalize(name), out texture) ? texture : null;
        }

        /// <summary>One warning per referenced portrait that has no PNG; those ANPCs use a vanilla face.</summary>
        public void WarnMissing(IEnumerable<string> referenced)
        {
            foreach (string name in referenced)
            {
                if (!byName.ContainsKey(name))
                    AdvancedNpcsMod.Log(AnpcFiles.PortraitsName + "/" + name + ".png: file: portrait not found; using a vanilla face");
            }
        }
    }
}
```

- [ ] **Step 2: Create `NpcTalk.cs`**

```csharp
using System.Reflection;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>
    /// Lets the player talk to an ANPC through DFU's citizen talk window (spec §9.2). TalkManager only talks to
    /// MobilePersonNPC/StaticNPC targets, so a hidden child carries a MobilePersonNPC "talk proxy" with the
    /// ANPC's name, race, gender and vanilla face; a PNG portrait replaces the face after the window opens.
    /// No UIWindowFactory override, so talk-window replacement mods keep working.
    /// </summary>
    public class NpcTalk : MonoBehaviour, IPlayerActivable
    {
        static readonly FieldInfo TexturePortraitField =
            typeof(DaggerfallTalkWindow).GetField("texturePortrait", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo PanelPortraitField =
            typeof(DaggerfallTalkWindow).GetField("panelPortrait", BindingFlags.Instance | BindingFlags.NonPublic);
        static bool warnedNoPortraitFields;

        MobilePersonNPC proxy;
        NpcBrain brain;
        Texture2D portrait;
        string displayName;

        /// <summary>The last mid-screen text this ANPC showed (read by the self-test).</summary>
        public string LastMessage { get; private set; }

        public MobilePersonNPC Proxy
        {
            get { return proxy; }
        }

        public Texture2D Portrait
        {
            get { return portrait; }
            set { portrait = value; }
        }

        public void Init(NpcInstance instance, Texture2D portraitTexture)
        {
            GameObject proxyObject = new GameObject("TalkProxy");
            proxyObject.transform.SetParent(transform, false);
            proxy = proxyObject.AddComponent<MobilePersonNPC>();
            proxy.NameNPC = instance.Name;
            proxy.Race = ToRace(instance.Race);
            proxy.Gender = instance.Gender == "Female" ? Genders.Female : Genders.Male;
            proxy.PersonFaceRecordId = instance.FaceRecord();
            displayName = instance.Name;
            portrait = portraitTexture;
            brain = GetComponent<NpcBrain>();
        }

        /// <summary>Called by DFU's PlayerActivate after its own enemy handling when the player clicks this ANPC.</summary>
        public void Activate(RaycastHit hit)
        {
            HandleActivate(GameManager.Instance.PlayerActivate.CurrentMode, hit.distance);
        }

        public void HandleActivate(PlayerActivateModes mode, float distance)
        {
            if (mode == PlayerActivateModes.Steal)
                return; // vanilla enemy pickpocketing handles this
            if (mode == PlayerActivateModes.Info)
            {
                Say("You see " + displayName + "."); // replaces vanilla's "You see a <class>."
                return;
            }
            if (distance > PlayerActivate.MobileNPCActivationDistance)
                return;
            TryTalk();
        }

        /// <summary>Opens the talk window if the ANPC is calm. Returns false if it refused.</summary>
        public bool TryTalk()
        {
            if (brain != null && brain.CurrentMode != NpcMode.Calm)
            {
                Say(displayName + " will not talk to you now.");
                return false;
            }
            TalkManager.Instance.TalkToMobileNPC(proxy);
            if (portrait != null)
                ApplyPortrait(DaggerfallUI.Instance.TalkWindow, portrait);
            return true;
        }

        /// <summary>The portrait texture the talk window currently shows (for the self-test).</summary>
        public static Texture2D CurrentTalkPortrait()
        {
            if (TexturePortraitField == null || DaggerfallUI.Instance.TalkWindow == null)
                return null;
            return TexturePortraitField.GetValue(DaggerfallUI.Instance.TalkWindow) as Texture2D;
        }

        void Say(string text)
        {
            LastMessage = text;
            DaggerfallUI.SetMidScreenText(text);
        }

        static void ApplyPortrait(DaggerfallTalkWindow window, Texture2D texture)
        {
            Panel panel = (window == null || PanelPortraitField == null) ? null : PanelPortraitField.GetValue(window) as Panel;
            if (TexturePortraitField == null || panel == null)
            {
                if (!warnedNoPortraitFields)
                {
                    warnedNoPortraitFields = true;
                    AdvancedNpcsMod.Log("The talk window has no portrait fields (replaced by another mod?); ANPCs show vanilla faces.");
                }
                return;
            }
            texture.filterMode = DaggerfallUI.Instance.GlobalFilterMode;
            TexturePortraitField.SetValue(window, texture);
            panel.BackgroundTexture = texture;
        }

        static Races ToRace(string race)
        {
            if (race == "Redguard")
                return Races.Redguard;
            if (race == "Nord")
                return Races.Nord;
            return Races.Breton;
        }
    }
}
```

- [ ] **Step 3: Wire it in**

In `NpcSpawner.Spawn`, after `brain.Init(instance, state);` add:

```csharp
            go.AddComponent<NpcTalk>().Init(instance, owner.Portraits.Get(instance.PortraitName));
```

In `AdvancedNpcsMod`, below `public NpcStateTable States { get; private set; }` add:

```csharp
        public PortraitLibrary Portraits { get; private set; }
```

and in `Awake`, after `Catalog = LoadCatalog();` add:

```csharp
            Portraits = PortraitLibrary.Load(AnpcFiles.PortraitsFolder);
            Portraits.WarnMissing(Catalog.ReferencedPortraits());
```

In `AdvancedNPCs.dfmod.json`, add after `"Assets/Game/Mods/AdvancedNPCs/Scripts/Runtime/NpcSpawner.cs",`:

```json
        "Assets/Game/Mods/AdvancedNPCs/Scripts/Runtime/NpcTalk.cs",
        "Assets/Game/Mods/AdvancedNPCs/Scripts/Runtime/PortraitLibrary.cs",
```

- [ ] **Step 4: Add the self-test checks**

In `SelfTest.cs`, add to the `using` block:

```csharp
using DaggerfallWorkshop.Game.UserInterfaceWindows;
```

Directly after the line

```csharp
            Check("calm NPCs do not block resting", !gm.AreEnemiesNearby(true), "GameManager.AreEnemiesNearby(resting) returned true");
```

insert:

```csharp
            // Talking (spec §9.2): name, PNG portrait, vanilla face fallback, Info text.
            NpcTalk talk = normal.GetComponent<NpcTalk>();
            Texture2D testPortrait = TestPortrait();
            talk.Portrait = testPortrait;
            bool opened = talk.TryTalk();
            for (int wait = 0; wait < 10 && !TalkWindowOpen(); wait++)
                yield return new WaitForSeconds(0.1f);
            Check("calm ANPC opens DFU's talk window", opened && TalkWindowOpen(),
                "opened=" + opened + ", top window=" + DaggerfallUI.UIManager.TopWindow);
            Check("talk window shows the ANPC's name", TalkManager.Instance.NameNPC == "Selftest Normal", "name=" + TalkManager.Instance.NameNPC);
            Check("talk window shows the ANPC's PNG portrait", NpcTalk.CurrentTalkPortrait() == testPortrait,
                "portrait=" + NpcTalk.CurrentTalkPortrait());
            CloseTalkWindow();
            yield return Settle;

            NpcTalk plain = bystander.GetComponent<NpcTalk>();
            plain.Portrait = null;
            opened = plain.TryTalk();
            for (int wait = 0; wait < 10 && !TalkWindowOpen(); wait++)
                yield return new WaitForSeconds(0.1f);
            Check("ANPC without portrait talks with a vanilla face",
                opened && TalkWindowOpen() && NpcTalk.CurrentTalkPortrait() != testPortrait,
                "opened=" + opened + ", portrait=" + NpcTalk.CurrentTalkPortrait() + ", face record=" + plain.Proxy.PersonFaceRecordId);
            CloseTalkWindow();
            yield return Settle;

            plain.HandleActivate(PlayerActivateModes.Info, 1f);
            Check("Info-mode click names the ANPC", plain.LastMessage == "You see Selftest Normal.", "message=" + plain.LastMessage);
```

Directly after the line

```csharp
            Check("hitting an already hostile NPC reports no new assault", Count(PlayerEntity.Crimes.Assault) == assaults + 1, CrimeList());
```

insert:

```csharp
            Check("hostile ANPC refuses to talk",
                !talk.TryTalk() && !TalkWindowOpen() && talk.LastMessage == "Selftest Normal will not talk to you now.",
                "message=" + talk.LastMessage + ", top window=" + DaggerfallUI.UIManager.TopWindow);
```

Add these helpers next to `Describe`:

```csharp
        static bool TalkWindowOpen()
        {
            return DaggerfallUI.UIManager.TopWindow is DaggerfallTalkWindow;
        }

        static void CloseTalkWindow()
        {
            if (TalkWindowOpen())
                DaggerfallUI.UIManager.PopWindow();
        }

        static Texture2D TestPortrait()
        {
            Texture2D texture = new Texture2D(64, 64, TextureFormat.ARGB32, false);
            Color32[] pixels = new Color32[64 * 64];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32((byte)(i % 64 * 4), (byte)(i / 64 * 4), 160, 255);
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }
```

- [ ] **Step 5: Document talking and portraits**

In `README.md`, in the field table add after the `race` row:

```markdown
| `portrait` | no | vanilla face | name of a PNG in `ANPCs/_Portraits/` without `.png` (any case) |
```

and add after the `## Behaviour` section:

```markdown
## Talking and portraits

- Click a calm ANPC in Talk or Grab mode to open DFU's citizen talk window with its name and portrait; vanilla topics work as for any citizen.
- Hostile or fleeing ANPCs refuse to talk. Info mode shows "You see <name>.".
- Portraits are PNG files in `ANPCs/_Portraits/`, shared by all ANPCs; 64 × 64 pixels recommended. Without one (or if the file is missing) the ANPC shows a vanilla face matching its race and gender.
```

- [ ] **Step 6: Verify**

Run, in order: `compile-check.sh` (Expected `compile-check: OK`), `runtime-check.sh` (exit 0), the manifest check (no output). Close the editor and the game, then:

```bash
"Assets/Game/Mods/AdvancedNPCs/Tools~/build-mod.sh"
git restore Assets/AddressableAssetsData/
"Assets/Game/Mods/AdvancedNPCs/Tools~/selftest.sh"
```

Expected: `SELFTEST DONE 24/24 passed` (18 from 1a + 6 new). `Player.log` contains `Loaded 0 portrait(s).` and no `Exception`.

If `calm ANPC opens DFU's talk window` fails only because the window is not yet on top (the spike saw a timing-only failure here), use superpowers:systematic-debugging: log `DaggerfallUI.UIManager.TopWindow` every 0.1 s after `TryTalk` to see whether the window opens late or closes again, and fix the cause (wait longer or keep input from closing it) — do not delete the check.

- [ ] **Step 7: Commit**

```bash
git status --short Assets/Game/Mods/AdvancedNPCs
git add -A Assets/Game/Mods/AdvancedNPCs
git commit -m "feat: talk to ANPCs through DFU's talk window with PNG portraits" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 8: Phase A hand-off to the user (manual check)**

Tell the user Phase A is ready and ask them to check in game (DFU_testing, Daggerfall NW gate):
1. `StreamingAssets/ANPCs/bram/npc.json` exists; the old `AdvancedNPCs/*.json` are `*.migrated`.
2. Click Bram: the talk window opens with "Bram the Cooper" and a vanilla face; vanilla topics answer.
3. Put any PNG at `StreamingAssets/ANPCs/_Portraits/bram.png`, add `"portrait": "bram"` to `ANPCs/bram/npc.json`, restart: the PNG shows in the talk window.
4. Info mode on Bram shows "You see Bram the Cooper.". Hit him, then click: he refuses to talk.

Wait for their result before starting Phase B.

---

## Phase B — generic townsfolk and the persistence setting

### Task 8: Generic templates

**Files:**
- Create: `Assets/Game/Mods/AdvancedNPCs/Scripts/Core/GenericSpawn.cs`
- Modify: `Assets/Game/Mods/AdvancedNPCs/Scripts/Core/NpcDefinition.cs` (add `Spawn`)
- Modify: `Assets/Game/Mods/AdvancedNPCs/Scripts/Core/DefinitionParser.cs` (replace `ReadGeneric`, add `ReadPlace`, `Xyz`, key lists)
- Modify: `Assets/Game/Mods/AdvancedNPCs/AdvancedNPCs.dfmod.json`
- Test: `Assets/Game/Mods/AdvancedNPCs/Editor/Tests/GenericTemplateTests.cs`

**Interfaces:**
- Consumes: `FieldReader` (Task 1), `PortraitNames` (Task 1), `DefinitionCatalog.SamePlaceName` (Task 2).
- Produces:
  - `class SpawnPlace { string Region; string Place; readonly List<float[]> Positions }`.
  - `class GenericSpawn { readonly List<string> LocationTypes (default TownCity, TownHamlet, TownVillage); readonly List<SpawnPlace> Places; int CountMin = 1; int CountMax = 3; bool Matches(TownInfo town, out SpawnPlace place) }`.
  - `class TownInfo { readonly int MapId; readonly string Region, Place, LocationType, DefaultRace; TownInfo(int mapId, string region, string place, string locationType, string defaultRace) }`.
  - `LocationTypeNames.All`, `LocationTypeNames.Default`, `LocationTypeNames.Canonical(string) -> string or null`.
  - `NpcDefinition.Spawn : GenericSpawn` (non-null for generic templates, null for unique ANPCs).

- [ ] **Step 1: Write the failing tests**

`Assets/Game/Mods/AdvancedNPCs/Editor/Tests/GenericTemplateTests.cs`:

```csharp
using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class GenericTemplateTests
    {
        static ParseResult Parse(string fields)
        {
            return DefinitionParser.ParseFolder("commoner", "{ \"kind\": \"generic\"" + (fields.Length > 0 ? ", " + fields : "") + " }");
        }

        static TownInfo Town(string region, string place, string type)
        {
            return new TownInfo(1, region, place, type, "Breton");
        }

        [Test]
        public void Minimal_AppliesDefaults()
        {
            ParseResult r = Parse("");
            Assert.IsTrue(r.Ok, r.Error);
            NpcDefinition d = r.Definition;
            Assert.AreEqual(NpcKind.Generic, d.Kind);
            Assert.AreEqual("", d.Name);
            Assert.AreEqual(0, d.Names.Count);
            Assert.AreEqual(0, d.Portraits.Count);
            Assert.IsNotNull(d.Spawn);
            CollectionAssert.AreEqual(new[] { "TownCity", "TownHamlet", "TownVillage" }, d.Spawn.LocationTypes);
            Assert.AreEqual(0, d.Spawn.Places.Count);
            Assert.AreEqual(1, d.Spawn.CountMin);
            Assert.AreEqual(3, d.Spawn.CountMax);
            Assert.AreEqual("Spellsword", d.BaseClass);
            Assert.AreEqual(0, r.Warnings.Count);
        }

        [Test]
        public void FullTemplate_IsRead()
        {
            ParseResult r = Parse(
                "\"name\": \"Guard\", \"names\": [\"Ann\", \"Bo\"], \"portrait\": \"A\", \"portraits\": [\"a.png\", \"b\"], " +
                "\"spawn\": { \"locationTypes\": [\"towncity\", \"Tavern\"], \"count\": [2, 4], " +
                "\"places\": [ { \"region\": \"Daggerfall\", \"place\": \"Daggerfall\", \"positions\": [[1, 2, 3], [4.5, 5, 6]] } ] }, " +
                "\"bravery\": \"Coward\"");
            Assert.IsTrue(r.Ok, r.Error);
            NpcDefinition d = r.Definition;
            Assert.AreEqual("Guard", d.Name);
            CollectionAssert.AreEqual(new[] { "Ann", "Bo" }, d.Names);
            CollectionAssert.AreEqual(new[] { "a", "b" }, d.Portraits);
            CollectionAssert.AreEqual(new[] { "TownCity", "Tavern" }, d.Spawn.LocationTypes);
            Assert.AreEqual(2, d.Spawn.CountMin);
            Assert.AreEqual(4, d.Spawn.CountMax);
            Assert.AreEqual(1, d.Spawn.Places.Count);
            Assert.AreEqual("Daggerfall", d.Spawn.Places[0].Region);
            Assert.AreEqual(2, d.Spawn.Places[0].Positions.Count);
            Assert.AreEqual(4.5f, d.Spawn.Places[0].Positions[1][0]);
            Assert.AreEqual(Bravery.Coward, d.Bravery);
        }

        [TestCase("\"location\": { \"region\": \"R\", \"place\": \"P\" }", "commoner/npc.json: location: belongs to unique ANPCs; generic templates use spawn")]
        [TestCase("\"position\": [0, 0, 0]", "commoner/npc.json: position: belongs to unique ANPCs; generic templates use spawn")]
        [TestCase("\"spawn\": { \"locationTypes\": [\"Castle\"] }", "commoner/npc.json: spawn.locationTypes: unknown location type \"Castle\"")]
        [TestCase("\"spawn\": { \"count\": [3, 1] }", "commoner/npc.json: spawn.count: need whole numbers 0 <= min <= max <= 20 (got [3, 1])")]
        [TestCase("\"spawn\": { \"count\": [0, 21] }", "commoner/npc.json: spawn.count: need whole numbers 0 <= min <= max <= 20 (got [0, 21])")]
        [TestCase("\"spawn\": { \"count\": [1.5, 2] }", "commoner/npc.json: spawn.count: need whole numbers 0 <= min <= max <= 20 (got [1.5, 2])")]
        [TestCase("\"spawn\": { \"count\": 2 }", "commoner/npc.json: spawn.count: must be [min, max]")]
        [TestCase("\"spawn\": { \"places\": [ { \"place\": \"P\" } ] }", "commoner/npc.json: spawn.places[0].region: required")]
        [TestCase("\"spawn\": { \"places\": [ { \"region\": \"R\", \"place\": \"P\", \"positions\": [[1, 2]] } ] }", "commoner/npc.json: spawn.places[0].positions: must be a list of [x, y, z]")]
        [TestCase("\"spawn\": []", "commoner/npc.json: spawn: must be an object")]
        [TestCase("\"names\": [\"\"]", "commoner/npc.json: names: must be a list of non-empty texts")]
        public void BadValues_AreRejected(string fields, string expected)
        {
            Assert.AreEqual(expected, Parse(fields).Error);
        }

        [Test]
        public void UnknownSpawnFields_Warn()
        {
            ParseResult r = Parse("\"spawn\": { \"colour\": 1, \"places\": [ { \"region\": \"R\", \"place\": \"P\", \"size\": 2 } ] }");
            Assert.IsTrue(r.Ok, r.Error);
            CollectionAssert.Contains(r.Warnings, "commoner/npc.json: spawn.colour: unknown field, ignored");
            CollectionAssert.Contains(r.Warnings, "commoner/npc.json: spawn.places[0].size: unknown field, ignored");
        }

        [Test]
        public void Matches_DefaultTypes()
        {
            GenericSpawn s = Parse("").Definition.Spawn;
            SpawnPlace place;
            Assert.IsTrue(s.Matches(Town("R", "P", "TownCity"), out place));
            Assert.IsNull(place);
            Assert.IsTrue(s.Matches(Town("R", "P", "TownVillage"), out place));
            Assert.IsFalse(s.Matches(Town("R", "P", "Tavern"), out place));
        }

        [Test]
        public void Matches_PlacesOverrideTypes_IgnoringCaseAndSpaces()
        {
            GenericSpawn s = Parse("\"spawn\": { \"locationTypes\": [\"Tavern\"], \"places\": [ { \"region\": \"Daggerfall\", \"place\": \"Daggerfall\" } ] }").Definition.Spawn;
            SpawnPlace place;
            Assert.IsTrue(s.Matches(Town(" daggerfall", "DAGGERFALL ", "TownCity"), out place));
            Assert.AreEqual("Daggerfall", place.Place);
            Assert.IsFalse(s.Matches(Town("Wayrest", "Wayrest", "Tavern"), out place));
        }

        [Test]
        public void Catalog_PutsTemplatesInGenerics_AndWarnsAboutDialogue()
        {
            DefinitionCatalog c = DefinitionCatalog.Build(new[]
            {
                new AnpcFolder("commoner", "{ \"kind\": \"generic\", \"portraits\": [\"c1\"] }", "{ \"topics\": [] }"),
            });
            Assert.AreEqual(0, c.ById.Count);
            Assert.AreEqual(1, c.Generics.Count);
            CollectionAssert.Contains(c.Messages, "commoner/dialogue.json: file: topics are for unique ANPCs only, ignored");
            CollectionAssert.Contains(c.Messages, "Loaded 0 unique ANPC(s) and 1 generic template(s).");
            CollectionAssert.AreEqual(new[] { "c1" }, c.ReferencedPortraits());
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `"Assets/Game/Mods/AdvancedNPCs/Tools~/run-tests.sh"`
Expected: `NO RESULTS FILE (compile error?)` with `error CS0246: The type or namespace name 'TownInfo' could not be found`.

- [ ] **Step 3: Implement**

`Assets/Game/Mods/AdvancedNPCs/Scripts/Core/GenericSpawn.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace AdvancedNPCs.Core
{
    /// <summary>The town a generic template is planned for.</summary>
    public class TownInfo
    {
        public readonly int MapId;
        public readonly string Region;
        public readonly string Place;
        /// <summary>A LocationTypeNames name, e.g. "TownCity".</summary>
        public readonly string LocationType;
        /// <summary>The region's people ("Breton", "Redguard", "Nord").</summary>
        public readonly string DefaultRace;

        public TownInfo(int mapId, string region, string place, string locationType, string defaultRace)
        {
            MapId = mapId;
            Region = region;
            Place = place;
            LocationType = locationType;
            DefaultRace = defaultRace;
        }
    }

    /// <summary>One entry of spawn.places: a town and, optionally, fixed spots there (from anpc_pos).</summary>
    public class SpawnPlace
    {
        public string Region;
        public string Place;
        public readonly List<float[]> Positions = new List<float[]>();
    }

    /// <summary>Where, and how many, instances of a generic template appear (spec §7.3).</summary>
    public class GenericSpawn
    {
        public readonly List<string> LocationTypes = new List<string>(LocationTypeNames.Default);
        public readonly List<SpawnPlace> Places = new List<SpawnPlace>();
        public int CountMin = 1;
        public int CountMax = 3;

        /// <summary>
        /// True if the template spawns in this town. When spawn.places is given only those towns match (and
        /// place is the matching entry); otherwise the town's location type must be listed (place is null).
        /// </summary>
        public bool Matches(TownInfo town, out SpawnPlace place)
        {
            place = null;
            if (Places.Count > 0)
            {
                foreach (SpawnPlace p in Places)
                {
                    if (DefinitionCatalog.SamePlaceName(p.Region, town.Region) && DefinitionCatalog.SamePlaceName(p.Place, town.Place))
                    {
                        place = p;
                        return true;
                    }
                }
                return false;
            }
            foreach (string type in LocationTypes)
            {
                if (string.Equals(type, town.LocationType, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }

    /// <summary>DFRegion.LocationTypes names (copied: Core has no DFU references).</summary>
    public static class LocationTypeNames
    {
        public static readonly string[] All =
        {
            "TownCity", "TownHamlet", "TownVillage", "HomeFarms", "DungeonLabyrinth", "ReligionTemple", "Tavern",
            "DungeonKeep", "HomeWealthy", "ReligionCult", "DungeonRuin", "HomePoor", "Graveyard", "Coven", "HomeYourShips",
        };

        public static readonly string[] Default = { "TownCity", "TownHamlet", "TownVillage" };

        public static string Canonical(string raw)
        {
            if (raw == null)
                return null;
            foreach (string name in All)
            {
                if (string.Equals(name, raw.Trim(), StringComparison.OrdinalIgnoreCase))
                    return name;
            }
            return null;
        }
    }
}
```

In `NpcDefinition.cs`, add below the `Names` field:

```csharp
        /// <summary>Generic templates only: where and how many (null for unique ANPCs).</summary>
        public GenericSpawn Spawn;
```

In `DefinitionParser.cs`, add below `static readonly string[] RaceNames = …;`:

```csharp
        static readonly string[] SpawnKeys = { "locationTypes", "places", "count" };
        static readonly string[] PlaceKeys = { "region", "place", "positions" };
```

and replace the whole `ReadGeneric` method (including its comment line) with:

```csharp
        static bool ReadGeneric(string file, Dictionary<string, object> o, NpcDefinition d, ParseResult r)
        {
            if (o.ContainsKey("location"))
                return Problem(r, file, "location", "belongs to unique ANPCs; generic templates use spawn");
            if (o.ContainsKey("position"))
                return Problem(r, file, "position", "belongs to unique ANPCs; generic templates use spawn");

            string problem;
            string name;
            if ((problem = FieldReader.Text(o, "name", "", out name)) != null)
                return Problem(r, file, "name", problem);
            d.Name = name;
            List<string> names;
            if ((problem = FieldReader.Texts(o, "names", out names)) != null)
                return Problem(r, file, "names", problem);
            if (names != null)
                d.Names.AddRange(names);

            string portrait;
            if ((problem = FieldReader.Text(o, "portrait", "", out portrait)) != null)
                return Problem(r, file, "portrait", problem);
            AddPortrait(d, portrait);
            List<string> portraits;
            if ((problem = FieldReader.Texts(o, "portraits", out portraits)) != null)
                return Problem(r, file, "portraits", problem);
            if (portraits != null)
            {
                foreach (string p in portraits)
                    AddPortrait(d, p);
            }

            d.Spawn = new GenericSpawn();
            Dictionary<string, object> spawn;
            if ((problem = FieldReader.Object(o, "spawn", out spawn)) != null)
                return Problem(r, file, "spawn", problem);
            if (spawn == null)
                return true;
            foreach (string key in FieldReader.UnknownKeys(spawn, SpawnKeys))
                r.Warnings.Add(file + ": spawn." + key + ": unknown field, ignored");

            List<string> types;
            if ((problem = FieldReader.Texts(spawn, "locationTypes", out types)) != null)
                return Problem(r, file, "spawn.locationTypes", problem);
            if (types != null)
            {
                d.Spawn.LocationTypes.Clear();
                foreach (string t in types)
                {
                    string canonical = LocationTypeNames.Canonical(t);
                    if (canonical == null)
                        return Problem(r, file, "spawn.locationTypes", "unknown location type \"" + t + "\"");
                    d.Spawn.LocationTypes.Add(canonical);
                }
            }

            List<Dictionary<string, object>> places;
            if ((problem = FieldReader.Objects(spawn, "places", out places)) != null)
                return Problem(r, file, "spawn.places", problem);
            if (places != null)
            {
                for (int i = 0; i < places.Count; i++)
                {
                    SpawnPlace p = new SpawnPlace();
                    if (!ReadPlace(file, "spawn.places[" + i + "]", places[i], p, r))
                        return false;
                    d.Spawn.Places.Add(p);
                }
            }

            double[] count;
            if (!FieldReader.Numbers(spawn, "count", out count) || (count != null && count.Length != 2))
                return Problem(r, file, "spawn.count", "must be [min, max]");
            if (count != null)
            {
                if (count[0] != Math.Floor(count[0]) || count[1] != Math.Floor(count[1]) ||
                    count[0] < 0 || count[0] > count[1] || count[1] > 20)
                    return Problem(r, file, "spawn.count", "need whole numbers 0 <= min <= max <= 20 (got [" +
                        FieldReader.Num(count[0]) + ", " + FieldReader.Num(count[1]) + "])");
                d.Spawn.CountMin = (int)count[0];
                d.Spawn.CountMax = (int)count[1];
            }
            return true;
        }

        static void AddPortrait(NpcDefinition d, string raw)
        {
            string name = PortraitNames.Normalize(raw);
            if (name.Length > 0 && !d.Portraits.Contains(name))
                d.Portraits.Add(name);
        }

        static bool ReadPlace(string file, string field, Dictionary<string, object> o, SpawnPlace p, ParseResult r)
        {
            foreach (string key in FieldReader.UnknownKeys(o, PlaceKeys))
                r.Warnings.Add(file + ": " + field + "." + key + ": unknown field, ignored");

            string problem;
            if ((problem = FieldReader.Text(o, "region", null, out p.Region)) != null)
                return Problem(r, file, field + ".region", problem);
            if (string.IsNullOrEmpty(p.Region))
                return Problem(r, file, field + ".region", "required");
            if ((problem = FieldReader.Text(o, "place", null, out p.Place)) != null)
                return Problem(r, file, field + ".place", problem);
            if (string.IsNullOrEmpty(p.Place))
                return Problem(r, file, field + ".place", "required");

            object raw;
            if (o.TryGetValue("positions", out raw) && raw != null)
            {
                List<object> list = raw as List<object>;
                if (list == null)
                    return Problem(r, file, field + ".positions", "must be a list of [x, y, z]");
                foreach (object item in list)
                {
                    float[] xyz = Xyz(item);
                    if (xyz == null)
                        return Problem(r, file, field + ".positions", "must be a list of [x, y, z]");
                    p.Positions.Add(xyz);
                }
            }
            return true;
        }

        static float[] Xyz(object item)
        {
            List<object> list = item as List<object>;
            if (list == null || list.Count != 3)
                return null;
            float[] xyz = new float[3];
            for (int i = 0; i < 3; i++)
            {
                if (!(list[i] is double))
                    return null;
                xyz[i] = (float)(double)list[i];
            }
            return xyz;
        }
```

In `AdvancedNPCs.dfmod.json`, add after `"Assets/Game/Mods/AdvancedNPCs/Scripts/Core/FieldReader.cs",`:

```json
        "Assets/Game/Mods/AdvancedNPCs/Scripts/Core/GenericSpawn.cs",
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `"Assets/Game/Mods/AdvancedNPCs/Tools~/run-tests.sh"` — Expected: `result="Passed"`, `failed="0"`.
Run: `"Assets/Game/Mods/AdvancedNPCs/Tools~/runtime-check.sh"` — Expected: exit 0.
Run the manifest check — Expected: no output.

- [ ] **Step 5: Commit**

```bash
git add Assets/Game/Mods/AdvancedNPCs/Scripts/Core/GenericSpawn.cs Assets/Game/Mods/AdvancedNPCs/Scripts/Core/NpcDefinition.cs Assets/Game/Mods/AdvancedNPCs/Scripts/Core/DefinitionParser.cs Assets/Game/Mods/AdvancedNPCs/Editor/Tests/GenericTemplateTests.cs Assets/Game/Mods/AdvancedNPCs/AdvancedNPCs.dfmod.json
git commit -m "feat: parse generic ANPC templates (names, portraits, spawn rules)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 9: Population planner

**Files:**
- Create: `Assets/Game/Mods/AdvancedNPCs/Scripts/Core/PopulationPlanner.cs`
- Modify: `Assets/Game/Mods/AdvancedNPCs/AdvancedNPCs.dfmod.json`
- Test: `Assets/Game/Mods/AdvancedNPCs/Editor/Tests/PopulationPlannerTests.cs`

**Interfaces:**
- Consumes: `NpcDefinition` with `Kind`, `Spawn`, `Names`, `Name`, `Portraits`, `Gender`, `Race` (Tasks 1, 8); `GenericSpawn.Matches`, `SpawnPlace`, `TownInfo` (Task 8); `NpcInstance`, `NpcInstance.PickGender`, `StableHash`, `SeededRandom`, `VanillaFaces` (Task 4).
- Produces:
  - `enum GenericMode { SamePeople, RandomEachVisit }` (top level).
  - `interface INameSource { string Generate(string race, string gender, uint seed); }`.
  - `PopulationPlanner.KeyFor(string templateId, int mapId, int n) -> string` (`"commoner@1234#2"`).
  - `PopulationPlanner.Plan(IList<NpcDefinition> templates, TownInfo town, GenericMode mode, int cap, int cellCount, uint visitSeed, INameSource names) -> List<NpcInstance>`: templates in ordinal id order; per template `count = Range(CountMin, CountMax)` from `StableHash(template@mapId)` (same people) or the visit seed (random); stops at `cap`; per instance, from its seed in this order: gender, name, portrait, face, then position (`spawn.places[].positions[n]` if present, else a cell index without repeats where possible, `-1` when `cellCount == 0`). `Persistent = (mode == SamePeople)`. `visitSeed` is ignored in same-people mode.

- [ ] **Step 1: Write the failing tests**

`Assets/Game/Mods/AdvancedNPCs/Editor/Tests/PopulationPlannerTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class PopulationPlannerTests
    {
        class FakeNames : INameSource
        {
            public readonly List<string> Calls = new List<string>();

            public string Generate(string race, string gender, uint seed)
            {
                Calls.Add(race + "/" + gender);
                return race + " " + gender + " " + seed;
            }
        }

        static NpcDefinition Template(string id, int min, int max)
        {
            NpcDefinition d = new NpcDefinition();
            d.Id = id;
            d.Folder = id;
            d.Kind = NpcKind.Generic;
            d.Name = "";
            d.Gender = "";
            d.BaseClass = "Bard";
            d.Spawn = new GenericSpawn();
            d.Spawn.CountMin = min;
            d.Spawn.CountMax = max;
            return d;
        }

        static TownInfo Town(int mapId)
        {
            return new TownInfo(mapId, "Daggerfall", "Daggerfall", "TownCity", "Breton");
        }

        static List<NpcInstance> Plan(GenericMode mode, int cap, int cells, uint visitSeed, params NpcDefinition[] templates)
        {
            return PopulationPlanner.Plan(templates, Town(1234), mode, cap, cells, visitSeed, new FakeNames());
        }

        static string Describe(List<NpcInstance> plan)
        {
            List<string> parts = new List<string>();
            foreach (NpcInstance i in plan)
                parts.Add(i.Key + "|" + i.Name + "|" + i.Gender + "|" + i.PortraitName + "|" + i.FaceOutfit + "/" + i.FaceVariant + "|" + i.CellIndex);
            return string.Join("\n", parts.ToArray());
        }

        static List<string> Keys(List<NpcInstance> plan)
        {
            List<string> keys = new List<string>();
            foreach (NpcInstance i in plan)
                keys.Add(i.Key);
            return keys;
        }

        [Test]
        public void KeyFor_Format()
        {
            Assert.AreEqual("commoner@1234#2", PopulationPlanner.KeyFor("commoner", 1234, 2));
        }

        [Test]
        public void Keys_FollowTemplateMapAndIndex()
        {
            List<NpcInstance> plan = Plan(GenericMode.SamePeople, 12, 100, 0, Template("commoner", 3, 3));
            CollectionAssert.AreEqual(new[] { "commoner@1234#0", "commoner@1234#1", "commoner@1234#2" }, Keys(plan));
            foreach (NpcInstance i in plan)
            {
                Assert.AreEqual("commoner", i.Definition.Id);
                Assert.AreEqual(StableHash.Of(i.Key), i.Seed);
                Assert.IsTrue(i.Persistent);
                Assert.AreEqual("Breton", i.Race);
            }
        }

        [Test]
        public void SamePeople_IsRepeatable_AndIgnoresVisitSeed()
        {
            NpcDefinition t = Template("commoner", 1, 5);
            Assert.AreEqual(Describe(Plan(GenericMode.SamePeople, 12, 500, 1, t)), Describe(Plan(GenericMode.SamePeople, 12, 500, 999, t)));
        }

        [Test]
        public void RandomMode_DependsOnVisitSeed_AndIsNotPersistent()
        {
            NpcDefinition t = Template("commoner", 4, 4);
            List<NpcInstance> a = Plan(GenericMode.RandomEachVisit, 12, 500, 1, t);
            List<NpcInstance> b = Plan(GenericMode.RandomEachVisit, 12, 500, 2, t);
            Assert.AreNotEqual(Describe(a), Describe(b));
            Assert.AreEqual(Describe(a), Describe(Plan(GenericMode.RandomEachVisit, 12, 500, 1, t)));
            foreach (NpcInstance i in a)
                Assert.IsFalse(i.Persistent);
        }

        [Test]
        public void SamePlaceNameDifferentMap_DifferentKeysAndPeople()
        {
            NpcDefinition t = Template("commoner", 3, 3);
            List<NpcInstance> one = PopulationPlanner.Plan(new[] { t }, new TownInfo(1, "Daggerfall", "Daggerfall", "TownCity", "Breton"),
                GenericMode.SamePeople, 12, 500, 0, new FakeNames());
            List<NpcInstance> two = PopulationPlanner.Plan(new[] { t }, new TownInfo(2, "Sentinel", "Daggerfall", "TownCity", "Breton"),
                GenericMode.SamePeople, 12, 500, 0, new FakeNames());
            foreach (string key in Keys(one))
                CollectionAssert.DoesNotContain(Keys(two), key);
            Assert.AreNotEqual(one[0].Seed, two[0].Seed);
        }

        [Test]
        public void Cap_KeepsTemplateIdOrderThenIndex()
        {
            List<NpcInstance> plan = Plan(GenericMode.SamePeople, 4, 100, 0, Template("b", 3, 3), Template("a", 3, 3));
            CollectionAssert.AreEqual(new[] { "a@1234#0", "a@1234#1", "a@1234#2", "b@1234#0" }, Keys(plan));
        }

        [Test]
        public void CapZero_PlansNothing()
        {
            Assert.AreEqual(0, Plan(GenericMode.SamePeople, 0, 100, 0, Template("a", 3, 3)).Count);
        }

        [Test]
        public void CountZero_PlansNothing()
        {
            Assert.AreEqual(0, Plan(GenericMode.SamePeople, 12, 100, 0, Template("a", 0, 0)).Count);
        }

        [Test]
        public void CountRange_StaysWithinBounds()
        {
            List<NpcDefinition> templates = new List<NpcDefinition>();
            for (int k = 0; k < 50; k++)
                templates.Add(Template("t" + k.ToString("00"), 1, 3));
            List<NpcInstance> plan = PopulationPlanner.Plan(templates, Town(1234), GenericMode.SamePeople, 1000, 1000, 0, new FakeNames());
            Dictionary<string, int> perTemplate = new Dictionary<string, int>();
            foreach (NpcInstance i in plan)
            {
                int n;
                perTemplate.TryGetValue(i.Definition.Id, out n);
                perTemplate[i.Definition.Id] = n + 1;
            }
            HashSet<int> counts = new HashSet<int>();
            foreach (NpcDefinition t in templates)
            {
                int n;
                perTemplate.TryGetValue(t.Id, out n);
                Assert.IsTrue(n >= 1 && n <= 3, t.Id + " has " + n);
                counts.Add(n);
            }
            Assert.IsTrue(counts.Count > 1, "every template rolled the same count");
        }

        [Test]
        public void FixedPositions_TakePrecedence()
        {
            NpcDefinition t = Template("guard", 3, 3);
            SpawnPlace place = new SpawnPlace();
            place.Region = "Daggerfall";
            place.Place = "Daggerfall";
            place.Positions.Add(new float[] { 1, 2, 3 });
            place.Positions.Add(new float[] { 4, 5, 6 });
            t.Spawn.Places.Add(place);
            List<NpcInstance> plan = Plan(GenericMode.SamePeople, 12, 100, 0, t);
            Assert.AreEqual(3, plan.Count);
            Assert.IsTrue(plan[0].HasFixedPosition);
            Assert.AreEqual(1f, plan[0].X);
            Assert.AreEqual(-1, plan[0].CellIndex);
            Assert.IsTrue(plan[1].HasFixedPosition);
            Assert.AreEqual(6f, plan[1].Z);
            Assert.IsFalse(plan[2].HasFixedPosition);
            Assert.IsTrue(plan[2].CellIndex >= 0 && plan[2].CellIndex < 100);
        }

        [Test]
        public void NoCells_MarksInstancesUnplaced()
        {
            List<NpcInstance> plan = Plan(GenericMode.SamePeople, 12, 0, 0, Template("a", 3, 3));
            Assert.AreEqual(3, plan.Count);
            foreach (NpcInstance i in plan)
            {
                Assert.IsFalse(i.HasFixedPosition);
                Assert.AreEqual(-1, i.CellIndex);
            }
        }

        [Test]
        public void FewCells_ReusesCells()
        {
            List<NpcInstance> plan = Plan(GenericMode.SamePeople, 12, 1, 0, Template("a", 3, 3));
            Assert.AreEqual(3, plan.Count);
            foreach (NpcInstance i in plan)
                Assert.AreEqual(0, i.CellIndex);
        }

        [Test]
        public void ManyCells_AreDistinct()
        {
            List<NpcInstance> plan = Plan(GenericMode.SamePeople, 12, 10000, 0, Template("a", 8, 8));
            HashSet<int> cells = new HashSet<int>();
            foreach (NpcInstance i in plan)
                cells.Add(i.CellIndex);
            Assert.AreEqual(8, cells.Count);
        }

        [Test]
        public void NonMatchingTownAndUniqueDefinitions_AreIgnored()
        {
            NpcDefinition tavernOnly = Template("barkeep", 2, 2);
            tavernOnly.Spawn.LocationTypes.Clear();
            tavernOnly.Spawn.LocationTypes.Add("Tavern");
            NpcDefinition unique = Template("bram", 2, 2);
            unique.Kind = NpcKind.Unique;
            Assert.AreEqual(0, Plan(GenericMode.SamePeople, 12, 100, 0, tavernOnly, unique).Count);
        }

        [Test]
        public void Names_FixedNameListOrGenerator()
        {
            NpcDefinition fixedName = Template("guard", 2, 2);
            fixedName.Name = "Guard";
            foreach (NpcInstance i in Plan(GenericMode.SamePeople, 12, 100, 0, fixedName))
                Assert.AreEqual("Guard", i.Name);

            NpcDefinition listed = Template("listed", 4, 4);
            listed.Names.Add("Ann");
            listed.Names.Add("Bo");
            foreach (NpcInstance i in Plan(GenericMode.SamePeople, 12, 100, 0, listed))
                CollectionAssert.Contains(new[] { "Ann", "Bo" }, i.Name);

            NpcDefinition generated = Template("nord", 2, 2);
            generated.Race = "Nord";
            FakeNames names = new FakeNames();
            List<NpcInstance> plan = PopulationPlanner.Plan(new[] { generated }, Town(1234), GenericMode.SamePeople, 12, 100, 0, names);
            Assert.AreEqual(2, names.Calls.Count);
            Assert.AreEqual("Nord/" + plan[0].Gender, names.Calls[0]);
            StringAssert.StartsWith("Nord " + plan[0].Gender + " ", plan[0].Name);
        }

        [Test]
        public void PortraitsAndGender()
        {
            NpcDefinition t = Template("a", 4, 4);
            t.Portraits.Add("c1");
            t.Portraits.Add("c2");
            t.Gender = "Female";
            foreach (NpcInstance i in Plan(GenericMode.SamePeople, 12, 100, 0, t))
            {
                CollectionAssert.Contains(new[] { "c1", "c2" }, i.PortraitName);
                Assert.AreEqual("Female", i.Gender);
            }
            foreach (NpcInstance i in Plan(GenericMode.SamePeople, 12, 100, 0, Template("b", 2, 2)))
                Assert.IsNull(i.PortraitName);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `"Assets/Game/Mods/AdvancedNPCs/Tools~/run-tests.sh"`
Expected: `NO RESULTS FILE (compile error?)` with `error CS0246: The type or namespace name 'INameSource' could not be found`.

- [ ] **Step 3: Implement**

`Assets/Game/Mods/AdvancedNPCs/Scripts/Core/PopulationPlanner.cs`:

```csharp
using System.Collections.Generic;
using System.Globalization;

namespace AdvancedNPCs.Core
{
    /// <summary>The "GenericPeople" mod setting (spec §8).</summary>
    public enum GenericMode
    {
        SamePeople,
        RandomEachVisit,
    }

    /// <summary>Makes a person's name for a race and gender; the same seed must give the same name.</summary>
    public interface INameSource
    {
        string Generate(string race, string gender, uint seed);
    }

    /// <summary>Decides which generic people a town gets (spec §7.4). Pure and deterministic.</summary>
    public static class PopulationPlanner
    {
        const int MaxCellAttempts = 10;

        public static string KeyFor(string templateId, int mapId, int n)
        {
            return templateId + "@" + mapId.ToString(CultureInfo.InvariantCulture) + "#" + n.ToString(CultureInfo.InvariantCulture);
        }

        /// <param name="cellCount">Number of walkable cells in the town; instances get an index into that list.</param>
        /// <param name="visitSeed">Seed for "Random each visit"; ignored for "Same people every visit".</param>
        public static List<NpcInstance> Plan(IList<NpcDefinition> templates, TownInfo town, GenericMode mode, int cap,
            int cellCount, uint visitSeed, INameSource names)
        {
            List<NpcInstance> result = new List<NpcInstance>();
            if (cap <= 0 || templates == null)
                return result;

            bool same = mode == GenericMode.SamePeople;
            SeededRandom visit = new SeededRandom(visitSeed);
            HashSet<int> usedCells = new HashSet<int>();

            List<NpcDefinition> ordered = new List<NpcDefinition>(templates);
            ordered.Sort(delegate (NpcDefinition a, NpcDefinition b) { return string.CompareOrdinal(a.Id, b.Id); });

            foreach (NpcDefinition t in ordered)
            {
                if (t.Kind != NpcKind.Generic || t.Spawn == null)
                    continue;
                SpawnPlace place;
                if (!t.Spawn.Matches(town, out place))
                    continue;

                string townKey = t.Id + "@" + town.MapId.ToString(CultureInfo.InvariantCulture);
                SeededRandom countRandom = new SeededRandom(same ? StableHash.Of(townKey) : visit.NextUInt());
                int count = countRandom.Range(t.Spawn.CountMin, t.Spawn.CountMax);

                for (int n = 0; n < count && result.Count < cap; n++)
                {
                    NpcInstance i = new NpcInstance();
                    i.Key = KeyFor(t.Id, town.MapId, n);
                    i.Definition = t;
                    i.Persistent = same;
                    i.Seed = same ? StableHash.Of(i.Key) : visit.NextUInt();

                    // Fixed order: gender, name, portrait, face, position.
                    SeededRandom rng = new SeededRandom(i.Seed);
                    i.Gender = NpcInstance.PickGender(t.Gender, rng);
                    i.Race = string.IsNullOrEmpty(t.Race) ? town.DefaultRace : t.Race;
                    i.Name = PickName(t, i, rng, names);
                    i.PortraitName = t.Portraits.Count > 0 ? rng.Pick(t.Portraits) : null;
                    i.FaceOutfit = rng.Next(VanillaFaces.OutfitVariants);
                    i.FaceVariant = rng.Next(VanillaFaces.FaceVariants);

                    if (place != null && n < place.Positions.Count)
                    {
                        float[] p = place.Positions[n];
                        i.HasFixedPosition = true;
                        i.X = p[0];
                        i.Y = p[1];
                        i.Z = p[2];
                    }
                    else
                    {
                        i.CellIndex = PickCell(rng, cellCount, usedCells);
                    }
                    result.Add(i);
                }
            }
            return result;
        }

        static string PickName(NpcDefinition t, NpcInstance i, SeededRandom rng, INameSource names)
        {
            if (t.Names.Count > 0)
                return rng.Pick(t.Names);
            if (!string.IsNullOrEmpty(t.Name))
                return t.Name;
            return names.Generate(i.Race, i.Gender, rng.NextUInt());
        }

        /// <summary>A cell not used yet if one turns up within a few tries; -1 when the town has no cells.</summary>
        static int PickCell(SeededRandom rng, int cellCount, HashSet<int> used)
        {
            if (cellCount <= 0)
                return -1;
            int cell = rng.Next(cellCount);
            for (int attempt = 1; attempt < MaxCellAttempts && used.Contains(cell); attempt++)
                cell = rng.Next(cellCount);
            used.Add(cell);
            return cell;
        }
    }
}
```

In `AdvancedNPCs.dfmod.json`, add after `"Assets/Game/Mods/AdvancedNPCs/Scripts/Core/NpcState.cs",`:

```json
        "Assets/Game/Mods/AdvancedNPCs/Scripts/Core/PopulationPlanner.cs",
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `"Assets/Game/Mods/AdvancedNPCs/Tools~/run-tests.sh"` — Expected: `result="Passed"`, `failed="0"`.
Run: `"Assets/Game/Mods/AdvancedNPCs/Tools~/runtime-check.sh"` — Expected: exit 0.
Run the manifest check — Expected: no output.

- [ ] **Step 5: Commit**

```bash
git add Assets/Game/Mods/AdvancedNPCs/Scripts/Core/PopulationPlanner.cs Assets/Game/Mods/AdvancedNPCs/Editor/Tests/PopulationPlannerTests.cs Assets/Game/Mods/AdvancedNPCs/AdvancedNPCs.dfmod.json
git commit -m "feat: plan generic townsfolk per town (seeded, capped, repeatable)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 10: Mod settings

**Files:**
- Create: `Assets/Game/Mods/AdvancedNPCs/Editor/ModSettingsWriter.cs`
- Modify: `Assets/Game/Mods/AdvancedNPCs/Editor/AdvancedNpcsModBuilder.cs` (`Run`)
- Create: `Assets/Game/Mods/AdvancedNPCs/Scripts/Runtime/ModConfig.cs`
- Modify: `Assets/Game/Mods/AdvancedNPCs/Scripts/Runtime/AdvancedNpcsMod.cs` (`Config`, `Init`, `Awake`)
- Modify: `Assets/Game/Mods/AdvancedNPCs/Scripts/Runtime/SelfTest.cs` (one check)
- Modify: `Assets/Game/Mods/AdvancedNPCs/AdvancedNPCs.dfmod.json`
- Generated (committed): `Assets/Game/Mods/AdvancedNPCs/modsettings.json`

**Interfaces:**
- Consumes: `GenericMode` (Task 9); DFU `ModSettingsData.Make(string path)` and `ModSettingsData.Save(string path)` (editor-only, `ModSettingsData.cs:258-266, 436-446`), `Section`, `MultipleChoiceKey`, `SliderIntKey`, `Mod.HasSettings`, `Mod.LoadSettingsCallback`, `Mod.LoadSettings()`, `ModSettings.GetInt(section, key)`.
- Produces: `ModConfig { GenericMode Mode = SamePeople; int MaxGenericPerTown = 12; bool FromSettings; void Attach(Mod mod); void Apply(ModSettings settings, ModSettingsChange change) }`; `AdvancedNpcsMod.Config : ModConfig`; `ModSettingsWriter.Write()` (editor).

The settings file is generated with DFU's own editor-side serializer (the same `Save` that DFU's Mod Settings editor window uses), so its FullSerializer format is exactly what DFU reads.

- [ ] **Step 1: Create the settings writer and call it from the builder**

`Assets/Game/Mods/AdvancedNPCs/Editor/ModSettingsWriter.cs`:

```csharp
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;

namespace AdvancedNPCs.EditorTools
{
    /// <summary>Writes modsettings.json (spec §8) with DFU's own serializer, so DFU shows a settings window for the mod.</summary>
    public static class ModSettingsWriter
    {
        public const string SettingsPath = "Assets/Game/Mods/AdvancedNPCs/modsettings.json";

        public static void Write()
        {
            // Make(path) of a file that does not exist returns empty settings.
            ModSettingsData data = ModSettingsData.Make("Assets/Game/Mods/AdvancedNPCs/__no_settings__.json");
            data.Version = "1.0";

            Section section = new Section();
            section.Name = "Population";
            section.Description = "Generic Advanced NPCs: townsfolk made from generic templates.";

            MultipleChoiceKey people = new MultipleChoiceKey();
            people.Name = "GenericPeople";
            people.Description = "Same people every visit: each town keeps its generic NPCs and what happens to them is saved. " +
                                 "Random each visit: re-rolled whenever a town loads; nothing about them is saved.";
            people.Options.Add("Same people every visit");
            people.Options.Add("Random each visit");
            people.Value = 0;
            section.Keys.Add(people);

            SliderIntKey max = new SliderIntKey();
            max.Name = "MaxGenericPerTown";
            max.Description = "Most generic NPCs in one town (0 turns them off).";
            max.Min = 0;
            max.Max = 30;
            max.Value = 12;
            section.Keys.Add(max);

            data.Sections.Add(section);
            data.Save(SettingsPath);
        }
    }
}
```

In `AdvancedNpcsModBuilder.Run`, make this the first statement:

```csharp
            ModSettingsWriter.Write();
```

In `AdvancedNPCs.dfmod.json`, add as the first entry of `Files`:

```json
        "Assets/Game/Mods/AdvancedNPCs/modsettings.json",
```

- [ ] **Step 2: Create `ModConfig.cs`**

```csharp
using UnityEngine;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using AdvancedNPCs.Core;
using DfuModSettings = DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings.ModSettings;

namespace AdvancedNPCs
{
    /// <summary>The mod's settings (spec §8). Changes apply the next time a town is built.</summary>
    public class ModConfig
    {
        public const string Section = "Population";

        public GenericMode Mode = GenericMode.SamePeople;
        public int MaxGenericPerTown = 12;
        /// <summary>True once values were read from the mod's settings (false: defaults).</summary>
        public bool FromSettings;

        public void Attach(Mod mod)
        {
            if (mod == null || !mod.HasSettings)
            {
                AdvancedNpcsMod.Log("No mod settings found; using defaults (same people every visit, at most 12 per town).");
                return;
            }
            mod.LoadSettingsCallback = Apply;
            mod.LoadSettings();
        }

        public void Apply(DfuModSettings settings, ModSettingsChange change)
        {
            Mode = settings.GetInt(Section, "GenericPeople") == 1 ? GenericMode.RandomEachVisit : GenericMode.SamePeople;
            MaxGenericPerTown = Mathf.Clamp(settings.GetInt(Section, "MaxGenericPerTown"), 0, 30);
            FromSettings = true;
            AdvancedNpcsMod.Log("Settings: generic people " + Mode + ", at most " + MaxGenericPerTown + " per town.");
        }
    }
}
```

In `AdvancedNpcsMod`:
- below `public PortraitLibrary Portraits { get; private set; }` add `public ModConfig Config { get; private set; }`;
- in `Awake`, as its first statement, add `Config = new ModConfig();`;
- in `Init`, directly after `Instance = go.AddComponent<AdvancedNpcsMod>();`, add `Instance.Config.Attach(mod);`.

In `AdvancedNPCs.dfmod.json`, add after `"Assets/Game/Mods/AdvancedNPCs/Scripts/Runtime/AnpcFiles.cs",`:

```json
        "Assets/Game/Mods/AdvancedNPCs/Scripts/Runtime/ModConfig.cs",
```

- [ ] **Step 3: Self-test check**

In `SelfTest.Steps`, after the last statement (the `Check("save data survives a serialize/deserialize round trip", …);` call), add:

```csharp
            Check("mod settings are read", mod.Config.FromSettings, "no modsettings.json in the mod, or DFU could not read it");
```

- [ ] **Step 4: Verify**

Run `compile-check.sh`, `runtime-check.sh`, the manifest check (all clean). Close editor and game, then:

```bash
"Assets/Game/Mods/AdvancedNPCs/Tools~/build-mod.sh"
git restore Assets/AddressableAssetsData/
grep -c "GenericPeople" Assets/Game/Mods/AdvancedNPCs/modsettings.json
"Assets/Game/Mods/AdvancedNPCs/Tools~/selftest.sh"
grep "Settings:" "$USERPROFILE/AppData/LocalLow/Daggerfall Workshop/Daggerfall Unity/Player.log"
```

Expected: `modsettings.json` exists and the `grep -c` prints at least `1`; `SELFTEST DONE 25/25 passed`; log line `Settings: generic people SamePeople, at most 12 per town.`. If the build log (`Builds/advancednpcs-build.log`) shows an error from `ModSettingsWriter`, fix it before going on (use superpowers:systematic-debugging).

Then ask the user to open DFU's mod list, select Advanced NPCs → Settings, and confirm the **Population** section shows both options; changing them and restarting must change the `Settings:` log line.

- [ ] **Step 5: Commit**

```bash
git status --short Assets/Game/Mods/AdvancedNPCs
git add -A Assets/Game/Mods/AdvancedNPCs
git commit -m "feat: add Population mod settings (generic people mode, max per town)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 11: Populate towns with generic ANPCs

**Files:**
- Create: `Assets/Game/Mods/AdvancedNPCs/Scripts/Runtime/DfuNameSource.cs`
- Modify: `Assets/Game/Mods/AdvancedNPCs/Scripts/Runtime/NpcSpawner.cs` (generic spawning)
- Modify: `Assets/Game/Mods/AdvancedNPCs/Scripts/Runtime/NpcBrain.cs` (`CurrentHealthFraction`)
- Modify: `Assets/Game/Mods/AdvancedNPCs/Scripts/Runtime/SelfTest.cs` (generic checks)
- Create: `Assets/Game/Mods/AdvancedNPCs/Examples/ANPCs/commoner/npc.json`
- Modify: `Assets/Game/Mods/AdvancedNPCs/README.md`, `Assets/Game/Mods/AdvancedNPCs/AdvancedNPCs.dfmod.json`

**Interfaces:**
- Consumes: `PopulationPlanner.Plan`, `GenericMode`, `INameSource` (Task 9); `TownInfo`, `GenericSpawn`, `SpawnPlace` (Task 8); `ModConfig.Mode/MaxGenericPerTown` (Task 10); `NpcSpawner.SpawnChecked`, `NpcSpawner.DefaultRace` (Task 6); DFU `CityNavigation.NavGridWidth/NavGridHeight/GetNavGridWeightLocal(int, int)/NavGridToWorldPosition(DFPosition)/WorldToScenePosition(DFPosition, bool)`, `NameHelper.FullName(BankTypes, Genders)`, `DFRandom.SaveSeed/srand(uint)/RestoreSeed`.
- Produces: `DfuNameSource : INameSource`; `NpcSpawner.SpawnGenerics(DaggerfallLocation location, IList<NpcDefinition> templates, GenericMode mode, int cap) -> List<NpcBrain>`; `NpcBrain.CurrentHealthFraction`.

- [ ] **Step 1: Create `DfuNameSource.cs`**

```csharp
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Utility;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>DFU's own person names, made repeatable by seeding DFU's random generator for one call.</summary>
    public class DfuNameSource : INameSource
    {
        public string Generate(string race, string gender, uint seed)
        {
            NameHelper.BankTypes bank = NameHelper.BankTypes.Breton;
            if (race == "Redguard")
                bank = NameHelper.BankTypes.Redguard;
            else if (race == "Nord")
                bank = NameHelper.BankTypes.Nord;

            DFRandom.SaveSeed();
            try
            {
                DFRandom.srand(seed);
                return DaggerfallUnity.Instance.NameHelper.FullName(bank, gender == "Female" ? Genders.Female : Genders.Male);
            }
            finally
            {
                DFRandom.RestoreSeed();
            }
        }
    }
}
```

- [ ] **Step 2: Spawn generics in `NpcSpawner`**

Add to the `using` block:

```csharp
using System.Collections;
using DaggerfallConnect.Utility;
using DaggerfallWorkshop.Game.Utility;
```

Add fields below `knownLocations`:

```csharp
        readonly INameSource names = new DfuNameSource();
        readonly HashSet<int> townsWithoutCells = new HashSet<int>();
```

At the end of `SpawnFor` (after the unique loop), add:

```csharp
            if (owner.Catalog.Generics.Count > 0)
                owner.StartCoroutine(SpawnGenericsLater(location));
```

Add these members below `SpawnTest`:

```csharp
        IEnumerator SpawnGenericsLater(DaggerfallLocation location)
        {
            yield return null; // let the town finish building before reading its navigation grid
            if (location == null || !location.gameObject.activeSelf)
                yield break;
            SpawnGenerics(location, owner.Catalog.Generics, owner.Config.Mode, owner.Config.MaxGenericPerTown);
        }

        /// <summary>Plans and spawns a town's generic ANPCs (spec §7.4). Returns the ANPCs spawned now.</summary>
        public List<NpcBrain> SpawnGenerics(DaggerfallLocation location, IList<NpcDefinition> templates, GenericMode mode, int cap)
        {
            List<NpcBrain> spawned = new List<NpcBrain>();
            if (location == null || templates == null || templates.Count == 0 || cap <= 0)
                return spawned;

            List<DFPosition> cells = WalkableCells(location);
            TownInfo town = new TownInfo(location.Summary.MapID, location.Summary.RegionName, location.Summary.LocationName,
                location.Summary.LocationType.ToString(), DefaultRace());
            uint visitSeed = (uint)UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            List<NpcInstance> plan = PopulationPlanner.Plan(templates, town, mode, cap, cells.Count, visitSeed, names);
            if (plan.Count == 0)
                return spawned;

            foreach (NpcInstance instance in plan)
            {
                if (!instance.HasFixedPosition)
                {
                    if (instance.CellIndex < 0)
                    {
                        if (townsWithoutCells.Add(town.MapId))
                            AdvancedNpcsMod.Log(town.Place + ": no walkable cells; generic ANPCs without fixed positions are not spawned there.");
                        continue;
                    }
                    Vector3 local = CellToLocal(location, cells[instance.CellIndex]);
                    instance.X = local.x;
                    instance.Y = local.y;
                    instance.Z = local.z;
                }
                NpcState state = instance.Persistent ? owner.States.GetOrCreate(instance.Key) : new NpcState();
                NpcBrain brain = SpawnChecked(instance, state, location);
                if (brain != null)
                    spawned.Add(brain);
            }
            AdvancedNpcsMod.Log(town.Place + ": " + spawned.Count + " generic ANPC(s) spawned (" + plan.Count + " planned, " +
                cells.Count + " walkable cells, " + mode + ").");
            return spawned;
        }

        /// <summary>The town's street cells (navigation weight &gt; 0), in grid order.</summary>
        static List<DFPosition> WalkableCells(DaggerfallLocation location)
        {
            List<DFPosition> cells = new List<DFPosition>();
            CityNavigation nav = location.GetComponent<CityNavigation>();
            if (nav == null)
                return cells;
            for (int y = 0; y < nav.NavGridHeight; y++)
            {
                for (int x = 0; x < nav.NavGridWidth; x++)
                {
                    if (nav.GetNavGridWeightLocal(x, y) > 0)
                        cells.Add(new DFPosition(x, y));
                }
            }
            return cells;
        }

        static Vector3 CellToLocal(DaggerfallLocation location, DFPosition cell)
        {
            CityNavigation nav = location.GetComponent<CityNavigation>();
            Vector3 scene = nav.WorldToScenePosition(nav.NavGridToWorldPosition(cell));
            return location.transform.InverseTransformPoint(scene);
        }
```

- [ ] **Step 3: `NpcBrain.CurrentHealthFraction`**

Add below the `Instance` property in `NpcBrain.cs`:

```csharp
        /// <summary>Health as a fraction of max (the saved fraction before the ANPC has started).</summary>
        public float CurrentHealthFraction
        {
            get { return entityBehaviour != null && entityBehaviour.Entity != null ? HealthFraction() : state.healthFraction; }
        }
```

- [ ] **Step 4: Self-test checks for generic townsfolk**

In `SelfTest.Steps`, after the `Check("mod settings are read", …);` line from Task 10, add:

```csharp
            // Generic townsfolk (spec §7.4, §8). The test template only matches this town.
            List<NpcDefinition> templates = new List<NpcDefinition>();
            templates.Add(GenericTemplate(location, 3));

            List<NpcBrain> first = SpawnGenerics(location, templates, GenericMode.SamePeople, 12);
            yield return Settle;
            string firstLayout = Layout(first);
            Check("generic template spawns its count", first.Count == 3, firstLayout);
            DiscardAll(first);
            yield return Settle;

            List<NpcBrain> second = SpawnGenerics(location, templates, GenericMode.SamePeople, 12);
            yield return Settle;
            Check("same-people mode brings back the same people", Layout(second) == firstLayout, firstLayout + " vs " + Layout(second));

            string hurtKey = second.Count > 0 ? second[0].Id : "?";
            if (second.Count > 0)
                SetHealthFraction(second[0], 0.5f);
            yield return Settle;
            DiscardAll(second);
            yield return Settle;
            NpcSaveData saved = (NpcSaveData)new NpcSaveDataInterface(mod.States, delegate { }).GetSaveData();
            NpcState savedState;
            Check("a generic ANPC's damage is saved under its key",
                saved.States.TryGetValue(hurtKey, out savedState) && Mathf.Abs(savedState.healthFraction - 0.5f) < 0.05f, hurtKey);

            List<NpcBrain> third = SpawnGenerics(location, templates, GenericMode.SamePeople, 12);
            yield return Settle;
            NpcBrain hurt = NpcBrain.Find(hurtKey);
            Check("a damaged generic ANPC comes back damaged",
                hurt != null && Mathf.Abs(hurt.CurrentHealthFraction - 0.5f) < 0.05f, hurt == null ? "missing" : hurt.Status);
            DiscardAll(third);
            yield return Settle;

            foreach (string key in ids)
            {
                if (key.StartsWith("selftest_generic@", StringComparison.Ordinal))
                    mod.States.Remove(key);
            }
            List<NpcBrain> random1 = SpawnGenerics(location, templates, GenericMode.RandomEachVisit, 12);
            yield return Settle;
            string randomLayout = Layout(random1);
            bool randomInTable = false;
            foreach (NpcBrain b in random1)
                randomInTable |= mod.States.Has(b.Id);
            DiscardAll(random1);
            yield return Settle;
            List<NpcBrain> random2 = SpawnGenerics(location, templates, GenericMode.RandomEachVisit, 12);
            yield return Settle;
            Check("random mode re-rolls the people", random2.Count == 3 && Layout(random2) != randomLayout, randomLayout + " vs " + Layout(random2));
            Check("random mode saves nothing about them", !randomInTable, randomLayout);
            DiscardAll(random2);
            yield return Settle;

            List<NpcBrain> none = SpawnGenerics(location, templates, GenericMode.SamePeople, 0);
            Check("MaxGenericPerTown 0 spawns no generic ANPCs", none.Count == 0, Layout(none));
```

Add these helpers next to `Make`:

```csharp
        /// <summary>Spawns generic test ANPCs; their keys go into ids so the end of the run removes them and their state.</summary>
        List<NpcBrain> SpawnGenerics(DaggerfallLocation location, List<NpcDefinition> templates, GenericMode mode, int cap)
        {
            List<NpcBrain> brains = spawner.SpawnGenerics(location, templates, mode, cap);
            foreach (NpcBrain b in brains)
            {
                if (!ids.Contains(b.Id))
                    ids.Add(b.Id);
            }
            return brains;
        }

        static void DiscardAll(List<NpcBrain> brains)
        {
            foreach (NpcBrain b in brains)
                NpcBrain.Discard(b);
        }

        /// <summary>Keys, names and rounded positions, sorted: equal strings mean the same people in the same places.</summary>
        static string Layout(List<NpcBrain> brains)
        {
            List<string> parts = new List<string>();
            foreach (NpcBrain b in brains)
            {
                if (b == null)
                    continue;
                Vector3 p = b.transform.localPosition;
                parts.Add(b.Id + "=" + b.DisplayName + "@" + Mathf.Round(p.x) + "," + Mathf.Round(p.z));
            }
            parts.Sort(StringComparer.Ordinal);
            return string.Join("; ", parts.ToArray());
        }

        static NpcDefinition GenericTemplate(DaggerfallLocation location, int count)
        {
            NpcDefinition d = new NpcDefinition();
            d.Id = "selftest_generic";
            d.Folder = d.Id;
            d.Kind = NpcKind.Generic;
            d.Name = "";
            d.Gender = "";
            d.BaseClass = "Spellsword";
            d.Bravery = Bravery.Normal;
            d.FleeHealthPercent = 25;
            d.CalmDownMinHours = 6f;
            d.CalmDownMaxHours = 48f;
            d.CrimeOnAttack = true;
            d.WanderRadius = 0f;
            d.SourceFile = "(selftest)";
            d.Spawn = new GenericSpawn();
            d.Spawn.CountMin = count;
            d.Spawn.CountMax = count;
            SpawnPlace here = new SpawnPlace();
            here.Region = location.Summary.RegionName;
            here.Place = location.Summary.LocationName;
            d.Spawn.Places.Add(here);
            return d;
        }
```

(`SelfTest.cs` already imports `System` and `System.Collections.Generic`.)

- [ ] **Step 5: Ship the generic example, document, bump the version**

Create `Assets/Game/Mods/AdvancedNPCs/Examples/ANPCs/commoner/npc.json`:

```json
{
  "kind": "generic",
  "spawn": {
    "locationTypes": ["TownCity", "TownHamlet", "TownVillage"],
    "count": [1, 3]
  },
  "baseClass": "Bard",
  "bravery": "Coward",
  "wanderRadius": 12
}
```

In `README.md`:

1. After the `## Fields (unique ANPCs)` table, add:

````markdown
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
| `portrait` / `portraits` | vanilla face | one portrait, or a list to pick from |
| `spawn.locationTypes` | `TownCity`, `TownHamlet`, `TownVillage` | location types the template appears in |
| `spawn.places` | all matching towns | list of `{ "region", "place" }`; only those towns. Add `"positions": [[x, y, z], …]` (from `anpc_pos`) for fixed spots |
| `spawn.count` | `[1, 3]` | instances per town, `0 <= min <= max <= 20` |

All behaviour fields of unique ANPCs (`race`, `baseClass`, `gender`, `bravery`, …) work the same. `location`,
`position` and `dialogue.json` are for unique ANPCs only. Instances are named `<template>@<map id>#<n>` in
`anpc_list` and in save data.

## Settings

In DFU's mod list, select Advanced NPCs → Settings → **Population**:

- **Generic people** — *Same people every visit* (default): each town keeps the same generic people, and what happens to them (killed, angry, hurt) is saved. *Random each visit*: re-rolled whenever the town loads; nothing about them is saved.
- **Max generic per town** — 0 to 30 (default 12); 0 turns generic ANPCs off.

Changes apply the next time a town loads.
````

2. In `## Console commands`, change the `anpc_list` line to `` - `anpc_list` — spawned ANPCs (unique and generic) with key, name, distance, direction, height and state. `` and the `anpc_summon` line to `` - `anpc_summon <id or key>` — moves a spawned ANPC in front of you (testing only; not saved). ``
3. In `## Examples`, append: `` `Examples/ANPCs/commoner` is a generic template that adds one to three cowardly commoners to every city, town and village. ``

In `AdvancedNPCs.dfmod.json`, change `"ModVersion": "0.1.0"` to `"ModVersion": "0.2.0"`, set

```json
    "ModDescription": "Persistent, data-defined NPCs: unique people and generic townsfolk you can talk to, who fight or flee by bravery when attacked, report crimes and calm down over in-game time. One folder per NPC in StreamingAssets/ANPCs.",
```

and add after `"Assets/Game/Mods/AdvancedNPCs/Scripts/Runtime/AnpcFiles.cs",`:

```json
        "Assets/Game/Mods/AdvancedNPCs/Scripts/Runtime/DfuNameSource.cs",
```

- [ ] **Step 6: Verify (full loop)**

```bash
"Assets/Game/Mods/AdvancedNPCs/Tools~/run-tests.sh"
"Assets/Game/Mods/AdvancedNPCs/Tools~/compile-check.sh"
"Assets/Game/Mods/AdvancedNPCs/Tools~/runtime-check.sh"
```

plus the manifest check. Close editor and game, then:

```bash
"Assets/Game/Mods/AdvancedNPCs/Tools~/build-mod.sh"
git restore Assets/AddressableAssetsData/
"Assets/Game/Mods/AdvancedNPCs/Tools~/selftest.sh"
grep -E "generic ANPC\(s\) spawned|commoner@" "$USERPROFILE/AppData/LocalLow/Daggerfall Workshop/Daggerfall Unity/Player.log" | head -20
```

Expected:
- Core tests `result="Passed"`; compile checks clean; manifest check silent.
- `SELFTEST DONE 32/32 passed`.
- Player.log has `Daggerfall: N generic ANPC(s) spawned (N planned, M walkable cells, SamePeople).` with `M > 0` and `1 <= N <= 3`, and `commoner@<mapId>#0 (<a generated name>): spawned.` lines. If `M` is 0 for Daggerfall, the navigation grid is not ready one frame after the town event: use superpowers:systematic-debugging (log `NavGridWidth/Height` and the cell count per frame) and wait for the grid instead of one frame.

- [ ] **Step 7: Commit**

```bash
git status --short Assets/Game/Mods/AdvancedNPCs
git add -A Assets/Game/Mods/AdvancedNPCs
git commit -m "feat: populate towns with generic ANPCs (same people or random per visit)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 8: Phase B hand-off to the user (manual check)**

Ask the user to check in game (DFU_testing):
1. Walking around Daggerfall city, a few commoners with DFU names wander the streets besides vanilla citizens; `anpc_list` shows them as `commoner@…#n`.
2. Talking to one opens the talk window with its name; hitting one makes it flee (Coward) and raises an assault.
3. Fast travel away and back: the same commoners (names, places) return; one that was killed stays dead.
4. Switch the setting to *Random each visit*, restart, reload: different commoners each time the town loads.
5. Frame rate in Daggerfall city with 12 generic ANPCs is acceptable (spec §17 risk). If not, report the numbers; lowering the default cap is a one-line change.

Phase C (extra dialogue topics) follows with its own spike and plan (spec §9.3).


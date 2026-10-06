# Advanced NPCs 1a Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A Daggerfall Unity mod that spawns persistent, data-defined NPCs in towns which stay calm, turn hostile only toward their attacker, fight or flee by bravery, calm down after in-game hours, raise crimes, and persist through save/load.

**Architecture:** Pure decision and data code lives in a Unity assembly `AdvancedNPCs.Core` (no DFU references, unit-tested with NUnit). Runtime glue (mod entry, spawner, brain, mover, save interface) uses DFU's enemy stack and lives in `Assembly-CSharp`. At game runtime DFU compiles all mod `.cs` sources from the `.dfmod` together, so the assembly split only matters inside the Unity Editor.

**Tech Stack:** Unity 2019.4.41f2, C# (conservative subset, see constraints), Daggerfall Unity 1.9.2 mod API, Unity Test Framework 1.1.31 (NUnit), `UnityEngine.JsonUtility`, FullSerializer (DFU save data).

**Spec:** `docs/superpowers/specs/2026-10-06-advanced-npcs-1a-design.md`

**Deviation from spec §11 layout (intentional):** the spec's `Data/` and `Logic/` folders are merged into `Scripts/Core/` (one assembly definition), and `NpcStateStore` is split into a pure `NpcStateTable` (Core) plus `NpcSaveDataInterface` (Runtime). This removes the spec §13 risk "tests can't reference mod code".

## Global Constraints

- Unity Editor: **2019.4.41f2 only** — `C:\Program Files\Unity\Hub\Editor\2019.4.41f2\Editor\Unity.exe`. Never open the project with any Unity 6 editor.
- Target: Daggerfall Unity **1.9.2** test install at `F:\_Projects\Dagerfall\DFU_testing`.
- Branch: `feature/advanced-npcs-1a`. Never commit to `master`. Do not push unless the user asks.
- C# subset (DFU compiles mod source at runtime with an older compiler): **no** string interpolation `$""`, **no** `?.` / `??=`, **no** expression-bodied members `=>`, **no** `nameof`, **no** `out var`, **no** tuples, **no** pattern matching. `var`, generics, LINQ, lambdas are fine.
- All log lines start with `[AdvancedNPCs] `.
- Definition folder: `<DFU>/DaggerfallUnity_Data/StreamingAssets/AdvancedNPCs/*.json`.
- Definition defaults: `baseClass` `Spellsword`, `gender` random, `bravery` `Normal`, `fleeHealthPercent` `25`, `calmDownHours` `[6, 48]`, `crimeOnAttack` `true`, `wanderRadius` `8`.
- Flee "safe" distance: `30` units. In-game time unit: DFU seconds (`DaggerfallDateTime.ToSeconds()`), 3600 per hour.
- Unity generates `.meta` files for every new file/folder under `Assets/`. Commit them together with the file.
- Do not modify any file outside `Assets/Game/Mods/AdvancedNPCs/` and `docs/`.

### Test command (used by Tasks 1–8)

The Unity Editor must be **closed** (batch mode cannot open a project that is already open). First run imports the whole project and can take 30–60+ minutes; later runs take a few minutes.

```bash
"/c/Program Files/Unity/Hub/Editor/2019.4.41f2/Editor/Unity.exe" -batchmode -nographics -projectPath "F:/_Projects/Dagerfall/daggerfall-unity" -runTests -testPlatform EditMode -testFilter "AdvancedNPCs" -testResults "F:/_Projects/Dagerfall/test-results.xml" -logFile "F:/_Projects/Dagerfall/unity-test.log"; grep -o '<test-run[^>]*>' "F:/_Projects/Dagerfall/test-results.xml"
```

Read the `result`, `passed`, `failed` attributes. If `test-results.xml` is missing, the run failed to compile: search `unity-test.log` for `error CS`.

## Review Focus

1. **Save then load in the same town** — the NPC must appear exactly once with its saved state (no duplicate, no reset). Pinned by `NpcStateTableTests.Restore_ReplacesContentAndKeepsUnknownIds` (Task 5) and manual check 7 in Task 8.
2. **One-hit kill of a calm NPC by the player** — must count as murder even though the NPC never became hostile. Pinned by `HostilityRulesTests.KilledByPlayer_*` (Task 4) and manual check 8 in Task 8.
3. **PC with a comma decimal locale (e.g. German)** — `anpc_pos` must print `12.5`, not `12,5`, or the pasted JSON breaks. Pinned by `PositionFormatTests.UsesDotUnderCommaCulture` (Task 2).
4. **Region/place typed with different case or stray spaces** (`"daggerfall "`) — must still match. Pinned by `DefinitionCatalogTests.ForLocation_IgnoresCaseAndSpaces` (Task 3).
5. **Starting a new game after playing a save** — NPC state from the old save must not leak (dead NPC must be alive again). Pinned by `NpcStateTableTests.Clear_ResetsEverything` (Task 5) and manual check 9 in Task 8.

---

## File Structure

```
Assets/Game/Mods/AdvancedNPCs/
  Scripts/
    Core/                                 (assembly AdvancedNPCs.Core — pure, unit-tested)
      AdvancedNPCs.Core.asmdef
      NpcDefinition.cs        Bravery enum, JSON shape, validated definition
      HumanClasses.cs         allowed baseClass names (mirror of MobileTypes 128-145)
      DefinitionParser.cs     one file → definition or error message
      DefinitionCatalog.cs    many files → id map, duplicates, location lookup
      PositionFormat.cs       culture-safe JSON snippet for anpc_pos
      HostilityRules.cs       bravery decision, calm deadline, hostile-flip and killer classification
      NpcState.cs             per-NPC saved state + NpcStateTable
    Runtime/                              (Assembly-CSharp — DFU glue, tested in game)
      AdvancedNpcsMod.cs      [Invoke] entry, folder loading, console command, new-game reset
      NpcSaveDataInterface.cs IHasModSaveData + versioned save class
      NpcSpawner.cs           location event → CreateEnemy → NpcBrain
      NpcMover.cs             wander / flee movement with gravity and animation
      NpcBrain.cs             state machine, hostility guard, crime, calm-down, death
  Editor/
    Tests/
      AdvancedNPCs.Tests.asmdef
      HarnessTests.cs
      DefinitionParserTests.cs
      DefinitionCatalogTests.cs
      PositionFormatTests.cs
      HostilityRulesTests.cs
      NpcStateTableTests.cs
  Examples/
    bram.json  coward_cora.json  brave_bors.json
  README.md                author guide
```

---

### Task 1: Test harness

Proves the project imports in 2019.4.41f2 and that an NUnit test in our own test assembly can see an `AdvancedNPCs.Core` type.

**Files:**
- Create: `Assets/Game/Mods/AdvancedNPCs/Scripts/Core/AdvancedNPCs.Core.asmdef`
- Create: `Assets/Game/Mods/AdvancedNPCs/Scripts/Core/HostilityRules.cs` (constant only; filled in Task 4)
- Create: `Assets/Game/Mods/AdvancedNPCs/Editor/Tests/AdvancedNPCs.Tests.asmdef`
- Test: `Assets/Game/Mods/AdvancedNPCs/Editor/Tests/HarnessTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: assembly `AdvancedNPCs.Core` (auto-referenced by Assembly-CSharp), test assembly `AdvancedNPCs.Tests`, namespace `AdvancedNPCs.Core`, `HostilityRules.SecondsPerHour` (`ulong`, 3600).

- [ ] **Step 1: Confirm the editor is closed and on the right branch**

```bash
cd "F:/_Projects/Dagerfall/daggerfall-unity" && git branch --show-current && (tasklist | grep -i "Unity.exe" || echo "no Unity running")
```
Expected: `feature/advanced-npcs-1a` and `no Unity running`. If Unity is running, ask the user to close it.

- [ ] **Step 2: Create the Core assembly definition**

`Assets/Game/Mods/AdvancedNPCs/Scripts/Core/AdvancedNPCs.Core.asmdef`:
```json
{
    "name": "AdvancedNPCs.Core",
    "references": [],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 3: Create the test assembly definition**

`Assets/Game/Mods/AdvancedNPCs/Editor/Tests/AdvancedNPCs.Tests.asmdef`:
```json
{
    "name": "AdvancedNPCs.Tests",
    "references": [
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner",
        "AdvancedNPCs.Core"
    ],
    "includePlatforms": ["Editor"],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": ["nunit.framework.dll"],
    "autoReferenced": false,
    "defineConstraints": ["UNITY_INCLUDE_TESTS"],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 4: Write the failing test**

`Assets/Game/Mods/AdvancedNPCs/Editor/Tests/HarnessTests.cs`:
```csharp
using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class HarnessTests
    {
        [Test]
        public void CoreAssemblyIsVisible()
        {
            Assert.AreEqual(3600UL, HostilityRules.SecondsPerHour);
        }
    }
}
```

- [ ] **Step 5: Run the test command — verify it fails to compile**

Run the Test command. Expected: no `test-results.xml`; `unity-test.log` contains `error CS0103: The name 'HostilityRules' does not exist` (or CS0246). This proves the test assembly compiles against Core and the failure is the missing type. (First run includes the full project import.)

- [ ] **Step 6: Minimal implementation**

`Assets/Game/Mods/AdvancedNPCs/Scripts/Core/HostilityRules.cs`:
```csharp
namespace AdvancedNPCs.Core
{
    /// <summary>Pure decision rules for Advanced NPC hostility. Filled in by Task 4.</summary>
    public static class HostilityRules
    {
        public const ulong SecondsPerHour = 3600;
    }
}
```

- [ ] **Step 7: Run the test command — verify it passes**

Expected: `<test-run ... result="Passed" total="1" passed="1" failed="0" ...>`.

If instead the log shows the test assembly cannot find `nunit.framework.dll`, stop and report to the user with the exact log line — do not work around it.

- [ ] **Step 8: Commit (include generated .meta files)**

```bash
cd "F:/_Projects/Dagerfall/daggerfall-unity" && git add Assets/Game/Mods/AdvancedNPCs Assets/Game/Mods/AdvancedNPCs.meta && git status --short && git commit -m "test: add AdvancedNPCs core assembly and NUnit harness

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Definition model, parser, position format

**Files:**
- Create: `Assets/Game/Mods/AdvancedNPCs/Scripts/Core/NpcDefinition.cs`
- Create: `Assets/Game/Mods/AdvancedNPCs/Scripts/Core/HumanClasses.cs`
- Create: `Assets/Game/Mods/AdvancedNPCs/Scripts/Core/DefinitionParser.cs`
- Create: `Assets/Game/Mods/AdvancedNPCs/Scripts/Core/PositionFormat.cs`
- Test: `Assets/Game/Mods/AdvancedNPCs/Editor/Tests/DefinitionParserTests.cs`
- Test: `Assets/Game/Mods/AdvancedNPCs/Editor/Tests/PositionFormatTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `enum Bravery { Coward, Normal, Brave }`
  - `class NpcDefinition` with public fields `string Id, Name, Region, Place, BaseClass, Gender, SourceFile; float X, Y, Z, CalmDownMinHours, CalmDownMaxHours, WanderRadius; Bravery Bravery; int FleeHealthPercent; bool CrimeOnAttack`. `Gender` is `"Male"`, `"Female"`, or `""` (random). `BaseClass` is the canonical `MobileTypes` name.
  - `class ParseResult { NpcDefinition Definition; string Error; bool Ok }`
  - `static ParseResult DefinitionParser.Parse(string fileName, string json)` — `Error` format: `"<fileName>: <field>: <problem>"`.
  - `static string HumanClasses.Canonical(string name)` — canonical name or `null`.
  - `static string PositionFormat.ToJsonSnippet(string region, string place, float x, float y, float z)`

- [ ] **Step 1: Write the failing parser tests**

`Assets/Game/Mods/AdvancedNPCs/Editor/Tests/DefinitionParserTests.cs`:
```csharp
using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class DefinitionParserTests
    {
        const string Minimal =
            "{ \"id\": \"bram\", \"name\": \"Bram\", " +
            "\"location\": { \"region\": \"Daggerfall\", \"place\": \"Daggerfall\" }, " +
            "\"position\": [1.5, 2, -3] }";

        static string With(string extraFields)
        {
            return Minimal.Substring(0, Minimal.Length - 1) + ", " + extraFields + " }";
        }

        [Test]
        public void Minimal_AppliesDefaults()
        {
            ParseResult r = DefinitionParser.Parse("bram.json", Minimal);
            Assert.IsTrue(r.Ok, r.Error);
            NpcDefinition d = r.Definition;
            Assert.AreEqual("bram", d.Id);
            Assert.AreEqual("Bram", d.Name);
            Assert.AreEqual("Daggerfall", d.Region);
            Assert.AreEqual("Daggerfall", d.Place);
            Assert.AreEqual(1.5f, d.X);
            Assert.AreEqual(2f, d.Y);
            Assert.AreEqual(-3f, d.Z);
            Assert.AreEqual("Spellsword", d.BaseClass);
            Assert.AreEqual("", d.Gender);
            Assert.AreEqual(Bravery.Normal, d.Bravery);
            Assert.AreEqual(25, d.FleeHealthPercent);
            Assert.AreEqual(6f, d.CalmDownMinHours);
            Assert.AreEqual(48f, d.CalmDownMaxHours);
            Assert.IsTrue(d.CrimeOnAttack);
            Assert.AreEqual(8f, d.WanderRadius);
            Assert.AreEqual("bram.json", d.SourceFile);
        }

        [Test]
        public void AllFields_AreReadAndCanonicalised()
        {
            ParseResult r = DefinitionParser.Parse("x.json", With(
                "\"baseClass\": \"nightblade\", \"gender\": \"female\", \"bravery\": \"brave\", " +
                "\"fleeHealthPercent\": 40, \"calmDownHours\": [1, 2], \"crimeOnAttack\": false, \"wanderRadius\": 0"));
            Assert.IsTrue(r.Ok, r.Error);
            Assert.AreEqual("Nightblade", r.Definition.BaseClass);
            Assert.AreEqual("Female", r.Definition.Gender);
            Assert.AreEqual(Bravery.Brave, r.Definition.Bravery);
            Assert.AreEqual(40, r.Definition.FleeHealthPercent);
            Assert.AreEqual(1f, r.Definition.CalmDownMinHours);
            Assert.AreEqual(2f, r.Definition.CalmDownMaxHours);
            Assert.IsFalse(r.Definition.CrimeOnAttack);
            Assert.AreEqual(0f, r.Definition.WanderRadius);
        }

        [Test]
        public void WindowsLineEndingsAndIndentation_Parse()
        {
            string json = "{\r\n  \"id\": \"bram\",\r\n  \"name\": \"Bram\",\r\n" +
                "  \"location\": { \"region\": \"A\", \"place\": \"B\" },\r\n  \"position\": [0, 0, 0]\r\n}\r\n";
            Assert.IsTrue(DefinitionParser.Parse("f.json", json).Ok);
        }

        [TestCase("", "f.json: file: empty")]
        [TestCase("{ not json", "f.json: file: invalid JSON")]
        public void BrokenFiles_AreRejected(string json, string expectedPrefix)
        {
            ParseResult r = DefinitionParser.Parse("f.json", json);
            Assert.IsFalse(r.Ok);
            StringAssert.StartsWith(expectedPrefix, r.Error);
        }

        [TestCase("{ \"name\": \"B\", \"location\": { \"region\": \"A\", \"place\": \"B\" }, \"position\": [0,0,0] }", "f.json: id: required")]
        [TestCase("{ \"id\": \"Bram\", \"name\": \"B\", \"location\": { \"region\": \"A\", \"place\": \"B\" }, \"position\": [0,0,0] }", "f.json: id: use only lowercase letters, digits and underscore (got \"Bram\")")]
        [TestCase("{ \"id\": \"b\", \"location\": { \"region\": \"A\", \"place\": \"B\" }, \"position\": [0,0,0] }", "f.json: name: required")]
        [TestCase("{ \"id\": \"b\", \"name\": \"B\", \"position\": [0,0,0] }", "f.json: location.region: required")]
        [TestCase("{ \"id\": \"b\", \"name\": \"B\", \"location\": { \"region\": \"A\" }, \"position\": [0,0,0] }", "f.json: location.place: required")]
        [TestCase("{ \"id\": \"b\", \"name\": \"B\", \"location\": { \"region\": \"A\", \"place\": \"B\" } }", "f.json: position: required, must be [x, y, z]")]
        [TestCase("{ \"id\": \"b\", \"name\": \"B\", \"location\": { \"region\": \"A\", \"place\": \"B\" }, \"position\": [0,0] }", "f.json: position: required, must be [x, y, z]")]
        public void MissingOrBadRequiredFields_AreRejected(string json, string expected)
        {
            ParseResult r = DefinitionParser.Parse("f.json", json);
            Assert.IsFalse(r.Ok);
            Assert.AreEqual(expected, r.Error);
        }

        [TestCase("\"baseClass\": \"Spelsword\"", "f.json: baseClass: unknown class \"Spelsword\"")]
        [TestCase("\"baseClass\": \"Knight_CityWatch\"", "f.json: baseClass: unknown class \"Knight_CityWatch\"")]
        [TestCase("\"baseClass\": \"Rat\"", "f.json: baseClass: unknown class \"Rat\"")]
        [TestCase("\"gender\": \"Other\"", "f.json: gender: must be Male or Female (got \"Other\")")]
        [TestCase("\"bravery\": \"Heroic\"", "f.json: bravery: must be Coward, Normal or Brave (got \"Heroic\")")]
        [TestCase("\"bravery\": \"1\"", "f.json: bravery: must be Coward, Normal or Brave (got \"1\")")]
        [TestCase("\"fleeHealthPercent\": 0", "f.json: fleeHealthPercent: must be 1-99 (got 0)")]
        [TestCase("\"fleeHealthPercent\": 100", "f.json: fleeHealthPercent: must be 1-99 (got 100)")]
        [TestCase("\"calmDownHours\": [5]", "f.json: calmDownHours: must be [min, max]")]
        [TestCase("\"calmDownHours\": [0, 5]", "f.json: calmDownHours: need 0 < min <= max (got [0, 5])")]
        [TestCase("\"calmDownHours\": [9, 5]", "f.json: calmDownHours: need 0 < min <= max (got [9, 5])")]
        [TestCase("\"wanderRadius\": -1", "f.json: wanderRadius: must be 0 or more (got -1)")]
        public void BadOptionalFields_AreRejected(string field, string expected)
        {
            ParseResult r = DefinitionParser.Parse("f.json", With(field));
            Assert.IsFalse(r.Ok);
            Assert.AreEqual(expected, r.Error);
        }
    }
}
```

- [ ] **Step 2: Write the failing position-format tests**

`Assets/Game/Mods/AdvancedNPCs/Editor/Tests/PositionFormatTests.cs`:
```csharp
using System.Globalization;
using System.Threading;
using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class PositionFormatTests
    {
        [Test]
        public void FormatsPasteableJson()
        {
            string s = PositionFormat.ToJsonSnippet("Daggerfall", "Daggerfall", 12.5f, 1f, -3.25f);
            Assert.AreEqual(
                "\"location\": { \"region\": \"Daggerfall\", \"place\": \"Daggerfall\" },\n\"position\": [12.5, 1, -3.25]",
                s);
        }

        [Test]
        public void UsesDotUnderCommaCulture()
        {
            CultureInfo old = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                string s = PositionFormat.ToJsonSnippet("R", "P", 12.5f, 0f, 0f);
                StringAssert.Contains("[12.5, 0, 0]", s);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = old;
            }
        }

        [Test]
        public void EscapesQuotesInNames()
        {
            string s = PositionFormat.ToJsonSnippet("R", "The \"Inn\"", 0f, 0f, 0f);
            StringAssert.Contains("\"place\": \"The \\\"Inn\\\"\"", s);
        }
    }
}
```

- [ ] **Step 3: Run the test command — verify it fails**

Expected: compile errors in `unity-test.log` for `DefinitionParser`, `ParseResult`, `NpcDefinition`, `Bravery`, `PositionFormat` (CS0246/CS0103).

- [ ] **Step 4: Implement `NpcDefinition.cs`**

```csharp
using System;

namespace AdvancedNPCs.Core
{
    public enum Bravery
    {
        Coward,
        Normal,
        Brave,
    }

    [Serializable]
    public class NpcLocationJson
    {
        public string region;
        public string place;
    }

    /// <summary>Raw JSON shape of one definition file. Field names are the JSON keys; initial values are the defaults.</summary>
    [Serializable]
    public class NpcDefinitionJson
    {
        public string id;
        public string name;
        public NpcLocationJson location;
        public float[] position;
        public string baseClass = "Spellsword";
        public string gender = "";
        public string bravery = "Normal";
        public int fleeHealthPercent = 25;
        public float[] calmDownHours = new float[] { 6f, 48f };
        public bool crimeOnAttack = true;
        public float wanderRadius = 8f;
    }

    /// <summary>A validated NPC definition used at runtime.</summary>
    public class NpcDefinition
    {
        public string Id;
        public string Name;
        public string Region;
        public string Place;
        public float X;
        public float Y;
        public float Z;
        public string BaseClass;
        public string Gender;
        public Bravery Bravery;
        public int FleeHealthPercent;
        public float CalmDownMinHours;
        public float CalmDownMaxHours;
        public bool CrimeOnAttack;
        public float WanderRadius;
        public string SourceFile;
    }
}
```

- [ ] **Step 5: Implement `HumanClasses.cs`**

```csharp
using System;

namespace AdvancedNPCs.Core
{
    /// <summary>
    /// Class names allowed for baseClass. Mirrors MobileTypes 128-145 in Assets/Scripts/DaggerfallUnityEnums.cs.
    /// Knight_CityWatch is excluded because DFU gives guards special crime handling.
    /// </summary>
    public static class HumanClasses
    {
        public static readonly string[] Names = new string[]
        {
            "Mage", "Spellsword", "Battlemage", "Sorcerer", "Healer", "Nightblade",
            "Bard", "Burglar", "Rogue", "Acrobat", "Thief", "Assassin",
            "Monk", "Archer", "Ranger", "Barbarian", "Warrior", "Knight",
        };

        /// <summary>Returns the canonical class name for a case-insensitive match, or null.</summary>
        public static string Canonical(string name)
        {
            if (name == null)
                return null;
            foreach (string n in Names)
            {
                if (string.Equals(n, name.Trim(), StringComparison.OrdinalIgnoreCase))
                    return n;
            }
            return null;
        }
    }
}
```

- [ ] **Step 6: Implement `DefinitionParser.cs`**

```csharp
using System;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;

namespace AdvancedNPCs.Core
{
    public class ParseResult
    {
        public NpcDefinition Definition;
        public string Error;

        public bool Ok
        {
            get { return Definition != null; }
        }
    }

    /// <summary>Turns one definition file into a validated NpcDefinition or a single error line.</summary>
    public static class DefinitionParser
    {
        static readonly Regex IdPattern = new Regex("^[a-z0-9_]+$");

        public static ParseResult Parse(string fileName, string json)
        {
            if (string.IsNullOrEmpty(json) || json.Trim().Length == 0)
                return Fail(fileName, "file", "empty");

            NpcDefinitionJson raw = new NpcDefinitionJson();
            try
            {
                JsonUtility.FromJsonOverwrite(json, raw);
            }
            catch (Exception e)
            {
                return Fail(fileName, "file", "invalid JSON (" + e.Message + ")");
            }

            if (string.IsNullOrEmpty(raw.id))
                return Fail(fileName, "id", "required");
            if (!IdPattern.IsMatch(raw.id))
                return Fail(fileName, "id", "use only lowercase letters, digits and underscore (got \"" + raw.id + "\")");
            if (string.IsNullOrEmpty(raw.name))
                return Fail(fileName, "name", "required");
            if (raw.location == null || string.IsNullOrEmpty(raw.location.region))
                return Fail(fileName, "location.region", "required");
            if (string.IsNullOrEmpty(raw.location.place))
                return Fail(fileName, "location.place", "required");
            if (raw.position == null || raw.position.Length != 3)
                return Fail(fileName, "position", "required, must be [x, y, z]");

            string baseClass = HumanClasses.Canonical(raw.baseClass);
            if (baseClass == null)
                return Fail(fileName, "baseClass", "unknown class \"" + raw.baseClass + "\"");

            string gender;
            if (string.IsNullOrEmpty(raw.gender))
                gender = "";
            else if (string.Equals(raw.gender, "Male", StringComparison.OrdinalIgnoreCase))
                gender = "Male";
            else if (string.Equals(raw.gender, "Female", StringComparison.OrdinalIgnoreCase))
                gender = "Female";
            else
                return Fail(fileName, "gender", "must be Male or Female (got \"" + raw.gender + "\")");

            Bravery bravery;
            if (!TryParseBravery(raw.bravery, out bravery))
                return Fail(fileName, "bravery", "must be Coward, Normal or Brave (got \"" + raw.bravery + "\")");

            if (raw.fleeHealthPercent < 1 || raw.fleeHealthPercent > 99)
                return Fail(fileName, "fleeHealthPercent", "must be 1-99 (got " + raw.fleeHealthPercent + ")");

            if (raw.calmDownHours == null || raw.calmDownHours.Length != 2)
                return Fail(fileName, "calmDownHours", "must be [min, max]");
            float min = raw.calmDownHours[0];
            float max = raw.calmDownHours[1];
            if (!(min > 0f) || min > max)
                return Fail(fileName, "calmDownHours", "need 0 < min <= max (got [" + Num(min) + ", " + Num(max) + "])");

            if (raw.wanderRadius < 0f)
                return Fail(fileName, "wanderRadius", "must be 0 or more (got " + Num(raw.wanderRadius) + ")");

            NpcDefinition d = new NpcDefinition();
            d.Id = raw.id;
            d.Name = raw.name;
            d.Region = raw.location.region;
            d.Place = raw.location.place;
            d.X = raw.position[0];
            d.Y = raw.position[1];
            d.Z = raw.position[2];
            d.BaseClass = baseClass;
            d.Gender = gender;
            d.Bravery = bravery;
            d.FleeHealthPercent = raw.fleeHealthPercent;
            d.CalmDownMinHours = min;
            d.CalmDownMaxHours = max;
            d.CrimeOnAttack = raw.crimeOnAttack;
            d.WanderRadius = raw.wanderRadius;
            d.SourceFile = fileName;

            ParseResult ok = new ParseResult();
            ok.Definition = d;
            return ok;
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

        static string Num(float f)
        {
            return f.ToString("0.###", CultureInfo.InvariantCulture);
        }

        static ParseResult Fail(string fileName, string field, string problem)
        {
            ParseResult r = new ParseResult();
            r.Error = fileName + ": " + field + ": " + problem;
            return r;
        }
    }
}
```

- [ ] **Step 7: Implement `PositionFormat.cs`**

```csharp
using System.Globalization;

namespace AdvancedNPCs.Core
{
    /// <summary>Builds the JSON snippet printed by the anpc_pos console command.</summary>
    public static class PositionFormat
    {
        public static string ToJsonSnippet(string region, string place, float x, float y, float z)
        {
            return "\"location\": { \"region\": \"" + Escape(region) + "\", \"place\": \"" + Escape(place) + "\" },\n" +
                   "\"position\": [" + Num(x) + ", " + Num(y) + ", " + Num(z) + "]";
        }

        static string Num(float f)
        {
            return f.ToString("0.##", CultureInfo.InvariantCulture);
        }

        static string Escape(string s)
        {
            if (s == null)
                return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
```

- [ ] **Step 8: Run the test command — verify it passes**

Expected: `result="Passed"`, `failed="0"`. If a `BrokenFiles` case fails because `JsonUtility` accepted the input, adjust the parser (not the test) so that broken input yields the expected error prefix.

- [ ] **Step 9: Commit**

```bash
cd "F:/_Projects/Dagerfall/daggerfall-unity" && git add Assets/Game/Mods/AdvancedNPCs && git commit -m "feat: parse and validate Advanced NPC definition files

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Definition catalog

**Files:**
- Create: `Assets/Game/Mods/AdvancedNPCs/Scripts/Core/DefinitionCatalog.cs`
- Test: `Assets/Game/Mods/AdvancedNPCs/Editor/Tests/DefinitionCatalogTests.cs`

**Interfaces:**
- Consumes: `DefinitionParser.Parse(string, string) → ParseResult`, `NpcDefinition` (Task 2).
- Produces:
  - `class DefinitionCatalog` with `Dictionary<string, NpcDefinition> ById` (ordinal keys), `List<string> Messages` (no prefix), `int Count`.
  - `static DefinitionCatalog DefinitionCatalog.Build(IEnumerable<KeyValuePair<string, string>> files)` — key = file name, value = file text.
  - `List<NpcDefinition> ForLocation(string region, string place)` — case-insensitive, trimmed.

- [ ] **Step 1: Write the failing tests**

`Assets/Game/Mods/AdvancedNPCs/Editor/Tests/DefinitionCatalogTests.cs`:
```csharp
using System.Collections.Generic;
using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class DefinitionCatalogTests
    {
        static string Npc(string id, string region, string place)
        {
            return "{ \"id\": \"" + id + "\", \"name\": \"N\", \"location\": { \"region\": \"" + region +
                   "\", \"place\": \"" + place + "\" }, \"position\": [0,0,0] }";
        }

        static KeyValuePair<string, string> File(string name, string text)
        {
            return new KeyValuePair<string, string>(name, text);
        }

        [Test]
        public void ValidFiles_AreIndexedById()
        {
            DefinitionCatalog c = DefinitionCatalog.Build(new[]
            {
                File("a.json", Npc("a", "R", "P")),
                File("b.json", Npc("b", "R", "P")),
            });
            Assert.AreEqual(2, c.Count);
            Assert.IsTrue(c.ById.ContainsKey("a"));
            Assert.IsTrue(c.ById.ContainsKey("b"));
            CollectionAssert.Contains(c.Messages, "Loaded 2 NPC definition(s).");
        }

        [Test]
        public void BadFile_IsSkippedAndLogged_OthersStillLoad()
        {
            DefinitionCatalog c = DefinitionCatalog.Build(new[]
            {
                File("bad.json", "{ nope"),
                File("good.json", Npc("good", "R", "P")),
            });
            Assert.AreEqual(1, c.Count);
            Assert.IsTrue(c.Messages.Exists(m => m.StartsWith("bad.json: file: invalid JSON")));
        }

        [Test]
        public void DuplicateId_FirstAlphabeticalWins()
        {
            DefinitionCatalog c = DefinitionCatalog.Build(new[]
            {
                File("z.json", Npc("same", "R", "Second")),
                File("a.json", Npc("same", "R", "First")),
            });
            Assert.AreEqual(1, c.Count);
            Assert.AreEqual("First", c.ById["same"].Place);
            CollectionAssert.Contains(c.Messages, "z.json: id: duplicate \"same\" (already defined in a.json), skipped");
        }

        [Test]
        public void ForLocation_IgnoresCaseAndSpaces()
        {
            DefinitionCatalog c = DefinitionCatalog.Build(new[]
            {
                File("a.json", Npc("a", "Daggerfall", "Daggerfall")),
                File("b.json", Npc("b", "Wayrest", "Wayrest")),
            });
            List<NpcDefinition> found = c.ForLocation("daggerfall ", " DAGGERFALL");
            Assert.AreEqual(1, found.Count);
            Assert.AreEqual("a", found[0].Id);
        }

        [Test]
        public void ForLocation_NullNames_ReturnEmpty()
        {
            DefinitionCatalog c = DefinitionCatalog.Build(new[] { File("a.json", Npc("a", "R", "P")) });
            Assert.AreEqual(0, c.ForLocation(null, null).Count);
        }

        [Test]
        public void NoFiles_IsEmptyNotError()
        {
            DefinitionCatalog c = DefinitionCatalog.Build(new KeyValuePair<string, string>[0]);
            Assert.AreEqual(0, c.Count);
            CollectionAssert.Contains(c.Messages, "Loaded 0 NPC definition(s).");
        }
    }
}
```

- [ ] **Step 2: Run the test command — verify it fails**

Expected: compile error `DefinitionCatalog` not found.

- [ ] **Step 3: Implement `DefinitionCatalog.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace AdvancedNPCs.Core
{
    /// <summary>All loaded NPC definitions, keyed by id, plus the messages produced while loading them.</summary>
    public class DefinitionCatalog
    {
        public readonly Dictionary<string, NpcDefinition> ById = new Dictionary<string, NpcDefinition>(StringComparer.Ordinal);
        public readonly List<string> Messages = new List<string>();

        public int Count
        {
            get { return ById.Count; }
        }

        public static DefinitionCatalog Build(IEnumerable<KeyValuePair<string, string>> files)
        {
            DefinitionCatalog catalog = new DefinitionCatalog();
            IEnumerable<KeyValuePair<string, string>> ordered =
                files.OrderBy(f => f.Key, StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, string> file in ordered)
            {
                ParseResult result = DefinitionParser.Parse(file.Key, file.Value);
                if (!result.Ok)
                {
                    catalog.Messages.Add(result.Error);
                    continue;
                }

                NpcDefinition existing;
                if (catalog.ById.TryGetValue(result.Definition.Id, out existing))
                {
                    catalog.Messages.Add(file.Key + ": id: duplicate \"" + result.Definition.Id +
                                         "\" (already defined in " + existing.SourceFile + "), skipped");
                    continue;
                }

                catalog.ById.Add(result.Definition.Id, result.Definition);
            }

            catalog.Messages.Add("Loaded " + catalog.Count + " NPC definition(s).");
            return catalog;
        }

        public List<NpcDefinition> ForLocation(string region, string place)
        {
            List<NpcDefinition> found = new List<NpcDefinition>();
            if (region == null || place == null)
                return found;
            foreach (NpcDefinition d in ById.Values)
            {
                if (Same(d.Region, region) && Same(d.Place, place))
                    found.Add(d);
            }
            return found;
        }

        static bool Same(string a, string b)
        {
            return string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
```

- [ ] **Step 4: Run the test command — verify it passes**

Expected: `result="Passed"`, `failed="0"`.

- [ ] **Step 5: Commit**

```bash
cd "F:/_Projects/Dagerfall/daggerfall-unity" && git add Assets/Game/Mods/AdvancedNPCs && git commit -m "feat: build Advanced NPC catalog with duplicate and location lookup

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Hostility rules

**Files:**
- Modify: `Assets/Game/Mods/AdvancedNPCs/Scripts/Core/HostilityRules.cs` (replace whole file)
- Test: `Assets/Game/Mods/AdvancedNPCs/Editor/Tests/HostilityRulesTests.cs`

**Interfaces:**
- Consumes: `Bravery` (Task 2).
- Produces:
  - `enum CombatChoice { Fight, Flee }`
  - `enum HostileFlip { None, PlayerAttack, EngineSweep }`
  - `static CombatChoice HostilityRules.Decide(Bravery bravery, float healthFraction, int fleeHealthPercent)`
  - `static ulong HostilityRules.NewCalmDeadline(ulong now, float minHours, float maxHours, System.Random rng)`
  - `static bool HostilityRules.IsCalmDue(ulong now, ulong deadline)`
  - `static HostileFlip HostilityRules.ClassifyHostileFlip(bool brainHostile, bool motorHostile, bool healthDropped, bool targetIsPlayer)`
  - `static bool HostilityRules.KilledByPlayer(bool hostileToPlayer, bool fightingCreature)`
  - `const ulong SecondsPerHour = 3600` (kept from Task 1)

- [ ] **Step 1: Write the failing tests**

`Assets/Game/Mods/AdvancedNPCs/Editor/Tests/HostilityRulesTests.cs`:
```csharp
using System;
using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class HostilityRulesTests
    {
        [TestCase(1.0f)]
        [TestCase(0.01f)]
        public void Coward_AlwaysFlees(float health)
        {
            Assert.AreEqual(CombatChoice.Flee, HostilityRules.Decide(Bravery.Coward, health, 25));
        }

        [TestCase(1.0f)]
        [TestCase(0.01f)]
        public void Brave_AlwaysFights(float health)
        {
            Assert.AreEqual(CombatChoice.Fight, HostilityRules.Decide(Bravery.Brave, health, 25));
        }

        [TestCase(1.0f, CombatChoice.Fight)]
        [TestCase(0.25f, CombatChoice.Fight)]
        [TestCase(0.24f, CombatChoice.Flee)]
        [TestCase(0.0f, CombatChoice.Flee)]
        public void Normal_FleesBelowThreshold(float health, CombatChoice expected)
        {
            Assert.AreEqual(expected, HostilityRules.Decide(Bravery.Normal, health, 25));
        }

        [Test]
        public void CalmDeadline_IsWithinRange()
        {
            Random rng = new Random(1234);
            for (int i = 0; i < 200; i++)
            {
                ulong deadline = HostilityRules.NewCalmDeadline(1000UL, 6f, 48f, rng);
                Assert.GreaterOrEqual(deadline, 1000UL + 6UL * 3600UL);
                Assert.LessOrEqual(deadline, 1000UL + 48UL * 3600UL);
            }
        }

        [Test]
        public void CalmDeadline_EqualMinMax_IsExact()
        {
            Assert.AreEqual(500UL + 2UL * 3600UL, HostilityRules.NewCalmDeadline(500UL, 2f, 2f, new Random(1)));
        }

        [Test]
        public void CalmDeadline_ReHitLater_MovesForward()
        {
            Random rng = new Random(7);
            ulong first = HostilityRules.NewCalmDeadline(0UL, 1f, 1f, rng);
            ulong second = HostilityRules.NewCalmDeadline(1800UL, 1f, 1f, rng);
            Assert.Greater(second, first);
        }

        [TestCase(99UL, 100UL, false)]
        [TestCase(100UL, 100UL, true)]
        [TestCase(10000000UL, 100UL, true)]
        public void IsCalmDue(ulong now, ulong deadline, bool expected)
        {
            Assert.AreEqual(expected, HostilityRules.IsCalmDue(now, deadline));
        }

        // brainHostile, motorHostile, healthDropped, targetIsPlayer -> expected
        [TestCase(false, false, false, false, HostileFlip.None)]
        [TestCase(false, false, true, false, HostileFlip.None)]
        [TestCase(false, true, false, false, HostileFlip.EngineSweep)]
        [TestCase(false, true, true, false, HostileFlip.PlayerAttack)]
        [TestCase(false, true, false, true, HostileFlip.PlayerAttack)]
        [TestCase(false, true, true, true, HostileFlip.PlayerAttack)]
        [TestCase(true, true, false, false, HostileFlip.None)]
        [TestCase(true, true, true, true, HostileFlip.None)]
        [TestCase(true, false, false, false, HostileFlip.None)]
        public void ClassifyHostileFlip(bool brainHostile, bool motorHostile, bool healthDropped, bool targetIsPlayer, HostileFlip expected)
        {
            Assert.AreEqual(expected, HostilityRules.ClassifyHostileFlip(brainHostile, motorHostile, healthDropped, targetIsPlayer));
        }

        [Test]
        public void KilledByPlayer_OneHitOnCalmNpc_CountsAsPlayer()
        {
            Assert.IsTrue(HostilityRules.KilledByPlayer(false, false));
        }

        [Test]
        public void KilledByPlayer_WhileHostileToPlayer_CountsAsPlayer()
        {
            Assert.IsTrue(HostilityRules.KilledByPlayer(true, false));
        }

        [Test]
        public void KilledByPlayer_CalmNpcFightingCreature_CountsAsCreature()
        {
            Assert.IsFalse(HostilityRules.KilledByPlayer(false, true));
        }
    }
}
```

- [ ] **Step 2: Run the test command — verify it fails**

Expected: compile errors for `CombatChoice`, `HostileFlip`, `Decide`, etc.

- [ ] **Step 3: Implement `HostilityRules.cs` (replace whole file)**

```csharp
using System;

namespace AdvancedNPCs.Core
{
    public enum CombatChoice
    {
        Fight,
        Flee,
    }

    public enum HostileFlip
    {
        None,
        PlayerAttack,
        EngineSweep,
    }

    /// <summary>Pure decision rules for Advanced NPC hostility. No Unity or DFU types.</summary>
    public static class HostilityRules
    {
        public const ulong SecondsPerHour = 3600;

        /// <summary>Fight or flee for the given bravery and current health fraction (0..1).</summary>
        public static CombatChoice Decide(Bravery bravery, float healthFraction, int fleeHealthPercent)
        {
            switch (bravery)
            {
                case Bravery.Coward:
                    return CombatChoice.Flee;
                case Bravery.Brave:
                    return CombatChoice.Fight;
                default:
                    return healthFraction * 100f < fleeHealthPercent ? CombatChoice.Flee : CombatChoice.Fight;
            }
        }

        /// <summary>In-game second at which a player-hostile NPC calms down.</summary>
        public static ulong NewCalmDeadline(ulong now, float minHours, float maxHours, Random rng)
        {
            double hours = minHours + rng.NextDouble() * (maxHours - minHours);
            return now + (ulong)Math.Round(hours * SecondsPerHour);
        }

        public static bool IsCalmDue(ulong now, ulong deadline)
        {
            return now >= deadline;
        }

        /// <summary>
        /// Explains why EnemyMotor.IsHostile turned true while the brain believed the NPC was calm.
        /// A real player attack always damages the NPC or points its senses at the player;
        /// GameManager.MakeEnemiesHostile() does neither.
        /// </summary>
        public static HostileFlip ClassifyHostileFlip(bool brainHostile, bool motorHostile, bool healthDropped, bool targetIsPlayer)
        {
            if (brainHostile || !motorHostile)
                return HostileFlip.None;
            return (healthDropped || targetIsPlayer) ? HostileFlip.PlayerAttack : HostileFlip.EngineSweep;
        }

        /// <summary>
        /// Who gets the blame for a death. DFU raises OnDeath before it tells the NPC who hit it,
        /// so a calm NPC that dies without fighting a creature must have been killed by the player.
        /// </summary>
        public static bool KilledByPlayer(bool hostileToPlayer, bool fightingCreature)
        {
            return hostileToPlayer || !fightingCreature;
        }
    }
}
```

- [ ] **Step 4: Run the test command — verify it passes**

Expected: `result="Passed"`, `failed="0"`.

- [ ] **Step 5: Commit**

```bash
cd "F:/_Projects/Dagerfall/daggerfall-unity" && git add Assets/Game/Mods/AdvancedNPCs && git commit -m "feat: add hostility rules for bravery, calm-down and attacker classification

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: NPC state table

**Files:**
- Create: `Assets/Game/Mods/AdvancedNPCs/Scripts/Core/NpcState.cs`
- Test: `Assets/Game/Mods/AdvancedNPCs/Editor/Tests/NpcStateTableTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `[Serializable] class NpcState { bool dead; bool hostile; ulong hostileUntil; int health = -1; NpcState Clone(); }` — `health` of `-1` means "never damaged, use full health".
  - `class NpcStateTable` with `NpcState GetOrCreate(string id)` (returns the live, stored instance), `bool Has(string id)`, `Dictionary<string, NpcState> Snapshot()` (deep copy), `void Restore(Dictionary<string, NpcState> states)` (deep copy in; `null` clears), `void Clear()`.

- [ ] **Step 1: Write the failing tests**

`Assets/Game/Mods/AdvancedNPCs/Editor/Tests/NpcStateTableTests.cs`:
```csharp
using System.Collections.Generic;
using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class NpcStateTableTests
    {
        [Test]
        public void GetOrCreate_NewId_IsFresh()
        {
            NpcStateTable t = new NpcStateTable();
            NpcState s = t.GetOrCreate("bram");
            Assert.IsFalse(s.dead);
            Assert.IsFalse(s.hostile);
            Assert.AreEqual(0UL, s.hostileUntil);
            Assert.AreEqual(-1, s.health);
            Assert.IsTrue(t.Has("bram"));
        }

        [Test]
        public void GetOrCreate_ReturnsSameInstance()
        {
            NpcStateTable t = new NpcStateTable();
            t.GetOrCreate("bram").dead = true;
            Assert.IsTrue(t.GetOrCreate("bram").dead);
        }

        [Test]
        public void Snapshot_IsIndependentCopy()
        {
            NpcStateTable t = new NpcStateTable();
            t.GetOrCreate("bram").health = 10;
            Dictionary<string, NpcState> snap = t.Snapshot();
            t.GetOrCreate("bram").health = 3;
            Assert.AreEqual(10, snap["bram"].health);
        }

        [Test]
        public void Restore_ReplacesContentAndKeepsUnknownIds()
        {
            NpcStateTable t = new NpcStateTable();
            t.GetOrCreate("old").dead = true;

            Dictionary<string, NpcState> saved = new Dictionary<string, NpcState>();
            NpcState s = new NpcState();
            s.hostile = true;
            s.hostileUntil = 99UL;
            s.health = 7;
            saved["bram"] = s;
            saved["removed_from_disk"] = new NpcState();

            t.Restore(saved);
            s.health = 1; // mutating the source must not affect the table

            Assert.IsFalse(t.Has("old"));
            Assert.IsTrue(t.Has("removed_from_disk"));
            NpcState r = t.GetOrCreate("bram");
            Assert.IsTrue(r.hostile);
            Assert.AreEqual(99UL, r.hostileUntil);
            Assert.AreEqual(7, r.health);
        }

        [Test]
        public void Restore_Null_Clears()
        {
            NpcStateTable t = new NpcStateTable();
            t.GetOrCreate("bram");
            t.Restore(null);
            Assert.IsFalse(t.Has("bram"));
        }

        [Test]
        public void Clear_ResetsEverything()
        {
            NpcStateTable t = new NpcStateTable();
            t.GetOrCreate("bram").dead = true;
            t.Clear();
            Assert.IsFalse(t.Has("bram"));
            Assert.IsFalse(t.GetOrCreate("bram").dead);
        }
    }
}
```

- [ ] **Step 2: Run the test command — verify it fails**

Expected: compile errors for `NpcStateTable`, `NpcState`.

- [ ] **Step 3: Implement `NpcState.cs`**

```csharp
using System;
using System.Collections.Generic;

namespace AdvancedNPCs.Core
{
    /// <summary>What we remember about one NPC between visits and saves. Field names are part of the save format.</summary>
    [Serializable]
    public class NpcState
    {
        public bool dead;
        public bool hostile;
        public ulong hostileUntil;
        public int health = -1;

        public NpcState Clone()
        {
            NpcState c = new NpcState();
            c.dead = dead;
            c.hostile = hostile;
            c.hostileUntil = hostileUntil;
            c.health = health;
            return c;
        }
    }

    /// <summary>All NPC states keyed by definition id. Entries for ids without a definition are kept.</summary>
    public class NpcStateTable
    {
        readonly Dictionary<string, NpcState> states = new Dictionary<string, NpcState>(StringComparer.Ordinal);

        public NpcState GetOrCreate(string id)
        {
            NpcState s;
            if (!states.TryGetValue(id, out s))
            {
                s = new NpcState();
                states.Add(id, s);
            }
            return s;
        }

        public bool Has(string id)
        {
            return states.ContainsKey(id);
        }

        public Dictionary<string, NpcState> Snapshot()
        {
            Dictionary<string, NpcState> copy = new Dictionary<string, NpcState>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, NpcState> kv in states)
                copy.Add(kv.Key, kv.Value.Clone());
            return copy;
        }

        public void Restore(Dictionary<string, NpcState> saved)
        {
            states.Clear();
            if (saved == null)
                return;
            foreach (KeyValuePair<string, NpcState> kv in saved)
            {
                if (kv.Key != null && kv.Value != null)
                    states[kv.Key] = kv.Value.Clone();
            }
        }

        public void Clear()
        {
            states.Clear();
        }
    }
}
```

- [ ] **Step 4: Run the test command — verify it passes**

Expected: `result="Passed"`, `failed="0"`. Total tests now ≈ 60.

- [ ] **Step 5: Commit**

```bash
cd "F:/_Projects/Dagerfall/daggerfall-unity" && git add Assets/Game/Mods/AdvancedNPCs && git commit -m "feat: add per-NPC state table with snapshot and restore

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Mod shell — entry point, loading, save data, console command, build

No NPCs spawn yet. Deliverable: the `.dfmod` loads in `DFU_testing`, logs the catalog, saves/loads mod data without errors, and `anpc_pos` prints a pasteable snippet.

**Files:**
- Create: `Assets/Game/Mods/AdvancedNPCs/Scripts/Runtime/AdvancedNpcsMod.cs`
- Create: `Assets/Game/Mods/AdvancedNPCs/Scripts/Runtime/NpcSaveDataInterface.cs`
- Create: `Assets/Game/Mods/AdvancedNPCs/Examples/bram.json`
- Create (via Mod Builder): `Assets/Game/Mods/AdvancedNPCs/AdvancedNPCs.dfmod.json`

**Interfaces:**
- Consumes: `DefinitionCatalog.Build`, `NpcStateTable`, `PositionFormat.ToJsonSnippet` (Tasks 2, 3, 5).
- Produces:
  - `AdvancedNpcsMod.Instance` (`AdvancedNpcsMod`), `.Catalog` (`DefinitionCatalog`), `.States` (`NpcStateTable`), `AdvancedNpcsMod.LogPrefix` (`"[AdvancedNPCs] "`), `static void AdvancedNpcsMod.Log(string)`, `static void AdvancedNpcsMod.LogError(string)`.
  - `AdvancedNpcsMod.OnStateRestored` (`event System.Action`) — raised after save data is restored or a new game starts. Task 7's spawner subscribes.
  - `NpcSaveData` (`[fsObject("v1")]`, field `Dictionary<string, NpcState> States`).

- [ ] **Step 1: Write `NpcSaveDataInterface.cs`**

```csharp
using System;
using System.Collections.Generic;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using FullSerializer;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    [fsObject("v1")]
    public class NpcSaveData
    {
        public Dictionary<string, NpcState> States = new Dictionary<string, NpcState>();
    }

    /// <summary>Connects the NPC state table to DFU's per-mod save data.</summary>
    public class NpcSaveDataInterface : IHasModSaveData
    {
        readonly NpcStateTable table;
        readonly Action onRestored;

        public NpcSaveDataInterface(NpcStateTable table, Action onRestored)
        {
            this.table = table;
            this.onRestored = onRestored;
        }

        public Type SaveDataType
        {
            get { return typeof(NpcSaveData); }
        }

        public object NewSaveData()
        {
            return new NpcSaveData();
        }

        public object GetSaveData()
        {
            NpcSaveData data = new NpcSaveData();
            data.States = table.Snapshot();
            return data;
        }

        public void RestoreSaveData(object saveData)
        {
            NpcSaveData data = saveData as NpcSaveData;
            table.Restore(data != null ? data.States : null);
            onRestored();
        }
    }
}
```

- [ ] **Step 2: Write `AdvancedNpcsMod.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using Wenzil.Console;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>Mod entry point. Owns the catalog and state table and wires DFU events.</summary>
    public class AdvancedNpcsMod : MonoBehaviour
    {
        public const string LogPrefix = "[AdvancedNPCs] ";
        public const string FolderName = "AdvancedNPCs";

        public static AdvancedNpcsMod Instance { get; private set; }

        public DefinitionCatalog Catalog { get; private set; }
        public NpcStateTable States { get; private set; }

        /// <summary>Raised after save data is restored or a new game starts.</summary>
        public event Action OnStateRestored;

        static Mod mod;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;
            GameObject go = new GameObject("AdvancedNPCs");
            Instance = go.AddComponent<AdvancedNpcsMod>();
            mod.SaveDataInterface = new NpcSaveDataInterface(Instance.States, Instance.RaiseStateRestored);
            mod.IsReady = true;
        }

        void Awake()
        {
            States = new NpcStateTable();
            Catalog = LoadCatalog();
            StartGameBehaviour.OnNewGame += OnNewGame;
            ConsoleCommandsDatabase.RegisterCommand("anpc_pos",
                "Prints your position as Advanced NPC definition JSON.", "anpc_pos", PosCommand);
        }

        void OnDestroy()
        {
            StartGameBehaviour.OnNewGame -= OnNewGame;
        }

        void OnNewGame()
        {
            States.Clear();
            RaiseStateRestored();
        }

        void RaiseStateRestored()
        {
            if (OnStateRestored != null)
                OnStateRestored();
        }

        static DefinitionCatalog LoadCatalog()
        {
            string folder = Path.Combine(Application.streamingAssetsPath, FolderName);
            List<KeyValuePair<string, string>> files = new List<KeyValuePair<string, string>>();
            if (!Directory.Exists(folder))
            {
                Log("No definitions folder at " + folder + "; nothing to spawn.");
            }
            else
            {
                foreach (string path in Directory.GetFiles(folder, "*.json"))
                {
                    try
                    {
                        files.Add(new KeyValuePair<string, string>(Path.GetFileName(path), File.ReadAllText(path)));
                    }
                    catch (Exception e)
                    {
                        LogError(Path.GetFileName(path) + ": file: could not read (" + e.Message + ")");
                    }
                }
            }

            DefinitionCatalog catalog = DefinitionCatalog.Build(files);
            foreach (string message in catalog.Messages)
                Log(message);
            return catalog;
        }

        static string PosCommand(params string[] args)
        {
            GameManager gm = GameManager.Instance;
            DaggerfallLocation location = gm.StreamingWorld.CurrentPlayerLocationObject;
            if (gm.PlayerEnterExit.IsPlayerInside || location == null)
                return "Stand outdoors inside a town first.";

            Vector3 local = location.transform.InverseTransformPoint(gm.PlayerObject.transform.position);
            return PositionFormat.ToJsonSnippet(location.Summary.RegionName, location.Summary.LocationName,
                local.x, local.y, local.z);
        }

        public static void Log(string message)
        {
            Debug.Log(LogPrefix + message);
        }

        public static void LogError(string message)
        {
            Debug.LogError(LogPrefix + message);
        }
    }
}
```

Note: `Init` reads `Instance.States` after `AddComponent`, which runs `Awake` synchronously, so `States` is already set.

- [ ] **Step 3: Write the example definition**

`Assets/Game/Mods/AdvancedNPCs/Examples/bram.json` (position `[0, 0, 0]` is the town origin; Task 8 Step 2 replaces it with real `anpc_pos` output):
```json
{
  "id": "daggerfall_city_bram",
  "name": "Bram the Cooper",
  "location": { "region": "Daggerfall", "place": "Daggerfall" },
  "position": [0, 0, 0],
  "baseClass": "Spellsword",
  "gender": "Male",
  "bravery": "Normal",
  "fleeHealthPercent": 25,
  "calmDownHours": [6, 48],
  "crimeOnAttack": true,
  "wanderRadius": 8
}
```

- [ ] **Step 4: Compile check in batch mode**

Run the Test command. Expected: still `result="Passed"` (all Core tests), and `grep -c "error CS" F:/_Projects/Dagerfall/unity-test.log` prints `0` — this proves the Runtime scripts compile against DFU in Assembly-CSharp.

- [ ] **Step 5: USER ACTION — create the mod manifest and build the .dfmod**

Ask the user to do this (Claude cannot click in the Unity Editor):
1. Unity Hub → Add → `F:\_Projects\Dagerfall\daggerfall-unity`, open with **2019.4.41f2**.
2. Menu **Daggerfall Tools → Mod Builder**.
3. Create new mod: Title `Advanced NPCs`, Version `0.1.0`, Author `GoatBoy-11`, DFU version `1.9.2`. Save as `Assets/Game/Mods/AdvancedNPCs/AdvancedNPCs.dfmod.json`.
4. Add every `.cs` file under `Scripts/Core/` and `Scripts/Runtime/`. Do **not** add `.asmdef` files or anything under `Editor/` or `Examples/`.
5. Target: Windows. Build, and tell Claude the output path of the `.dfmod`.
6. Close the Unity Editor afterwards (batch-mode tests need it closed).

- [ ] **Step 6: Install and smoke-run**

Replace `<built path>` with the path the user reported:
```bash
cp "<built path>/advancednpcs.dfmod" "F:/_Projects/Dagerfall/DFU_testing/DaggerfallUnity_Data/StreamingAssets/Mods/" && mkdir -p "F:/_Projects/Dagerfall/DFU_testing/DaggerfallUnity_Data/StreamingAssets/AdvancedNPCs" && cp "F:/_Projects/Dagerfall/daggerfall-unity/Assets/Game/Mods/AdvancedNPCs/Examples/bram.json" "F:/_Projects/Dagerfall/DFU_testing/DaggerfallUnity_Data/StreamingAssets/AdvancedNPCs/"
```
User launches `DaggerfallUnity.exe`, enables **Advanced NPCs** in the mod list, loads a save, saves once, loads that save, opens the console and runs `anpc_pos` while standing in a town, then quits. Then:
```bash
grep -E "\[AdvancedNPCs\]|Exception|error CS" "$USERPROFILE/AppData/LocalLow/Daggerfall Workshop/Daggerfall Unity/Player.log" | head -30
```
Expected: `[AdvancedNPCs] Loaded 1 NPC definition(s).`, no `Exception` mentioning `AdvancedNPCs`, no `error CS`. `anpc_pos` printed `"location": {...},` and `"position": [x, y, z]` with dots as decimal separators.

- [ ] **Step 7: Commit**

```bash
cd "F:/_Projects/Dagerfall/daggerfall-unity" && git add Assets/Game/Mods/AdvancedNPCs && git status --short && git commit -m "feat: add Advanced NPCs mod entry, save data and anpc_pos command

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```
Do not commit build output; if a build folder shows as untracked, leave it.

---

### Task 7: Spawner, mover and brain

Deliverable: NPCs spawn in their town, wander when calm, turn hostile only toward attackers, fight or flee by bravery, raise crimes, calm down over in-game time, stay dead, and survive save/load without duplicates.

**Files:**
- Create: `Assets/Game/Mods/AdvancedNPCs/Scripts/Runtime/NpcMover.cs`
- Create: `Assets/Game/Mods/AdvancedNPCs/Scripts/Runtime/NpcBrain.cs`
- Create: `Assets/Game/Mods/AdvancedNPCs/Scripts/Runtime/NpcSpawner.cs`
- Modify: `Assets/Game/Mods/AdvancedNPCs/Scripts/Runtime/AdvancedNpcsMod.cs` (create the spawner in `Awake`)

**Interfaces:**
- Consumes: `AdvancedNpcsMod.Instance/.Catalog/.States/.OnStateRestored/.Log/.LogError` (Task 6); `HostilityRules.*`, `CombatChoice`, `HostileFlip` (Task 4); `NpcDefinition`, `Bravery` (Task 2); `NpcState`, `NpcStateTable` (Task 5).
- DFU APIs used (verified in source): `GameObjectHelper.CreateEnemy(string, MobileTypes, Vector3, MobileGender, Transform, MobileReactions)`, `DaggerfallEnemy.LoadID`, `StreamingWorld.OnCreateLocationGameObject(DaggerfallLocation)`, `StreamingWorld.CurrentPlayerLocationObject`, `DaggerfallLocation.Summary.RegionName/LocationName`, `EnemyMotor.IsHostile`, `EnemySenses.Target`, `DaggerfallEntityBehaviour.Entity.CurrentHealth/MaxHealth/OnDeath`, `MobileUnit.EnemyState`, `MobileUnit.ChangeEnemyState(MobileStates)`, `PlayerEntity.CrimeCommitted`, `PlayerEntity.Crimes.Assault/Murder`, `PlayerEntity.SpawnCityGuards(bool)`, `DaggerfallUnity.Instance.WorldTime.Now.ToSeconds()`.
- Produces: `NpcSpawner(AdvancedNpcsMod owner)` with `void Enable()`, `void Disable()`, `void RespawnCurrentLocation()`; `NpcBrain` with `static bool IsLive(string id)`, `static void DespawnAll()`, `void Init(NpcDefinition def, NpcState state)`; `NpcMover` with `void Stop()`, `void MoveToward(Vector3 worldPos, float speed)`, `void MoveAway(Vector3 worldPos, float speed)`.

- [ ] **Step 1: Write `NpcMover.cs`**

```csharp
using UnityEngine;
using DaggerfallWorkshop;

namespace AdvancedNPCs
{
    /// <summary>Simple direct movement used while the vanilla EnemyMotor is switched off (calm and fleeing).</summary>
    [RequireComponent(typeof(CharacterController))]
    public class NpcMover : MonoBehaviour
    {
        public const float WalkSpeed = 1.5f;
        public const float RunSpeed = 5f;

        CharacterController controller;
        MobileUnit mobile;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            mobile = GetComponentInChildren<MobileUnit>();
        }

        public void Stop()
        {
            if (controller.enabled)
                controller.SimpleMove(Vector3.zero); // keeps gravity applied while standing
            SetAnim(MobileStates.Idle);
        }

        public void MoveToward(Vector3 worldPos, float speed)
        {
            Move(worldPos - transform.position, speed);
        }

        public void MoveAway(Vector3 worldPos, float speed)
        {
            Move(transform.position - worldPos, speed);
        }

        void Move(Vector3 direction, float speed)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                Stop();
                return;
            }
            direction.Normalize();
            transform.rotation = Quaternion.LookRotation(direction);
            if (controller.enabled)
                controller.SimpleMove(direction * speed); // SimpleMove applies gravity
            SetAnim(MobileStates.Move);
        }

        void SetAnim(MobileStates state)
        {
            if (mobile != null && mobile.EnemyState != state)
                mobile.ChangeEnemyState(state);
        }
    }
}
```

- [ ] **Step 2: Write `NpcBrain.cs`**

```csharp
using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>
    /// Authoritative state machine for one Advanced NPC. Vanilla EnemyMotor/EnemyAttack only run while Fighting.
    /// Undoes GameManager.MakeEnemiesHostile() for NPCs that were not actually attacked.
    /// </summary>
    [RequireComponent(typeof(NpcMover))]
    public class NpcBrain : MonoBehaviour
    {
        enum Mode { Calm, Fighting, Fleeing, Dead }

        const float SafeDistance = 30f;
        const float CreatureGiveUpDistance = 40f;
        const float CalmCheckInterval = 2f;
        const float WanderPauseMin = 2f;
        const float WanderPauseMax = 6f;

        static readonly Dictionary<string, NpcBrain> live = new Dictionary<string, NpcBrain>();

        NpcDefinition def;
        NpcState state;
        Mode mode = Mode.Calm;
        DaggerfallEntityBehaviour threat;

        DaggerfallEntityBehaviour entityBehaviour;
        EnemyMotor motor;
        EnemySenses senses;
        EnemyAttack attack;
        NpcMover mover;

        int lastHealth;
        Vector3 homeLocal;
        Vector3 wanderTargetLocal;
        bool hasWanderTarget;
        float wanderPause;
        float calmCheckTimer;
        System.Random rng;

        public static bool IsLive(string id)
        {
            NpcBrain b;
            return live.TryGetValue(id, out b) && b != null;
        }

        public static void DespawnAll()
        {
            foreach (NpcBrain b in new List<NpcBrain>(live.Values))
            {
                if (b != null)
                    Destroy(b.gameObject);
            }
            live.Clear();
        }

        public void Init(NpcDefinition definition, NpcState npcState)
        {
            def = definition;
            state = npcState;
            rng = new System.Random(definition.Id.GetHashCode() ^ System.Environment.TickCount);
            live[def.Id] = this;
        }

        void Start()
        {
            entityBehaviour = GetComponent<DaggerfallEntityBehaviour>();
            motor = GetComponent<EnemyMotor>();
            senses = GetComponent<EnemySenses>();
            attack = GetComponent<EnemyAttack>();
            mover = GetComponent<NpcMover>();
            homeLocal = transform.localPosition;

            if (state.health > 0)
                entityBehaviour.Entity.CurrentHealth = state.health;
            lastHealth = entityBehaviour.Entity.CurrentHealth;
            entityBehaviour.Entity.OnDeath += OnDeath;

            if (state.hostile && HostilityRules.IsCalmDue(Now(), state.hostileUntil))
                state.hostile = false;

            if (state.hostile)
            {
                threat = GameManager.Instance.PlayerEntityBehaviour;
                EnterCombat();
            }
            else
            {
                SetMode(Mode.Calm);
            }
        }

        void OnDestroy()
        {
            if (entityBehaviour != null && entityBehaviour.Entity != null)
            {
                entityBehaviour.Entity.OnDeath -= OnDeath;
                if (mode != Mode.Dead)
                    state.health = entityBehaviour.Entity.CurrentHealth;
            }
            NpcBrain current;
            if (def != null && live.TryGetValue(def.Id, out current) && current == this)
                live.Remove(def.Id);
        }

        void Update()
        {
            if (mode == Mode.Dead || entityBehaviour == null)
                return;

            DaggerfallEntityBehaviour player = GameManager.Instance.PlayerEntityBehaviour;
            int health = entityBehaviour.Entity.CurrentHealth;
            bool healthDropped = health < lastHealth;
            lastHealth = health;
            state.health = health;

            // Hostility guard: decide why IsHostile changed before anything else reads it.
            HostileFlip flip = HostilityRules.ClassifyHostileFlip(
                state.hostile, motor.IsHostile, healthDropped, senses.Target == player);
            if (flip == HostileFlip.PlayerAttack)
            {
                OnAttackedByPlayer();
            }
            else if (flip == HostileFlip.EngineSweep)
            {
                motor.IsHostile = false;
                if (senses.Target == player)
                    senses.Target = null;
            }
            else if (healthDropped && state.hostile && threat == player)
            {
                // Player hit again while already hostile: anger lasts longer, no new assault.
                state.hostileUntil = HostilityRules.NewCalmDeadline(Now(), def.CalmDownMinHours, def.CalmDownMaxHours, rng);
            }
            else if (healthDropped && !state.hostile && senses.Target != null && senses.Target != player)
            {
                OnAttackedByCreature(senses.Target);
            }

            // Keep vanilla in line with our state (EnemyMotor.Start resets IsHostile from MobileReactions).
            motor.IsHostile = state.hostile;

            UpdateCalmDown();

            switch (mode)
            {
                case Mode.Calm:
                    UpdateCalm();
                    break;
                case Mode.Fighting:
                    UpdateFighting();
                    break;
                case Mode.Fleeing:
                    UpdateFleeing();
                    break;
            }
        }

        void OnAttackedByPlayer()
        {
            bool wasCalm = !state.hostile;
            state.hostile = true;
            state.hostileUntil = HostilityRules.NewCalmDeadline(Now(), def.CalmDownMinHours, def.CalmDownMaxHours, rng);
            threat = GameManager.Instance.PlayerEntityBehaviour;

            if (wasCalm && def.CrimeOnAttack)
            {
                PlayerEntity player = GameManager.Instance.PlayerEntity;
                player.CrimeCommitted = PlayerEntity.Crimes.Assault;
                player.SpawnCityGuards(true);
                AdvancedNpcsMod.Log(def.Id + ": assaulted by player.");
            }
            EnterCombat();
        }

        void OnAttackedByCreature(DaggerfallEntityBehaviour attacker)
        {
            if (mode != Mode.Calm)
                return;
            threat = attacker;
            EnterCombat();
        }

        void EnterCombat()
        {
            CombatChoice choice = HostilityRules.Decide(def.Bravery, HealthFraction(), def.FleeHealthPercent);
            SetMode(choice == CombatChoice.Flee ? Mode.Fleeing : Mode.Fighting);
        }

        void UpdateCalmDown()
        {
            calmCheckTimer -= Time.deltaTime;
            if (calmCheckTimer > 0f)
                return;
            calmCheckTimer = CalmCheckInterval;

            if (state.hostile && HostilityRules.IsCalmDue(Now(), state.hostileUntil))
            {
                state.hostile = false;
                motor.IsHostile = false;
                AdvancedNpcsMod.Log(def.Id + ": calmed down.");
                if (threat == GameManager.Instance.PlayerEntityBehaviour)
                    SetMode(Mode.Calm);
            }
        }

        void UpdateCalm()
        {
            if (def.WanderRadius <= 0f)
            {
                mover.Stop();
                return;
            }

            if (!hasWanderTarget)
            {
                wanderPause -= Time.deltaTime;
                mover.Stop();
                if (wanderPause > 0f)
                    return;
                Vector2 offset = Random.insideUnitCircle * def.WanderRadius;
                wanderTargetLocal = homeLocal + new Vector3(offset.x, 0f, offset.y);
                hasWanderTarget = true;
            }

            Vector3 targetWorld = transform.parent != null ? transform.parent.TransformPoint(wanderTargetLocal) : wanderTargetLocal;
            Vector3 flat = targetWorld - transform.position;
            flat.y = 0f;
            if (flat.magnitude < 0.5f)
            {
                hasWanderTarget = false;
                wanderPause = Random.Range(WanderPauseMin, WanderPauseMax);
                mover.Stop();
                return;
            }
            mover.MoveToward(targetWorld, NpcMover.WalkSpeed);
        }

        void UpdateFighting()
        {
            if (ThreatGone())
            {
                EndCreatureFight();
                return;
            }
            if (threat != GameManager.Instance.PlayerEntityBehaviour)
                senses.Target = threat;
            if (HostilityRules.Decide(def.Bravery, HealthFraction(), def.FleeHealthPercent) == CombatChoice.Flee)
                SetMode(Mode.Fleeing);
        }

        void UpdateFleeing()
        {
            if (ThreatGone())
            {
                EndCreatureFight();
                return;
            }
            float distance = Vector3.Distance(transform.position, threat.transform.position);
            if (distance >= SafeDistance && !CanSee(threat))
                mover.Stop(); // cower
            else
                mover.MoveAway(threat.transform.position, NpcMover.RunSpeed);
        }

        bool ThreatGone()
        {
            if (threat == GameManager.Instance.PlayerEntityBehaviour)
                return false; // player hostility ends only by calm-down
            return threat == null || threat.Entity == null || threat.Entity.CurrentHealth <= 0 ||
                   Vector3.Distance(transform.position, threat.transform.position) > CreatureGiveUpDistance;
        }

        void EndCreatureFight()
        {
            if (state.hostile)
            {
                threat = GameManager.Instance.PlayerEntityBehaviour;
                EnterCombat();
            }
            else
            {
                SetMode(Mode.Calm);
            }
        }

        bool CanSee(DaggerfallEntityBehaviour other)
        {
            Vector3 from = transform.position + Vector3.up * 0.5f;
            Vector3 to = other.transform.position + Vector3.up * 0.5f;
            RaycastHit hit;
            if (!Physics.Linecast(from, to, out hit))
                return true;
            return hit.collider.GetComponentInParent<DaggerfallEntityBehaviour>() == other;
        }

        void SetMode(Mode newMode)
        {
            mode = newMode;
            bool fighting = newMode == Mode.Fighting;
            motor.enabled = fighting;
            attack.enabled = fighting;
            mover.enabled = !fighting && newMode != Mode.Dead;

            if (newMode == Mode.Calm)
            {
                threat = null;
                senses.Target = null;
                hasWanderTarget = false;
                wanderPause = Random.Range(WanderPauseMin, WanderPauseMax);
            }
        }

        void OnDeath(DaggerfallEntity entity)
        {
            bool fightingCreature = threat != null && threat != GameManager.Instance.PlayerEntityBehaviour;
            bool byPlayer = HostilityRules.KilledByPlayer(state.hostile, fightingCreature);

            mode = Mode.Dead;
            state.dead = true;
            state.hostile = false;

            if (byPlayer && def.CrimeOnAttack)
            {
                PlayerEntity player = GameManager.Instance.PlayerEntity;
                player.CrimeCommitted = PlayerEntity.Crimes.Murder;
                player.SpawnCityGuards(true);
            }
            AdvancedNpcsMod.Log(def.Id + ": died" + (byPlayer ? " (player)." : " (creature)."));
        }

        float HealthFraction()
        {
            int max = entityBehaviour.Entity.MaxHealth;
            return max > 0 ? (float)entityBehaviour.Entity.CurrentHealth / max : 1f;
        }

        static ulong Now()
        {
            return DaggerfallUnity.Instance.WorldTime.Now.ToSeconds();
        }
    }
}
```

- [ ] **Step 3: Write `NpcSpawner.cs`**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Utility;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>Spawns Advanced NPCs when their town's GameObject is created.</summary>
    public class NpcSpawner
    {
        readonly AdvancedNpcsMod owner;

        public NpcSpawner(AdvancedNpcsMod owner)
        {
            this.owner = owner;
        }

        public void Enable()
        {
            StreamingWorld.OnCreateLocationGameObject += SpawnFor;
            owner.OnStateRestored += RespawnCurrentLocation;
        }

        public void Disable()
        {
            StreamingWorld.OnCreateLocationGameObject -= SpawnFor;
            owner.OnStateRestored -= RespawnCurrentLocation;
        }

        /// <summary>After a load or new game, rebuild NPCs in the current town from the restored state.</summary>
        public void RespawnCurrentLocation()
        {
            NpcBrain.DespawnAll();
            if (GameManager.Instance == null || GameManager.Instance.StreamingWorld == null)
                return;
            SpawnFor(GameManager.Instance.StreamingWorld.CurrentPlayerLocationObject);
        }

        void SpawnFor(DaggerfallLocation location)
        {
            if (location == null)
                return;

            List<NpcDefinition> defs = owner.Catalog.ForLocation(location.Summary.RegionName, location.Summary.LocationName);
            foreach (NpcDefinition def in defs)
            {
                if (NpcBrain.IsLive(def.Id))
                    continue;
                NpcState state = owner.States.GetOrCreate(def.Id);
                if (state.dead)
                    continue;
                try
                {
                    Spawn(def, state, location.transform);
                }
                catch (Exception e)
                {
                    AdvancedNpcsMod.LogError(def.Id + ": spawn failed (" + e.Message + ")");
                }
            }
        }

        static void Spawn(NpcDefinition def, NpcState state, Transform parent)
        {
            MobileTypes type = (MobileTypes)Enum.Parse(typeof(MobileTypes), def.BaseClass);
            MobileGender gender = MobileGender.Unspecified;
            if (def.Gender == "Male")
                gender = MobileGender.Male;
            else if (def.Gender == "Female")
                gender = MobileGender.Female;

            GameObject go = GameObjectHelper.CreateEnemy(def.Name, type, new Vector3(def.X, def.Y, def.Z),
                gender, parent, MobileReactions.Passive);

            // SerializableEnemy only registers with the vanilla save system when LoadID != 0.
            // Keeping it 0 means our own state table is the only save, so loads never duplicate NPCs.
            DaggerfallEnemy enemy = go.GetComponent<DaggerfallEnemy>();
            if (enemy != null)
                enemy.LoadID = 0;

            go.AddComponent<NpcMover>();
            NpcBrain brain = go.AddComponent<NpcBrain>();
            brain.Init(def, state);
            AdvancedNpcsMod.Log(def.Id + ": spawned in " + def.Place + ".");
        }
    }
}
```

- [ ] **Step 4: Wire the spawner into `AdvancedNpcsMod.cs`**

Add a field below `static Mod mod;`:
```csharp
        NpcSpawner spawner;
```

In `Awake()`, after the `ConsoleCommandsDatabase.RegisterCommand(...)` call, add:
```csharp
            spawner = new NpcSpawner(this);
            spawner.Enable();
```

Replace `OnDestroy()` with:
```csharp
        void OnDestroy()
        {
            StartGameBehaviour.OnNewGame -= OnNewGame;
            if (spawner != null)
                spawner.Disable();
        }
```

- [ ] **Step 5: Compile check in batch mode**

Run the Test command. Expected: `result="Passed"` and `grep -c "error CS" F:/_Projects/Dagerfall/unity-test.log` prints `0`.

- [ ] **Step 6: USER ACTION — rebuild the .dfmod**

Ask the user: open the project in 2019.4.41f2 → **Daggerfall Tools → Mod Builder** → load `AdvancedNPCs.dfmod.json` → add the three new Runtime `.cs` files → Build (Windows) → close the editor. Then copy the `.dfmod` into `DFU_testing` with the Task 6 Step 6 `cp` command.

- [ ] **Step 7: Spawn smoke check**

User loads a save near Daggerfall city (or travels there) and quits. Then:
```bash
grep -E "\[AdvancedNPCs\]|NullReference|Exception" "$USERPROFILE/AppData/LocalLow/Daggerfall Workshop/Daggerfall Unity/Player.log" | head -30
```
Expected: `[AdvancedNPCs] daggerfall_city_bram: spawned in Daggerfall.` and no exceptions from `AdvancedNPCs` types. If the location never matches, the `anpc_pos` output from Task 6 shows the exact region/place strings to use in `bram.json`.

- [ ] **Step 8: Commit**

```bash
cd "F:/_Projects/Dagerfall/daggerfall-unity" && git add Assets/Game/Mods/AdvancedNPCs && git commit -m "feat: spawn Advanced NPCs with hostility guard, bravery, crime and calm-down

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: Example NPCs, in-game verification, author guide

**Files:**
- Modify: `Assets/Game/Mods/AdvancedNPCs/Examples/bram.json` (real position)
- Create: `Assets/Game/Mods/AdvancedNPCs/Examples/coward_cora.json`
- Create: `Assets/Game/Mods/AdvancedNPCs/Examples/brave_bors.json`
- Create: `Assets/Game/Mods/AdvancedNPCs/README.md`

**Interfaces:**
- Consumes: everything above.
- Produces: verified mod build plus author documentation.

- [ ] **Step 1: USER ACTION — capture three positions**

User stands at three outdoor spots a few metres apart in Daggerfall city, runs `anpc_pos` at each, and pastes the outputs to Claude.

- [ ] **Step 2: Write the example files with the real positions**

In `bram.json`, replace the `location` and `position` lines with the spot-1 output. Create the two files below, then replace their `location` and `position` lines with the spot-2 and spot-3 outputs. The committed files must contain the user's pasted values, not `[0, 0, 0]`.

`coward_cora.json`:
```json
{
  "id": "daggerfall_city_cora",
  "name": "Cora the Baker",
  "location": { "region": "Daggerfall", "place": "Daggerfall" },
  "position": [0, 0, 0],
  "baseClass": "Bard",
  "gender": "Female",
  "bravery": "Coward",
  "calmDownHours": [1, 2],
  "wanderRadius": 6
}
```

`brave_bors.json`:
```json
{
  "id": "daggerfall_city_bors",
  "name": "Bors the Smith",
  "location": { "region": "Daggerfall", "place": "Daggerfall" },
  "position": [0, 0, 0],
  "baseClass": "Warrior",
  "gender": "Male",
  "bravery": "Brave",
  "calmDownHours": [1, 2],
  "crimeOnAttack": true,
  "wanderRadius": 4
}
```
Copy all three files into `DFU_testing/DaggerfallUnity_Data/StreamingAssets/AdvancedNPCs/`.

- [ ] **Step 3: USER ACTION — run the manual checklist**

Give the user this checklist; they report pass/fail per line. After the session, grep `Player.log` for `[AdvancedNPCs]` and exceptions and attach the relevant lines to any failure.

1. Bram, Cora and Bors appear near their spots and wander.
2. Standing next to them, **Rest** and **Fast travel** both work.
3. Hit Bram once: he fights back, HUD shows the assault crime, guards appear.
4. Cora and Bors did **not** turn hostile from hitting Bram.
5. Hit Cora: she runs away immediately and hides once far away and out of sight. Hit Bors repeatedly: he never flees. Get Bram below a quarter health: he flees.
6. Leave the city, rest 48+ hours elsewhere, return: Bram is calm (log shows `calmed down`).
7. Save next to a living, damaged NPC; load that save: the NPC appears **once**, with the same damage.
8. Kill Cora with a single hit while she is calm (strong weapon or console god mode): murder crime is raised. Save, load, leave and return: she does not respawn.
9. Start a **new game** after step 8 and travel to Daggerfall: Cora is alive again.

- [ ] **Step 4: Fix any failures**

For each failing line: use superpowers:systematic-debugging, reproduce, fix in the smallest file that owns the behaviour, add a Core unit test if the cause was a rule in `HostilityRules`, `DefinitionParser`, `DefinitionCatalog` or `NpcStateTable`, rebuild, and re-run only the failed checklist lines plus line 7.

- [ ] **Step 5: Write `README.md` (author guide)**

`Assets/Game/Mods/AdvancedNPCs/README.md`:
````markdown
# Advanced NPCs — making your own NPC

1. Install the mod and enable **Advanced NPCs** in the DFU mod list.
2. Create the folder `DaggerfallUnity_Data/StreamingAssets/AdvancedNPCs/` in your game folder if it does not exist.
3. In game, stand outdoors where the NPC should live, open the console (`~`) and type `anpc_pos`.
4. Make a new file `my_npc.json` in that folder with an `id`, a `name`, and the two lines `anpc_pos` printed:

```json
{
  "id": "my_npc",
  "name": "My NPC",
  "location": { "region": "Daggerfall", "place": "Daggerfall" },
  "position": [12.5, 1, -3.25]
}
```

5. Restart the game. Check `Player.log` for lines starting with `[AdvancedNPCs]` — every problem names the file and field.

## Fields

| Field | Required | Default | Allowed values |
|---|---|---|---|
| `id` | yes | — | lowercase letters, digits, `_`; unique; never change it after release (it keys save data) |
| `name` | yes | — | any text |
| `location` | yes | — | from `anpc_pos` |
| `position` | yes | — | from `anpc_pos` |
| `baseClass` | no | `Spellsword` | Mage, Spellsword, Battlemage, Sorcerer, Healer, Nightblade, Bard, Burglar, Rogue, Acrobat, Thief, Assassin, Monk, Archer, Ranger, Barbarian, Warrior, Knight |
| `gender` | no | random | `Male`, `Female` |
| `bravery` | no | `Normal` | `Coward` (always flees), `Normal` (flees at low health), `Brave` (fights to the death) |
| `fleeHealthPercent` | no | `25` | 1–99, used by `Normal` |
| `calmDownHours` | no | `[6, 48]` | `[min, max]` in-game hours before a hostile NPC forgives the player |
| `crimeOnAttack` | no | `true` | `true`: attacking is assault, killing is murder |
| `wanderRadius` | no | `8` | metres around the spawn point; `0` stands still |

## Behaviour

- Calm NPCs never stop you resting or travelling.
- They only fight whoever attacked them.
- Killed NPCs stay dead in that save.
````

- [ ] **Step 6: Run the Core tests one last time**

Run the Test command. Expected: `result="Passed"`, `failed="0"`.

- [ ] **Step 7: Commit**

```bash
cd "F:/_Projects/Dagerfall/daggerfall-unity" && git add Assets/Game/Mods/AdvancedNPCs && git commit -m "docs: add Advanced NPC examples and author guide

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

## Spec coverage map

| Spec section | Task |
|---|---|
| §2 goals, success criteria 1–8 | 7 (behaviour), 8 (checklist 1–9) |
| §4 enemy stack + guard | 7 (`NpcBrain` guard, `CreateEnemy(..., Passive)`) |
| §5 engine facts | 7 (APIs listed in Interfaces) |
| §6.1–6.2 definition, loader | 2, 3, 6 |
| §6.3 state store | 5, 6 |
| §6.4 spawner | 7 |
| §6.5 brain | 7 |
| §6.6 rules | 4 |
| §7.1–7.8 behaviour | 7; rules unit-tested in 4 |
| §8 format + defaults | 2 (tests), 8 (README) |
| §9.1 spawning, duplicate prevention (`LoadID = 0`) | 7 |
| §9.2 saving, versioned data | 5, 6 |
| §9.3 `anpc_pos` | 2 (format), 6 (command) |
| §10 error handling | 2, 3 (messages), 6 (logging), 7 (spawn failure) |
| §12 testing | 1–5 unit, 6–7 smoke, 8 manual |
| §13 risks | 1 (test access), 7 (duplicates, ground, motor off), 8 (verification) |

Spec §9.1 step 4 (delayed ground re-alignment) is deferred: `CreateEnemy` already aligns to ground and `anpc_pos` records the player's real height. If checklist line 1 shows floating or sunken NPCs, add a one-frame-later `GameObjectHelper.AlignControllerToGround` call in `NpcBrain.Start` (Task 8 Step 4).

Spec §10 "location never matched → warning once" is intentionally reduced: `anpc_pos` prints exact names and the spawn log line confirms matches. Add the warning only if users report confusion.

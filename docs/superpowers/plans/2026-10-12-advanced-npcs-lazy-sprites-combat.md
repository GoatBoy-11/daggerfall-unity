# Advanced NPCs: on-demand sprites, retaliation, bravery, brave patrols — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Sprite sets load only while used (indexed at start, loaded one image per frame, unloaded 60 s after the last user); calm ANPCs fight back when swung at and missed; bravery gains Prudent and a chance-based Normal; calm Brave ANPCs attack enemies they see.

**Architecture:** Pure rules in `Scripts/Core` (`PngHeader`, `HostilityRules.Decide` with `FleeRoll`, `HostilityRules.AttackedWhileCalm`, `AggroRules`, parsing of `fleeChance`/`aggroRange`) with NUnit tests. Runtime: `SpriteLibrary` indexes at start and is ticked by `AdvancedNpcsMod.Update` to load/unload; `NpcSprite.Begin` waits invisibly for its set; `NpcCorpseSprite` holds its set; `NpcBrain` gets retaliation, the new flee decision and the brave patrol.

**Tech Stack:** C# for DFU's runtime mod compiler (Mono; no nested enums; every new `.cs` listed in `AdvancedNPCs.dfmod.json`), Unity 2019.4 runtime, NUnit (Core only, Unity 6 host), bash tools in `Tools~`.

**Spec:** `docs/superpowers/specs/2026-10-12-advanced-npcs-lazy-sprites-combat-design.md`

All paths are relative to `Assets/Game/Mods/AdvancedNPCs/` unless they start with `docs/`.

## Global Constraints

- Unload delay **60 s** real time after the last user (`SpriteLibrary.UnloadDelaySeconds`; the self-test may shorten it).
- Loader: **one image per frame**; ANPC invisible (vanilla billboard hidden, no quad) until its set is ready.
- Normal: rolls once below **fleeHealthPercent / 2**; `fleeChance` whole number **0–100, default 30**; Prudent flees below `fleeHealthPercent` (default 25).
- `aggroRange` number **1–50 m, default 15**; patrol check every **0.5 s**; only calm Brave ANPCs patrol.
- Foes: ANPCs with `IsEnemy`; vanilla `EnemyMotor.IsHostile` entities that are alive and not `CityWatch`/`PlayerAlly`; never ANPCs only angry at the player.
- `Bravery.Prudent` appended at the end of the enum.
- Every new `.cs` under `Scripts/` goes into `AdvancedNPCs.dfmod.json` `"Files"`. `build-mod.sh` also compiles `Editor/Tests`: a red unit test fails the build.
- Self-test steps are driven manually (`while (x.MoveNext()) yield return x.Current;`): never `yield return SomeIEnumerator()` directly.
- Mod version **0.12.0**.

## Review Focus

1. A sprite set whose last ANPC leaves while it is still loading must finish loading and then unload on its deadline → self-test "a set whose user left mid-load settles unloaded" (Task 4).
2. An ANPC killed while its set is still loading must not throw → `NpcBrain.OnDeath` already requires `sprite.Set != null`; `Set` stays null while pending (Task 4); covered by the mid-load self-test discarding a pending ANPC.
3. A sheet that became unreadable after startup must not crash the loader → `LoadNext` logs and drops it; a set left without idle becomes `Failed` and its ANPCs show the vanilla sprite (Task 4).
4. Normal's flee roll must not re-roll every frame → unit test `Normal_RollsOnceWhileBelow` (Task 2).
5. A brave ANPC must not attack the city watch or an ANPC only angry at the player → `AggroRulesTests` rows + self-test "a brave ANPC ignores an ANPC only angry at the player" (Tasks 3, 5).

## Verification commands

- Unit: `bash Tools~/run-tests.sh` → `result="Passed" ...`. Baseline 622.
- `bash Tools~/compile-check.sh` → OK; `bash Tools~/runtime-check.sh` → two OK lines.
- Build: `bash Tools~/build-mod.sh --no-examples`, then from repo root `git restore Assets/AddressableAssetsData/`.
- In-game: `bash Tools~/selftest.sh | grep -E "FAIL|SELFTEST DONE"`; full list: `grep "SELFTEST FAIL" "$USERPROFILE/AppData/LocalLow/Daggerfall Workshop/Daggerfall Unity/Player.log"`. Baseline 162/162. Known flaky pair: "magic town caster ... past the town edge" (re-run once if only those fail).
- In-game runs open windows: run build + self-test with the Bash tool's `run_in_background`.

---

### Task 1: `PngHeader` (Core)

**Files:** Create `Scripts/Core/PngHeader.cs`; Test create `Editor/Tests/PngHeaderTests.cs`; Modify `AdvancedNPCs.dfmod.json` (add `"Assets/Game/Mods/AdvancedNPCs/Scripts/Core/PngHeader.cs",` after the `Scripts/Core/NpcState.cs` entry).

**Interfaces:** Produces `PngHeader.TryReadSize(byte[] head, out int width, out int height) : bool`, `PngHeader.Length = 24`.

- [ ] **Step 1: Failing test** — `Editor/Tests/PngHeaderTests.cs`:

```csharp
using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    /// <summary>Sheet sizes read from the PNG header at startup (spec lazy-sprites §A).</summary>
    public class PngHeaderTests
    {
        static byte[] Head(int w, int h)
        {
            byte[] b = { 137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R',
                0, 0, 0, 0, 0, 0, 0, 0 };
            b[16] = (byte)(w >> 24); b[17] = (byte)(w >> 16); b[18] = (byte)(w >> 8); b[19] = (byte)w;
            b[20] = (byte)(h >> 24); b[21] = (byte)(h >> 16); b[22] = (byte)(h >> 8); b[23] = (byte)h;
            return b;
        }

        [Test]
        public void ReadsWidthAndHeight()
        {
            int w, h;
            Assert.IsTrue(PngHeader.TryReadSize(Head(1320, 2048), out w, out h));
            Assert.AreEqual(1320, w);
            Assert.AreEqual(2048, h);
        }

        [Test]
        public void RejectsShortOrForeignData()
        {
            int w, h;
            Assert.IsFalse(PngHeader.TryReadSize(new byte[10], out w, out h));
            Assert.IsFalse(PngHeader.TryReadSize(null, out w, out h));
            byte[] notPng = Head(16, 16);
            notPng[1] = (byte)'X';
            Assert.IsFalse(PngHeader.TryReadSize(notPng, out w, out h));
            byte[] noIhdr = Head(16, 16);
            noIhdr[12] = (byte)'X';
            Assert.IsFalse(PngHeader.TryReadSize(noIhdr, out w, out h));
            Assert.IsFalse(PngHeader.TryReadSize(Head(0, 16), out w, out h));
        }
    }
}
```

- [ ] **Step 2: RED** — `bash Tools~/run-tests.sh` → compile error `'PngHeader' does not exist`.
- [ ] **Step 3: Implement** — `Scripts/Core/PngHeader.cs`:

```csharp
namespace AdvancedNPCs.Core
{
    /// <summary>Width and height from a PNG's first 24 bytes (signature + IHDR), without decoding the image.</summary>
    public static class PngHeader
    {
        public const int Length = 24;
        static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };

        public static bool TryReadSize(byte[] head, out int width, out int height)
        {
            width = height = 0;
            if (head == null || head.Length < Length)
                return false;
            for (int i = 0; i < Signature.Length; i++)
            {
                if (head[i] != Signature[i])
                    return false;
            }
            if (head[12] != 'I' || head[13] != 'H' || head[14] != 'D' || head[15] != 'R')
                return false;
            width = (head[16] << 24) | (head[17] << 16) | (head[18] << 8) | head[19];
            height = (head[20] << 24) | (head[21] << 16) | (head[22] << 8) | head[23];
            return width > 0 && height > 0;
        }
    }
}
```

  Add the dfmod.json entry.
- [ ] **Step 4: GREEN** — run-tests Passed 624; compile-check, runtime-check OK.
- [ ] **Step 5: Commit** `feat(anpc): read sprite sheet sizes from the PNG header`.

---

### Task 2: Bravery — Prudent, chance-based Normal, `fleeChance`

**Files:** Modify `Scripts/Core/NpcDefinition.cs`, `Scripts/Core/HostilityRules.cs`, `Scripts/Core/DefinitionParser.cs`, `Scripts/Runtime/NpcBrain.cs` (the two `Decide` calls + one field); Test modify `Editor/Tests/HostilityRulesTests.cs`, create `Editor/Tests/BraveryTests.cs`.

**Interfaces:** Produces `Bravery.Prudent`; `public struct FleeRoll { public bool Below; public bool Flee; }`; `HostilityRules.Decide(Bravery, float healthFraction, int fleeHealthPercent, int fleeChance, ref FleeRoll roll, double random01) : CombatChoice`; `NpcDefinition.FleeChance` (int, default 30).

- [ ] **Step 1: Failing tests** — in `Editor/Tests/HostilityRulesTests.cs` replace `Coward_AlwaysFlees`, `Brave_AlwaysFights`, `Normal_FleesBelowThreshold` with:

```csharp
        static CombatChoice Once(Bravery b, float health, int chance = 30, double random01 = 0.5)
        {
            FleeRoll roll = new FleeRoll();
            return HostilityRules.Decide(b, health, 25, chance, ref roll, random01);
        }

        [TestCase(1.0f)]
        [TestCase(0.01f)]
        public void Coward_AlwaysFlees(float health)
        {
            Assert.AreEqual(CombatChoice.Flee, Once(Bravery.Coward, health));
        }

        [TestCase(1.0f)]
        [TestCase(0.01f)]
        public void Brave_AlwaysFights(float health)
        {
            Assert.AreEqual(CombatChoice.Fight, Once(Bravery.Brave, health, 100, 0.0));
        }

        [TestCase(1.0f, CombatChoice.Fight)]
        [TestCase(0.25f, CombatChoice.Fight)]
        [TestCase(0.24f, CombatChoice.Flee)]
        [TestCase(0.0f, CombatChoice.Flee)]
        public void Prudent_FleesBelowThreshold(float health, CombatChoice expected)
        {
            Assert.AreEqual(expected, Once(Bravery.Prudent, health));
        }

        [Test]
        public void Normal_UsesHalfTheThresholdAndTheChance()
        {
            Assert.AreEqual(CombatChoice.Fight, Once(Bravery.Normal, 0.20f, 100, 0.0));   // above 12.5 %
            Assert.AreEqual(CombatChoice.Flee, Once(Bravery.Normal, 0.10f, 100, 0.99));   // chance 100
            Assert.AreEqual(CombatChoice.Fight, Once(Bravery.Normal, 0.10f, 0, 0.0));     // chance 0
            Assert.AreEqual(CombatChoice.Flee, Once(Bravery.Normal, 0.10f, 30, 0.29));    // rolled under 30
            Assert.AreEqual(CombatChoice.Fight, Once(Bravery.Normal, 0.10f, 30, 0.30));
        }

        [Test]
        public void Normal_RollsOnceWhileBelow()
        {
            FleeRoll roll = new FleeRoll();
            Assert.AreEqual(CombatChoice.Fight, HostilityRules.Decide(Bravery.Normal, 0.10f, 25, 30, ref roll, 0.9));
            // Later frames with lucky numbers keep the first answer while still below the line.
            Assert.AreEqual(CombatChoice.Fight, HostilityRules.Decide(Bravery.Normal, 0.05f, 25, 30, ref roll, 0.0));
            // Healed above the line, then below again: a new roll.
            Assert.AreEqual(CombatChoice.Fight, HostilityRules.Decide(Bravery.Normal, 0.50f, 25, 30, ref roll, 0.0));
            Assert.AreEqual(CombatChoice.Flee, HostilityRules.Decide(Bravery.Normal, 0.10f, 25, 30, ref roll, 0.0));
        }
```

  Create `Editor/Tests/BraveryTests.cs`:

```csharp
using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    /// <summary>"bravery", "fleeChance" and "aggroRange" in npc.json (spec lazy-sprites §C-D).</summary>
    public class BraveryTests
    {
        const string Unique =
            "{ \"kind\": \"unique\", \"name\": \"Grub\", " +
            "\"location\": { \"region\": \"Daggerfall\", \"place\": \"Daggerfall\" }, " +
            "\"position\": [1.5, 2, -3] }";

        static ParseResult With(string extraFields)
        {
            return DefinitionParser.ParseFolder("grub", Unique.Substring(0, Unique.Length - 1) + ", " + extraFields + " }");
        }

        [Test]
        public void Defaults_NormalWithThirtyPercent()
        {
            ParseResult r = DefinitionParser.ParseFolder("grub", Unique);
            Assert.IsTrue(r.Ok, r.Error);
            Assert.AreEqual(Bravery.Normal, r.Definition.Bravery);
            Assert.AreEqual(30, r.Definition.FleeChance);
        }

        [Test]
        public void Prudent_Parses()
        {
            ParseResult r = With("\"bravery\": \"prudent\"");
            Assert.IsTrue(r.Ok, r.Error);
            Assert.AreEqual(Bravery.Prudent, r.Definition.Bravery);
        }

        [Test]
        public void FleeChance_Reads()
        {
            ParseResult r = With("\"fleeChance\": 75");
            Assert.IsTrue(r.Ok, r.Error);
            Assert.AreEqual(75, r.Definition.FleeChance);
        }

        [TestCase("-1")]
        [TestCase("101")]
        [TestCase("12.5")]
        [TestCase("\"half\"")]
        public void FleeChance_RejectsBadValues(string value)
        {
            ParseResult r = With("\"fleeChance\": " + value);
            Assert.IsFalse(r.Ok);
            StringAssert.Contains("fleeChance", r.Error);
        }

        [Test]
        public void UnknownBravery_ListsAllFour()
        {
            ParseResult r = With("\"bravery\": \"Reckless\"");
            Assert.IsFalse(r.Ok);
            StringAssert.Contains("Coward, Normal, Prudent or Brave", r.Error);
        }
    }
}
```

- [ ] **Step 2: RED** — run-tests → compile errors (`FleeRoll`, `Bravery.Prudent`, `FleeChance`).
- [ ] **Step 3: Implement**
  - `NpcDefinition.cs`: enum `Coward, Normal, Brave, Prudent,` (appended). After `public int FleeHealthPercent;`:

```csharp
        /// <summary>"fleeChance": % chance a Normal ANPC flees once below half of FleeHealthPercent (0-100).</summary>
        public int FleeChance = 30;
```

  - `HostilityRules.cs`: add before `public static class HostilityRules`:

```csharp
    /// <summary>A Normal ANPC's one flee roll while its health is below the line (kept per ANPC, not saved).</summary>
    public struct FleeRoll
    {
        public bool Below;
        public bool Flee;
    }
```

    and replace `Decide`:

```csharp
        /// <summary>
        /// Fight or flee for the bravery and current health fraction (0..1). Prudent flees below fleeHealthPercent;
        /// Normal rolls once (fleeChance %) when it first drops below half of it and keeps that answer while below.
        /// </summary>
        public static CombatChoice Decide(Bravery bravery, float healthFraction, int fleeHealthPercent, int fleeChance,
            ref FleeRoll roll, double random01)
        {
            switch (bravery)
            {
                case Bravery.Coward:
                    return CombatChoice.Flee;
                case Bravery.Brave:
                    return CombatChoice.Fight;
                case Bravery.Prudent:
                    return healthFraction * 100f < fleeHealthPercent ? CombatChoice.Flee : CombatChoice.Fight;
                default:
                    if (healthFraction * 100f >= fleeHealthPercent / 2f)
                    {
                        roll.Below = false;
                        return CombatChoice.Fight;
                    }
                    if (!roll.Below)
                    {
                        roll.Below = true;
                        roll.Flee = random01 * 100.0 < fleeChance;
                    }
                    return roll.Flee ? CombatChoice.Flee : CombatChoice.Fight;
            }
        }
```

  - `DefinitionParser.cs`: in `SharedKeys` add `"fleeChance", "aggroRange",` after `"fleeHealthPercent",`; bravery message → `"must be Coward, Normal, Prudent or Brave (got \""`; after the `fleeHealthPercent` checks:

```csharp
            double fleeChance;
            if ((problem = FieldReader.Number(o, "fleeChance", 30, out fleeChance)) != null || (o.ContainsKey("fleeChance") && o["fleeChance"] == null) ||
                fleeChance != Math.Floor(fleeChance) || fleeChance < 0 || fleeChance > 100)
                return Problem(r, file, "fleeChance", "must be a whole number from 0 to 100");
```

    and next to `d.FleeHealthPercent = (int)flee;`: `d.FleeChance = (int)fleeChance;`.
  - `NpcBrain.cs`: field after `float calmCheckTimer;`: `FleeRoll fleeRoll;         // Normal's one flee roll while badly hurt`; both `Decide` calls become `HostilityRules.Decide(def.Bravery, HealthFraction(), def.FleeHealthPercent, def.FleeChance, ref fleeRoll, rng.NextDouble())`.
- [ ] **Step 4: GREEN** — run-tests Passed; compile-check, runtime-check OK.
- [ ] **Step 5: Commit** `feat(anpc): Prudent bravery and a chance-based Normal (fleeChance)`.

---

### Task 3: `AttackedWhileCalm`, `AggroRules`, `aggroRange`

**Files:** Modify `Scripts/Core/HostilityRules.cs`, `Scripts/Core/NpcDefinition.cs`, `Scripts/Core/DefinitionParser.cs`; Create `Scripts/Core/AggroRules.cs` (dfmod.json entry after `Scripts/Core/Bearing.cs` or with the other Core entries); Test create `Editor/Tests/AggroRulesTests.cs`, add to `HostilityRulesTests.cs` and `BraveryTests.cs`.

**Interfaces:** Produces `HostilityRules.AttackedWhileCalm(bool calm, bool pursuitTimerSet, bool targetIsOther, bool targetAlive) : bool`; `AggroRules.DefaultRange = 15f`, `MinRange = 1`, `MaxRange = 50`, `CheckInterval = 0.5f`, `AggroRules.IsFoe(bool isAnpc, bool anpcIsEnemy, bool vanillaHostile, bool watchOrAlly, bool alive) : bool`; `NpcDefinition.AggroRange` (float, default 15).

- [ ] **Step 1: Failing tests** — `Editor/Tests/AggroRulesTests.cs`:

```csharp
using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    /// <summary>Who a calm Brave ANPC attacks on sight (spec lazy-sprites §D).</summary>
    public class AggroRulesTests
    {
        [TestCase(true, true, false, false, true, true, TestName = "IsFoe_EnemyAnpc")]
        [TestCase(true, false, false, false, true, false, TestName = "IsFoe_AnpcOnlyAngryAtPlayer")]
        [TestCase(true, true, false, false, false, false, TestName = "IsFoe_DeadEnemyAnpc")]
        [TestCase(false, false, true, false, true, true, TestName = "IsFoe_HostileVanillaEnemy")]
        [TestCase(false, false, true, true, true, false, TestName = "IsFoe_CityWatchOrAlly")]
        [TestCase(false, false, false, false, true, false, TestName = "IsFoe_PeacefulVanilla")]
        public void IsFoe(bool isAnpc, bool anpcIsEnemy, bool vanillaHostile, bool watchOrAlly, bool alive, bool expected)
        {
            Assert.AreEqual(expected, AggroRules.IsFoe(isAnpc, anpcIsEnemy, vanillaHostile, watchOrAlly, alive));
        }
    }
}
```

  Add to `HostilityRulesTests.cs`:

```csharp
        [TestCase(true, true, true, true, true)]
        [TestCase(false, true, true, true, false)]
        [TestCase(true, false, true, true, false)]
        [TestCase(true, true, false, true, false)]
        [TestCase(true, true, true, false, false)]
        public void AttackedWhileCalm(bool calm, bool timerSet, bool targetIsOther, bool targetAlive, bool expected)
        {
            Assert.AreEqual(expected, HostilityRules.AttackedWhileCalm(calm, timerSet, targetIsOther, targetAlive));
        }
```

  Add to `BraveryTests.cs`:

```csharp
        [Test]
        public void AggroRange_DefaultsAndReads()
        {
            Assert.AreEqual(15f, DefinitionParser.ParseFolder("grub", Unique).Definition.AggroRange, 1e-6);
            ParseResult r = With("\"aggroRange\": 8.5");
            Assert.IsTrue(r.Ok, r.Error);
            Assert.AreEqual(8.5f, r.Definition.AggroRange, 1e-6);
        }

        [TestCase("0.5")]
        [TestCase("51")]
        [TestCase("\"far\"")]
        public void AggroRange_RejectsBadValues(string value)
        {
            ParseResult r = With("\"aggroRange\": " + value);
            Assert.IsFalse(r.Ok);
            StringAssert.Contains("aggroRange", r.Error);
        }
```

- [ ] **Step 2: RED** — run-tests → compile errors (`AggroRules`, `AttackedWhileCalm`, `AggroRange`).
- [ ] **Step 3: Implement**
  - `Scripts/Core/AggroRules.cs`:

```csharp
namespace AdvancedNPCs.Core
{
    /// <summary>Brave patrols (spec lazy-sprites §D): who a calm Brave ANPC attacks on sight. No Unity or DFU types.</summary>
    public static class AggroRules
    {
        public const float DefaultRange = 15f;
        public const double MinRange = 1, MaxRange = 50;
        /// <summary>Seconds between looks around.</summary>
        public const float CheckInterval = 0.5f;

        /// <summary>
        /// An enemy ANPC (hostile attitude), or a vanilla enemy hostile to the player that is not the city watch or a
        /// player ally; alive. ANPCs only angry at the player are not foes.
        /// </summary>
        public static bool IsFoe(bool isAnpc, bool anpcIsEnemy, bool vanillaHostile, bool watchOrAlly, bool alive)
        {
            if (!alive)
                return false;
            return isAnpc ? anpcIsEnemy : vanillaHostile && !watchOrAlly;
        }
    }
}
```

  - `HostilityRules.cs`:

```csharp
        /// <summary>
        /// A calm ANPC was swung at: DFU's MakeEnemyHostileToAttacker set the pursuit timer and a non-player target
        /// (on a hit or a miss). Only an attack sets that timer while the motor is off.
        /// </summary>
        public static bool AttackedWhileCalm(bool calm, bool pursuitTimerSet, bool targetIsOther, bool targetAlive)
        {
            return calm && pursuitTimerSet && targetIsOther && targetAlive;
        }
```

  - `NpcDefinition.cs` after `FleeChance`:

```csharp
        /// <summary>"aggroRange": metres a calm Brave ANPC looks for enemies (1-50).</summary>
        public float AggroRange = AggroRules.DefaultRange;
```

  - `DefinitionParser.cs` after the `fleeChance` check:

```csharp
            double aggro;
            if ((problem = FieldReader.Number(o, "aggroRange", AggroRules.DefaultRange, out aggro)) != null || (o.ContainsKey("aggroRange") && o["aggroRange"] == null) ||
                double.IsNaN(aggro) || aggro < AggroRules.MinRange || aggro > AggroRules.MaxRange)
                return Problem(r, file, "aggroRange", "must be a number from 1 to 50 (metres)");
```

    and `d.AggroRange = (float)aggro;` next to `d.FleeChance`.
- [ ] **Step 4: GREEN** — run-tests Passed; compile-check, runtime-check OK.
- [ ] **Step 5: Commit** `feat(anpc): retaliation and brave-patrol rules, aggroRange`.

---

### Task 4: On-demand sprite loading

**Files:** Modify `Scripts/Runtime/SpriteLibrary.cs`, `Scripts/Runtime/AdvancedNpcsMod.cs`, `Scripts/Runtime/NpcSprite.cs`, `Scripts/Runtime/NpcCorpseSprite.cs`, `Scripts/Runtime/NpcGait.cs`, `Scripts/Runtime/NpcSpawner.cs`, `Scripts/Runtime/SelfTest.cs`, `Scripts/Runtime/SelfTestMagic.cs`; Create `Scripts/Runtime/SelfTestSprites.cs` (dfmod.json entry after `SelfTestReplies.cs`).

**Interfaces:**
- Consumes `PngHeader` (Task 1).
- Produces `LoadedSpriteSet.Dir/HasDeathStatic/IsReady/Failed/Users`, `LoadedSpriteSet.Acquire()/Release()`; `SpriteLibrary.IndexSet(string label, string dir) : LoadedSpriteSet` (public static), `SpriteLibrary.Tick(float now)`, `SpriteLibrary.UnloadDelaySeconds` (public static float = 60); `NpcSprite.Begin(LoadedSpriteSet, float worldHeight, float verticalScale = 1f)`, `NpcSprite.ChosenSet`, `NpcSprite.Pending`.

- [ ] **Step 1: Failing self-test** — create `Scripts/Runtime/SelfTestSprites.cs`:

```csharp
using System.Collections;
using System.IO;
using UnityEngine;
using DaggerfallWorkshop;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>Self-test: sprite sets load while used and unload after (spec lazy-sprites §A).</summary>
    public partial class SelfTest
    {
        IEnumerator LazySpriteSteps(DaggerfallLocation location, Transform player)
        {
            string dir = Path.Combine(Application.temporaryCachePath, "anpc_selftest_lazy/sprites");
            Directory.CreateDirectory(dir);
            foreach (string n in new[] { "idle", "walk", "death" })
                File.WriteAllBytes(Path.Combine(dir, n + ".png"), SolidTexture(32, 32 * 8, new Color32(90, 160, 90, 255)).EncodeToPNG());
            File.WriteAllText(Path.Combine(dir, SpriteLibrary.SetFile),
                "{ \"pixelsPerUnit\": 10, \"cellHeight\": 32, \"groundY\": 4, \"fps\": 8, \"anchorX\": \"centre\", " +
                "\"animations\": { \"idle\": { \"cellWidth\": 16, \"frames\": 2 }, \"walk\": { \"cellWidth\": 16, \"frames\": 2 }, " +
                "\"death\": { \"cellWidth\": 16, \"frames\": 2 } } }");
            LoadedSpriteSet lazy = SpriteLibrary.IndexSet("selftest_lazy/sprites", dir);
            Check("a sprite set is indexed without loading its sheets", lazy != null && !lazy.IsReady && lazy.Sheets.Count == 0,
                lazy == null ? "not indexed" : "ready=" + lazy.IsReady + ", sheets=" + lazy.Sheets.Count);
            if (lazy == null)
                yield break;
            mod.Sprites.Add("selftest_lazy", lazy);
            float oldDelay = SpriteLibrary.UnloadDelaySeconds;
            SpriteLibrary.UnloadDelaySeconds = 0.5f;
            try
            {
                NpcBrain b = SpawnDefinition(TestDefinition("selftest_lazy", Bravery.Normal, location, player, 0f), location);
                NpcSprite sprite = b.GetComponent<NpcSprite>();
                bool pendingAtSpawn = sprite != null && sprite.Pending && !sprite.Showing;
                float until = Time.time + 3f;
                while (Time.time < until && sprite != null && sprite.Pending)
                    yield return null;
                yield return null;
                Check("an ANPC waits invisibly for its set, then shows it", pendingAtSpawn && sprite != null && !sprite.Pending &&
                    sprite.Showing && lazy.IsReady && lazy.Users == 1,
                    "pending at spawn=" + pendingAtSpawn + ", ready=" + lazy.IsReady + ", users=" + lazy.Users);
                NpcBrain.Discard(b);
                yield return new WaitForSeconds(1.5f);
                Check("a set unloads after its last user", !lazy.IsReady && lazy.Sheets.Count == 0 && lazy.Users == 0,
                    "ready=" + lazy.IsReady + ", sheets=" + lazy.Sheets.Count + ", users=" + lazy.Users);

                // Gone while its set is still loading: no error, and the set still settles unloaded.
                NpcDefinition quickDef = TestDefinition("selftest_lazy_quick", Bravery.Normal, location, player, 1f);
                quickDef.Folder = "selftest_lazy";
                NpcBrain quick = SpawnDefinition(quickDef, location);
                NpcBrain.Discard(quick);
                yield return new WaitForSeconds(1.5f);
                Check("a set whose user left mid-load settles unloaded", !lazy.IsReady && lazy.Sheets.Count == 0 && lazy.Users == 0,
                    "ready=" + lazy.IsReady + ", sheets=" + lazy.Sheets.Count + ", users=" + lazy.Users);
            }
            finally
            {
                SpriteLibrary.UnloadDelaySeconds = oldDelay;
                mod.Sprites.Remove("selftest_lazy");
            }
        }
    }
}
```

  In `SelfTest.cs` `Steps()`, right before the `GaitSteps` block:

```csharp
            IEnumerator lazySprites = LazySpriteSteps(location, playerTransform);
            while (lazySprites.MoveNext())
                yield return lazySprites.Current;
```

  dfmod.json: `"Assets/Game/Mods/AdvancedNPCs/Scripts/Runtime/SelfTestSprites.cs",` after `SelfTestReplies.cs`.
- [ ] **Step 2: RED** — `bash Tools~/compile-check.sh` → errors (`IndexSet`, `UnloadDelaySeconds`, `Pending`, `IsReady`, `Users`).
- [ ] **Step 3: `SpriteLibrary`** (`Scripts/Runtime/SpriteLibrary.cs`)
  - `LoadedSpriteSet` add:

```csharp
        /// <summary>Folder the sheets are read from; null for sets built from textures (self-test), which never unload.</summary>
        public string Dir;
        public bool HasDeathStatic;
        /// <summary>Every sheet (and death_static.png) is loaded and uploaded.</summary>
        public bool IsReady;
        /// <summary>Loading left no usable idle sheet: its ANPCs keep the vanilla sprite.</summary>
        public bool Failed;
        /// <summary>Live ANPC sprites and corpses using the set.</summary>
        public int Users;
        internal SpriteLibrary Library;
        internal float UnloadAt = -1f;
        internal Queue<string> Pending;
        internal float IdleHeight;

        public void Acquire()
        {
            if (Library != null)
                Library.Acquire(this);
        }

        public void Release()
        {
            if (Library != null)
                Library.Release(this);
        }
```

  - `SpriteLibrary` add `public static float UnloadDelaySeconds = 60f;` and `readonly List<LoadedSpriteSet> loading = new List<LoadedSpriteSet>();`.
  - `Load`: `LoadSet(...)` → `IndexSet(...)`; log `"Indexed " + library.Count + " sprite set(s) in " + watch.ElapsedMilliseconds + " ms."`.
  - `Add`: after `sets.Add(set);` → `if (set.Dir != null) set.Library = this;`.
  - `FromTextures`: `loaded.IsReady = true;`.
  - Rename `LoadSet` → `public static LoadedSpriteSet IndexSet(string label, string dir)`; keep the JSON parse/log code; in the animation loop, after the `StateOf` skip, replace the `LoadPng`/size-check block with:

```csharp
                int w, h;
                string headerProblem = ReadSize(Path.Combine(dir, a.Name + ".png"), out w, out h);
                if (headerProblem != null)
                {
                    AdvancedNpcsMod.LogError(label + "/" + a.Name + ".png: file: " + headerProblem + " (sheet skipped)");
                    skipped.Add(a.Name);
                    continue;
                }
                string sizeError = SpriteSetParser.CheckSheetSize(label, a, set.CellHeight, w, h);
                if (sizeError != null)
                {
                    AdvancedNpcsMod.LogError(sizeError + " (sheet skipped)");
                    skipped.Add(a.Name);
                    continue;
                }
```

    keep the removal of skipped animations and the `IsValid` check (log, `return null`; no textures to destroy), and replace everything after it (death_static load, StandingHeight, Finish loop) with:

```csharp
            loaded.Dir = dir;
            loaded.HasDeathStatic = File.Exists(Path.Combine(dir, DeathStaticFile));
            return loaded;
```

  - Add `ReadSize`, `Acquire`, `Release`, `Tick`, `LoadNext`, `FinishLoading`, `Unload`, `DestroyTextures`:

```csharp
        /// <summary>Null and the size, or what is wrong with the file.</summary>
        static string ReadSize(string path, out int width, out int height)
        {
            width = height = 0;
            if (!File.Exists(path))
                return "missing";
            try
            {
                byte[] head = new byte[PngHeader.Length];
                using (FileStream f = File.OpenRead(path))
                {
                    if (f.Read(head, 0, head.Length) != head.Length)
                        return "not a readable PNG";
                }
                return PngHeader.TryReadSize(head, out width, out height) ? null : "not a readable PNG";
            }
            catch (Exception e)
            {
                return "could not read (" + e.Message + ")";
            }
        }

        /// <summary>One more user; starts loading an indexed set.</summary>
        public void Acquire(LoadedSpriteSet s)
        {
            s.Users++;
            s.UnloadAt = -1f;
            if (s.IsReady || s.Failed || s.Pending != null)
                return;
            s.Pending = new Queue<string>();
            List<string> names = new List<string>(s.Set.Animations.Keys);
            names.Sort(StringComparer.Ordinal);
            foreach (string n in names)
                s.Pending.Enqueue(n);
            if (s.HasDeathStatic)
                s.Pending.Enqueue(DeathStaticFile);
            s.IdleHeight = 0f;
            loading.Add(s);
        }

        /// <summary>One user fewer; the last one starts the unload countdown.</summary>
        public void Release(LoadedSpriteSet s)
        {
            if (s.Users > 0)
                s.Users--;
            if (s.Users == 0 && s.Dir != null)
                s.UnloadAt = Time.unscaledTime + UnloadDelaySeconds;
        }

        /// <summary>Every frame (AdvancedNpcsMod.Update): loads one image, unloads sets whose countdown ran out.</summary>
        public void Tick(float now)
        {
            if (loading.Count > 0)
            {
                LoadedSpriteSet s = loading[0];
                if (s.Pending.Count > 0)
                    LoadNext(s);
                if (s.Pending.Count == 0)
                {
                    loading.RemoveAt(0);
                    FinishLoading(s);
                }
            }
            foreach (List<LoadedSpriteSet> sets in byFolder.Values)
            {
                foreach (LoadedSpriteSet s in sets)
                {
                    if (s.Users == 0 && s.UnloadAt >= 0f && now >= s.UnloadAt && s.Pending == null && s.IsReady)
                        Unload(s);
                }
            }
        }

        void LoadNext(LoadedSpriteSet s)
        {
            string name = s.Pending.Dequeue();
            if (name == DeathStaticFile)
            {
                s.DeathStatic = LoadPng(s.Label + "/" + DeathStaticFile, Path.Combine(s.Dir, DeathStaticFile), false);
                if (s.DeathStatic != null)
                    Finish(s.DeathStatic);
                return;
            }
            SpriteAnimation a;
            if (!s.Set.Animations.TryGetValue(name, out a))
                return;
            Texture2D t = LoadPng(s.Label + "/" + name + ".png", Path.Combine(s.Dir, name + ".png"), true);
            string sizeError = t != null ? SpriteSetParser.CheckSheetSize(s.Label, a, s.Set.CellHeight, t.width, t.height) : null;
            if (t == null || sizeError != null)
            {
                if (sizeError != null)
                    AdvancedNpcsMod.LogError(sizeError + " (sheet skipped)");
                if (t != null)
                    UnityEngine.Object.Destroy(t);
                s.Set.Animations.Remove(name);
                return;
            }
            if (s.Set.StandingHeight <= 0 && SpriteStates.StateOf(name) == SpriteStates.Idle)
                s.IdleHeight = Math.Max(s.IdleHeight, IdleHeight(s.Set, a, t));
            Finish(t);
            s.Sheets[name] = t;
        }

        void FinishLoading(LoadedSpriteSet s)
        {
            s.Pending = null;
            if (!SpriteStates.IsValid(s.Set))
            {
                AdvancedNpcsMod.LogError(s.Label + ": no usable idle sheet; its ANPCs keep the vanilla sprite");
                DestroyTextures(s);
                s.Failed = true;
                return;
            }
            s.StandingHeightPx = s.Set.StandingHeight > 0 ? s.Set.StandingHeight : s.IdleHeight;
            s.IsReady = true;
            if (s.Users == 0)
                s.UnloadAt = Time.unscaledTime + UnloadDelaySeconds;
        }

        void Unload(LoadedSpriteSet s)
        {
            DestroyTextures(s);
            s.IsReady = false;
            s.UnloadAt = -1f;
            AdvancedNpcsMod.Log(s.Label + ": sprite set unloaded (no one uses it).");
        }

        static void DestroyTextures(LoadedSpriteSet s)
        {
            foreach (Texture2D t in s.Sheets.Values)
                UnityEngine.Object.Destroy(t);
            s.Sheets.Clear();
            if (s.DeathStatic != null)
                UnityEngine.Object.Destroy(s.DeathStatic);
            s.DeathStatic = null;
        }
```

  - Split `StandingHeight`: move the per-animation body into `static float IdleHeight(SpriteSet set, SpriteAnimation a, Texture2D t)` (local `best` starts at 0, returns it as float); `StandingHeight(set, sheets)` becomes `if (set.StandingHeight > 0) return set.StandingHeight;` then the max of `IdleHeight` over idle variants present in `sheets` (still used by `FromTextures`).
- [ ] **Step 4: Tick** — `AdvancedNpcsMod.cs` add:

```csharp
        void Update()
        {
            if (Sprites != null)
                Sprites.Tick(Time.unscaledTime);
        }
```

- [ ] **Step 5: `NpcSprite` pending** — fields after `NpcGait gait;`:

```csharp
        LoadedSpriteSet waitingSet;     // chosen, still loading: nothing drawn yet
        float waitingHeight;
        float waitingScale = 1f;
        LoadedSpriteSet heldSet;        // acquired by Begin, released when this sprite goes
```

  members:

```csharp
        /// <summary>The set this person wears, loaded or still loading.</summary>
        public LoadedSpriteSet ChosenSet
        {
            get { return set != null ? set : waitingSet; }
        }

        /// <summary>Waiting for its set to load (invisible meanwhile).</summary>
        public bool Pending
        {
            get { return waitingSet != null; }
        }

        /// <summary>Spawner entry: shows the set now if it is loaded, else waits for it with the vanilla picture hidden.</summary>
        public void Begin(LoadedSpriteSet loaded, float worldHeight, float verticalScale = 1f)
        {
            heldSet = loaded;
            loaded.Acquire();
            if (loaded.IsReady)
            {
                Init(loaded, worldHeight, verticalScale);
                return;
            }
            waitingSet = loaded;
            waitingHeight = worldHeight;
            waitingScale = verticalScale;
            DaggerfallEnemy enemy = GetComponent<DaggerfallEnemy>();
            MobileUnit unit = enemy != null ? enemy.MobileUnit : null;
            vanilla = unit != null ? unit.GetComponent<MeshRenderer>() : null;
            if (vanilla != null)
                vanilla.forceRenderingOff = true;
        }
```

  at the top of `Update()` (before `if (set == null) return;`):

```csharp
            if (waitingSet != null)
            {
                if (waitingSet.IsReady)
                {
                    LoadedSpriteSet ready = waitingSet;
                    waitingSet = null;
                    Init(ready, waitingHeight, waitingScale);
                }
                else if (waitingSet.Failed)
                {
                    waitingSet = null;
                    if (vanilla != null)
                        vanilla.forceRenderingOff = false;
                }
                else if (vanilla != null)
                {
                    vanilla.forceRenderingOff = true;
                }
                return;
            }
```

  `OnDestroy`: add first `if (heldSet != null) heldSet.Release();`.
- [ ] **Step 6: corpse, gait, spawner, existing tests**
  - `NpcCorpseSprite.Spawn`: after `corpse.set = from.Set;` add `corpse.set.Acquire();`; `OnDestroy` first line: `if (set != null) set.Release();`.
  - `NpcGait.Init`: `turret = sprite != null && sprite.ChosenSet != null && SpriteStates.IsStationary(sprite.ChosenSet.Set);`.
  - `NpcSpawner`: `sprite.Init(spriteSet, height, def.SpriteHeightScale);` → `sprite.Begin(spriteSet, height, def.SpriteHeightScale);`.
  - `SelfTestMagic.cs` sample checks: `sprite.Set.Set.Animations.Count` → `sprite.ChosenSet.Set.Animations.Count`; `sprite.Set.Label` → `sprite.ChosenSet.Label`.
  - `SelfTest.cs` looks: in the first block after `NpcBrain look = Make("selftest_look", ...)` replace `yield return new WaitForSeconds(1f);` with

```csharp
                NpcSprite lookSprite = look.GetComponent<NpcSprite>();
                float lookUntil = Time.time + 10f;
                while (lookSprite != null && lookSprite.Pending && Time.time < lookUntil)
                    yield return null;
                yield return new WaitForSeconds(1f);
```

    and in the loop after `NpcBrain each = Make(...)` the same with `eachSprite`/`eachUntil`/`each`.
- [ ] **Step 7: Verify** — compile-check, runtime-check, run-tests; build `--no-examples`; self-test (background) → 4 new checks pass, total 166/166; `grep -o "Indexed [0-9]* sprite set(s) in [0-9]* ms" Player.log` (expect well under 1 s).
- [ ] **Step 8: Commit** `feat(anpc): load sprite sets on demand and unload them when unused` (+ new `.meta` files).

---

### Task 5: Retaliation on a miss and brave patrols (runtime)

**Files:** Modify `Scripts/Runtime/NpcBrain.cs`, `Scripts/Runtime/SelfTest.cs`; Create `Scripts/Runtime/SelfTestCombat.cs` (dfmod.json entry after `SelfTestGait.cs`).

**Interfaces:** Consumes `HostilityRules.AttackedWhileCalm`, `AggroRules` (Task 3), `NpcDefinition.AggroRange/FleeChance`, `FleeRoll` (Task 2); `SelfTest.DiscardTestPeople()` and `SelfTest.OpenDirection(Transform, float)` (static, `SelfTestGait.cs`). Produces `NpcBrain.Threat : DaggerfallEntityBehaviour`.

- [ ] **Step 1: Failing self-test** — `Scripts/Runtime/SelfTestCombat.cs`:

```csharp
using System.Collections;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>Self-test: retaliation on a miss, bravery, brave patrols (spec lazy-sprites §B-D).</summary>
    public partial class SelfTest
    {
        IEnumerator CombatSteps(DaggerfallLocation location, Transform player)
        {
            DaggerfallEntityBehaviour playerBehaviour = GameManager.Instance.PlayerEntityBehaviour;
            DiscardTestPeople();
            Vector3 open = OpenDirection(player, 16f);
            mod.Sprites.Add("selftest_post", TestSpriteSet(false, "idle", "attack", "death"));

            // A swing that misses: DFU calls MakeEnemyHostileToAttacker without any damage.
            NpcBrain victim = SpawnDefinition(TestDefinition("selftest_missed", Bravery.Normal, location, player, 0f), location);
            NpcBrain swinger = SpawnDefinition(TestDefinition("selftest_swinger", Bravery.Normal, location, player, 0f), location);
            yield return Settle;
            victim.Teleport(player.position + open * 5f);
            swinger.Teleport(player.position + open * 6.5f);
            yield return Settle;
            int before = victim.EntityBehaviour.Entity.CurrentHealth;
            victim.Motor.MakeEnemyHostileToAttacker(swinger.EntityBehaviour);
            yield return null;
            yield return null;
            Check("a calm ANPC fights back when an attack misses", victim.CurrentMode == NpcMode.Fighting &&
                victim.Threat == swinger.EntityBehaviour && victim.EntityBehaviour.Entity.CurrentHealth == before, victim.Status);
            NpcBrain.Discard(victim);
            NpcBrain.Discard(swinger);

            // Bravery at low health, decided when the fight starts.
            IEnumerator c = BraveryCase(location, player, open, Bravery.Normal, 100, 0.10f, NpcMode.Fleeing, "Normal flees below half the line when fleeChance is 100");
            while (c.MoveNext()) yield return c.Current;
            c = BraveryCase(location, player, open, Bravery.Normal, 0, 0.10f, NpcMode.Fighting, "Normal fights on when fleeChance is 0");
            while (c.MoveNext()) yield return c.Current;
            c = BraveryCase(location, player, open, Bravery.Normal, 100, 0.20f, NpcMode.Fighting, "Normal fights above half the line");
            while (c.MoveNext()) yield return c.Current;
            c = BraveryCase(location, player, open, Bravery.Prudent, 0, 0.20f, NpcMode.Fleeing, "Prudent flees below fleeHealthPercent");
            while (c.MoveNext()) yield return c.Current;

            // Brave patrol: an enemy post (no walk sheet: cannot move) beyond aggroRange is ignored; within it, attacked.
            NpcBrain post = SpawnDefinition(TestDefinition("selftest_post", Bravery.Brave, location, player, 0f), location);
            NpcDefinition braveDef = TestDefinition("selftest_patrol", Bravery.Brave, location, player, 0f);
            braveDef.AggroRange = 5f;
            NpcBrain brave = SpawnDefinition(braveDef, location);
            yield return Settle;
            post.Teleport(player.position + open * 14f);
            brave.Teleport(player.position + open * 6f);
            post.SwitchHostile(true);
            yield return new WaitForSeconds(1.5f);
            Check("a brave ANPC ignores an enemy beyond its aggroRange", brave.CurrentMode == NpcMode.Calm, brave.Status);
            brave.Teleport(player.position + open * 10f);
            float until = Time.time + 3f;
            while (Time.time < until && brave.CurrentMode == NpcMode.Calm)
                yield return null;
            Check("a brave ANPC attacks an enemy it sees within range", brave.CurrentMode == NpcMode.Fighting &&
                brave.Threat == post.EntityBehaviour, brave.Status);
            post.SwitchHostile(false);
            NpcBrain.Discard(post);
            NpcBrain.Discard(brave);

            // Only angry at the player (hit by the player, not an enemy): not a foe.
            NpcBrain angry = SpawnDefinition(TestDefinition("selftest_angry", Bravery.Brave, location, player, 0f), location);
            NpcBrain watcher = SpawnDefinition(TestDefinition("selftest_watcher", Bravery.Brave, location, player, 0f), location);
            yield return Settle;
            angry.Teleport(player.position + open * 3f);
            watcher.Teleport(player.position + open * 7f);
            yield return Settle;
            PlayerHit(angry, playerBehaviour, 1);
            yield return new WaitForSeconds(1.5f);
            Check("a brave ANPC ignores an ANPC only angry at the player", watcher.CurrentMode == NpcMode.Calm, watcher.Status);
            NpcBrain.Discard(angry);
            NpcBrain.Discard(watcher);

            mod.Sprites.Remove("selftest_post");
            DiscardTestPeople();
            yield return Settle;
        }

        IEnumerator BraveryCase(DaggerfallLocation location, Transform player, Vector3 open, Bravery bravery, int fleeChance,
            float health, NpcMode expected, string name)
        {
            NpcDefinition d = TestDefinition("selftest_bravery", bravery, location, player, 0f);
            d.FleeChance = fleeChance;
            NpcBrain b = SpawnDefinition(d, location);
            NpcBrain other = SpawnDefinition(TestDefinition("selftest_bravery_foe", Bravery.Normal, location, player, 0f), location);
            yield return Settle;
            b.Teleport(player.position + open * 5f);
            other.Teleport(player.position + open * 7f);
            yield return Settle;
            DaggerfallEntity e = b.EntityBehaviour.Entity;
            e.CurrentHealth = Mathf.Max(1, Mathf.RoundToInt(e.MaxHealth * health));
            yield return null;
            b.Motor.MakeEnemyHostileToAttacker(other.EntityBehaviour);
            yield return null;
            yield return null;
            Check(name, b.CurrentMode == expected, b.Status);
            NpcBrain.Discard(b);
            NpcBrain.Discard(other);
            yield return null;
        }
    }
}
```

  In `SelfTest.cs` `Steps()`, right after the `GaitSteps` block:

```csharp
            IEnumerator combat = CombatSteps(location, playerTransform);
            while (combat.MoveNext())
                yield return combat.Current;
```

  dfmod.json: `"Assets/Game/Mods/AdvancedNPCs/Scripts/Runtime/SelfTestCombat.cs",` after `SelfTestGait.cs`.
- [ ] **Step 2: RED** — `bash Tools~/compile-check.sh` → `'NpcBrain' does not contain a definition for 'Threat'`.
- [ ] **Step 3: `NpcBrain`**
  - After the `Definition` property:

```csharp
        /// <summary>Whom it fights or flees from (null when calm).</summary>
        public DaggerfallEntityBehaviour Threat
        {
            get { return threat; }
        }
```

  - Fields after `NpcGait gait;`: `float patrolTimer;` and `static readonly Collider[] nearby = new Collider[64];`; in `Start()` add `patrolTimer = Random.Range(0f, AggroRules.CheckInterval);`.
  - In `Update()` the `case NpcMode.Calm:` block becomes:

```csharp
                case NpcMode.Calm:
                    if (HostilityRules.AttackedWhileCalm(true, motor.GiveUpTimer > 0, senses.Target != null && senses.Target != player,
                        senses.Target != null && senses.Target.Entity != null && senses.Target.Entity.CurrentHealth > 0))
                    {
                        // Swung at, hit or missed (DFU's MakeEnemyHostileToAttacker): fight back or flee.
                        OnAttackedByCreature(senses.Target);
                        break;
                    }
                    motor.GiveUpTimer = 0;
                    if (def.Bravery == Bravery.Brave && Patrol())
                        break;
                    UpdateCalm();
                    break;
```

  - Methods:

```csharp
        /// <summary>A calm Brave ANPC attacks the nearest foe it sees within aggroRange (spec lazy-sprites §D).</summary>
        bool Patrol()
        {
            patrolTimer -= Time.deltaTime;
            if (patrolTimer > 0f)
                return false;
            patrolTimer = AggroRules.CheckInterval;
            DaggerfallEntityBehaviour foe = FindFoe(def.AggroRange);
            if (foe == null)
                return false;
            AdvancedNpcsMod.Log(key + ": attacks " + foe.name + " on sight.");
            OnAttackedByCreature(foe);
            return true;
        }

        DaggerfallEntityBehaviour FindFoe(float range)
        {
            DaggerfallEntityBehaviour best = null;
            float bestDistance = range;
            foreach (NpcBrain other in All())
            {
                if (other == this || other.entityBehaviour == null || other.entityBehaviour.Entity == null)
                    continue;
                bool alive = other.mode != NpcMode.Dead && other.entityBehaviour.Entity.CurrentHealth > 0;
                if (!AggroRules.IsFoe(true, other.IsEnemy, false, false, alive))
                    continue;
                float d = Vector3.Distance(transform.position, other.transform.position);
                if (d <= bestDistance && CanSee(other.entityBehaviour))
                {
                    best = other.entityBehaviour;
                    bestDistance = d;
                }
            }
            int count = Physics.OverlapSphereNonAlloc(transform.position, range, nearby, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                DaggerfallEntityBehaviour e = nearby[i].GetComponentInParent<DaggerfallEntityBehaviour>();
                if (e == null || e == entityBehaviour || e.Entity == null || e.GetComponent<NpcBrain>() != null)
                    continue;
                EnemyMotor m = e.GetComponent<EnemyMotor>();
                if (m == null)
                    continue;
                bool watchOrAlly = e.Entity.Team == MobileTeams.CityWatch || e.Entity.Team == MobileTeams.PlayerAlly;
                if (!AggroRules.IsFoe(false, false, m.IsHostile, watchOrAlly, e.Entity.CurrentHealth > 0))
                    continue;
                float d = Vector3.Distance(transform.position, e.transform.position);
                if (d <= bestDistance && CanSee(e))
                {
                    best = e;
                    bestDistance = d;
                }
            }
            return best;
        }
```

- [ ] **Step 4: GREEN** — compile-check, runtime-check, run-tests; build `--no-examples`; self-test (background) → 8 new checks pass, total 174/174. If existing checks change because calm Brave test ANPCs now patrol (e.g. `selftest_brave`, `selftest_townie` near `selftest_enemy`/`selftest_orc`), adjust those tests (bravery `Prudent` or more distance) and ledger a ruling per changed check.
- [ ] **Step 5: Commit** `feat(anpc): fight back on a missed attack; brave ANPCs attack enemies on sight` (+ metas).

---

### Task 6: Docs, version, final verification

**Files:** `README.md`, `AdvancedNPCs.dfmod.json`.

- [ ] **Step 1: README**
  - Intro: "only fight whoever attacks them" → "fight whoever attacks them (even when the attack misses); brave ones also attack enemies they see".
  - Settings table: `bravery` row text → `` `Coward` (always flees), `Prudent` (flees below `fleeHealthPercent`), `Normal` (once below half of it, a `fleeChance` % chance to flee, rolled once), `Brave` (never flees; when calm, attacks enemies it sees within `aggroRange`) ``; `fleeHealthPercent` row → `1–99, used by Prudent (Normal uses half of it)`; add rows `` | `fleeChance` | no | `30` | 0–100: Normal's chance to flee once badly hurt | `` and `` | `aggroRange` | no | `15` | 1–50 m: how far a calm Brave ANPC looks for enemies (enemy ANPCs and hostile vanilla enemies, never the city watch) | ``.
  - Under the table: `Before 0.12, "Normal" meant "always flees at low health"; use "Prudent" for that now.`
  - Sprite notes: new bullet `Sprite sets load when an ANPC using them appears (an ANPC may show up a moment after spawning) and unload a minute after the last one is gone; at startup only the files' sizes are checked.`
- [ ] **Step 2:** `"ModVersion": "0.12.0"`.
- [ ] **Step 3: Full verification** (background): compile-check, runtime-check, run-tests, build `--no-examples`, `git restore Assets/AddressableAssetsData/`, self-test 174/174 (re-run once if only the flaky magic-edge pair fails), sample smoke `grep -E "(run|greet_[0-9]+|idle_[0-9]+)\.png: (file: must be|not an)" Player.log || echo ok`, and the `Indexed ... ms` line.
- [ ] **Step 4: Commit** `docs(anpc): bravery, retaliation, brave patrols, on-demand sprites; mod 0.12.0`.

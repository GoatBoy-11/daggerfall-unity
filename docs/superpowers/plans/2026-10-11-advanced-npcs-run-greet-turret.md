# Advanced NPCs: running, greetings and turrets — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** ANPCs run when chasing a far target or fleeing (unless `canRun: false`), greet liked players/ANPCs with a `greet*` sheet, and never move when their sprite set has no walk or run sheet.

**Architecture:** Pure rules in `Scripts/Core` (sheet states, `GaitRules`, `GreetRules`, `combat` parsing) with NUnit tests. Runtime components: `NpcGait` (decides the gait each frame, adds the run boost on top of DFU's `EnemyMotor`, pins turrets), `NpcGreeter` (finds liked targets, faces them, plays `greet`). `NpcBrain` uses run speed for fleeing and keeps turrets still; `NpcSprite` picks run/walk/idle from the gait.

**Tech Stack:** C# for DFU's runtime mod compiler (Mono; no nested enums; an enum-typed field must be declared in an earlier-compiled file), Unity 2019.4 runtime, NUnit (Core only, Unity 6 host), bash tool scripts in `Tools~`.

**Spec:** `docs/superpowers/specs/2026-10-11-advanced-npcs-run-greet-turret-design.md`

All paths below are relative to `Assets/Game/Mods/AdvancedNPCs/` unless they start with `docs/`.

## Global Constraints

- Run speed default **1.2 × walk speed**; `combat.runSpeed` from **1 to 2**.
- `combat.canRun` default **true**; false = never run, chase/flee at walk speed, walk sheets.
- Walk speed = DFU enemy formula `(Stats.LiveSpeed + 150) / 40` m/s.
- Chase runs when target in sight and distance **> 6 m**; stops running when **< 4 m** or out of sight.
- Greeting: distance **≤ 4 m**, in sight, **25 %** chance once per encounter, **2 game hours** cooldown per target; opinion = player: calm + reaction `likes`/`loves`; ANPC: both calm, neither an enemy.
- Turret = custom sprite set with no `walk*` and no `run*` sheet; never wanders, chases, flees or is knocked back; still turns and attacks.
- Fallbacks: run → walk → idle; walk → run → idle; greet → none.
- Every new `.cs` file under `Scripts/` must be added to `AdvancedNPCs.dfmod.json` `"Files"` (DFU compiles mod sources from that list; a missing file kills the mod).
- `Gait` enum lives in `Scripts/Core/GaitRules.cs` (compiles before `Scripts/Runtime/*` in both path and file-name order).
- Mod version **0.11.0**.
- Match surrounding code: `/// <summary>` on public members, braces on their own lines, 4-space indent.

## Review Focus

1. A chaser whose target is out of sight (behind a wall) at long range must not run or get boosted → unit test `Fighting_OutOfSight_Walks` (Task 2).
2. A floating-origin shift (DFU moves the whole town by hundreds of metres) must not be mistaken for movement by the turret pin or the run boost → self-test "world shift does not boost or unpin" (Task 4).
3. A turret spawned slightly above ground must still settle (the pin is horizontal only) → turret self-test allows vertical change and checks horizontal drift only (Task 4).
4. Two same-side ANPCs standing next to each other must not greet each other over and over → unit tests `StayingInRange_DoesNotRollAgain` and `Cooldown_BlocksTheSameTargetForTwoHours` (Task 3).
5. `canRun: false` with a run-only sprite set walks with the run sheet at walk speed; `runSpeed` given with `canRun: false` loads → unit tests `Walk_FallsBackToRun`, `Fighting_CannotRun_Walks`, `CanRunFalse_WithRunSpeed_Loads` (Tasks 1, 2).

## Verification commands (used by every task)

- Core unit tests: `bash Tools~/run-tests.sh` → `result="Passed" total="N" passed="N" failed="0"`. Baseline: 587.
- Compile against DFU assemblies: `bash Tools~/compile-check.sh` → `compile-check: OK (...)`.
- DFU's own runtime compiler: `bash Tools~/runtime-check.sh` → two lines `runtime-check: OK (...)`.
- Build + install (DFU and Unity closed): `bash Tools~/build-mod.sh --no-examples`, then from the repo root `git restore Assets/AddressableAssetsData/`.
- In-game self-test: `bash Tools~/selftest.sh | grep -E "FAIL|SELFTEST DONE"`. Baseline 152/152. Known flaky pair: "magic town caster keeps casting past the town edge" / "... spends rather than refills" (re-run once if only those fail).

---

### Task 1: `run*` and `greet*` sheet states, fallbacks, stationary sets

**Files:**
- Modify: `Scripts/Core/SpriteStates.cs`
- Modify: `Scripts/Runtime/SpriteLibrary.cs` (skip message only)
- Test: `Editor/Tests/SpriteStatesTests.cs`

**Interfaces:**
- Produces: `SpriteStates.Run = "run"`, `SpriteStates.Greet = "greet"`, `SpriteStates.IsStationary(SpriteSet set) : bool`; `Variants(set, Run|Walk|Greet)` with the fallbacks above.

- [ ] **Step 1: Write the failing tests** — in `Editor/Tests/SpriteStatesTests.cs` add to the `StateOf` cases (above `[TestCase("idle_x", null)]`):

```csharp
        [TestCase("run", "run")]
        [TestCase("Run_2", "run")]
        [TestCase("greet", "greet")]
        [TestCase("greet_1", "greet")]
        [TestCase("talk_1", null)]
        [TestCase("pray_1", null)]
```

and add before `IsValid_NeedsIdle`:

```csharp
        [Test]
        public void Run_FallsBackToWalkThenIdle()
        {
            CollectionAssert.AreEqual(new[] { "run" }, Names(SpriteStates.Variants(Set("idle_1", "walk", "run"), SpriteStates.Run)));
            CollectionAssert.AreEqual(new[] { "walk" }, Names(SpriteStates.Variants(Set("idle_1", "walk"), SpriteStates.Run)));
            CollectionAssert.AreEqual(new[] { "idle_1" }, Names(SpriteStates.Variants(Set("idle_1"), SpriteStates.Run)));
        }

        [Test]
        public void Walk_FallsBackToRun()
        {
            CollectionAssert.AreEqual(new[] { "run" }, Names(SpriteStates.Variants(Set("idle_1", "run"), SpriteStates.Walk)));
            CollectionAssert.AreEqual(new[] { "walk" }, Names(SpriteStates.Variants(Set("idle_1", "walk", "run"), SpriteStates.Walk)));
        }

        [Test]
        public void Greet_HasNoFallback()
        {
            Assert.AreEqual(0, SpriteStates.Variants(Set("idle_1", "walk", "attack_1"), SpriteStates.Greet).Count);
            CollectionAssert.AreEqual(new[] { "greet_1" }, Names(SpriteStates.Variants(Set("idle_1", "greet_1"), SpriteStates.Greet)));
        }

        [Test]
        public void IsStationary_WhenNoWalkAndNoRun()
        {
            Assert.IsTrue(SpriteStates.IsStationary(Set("idle_1", "attack_1", "death_1")));
            Assert.IsFalse(SpriteStates.IsStationary(Set("idle_1", "walk")));
            Assert.IsFalse(SpriteStates.IsStationary(Set("idle_1", "run")));
        }
```

- [ ] **Step 2: Run to verify failure** — `bash Tools~/run-tests.sh` → `NO RESULTS FILE`, `error CS0117: 'SpriteStates' does not contain a definition for 'Run'`.

- [ ] **Step 3: Implement** — in `Scripts/Core/SpriteStates.cs`:

After `public const string Walk = "walk";` add:

```csharp
        public const string Run = "run";
        /// <summary>Greeting someone it likes (NpcGreeter); never a fallback for anything.</summary>
        public const string Greet = "greet";
```

List becomes:

```csharp
        static readonly string[] All = { Idle, Walk, Run, Hit, Attack, RangedAttack, Cast, Death, Greet };
```

Replace `Variants` (doc comment and body):

```csharp
        /// <summary>
        /// Sheets for a state, sorted by name; cast falls back to attack sheets only (none: the spell plays no animation);
        /// attack_range falls back to attack, then idle; run falls back to walk and walk to run, then idle; hit and
        /// attack fall back to idle; death and greet have no fallback.
        /// </summary>
        public static List<SpriteAnimation> Variants(SpriteSet set, string state)
        {
            List<SpriteAnimation> found = Own(set, state);
            if (found.Count > 0)
                return found;
            if (state == Cast)
                return Own(set, Attack);
            if (state == RangedAttack)
                return Variants(set, Attack);
            if (state == Run || state == Walk)
            {
                List<SpriteAnimation> other = Own(set, state == Run ? Walk : Run);
                return other.Count > 0 ? other : Own(set, Idle);
            }
            if (state == Hit || state == Attack)
                return Variants(set, Idle);
            return found;
        }
```

After `IsAttackSheet` add:

```csharp
        /// <summary>A set with no walk* and no run* sheet: its person never moves (a turret).</summary>
        public static bool IsStationary(SpriteSet set)
        {
            return Own(set, Walk).Count == 0 && Own(set, Run).Count == 0;
        }
```

In `Scripts/Runtime/SpriteLibrary.cs` the skip message becomes
`"not an idle*, walk*, run*, hit*, attack*, attack_range*, cast*, greet* or death* sheet; not loaded"`.

- [ ] **Step 4: Run to verify pass** — `bash Tools~/run-tests.sh` → Passed, total 597 (587 + 10), failed 0. The existing `Fallbacks` test still passes (walk on an idle-only set → `idle_1`).

- [ ] **Step 5: Commit**

```bash
git add Assets/Game/Mods/AdvancedNPCs/Scripts/Core/SpriteStates.cs Assets/Game/Mods/AdvancedNPCs/Scripts/Runtime/SpriteLibrary.cs Assets/Game/Mods/AdvancedNPCs/Editor/Tests/SpriteStatesTests.cs
git commit -m "feat(anpc): run and greet sheet states, walk/run fallbacks, stationary sets"
```

---

### Task 2: `GaitRules` and `combat.runSpeed` / `combat.canRun`

**Files:**
- Create: `Scripts/Core/GaitRules.cs`
- Modify: `Scripts/Core/CombatRules.cs`, `Scripts/Core/NpcDefinition.cs`, `Scripts/Core/DefinitionParser.cs` (the `CombatRules.Read` call)
- Modify: `AdvancedNPCs.dfmod.json` (add `"Assets/Game/Mods/AdvancedNPCs/Scripts/Core/GaitRules.cs",` after the `GenericSpawn.cs` line)
- Test: create `Editor/Tests/GaitRulesTests.cs`; modify `Editor/Tests/CombatTests.cs`

**Interfaces:**
- Produces (namespace `AdvancedNPCs.Core`):
  - `public enum Gait { Still, Walk, Run }`
  - `GaitRules.RunStart = 6f`, `GaitRules.RunStop = 4f`, `GaitRules.DefaultRunSpeed = 1.2f`, `GaitRules.MinRunSpeed = 1`, `GaitRules.MaxRunSpeed = 2`
  - `GaitRules.WalkSpeed(int liveSpeed) : float`
  - `GaitRules.RunSpeed(float walkSpeed, float runSpeed, bool canRun) : float`
  - `GaitRules.Fighting(bool targetInSight, float distance, bool wasRunning, bool canRun) : Gait`
  - `GaitRules.Fleeing(bool moving, bool canRun) : Gait`
  - `GaitRules.Calm(bool moving) : Gait`
  - `NpcDefinition.RunSpeed` (float, default 1.2f), `NpcDefinition.CanRun` (bool, default true)
  - `CombatRules.Read(root, isCreature, out key, out health, out damage, out attackSpeed, out rangedAtDistance, out rangedAnimation, out float runSpeed, out bool canRun)`

- [ ] **Step 1: Write the failing tests** — create `Editor/Tests/GaitRulesTests.cs`:

```csharp
using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    /// <summary>Running while chasing or fleeing (spec run/greet/turret §2).</summary>
    public class GaitRulesTests
    {
        [Test]
        public void WalkSpeed_IsDfuEnemyFormula()
        {
            Assert.AreEqual(5f, GaitRules.WalkSpeed(50), 1e-5);
            Assert.AreEqual(3.75f, GaitRules.WalkSpeed(0), 1e-5);
        }

        [Test]
        public void RunSpeed_ScalesWalk_UnlessItCannotRun()
        {
            Assert.AreEqual(6f, GaitRules.RunSpeed(5f, 1.2f, true), 1e-5);
            Assert.AreEqual(5f, GaitRules.RunSpeed(5f, 1.2f, false), 1e-5);
        }

        [Test]
        public void Fighting_FarAndSeen_Runs()
        {
            Assert.AreEqual(Gait.Run, GaitRules.Fighting(true, 10f, false, true));
        }

        [Test]
        public void Fighting_Near_Walks()
        {
            Assert.AreEqual(Gait.Walk, GaitRules.Fighting(true, 3f, false, true));
            Assert.AreEqual(Gait.Walk, GaitRules.Fighting(true, 3.9f, true, true));
        }

        [Test]
        public void Fighting_BetweenStopAndStart_KeepsWhatItWasDoing()
        {
            Assert.AreEqual(Gait.Run, GaitRules.Fighting(true, 5f, true, true));
            Assert.AreEqual(Gait.Walk, GaitRules.Fighting(true, 5f, false, true));
        }

        [Test]
        public void Fighting_OutOfSight_Walks()
        {
            Assert.AreEqual(Gait.Walk, GaitRules.Fighting(false, 20f, true, true));
        }

        [Test]
        public void Fighting_CannotRun_Walks()
        {
            Assert.AreEqual(Gait.Walk, GaitRules.Fighting(true, 20f, true, false));
        }

        [Test]
        public void Fleeing_RunsWhileMoving()
        {
            Assert.AreEqual(Gait.Run, GaitRules.Fleeing(true, true));
            Assert.AreEqual(Gait.Walk, GaitRules.Fleeing(true, false));
            Assert.AreEqual(Gait.Still, GaitRules.Fleeing(false, true));
        }

        [Test]
        public void Calm_WalksOrStands()
        {
            Assert.AreEqual(Gait.Walk, GaitRules.Calm(true));
            Assert.AreEqual(Gait.Still, GaitRules.Calm(false));
        }
    }
}
```

In `Editor/Tests/CombatTests.cs` add before `ScaleHealth_RoundsAndNeverDropsBelowOne`:

```csharp
        [Test]
        public void Running_DefaultsToOnAtOnePointTwo()
        {
            ParseResult r = DefinitionParser.ParseFolder("grub", Unique);
            Assert.IsTrue(r.Ok, r.Error);
            Assert.IsTrue(r.Definition.CanRun);
            Assert.AreEqual(1.2f, r.Definition.RunSpeed, 1e-6);
        }

        [Test]
        public void Running_ReadsRunSpeedAndCanRun()
        {
            ParseResult r = With("\"baseClass\": \"Knight\", \"combat\": { \"runSpeed\": 1.5 }");
            Assert.IsTrue(r.Ok, r.Error);
            Assert.AreEqual(1.5f, r.Definition.RunSpeed, 1e-6);
            Assert.IsFalse(r.Definition.HasCombatTuning);
            ParseResult off = With("\"baseClass\": \"Knight\", \"combat\": { \"canRun\": false }");
            Assert.IsTrue(off.Ok, off.Error);
            Assert.IsFalse(off.Definition.CanRun);
        }

        [Test]
        public void CanRunFalse_WithRunSpeed_Loads()
        {
            ParseResult r = With("\"baseClass\": \"Knight\", \"combat\": { \"canRun\": false, \"runSpeed\": 1.8 }");
            Assert.IsTrue(r.Ok, r.Error);
            Assert.IsFalse(r.Definition.CanRun);
        }

        [Test]
        public void Running_RejectsBadValues()
        {
            StringAssert.Contains("combat.runSpeed", With("\"baseClass\": \"Knight\", \"combat\": { \"runSpeed\": 0.9 }").Error);
            StringAssert.Contains("combat.runSpeed", With("\"baseClass\": \"Knight\", \"combat\": { \"runSpeed\": 2.5 }").Error);
            StringAssert.Contains("combat.runSpeed", With("\"baseClass\": \"Knight\", \"combat\": { \"runSpeed\": \"fast\" }").Error);
            StringAssert.Contains("combat.canRun", With("\"baseClass\": \"Knight\", \"combat\": { \"canRun\": \"no\" }").Error);
        }
```

- [ ] **Step 2: Run to verify failure** — `bash Tools~/run-tests.sh` → compile error `The type or namespace name 'GaitRules' could not be found`.

- [ ] **Step 3: Implement** — create `Scripts/Core/GaitRules.cs`:

```csharp
namespace AdvancedNPCs.Core
{
    /// <summary>What an ANPC's legs do this frame. Top-level: DFU's runtime compiler cannot load nested enums.</summary>
    public enum Gait { Still, Walk, Run }

    /// <summary>
    /// Running while chasing or fleeing (spec run/greet/turret §2). DFU's motor chases at walk speed; a running ANPC
    /// moves runSpeed times as fast. No Unity or DFU types.
    /// </summary>
    public static class GaitRules
    {
        /// <summary>A chaser starts running when its target is farther than this (metres)...</summary>
        public const float RunStart = 6f;
        /// <summary>...and walks again when closer than this, so it does not flicker between the two.</summary>
        public const float RunStop = 4f;
        public const float DefaultRunSpeed = 1.2f;
        public const double MinRunSpeed = 1, MaxRunSpeed = 2;

        /// <summary>DFU's enemy move speed in m/s: (Speed + PlayerSpeedChanger.dfWalkBase) * MeshReader.GlobalScale.</summary>
        public static float WalkSpeed(int liveSpeed)
        {
            return (liveSpeed + 150f) / 40f;
        }

        public static float RunSpeed(float walkSpeed, float runSpeed, bool canRun)
        {
            return canRun ? walkSpeed * runSpeed : walkSpeed;
        }

        public static Gait Fighting(bool targetInSight, float distance, bool wasRunning, bool canRun)
        {
            if (!canRun || !targetInSight)
                return Gait.Walk;
            if (distance > RunStart)
                return Gait.Run;
            if (wasRunning && distance >= RunStop)
                return Gait.Run;
            return Gait.Walk;
        }

        public static Gait Fleeing(bool moving, bool canRun)
        {
            if (!moving)
                return Gait.Still;
            return canRun ? Gait.Run : Gait.Walk;
        }

        public static Gait Calm(bool moving)
        {
            return moving ? Gait.Walk : Gait.Still;
        }
    }
}
```

In `Scripts/Core/NpcDefinition.cs` after `public string RangedAnimation;` add:

```csharp
        /// <summary>"combat": running speed as a multiple of walk speed (1-2).</summary>
        public float RunSpeed = GaitRules.DefaultRunSpeed;
        /// <summary>"combat": false = never runs (chases and flees at walk speed).</summary>
        public bool CanRun = true;
```

In `Scripts/Core/CombatRules.cs`:
- `Keys` = `{ "health", "damage", "attackSpeed", "rangedAtDistance", "rangedAnimation", "runSpeed", "canRun" }`.
- Unknown-key message: `"unknown setting (use health, damage, attackSpeed, rangedAtDistance, rangedAnimation, runSpeed or canRun)"`.
- Signature: append `, out float runSpeed, out bool canRun` after `out string rangedAnimation`; add `runSpeed = GaitRules.DefaultRunSpeed;` and `canRun = true;` next to `rangedAnimation = null;`.
- Before the final `key = "combat";` add:

```csharp
            if (o.ContainsKey("runSpeed"))
            {
                double rs;
                key = "combat.runSpeed";
                if (o["runSpeed"] == null || FieldReader.Number(o, "runSpeed", GaitRules.DefaultRunSpeed, out rs) != null ||
                    double.IsNaN(rs) || rs < GaitRules.MinRunSpeed || rs > GaitRules.MaxRunSpeed)
                    return "must be a number from 1 to 2 (times walking speed)";
                runSpeed = (float)rs;
            }
            object rawCanRun;
            if (o.TryGetValue("canRun", out rawCanRun))
            {
                key = "combat.canRun";
                if (!(rawCanRun is bool))
                    return "must be true or false";
                canRun = (bool)rawCanRun;
            }
```

- Class summary: before `/// No Unity or DFU types.` add the line `/// "runSpeed" (1-2, default 1.2) is running speed as a multiple of walking; "canRun": false never runs.`

In `Scripts/Core/DefinitionParser.cs` the call becomes:

```csharp
            if ((problem = CombatRules.Read(o, d.IsCreature, out combatKey, out d.HealthScale, out d.DamageScale, out d.AttackSpeed,
                    out d.RangedAtDistance, out d.RangedAnimation, out d.RunSpeed, out d.CanRun)) != null)
```

Add `"Assets/Game/Mods/AdvancedNPCs/Scripts/Core/GaitRules.cs",` to `AdvancedNPCs.dfmod.json` after the `GenericSpawn.cs` entry.

- [ ] **Step 4: Run to verify pass** — `bash Tools~/run-tests.sh` → Passed, total 610; `bash Tools~/compile-check.sh` and `bash Tools~/runtime-check.sh` → OK.

- [ ] **Step 5: Commit**

```bash
git add Assets/Game/Mods/AdvancedNPCs/Scripts/Core Assets/Game/Mods/AdvancedNPCs/Editor/Tests Assets/Game/Mods/AdvancedNPCs/AdvancedNPCs.dfmod.json
git commit -m "feat(anpc): gait rules and combat.runSpeed / combat.canRun"
```

---

### Task 3: `GreetRules` (opinion, encounter, cooldown, chance)

**Files:**
- Create: `Scripts/Core/GreetRules.cs`
- Modify: `AdvancedNPCs.dfmod.json` (add `"Assets/Game/Mods/AdvancedNPCs/Scripts/Core/GreetRules.cs",` after `GaitRules.cs`)
- Test: create `Editor/Tests/GreetRulesTests.cs`

**Interfaces:**
- Produces (namespace `AdvancedNPCs.Core`):
  - `GreetRules.Distance = 4f`, `GreetRules.Chance = 0.25`, `GreetRules.CooldownHours = 2.0`, `GreetRules.PlayerKey = "player"`
  - `GreetRules.LikesPlayer(bool selfCalm, string reactionBand) : bool`
  - `GreetRules.SameSide(bool selfCalm, bool selfEnemy, bool otherCalm, bool otherEnemy) : bool`
  - `class GreetMemory`: `bool Consider(string target, bool inRangeAndSeen, bool likes, double nowHours, double roll, double chance)` (true = greet now), `int Count`.

- [ ] **Step 1: Write the failing tests** — create `Editor/Tests/GreetRulesTests.cs`:

```csharp
using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    /// <summary>Greetings (spec run/greet/turret §5).</summary>
    public class GreetRulesTests
    {
        [TestCase(true, "likes", true)]
        [TestCase(true, "loves", true)]
        [TestCase(true, "neutral", false)]
        [TestCase(true, "dislikes", false)]
        [TestCase(false, "loves", false)]
        public void LikesPlayer(bool calm, string band, bool expected)
        {
            Assert.AreEqual(expected, GreetRules.LikesPlayer(calm, band));
        }

        [Test]
        public void SameSide_NeedsBothCalmAndNoEnemy()
        {
            Assert.IsTrue(GreetRules.SameSide(true, false, true, false));
            Assert.IsFalse(GreetRules.SameSide(true, false, false, false));
            Assert.IsFalse(GreetRules.SameSide(true, true, true, false));
            Assert.IsFalse(GreetRules.SameSide(true, false, true, true));
        }

        [Test]
        public void NewEncounter_LikedAndLuckyRoll_Greets()
        {
            GreetMemory m = new GreetMemory();
            Assert.IsTrue(m.Consider("player", true, true, 10.0, 0.1, GreetRules.Chance));
            Assert.AreEqual(1, m.Count);
        }

        [Test]
        public void UnluckyRoll_DoesNotGreet()
        {
            Assert.IsFalse(new GreetMemory().Consider("player", true, true, 10.0, 0.9, GreetRules.Chance));
        }

        [Test]
        public void NotLiked_OrOutOfRange_DoesNotGreet()
        {
            Assert.IsFalse(new GreetMemory().Consider("player", true, false, 10.0, 0.0, 1.0));
            Assert.IsFalse(new GreetMemory().Consider("player", false, true, 10.0, 0.0, 1.0));
        }

        [Test]
        public void StayingInRange_DoesNotRollAgain()
        {
            GreetMemory m = new GreetMemory();
            Assert.IsFalse(m.Consider("bob", true, true, 10.0, 0.9, 0.25));   // the encounter's one roll fails
            Assert.IsFalse(m.Consider("bob", true, true, 10.1, 0.0, 0.25));   // still the same encounter
            Assert.IsFalse(m.Consider("bob", false, true, 10.2, 0.0, 0.25));  // leaves
            Assert.IsTrue(m.Consider("bob", true, true, 10.3, 0.0, 0.25));    // a new encounter rolls again
        }

        [Test]
        public void Cooldown_BlocksTheSameTargetForTwoHours()
        {
            GreetMemory m = new GreetMemory();
            Assert.IsTrue(m.Consider("bob", true, true, 10.0, 0.0, 1.0));
            m.Consider("bob", false, true, 10.5, 0.0, 1.0);
            Assert.IsFalse(m.Consider("bob", true, true, 11.0, 0.0, 1.0));    // 1 hour later
            m.Consider("bob", false, true, 11.5, 0.0, 1.0);
            Assert.IsTrue(m.Consider("bob", true, true, 12.1, 0.0, 1.0));     // over 2 hours later
            Assert.IsTrue(m.Consider("ann", true, true, 12.1, 0.0, 1.0));     // another target is separate
        }
    }
}
```

- [ ] **Step 2: Run to verify failure** — `bash Tools~/run-tests.sh` → compile error `'GreetRules' could not be found`.

- [ ] **Step 3: Implement** — create `Scripts/Core/GreetRules.cs`:

```csharp
using System.Collections.Generic;

namespace AdvancedNPCs.Core
{
    /// <summary>Who an ANPC greets, and how often (spec run/greet/turret §5). No Unity or DFU types.</summary>
    public static class GreetRules
    {
        public const float Distance = 4f;
        public const double Chance = 0.25;
        public const double CooldownHours = 2.0;
        /// <summary>The player's key in GreetMemory (ANPCs use their own keys).</summary>
        public const string PlayerKey = "player";

        /// <summary>The player: this ANPC is calm toward them and DFU's reaction to them is likes or loves.</summary>
        public static bool LikesPlayer(bool selfCalm, string reactionBand)
        {
            return selfCalm && (reactionBand == "likes" || reactionBand == "loves");
        }

        /// <summary>Another ANPC: both calm, neither an enemy of the player.</summary>
        public static bool SameSide(bool selfCalm, bool selfEnemy, bool otherCalm, bool otherEnemy)
        {
            return selfCalm && otherCalm && !selfEnemy && !otherEnemy;
        }
    }

    /// <summary>
    /// One ANPC's greeting memory: who is near now (a target that stays near is one encounter, rolled once) and when
    /// it last greeted each target (cooldown). Kept in memory only.
    /// </summary>
    public class GreetMemory
    {
        readonly HashSet<string> near = new HashSet<string>();
        readonly Dictionary<string, double> lastGreet = new Dictionary<string, double>();

        /// <summary>Greetings given (self-test).</summary>
        public int Count { get; private set; }

        /// <summary>True when the ANPC should greet this target now. Call for every candidate at each check.</summary>
        public bool Consider(string target, bool inRangeAndSeen, bool likes, double nowHours, double roll, double chance)
        {
            if (!inRangeAndSeen)
            {
                near.Remove(target);
                return false;
            }
            if (!near.Add(target))
                return false;               // the same encounter: rolled already
            double last;
            if (!likes || (lastGreet.TryGetValue(target, out last) && nowHours - last < GreetRules.CooldownHours))
                return false;
            if (roll >= chance)
                return false;
            lastGreet[target] = nowHours;
            Count++;
            return true;
        }
    }
}
```

Add `"Assets/Game/Mods/AdvancedNPCs/Scripts/Core/GreetRules.cs",` to `AdvancedNPCs.dfmod.json` after `GaitRules.cs`.

- [ ] **Step 4: Run to verify pass** — `bash Tools~/run-tests.sh` → Passed, total 621; compile-check and runtime-check OK.

- [ ] **Step 5: Commit**

```bash
git add Assets/Game/Mods/AdvancedNPCs/Scripts/Core/GreetRules.cs Assets/Game/Mods/AdvancedNPCs/Editor/Tests/GreetRulesTests.cs Assets/Game/Mods/AdvancedNPCs/AdvancedNPCs.dfmod.json
git commit -m "feat(anpc): greeting rules (opinion, encounter, cooldown, chance)"
```

---

### Task 4: `NpcGait` — run boost, turret pin, flee speed, run/walk animation

**Files:**
- Create: `Scripts/Runtime/NpcGait.cs`, `Scripts/Runtime/SelfTestGait.cs`
- Modify: `Scripts/Runtime/NpcBrain.cs` (UpdateCalm, UpdateFleeing, Teleport, CanSee public, HoldStill, Definition)
- Modify: `Scripts/Runtime/NpcSprite.cs` (UseGait, WantedState, loop states)
- Modify: `Scripts/Runtime/NpcSpawner.cs` (add NpcGait)
- Modify: `Scripts/Runtime/SelfTest.cs` (call GaitSteps; TestSpriteSet overload)
- Modify: `AdvancedNPCs.dfmod.json` (add `Runtime/NpcGait.cs` after `Runtime/NpcCrime.cs`; `Runtime/SelfTestGait.cs` after `Runtime/SelfTest.cs`)

**Interfaces:**
- Consumes: `Gait`, `GaitRules.*` (Task 2); `SpriteStates.Run`, `SpriteStates.IsStationary` (Task 1); `NpcDefinition.RunSpeed/CanRun`.
- Produces:
  - `NpcGait.Init(NpcDefinition)`, `NpcGait.Repin()`, `NpcGait.Current : Gait`, `NpcGait.IsTurret : bool`, `NpcGait.WalkSpeed : float`, `NpcGait.RunSpeed : float`, `NpcGait.Boosted : float`
  - `NpcBrain.HoldStill(float seconds)`, `public bool NpcBrain.CanSee(DaggerfallEntityBehaviour other)`, `NpcBrain.Definition : NpcDefinition`
  - `NpcSprite.UseGait(NpcGait gait)`
  - `SelfTest.TestSpriteSet(bool withDeathStatic, params string[] names)` overload; `SelfTest.GaitSteps(DaggerfallLocation, Transform)`

- [ ] **Step 1: Write the failing self-test** — create `Scripts/Runtime/SelfTestGait.cs`:

```csharp
using System.Collections;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>Self-test: running, turrets and greetings (spec run/greet/turret §7).</summary>
    public partial class SelfTest
    {
        IEnumerator GaitSteps(DaggerfallLocation location, Transform player)
        {
            DaggerfallEntityBehaviour playerBehaviour = GameManager.Instance.PlayerEntityBehaviour;
            mod.Sprites.Add("selftest_runner", TestSpriteSet(false, "idle", "walk", "run", "attack", "death"));
            mod.Sprites.Add("selftest_turret", TestSpriteSet(false, "idle", "attack", "death"));

            // A hostile runner far away runs (gait, sheet, boost); near the player it walks.
            NpcDefinition runnerDef = TestDefinition("selftest_runner", Bravery.Brave, location, player, 0f);
            runnerDef.BaseClass = "Warrior";
            NpcBrain runner = SpawnDefinition(runnerDef, location);
            yield return Settle;
            runner.Teleport(player.position + player.forward * 14f);
            runner.SwitchHostile(true);
            NpcGait runnerGait = runner.GetComponent<NpcGait>();
            NpcSprite runnerSprite = runner.GetComponent<NpcSprite>();
            bool sawRun = false, sawRunSheet = false;
            float until = Time.time + 3f;
            while (Time.time < until && !(sawRun && sawRunSheet))
            {
                sawRun |= runnerGait != null && runnerGait.Current == Gait.Run;
                sawRunSheet |= runnerSprite != null && runnerSprite.CurrentAnimation == "run";
                yield return null;
            }
            Check("far hostile ANPC runs with its run sheet and a speed boost", sawRun && sawRunSheet && runnerGait.Boosted > 0f,
                "run=" + sawRun + ", sheet=" + (runnerSprite != null ? runnerSprite.CurrentAnimation : "none") +
                ", boosted=" + (runnerGait != null ? runnerGait.Boosted : -1f) + ", " + runner.Status);
            until = Time.time + 6f;
            while (Time.time < until && Vector3.Distance(runner.transform.position, player.position) > 2.5f)
                yield return null;
            yield return new WaitForSeconds(0.3f);
            Check("near the player it no longer runs", runnerGait != null && runnerGait.Current != Gait.Run,
                "gait=" + (runnerGait != null ? runnerGait.Current.ToString() : "none") + ", distance=" +
                Vector3.Distance(runner.transform.position, player.position));
            runner.SwitchHostile(false);
            NpcBrain.Discard(runner);

            // canRun false: far away it still walks.
            NpcDefinition walkerDef = TestDefinition("selftest_walker", Bravery.Brave, location, player, 0f);
            walkerDef.Folder = "selftest_runner";
            walkerDef.CanRun = false;
            NpcBrain walker = SpawnDefinition(walkerDef, location);
            yield return Settle;
            walker.Teleport(player.position + player.forward * 14f);
            walker.SwitchHostile(true);
            NpcGait walkerGait = walker.GetComponent<NpcGait>();
            bool walkerRan = false;
            until = Time.time + 1.5f;
            while (Time.time < until)
            {
                walkerRan |= walkerGait != null && walkerGait.Current == Gait.Run;
                yield return null;
            }
            Check("canRun false never runs", walkerGait != null && !walkerRan && walkerGait.Boosted == 0f,
                "boosted=" + (walkerGait != null ? walkerGait.Boosted : -1f));
            walker.SwitchHostile(false);
            NpcBrain.Discard(walker);

            // A fleeing coward runs at run speed.
            NpcDefinition cowardDef = TestDefinition("selftest_coward", Bravery.Coward, location, player, 2f);
            cowardDef.Folder = "selftest_runner";
            NpcBrain coward = SpawnDefinition(cowardDef, location);
            yield return Settle;
            PlayerHit(coward, playerBehaviour, 1);
            yield return new WaitForSeconds(0.3f);
            NpcGait cowardGait = coward.GetComponent<NpcGait>();
            Vector3 from = coward.transform.localPosition;
            yield return new WaitForSeconds(0.5f);
            Vector3 fled = coward.transform.localPosition - from;
            fled.y = 0;
            float fleeSpeed = fled.magnitude / 0.5f;
            Check("a fleeing ANPC runs at run speed", coward.CurrentMode == NpcMode.Fleeing && cowardGait != null &&
                cowardGait.Current == Gait.Run && fleeSpeed > cowardGait.RunSpeed * 0.8f,
                coward.Status + ", speed " + fleeSpeed + " (run " + (cowardGait != null ? cowardGait.RunSpeed : -1f) + ")");
            NpcBrain.Discard(coward);

            // A turret (no walk, no run sheet) stays put while hostile and still faces the player; a world shift does
            // not move it. Only horizontal drift counts: it may still settle onto the ground.
            NpcDefinition turretDef = TestDefinition("selftest_turret", Bravery.Brave, location, player, 0f);
            NpcBrain turret = SpawnDefinition(turretDef, location);
            yield return Settle;
            turret.Teleport(player.position + player.forward * 6f);
            yield return new WaitForSeconds(0.5f);
            Vector3 spot = turret.transform.localPosition;
            turret.SwitchHostile(true);
            yield return new WaitForSeconds(3f);
            Vector3 drift = turret.transform.localPosition - spot;
            drift.y = 0;
            Vector3 toPlayer = player.position - turret.transform.position;
            toPlayer.y = 0;
            float facing = Vector3.Angle(turret.transform.forward, toPlayer);
            NpcGait turretGait = turret.GetComponent<NpcGait>();
            Check("a turret does not move but faces its target", turretGait != null && turretGait.IsTurret && drift.magnitude < 0.05f && facing < 30f,
                "drift=" + drift.magnitude + ", facing off by " + facing);
            Transform town = turret.transform.parent;
            town.position += new Vector3(500f, 0f, 0f);
            yield return null;
            yield return null;
            Vector3 shifted = turret.transform.localPosition - spot;
            town.position -= new Vector3(500f, 0f, 0f);
            shifted.y = 0;
            Check("world shift does not boost or unpin", shifted.magnitude < 0.05f, "moved " + shifted.magnitude);
            turret.SwitchHostile(false);
            NpcBrain.Discard(turret);

            mod.Sprites.Remove("selftest_runner");
            mod.Sprites.Remove("selftest_turret");
            yield return Settle;
        }
    }
}
```

In `Scripts/Runtime/SelfTest.cs` replace the existing `TestSpriteSet(bool withDeathStatic)` with:

```csharp
        static LoadedSpriteSet TestSpriteSet(bool withDeathStatic)
        {
            return TestSpriteSet(withDeathStatic, "idle", "walk", "death", "hit", "attack");
        }

        /// <summary>A small solid-colour sprite set with the named sheets (an attack sheet gets action frame 6 of 8).</summary>
        static LoadedSpriteSet TestSpriteSet(bool withDeathStatic, params string[] names)
        {
            SpriteSet set = new SpriteSet();
            set.PixelsPerUnit = 10f;
            set.CellHeight = 32;
            set.GroundY = 4;
            set.Fps = 8;
            Dictionary<string, Texture2D> sheets = new Dictionary<string, Texture2D>();
            for (int i = 0; i < names.Length; i++)
            {
                SpriteAnimation a = new SpriteAnimation();
                a.Name = names[i];
                a.CellWidth = 16;
                a.Frames = names[i] == "attack" ? 8 : names[i] == "idle" || names[i] == "hit" ? 3 : 2;
                if (a.Name == "attack")
                    a.ActionFrame = 5;
                set.Animations[a.Name] = a;
                sheets[a.Name] = SolidTexture(16 * a.Frames, 32 * 8, new Color32((byte)(40 * i), 120, 200, 255));
            }
            Texture2D deathStatic = withDeathStatic ? SolidTexture(24, 12, new Color32(160, 0, 0, 255)) : null;
            return SpriteLibrary.FromTextures("selftest", set, sheets, deathStatic);
        }
```

(Frame counts of the default call are unchanged: idle 3, walk 2, death 2, hit 3, attack 8. Colours change slightly; no check depends on them.)

In `Steps()`, right before `IEnumerator magic = MagicSteps(location, playerTransform);` add:

```csharp
            IEnumerator gait = GaitSteps(location, playerTransform);
            while (gait.MoveNext())
                yield return gait.Current;
```

Add `"Assets/Game/Mods/AdvancedNPCs/Scripts/Runtime/SelfTestGait.cs",` after the `SelfTest.cs` entry in `AdvancedNPCs.dfmod.json`.

- [ ] **Step 2: Verify it fails to compile** — `bash Tools~/compile-check.sh` → `The type or namespace name 'NpcGait' could not be found`.

- [ ] **Step 3: Implement `NpcGait`** — create `Scripts/Runtime/NpcGait.cs`:

```csharp
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>
    /// Running and turrets (spec run/greet/turret §3). Decides the gait each frame after DFU's EnemyMotor has moved
    /// the controller; while chasing at a run it adds (runSpeed - 1) of that movement on top, so DFU keeps its
    /// pathing, doors and attacks. A turret (no walk and no run sheet) is moved back to its spot every frame.
    /// </summary>
    public class NpcGait : MonoBehaviour
    {
        const float MovingSpeed = 0.15f;     // m/s above which the ANPC counts as moving (as NpcSprite)

        NpcBrain brain;
        NpcDefinition def;
        EnemySenses senses;
        EnemyMotor motor;
        CharacterController controller;
        MobileUnit mobile;
        DaggerfallEntityBehaviour behaviour;
        bool turret;
        Vector3 pinLocal;
        Vector3 lastLocal;
        Gait gait = Gait.Still;
        float boosted;

        public Gait Current
        {
            get { return gait; }
        }

        public bool IsTurret
        {
            get { return turret; }
        }

        /// <summary>Metres added on top of DFU's motor so far (self-test).</summary>
        public float Boosted
        {
            get { return boosted; }
        }

        /// <summary>DFU's enemy move speed for this ANPC, m/s.</summary>
        public float WalkSpeed
        {
            get
            {
                int live = behaviour != null && behaviour.Entity != null ? behaviour.Entity.Stats.LiveSpeed : 50;
                return GaitRules.WalkSpeed(live);
            }
        }

        /// <summary>Running speed (walk speed when it cannot run), m/s.</summary>
        public float RunSpeed
        {
            get { return def != null ? GaitRules.RunSpeed(WalkSpeed, def.RunSpeed, def.CanRun) : WalkSpeed; }
        }

        /// <summary>Call after the sprite (if any) has its set, so a turret can be recognised.</summary>
        public void Init(NpcDefinition definition)
        {
            def = definition;
            brain = GetComponent<NpcBrain>();
            senses = GetComponent<EnemySenses>();
            motor = GetComponent<EnemyMotor>();
            controller = GetComponent<CharacterController>();
            mobile = GetComponentInChildren<MobileUnit>();
            behaviour = GetComponent<DaggerfallEntityBehaviour>();
            NpcSprite sprite = GetComponent<NpcSprite>();
            turret = sprite != null && sprite.Set != null && SpriteStates.IsStationary(sprite.Set.Set);
            Repin();
        }

        /// <summary>Remembers where it stands now (spawn, Teleport). Town-local: floating-origin shifts are not movement.</summary>
        public void Repin()
        {
            pinLocal = transform.localPosition;
            lastLocal = pinLocal;
        }

        void LateUpdate()
        {
            if (brain == null || def == null || brain.CurrentMode == NpcMode.Dead || controller == null || !controller.enabled)
                return;
            Vector3 local = transform.localPosition;
            if (turret)
            {
                gait = Gait.Still;
                Vector3 back = pinLocal - local;
                back.y = 0;
                if (back.sqrMagnitude > 1e-8f)
                    controller.Move(ToWorld(back));
                lastLocal = transform.localPosition;
                return;
            }

            Vector3 moved = local - lastLocal;
            moved.y = 0;
            float dt = Time.deltaTime;
            bool moving = dt > 0 && moved.magnitude / dt > MovingSpeed;
            if (brain.CurrentMode == NpcMode.Fighting)
            {
                gait = GaitRules.Fighting(senses != null && senses.TargetInSight, senses != null ? senses.DistanceToTarget : 0f,
                    gait == Gait.Run, def.CanRun);
                if (gait == Gait.Run && moving && (motor == null || motor.KnockbackSpeed <= 0) &&
                    (mobile == null || !mobile.IsPlayingOneShot()))
                {
                    Vector3 extra = moved * (def.RunSpeed - 1f);
                    controller.Move(ToWorld(extra));
                    boosted += extra.magnitude;
                }
            }
            else if (brain.CurrentMode == NpcMode.Fleeing)
            {
                gait = GaitRules.Fleeing(moving, def.CanRun);
            }
            else
            {
                gait = GaitRules.Calm(moving);
            }
            lastLocal = transform.localPosition;
        }

        Vector3 ToWorld(Vector3 localDelta)
        {
            return transform.parent != null ? transform.parent.TransformVector(localDelta) : localDelta;
        }
    }
}
```

Add `"Assets/Game/Mods/AdvancedNPCs/Scripts/Runtime/NpcGait.cs",` after the `NpcCrime.cs` entry in `AdvancedNPCs.dfmod.json`.

- [ ] **Step 4: Wire the brain** — in `Scripts/Runtime/NpcBrain.cs`:
- Fields after `float calmCheckTimer;`:

```csharp
        float holdStill;        // seconds the calm NPC stands still (greeting)
        NpcGait gait;
```

- In `Start()` after `mover = GetComponent<NpcMover>();` add `gait = GetComponent<NpcGait>();`.
- After the `Instance` property add:

```csharp
        public NpcDefinition Definition
        {
            get { return def; }
        }

        /// <summary>Stops calm wandering for a while (greetings). Fighting and fleeing are not affected.</summary>
        public void HoldStill(float seconds)
        {
            holdStill = Mathf.Max(holdStill, seconds);
        }

        bool IsTurret
        {
            get { return gait != null && gait.IsTurret; }
        }
```

- In `Teleport`, after `hasWanderTarget = false;` add:

```csharp
            NpcGait g = GetComponent<NpcGait>();
            if (g != null)
                g.Repin();
```

- At the top of `UpdateCalm()` insert:

```csharp
            if (holdStill > 0f || IsTurret)
            {
                holdStill = Mathf.Max(0f, holdStill - Time.deltaTime);
                mover.Stop();
                return;
            }
```

- In `UpdateFleeing()` replace the final `if/else` with:

```csharp
            if (IsTurret || (distance >= SafeDistance && !CanSee(threat)))
                mover.Stop(); // cower (a turret never leaves its spot)
            else
                mover.MoveAway(threat.transform.position, gait != null ? gait.RunSpeed : NpcMover.RunSpeed);
```

- Make `CanSee` public with a summary:

```csharp
        /// <summary>A clear line between this NPC and another entity (half a metre above their origins).</summary>
        public bool CanSee(DaggerfallEntityBehaviour other)
```

- [ ] **Step 5: Animation from the gait** — in `Scripts/Runtime/NpcSprite.cs`:
- Field after `MeshRenderer quadRenderer;`: `NpcGait gait;`
- Method after `PlayCast()`:

```csharp
        /// <summary>The gait that chooses run, walk or idle (the spawner sets it before Init).</summary>
        public void UseGait(NpcGait source)
        {
            gait = source;
        }
```

- In `Init`, after `entityBehaviour = GetComponent<DaggerfallEntityBehaviour>();` add `if (gait == null) gait = GetComponent<NpcGait>();`.
- Replace the last line of `WantedState()` (`return speed > WalkSpeed ? SpriteStates.Walk : SpriteStates.Idle;`) with:

```csharp
            if (speed <= WalkSpeed)
                return SpriteStates.Idle;       // standing, or turning on the spot
            return gait != null && gait.Current == Gait.Run ? SpriteStates.Run : SpriteStates.Walk;
```

- In `Draw()`: `bool loop = state == SpriteStates.Idle || state == SpriteStates.Walk || state == SpriteStates.Run;`

- [ ] **Step 6: Spawner** — in `Scripts/Runtime/NpcSpawner.cs` the sprite block becomes:

```csharp
            NpcGait gait = go.AddComponent<NpcGait>();
            if (spriteSets.Count > 0)
            {
                LoadedSpriteSet spriteSet = spriteSets[SpriteStates.PickSet(spriteSets.Count, instance.Seed)];
                float height = def.SpriteHeight > 0 ? def.SpriteHeight : NpcSprite.DefaultHeight(go);
                NpcSprite sprite = go.AddComponent<NpcSprite>();
                sprite.RangedSheet = def.RangedAnimation;
                sprite.UseGait(gait);
                sprite.Init(spriteSet, height, def.SpriteHeightScale);
                if (def.RangedAnimation != null && !spriteSet.Sheets.ContainsKey(def.RangedAnimation))
                    AdvancedNpcsMod.Log(spriteSet.Label + ": no " + def.RangedAnimation + ".png for combat.rangedAnimation; bow shots play attack_range*/attack* sheets.");
            }
            gait.Init(def);
```

- [ ] **Step 7: Verify compile + Core tests** — `bash Tools~/compile-check.sh`, `bash Tools~/runtime-check.sh`, `bash Tools~/run-tests.sh` → OK / Passed 621.

- [ ] **Step 8: Build and run the in-game self-test** — close DFU/Unity; `bash Tools~/build-mod.sh --no-examples`; `git restore Assets/AddressableAssetsData/` (repo root); `bash Tools~/selftest.sh | grep -E "FAIL|SELFTEST DONE"`.
Expected: 6 new checks pass; `SELFTEST DONE 158/158 passed`. If the runner check fails only because there is no line of sight at 14 m, use 10 m (still > RunStart) and say so in the commit message.

- [ ] **Step 9: Commit** (plus any `.meta` files the build created for new scripts)

```bash
git add Assets/Game/Mods/AdvancedNPCs/Scripts/Runtime Assets/Game/Mods/AdvancedNPCs/AdvancedNPCs.dfmod.json
git commit -m "feat(anpc): run when chasing or fleeing, turrets stay put"
```

---

### Task 5: `NpcGreeter` — greet liked players and ANPCs

**Files:**
- Create: `Scripts/Runtime/NpcGreeter.cs`
- Modify: `Scripts/Runtime/NpcSprite.cs` (PlayGreet, GreetStarts, GreetSeconds)
- Modify: `Scripts/Runtime/NpcSpawner.cs` (add NpcGreeter when the set has a greet sheet)
- Modify: `Scripts/Runtime/SelfTestGait.cs` (greeting checks)
- Modify: `AdvancedNPCs.dfmod.json` (add `Runtime/NpcGreeter.cs` after `Runtime/NpcGait.cs`)

**Interfaces:**
- Consumes: `GreetRules`, `GreetMemory` (Task 3); `NpcBrain.HoldStill`, `NpcBrain.CanSee` (Task 4); existing `NpcBrain.All()`, `IsEnemy`, `State.hostile`, `CurrentMode`, `Id`, `EntityBehaviour`; `SpriteStates.Greet` (Task 1).
- Produces: `NpcSprite.PlayGreet() : bool`, `NpcSprite.GreetStarts : int`, `NpcSprite.GreetSeconds : float`; `NpcGreeter.Greetings : int`; statics `NpcGreeter.ReactionOverride : string`, `NpcGreeter.ChanceOverride : float` (self-test; negative = none).

- [ ] **Step 1: Write the failing self-test** — in `Scripts/Runtime/SelfTestGait.cs`, before `mod.Sprites.Remove("selftest_runner");` add:

```csharp
            // Greetings: a calm ANPC with a greet sheet greets a liked player once per encounter, facing them.
            mod.Sprites.Add("selftest_greeter", TestSpriteSet(false, "idle", "walk", "greet", "death"));
            NpcGreeter.ReactionOverride = "likes";
            NpcGreeter.ChanceOverride = 1f;
            try
            {
                NpcDefinition greeterDef = TestDefinition("selftest_greeter", Bravery.Normal, location, player, 0f);
                NpcBrain greeter = SpawnDefinition(greeterDef, location);
                yield return Settle;
                greeter.Teleport(player.position + player.forward * 3f);
                greeter.transform.rotation = Quaternion.LookRotation(player.forward);     // its back to the player
                NpcSprite greetSprite = greeter.GetComponent<NpcSprite>();
                NpcGreeter greetBrain = greeter.GetComponent<NpcGreeter>();
                float until = Time.time + 2f;
                while (Time.time < until && (greetSprite == null || greetSprite.GreetStarts == 0))
                    yield return null;
                yield return new WaitForSeconds(0.4f);
                Vector3 toPlayer = player.position - greeter.transform.position;
                toPlayer.y = 0;
                float facing = Vector3.Angle(greeter.transform.forward, toPlayer);
                Check("a calm ANPC greets a liked player and faces them", greetSprite != null && greetSprite.GreetStarts == 1 && facing < 30f,
                    "greets=" + (greetSprite != null ? greetSprite.GreetStarts : -1) + ", facing off by " + facing);
                // Leave and come back: still within the 2-hour cooldown.
                greeter.Teleport(player.position + player.forward * 9f);
                yield return new WaitForSeconds(0.8f);
                greeter.Teleport(player.position + player.forward * 3f);
                yield return new WaitForSeconds(1.2f);
                Check("no second greeting within the cooldown", greetBrain != null && greetBrain.Greetings == 1,
                    "greetings=" + (greetBrain != null ? greetBrain.Greetings : -1));
                NpcBrain.Discard(greeter);

                NpcGreeter.ReactionOverride = "dislikes";
                NpcDefinition coldDef = TestDefinition("selftest_cold", Bravery.Normal, location, player, 0f);
                coldDef.Folder = "selftest_greeter";
                NpcBrain cold = SpawnDefinition(coldDef, location);
                yield return Settle;
                cold.Teleport(player.position + player.forward * 3f);
                yield return new WaitForSeconds(1.5f);
                NpcSprite coldSprite = cold.GetComponent<NpcSprite>();
                Check("a disliked player is not greeted", coldSprite != null && coldSprite.GreetStarts == 0,
                    "greets=" + (coldSprite != null ? coldSprite.GreetStarts : -1));
                NpcBrain.Discard(cold);
            }
            finally
            {
                NpcGreeter.ReactionOverride = null;
                NpcGreeter.ChanceOverride = -1f;
            }
            mod.Sprites.Remove("selftest_greeter");
```

(`yield return` inside `try` with only a `finally` is valid in a C# iterator.)

- [ ] **Step 2: Verify it fails to compile** — `bash Tools~/compile-check.sh` → `'NpcGreeter' could not be found`.

- [ ] **Step 3: Sprite support** — in `Scripts/Runtime/NpcSprite.cs`:
- Field `int greetStarts;` after `int hitStarts;`, and the property:

```csharp
        /// <summary>How often a greet sheet has started (self-test).</summary>
        public int GreetStarts
        {
            get { return greetStarts; }
        }
```

- After `UseGait` add:

```csharp
        /// <summary>Plays a greet sheet once; false when the set has none or an attack, cast or hit sheet is playing.</summary>
        public bool PlayGreet()
        {
            if (set == null || SpriteStates.Variants(set.Set, SpriteStates.Greet).Count == 0 ||
                (oneShotLeft > 0 && state != SpriteStates.Greet))
                return false;
            StartOneShot(SpriteStates.Greet);
            greetStarts++;
            return true;
        }

        /// <summary>Seconds a greet sheet plays (0 without one).</summary>
        public float GreetSeconds
        {
            get
            {
                List<SpriteAnimation> greets = set != null ? SpriteStates.Variants(set.Set, SpriteStates.Greet) : null;
                return greets != null && greets.Count > 0 ? (float)greets[0].Frames / set.Set.Fps : 0f;
            }
        }
```

- [ ] **Step 4: Implement `NpcGreeter`** — create `Scripts/Runtime/NpcGreeter.cs`:

```csharp
using UnityEngine;
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>
    /// Greetings (spec run/greet/turret §5): a calm ANPC with a greet sheet now and then stops, turns to a player or
    /// ANPC it likes within 4 m and in sight, and plays the sheet once. Once per encounter, 25 %, 2 game hours per target.
    /// </summary>
    public class NpcGreeter : MonoBehaviour
    {
        const float CheckInterval = 0.5f;
        const float TurnDegreesPerSecond = 540f;

        /// <summary>Self-test: forces the reaction band toward the player (null = DFU's).</summary>
        public static string ReactionOverride;
        /// <summary>Self-test: forces the chance (negative = GreetRules.Chance).</summary>
        public static float ChanceOverride = -1f;

        readonly GreetMemory memory = new GreetMemory();
        NpcBrain brain;
        NpcSprite sprite;
        float nextCheck;
        Transform facingTarget;
        float facingLeft;

        /// <summary>Greetings given (self-test).</summary>
        public int Greetings
        {
            get { return memory.Count; }
        }

        void Start()
        {
            brain = GetComponent<NpcBrain>();
            sprite = GetComponent<NpcSprite>();
            nextCheck = Time.time + Random.Range(0f, CheckInterval);
        }

        void Update()
        {
            if (brain == null || sprite == null || brain.CurrentMode != NpcMode.Calm)
            {
                facingTarget = null;
                return;
            }
            if (facingTarget != null && facingLeft > 0f)
            {
                facingLeft -= Time.deltaTime;
                Vector3 to = facingTarget.position - transform.position;
                to.y = 0;
                if (to.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), TurnDegreesPerSecond * Time.deltaTime);
            }
            if (Time.time < nextCheck)
                return;
            nextCheck = Time.time + CheckInterval;
            CheckCandidates();
        }

        void CheckCandidates()
        {
            bool selfCalm = brain.CurrentMode == NpcMode.Calm && !brain.State.hostile && !brain.IsEnemy;
            double now = DaggerfallUnity.Instance.WorldTime.Now.ToSeconds() / 3600.0;
            double chance = ChanceOverride >= 0f ? ChanceOverride : GreetRules.Chance;

            DaggerfallEntityBehaviour player = GameManager.Instance.PlayerEntityBehaviour;
            if (player != null)
            {
                bool near = Near(player.transform) && brain.CanSee(player);
                bool likes = near && GreetRules.LikesPlayer(selfCalm, PlayerReactionBand());
                if (memory.Consider(GreetRules.PlayerKey, near, likes, now, Random.value, chance))
                {
                    Greet(player.transform);
                    return;
                }
            }
            foreach (NpcBrain other in NpcBrain.All())
            {
                if (other == brain || other.CurrentMode == NpcMode.Dead || other.EntityBehaviour == null)
                    continue;
                bool near = Near(other.transform) && brain.CanSee(other.EntityBehaviour);
                bool likes = near && GreetRules.SameSide(selfCalm, brain.IsEnemy,
                    other.CurrentMode == NpcMode.Calm && !other.State.hostile, other.IsEnemy);
                if (memory.Consider(other.Id, near, likes, now, Random.value, chance))
                {
                    Greet(other.transform);
                    return;
                }
            }
        }

        bool Near(Transform target)
        {
            Vector3 d = target.position - transform.position;
            d.y = 0;
            return d.magnitude <= GreetRules.Distance;
        }

        void Greet(Transform target)
        {
            facingTarget = target;
            facingLeft = 0.6f;
            if (sprite.PlayGreet())
                brain.HoldStill(sprite.GreetSeconds + 0.3f);
        }

        /// <summary>DFU's reaction to the player for a townsperson here (the region's people), as a dialogue band.</summary>
        static string PlayerReactionBand()
        {
            if (ReactionOverride != null)
                return ReactionOverride;
            try
            {
                FactionFile.FactionData people;
                PlayerEntity p = GameManager.Instance.PlayerEntity;
                if (TalkManager.Instance == null ||
                    !p.FactionData.GetFactionData(GameManager.Instance.PlayerGPS.GetPeopleOfCurrentRegion(), out people))
                    return "neutral";
                return ConditionParser.ReactionBand(TalkManager.Instance.GetReactionToPlayer(people));
            }
            catch (System.Exception)
            {
                return "neutral";
            }
        }
    }
}
```

- [ ] **Step 5: Spawner** — in `Scripts/Runtime/NpcSpawner.cs`, inside the sprite block right after `sprite.Init(...)`:

```csharp
                if (SpriteStates.Variants(spriteSet.Set, SpriteStates.Greet).Count > 0)
                    go.AddComponent<NpcGreeter>();
```

Add `"Assets/Game/Mods/AdvancedNPCs/Scripts/Runtime/NpcGreeter.cs",` after `NpcGait.cs` in `AdvancedNPCs.dfmod.json`.

- [ ] **Step 6: Verify** — compile-check, runtime-check, run-tests; build with `--no-examples`; `git restore Assets/AddressableAssetsData/`; `bash Tools~/selftest.sh | grep -E "FAIL|SELFTEST DONE"` → `161/161`.

- [ ] **Step 7: Commit** (plus new `.meta` files)

```bash
git add Assets/Game/Mods/AdvancedNPCs/Scripts/Runtime Assets/Game/Mods/AdvancedNPCs/AdvancedNPCs.dfmod.json
git commit -m "feat(anpc): greet liked players and same-side ANPCs"
```

---

### Task 6: Docs, version, sample smoke, final verification

**Files:**
- Modify: `README.md`, `AdvancedNPCs.dfmod.json` (`"ModVersion": "0.11.0"`)

- [ ] **Step 1: README**
- Settings table row for `combat`: example `{ "health": 0.7, "attackSpeed": 1.3, "rangedAtDistance": true, "runSpeed": 1.2, "canRun": true }`.
- Combat tuning section, after the `rangedAnimation` bullet and its JSON block, add:

```markdown
- `runSpeed` (1–2, default 1.2): how much faster than walking the ANPC runs. A fighting ANPC runs when it sees its
  target more than 6 m away and walks again under 4 m; a fleeing ANPC runs. DFU's walking speed for enemies is
  about the player's, so the default lets a running player escape or catch up.
- `canRun` (true/false, default true): `false` never runs; it chases and flees at walking speed with walk sheets.
```

- Sheet-names bullet: add `run*` (running) and `greet*` (greeting) to the list, and replace its fallback sentence with: `Missing cast sheets use attack sheets (no attack sheet either: spells play no animation); missing attack_range sheets use attack sheets; missing run sheets use walk sheets and missing walk sheets use run sheets, then idle; missing hit/attack sheets use idle; greet sheets have no fallback; a set needs at least one idle sheet.`
- After that bullet add:

```markdown
- Turret: a set with no `walk*` and no `run*` sheet never moves: it does not wander, chase, flee or get knocked
  back, but still turns to its target and attacks, shoots or casts from where it stands.
- Greeting: a calm ANPC with a `greet*` sheet sometimes (25 % per encounter) stops, turns and greets the player
  within 4 m and in sight when its people like you (`likes` or `loves`, the reaction dialogue uses), or another
  calm ANPC that is not an enemy. It greets the same person at most once every 2 game hours (not saved).
```

- [ ] **Step 2: Version** — `"ModVersion": "0.11.0"` in `AdvancedNPCs.dfmod.json`.

- [ ] **Step 3: Full verification** — run-tests (Passed 621), compile-check, runtime-check, build `--no-examples`, `git restore Assets/AddressableAssetsData/`, selftest → `161/161` (re-run once if only the known flaky magic-edge pair fails).

- [ ] **Step 4: Sample smoke** — the installed samples' `run`/`greet` sheets must load. After the self-test run:

```bash
grep -E "(run|greet_[0-9]+)\.png: (file: must be|not an)" "$USERPROFILE/AppData/LocalLow/Daggerfall Workshop/Daggerfall Unity/Player.log" || echo "no run/greet sheet problems"
```

Expected: `no run/greet sheet problems`.

- [ ] **Step 5: Commit**

```bash
git add Assets/Game/Mods/AdvancedNPCs/README.md Assets/Game/Mods/AdvancedNPCs/AdvancedNPCs.dfmod.json
git commit -m "docs(anpc): running, greetings and turrets; mod 0.11.0"
```

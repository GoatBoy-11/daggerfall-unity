# Optional AI Packages Implementation Plan

**Goal:** Add optional reusable JSON packages and schedules, preserving existing AI as fallback.
**Architecture:** Pure Core schedule/package validation and motion decisions, called through a runtime driver only from calm AI. The mod owns a reloadable package library.
**Tech Stack:** C# 4, Unity 2019, DFU runtime, existing NUnit host.
**Spec:** ../specs/2026-10-13-advanced-npcs-packages-design.md

## Constraints and review focus
Preserve uncommitted work. Invalid optional data must not remove NPCs. Time skips select the current slot. Overrides must not mutate shared data. Interruptions and failures release control. All sources must be in the manifest.

## Tasks
- [x] Add Core schedule/package validation and selection with boundary/fallback/isolation tests.
- [x] Add testable local motion progress, patrol waits and stuck recovery.
- [x] Load packages and hook calm AI, reload, diagnostics and running animation.
- [x] Add schemas, portable examples, opt-in test NPC and author documentation.
- [x] Run full tests, schemas, compilation and runtime compiler checks; review diff.

Execute in this chat under the user's explicit instruction to create the branch and start building the agreed design.

## Results

- Branch: feature/anpc-ai-packages.
- Core NUnit: 872 passed, 0 failed, 0 skipped, executed with cached NUnit/Mono. Unity 6 batch test host lacked an Editor license.
- DFU runtime compiler: passed in both source orders; 159 types loaded.
- Unity 2019 bundle build: succeeded at F:/_Projects/Dagerfall/output/ai-packages-build/StandaloneWindows/advancednpcs.dfmod.
- Focused in-game suite: 9/9 passed. Test restored the prior installed bundle/settings.
- New package schema and all five new examples: passed.
- Full schema validation: 28 existing sprite/dialogue errors (talk/pray animation names and guild-list condition), outside package scope. Parser/schema field parity passes.
- git diff --check: passed. Existing uncommitted healing/sprite work retained.

Logs: output/ai-packages-core-tests/results.xml, output/ai-packages-selftest.log and output/ai-packages-build.log under F:/_Projects/Dagerfall.

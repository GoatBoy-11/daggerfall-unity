# Advanced NPCs Phase C2 (reply choices and actions) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Player reply choices after ANPC answers (DFU list picker), actions (gold, items, reputation, quests,
enemy, end conversation), and the C1 leftovers (duplicate JSON keys, override notes).

**Architecture:** Core parses replies/actions into the dialogue model and decides which replies are offered and
what a line does (`Conversation.Replies`, `Conversation.Say`, `IDialogueActions`); runtime performs actions through
`GameFacts` and drives the picker from `TopicInjector`.

**Tech Stack:** C# for DFU 1.9.2's runtime compiler; NUnit in the Unity 6 test host; mod's own Json.cs.

**Spec:** `docs/superpowers/specs/2026-10-08-advanced-npcs-replies-design.md`

**Execution:** native, user AFK with full autonomy; fresh reviewer at the end.

## Global Constraints

- Stock DFU 1.9.2 APIs only (compile-check.sh decides).
- Nesting limit 8; gold 1–100000; reputation −20…20.
- Replies with takeGold/takeItem only offered when affordable; topics with them only listed when affordable.
- Message format `<file>: topic "<caption>": <field>: <problem>`.
- No new save fields.

## Review Focus

1. Picker open when the talk window closes (Escape twice, walking away) → picker closes, no exception.
2. A reply's answer with replies AND the reply's own replies → the answer's win (same rule as topics).
3. becomeEnemy while the picker is open → window closes first, then the ANPC turns hostile (no talking to an enemy).
4. takeGold when gold changed between listing and picking → re-check before taking; refuse with a line.
5. A topic used again while its picker is open (keyboard) → no second picker.

## Tasks

1. **Model + parsing** (`Dialogue.cs`, `DialogueParser.cs`; tests `ReplyParserTests.cs`): `DialogueReply { Id, Text,
   When, Answers, Replies, Actions }`, `DialogueActions { GiveGold, TakeGold, GiveItem, TakeItem, Reputation,
   StartQuest, BecomeEnemy, EndConversation }` on topic/answer/reply; `replies` on topics and answers; nesting limit.
2. **Conversation rules** (`Conversation.cs`; tests): `IDialogueActions` (Gold, HasItemNamed, GiveGold/TakeGold/
   GiveItem/TakeItem/ChangeReputation/StartQuest/BecomeEnemy/EndConversation); `Conversation.Replies(topic, answer,
   facts, actions)`; `Conversation.Say(reply, facts, rng, state, actions)`; actions order; affordability for topics.
3. **Json duplicates + override notes** (`Json.cs`, `DialogueParser.cs`, `DefinitionParser.cs`, `DialogueLibrary.cs`).
4. **Runtime actions** (`GameFacts.cs`, `DialogueFiles.cs`): gold, items by template name (load check), reputation via
   `PlayerGPS.GetPeopleOfCurrentRegion()` + `PersistentFactionData.ChangeReputation`, `QuestMachine.StartQuest`,
   enemy via `NpcBrain.SwitchHostile(true)` after `PopToHUD`, end via `PopToHUD`.
5. **Picker flow** (`TopicInjector.cs`): open next frame, OnItemPicked, cancel, closing window, guard double picker.
6. **Content, README, anpc_topics, self-test** (`tavern_wench.json`, README "Replies and actions", SelfTest checks).
7. **Verify + review**: run-tests, compile-check, runtime-check, build, selftest ×3, fresh reviewer, fixes.

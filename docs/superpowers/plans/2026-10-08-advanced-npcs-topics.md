# Advanced NPCs Phase C1 (dialogue topics) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** JSON-authored "Tell Me About" topics for ANPCs: shared dialogue types, conditions, follow-ups, conditional
answers, flags, tone-aware questions, greetings, author tools; vertical slice on the user's `wench`.

**Architecture:** All rules live in Core (pure C#, NUnit): parse files → model; `Condition.Holds(IDialogueFacts)`;
`Conversation` decides visible topics, answers and effects. Runtime supplies DFU facts (`GameFacts`), injects topics
into DFU's talk window (`TopicInjector`, mechanism from the spike) and saves flags/asked.

**Tech Stack:** C# (DFU 1.9.2 runtime compiler: no nested enums, no enum-typed fields across files), Unity 2019.4,
NUnit in the Unity 6 test host, mod's own `Json.cs`.

**Spec:** `docs/superpowers/specs/2026-10-08-advanced-npcs-topics-design.md`

**Execution:** native (the user's standing preference: spec → plan → native execution → fresh reviewer). Core
tasks give exact interfaces and test lists; implementations follow the existing code style (FieldReader, message
format `file: field: problem`).

## Global Constraints

- Works on stock DFU 1.9.2; no core changes; no `UIWindowFactory` override.
- Message format: `<file>: <topic caption or field>: <problem>`; one line per problem; a bad topic never stops others.
- Tone indices match `DaggerfallTalkWindow.TalkToneToIndex`: polite 0, normal 1, blunt 2; -1 = unknown.
- Reaction bands: dislikes < 0, neutral 0–9, likes 10–29, loves ≥ 30.
- Ids and flag names: lowercase letters, digits, `_` after normalisation.
- Limits: 40 topics per file, 30 visible extra topics per conversation, caption > 24 chars warns.
- Store tone as `int`, never as an enum field (DFU runtime compiler TypeLoadException).

## Review Focus

1. Topic used via OK button or keyboard (not double-click) → answer still replaced (hook OK + per-frame check).
2. Quest adds a topic mid-conversation (`AssembleTopicLists`) → our items re-inserted, not lost.
3. Changing the list inside the click handler → re-triggered click (defer refresh one frame).
4. Topic whose only answers are tone-conditional → shown regardless of tone; never empty answer.
5. Old saves without `Flags` / `asked` → load as empty, no exception.

Each is covered: 1–3 by self-test checks (Task 8), 4 by `ConversationTests`, 5 by `NpcStateTableTests`/save code.

---

### Task 1: Ids, text macros, model

**Files:** Create `Scripts/Core/Dialogue.cs`, `Scripts/Core/TextMacros.cs`; Test `Editor/Tests/TextMacrosTests.cs`.

**Produces:**
- `DialogueIds.Normalize(string) -> string` (lowercase; runs of chars outside `[a-z0-9]` → `_`; trim `_`).
- `Tones` constants `Polite=0, Normal=1, Blunt=2`; `Tones.Parse(string) -> int` (-1 invalid).
- `QuestionText { string All, Polite, Normal, Blunt; string For(int tone) }` (tone override → `Normal` → `All` →
  default line for the tone).
- `DialogueAnswer { string Text; Condition When; List<string> Sets, Clears }`.
- `DialogueTopic { string Id, Caption, Source; Condition When; QuestionText Question; List<DialogueAnswer> Answers;
  List<string> Sets, Clears; bool Once }`.
- `DialogueFile { string Name; List<DialogueTopic> Topics; List<DialogueAnswer> Greetings }`.
- `TextMacros.Expand(string text, IDictionary<string,string> values) -> string`;
  `TextMacros.Unknown(string text, bool allowTopic) -> List<string>`. Known: player, npc, town, region, topic.

**Tests:** normalise ("Any rumours?" → `any_rumours`, "  The  North-Gate " → `the_north_gate`, "" → ""); expand
known; leave unknown `{foo}` as written; `Unknown` finds `foo`, ignores `topic` only when allowed; unbalanced braces
untouched; `QuestionText.For` fallback chain incl. default lines with `{topic}`.

### Task 2: Conditions

**Files:** Create `Scripts/Core/Condition.cs`; Test `Editor/Tests/ConditionTests.cs`.

**Produces:**
- `interface IDialogueFacts { int Hour; bool IsNight; string Season; bool Raining, Storming, Snowing, Overcast;
  string Region, Town; int PlayerLevel; string PlayerRace; string PlayerGender; int Gold; bool HasItem(string);
  int GuildRank(string guildKey) /* -1 = not a member */; string Reaction; int Tone; bool HasFlag(string);
  bool Asked(string topicId); bool QuestGlobal(string nameOrNumber); }`
- `Condition { bool Holds(IDialogueFacts f); string FirstFailing(IDialogueFacts f) /* key or null */;
  List<string> AskedIds; List<string> QuestGlobals; bool UsesTone; bool IsEmpty }`
- `ConditionParser.Parse(Dictionary<string,object> o, string where, List<string> messages, bool allowTone) -> Condition`
- Guild keys: `fightersguild, magesguild, thievesguild, darkbrotherhood, temple, knightlyorder` (`Guilds.Key(name)`).

**Tests (per key):** hours plain/wrapping/all-day; time; season incl. `fall`=`autumn` and list; weather `rain`
includes storm; region/town case-insensitive; min/maxLevel; playerRace spaces ignored; playerGender; minGold;
hasItem list = any; guild + minGuildRank; reaction list; asked/notAsked all/none; flags/notFlags; questGlobal;
tone holds when facts tone = -1; `any`; empty `when` holds; unknown key → message + ignored; bad value → message,
rest still applies; `tone` with `allowTone=false` → message + ignored; `FirstFailing` returns the key.

### Task 3: Dialogue file parser

**Files:** Create `Scripts/Core/DialogueParser.cs`; Test `Editor/Tests/DialogueParserTests.cs`.

**Produces:** `DialogueParser.Parse(string source, string json) -> DialogueParseResult { DialogueFile File;
List<string> Messages }` (File null only for unreadable JSON / non-object root / no `topics` and no `greetings`).

**Tests:** minimal topic; id defaults from caption; duplicate id → second skipped; missing caption/answers →
topic skipped, others kept; answer as text or object; question text / per tone / bad tone key; sets/clears normalised;
once; > 40 topics warns and truncates; long caption warns; unknown topic field warns; unknown placeholder warns;
greetings with `tone` warn; `asked` to unknown id in same file warns; invalid JSON → File null + message.

### Task 4: Library, definition field, catalog

**Files:** Create `Scripts/Core/DialogueLibrary.cs`; Modify `NpcDefinition.cs`, `DefinitionParser.cs`,
`DefinitionCatalog.cs`; Tests `Editor/Tests/DialogueLibraryTests.cs`, add cases to `DefinitionParserTests.cs`,
`DefinitionCatalogTests.cs`.

**Produces:**
- `NpcDefinition.Dialogue : List<string>` (type ids), `NpcDefinition.OwnDialogue : DialogueFile`.
- `npc.json` `dialogue`: text or list of texts → normalised ids; wrong type → warning, ignored (NPC still loads).
- Catalog parses every folder's `dialogue.json` (generic too) into `OwnDialogue`; old "unique only" warning removed.
- `ComposedDialogue { List<string> Types; List<DialogueTopic> Topics; List<DialogueAnswer> Greetings; bool IsEmpty }`
- `DialogueLibrary { void Add(DialogueFile); bool Has(string); int Count; IEnumerable<DialogueFile> Files;
  ComposedDialogue Compose(NpcDefinition d); List<string> MissingTypes(IEnumerable<NpcDefinition>) }`

**Tests:** compose order; same id replaces in place; folder last; greetings concatenated; unknown type skipped and
reported by `MissingTypes`; definition with no dialogue → empty.

### Task 5: Conversation rules, flags, asked

**Files:** Create `Scripts/Core/Conversation.cs`, `Scripts/Core/FlagSet.cs`; Modify `NpcState.cs`; Tests
`Editor/Tests/ConversationTests.cs`, `Editor/Tests/FlagSetTests.cs`, add to `NpcStateTableTests.cs`.

**Produces:**
- `interface IDialogueState { void SetFlag(string); void ClearFlag(string); void MarkAsked(string topicId); }`
- `Conversation.Visible(ComposedDialogue d, IDialogueFacts f, int max) -> List<DialogueTopic>`;
  `Conversation.WhyHidden(DialogueTopic t, IDialogueFacts f) -> string` (null = shown; `once`, `when: <key>`,
  `no answer applies`); `Conversation.PickAnswer(List<DialogueAnswer> a, IDialogueFacts f, Random rng, bool ignoreTone)`;
  `Conversation.Ask(DialogueTopic t, IDialogueFacts f, Random rng, IDialogueState s) -> string` (text, applies
  topic + answer sets/clears, marks asked); `Conversation.Greeting(ComposedDialogue d, IDialogueFacts f, Random rng)
  -> string` (null = vanilla).
- `FlagSet { bool Set(string), bool Clear(string), bool Has(string), List<string> Names(), void Restore(IEnumerable<string>), void ClearAll() }`
- `NpcState.asked : List<string>` (Clone copies, IsDefault requires empty).

**Tests:** specific beats plain; random among winners (seeded); topic with only conditional answers hidden when none
applies; tone-only answers → topic shown at tone -1, at blunt the blunt answer, at polite the plain/first answer;
once hides after asked; asked follow-up appears after Ask; sets/clears applied (topic + answer); cap at max;
greeting none → null; state with asked not default and cloned.

### Task 6: Runtime loading, facts, save data

**Files:** Create `Scripts/Runtime/DialogueFiles.cs`, `Scripts/Runtime/GameFacts.cs`; Modify `AdvancedNpcsMod.cs`,
`NpcSaveDataInterface.cs`, `AnpcFiles.cs`.

- `AdvancedNpcsMod.Dialogue : DialogueLibrary`, `Flags : FlagSet`; load `_Dialogue/*.json` at Awake; warn missing
  types once; check quest-global names against `QuestMachine.Instance.GlobalVarsTable` (when available).
- `NpcSaveData.Flags : List<string>` (null-safe restore); new game clears flags.
- `GameFacts(NpcBrain brain, Func<int> tone)` implements `IDialogueFacts` and `IDialogueState`, plus
  `Macros() -> Dictionary<string,string>`; reaction from `TalkManager.reactionToPlayer` (reflection, neutral fallback).

### Task 7: Talk window injection

**Files:** Create `Scripts/Runtime/TopicInjector.cs`; Modify `NpcTalk.cs`.

`TopicInjector` (MonoBehaviour on the mod object): `Begin(DaggerfallTalkWindow w, NpcTalk talk, ComposedDialogue d)`,
`bool Active`, `List<string> ShownCaptions()`, `int AnsweredCount`. Implements spec §7 steps 2–7. `NpcTalk.TryTalk`
calls it after the portrait. Reflection failure → one log line, no topics.

### Task 8: Author tools, mod messages, self-test, content, README

**Files:** Modify `AdvancedNpcsMod.cs` (commands `anpc_topics`, `anpc_reload_dialogue`, `anpc_flag`; messages
`SetFlag`, `HasFlag`), `SelfTest.cs`; Create `Examples/ANPCs/_Dialogue/tavern_wench.json`,
`Examples/ANPCs/_Dialogue/README.txt`; Modify `README.md`, `Tools~/build-mod.sh` if it copies examples by list;
DFU_testing `ANPCs/wench/npc.json` gets `"dialogue": "tavern_wench"`.

Self-test checks (spec §10), each one `Check(...)` line: inserted after "Where am I?"; hour-conditional topic;
answer + tone question in conversation; OK-button path; follow-up same conversation; once disappears; flag crosses
NPCs; specific beats plain; blunt player line; greeting replaced; list rebuild re-inserts; close removes; vanilla
"Where am I?" still answers. LOOK: wench talk window screenshot.

### Task 9: Verify and review

Run `Tools~/run-tests.sh`, `compile-check.sh`, `runtime-check.sh`, `build-mod.sh` (then
`git restore Assets/AddressableAssetsData/`), `selftest.sh` ×3. Fresh reviewer on the whole branch; fix findings.

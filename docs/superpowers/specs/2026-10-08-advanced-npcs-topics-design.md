# Advanced NPCs — Phase C1: dialogue topics (design)

Date: 2026-10-08. Builds on the v2 spec §9 (talking) and the topics spike findings
(`2026-10-07-advanced-npcs-topics-spike-findings.md`). The user approved the folder layout (§3) and gave full autonomy
for the rest while away, with one priority: **custom dialogue must be intuitive and easy to write**.

## 1. Summary

ANPCs get extra "Tell Me About" topics written in JSON. Topics come from shared **dialogue types**
(`ANPCs/_Dialogue/<type>.json`, e.g. `tavern_wench`) that any ANPC, generic or unique, can use, plus an optional
per-folder `dialogue.json`. Topics can be shown only under **conditions** (time, place, weather, player, the NPC's
regard for the player, flags, quest globals), unlock **follow-up topics**, give **different answers** under different
conditions, and set **flags** other topics (and other NPCs) react to. The player's question follows the tone buttons.
A type can also replace the NPC's **greeting**.

Phase C is split: **C1** (this spec) uses DFU's own topic list. **C2** (later, own spec) adds player reply choices in
a popup, reusing C1's `when` conditions and file format.

Vertical slice: the user's `wench` ANPC uses a new `tavern_wench` type with a handful of test topics showing every
feature; her portrait (`wench_unique.png`) is already assigned and matches her sprite.

## 2. Goals and success criteria

1. An author writes `ANPCs/_Dialogue/tavern_wench.json` and adds `"dialogue": "tavern_wench"` to an ANPC's `npc.json`.
   No Unity, no ids needed for simple topics.
2. Talking to that ANPC lists its topics in "Tell Me About" **right after "Where am I?"** (before quest and guild
   topics). Choosing one shows the question in the player's chosen tone and the authored answer, laid out like vanilla.
3. Vanilla topics keep working; other NPCs never show the topics; nothing reaches DFU's conversation save data.
4. Topics with a `when` show only while it holds. A topic with `"asked": "<topic>"` appears **in the same
   conversation** right after that topic is asked. A `"once"` topic disappears after it is asked.
5. Answers with a `when` that holds win over plain answers; among the winners one is picked at random.
6. `sets` / `clears` change player-wide flags that are saved with the game; `asked` is remembered per person.
7. A type's `greetings` replace the vanilla greeting when one matches.
8. Every data problem gives one `[AdvancedNPCs]` line naming file, topic and field; a bad topic never stops the
   rest. Typos in condition keys, values and `{placeholders}` are reported at load.
9. `anpc_topics` shows, for the ANPC in front of the player, which topics are shown and why the others are hidden;
   `anpc_reload_dialogue` reloads all dialogue files without restarting; `anpc_flag` lists, sets and clears flags.

## 3. Files

```
ANPCs/
  _Dialogue/
    tavern_wench.json      a dialogue type (file name = type id)
  wench/
    npc.json               "dialogue": "tavern_wench"
  cooper_jareth/
    npc.json               "dialogue": ["gossip", "tavern_wench"]
    dialogue.json          topics only this folder's people have
```

- Type ids follow folder-name rules (lowercase letters, digits, `_`); the file name is normalised like name lists.
- `npc.json` field `dialogue`: one type id or a list. Allowed for unique and generic ANPCs (the v2 "unique only"
  rule and its warning are dropped).
- A folder's own `dialogue.json` is now used by generic templates too (every person made from the template).
- **Composition:** topics of the types in listed order, then the folder's own file. A topic whose `id` equals an
  earlier one **replaces** it in place. Greetings are composed the same way (all lists concatenated; folder last).
- Unknown type id: one warning naming the ANPC (`wench/npc.json: dialogue "tavern_wnch": no such file in _Dialogue`),
  the ANPC loads without it.
- Limits: 40 topics per file (warning, extras ignored); 30 visible extra topics per conversation (after conditions;
  the rest are dropped with one log line per conversation).

## 4. File format

```json
{
  "greetings": [
    { "when": { "reaction": "dislikes" }, "text": "You again. Drink or leave." },
    "Welcome in, {player}! Mind the floor."
  ],
  "topics": [
    {
      "caption": "Any rumours?",
      "id": "rumours",
      "answers": ["A sailor's gone missing from the docks. Ask me about him if you care."],
      "sets": ["heard_sailor_rumour"]
    },
    {
      "caption": "The missing sailor",
      "when": { "asked": "rumours" },
      "question": { "polite": "Could you tell me more about the sailor?", "blunt": "The sailor. Talk." },
      "answers": [
        { "when": { "tone": "blunt" }, "text": "Watch your mouth. ...Fine. The old warehouse." },
        "Jory, his name was. Last seen near the old warehouse."
      ],
      "once": true
    }
  ]
}
```

### 4.1 Topic fields

| Field | Required | Meaning |
|---|---|---|
| `caption` | yes | Text in the topic list. Longer than 24 characters → warning (narrow list). |
| `id` | no | Name other topics use in `asked`. Default: the caption normalised (lowercase, runs of non-letters/digits → `_`, trimmed): "Any rumours?" → `any_rumours`. Must be unique in the file (case ignored). |
| `answers` | yes | One or more answers: a text, or `{ "text": ..., "when": {...}, "sets": [...], "clears": [...] }`. |
| `when` | no | Condition for showing the topic (§5). |
| `question` | no | The player's line. A text (all tones) or `{ "polite", "normal", "blunt" }` (missing tones fall back to `normal`, then to the default). Default per tone: polite "Could you tell me about {topic}?", normal "Tell me about {topic}.", blunt "What do you know about {topic}?". |
| `sets` / `clears` | no | Flags set / cleared when the topic is answered (in addition to the chosen answer's own). |
| `once` | no | `true`: hidden for this person after it was asked once. |

**Answer choice:** answers whose `when` holds are *specific*; answers without `when` are *plain*. If any specific
answer holds, one of those is picked at random, otherwise one of the plain ones. If none qualifies, the topic is not
shown (so a topic whose only answers are conditional disappears when none applies). For deciding whether a topic is
shown, `tone` conditions count as holding (the tone can still change before the player asks); if, when asked, no
answer qualifies under the actual tone, a plain answer is used, or else the first answer.

### 4.2 Greetings

`greetings`: same answer syntax (no `sets`/`clears`; `tone` not allowed — the player has not chosen one yet). When a
conversation starts, the greeting is picked with the answer rule; if none qualifies, DFU's own greeting stays.

### 4.3 Placeholders

In captions, questions, answers and greetings: `{player}` (player's name), `{npc}` (the ANPC's name), `{town}`,
`{region}`, and in questions `{topic}` (the caption). An unknown `{word}` is a load warning and is shown as written.

## 5. Conditions (`when`)

A `when` object holds if **every key in it holds**. `"any": [ {...}, {...} ]` holds if at least one of its objects
holds (for OR). A list value in a "one of" key means *any of these*; in `asked`/`flags` keys it means *all of these*.

| Key | Value | Holds when |
|---|---|---|
| `hours` | `[from, to]` whole hours 0–23 | game hour in `[from, to)`, wrapping past midnight (`[20, 6]`) — same rule as `hostileHours` |
| `time` | `day` / `night` | DFU's `IsDay` / `IsNight` |
| `season` | one of `spring`, `summer`, `autumn` (or `fall`), `winter` | current season |
| `weather` | one of `clear`, `overcast`, `rain`, `storm`, `snow` | player's weather (`rain` includes storms) |
| `region` | one of region names | current region (case ignored) |
| `town` | one of place names | current location name (case ignored) |
| `minLevel` / `maxLevel` | number | player level ≥ / ≤ |
| `playerRace` | one of race names (`Breton`, `Redguard`, `Nord`, `DarkElf`, `HighElf`, `WoodElf`, `Khajiit`, `Argonian`; spaces ignored) | player's race |
| `playerGender` | `Male` / `Female` | player's gender |
| `minGold` | number | gold carried ≥ |
| `hasItem` | one of item names | player carries an item whose name or long name matches (case ignored) |
| `guild` | one of `Fighters Guild`, `Mages Guild`, `Thieves Guild`, `Dark Brotherhood`, `Temple`, `Knightly Order` (spaces ignored) | player is a member |
| `minGuildRank` | number 0–10 | with `guild`: rank in that guild ≥ |
| `reaction` | one of `dislikes`, `neutral`, `likes`, `loves` | how this NPC regards the player: DFU's reaction value < 0 / 0–9 / 10–29 / ≥ 30 (the bands DFU uses for its greetings) |
| `asked` / `notAsked` | topic id(s) | all / none of these were asked of this person |
| `flags` / `notFlags` | flag name(s) | all / none of these flags are set |
| `questGlobal` / `notQuestGlobal` | name(s) from DFU's `Quests-GlobalVars` table, or numbers 0–63 | all / none are true |
| `tone` | one of `polite`, `normal`, `blunt` | the player's tone (answers only; elsewhere a load warning and ignored) |
| `any` | list of `when` objects | at least one holds |

`asked`/`notAsked` accept an id or a caption (both are normalised the same way). An `asked`/`notAsked` naming no
topic of an ANPC's **composed** set is a load warning (checked per ANPC after composition, so references across
files are checked too, without false warnings). Default question lines lowercase a leading "The/A/An" of the
caption ("Tell me about the house ale."); authored questions are used as written.
Unknown keys and invalid values are load warnings naming the file, topic and key; an invalid key is ignored (the
rest of the condition still applies). Flag names: lowercase letters, digits and `_` (normalised like ids).
Quest-global names are checked against DFU's table when the game loads the files (Core only checks the syntax).

## 6. State

- **Flags**: one player-wide set of names, saved in the mod's save data (`NpcSaveData.Flags`, missing in older
  saves = empty). Shared by all NPCs and files, so one NPC's answer can unlock another NPC's topic.
- **Asked**: per person, `NpcState.asked` (list of topic ids). Saved like the rest of the state (unique ANPCs,
  placed people, generic people in same-people mode; not saved in random mode). `IsDefault` treats a non-empty
  list as non-default.
- Other mods: mod messages `SetFlag` (data `object[] { string name, bool on }`) and `HasFlag` (data `string`,
  callback gets `bool`).

## 7. In the talk window (`TopicInjector`, from the spike)

1. `NpcTalk.TryTalk` opens the window as today, then starts a conversation for the ANPC's composed dialogue.
2. One frame later (controls exist), the injector inserts our `ListItem`s (`type = Item`,
   `questionType = NoQuestion`, `key = "anpc_topic:<n>"`) into `TalkManager.ListTopicTellMeAbout` right after the
   `WhereAmI` item (index 0 if there is none), and refreshes the window list. Greeting: if one qualifies, the first
   conversation line is replaced (the window's `listboxConversation` item 0 text).
3. **Answering:** after the window has handled a "use" (list double-click / Enter: `listboxTopic.OnUseSelectedItem`;
   OK button: `buttonOkay.OnMouseClick`) on one of our items, it has added two blank lines; we remove them and add our
   question/answer through the window's `SetQuestionAnswerPairInConversationListbox`. A per-frame check catches any
   other path (two new blank lines while one of our items is selected).
4. **Player line:** while one of our items is selected, the window's "player says" label shows our question for the
   current tone (checked every frame, so tone buttons update it).
5. **Refresh:** after an answer (flags/asked changed) the visible topic set is recomputed **next frame** (changing the
   list inside the click handler re-triggers the click — DFU comment in `SelectTopicFromTopicList`). Also every frame:
   if DFU replaced the list (`AssembleTopicLists`), our items are re-inserted.
6. **Close:** when the window leaves the UI stack (`UIManager.OnWindowChange`), our items are removed.
7. Reflection names (`listboxTopic`, `listboxConversation`, `listCurrentTopics`, `textlabelPlayerSays`, `buttonOkay`,
   `selectedTalkTone`, `SetQuestionAnswerPairInConversationListbox`, TalkManager `reactionToPlayer`). If one is
   missing (a talk-window replacement mod), log once and show no extra topics (no crash); a missing
   `reactionToPlayer` means `neutral`.

## 8. Code structure

Core (no Unity, NUnit-tested):

| Unit | Responsibility |
|---|---|
| `Dialogue.cs` | Model: `DialogueFile`, `DialogueTopic`, `DialogueAnswer`, `QuestionText`. |
| `Condition.cs` | `Condition` model and `Holds(IDialogueFacts)`. |
| `IDialogueFacts` | What conditions read: hour, day/night, season, weather, region, town, player level/race/gender/gold, items, guilds, reaction, tone, flags, asked, quest globals. |
| `DialogueParser.cs` | JSON → `DialogueFile` + messages; ids, conditions, placeholders validated. |
| `DialogueLibrary.cs` | Types by id; `Compose(typeIds, folderFile)` → topics + greetings + messages. |
| `Conversation.cs` | `VisibleTopics(facts)`, `Answer(topic, facts, rng, state)` (picks answer, applies sets/clears/asked), `Question(topic, tone)`, `Greeting(facts, rng)`. |
| `TextMacros.cs` | `{player}` etc. expansion and unknown-placeholder detection. |
| `FlagSet` | Normalised flag names; snapshot/restore. |

Runtime: `DialogueFiles` (reads `_Dialogue`, reload), `GameFacts : IDialogueFacts` (reads DFU),
`TopicInjector` (§7), `NpcTalk` (starts it), save data (`Flags`, `asked`), console commands, mod messages, self-test.

## 9. Console

- `anpc_topics [id]` — the nearest calm ANPC within 5 m (or `id`): its dialogue types, each topic with `shown` or the
  first failing condition key (`hidden: time`), and asked topics.
- `anpc_reload_dialogue` — re-reads `_Dialogue/*.json` and every folder's `dialogue.json`; prints the problems.
- `anpc_flag` (list) / `anpc_flag <name> on|off`.

## 10. Self-test (in game, unattended)

Using an in-memory test type: topics inserted right after "Where am I?"; a conditional topic hidden/shown by the
hour; choosing a topic adds the tone question and the authored answer; a follow-up appears after its parent is asked
(same conversation); a `once` topic disappears; a flag set by one NPC's topic shows a topic on another NPC;
specific answer beats plain; blunt tone changes the player line; greeting replaced; DFU rebuilding the list keeps our
items; closing removes them; a vanilla topic still answers. Plus a LOOK screenshot of the wench's talk window
(portrait and topics) for the user.

## 11. Non-goals (C1)

Player reply choices (C2). Actions other than flags (gold, items, disposition, starting quests). Topics in the
"Where is" lists. Localisation of the default question lines. Cross-file `asked` validation.

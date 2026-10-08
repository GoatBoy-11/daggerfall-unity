# Advanced NPCs — Phase C2: reply choices and actions (design)

Date: 2026-10-08. Follows the C1 dialogue spec (`2026-10-08-advanced-npcs-topics-design.md`). Written while the user
was away with full autonomy ("return to the speech topics, do everything needed there"); the user chose earlier that
"branches" include **player reply choices** (C2) and that topics may have conditions; actions were left open and are
decided here. Priority stays: custom dialogue must be intuitive and easy to write.

## 1. Summary

After an ANPC answers, the player may get a short list of **replies** to choose from (a DFU list picker over the
talk window). A reply adds the player's line and the ANPC's answer to the conversation and may lead to further
replies, so authors can write small dialogue trees. Topics, answers and replies can carry **actions**: give or take
gold, give or take an item, change how the region's people regard the player, start a DFU quest, turn the ANPC into
an enemy, or end the conversation. Replies use the same `when` conditions as C1; replies whose action cannot be done
(not enough gold, item missing) are not offered. Two C1 leftovers are closed: duplicate JSON keys and same-id topic
overrides are reported.

## 2. Goals and success criteria

1. `"replies": [ { "text": "...", "answers": [...] } ]` on a topic or an answer: after that answer, a picker lists the
   replies whose `when` holds, plus **"(Say nothing.)"**. Choosing one shows the player's line (question colour) and
   an answer picked by the C1 answer rule; that answer's `replies` (or the reply's own) open the next picker.
   "(Say nothing.)" or Escape closes the picker and returns to the topic list.
2. Actions work on topics, answers and replies, run when that line is said, and are reported in `anpc_topics`.
3. A reply with `takeGold` / `takeItem` is only offered if the player has it; a topic with them is only listed if so.
4. Replies and actions are saved effects only through existing state (flags, asked, gold, items, reputation, quests,
   enemy switch); nothing new in the save format.
5. Every problem is one `[AdvancedNPCs]` line naming file, topic and field (unknown action, bad amount, unknown item,
   reply without text or answers, nesting deeper than 8). Duplicate JSON keys and same-id overrides are reported.
6. Example: the wench's `tavern_wench` type uses replies and actions (buy a mug of ale, pay for information,
   promise to look for Jory).

## 3. Format

```json
{
  "caption": "The house ale",
  "answers": ["Two coppers a mug. Want one?"],
  "replies": [
    { "text": "A mug, please. (2 gold)", "takeGold": 2, "reputation": 1,
      "answers": ["Here you go, love. Mind the foam."] },
    { "text": "Not today.", "answers": ["Suit yourself."] }
  ]
}
```

| Field (reply) | Required | Meaning |
|---|---|---|
| `text` | yes | The player's line, shown in the picker and then in the conversation. Placeholders allowed. |
| `answers` | yes | The ANPC's answer (C1 answer syntax: text or `{ "when", "text", ... }`; answers may have `replies`). |
| `when` | no | Offer the reply only while this holds (C1 conditions, `tone` allowed). |
| `replies` | no | Replies after this reply's answer when the chosen answer has none. |
| `id` | no | Name for `asked` (default: the text normalised). Asking a reply marks `<topic id>/<reply id>` asked. |
| actions | no | See §4. |

Replies may also sit on an answer object (`{ "text": "...", "replies": [...] }`); the answer's replies win over the
topic's. Nesting is limited to 8 levels (a warning; deeper replies are dropped).

## 4. Actions (on topics, answers and replies)

| Field | Value | Effect |
|---|---|---|
| `sets` / `clears` | flag names | C1, unchanged |
| `giveGold` | 1–100000 | the player gets that much gold |
| `takeGold` | 1–100000 | the player pays it; a reply/topic with it is only offered when the player has it |
| `giveItem` | item name | the player gets one item of that template (DFU item names: "Ruby", "Dagger", "Holy water", …) |
| `takeItem` | item name | one carried item with that name is removed; only offered when the player has one |
| `reputation` | −20…20 | changes the player's standing with the region's people (DFU's "People of …" faction): this is what `reaction` reads |
| `startQuest` | quest name | starts a DFU quest by its file name (e.g. "A0C00Y00"); a missing quest is logged |
| `becomeEnemy` | `true` | after the line the ANPC turns into an enemy (like `anpc_hostile <id> on`, saved); the talk window closes |
| `endConversation` | `true` | the talk window closes after the line |

Order when a line is said: takes (gold, item) → gives → reputation → flags → startQuest → becomeEnemy /
endConversation. Unknown item or quest names are load warnings when they can be checked (items at load; quests when
started).

## 5. Runtime

- `TopicInjector` already answers topics. After it adds a topic's question/answer, it checks the replies of the
  chosen answer (else the topic) through Core (`Conversation.Replies`), and opens a `DaggerfallListPickerWindow`
  one frame later (UI event safety). `OnItemPicked`: close the picker, add the reply line and its answer with the
  window's own `SetQuestionAnswerPairInConversationListbox`, run actions, then the next picker if any. Cancel closes.
- Actions go through an `IDialogueActions` interface (Core decides, runtime does), implemented by `GameFacts`.
- `becomeEnemy` / `endConversation`: after the picker closes, pop to the HUD; `becomeEnemy` then calls
  `NpcBrain.SwitchHostile(true)`.

## 6. Leftovers from C1

- **Duplicate JSON keys**: `Json.Parse` records duplicate keys (path + key); dialogue files and npc.json report
  "field X appears twice; the last one is used".
- **Same-id overrides**: `DialogueLibrary.Check` notes "topic X in B replaces the one in A for <npc>" (intentional
  overrides are legal; the note tells authors it happened).
- **Quest greetings**: not an issue — DFU only builds quest greetings for static NPCs; ANPCs talk through a mobile
  proxy (ruled, no change).

## 7. Testing

Core NUnit: reply parsing, nesting limit, action parsing and validation, implied conditions (takeGold/takeItem),
`Conversation.Replies` choice, action order, duplicate keys, override notes. In-game self-test: picker opens after
an answer with replies; picking adds both lines; say-nothing closes; takeGold removes gold and hides when poor;
giveGold; reputation changes `reaction` value; nested reply; endConversation closes the window; becomeEnemy makes
an enemy. LOOK: wench conversation with the reply picker open.

## 8. Non-goals

Free-text input; skill checks (persuasion rolls); voice; topics in "Where is"; giving items with materials or
enchantments; per-ANPC disposition separate from the region's reputation.

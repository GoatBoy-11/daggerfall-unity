# Advanced NPCs — Phase C topics spike: findings

Date: 2026-10-07. Spike for v2 spec §9.3 ("extra topics for unique ANPCs"), done while the user was away.
Code: throwaway branch `spike/topics`, commit `46cd9468d` (`Scripts/Runtime/TopicSpike.cs` + 5 `SPIKE` checks in
the self-test). Nothing of it is on `feature/advanced-npcs-v2`.

## Result

**The preferred mechanism, list injection, works.** All exit criteria pass in game (unattended self-test, 68/68):

| criterion | result |
|---|---|
| the topic is listed under "Tell Me About" | yes (after the vanilla entries: news, where am I, guilds…) |
| choosing it shows the authored answer | yes: "Tell me about Spike rumour." (question colour) and the answer, wrapped like vanilla lines |
| vanilla topics still answer | yes ("Any news?" gave a real rumour right after) |
| the topic is gone when the window closes | yes (removed from TalkManager's list) |
| absent for the next NPC talked to | yes |

The fallback (a `DaggerfallTalkWindow` subclass registered with `UIWindowFactory`) is not needed.

## How it works

1. After the talk window is open **and has built its controls**, append `TalkManager.ListItem`s to
   `TalkManager.Instance.ListTopicTellMeAbout` (`type = Item`, `questionType = NoQuestion`, `key = "anpc_topic:<npc>:<n>"`)
   and call `DaggerfallTalkWindow.UpdateListboxTopic()`.
2. Subscribe once to the window's topic list box `OnUseSelectedItem` (protected field `listboxTopic`, by reflection).
   The window subscribed in its own Setup, so its handler runs first: for a `NoQuestion` item
   `TalkManager.GetAnswerText` returns `""` with no side effects, and the window adds two blank lines.
3. Our handler finds the chosen item (`listCurrentTopics[listboxTopic.SelectedIndex]`), removes the two blank lines
   from `listboxConversation` and calls the window's own `SetQuestionAnswerPairInConversationListbox(question, answer)`
   (protected, by reflection), so layout, colours, modern-style option and scrolling are vanilla.
4. On `DaggerfallUI.UIManager.OnWindowChange`, when the window is no longer in the UI stack, remove our items.
   (`OnClose` alone is not enough: `PopToHUD` does not raise it.)

## Findings that the Phase C plan must handle

- **Timing:** the talk window builds its controls in its first `Update` after being pushed. Right after
  `TalkToMobileNPC` the list box fields are still null (the first spike run failed with a NullReferenceException).
  Inject one frame later (a coroutine), and give up quietly if the fields stay null.
- **DFU rebuilds the list:** `TalkManager.AssembleTopicLists` replaces `ListTopicTellMeAbout` with a new list when a
  conversation starts (if flagged) and **in the middle of a conversation** when a quest adds a topic
  (`AssembleTopicLists(true)` then calls `UpdateListboxTopic`). Our items would vanish. While the window is open,
  check each frame that our items are still in the current list (compare the list reference) and re-add them.
- **Reflection names** used: fields `listboxTopic`, `listboxConversation`, `listCurrentTopics`; method
  `SetQuestionAnswerPairInConversationListbox`. Talk-window mods that subclass `DaggerfallTalkWindow` keep them;
  a mod with a completely different window would not: then log once and show no extra topics (no crash).
- **Save data:** our items have `questID = 0` and no quest resource, so nothing reaches DFU's conversation save data;
  they live only while the window is open.
- **Logbook:** "Copy to logbook" copies the visible lines, so authored answers copy like vanilla ones.
- **Question text:** the spike uses a fixed English "Tell me about <caption>." DFU's own Tell-Me-About questions
  come from text records per tone; a later version could pick a polite/normal/blunt phrasing.
- **Position:** extra topics appear after all vanilla entries (guilds, temples…), so in big towns the player has to
  scroll. Inserting them right after "Where am I?" is possible (insert at index 2) — worth deciding in the plan.

## Open decisions for the user (before the Phase C plan)

1. Where the topics go in the list: at the end (as in the spike) or near the top.
2. Question wording: one fixed line, or per tone.
3. Whether generic ANPCs may have topics too (the v2 spec says unique only).
4. Topic conditions/actions (quests, items, disposition) — the v2 spec left them for later; the `dialogue.json`
   format leaves room.

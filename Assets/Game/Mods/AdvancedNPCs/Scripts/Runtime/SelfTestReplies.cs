using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>Self-test steps for reply choices and actions (C2 spec §7).</summary>
    public partial class SelfTest
    {
        IEnumerator ReplySteps(DaggerfallLocation location, Transform playerTransform)
        {
            GameManager gm = GameManager.Instance;
            DialogueParseResult parsed = DialogueParser.Parse("(selftest)", "{ \"topics\": [" +
                "{ \"caption\": \"Selftest deal\", \"answers\": [\"Want one?\"], \"replies\": [ " +
                "  { \"text\": \"Buy (2 gold)\", \"takeGold\": 2, \"reputation\": 1, \"answers\": [\"Sold.\"], \"replies\": [ { \"text\": \"Another?\", \"answers\": [\"No more.\"] } ] }, " +
                "  { \"text\": \"No\", \"answers\": [\"Fine.\"] } ] }," +
                "{ \"caption\": \"Selftest gift\", \"answers\": [ { \"text\": \"Take this.\", \"giveGold\": 5 } ] }," +
                "{ \"caption\": \"Selftest leave\", \"answers\": [\"Going?\"], \"replies\": [ { \"text\": \"Leave\", \"endConversation\": true, \"answers\": [\"Farewell.\"] } ] }," +
                "{ \"caption\": \"Selftest insult\", \"answers\": [\"What?\"], \"replies\": [ { \"text\": \"Insult\", \"becomeEnemy\": true, \"answers\": [\"Guards!\"] } ] } ] }");
            Check("reply test dialogue parses without problems", parsed.File != null && parsed.Messages.Count == 0, Join(parsed.Messages));
            if (parsed.File == null)
                yield break;
            parsed.File.Name = "selftest_replies";
            mod.AddDialogueType(parsed.File);

            NpcDefinition def = TestDefinition("selftest_replier", Bravery.Normal, location, playerTransform, -2f);
            def.Dialogue.Add("selftest_replies");
            NpcBrain npc = SpawnDefinition(def, location);
            yield return Settle;

            PlayerEntityGold gold = new PlayerEntityGold();
            gm.PlayerEntity.GoldPieces = 10;
            int region = gm.PlayerGPS.GetPeopleOfCurrentRegion();
            int repBefore = Reputation(region);
            TopicInjector topics = mod.Topics;

            // A deal: the picker opens after the answer, offers what the player can pay, the reply's answer and its own replies follow.
            yield return OpenTalk(npc);
            yield return UseTopic("Selftest deal");
            Check("replies are offered after an answer", topics.PickerOpen && Join(topics.OfferedTexts()) == "[Buy (2 gold) | No]",
                "open " + topics.PickerOpen + ", offered " + Join(topics.OfferedTexts()));
            topics.PickReply(0);
            yield return null;
            yield return null;
            List<string> lines = topics.ConversationLines();
            bool paired = lines.Count >= 2 && lines[lines.Count - 2] == "Buy (2 gold)" && lines[lines.Count - 1] == "Sold.";
            Check("choosing a reply adds the player's line and the answer, and runs its actions",
                paired && gm.PlayerEntity.GoldPieces == 8 && Reputation(region) == repBefore + 1,
                "lines " + Join(lines) + ", gold " + gm.PlayerEntity.GoldPieces + ", reputation " + repBefore + " -> " + Reputation(region));
            Check("a reply's own replies follow", topics.PickerOpen && Join(topics.OfferedTexts()) == "[Another?]", "offered " + Join(topics.OfferedTexts()));
            topics.PickReply(topics.OfferedTexts().Count);
            yield return null;
            Check("\"(Say nothing.)\" closes the replies", !topics.PickerOpen && TalkWindowOpen(), "picker " + topics.PickerOpen);

            gm.PlayerEntity.GoldPieces = 1;
            yield return UseTopic("Selftest deal");
            Check("a reply the player cannot pay for is not offered", Join(topics.OfferedTexts()) == "[No]", "offered " + Join(topics.OfferedTexts()));
            topics.PickReply(topics.OfferedTexts().Count);
            yield return null;

            yield return UseTopic("Selftest gift");
            Check("an answer can give gold", gm.PlayerEntity.GoldPieces == 6, "gold " + gm.PlayerEntity.GoldPieces);

            yield return UseTopic("Selftest leave");
            topics.PickReply(0);
            yield return null;
            yield return null;
            yield return null;
            Check("endConversation closes the talk window and shows the last line", !TalkWindowOpen() && !topics.Active && TopMessage() == "Farewell.",
                "talk window open " + TalkWindowOpen() + ", message \"" + TopMessage() + "\"");
            DaggerfallUI.Instance.PopToHUD();
            yield return null;

            yield return OpenTalk(npc);
            yield return UseTopic("Selftest insult");
            topics.PickReply(0);
            yield return null;
            yield return null;
            yield return null;
            Check("becomeEnemy closes the window, then the ANPC is an enemy", !TalkWindowOpen() && npc.IsEnemy,
                "talk window open " + TalkWindowOpen() + ", enemy " + npc.IsEnemy);
            DaggerfallUI.Instance.PopToHUD();
            yield return null;

            // Items: given by name, taken back; quests: a missing name is reported.
            GameFacts facts = new GameFacts(null, mod.Flags, "", null);
            int rubies = CountNamed("Ruby");
            facts.GiveItem("Ruby");
            bool given = CountNamed("Ruby") == rubies + 1 && facts.HasItem("Ruby");
            facts.TakeItem("Ruby");
            Check("giveItem and takeItem by name", given && CountNamed("Ruby") == rubies, "rubies " + rubies + " -> " + CountNamed("Ruby"));
            GameFacts.LastProblem = null;
            facts.StartQuest("ANPC_NO_SUCH_QUEST");
            Check("startQuest with a missing quest is reported", GameFacts.LastProblem != null && GameFacts.LastProblem.Contains("no such quest"),
                "problem: " + GameFacts.LastProblem);

            npc.SwitchHostile(false);
            gm.PlayerEntity.FactionData.ChangeReputation(region, repBefore - Reputation(region));
            gold.Restore();
            NpcBrain.Discard(npc);
            yield return Settle;
        }

        /// <summary>Remembers the player's gold at creation and puts it back.</summary>
        class PlayerEntityGold
        {
            readonly int before = GameManager.Instance.PlayerEntity.GoldPieces;

            public void Restore()
            {
                GameManager.Instance.PlayerEntity.GoldPieces = before;
            }
        }

        string TopMessage()
        {
            return DaggerfallUI.UIManager.TopWindow is DaggerfallMessageBox ? mod.Topics.FinalMessage : null;
        }

        static int CountNamed(string name)
        {
            int n = 0;
            DaggerfallWorkshop.Game.Items.ItemCollection items = GameManager.Instance.PlayerEntity.Items;
            for (int i = 0; i < items.Count; i++)
            {
                DaggerfallWorkshop.Game.Items.DaggerfallUnityItem item = items.GetItem(i);
                if (item != null && string.Equals(DaggerfallUnity.Instance.ItemHelper.ResolveItemName(item), name, System.StringComparison.OrdinalIgnoreCase))
                    n += item.stackCount;
            }
            return n;
        }

        static int Reputation(int factionId)
        {
            FactionFile.FactionData data;
            return GameManager.Instance.PlayerEntity.FactionData.GetFactionData(factionId, out data) ? data.rep : 0;
        }

        IEnumerator OpenTalk(NpcBrain npc)
        {
            npc.GetComponent<NpcTalk>().TryTalk();
            for (int wait = 0; wait < 60 && !mod.Topics.Started; wait++)
                yield return null;
            yield return null;
            CallWindow(DaggerfallUI.Instance.TalkWindow, "SetTalkModeTellMeAbout");
            yield return null;
        }

        IEnumerator UseTopic(string caption)
        {
            mod.Topics.Select(caption);
            TopicListBox(DaggerfallUI.Instance.TalkWindow).UseSelectedItem();
            yield return null;
            yield return null;
            yield return null;
        }
    }
}

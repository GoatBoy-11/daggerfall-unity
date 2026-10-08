using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Guilds;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.Questing;
using DaggerfallWorkshop.Utility;
using DaggerfallConnect.Arena2;
using DaggerfallConnect.FallExe;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>
    /// The live game as dialogue conditions see it (spec C1 §5), for one conversation with one ANPC, and what asking
    /// a topic changes (player-wide flags, the person's asked topics).
    /// </summary>
    public class GameFacts : IDialogueFacts, IDialogueState, IDialogueActions
    {
        /// <summary>Set by the talk window code: turn the ANPC into an enemy / close the conversation.</summary>
        public Action OnBecomeEnemy;
        public Action OnEndConversation;

        static readonly FieldInfo ReactionField =
            typeof(TalkManager).GetField("reactionToPlayer", BindingFlags.Instance | BindingFlags.NonPublic);
        static bool warnedReaction;

        readonly NpcState state;
        readonly FlagSet flags;
        readonly string npcName;
        readonly Func<int> tone;

        /// <param name="tone">The tone buttons' current choice (Tones), or null for unknown.</param>
        public GameFacts(NpcState state, FlagSet flags, string npcName, Func<int> tone)
        {
            this.state = state;
            this.flags = flags;
            this.npcName = npcName;
            this.tone = tone;
        }

        static PlayerEntity Player
        {
            get { return GameManager.Instance.PlayerEntity; }
        }

        public int Hour
        {
            get { return DaggerfallUnity.Instance.WorldTime.Now.Hour; }
        }

        public bool IsNight
        {
            get { return DaggerfallUnity.Instance.WorldTime.Now.IsNight; }
        }

        public string Season
        {
            get
            {
                switch (DaggerfallUnity.Instance.WorldTime.Now.SeasonValue)
                {
                    case DaggerfallDateTime.Seasons.Fall: return "autumn";
                    case DaggerfallDateTime.Seasons.Spring: return "spring";
                    case DaggerfallDateTime.Seasons.Winter: return "winter";
                    default: return "summer";
                }
            }
        }

        public bool Raining { get { return GameManager.Instance.WeatherManager.IsRaining; } }
        public bool Storming { get { return GameManager.Instance.WeatherManager.IsStorming; } }
        public bool Snowing { get { return GameManager.Instance.WeatherManager.IsSnowing; } }
        public bool Overcast { get { return GameManager.Instance.WeatherManager.IsOvercast; } }

        public string Region
        {
            get
            {
                DaggerfallLocation location = GameManager.Instance.StreamingWorld.CurrentPlayerLocationObject;
                return location != null ? location.Summary.RegionName : GameManager.Instance.PlayerGPS.CurrentRegionName;
            }
        }

        public string Town
        {
            get
            {
                DaggerfallLocation location = GameManager.Instance.StreamingWorld.CurrentPlayerLocationObject;
                return location != null ? location.Summary.LocationName : "";
            }
        }

        public int PlayerLevel { get { return Player.Level; } }

        public string PlayerRace
        {
            get { return ((Races)Player.BirthRaceTemplate.ID).ToString().ToLowerInvariant(); }
        }

        public string PlayerGender
        {
            get { return Player.Gender == Genders.Female ? "female" : "male"; }
        }

        public int Gold { get { return Player.GoldPieces; } }

        /// <summary>How this NPC regards the player, from the reaction DFU computed when the conversation started.</summary>
        public string Reaction
        {
            get
            {
                if (ReactionField == null || TalkManager.Instance == null)
                {
                    if (!warnedReaction)
                    {
                        warnedReaction = true;
                        AdvancedNpcsMod.Log("TalkManager has no reactionToPlayer field; \"reaction\" conditions see neutral.");
                    }
                    return "neutral";
                }
                return ConditionParser.ReactionBand((int)ReactionField.GetValue(TalkManager.Instance));
            }
        }

        public int Tone
        {
            get { return tone != null ? tone() : Tones.Unknown; }
        }

        public bool HasItem(string name)
        {
            ItemCollection items = Player.Items;
            ItemHelper helper = DaggerfallUnity.Instance.ItemHelper;
            for (int i = 0; i < items.Count; i++)
            {
                DaggerfallUnityItem item = items.GetItem(i);
                if (item == null || item.IsQuestItem)
                    continue;
                if (SameName(helper.ResolveItemName(item), name) || SameName(helper.ResolveItemLongName(item), name))
                    return true;
            }
            return false;
        }

        static bool SameName(string a, string b)
        {
            return a != null && b != null && string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        public int GuildRank(string guildKey)
        {
            FactionFile.GuildGroups group;
            switch (guildKey)
            {
                case "fightersguild": group = FactionFile.GuildGroups.FightersGuild; break;
                case "magesguild": group = FactionFile.GuildGroups.MagesGuild; break;
                case "thievesguild": group = FactionFile.GuildGroups.GeneralPopulace; break;
                case "darkbrotherhood": group = FactionFile.GuildGroups.DarkBrotherHood; break;
                case "temple": group = FactionFile.GuildGroups.HolyOrder; break;
                case "knightlyorder": group = FactionFile.GuildGroups.KnightlyOrder; break;
                default: return -1;
            }
            IGuild guild;
            if (GameManager.Instance.GuildManager.GetJoinedGuildOfGuildGroup(group, out guild) && guild != null)
                return guild.Rank;
            return -1;
        }

        public bool HasFlag(string flag)
        {
            return flags.Has(flag);
        }

        public bool Asked(string topicId)
        {
            return state != null && state.HasAsked(topicId);
        }

        public bool QuestGlobal(string nameOrNumber)
        {
            int id = GlobalVarId(nameOrNumber);
            return id >= 0 && Player.GlobalVars.GetGlobalVar(id);
        }

        /// <summary>A quest global's number from its name in DFU's Quests-GlobalVars table (or a number), -1 if unknown.</summary>
        public static int GlobalVarId(string nameOrNumber)
        {
            int id;
            if (int.TryParse(nameOrNumber, NumberStyles.Integer, CultureInfo.InvariantCulture, out id))
                return id >= 0 && id < 64 ? id : -1;
            if (QuestMachine.Instance == null || QuestMachine.Instance.GlobalVarsTable == null)
                return -1;
            Table table = QuestMachine.Instance.GlobalVarsTable;
            return table.HasValue(nameOrNumber) ? table.GetInt("id", nameOrNumber) : -1;
        }

        public void SetFlag(string flag)
        {
            flags.Set(flag);
        }

        public void ClearFlag(string flag)
        {
            flags.Clear(flag);
        }

        public void MarkAsked(string topicId)
        {
            if (state != null)
                state.MarkAsked(topicId);
        }

        // --- Actions (C2 spec §4)

        public void GiveGold(int amount)
        {
            Player.GoldPieces += amount;
        }

        public void TakeGold(int amount)
        {
            Player.GoldPieces = Math.Max(0, Player.GoldPieces - amount);
        }

        public void GiveItem(string name)
        {
            ItemGroups group;
            int templateIndex;
            if (!FindItemTemplate(name, out group, out templateIndex))
            {
                AdvancedNpcsMod.Log("giveItem \"" + name + "\": no DFU item with this name; nothing given.");
                return;
            }
            Player.Items.AddItem(ItemBuilder.CreateItem(group, templateIndex));
        }

        /// <summary>One matching item: never a quest item; an unequipped one first, else one unequipped like DFU does it.</summary>
        public void TakeItem(string name)
        {
            DaggerfallUnityItem item = FindTakeable(name, false) ?? FindTakeable(name, true);
            if (item == null)
                return;
            if (item.IsEquipped && Player.ItemEquipTable.UnequipItem(item))
                Player.UpdateEquippedArmorValues(item, false);
            Player.Items.RemoveOne(item);
        }

        static DaggerfallUnityItem FindTakeable(string name, bool equipped)
        {
            ItemCollection items = Player.Items;
            ItemHelper helper = DaggerfallUnity.Instance.ItemHelper;
            for (int i = 0; i < items.Count; i++)
            {
                DaggerfallUnityItem item = items.GetItem(i);
                if (item == null || item.IsQuestItem || item.IsEquipped != equipped)
                    continue;
                if (SameName(helper.ResolveItemName(item), name) || SameName(helper.ResolveItemLongName(item), name))
                    return item;
            }
            return null;
        }

        /// <summary>The player's standing with the region's people (what DFU's reaction, and so "reaction", is based on).</summary>
        public void ChangeReputation(int amount)
        {
            try
            {
                Player.FactionData.ChangeReputation(GameManager.Instance.PlayerGPS.GetPeopleOfCurrentRegion(), amount);
            }
            catch (Exception e)
            {
                AdvancedNpcsMod.Log("reputation: could not change the region's standing (" + e.Message + ").");
            }
        }

        /// <summary>Last action problem (self-test).</summary>
        public static string LastProblem;

        /// <summary>Through DFU's quest lists, so quests shipped in mods and quest packs are found too.</summary>
        public void StartQuest(string name)
        {
            try
            {
                Quest quest = GameManager.Instance.QuestListsManager.GetQuest(name);
                if (quest == null)
                {
                    LastProblem = "startQuest \"" + name + "\": no such quest (check its file name and that its quest pack is installed)";
                    AdvancedNpcsMod.LogError(LastProblem);
                    return;
                }
                QuestMachine.Instance.StartQuest(quest);
            }
            catch (Exception e)
            {
                LastProblem = "startQuest \"" + name + "\": could not start (" + e.Message + ")";
                AdvancedNpcsMod.LogError(LastProblem);
            }
        }

        public void BecomeEnemy()
        {
            if (OnBecomeEnemy != null)
                OnBecomeEnemy();
        }

        public void EndConversation()
        {
            if (OnEndConversation != null)
                OnEndConversation();
        }

        /// <summary>A DFU item template by name (case ignored), e.g. "Ruby", "Dagger", "Holy water".</summary>
        public static bool FindItemTemplate(string name, out ItemGroups group, out int templateIndex)
        {
            group = ItemGroups.None;
            templateIndex = -1;
            ItemHelper helper = DaggerfallUnity.Instance.ItemHelper;
            foreach (ItemGroups g in Enum.GetValues(typeof(ItemGroups)))
            {
                Array values;
                try
                {
                    values = helper.GetEnumArray(g);
                }
                catch (Exception)
                {
                    continue;
                }
                if (values == null)
                    continue;
                for (int i = 0; i < values.Length; i++)
                {
                    ItemTemplate t = helper.GetItemTemplate(g, i);
                    if (SameName(t.name, name))
                    {
                        group = g;
                        templateIndex = Convert.ToInt32(values.GetValue(i));
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>Values for {player}, {npc}, {town} and {region}.</summary>
        public Dictionary<string, string> Macros()
        {
            Dictionary<string, string> values = new Dictionary<string, string>();
            values[TextMacros.Player] = Player.Name;
            values[TextMacros.Npc] = npcName;
            values[TextMacros.Town] = Town;
            values[TextMacros.Region] = Region;
            return values;
        }
    }
}

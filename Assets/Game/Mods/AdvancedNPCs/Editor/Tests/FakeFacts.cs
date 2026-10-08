using System;
using System.Collections.Generic;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    /// <summary>Settable IDialogueFacts and IDialogueState for Core dialogue tests.</summary>
    public class FakeFacts : IDialogueFacts, IDialogueState
    {
        public int HourValue = 12;
        public bool Night;
        public string SeasonValue = "summer";
        public bool Rain, Storm, Snow, Clouds;
        public string RegionValue = "Daggerfall";
        public string TownValue = "Daggerfall";
        public int Level = 1;
        public string Race = "breton";
        public string Gender = "male";
        public int GoldValue;
        public string ReactionValue = "neutral";
        public int ToneValue = Tones.Unknown;
        public readonly List<string> Items = new List<string>();
        public readonly Dictionary<string, int> Guilds = new Dictionary<string, int>();
        public readonly HashSet<string> Flags = new HashSet<string>();
        public readonly HashSet<string> AskedTopics = new HashSet<string>();
        public readonly HashSet<string> Globals = new HashSet<string>();

        public int Hour { get { return HourValue; } }
        public bool IsNight { get { return Night; } }
        public string Season { get { return SeasonValue; } }
        public bool Raining { get { return Rain; } }
        public bool Storming { get { return Storm; } }
        public bool Snowing { get { return Snow; } }
        public bool Overcast { get { return Clouds; } }
        public string Region { get { return RegionValue; } }
        public string Town { get { return TownValue; } }
        public int PlayerLevel { get { return Level; } }
        public string PlayerRace { get { return Race; } }
        public string PlayerGender { get { return Gender; } }
        public int Gold { get { return GoldValue; } }
        public string Reaction { get { return ReactionValue; } }
        public int Tone { get { return ToneValue; } }

        public bool HasItem(string name)
        {
            return Items.Exists(delegate (string i) { return string.Equals(i, name, StringComparison.OrdinalIgnoreCase); });
        }

        public int GuildRank(string guildKey)
        {
            int rank;
            return Guilds.TryGetValue(guildKey, out rank) ? rank : -1;
        }

        public bool HasFlag(string flag) { return Flags.Contains(flag); }
        public bool Asked(string topicId) { return AskedTopics.Contains(topicId); }
        public bool QuestGlobal(string nameOrNumber) { return Globals.Contains(nameOrNumber); }

        public void SetFlag(string flag) { Flags.Add(flag); }
        public void ClearFlag(string flag) { Flags.Remove(flag); }
        public void MarkAsked(string topicId) { AskedTopics.Add(topicId); }
    }
}

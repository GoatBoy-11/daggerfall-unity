using System.Collections.Generic;
using System.Text;

namespace AdvancedNPCs.Core
{
    /// <summary>Topic ids, flag names and dialogue type ids (spec C1 §4.1).</summary>
    public static class DialogueIds
    {
        /// <summary>Lowercase; runs of characters other than a-z and 0-9 become one "_"; no "_" at either end.</summary>
        public static string Normalize(string text)
        {
            if (text == null)
                return "";
            StringBuilder sb = new StringBuilder();
            bool gap = false;
            foreach (char c in text.ToLowerInvariant())
            {
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                {
                    if (gap && sb.Length > 0)
                        sb.Append('_');
                    sb.Append(c);
                    gap = false;
                }
                else
                {
                    gap = true;
                }
            }
            return sb.ToString();
        }
    }

    /// <summary>The player's tone, as DaggerfallTalkWindow.TalkToneToIndex numbers it. Kept as int (DFU's compiler).</summary>
    public static class Tones
    {
        public const int Unknown = -1;
        public const int Polite = 0;
        public const int Normal = 1;
        public const int Blunt = 2;

        public static readonly string[] Names = { "polite", "normal", "blunt" };

        /// <summary>-1 if the text is not a tone name.</summary>
        public static int Parse(string text)
        {
            if (text == null)
                return Unknown;
            string t = text.Trim().ToLowerInvariant();
            for (int i = 0; i < Names.Length; i++)
            {
                if (Names[i] == t)
                    return i;
            }
            return Unknown;
        }
    }

    /// <summary>The player's line for a topic, per tone (spec C1 §4.1 "question").</summary>
    public class QuestionText
    {
        public static readonly string[] Defaults =
        {
            "Could you tell me about {topic}?",
            "Tell me about {topic}.",
            "What do you know about {topic}?",
        };

        /// <summary>One text for every tone.</summary>
        public string All;
        public string Polite;
        public string Normal;
        public string Blunt;

        /// <summary>The tone's own text, else the text for all tones, else the normal one, else the default line.</summary>
        public string For(int tone)
        {
            string own = tone == Tones.Polite ? Polite : tone == Tones.Blunt ? Blunt : Normal;
            if (!string.IsNullOrEmpty(own))
                return own;
            if (!string.IsNullOrEmpty(All))
                return All;
            if (!string.IsNullOrEmpty(Normal))
                return Normal;
            return Default(tone);
        }

        public static string Default(int tone)
        {
            return tone >= 0 && tone < Defaults.Length ? Defaults[tone] : Defaults[Tones.Normal];
        }
    }

    /// <summary>What saying a line does besides flags (C2 spec §4). Zero / null / false = nothing.</summary>
    public class DialogueActions
    {
        public int GiveGold;
        public int TakeGold;
        public string GiveItem;
        public string TakeItem;
        /// <summary>Change of the player's standing with the region's people, -20..20.</summary>
        public int Reputation;
        public string StartQuest;
        public bool BecomeEnemy;
        public bool EndConversation;

        public bool IsEmpty
        {
            get
            {
                return GiveGold == 0 && TakeGold == 0 && GiveItem == null && TakeItem == null && Reputation == 0 && StartQuest == null &&
                       !BecomeEnemy && !EndConversation;
            }
        }
    }

    public class DialogueAnswer
    {
        public string Text;
        /// <summary>Null: a plain answer.</summary>
        public Condition When;
        public readonly List<string> Sets = new List<string>();
        public readonly List<string> Clears = new List<string>();
        public readonly DialogueActions Actions = new DialogueActions();
        /// <summary>The player's choices after this answer (win over the topic's or reply's own).</summary>
        public readonly List<DialogueReply> Replies = new List<DialogueReply>();
    }

    /// <summary>A line the player can choose after an answer (C2 spec §3).</summary>
    public class DialogueReply
    {
        public string Id;
        public string Text;
        public Condition When;
        public readonly List<DialogueAnswer> Answers = new List<DialogueAnswer>();
        /// <summary>Choices after this reply's answer when that answer has none of its own.</summary>
        public readonly List<DialogueReply> Replies = new List<DialogueReply>();
        public readonly List<string> Sets = new List<string>();
        public readonly List<string> Clears = new List<string>();
        public readonly DialogueActions Actions = new DialogueActions();
    }

    public class DialogueTopic
    {
        public string Id;
        public string Caption;
        /// <summary>Where the topic was read, for messages ("_Dialogue/tavern_wench.json").</summary>
        public string Source;
        /// <summary>Null: always shown.</summary>
        public Condition When;
        public QuestionText Question = new QuestionText();
        public readonly List<DialogueAnswer> Answers = new List<DialogueAnswer>();
        public readonly List<string> Sets = new List<string>();
        public readonly List<string> Clears = new List<string>();
        public bool Once;
        public readonly DialogueActions Actions = new DialogueActions();
        /// <summary>The player's choices after this topic's answer, unless that answer has its own.</summary>
        public readonly List<DialogueReply> Replies = new List<DialogueReply>();
    }

    /// <summary>What asking a topic changes: player-wide flags and the person's asked topics.</summary>
    public interface IDialogueState
    {
        void SetFlag(string flag);
        void ClearFlag(string flag);
        void MarkAsked(string topicId);
    }

    /// <summary>One dialogue file: a type in _Dialogue or a folder's dialogue.json.</summary>
    public class DialogueFile
    {
        /// <summary>Type id for _Dialogue files; the folder name for a folder's own file.</summary>
        public string Name;
        public readonly List<DialogueTopic> Topics = new List<DialogueTopic>();
        public readonly List<DialogueAnswer> Greetings = new List<DialogueAnswer>();
    }
}

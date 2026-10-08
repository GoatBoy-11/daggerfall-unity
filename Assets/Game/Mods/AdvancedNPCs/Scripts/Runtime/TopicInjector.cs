using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>
    /// Shows an ANPC's dialogue topics in DFU's talk window (spec C1 §7, mechanism from the topics spike): our
    /// ListItems go into TalkManager's "Tell Me About" list right after "Where am I?"; when one is used, the two
    /// blank lines the window adds for it are replaced by our question and answer, laid out by the window itself.
    /// Nothing is left in TalkManager once the window closes.
    /// </summary>
    public class TopicInjector : MonoBehaviour
    {
        public const string KeyPrefix = "anpc_topic:";
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        const int MaxWaitFrames = 30;

        static readonly FieldInfo TopicListField = typeof(DaggerfallTalkWindow).GetField("listboxTopic", Hidden);
        static readonly FieldInfo ConversationField = typeof(DaggerfallTalkWindow).GetField("listboxConversation", Hidden);
        static readonly FieldInfo CurrentTopicsField = typeof(DaggerfallTalkWindow).GetField("listCurrentTopics", Hidden);
        static readonly FieldInfo PlayerSaysField = typeof(DaggerfallTalkWindow).GetField("textlabelPlayerSays", Hidden);
        static readonly FieldInfo OkButtonField = typeof(DaggerfallTalkWindow).GetField("buttonOkay", Hidden);
        static readonly FieldInfo ToneField = typeof(DaggerfallTalkWindow).GetField("selectedTalkTone", Hidden);
        static readonly FieldInfo OptionField = typeof(DaggerfallTalkWindow).GetField("selectedTalkOption", Hidden);
        static readonly MethodInfo AddPairMethod = typeof(DaggerfallTalkWindow).GetMethod("SetQuestionAnswerPairInConversationListbox", Hidden);
        static bool warnedMissing;

        readonly System.Random rng = new System.Random();
        readonly Dictionary<string, DialogueTopic> byKey = new Dictionary<string, DialogueTopic>(StringComparer.Ordinal);
        readonly List<TalkManager.ListItem> items = new List<TalkManager.ListItem>();

        DaggerfallTalkWindow window;
        DaggerfallTalkWindow hookedWindow;
        ComposedDialogue dialogue;
        GameFacts facts;
        Dictionary<string, string> macros;
        List<TalkManager.ListItem> injectedInto;
        int waitFrames;
        bool started;
        bool refreshNext;
        bool warnedCap;
        int conversationCount;

        /// <summary>A conversation with topics is going on.</summary>
        public bool Active
        {
            get { return window != null; }
        }

        /// <summary>The topics are in the window's list (the first frames after opening they are not yet).</summary>
        public bool Started
        {
            get { return window != null && started; }
        }

        // For the self-test and anpc_topics.
        public int AnsweredCount { get; private set; }
        public int Injections { get; private set; }
        public string LastQuestion { get; private set; }
        public string LastAnswer { get; private set; }
        public string GreetingShown { get; private set; }

        public GameFacts Facts
        {
            get { return facts; }
        }

        public void Begin(DaggerfallTalkWindow talkWindow, NpcState state, FlagSet flags, string npcName, ComposedDialogue composed)
        {
            End();
            if (talkWindow == null || composed == null || composed.IsEmpty)
                return;
            if (!ReflectionWorks())
            {
                if (!warnedMissing)
                {
                    warnedMissing = true;
                    AdvancedNpcsMod.Log("The talk window is not DFU's own (replaced by another mod?); ANPC dialogue topics are not shown.");
                }
                return;
            }
            window = talkWindow;
            dialogue = composed;
            facts = new GameFacts(state, flags, npcName, CurrentTone);
            macros = facts.Macros();
            started = false;
            waitFrames = 0;
            refreshNext = false;
            warnedCap = false;
            GreetingShown = null;
            DaggerfallUI.UIManager.OnWindowChange += OnWindowChange;
        }

        /// <summary>Takes our topics out of TalkManager and forgets the conversation.</summary>
        public void End()
        {
            if (TalkManager.Instance != null && TalkManager.Instance.ListTopicTellMeAbout != null)
                RemoveOurs(TalkManager.Instance.ListTopicTellMeAbout);
            if (injectedInto != null)
                RemoveOurs(injectedInto);
            injectedInto = null;
            items.Clear();
            byKey.Clear();
            window = null;
            facts = null;
            DaggerfallUI.UIManager.OnWindowChange -= OnWindowChange;
        }

        /// <summary>Captions of our topics in TalkManager's list, in list order.</summary>
        public List<string> ShownCaptions()
        {
            List<string> captions = new List<string>();
            foreach (TalkManager.ListItem item in items)
                captions.Add(item.caption);
            return captions;
        }

        /// <summary>Index of our first topic in TalkManager's Tell Me About list, or -1.</summary>
        public int FirstIndexInList()
        {
            if (items.Count == 0 || TalkManager.Instance == null || TalkManager.Instance.ListTopicTellMeAbout == null)
                return -1;
            return TalkManager.Instance.ListTopicTellMeAbout.IndexOf(items[0]);
        }

        void Update()
        {
            if (window == null)
                return;
            if (!started)
            {
                // The window builds its controls in its first Update after being pushed (spike finding).
                if (TopicList == null || ConversationList == null)
                {
                    if (++waitFrames > MaxWaitFrames)
                    {
                        AdvancedNpcsMod.Log("The talk window did not build its controls; no dialogue topics this time.");
                        End();
                    }
                    return;
                }
                started = true;
                Hook();
                ApplyGreeting();
                Inject();
                conversationCount = ConversationList.Count;
                return;
            }
            if (!DaggerfallUI.UIManager.ContainsWindow(window))
            {
                End();
                return;
            }
            // Refresh after an answer one frame later: changing the list inside the click handler re-triggers the
            // click (DFU comment in SelectTopicFromTopicList). DFU also replaces the list when a quest adds a topic.
            if (refreshNext || !StillInList())
            {
                refreshNext = false;
                Inject();
            }
            if (ConversationList.Count != conversationCount)
                HandleUse();
            UpdatePlayerLine();
        }

        void OnWindowChange(object sender, EventArgs e)
        {
            if (window != null && !DaggerfallUI.UIManager.ContainsWindow(window))
                End();
        }

        void Hook()
        {
            if (hookedWindow == window)
                return;
            // The window subscribed in its Setup, so it handles the use first and these run after it.
            TopicList.OnUseSelectedItem += HandleUse;
            Button ok = OkButtonField.GetValue(window) as Button;
            if (ok != null)
                ok.OnMouseClick += OnOkay;
            hookedWindow = window;
        }

        void OnOkay(BaseScreenComponent sender, Vector2 position)
        {
            HandleUse();
        }

        /// <summary>
        /// If the window just answered one of our topics (it adds a blank question and a blank answer for it), puts
        /// our question and answer there instead. Safe to call more than once per use.
        /// </summary>
        void HandleUse()
        {
            if (window == null || !started)
                return;
            ListBox conversation = ConversationList;
            DialogueTopic topic = SelectedTopic();
            int count = conversation.Count;
            bool blankPair = count >= conversationCount + 2 && count >= 2 && Blank(conversation, count - 1) && Blank(conversation, count - 2);
            if (topic == null || !blankPair)
            {
                conversationCount = count;
                return;
            }
            conversation.RemoveItem(count - 1);
            conversation.RemoveItem(count - 2);

            string question = TextMacros.Expand(Conversation.Question(topic, CurrentTone()), macros);
            string answer = TextMacros.Expand(Conversation.Ask(topic, facts, rng, facts), macros);
            AddPairMethod.Invoke(window, new object[] { question, answer });
            conversationCount = conversation.Count;
            AnsweredCount++;
            LastQuestion = question;
            LastAnswer = answer;
            refreshNext = true;
        }

        static bool Blank(ListBox list, int index)
        {
            ListBox.ListItem item = list.GetItem(index);
            return item != null && item.textLabel != null && string.IsNullOrEmpty((item.textLabel.Text ?? "").Trim());
        }

        void Inject()
        {
            List<TalkManager.ListItem> list = TalkManager.Instance.ListTopicTellMeAbout;
            if (list == null)
                return;
            RemoveOurs(list);
            if (injectedInto != null && injectedInto != list)
                RemoveOurs(injectedInto);
            items.Clear();
            byKey.Clear();

            List<DialogueTopic> visible = Conversation.Visible(dialogue, facts, Conversation.MaxVisible);
            if (!warnedCap && visible.Count == Conversation.MaxVisible && Conversation.Visible(dialogue, facts, int.MaxValue).Count > Conversation.MaxVisible)
            {
                warnedCap = true;
                AdvancedNpcsMod.Log("More than " + Conversation.MaxVisible + " dialogue topics apply; only the first " + Conversation.MaxVisible + " are listed.");
            }
            for (int i = 0; i < visible.Count; i++)
            {
                TalkManager.ListItem item = new TalkManager.ListItem();
                item.type = TalkManager.ListItemType.Item;
                item.questionType = TalkManager.QuestionType.NoQuestion;
                item.caption = TextMacros.Expand(visible[i].Caption, macros);
                item.key = KeyPrefix + i;
                items.Add(item);
                byKey[item.key] = visible[i];
            }

            // Right after "Where am I?", before quest and guild topics (spec C1 §2.2).
            int at = list.FindIndex(delegate (TalkManager.ListItem item) { return item.questionType == TalkManager.QuestionType.WhereAmI; }) + 1;
            list.InsertRange(at, items);
            injectedInto = list;
            Injections++;
            window.UpdateListboxTopic();
        }

        bool StillInList()
        {
            List<TalkManager.ListItem> list = TalkManager.Instance.ListTopicTellMeAbout;
            if (list != injectedInto)
                return false;
            foreach (TalkManager.ListItem item in items)
            {
                if (!list.Contains(item))
                    return false;
            }
            return true;
        }

        static void RemoveOurs(List<TalkManager.ListItem> list)
        {
            list.RemoveAll(delegate (TalkManager.ListItem item)
            {
                return item.key != null && item.key.StartsWith(KeyPrefix, StringComparison.Ordinal);
            });
        }

        void ApplyGreeting()
        {
            string greeting = Conversation.Greeting(dialogue, facts, rng);
            ListBox conversation = ConversationList;
            if (greeting == null || conversation.Count != 1)
                return;
            greeting = TextMacros.Expand(greeting, macros);
            ListBox.ListItem old = conversation.GetItem(0);
            ListBox.ListItem added;
            conversation.AddItem(greeting, out added, 0);
            added.textColor = old.textColor;
            added.selectedTextColor = old.selectedTextColor;
            added.textLabel.HorizontalAlignment = old.textLabel.HorizontalAlignment;
            added.textLabel.HorizontalTextAlignment = old.textLabel.HorizontalTextAlignment;
            added.textLabel.TextScale = old.textLabel.TextScale;
            added.textLabel.MaxWidth = old.textLabel.MaxWidth;
            added.textLabel.BackgroundColor = old.textLabel.BackgroundColor;
            conversation.RemoveItem(1);
            GreetingShown = greeting;
        }

        /// <summary>While one of our topics is selected, the "player says" line shows our question for the current tone.</summary>
        void UpdatePlayerLine()
        {
            if (OptionField == null || !"TellMeAbout".Equals(OptionField.GetValue(window).ToString()))
                return;
            DialogueTopic topic = SelectedTopic();
            TextLabel label = PlayerSaysField.GetValue(window) as TextLabel;
            if (topic == null || label == null)
                return;
            string question = TextMacros.Expand(Conversation.Question(topic, CurrentTone()), macros);
            if (label.Text != question)
                label.Text = question;
        }

        /// <summary>The player's line as it shows now (self-test).</summary>
        public string PlayerLine()
        {
            TextLabel label = window != null ? PlayerSaysField.GetValue(window) as TextLabel : null;
            return label != null ? label.Text : null;
        }

        DialogueTopic SelectedTopic()
        {
            ListBox topics = TopicList;
            List<TalkManager.ListItem> current = CurrentTopicsField.GetValue(window) as List<TalkManager.ListItem>;
            if (topics == null || current == null)
                return null;
            int index = topics.SelectedIndex;
            if (index < 0 || index >= current.Count || current[index].key == null)
                return null;
            DialogueTopic topic;
            return byKey.TryGetValue(current[index].key, out topic) ? topic : null;
        }

        /// <summary>The tone buttons' choice (Tones numbering); normal when unknown.</summary>
        public int CurrentTone()
        {
            if (window == null || ToneField == null)
                return Tones.Normal;
            return DaggerfallTalkWindow.TalkToneToIndex((DaggerfallTalkWindow.TalkTone)ToneField.GetValue(window));
        }

        ListBox TopicList
        {
            get { return window != null ? TopicListField.GetValue(window) as ListBox : null; }
        }

        ListBox ConversationList
        {
            get { return window != null ? ConversationField.GetValue(window) as ListBox : null; }
        }

        static bool ReflectionWorks()
        {
            return TopicListField != null && ConversationField != null && CurrentTopicsField != null && PlayerSaysField != null &&
                   OkButtonField != null && ToneField != null && AddPairMethod != null;
        }

        // Self-test helpers: act like the player.

        /// <summary>Selects one of our topics by caption in the window's list; false if it is not listed.</summary>
        public bool Select(string caption)
        {
            ListBox topics = TopicList;
            List<TalkManager.ListItem> current = window != null ? CurrentTopicsField.GetValue(window) as List<TalkManager.ListItem> : null;
            if (topics == null || current == null)
                return false;
            for (int i = 0; i < current.Count; i++)
            {
                if (current[i].caption == caption)
                {
                    topics.SelectedIndex = i;
                    return true;
                }
            }
            return false;
        }

        /// <summary>The text of the window's conversation lines, oldest first.</summary>
        public List<string> ConversationLines()
        {
            List<string> lines = new List<string>();
            ListBox conversation = ConversationList;
            if (conversation == null)
                return lines;
            for (int i = 0; i < conversation.Count; i++)
                lines.Add(conversation.GetItem(i).textLabel.Text);
            return lines;
        }
    }
}

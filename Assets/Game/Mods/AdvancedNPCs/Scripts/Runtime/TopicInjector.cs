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
        NpcBrain brain;
        // Reply choices (C2): the picker over the talk window, what it offers, and what to open next frame.
        public const string SayNothing = "(Say nothing.)";
        DaggerfallListPickerWindow picker;
        List<DialogueReply> offered = new List<DialogueReply>();
        List<DialogueReply> pendingReplies;
        string replyTopicId;
        bool endRequested;
        bool enemyRequested;
        Dictionary<string, string> macros;
        List<TalkManager.ListItem> injectedInto;
        int waitFrames;
        bool started;
        bool refreshNext;
        bool warnedCap;
        bool failed;
        static bool warnedNoBlankPair;
        float lastRecheck;
        int conversationCount;
        const float RecheckSeconds = 1f;

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

        public void Begin(DaggerfallTalkWindow talkWindow, NpcBrain npc, FlagSet flags, string npcName, ComposedDialogue composed)
        {
            End();
            // DFU may refuse to talk (bad reaction, rejected before, racial override) without opening the window.
            if (talkWindow == null || composed == null || composed.IsEmpty || !DaggerfallUI.UIManager.ContainsWindow(talkWindow))
                return;
            string missing = MissingMembers();
            if (missing.Length > 0)
            {
                if (!warnedMissing)
                {
                    warnedMissing = true;
                    AdvancedNpcsMod.Log("The talk window lacks " + missing + " (another mod replaced it, or a different DFU version); ANPC dialogue topics are not shown.");
                }
                return;
            }
            failed = false;
            lastRecheck = Time.realtimeSinceStartup;
            window = talkWindow;
            dialogue = composed;
            brain = npc;
            facts = new GameFacts(npc != null ? npc.State : null, flags, npcName, CurrentTone);
            facts.OnEndConversation = delegate { endRequested = true; };
            facts.OnBecomeEnemy = delegate { enemyRequested = true; };
            pendingReplies = null;
            endRequested = false;
            enemyRequested = false;
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
            ClosePicker();
            pendingReplies = null;
            window = null;
            facts = null;
            brain = null;
            DaggerfallUI.UIManager.OnWindowChange -= OnWindowChange;
        }

        /// <summary>The reply picker is open (self-test).</summary>
        public bool PickerOpen
        {
            get { return picker != null && DaggerfallUI.UIManager.ContainsWindow(picker); }
        }

        /// <summary>What the open picker offers, without "(Say nothing.)" (self-test).</summary>
        public List<string> OfferedTexts()
        {
            return offered.ConvertAll(delegate (DialogueReply r) { return TextMacros.Expand(r.Text, macros); });
        }

        public int RepliesSaid { get; private set; }

        void OpenPicker()
        {
            offered = pendingReplies;
            pendingReplies = null;
            picker = new DaggerfallListPickerWindow(DaggerfallUI.UIManager, window);
            foreach (DialogueReply r in offered)
                picker.ListBox.AddItem(TextMacros.Expand(r.Text, macros));
            picker.ListBox.AddItem(SayNothing);
            picker.OnItemPicked += OnReplyPicked;
            DaggerfallUI.UIManager.PushWindow(picker);
        }

        void ClosePicker()
        {
            if (picker == null)
                return;
            picker.OnItemPicked -= OnReplyPicked;
            if (DaggerfallUI.UIManager.ContainsWindow(picker))
                picker.CloseWindow();
            picker = null;
        }

        void OnReplyPicked(int index, string text)
        {
            try
            {
                PickReply(index);
            }
            catch (Exception e)
            {
                Fail("answering a reply", e);
            }
        }

        /// <summary>
        /// The player chose offered reply index (the last entry, or anything out of range, is "(Say nothing.)"): the
        /// player's line and the ANPC's answer go into the conversation, its actions run, and its own replies follow.
        /// </summary>
        public void PickReply(int index)
        {
            ClosePicker();
            if (window == null || index < 0 || index >= offered.Count)
                return;
            DialogueReply r = offered[index];
            string line = TextMacros.Expand(r.Text, macros);
            string answer;
            DialogueAnswer next = null;
            // Checked again: the player's gold or items may have changed since the list was made.
            if (!Conversation.CanSay(r, facts))
                answer = "(You cannot do that now.)";
            else
                answer = TextMacros.Expand(Conversation.Say(r, replyTopicId, facts, rng, facts, facts, out next), macros);
            AddPairMethod.Invoke(window, new object[] { line, answer });
            ListBox conversation = ConversationList;
            if (conversation != null)
                conversationCount = conversation.Count;
            RepliesSaid++;
            LastQuestion = line;
            LastAnswer = answer;
            refreshNext = true;
            if (next != null)
            {
                List<DialogueReply> more = Conversation.Replies(next, r.Replies, facts);
                if (more.Count > 0)
                    pendingReplies = more;
            }
        }

        /// <summary>The last line shown in a message box when the conversation ended (self-test).</summary>
        public string FinalMessage { get; private set; }

        /// <summary>endConversation / becomeEnemy: close the talk window first, then turn hostile (never talk to an enemy).</summary>
        void FinishConversation()
        {
            bool enemy = enemyRequested;
            NpcBrain npc = brain;
            endRequested = false;
            enemyRequested = false;
            pendingReplies = null;
            string lastLine = LastAnswer;
            DaggerfallUI.Instance.PopToHUD();
            End();
            if (enemy && npc != null)
                npc.SwitchHostile(true);
            // The parting (or threatening) line was on screen for one frame only: show it once more.
            FinalMessage = null;
            if (!string.IsNullOrEmpty(lastLine))
            {
                DaggerfallUI.MessageBox(lastLine);
                FinalMessage = lastLine;
            }
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
            try
            {
                Step();
            }
            catch (Exception e)
            {
                Fail("updating the talk window", e);
            }
        }

        /// <summary>Logs the problem once and stops showing topics for this conversation (no error every frame).</summary>
        void Fail(string doing, Exception e)
        {
            if (!failed)
                AdvancedNpcsMod.LogError("Dialogue topics stopped while " + doing + ": " + e);
            failed = true;
            End();
        }

        void Step()
        {
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
            // Conditions can also change while talking (the hour, a flag set by another mod): recheck now and then.
            bool recheck = Time.realtimeSinceStartup - lastRecheck >= RecheckSeconds;
            if (recheck)
                lastRecheck = Time.realtimeSinceStartup;
            if (refreshNext || !StillInList() || (recheck && VisibleChanged()))
            {
                refreshNext = false;
                Inject();
            }
            if (ConversationList.Count != conversationCount)
                Use(false);
            if (endRequested || enemyRequested)
            {
                FinishConversation();
                return;
            }
            // Open replies one frame after the line that offered them (never inside a UI event).
            if (pendingReplies != null && !PickerOpen)
                OpenPicker();
            UpdatePlayerLine();
        }

        bool VisibleChanged()
        {
            List<DialogueTopic> visible = Conversation.Visible(dialogue, facts, Conversation.MaxVisible);
            if (visible.Count != items.Count)
                return true;
            for (int i = 0; i < visible.Count; i++)
            {
                DialogueTopic shown;
                if (!byKey.TryGetValue(items[i].key, out shown) || shown != visible[i])
                    return true;
            }
            return false;
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
            TopicList.OnUseSelectedItem += OnListUse;
            Button ok = OkButtonField.GetValue(window) as Button;
            if (ok != null)
                ok.OnMouseClick += OnOkay;
            hookedWindow = window;
        }

        void OnOkay(BaseScreenComponent sender, Vector2 position)
        {
            OnListUse();
        }

        void OnListUse()
        {
            if (window == null)
                return;
            try
            {
                Use(true);
            }
            catch (Exception e)
            {
                Fail("answering a topic", e);
            }
        }

        /// <summary>
        /// If the window just answered one of our topics (it adds a blank question and a blank answer for it), puts
        /// our question and answer there instead. Safe to call more than once per use. fromEvent: called right after
        /// the player used a topic; if the window added no blank pair for ours (a modified window), answer anyway.
        /// </summary>
        void Use(bool fromEvent)
        {
            if (window == null || !started)
                return;
            ListBox conversation = ConversationList;
            DialogueTopic topic = SelectedTopic();
            int count = conversation.Count;
            bool blankPair = count >= conversationCount + 2 && count >= 2 && Blank(conversation, count - 1) && Blank(conversation, count - 2);
            if (topic == null || (!blankPair && !fromEvent))
            {
                conversationCount = count;
                return;
            }
            if (!blankPair && !warnedNoBlankPair)
            {
                warnedNoBlankPair = true;
                AdvancedNpcsMod.Log("The talk window did not add its usual blank lines for a dialogue topic (modified window?); answering anyway.");
            }

            // Work out everything first, so a problem leaves the conversation as it was.
            string question = TextMacros.Expand(Conversation.Question(topic, CurrentTone()), macros);
            DialogueAnswer chosen;
            string answer = TextMacros.Expand(Conversation.Ask(topic, facts, rng, facts, facts, out chosen), macros);
            if (blankPair)
            {
                conversation.RemoveItem(count - 1);
                conversation.RemoveItem(count - 2);
            }
            AddPairMethod.Invoke(window, new object[] { question, answer });
            conversationCount = conversation.Count;
            AnsweredCount++;
            LastQuestion = question;
            LastAnswer = answer;
            refreshNext = true;
            List<DialogueReply> replies = Conversation.Replies(chosen, topic.Replies, facts);
            replyTopicId = topic.Id;
            pendingReplies = replies.Count > 0 ? replies : null;
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

        /// <summary>Names of the talk-window members this needs that are missing, comma-separated ("" when all are there).</summary>
        static string MissingMembers()
        {
            List<string> missing = new List<string>();
            if (TopicListField == null) missing.Add("listboxTopic");
            if (ConversationField == null) missing.Add("listboxConversation");
            if (CurrentTopicsField == null) missing.Add("listCurrentTopics");
            if (PlayerSaysField == null) missing.Add("textlabelPlayerSays");
            if (OkButtonField == null) missing.Add("buttonOkay");
            if (ToneField == null) missing.Add("selectedTalkTone");
            if (AddPairMethod == null) missing.Add("SetQuestionAnswerPairInConversationListbox");
            return string.Join(", ", missing.ToArray());
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

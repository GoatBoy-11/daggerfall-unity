using System;
using System.Collections.Generic;

namespace AdvancedNPCs.Core
{
    /// <summary>What dialogue lines can do in the game (C2 spec §4); the runtime does it, Core decides when.</summary>
    public interface IDialogueActions
    {
        void GiveGold(int amount);
        void TakeGold(int amount);
        void GiveItem(string name);
        void TakeItem(string name);
        void ChangeReputation(int amount);
        void StartQuest(string name);
        void BecomeEnemy();
        void EndConversation();
    }

    /// <summary>
    /// Which topics an ANPC offers right now, which answer it gives and what asking changes (spec C1 §4.1, §5;
    /// replies and actions: C2 §3, §4).
    /// </summary>
    public static class Conversation
    {
        public const int MaxVisible = 30;

        /// <summary>The topics to list, in composed order, at most max.</summary>
        public static List<DialogueTopic> Visible(ComposedDialogue d, IDialogueFacts facts, int max)
        {
            List<DialogueTopic> shown = new List<DialogueTopic>();
            foreach (DialogueTopic t in d.Topics)
            {
                if (shown.Count >= max)
                    break;
                if (WhyHidden(t, facts) == null)
                    shown.Add(t);
            }
            return shown;
        }

        /// <summary>Null when the topic is shown; otherwise the reason ("when: time", "once (already asked)", ...).</summary>
        public static string WhyHidden(DialogueTopic t, IDialogueFacts facts)
        {
            if (t.Once && facts.Asked(t.Id))
                return "once (already asked)";
            string needs = Needs(t.Actions, facts);
            if (needs != null)
                return needs;
            string failing = t.When != null ? t.When.FirstFailing(facts) : null;
            if (failing != null)
                return "when: " + failing;
            if (PickAnswer(t.Answers, facts, null, true) == null)
                return "no answer applies";
            return null;
        }

        /// <summary>
        /// Answers whose condition holds win over plain ones; one of the winners at random (the first without rng).
        /// With ignoreTone, tone clauses count as holding (the player may still change tone). Null if none applies.
        /// </summary>
        public static DialogueAnswer PickAnswer(List<DialogueAnswer> answers, IDialogueFacts facts, Random rng, bool ignoreTone)
        {
            IDialogueFacts f = ignoreTone ? new ToneUnknown(facts) : facts;
            List<DialogueAnswer> specific = new List<DialogueAnswer>();
            List<DialogueAnswer> plain = new List<DialogueAnswer>();
            foreach (DialogueAnswer a in answers)
            {
                // An answer that takes gold or an item the player does not have is never given (C2 review I3).
                if (Needs(a.Actions, facts) != null)
                    continue;
                if (a.When == null || a.When.IsEmpty)
                    plain.Add(a);
                else if (a.When.Holds(f))
                    specific.Add(a);
            }
            List<DialogueAnswer> pool = specific.Count > 0 ? specific : plain;
            if (pool.Count == 0)
                return null;
            return pool[rng != null ? rng.Next(pool.Count) : 0];
        }

        /// <summary>The player's line for the tone, with {topic} filled in (other placeholders are left for the caller).</summary>
        public static string Question(DialogueTopic t, int tone)
        {
            string line = t.Question.For(tone);
            Dictionary<string, string> values = new Dictionary<string, string>();
            // Default lines read "Tell me about the house ale.", not "... The house ale.", and "Could you tell me about
            // any rumours?" not "... Any rumours??"; authored lines are kept as written.
            values[TextMacros.Topic] = line == QuestionText.Default(tone) ? LowerArticle(t.Caption).TrimEnd('?', '!', '.', ' ') : t.Caption;
            return TextMacros.Expand(line, values);
        }

        static string LowerArticle(string caption)
        {
            foreach (string article in new[] { "The ", "A ", "An " })
            {
                if (caption.StartsWith(article, StringComparison.Ordinal))
                    return article.ToLowerInvariant() + caption.Substring(article.Length);
            }
            return caption;
        }

        /// <summary>Picks the answer, applies the topic's and the answer's sets/clears and marks the topic asked.</summary>
        public static string Ask(DialogueTopic t, IDialogueFacts facts, Random rng, IDialogueState state)
        {
            DialogueAnswer chosen;
            return Ask(t, facts, rng, state, null, out chosen);
        }

        /// <summary>As Ask, also running the topic's and the answer's actions; chosen = the answer given (for its replies).</summary>
        public static string Ask(DialogueTopic t, IDialogueFacts facts, Random rng, IDialogueState state, IDialogueActions actions, out DialogueAnswer chosen)
        {
            chosen = PickAnswer(t.Answers, facts, rng, false) ?? t.Answers[0];
            Run(state, actions, new[] { t.Sets, chosen.Sets }, new[] { t.Clears, chosen.Clears }, new[] { t.Actions, chosen.Actions });
            state.MarkAsked(t.Id);
            return chosen.Text;
        }

        /// <summary>
        /// The replies to offer after an answer: the answer's own if it has any, else the fallback (the topic's, or the
        /// reply's that led here); only those whose `when` holds and whose takeGold / takeItem the player can pay.
        /// </summary>
        public static List<DialogueReply> Replies(DialogueAnswer answer, List<DialogueReply> fallback, IDialogueFacts facts)
        {
            List<DialogueReply> source = answer != null && answer.Replies.Count > 0 ? answer.Replies : fallback;
            List<DialogueReply> offered = new List<DialogueReply>();
            if (source == null)
                return offered;
            foreach (DialogueReply r in source)
            {
                if (CanSay(r, facts))
                    offered.Add(r);
            }
            return offered;
        }

        /// <summary>The reply may be said now (checked again when picked: gold may have changed meanwhile).</summary>
        public static bool CanSay(DialogueReply r, IDialogueFacts facts)
        {
            return Condition.Check(r.When, facts) && Needs(r.Actions, facts) == null && PickAnswer(r.Answers, facts, null, false) != null;
        }

        /// <summary>
        /// The player says the reply: picks the ANPC's answer (next, for its replies), runs the reply's and the answer's
        /// effects and marks "&lt;topic&gt;/&lt;reply&gt;" asked (normalised, so `"asked": "Ale/A mug, please."` matches).
        /// </summary>
        public static string Say(DialogueReply r, string topicId, IDialogueFacts facts, Random rng, IDialogueState state,
            IDialogueActions actions, out DialogueAnswer next)
        {
            next = PickAnswer(r.Answers, facts, rng, false) ?? r.Answers[0];
            Run(state, actions, new[] { r.Sets, next.Sets }, new[] { r.Clears, next.Clears }, new[] { r.Actions, next.Actions });
            state.MarkAsked(DialogueIds.Normalize(topicId + "/" + r.Id));
            return next.Text;
        }

        /// <summary>Null when the player can pay what the line takes, else "needs 50 gold" / "needs Ruby".</summary>
        public static string Needs(DialogueActions a, IDialogueFacts facts)
        {
            if (a.TakeGold > 0 && facts.Gold < a.TakeGold)
                return "needs " + a.TakeGold + " gold";
            if (a.TakeItem != null && !facts.HasItem(a.TakeItem))
                return "needs " + a.TakeItem;
            return null;
        }

        /// <summary>
        /// Effects of one line in a fixed order (C2 §4): takes, gives, reputation, flags, then quest, enemy and the end
        /// of the conversation. Each phase runs for every part of the line (topic or reply first, then the answer).
        /// </summary>
        static void Run(IDialogueState state, IDialogueActions act, List<string>[] sets, List<string>[] clears, DialogueActions[] parts)
        {
            if (act != null)
            {
                foreach (DialogueActions a in parts)
                {
                    if (a.TakeGold > 0)
                        act.TakeGold(a.TakeGold);
                    if (a.TakeItem != null)
                        act.TakeItem(a.TakeItem);
                    if (a.GiveGold > 0)
                        act.GiveGold(a.GiveGold);
                    if (a.GiveItem != null)
                        act.GiveItem(a.GiveItem);
                }
                foreach (DialogueActions a in parts)
                {
                    if (a.Reputation != 0)
                        act.ChangeReputation(a.Reputation);
                }
            }
            for (int i = 0; i < sets.Length; i++)
            {
                foreach (string flag in sets[i])
                    state.SetFlag(flag);
                foreach (string flag in clears[i])
                    state.ClearFlag(flag);
            }
            if (act == null)
                return;
            foreach (DialogueActions a in parts)
            {
                if (a.StartQuest != null)
                    act.StartQuest(a.StartQuest);
            }
            bool enemy = false, end = false;
            foreach (DialogueActions a in parts)
            {
                enemy |= a.BecomeEnemy;
                end |= a.EndConversation;
            }
            if (enemy)
                act.BecomeEnemy();
            if (end)
                act.EndConversation();
        }

        /// <summary>The greeting to show instead of DFU's, or null to keep DFU's.</summary>
        public static string Greeting(ComposedDialogue d, IDialogueFacts facts, Random rng)
        {
            DialogueAnswer a = PickAnswer(d.Greetings, facts, rng, false);
            return a != null ? a.Text : null;
        }

        /// <summary>The same facts with the tone not known yet.</summary>
        class ToneUnknown : IDialogueFacts
        {
            readonly IDialogueFacts f;

            public ToneUnknown(IDialogueFacts facts)
            {
                f = facts;
            }

            public int Hour { get { return f.Hour; } }
            public bool IsNight { get { return f.IsNight; } }
            public string Season { get { return f.Season; } }
            public bool Raining { get { return f.Raining; } }
            public bool Storming { get { return f.Storming; } }
            public bool Snowing { get { return f.Snowing; } }
            public bool Overcast { get { return f.Overcast; } }
            public string Region { get { return f.Region; } }
            public string Town { get { return f.Town; } }
            public int PlayerLevel { get { return f.PlayerLevel; } }
            public string PlayerRace { get { return f.PlayerRace; } }
            public string PlayerGender { get { return f.PlayerGender; } }
            public int Gold { get { return f.Gold; } }
            public string Reaction { get { return f.Reaction; } }
            public int Tone { get { return Tones.Unknown; } }
            public bool HasItem(string name) { return f.HasItem(name); }
            public int GuildRank(string guildKey) { return f.GuildRank(guildKey); }
            public bool HasFlag(string flag) { return f.HasFlag(flag); }
            public bool Asked(string topicId) { return f.Asked(topicId); }
            public bool QuestGlobal(string nameOrNumber) { return f.QuestGlobal(nameOrNumber); }
        }
    }

    /// <summary>Player-wide dialogue flags (spec C1 §6), saved with the game. Names are normalised like topic ids.</summary>
    public class FlagSet
    {
        readonly HashSet<string> flags = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>True if the flag was not set before.</summary>
        public bool Set(string name)
        {
            string n = DialogueIds.Normalize(name);
            return n.Length > 0 && flags.Add(n);
        }

        /// <summary>True if the flag was set before.</summary>
        public bool Clear(string name)
        {
            return flags.Remove(DialogueIds.Normalize(name));
        }

        public bool Has(string name)
        {
            return flags.Contains(DialogueIds.Normalize(name));
        }

        /// <summary>Set flags, sorted.</summary>
        public List<string> Names()
        {
            List<string> names = new List<string>(flags);
            names.Sort(StringComparer.Ordinal);
            return names;
        }

        public void Restore(IEnumerable<string> names)
        {
            flags.Clear();
            if (names == null)
                return;
            foreach (string n in names)
                Set(n);
        }

        public void ClearAll()
        {
            flags.Clear();
        }
    }
}

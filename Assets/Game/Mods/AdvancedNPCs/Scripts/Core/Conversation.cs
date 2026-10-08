using System;
using System.Collections.Generic;

namespace AdvancedNPCs.Core
{
    /// <summary>
    /// Which topics an ANPC offers right now, which answer it gives and what asking changes (spec C1 §4.1, §5).
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
            DialogueAnswer a = PickAnswer(t.Answers, facts, rng, false) ?? t.Answers[0];
            foreach (string flag in t.Sets)
                state.SetFlag(flag);
            foreach (string flag in t.Clears)
                state.ClearFlag(flag);
            foreach (string flag in a.Sets)
                state.SetFlag(flag);
            foreach (string flag in a.Clears)
                state.ClearFlag(flag);
            state.MarkAsked(t.Id);
            return a.Text;
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

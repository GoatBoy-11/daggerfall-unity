using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AdvancedNPCs.Core
{
    /// <summary>{player}, {npc}, {town}, {region} and {topic} in dialogue texts (spec C1 §4.3).</summary>
    public static class TextMacros
    {
        public const string Player = "player";
        public const string Npc = "npc";
        public const string Town = "town";
        public const string Region = "region";
        public const string Topic = "topic";

        static readonly string[] Known = { Player, Npc, Town, Region, Topic };
        static readonly Regex Pattern = new Regex(@"\{([A-Za-z_]+)\}");

        /// <summary>Replaces each {word} that has a value; anything else is left as written.</summary>
        public static string Expand(string text, IDictionary<string, string> values)
        {
            if (string.IsNullOrEmpty(text) || values == null)
                return text;
            return Pattern.Replace(text, delegate (Match m)
            {
                string value;
                return values.TryGetValue(m.Groups[1].Value, out value) && value != null ? value : m.Value;
            });
        }

        /// <summary>Placeholders in text that are not known (topic only counts as known where allowTopic).</summary>
        public static List<string> Unknown(string text, bool allowTopic)
        {
            List<string> unknown = new List<string>();
            if (string.IsNullOrEmpty(text))
                return unknown;
            foreach (Match m in Pattern.Matches(text))
            {
                string word = m.Groups[1].Value;
                bool known = System.Array.IndexOf(Known, word) >= 0 && (allowTopic || word != Topic);
                if (!known && !unknown.Contains(word))
                    unknown.Add(word);
            }
            return unknown;
        }
    }
}

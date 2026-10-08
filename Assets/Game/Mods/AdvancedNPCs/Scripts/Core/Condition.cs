using System;
using System.Collections.Generic;
using System.Globalization;

namespace AdvancedNPCs.Core
{
    /// <summary>
    /// What dialogue conditions read about the game (spec C1 §5). Enum-like values are lowercase keys:
    /// Season "spring|summer|autumn|winter", PlayerRace "breton|...|darkelf", PlayerGender "male|female",
    /// Reaction "dislikes|neutral|likes|loves". Tone is -1 while not known (deciding whether a topic is shown).
    /// </summary>
    public interface IDialogueFacts
    {
        int Hour { get; }
        bool IsNight { get; }
        string Season { get; }
        bool Raining { get; }
        bool Storming { get; }
        bool Snowing { get; }
        bool Overcast { get; }
        string Region { get; }
        string Town { get; }
        int PlayerLevel { get; }
        string PlayerRace { get; }
        string PlayerGender { get; }
        int Gold { get; }
        string Reaction { get; }
        int Tone { get; }
        bool HasItem(string name);
        /// <summary>Rank in the guild (a Guilds key), or -1 when not a member.</summary>
        int GuildRank(string guildKey);
        bool HasFlag(string flag);
        bool Asked(string topicId);
        /// <summary>A name from DFU's Quests-GlobalVars table or a number 0-63.</summary>
        bool QuestGlobal(string nameOrNumber);
    }

    /// <summary>Guild names authors may write, and the keys IDialogueFacts.GuildRank takes.</summary>
    public static class Guilds
    {
        public static readonly string[] Keys = { "fightersguild", "magesguild", "thievesguild", "darkbrotherhood", "temple", "knightlyorder" };

        /// <summary>"Fighters Guild" / "fighters_guild" / "FightersGuild" -> "fightersguild"; null if not a guild.</summary>
        public static string Key(string name)
        {
            string k = DialogueIds.Normalize(name).Replace("_", "");
            return Array.IndexOf(Keys, k) >= 0 ? k : null;
        }
    }

    /// <summary>A parsed "when" object: every clause must hold.</summary>
    public class Condition
    {
        readonly List<string> keys = new List<string>();
        readonly List<Predicate<IDialogueFacts>> tests = new List<Predicate<IDialogueFacts>>();

        /// <summary>Topic ids named in asked / notAsked (here and inside "any").</summary>
        public readonly List<string> AskedIds = new List<string>();
        /// <summary>Quest-global names (not numbers) named here and inside "any".</summary>
        public readonly List<string> QuestGlobals = new List<string>();
        /// <summary>A tone clause appears here or inside "any".</summary>
        public bool UsesTone;

        public bool IsEmpty
        {
            get { return keys.Count == 0; }
        }

        public void Add(string key, Predicate<IDialogueFacts> test)
        {
            keys.Add(key);
            tests.Add(test);
        }

        public bool Holds(IDialogueFacts facts)
        {
            return FirstFailing(facts) == null;
        }

        /// <summary>The key of the first clause that does not hold, or null.</summary>
        public string FirstFailing(IDialogueFacts facts)
        {
            for (int i = 0; i < tests.Count; i++)
            {
                if (!tests[i](facts))
                    return keys[i];
            }
            return null;
        }

        /// <summary>A missing condition always holds.</summary>
        public static bool Check(Condition c, IDialogueFacts facts)
        {
            return c == null || c.Holds(facts);
        }
    }

    /// <summary>Reads a "when" object (spec C1 §5). Problems become messages; the bad key is ignored.</summary>
    public static class ConditionParser
    {
        static readonly string[] KnownKeys =
        {
            "hours", "time", "season", "weather", "region", "town", "minLevel", "maxLevel", "playerRace", "playerGender",
            "minGold", "hasItem", "guild", "minGuildRank", "reaction", "asked", "notAsked", "flags", "notFlags",
            "questGlobal", "notQuestGlobal", "tone", "any",
        };
        static readonly string[] Seasons = { "spring", "summer", "autumn", "fall", "winter" };
        static readonly string[] Weathers = { "clear", "overcast", "rain", "storm", "snow" };
        static readonly string[] Races = { "breton", "redguard", "nord", "darkelf", "highelf", "woodelf", "khajiit", "argonian" };
        static readonly string[] Genders = { "male", "female" };
        public static readonly string[] Reactions = { "dislikes", "neutral", "likes", "loves" };

        /// <summary>DFU's reaction value as a band: below 0, 0-9, 10-29, 30 and up (TalkManager greeting thresholds).</summary>
        public static string ReactionBand(int reaction)
        {
            if (reaction >= 30)
                return "loves";
            if (reaction >= 10)
                return "likes";
            if (reaction >= 0)
                return "neutral";
            return "dislikes";
        }

        /// <param name="where">Message prefix, e.g. "_Dialogue/tavern_wench.json: topic \"Ale\"".</param>
        public static Condition Parse(Dictionary<string, object> o, string where, List<string> messages, bool allowTone)
        {
            return Parse(o, where, messages, allowTone, true);
        }

        /// <param name="allowConversation">False outside dialogue (spawn rules): asked, notAsked and reaction are rejected.</param>
        public static Condition Parse(Dictionary<string, object> o, string where, List<string> messages, bool allowTone, bool allowConversation)
        {
            Condition c = new Condition();
            if (o == null)
                return c;

            List<string> keys = new List<string>(o.Keys);
            keys.Sort(StringComparer.Ordinal);
            foreach (string key in keys)
            {
                object raw = o[key];
                string problem = null;
                switch (key)
                {
                    case "hours": problem = Hours(c, raw); break;
                    case "time": problem = Time(c, raw); break;
                    case "season": problem = Season(c, raw); break;
                    case "weather": problem = Weather(c, raw); break;
                    case "region": problem = Place(c, key, raw, delegate (IDialogueFacts f) { return f.Region; }); break;
                    case "town": problem = Place(c, key, raw, delegate (IDialogueFacts f) { return f.Town; }); break;
                    case "minLevel": problem = Minimum(c, key, raw, delegate (IDialogueFacts f) { return f.PlayerLevel; }); break;
                    case "maxLevel": problem = Maximum(c, key, raw, delegate (IDialogueFacts f) { return f.PlayerLevel; }); break;
                    case "minGold": problem = Minimum(c, key, raw, delegate (IDialogueFacts f) { return f.Gold; }); break;
                    case "playerRace": problem = OneOf(c, key, raw, Races, true, delegate (IDialogueFacts f) { return f.PlayerRace; }); break;
                    case "playerGender": problem = OneOf(c, key, raw, Genders, false, delegate (IDialogueFacts f) { return f.PlayerGender; }); break;
                    case "reaction": problem = !allowConversation ? OnlyDialogue : OneOf(c, key, raw, Reactions, false, delegate (IDialogueFacts f) { return f.Reaction; }); break;
                    case "hasItem": problem = HasItem(c, raw); break;
                    case "guild": problem = Guild(c, raw, o); break;
                    case "minGuildRank": problem = o.ContainsKey("guild") ? null : "needs \"guild\""; break;
                    case "asked": problem = !allowConversation ? OnlyDialogue : Ids(c, key, raw, true, false); break;
                    case "notAsked": problem = !allowConversation ? OnlyDialogue : Ids(c, key, raw, true, true); break;
                    case "flags": problem = Ids(c, key, raw, false, false); break;
                    case "notFlags": problem = Ids(c, key, raw, false, true); break;
                    case "questGlobal": problem = QuestGlobal(c, key, raw, false); break;
                    case "notQuestGlobal": problem = QuestGlobal(c, key, raw, true); break;
                    case "tone": problem = allowTone ? Tone(c, raw) : "only allowed in answers"; break;
                    case "any": problem = Any(c, raw, where, messages, allowTone, allowConversation); break;
                    default:
                        string suggestion = Suggest(key);
                        problem = "unknown condition" + (suggestion != null ? " (did you mean \"" + suggestion + "\"?)" : "");
                        break;
                }
                if (problem != null)
                {
                    // A broken condition must not unlock what it was meant to lock: it never holds until fixed.
                    messages.Add(where + ": when: " + key + ": " + problem + NeverHolds);
                    c.Add(key + " (invalid)", delegate (IDialogueFacts f) { return false; });
                }
            }
            return c;
        }

        public const string NeverHolds = "; this condition never holds until fixed";
        const string OnlyDialogue = "only allowed in dialogue";

        static string Hours(Condition c, object raw)
        {
            List<object> list = raw as List<object>;
            if (list == null || list.Count != 2 || !WholeHour(list[0]) || !WholeHour(list[1]))
                return "must be [from, to], whole hours 0-23";
            int from = (int)(double)list[0];
            int to = (int)(double)list[1];
            c.Add("hours", delegate (IDialogueFacts f) { return InHours(f.Hour, from, to); });
            return null;
        }

        /// <summary>[from, to) wrapping past midnight; from == to means all day (same rule as hostileHours).</summary>
        public static bool InHours(int hour, int from, int to)
        {
            if (from == to)
                return true;
            if (from < to)
                return hour >= from && hour < to;
            return hour >= from || hour < to;
        }

        static bool WholeHour(object o)
        {
            if (!(o is double))
                return false;
            double h = (double)o;
            return h == Math.Floor(h) && h >= 0 && h <= 23;
        }

        static string Time(Condition c, object raw)
        {
            string t = raw as string;
            t = t != null ? t.Trim().ToLowerInvariant() : null;
            if (t != "day" && t != "night")
                return "must be day or night";
            bool night = t == "night";
            c.Add("time", delegate (IDialogueFacts f) { return f.IsNight == night; });
            return null;
        }

        static string Season(Condition c, object raw)
        {
            List<string> values;
            string problem = Choices(raw, Seasons, false, out values);
            if (problem != null)
                return problem;
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] == "fall")
                    values[i] = "autumn";
            }
            c.Add("season", delegate (IDialogueFacts f) { return values.Contains(f.Season); });
            return null;
        }

        static string Weather(Condition c, object raw)
        {
            List<string> values;
            string problem = Choices(raw, Weathers, false, out values);
            if (problem != null)
                return problem;
            c.Add("weather", delegate (IDialogueFacts f)
            {
                foreach (string w in values)
                {
                    if (WeatherIs(f, w))
                        return true;
                }
                return false;
            });
            return null;
        }

        static bool WeatherIs(IDialogueFacts f, string w)
        {
            switch (w)
            {
                case "rain": return f.Raining || f.Storming;
                case "storm": return f.Storming;
                case "snow": return f.Snowing;
                case "overcast": return f.Overcast && !f.Raining && !f.Storming && !f.Snowing;
                default: return !f.Overcast && !f.Raining && !f.Storming && !f.Snowing;
            }
        }

        static string Place(Condition c, string key, object raw, Func<IDialogueFacts, string> read)
        {
            List<string> values;
            if (!Texts(raw, out values))
                return "must be a name or a list of names";
            c.Add(key, delegate (IDialogueFacts f)
            {
                string here = read(f);
                foreach (string v in values)
                {
                    if (DefinitionCatalog.SamePlaceName(v, here))
                        return true;
                }
                return false;
            });
            return null;
        }

        static string Minimum(Condition c, string key, object raw, Func<IDialogueFacts, int> read)
        {
            if (!(raw is double))
                return "must be a number";
            double min = (double)raw;
            c.Add(key, delegate (IDialogueFacts f) { return read(f) >= min; });
            return null;
        }

        static string Maximum(Condition c, string key, object raw, Func<IDialogueFacts, int> read)
        {
            if (!(raw is double))
                return "must be a number";
            double max = (double)raw;
            c.Add(key, delegate (IDialogueFacts f) { return read(f) <= max; });
            return null;
        }

        static string OneOf(Condition c, string key, object raw, string[] allowed, bool ignoreSpaces, Func<IDialogueFacts, string> read)
        {
            List<string> values;
            string problem = Choices(raw, allowed, ignoreSpaces, out values);
            if (problem != null)
                return problem;
            c.Add(key, delegate (IDialogueFacts f) { return values.Contains(read(f)); });
            return null;
        }

        static string HasItem(Condition c, object raw)
        {
            List<string> values;
            if (!Texts(raw, out values))
                return "must be an item name or a list of names";
            c.Add("hasItem", delegate (IDialogueFacts f)
            {
                foreach (string v in values)
                {
                    if (f.HasItem(v))
                        return true;
                }
                return false;
            });
            return null;
        }

        static string Guild(Condition c, object raw, Dictionary<string, object> o)
        {
            List<string> names;
            if (!Texts(raw, out names))
                return "must be a guild name or a list of names";
            List<string> keys = new List<string>();
            foreach (string n in names)
            {
                string k = Guilds.Key(n);
                if (k == null)
                    return "unknown guild \"" + n + "\" (use Fighters Guild, Mages Guild, Thieves Guild, Dark Brotherhood, Temple or Knightly Order)";
                keys.Add(k);
            }
            int minRank = 0;
            object rawRank;
            if (o.TryGetValue("minGuildRank", out rawRank) && rawRank != null)
            {
                if (!(rawRank is double) || (double)rawRank < 0 || (double)rawRank > 10)
                    return "minGuildRank must be a number 0-10";
                minRank = (int)(double)rawRank;
            }
            c.Add("guild", delegate (IDialogueFacts f)
            {
                foreach (string k in keys)
                {
                    if (f.GuildRank(k) >= minRank)
                        return true;
                }
                return false;
            });
            return null;
        }

        static string Ids(Condition c, string key, object raw, bool topics, bool none)
        {
            List<string> names;
            if (!Texts(raw, out names))
                return "must be a name or a list of names";
            List<string> ids = new List<string>();
            foreach (string n in names)
            {
                string id = DialogueIds.Normalize(n);
                if (id.Length == 0)
                    return "\"" + n + "\" has no letters or digits";
                ids.Add(id);
                if (topics)
                    c.AskedIds.Add(id);
            }
            c.Add(key, delegate (IDialogueFacts f)
            {
                foreach (string id in ids)
                {
                    bool has = topics ? f.Asked(id) : f.HasFlag(id);
                    if (has == none)
                        return false;
                }
                return true;
            });
            return null;
        }

        static string QuestGlobal(Condition c, string key, object raw, bool none)
        {
            List<object> list = raw as List<object>;
            if (list == null)
                list = new List<object> { raw };
            List<string> names = new List<string>();
            foreach (object item in list)
            {
                if (item is double)
                {
                    double n = (double)item;
                    if (n != Math.Floor(n) || n < 0 || n > 63)
                        return "numbers must be whole, 0-63";
                    names.Add(((int)n).ToString(CultureInfo.InvariantCulture));
                }
                else if (item is string && ((string)item).Trim().Length > 0)
                {
                    string name = ((string)item).Trim();
                    names.Add(name);
                    c.QuestGlobals.Add(name);
                }
                else
                {
                    return "must be a quest global name or number, or a list of them";
                }
            }
            c.Add(key, delegate (IDialogueFacts f)
            {
                foreach (string n in names)
                {
                    if (f.QuestGlobal(n) == none)
                        return false;
                }
                return true;
            });
            return null;
        }

        static string Tone(Condition c, object raw)
        {
            List<string> values;
            string problem = Choices(raw, Tones.Names, false, out values);
            if (problem != null)
                return problem;
            List<int> tones = new List<int>();
            foreach (string v in values)
                tones.Add(Tones.Parse(v));
            c.UsesTone = true;
            c.Add("tone", delegate (IDialogueFacts f) { return f.Tone < 0 || tones.Contains(f.Tone); });
            return null;
        }

        static string Any(Condition c, object raw, string where, List<string> messages, bool allowTone, bool allowConversation)
        {
            List<object> list = raw as List<object>;
            if (list == null || list.Count == 0)
                return "must be a list of condition objects";
            List<Condition> options = new List<Condition>();
            foreach (object item in list)
            {
                Dictionary<string, object> o = item as Dictionary<string, object>;
                if (o == null)
                    return "must be a list of condition objects";
                Condition option = Parse(o, where, messages, allowTone, allowConversation);
                options.Add(option);
                c.AskedIds.AddRange(option.AskedIds);
                c.QuestGlobals.AddRange(option.QuestGlobals);
                c.UsesTone |= option.UsesTone;
            }
            c.Add("any", delegate (IDialogueFacts f)
            {
                foreach (Condition option in options)
                {
                    if (option.Holds(f))
                        return true;
                }
                return false;
            });
            return null;
        }

        /// <summary>A text or a list of texts, each one of allowed (case ignored; spaces too when ignoreSpaces).</summary>
        static string Choices(object raw, string[] allowed, bool ignoreSpaces, out List<string> values)
        {
            values = null;
            List<string> texts;
            if (!Texts(raw, out texts))
                return "must be one of " + string.Join(", ", allowed) + " (or a list of them)";
            List<string> result = new List<string>();
            foreach (string t in texts)
            {
                string v = t.Trim().ToLowerInvariant();
                if (ignoreSpaces)
                    v = v.Replace(" ", "").Replace("_", "");
                if (Array.IndexOf(allowed, v) < 0)
                    return "\"" + t + "\" is not one of " + string.Join(", ", allowed);
                result.Add(v);
            }
            values = result;
            return null;
        }

        /// <summary>A non-empty text or a non-empty list of non-empty texts.</summary>
        static bool Texts(object raw, out List<string> values)
        {
            values = new List<string>();
            string single = raw as string;
            if (single != null)
            {
                if (single.Trim().Length == 0)
                    return false;
                values.Add(single);
                return true;
            }
            List<object> list = raw as List<object>;
            if (list == null || list.Count == 0)
                return false;
            foreach (object item in list)
            {
                string s = item as string;
                if (s == null || s.Trim().Length == 0)
                    return false;
                values.Add(s);
            }
            return true;
        }

        public static string Suggest(string key)
        {
            return Typos.Closest(key, KnownKeys);
        }
    }

    /// <summary>"Did you mean" help for misspelt field names.</summary>
    public static class Typos
    {
        /// <summary>The known name closest to a typo (same letters ignoring case, or at most 2 edits), or null.</summary>
        public static string Closest(string key, string[] knownNames)
        {
            string best = null;
            int bestDistance = 3;
            foreach (string known in knownNames)
            {
                int d = string.Equals(known, key, StringComparison.OrdinalIgnoreCase) ? 0 : Distance(known.ToLowerInvariant(), key.ToLowerInvariant());
                if (d < bestDistance)
                {
                    best = known;
                    bestDistance = d;
                }
            }
            return best;
        }

        static int Distance(string a, string b)
        {
            int[,] d = new int[a.Length + 1, b.Length + 1];
            for (int i = 0; i <= a.Length; i++)
                d[i, 0] = i;
            for (int j = 0; j <= b.Length; j++)
                d[0, j] = j;
            for (int i = 1; i <= a.Length; i++)
            {
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                }
            }
            return d[a.Length, b.Length];
        }
    }
}

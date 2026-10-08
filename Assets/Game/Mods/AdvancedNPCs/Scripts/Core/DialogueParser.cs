using System.Collections.Generic;

namespace AdvancedNPCs.Core
{
    public class DialogueParseResult
    {
        /// <summary>Null when the file cannot be used at all.</summary>
        public DialogueFile File;
        public readonly List<string> Messages = new List<string>();
    }

    /// <summary>
    /// Reads a dialogue file (a _Dialogue type or a folder's dialogue.json, spec C1 §4). A bad topic, answer or field
    /// is reported and skipped; the rest of the file still loads.
    /// </summary>
    public static class DialogueParser
    {
        public const int MaxTopics = 40;
        public const int LongCaption = 24;

        static readonly string[] FileKeys = { "topics", "greetings" };
        public const int MaxReplyDepth = 8;

        static readonly string[] ActionKeys = { "giveGold", "takeGold", "giveItem", "takeItem", "reputation", "startQuest", "becomeEnemy", "endConversation" };
        static readonly string[] TopicKeys = Join(new[] { "caption", "id", "answers", "when", "question", "sets", "clears", "once", "replies" }, ActionKeys);
        static readonly string[] AnswerKeys = Join(new[] { "text", "when", "sets", "clears", "replies" }, ActionKeys);
        static readonly string[] ReplyKeys = Join(new[] { "text", "id", "when", "answers", "replies", "sets", "clears" }, ActionKeys);

        static string[] Join(string[] a, string[] b)
        {
            List<string> all = new List<string>(a);
            all.AddRange(b);
            return all.ToArray();
        }

        public static DialogueParseResult Parse(string source, string json)
        {
            DialogueParseResult r = new DialogueParseResult();
            if (string.IsNullOrEmpty(json) || json.Trim().Length == 0)
            {
                r.Messages.Add(source + ": file: empty");
                return r;
            }
            object root;
            try
            {
                root = Json.Parse(json);
            }
            catch (JsonException e)
            {
                r.Messages.Add(source + ": file: invalid JSON (" + e.Message + ")");
                return r;
            }
            Dictionary<string, object> o = root as Dictionary<string, object>;
            if (o == null)
            {
                r.Messages.Add(source + ": file: invalid JSON (top level must be an object)");
                return r;
            }
            if (!o.ContainsKey("topics") && !o.ContainsKey("greetings"))
            {
                r.Messages.Add(source + ": file: needs \"topics\" or \"greetings\"");
                return r;
            }

            DialogueFile file = new DialogueFile();
            UnknownKeys(o, FileKeys, source, r.Messages);
            ReadTopics(source, o, file, r.Messages);
            ReadGreetings(source, o, file, r.Messages);
            r.File = file;
            return r;
        }

        static void ReadTopics(string source, Dictionary<string, object> o, DialogueFile file, List<string> messages)
        {
            object raw;
            if (!o.TryGetValue("topics", out raw) || raw == null)
                return;
            List<object> list = raw as List<object>;
            if (list == null)
            {
                messages.Add(source + ": topics: must be a list of topic objects");
                return;
            }
            if (list.Count > MaxTopics)
                messages.Add(source + ": topics: more than " + MaxTopics + " topics, the rest are ignored");

            HashSet<string> ids = new HashSet<string>();
            for (int i = 0; i < list.Count && i < MaxTopics; i++)
            {
                Dictionary<string, object> t = list[i] as Dictionary<string, object>;
                if (t == null)
                {
                    messages.Add(source + ": topic " + (i + 1) + ": must be an object, topic skipped");
                    continue;
                }
                DialogueTopic topic = ReadTopic(source, i, t, messages);
                if (topic == null)
                    continue;
                if (!ids.Add(topic.Id))
                {
                    messages.Add(source + ": topic \"" + topic.Caption + "\": id: \"" + topic.Id + "\" is used by an earlier topic, topic skipped");
                    continue;
                }
                file.Topics.Add(topic);
            }
        }

        static DialogueTopic ReadTopic(string source, int index, Dictionary<string, object> t, List<string> messages)
        {
            string caption = t.ContainsKey("caption") ? t["caption"] as string : null;
            if (caption == null || caption.Trim().Length == 0)
            {
                messages.Add(source + ": topic " + (index + 1) + ": caption: required, topic skipped");
                return null;
            }
            caption = caption.Trim();
            string where = source + ": topic \"" + caption + "\"";

            DialogueTopic topic = new DialogueTopic();
            topic.Caption = caption;
            topic.Source = source;
            if (caption.Length > LongCaption)
                messages.Add(where + ": caption: longer than " + LongCaption + " characters, may not fit the topic list");
            Placeholders(caption, false, where + ": caption", messages);

            topic.Id = DialogueIds.Normalize(caption);
            object rawId;
            if (t.TryGetValue("id", out rawId) && rawId != null)
            {
                string id = DialogueIds.Normalize(rawId as string);
                if (id.Length == 0)
                    messages.Add(where + ": id: must be a name with letters or digits, using \"" + topic.Id + "\"");
                else
                    topic.Id = id;
            }
            if (topic.Id.Length == 0)
                topic.Id = "topic_" + (index + 1);

            UnknownKeys(t, TopicKeys, where, messages);

            object rawWhen;
            if (t.TryGetValue("when", out rawWhen) && rawWhen != null)
            {
                Dictionary<string, object> when = rawWhen as Dictionary<string, object>;
                if (when == null)
                    messages.Add(where + ": when: must be an object, ignored");
                else
                    topic.When = ConditionParser.Parse(when, where, messages, false);
            }

            ReadQuestion(where, t, topic.Question, messages);
            ReadNames(where, t, "sets", topic.Sets, messages);
            ReadNames(where, t, "clears", topic.Clears, messages);
            ReadActions(where, t, topic.Actions, messages);
            ReadReplies(where + ": replies", t, topic.Replies, 1, messages);

            object rawOnce;
            if (t.TryGetValue("once", out rawOnce) && rawOnce != null)
            {
                if (rawOnce is bool)
                    topic.Once = (bool)rawOnce;
                else
                    messages.Add(where + ": once: must be true or false, ignored");
            }

            object rawAnswers;
            t.TryGetValue("answers", out rawAnswers);
            ReadAnswers(where + ": answers", "answer", rawAnswers, topic.Answers, true, messages, 1);
            if (topic.Answers.Count == 0)
            {
                messages.Add(where + ": answers: required (one or more texts), topic skipped");
                return null;
            }
            return topic;
        }

        static void ReadQuestion(string where, Dictionary<string, object> t, QuestionText q, List<string> messages)
        {
            object raw;
            if (!t.TryGetValue("question", out raw) || raw == null)
                return;
            string all = raw as string;
            if (all != null)
            {
                if (all.Trim().Length > 0)
                {
                    q.All = all;
                    Placeholders(all, true, where + ": question", messages);
                }
                return;
            }
            Dictionary<string, object> perTone = raw as Dictionary<string, object>;
            if (perTone == null)
            {
                messages.Add(where + ": question: must be a text or { \"polite\", \"normal\", \"blunt\" }, ignored");
                return;
            }
            foreach (KeyValuePair<string, object> kv in perTone)
            {
                int tone = Tones.Parse(kv.Key);
                string text = kv.Value as string;
                if (tone == Tones.Unknown)
                {
                    messages.Add(where + ": question: " + kv.Key + ": unknown tone (use polite, normal or blunt), ignored");
                    continue;
                }
                if (text == null || text.Trim().Length == 0)
                {
                    messages.Add(where + ": question: " + kv.Key + ": must be a text, ignored");
                    continue;
                }
                Placeholders(text, true, where + ": question: " + kv.Key, messages);
                if (tone == Tones.Polite)
                    q.Polite = text;
                else if (tone == Tones.Blunt)
                    q.Blunt = text;
                else
                    q.Normal = text;
            }
        }

        static void ReadGreetings(string source, Dictionary<string, object> o, DialogueFile file, List<string> messages)
        {
            object raw;
            if (!o.TryGetValue("greetings", out raw) || raw == null)
                return;
            ReadAnswers(source + ": greetings", "greeting", raw, file.Greetings, false, messages, 0);
        }

        /// <param name="isAnswer">False for greetings: no tone, no sets/clears.</param>
        /// <param name="depth">Reply nesting level of these answers (replies on them are one deeper).</param>
        static void ReadAnswers(string where, string noun, object raw, List<DialogueAnswer> into, bool isAnswer, List<string> messages, int depth)
        {
            if (raw == null)
                return;
            List<object> list = raw as List<object>;
            if (list == null)
                list = new List<object> { raw };
            for (int i = 0; i < list.Count; i++)
            {
                string at = where + ": " + noun + " " + (i + 1);
                DialogueAnswer a = new DialogueAnswer();
                string text = list[i] as string;
                Dictionary<string, object> obj = list[i] as Dictionary<string, object>;
                if (text != null)
                {
                    a.Text = text;
                }
                else if (obj != null)
                {
                    UnknownKeys(obj, AnswerKeys, at, messages);
                    a.Text = obj.ContainsKey("text") ? obj["text"] as string : null;
                    object rawWhen;
                    if (obj.TryGetValue("when", out rawWhen) && rawWhen != null)
                    {
                        Dictionary<string, object> when = rawWhen as Dictionary<string, object>;
                        if (when == null)
                            messages.Add(at + ": when: must be an object, ignored");
                        else
                            a.When = ConditionParser.Parse(when, at, messages, isAnswer);
                    }
                    if (isAnswer)
                    {
                        ReadNames(at, obj, "sets", a.Sets, messages);
                        ReadNames(at, obj, "clears", a.Clears, messages);
                        ReadActions(at, obj, a.Actions, messages);
                        ReadReplies(at + ": replies", obj, a.Replies, depth + 1, messages);
                    }
                    else
                    {
                        List<string> notHere = new List<string> { "sets", "clears", "replies" };
                        notHere.AddRange(ActionKeys);
                        foreach (string key in notHere)
                        {
                            if (obj.ContainsKey(key))
                                messages.Add(at + ": " + key + ": not allowed in greetings, ignored");
                        }
                    }
                    if (a.Text == null || a.Text.Trim().Length == 0)
                    {
                        messages.Add(at + ": text: required, " + noun + " skipped");
                        continue;
                    }
                }
                else
                {
                    messages.Add(at + ": must be a text or an object with \"text\", " + noun + " skipped");
                    continue;
                }
                if (a.Text.Trim().Length == 0)
                {
                    messages.Add(at + ": empty text, " + noun + " skipped");
                    continue;
                }
                Placeholders(a.Text, false, at, messages);
                into.Add(a);
            }
        }

        /// <summary>The "replies" list of a topic, answer or reply (C2 spec §3).</summary>
        static void ReadReplies(string where, Dictionary<string, object> o, List<DialogueReply> into, int depth, List<string> messages)
        {
            object raw;
            if (!o.TryGetValue("replies", out raw) || raw == null)
                return;
            List<object> list = raw as List<object>;
            if (list == null)
            {
                messages.Add(where + ": must be a list of reply objects, ignored");
                return;
            }
            if (depth > MaxReplyDepth)
            {
                messages.Add(where + ": nested deeper than " + MaxReplyDepth + " levels, the deeper ones are ignored");
                return;
            }
            for (int i = 0; i < list.Count; i++)
            {
                string at = where + ": reply " + (i + 1);
                Dictionary<string, object> r = list[i] as Dictionary<string, object>;
                if (r == null)
                {
                    messages.Add(at + ": must be an object with \"text\" and \"answers\", reply skipped");
                    continue;
                }
                string text = r.ContainsKey("text") ? r["text"] as string : null;
                if (text == null || text.Trim().Length == 0)
                {
                    messages.Add(at + ": text: required, reply skipped");
                    continue;
                }
                UnknownKeys(r, ReplyKeys, at, messages);
                DialogueReply reply = new DialogueReply();
                reply.Text = text;
                Placeholders(text, false, at + ": text", messages);
                reply.Id = DialogueIds.Normalize(text);
                object rawId;
                if (r.TryGetValue("id", out rawId) && rawId != null)
                {
                    string id = DialogueIds.Normalize(rawId as string);
                    if (id.Length > 0)
                        reply.Id = id;
                    else
                        messages.Add(at + ": id: must be a name with letters or digits, using \"" + reply.Id + "\"");
                }
                object rawWhen;
                if (r.TryGetValue("when", out rawWhen) && rawWhen != null)
                {
                    Dictionary<string, object> when = rawWhen as Dictionary<string, object>;
                    if (when == null)
                        messages.Add(at + ": when: must be an object, ignored");
                    else
                        reply.When = ConditionParser.Parse(when, at, messages, true);
                }
                ReadNames(at, r, "sets", reply.Sets, messages);
                ReadNames(at, r, "clears", reply.Clears, messages);
                ReadActions(at, r, reply.Actions, messages);
                object rawAnswers;
                r.TryGetValue("answers", out rawAnswers);
                ReadAnswers(at + ": answers", "answer", rawAnswers, reply.Answers, true, messages, depth);
                if (reply.Answers.Count == 0)
                {
                    messages.Add(at + ": answers: required (one or more texts), reply skipped");
                    continue;
                }
                ReadReplies(at + ": replies", r, reply.Replies, depth + 1, messages);
                into.Add(reply);
            }
        }

        /// <summary>giveGold, takeGold, giveItem, takeItem, reputation, startQuest, becomeEnemy, endConversation (C2 spec §4).</summary>
        static void ReadActions(string where, Dictionary<string, object> o, DialogueActions a, List<string> messages)
        {
            a.GiveGold = Amount(where, o, "giveGold", 1, 100000, "a whole number 1-100000", messages);
            a.TakeGold = Amount(where, o, "takeGold", 1, 100000, "a whole number 1-100000", messages);
            a.Reputation = Amount(where, o, "reputation", -20, 20, "a whole number -20 to 20 (not 0)", messages);
            a.GiveItem = Name(where, o, "giveItem", "an item name", messages);
            a.TakeItem = Name(where, o, "takeItem", "an item name", messages);
            a.StartQuest = Name(where, o, "startQuest", "a quest name", messages);
            a.BecomeEnemy = Flag(where, o, "becomeEnemy", messages);
            a.EndConversation = Flag(where, o, "endConversation", messages);
        }

        static int Amount(string where, Dictionary<string, object> o, string key, int min, int max, string rule, List<string> messages)
        {
            object raw;
            if (!o.TryGetValue(key, out raw) || raw == null)
                return 0;
            if (!(raw is double) || (double)raw != System.Math.Floor((double)raw) || (double)raw < min || (double)raw > max || (double)raw == 0)
            {
                messages.Add(where + ": " + key + ": must be " + rule + ", ignored");
                return 0;
            }
            return (int)(double)raw;
        }

        static string Name(string where, Dictionary<string, object> o, string key, string rule, List<string> messages)
        {
            object raw;
            if (!o.TryGetValue(key, out raw) || raw == null)
                return null;
            string s = raw as string;
            if (s == null || s.Trim().Length == 0)
            {
                messages.Add(where + ": " + key + ": must be " + rule + ", ignored");
                return null;
            }
            return s.Trim();
        }

        static bool Flag(string where, Dictionary<string, object> o, string key, List<string> messages)
        {
            object raw;
            if (!o.TryGetValue(key, out raw) || raw == null)
                return false;
            if (!(raw is bool))
            {
                messages.Add(where + ": " + key + ": must be true or false, ignored");
                return false;
            }
            return (bool)raw;
        }

        /// <summary>Flag names (a name or a list) into names, normalised.</summary>
        static void ReadNames(string where, Dictionary<string, object> o, string key, List<string> names, List<string> messages)
        {
            object raw;
            if (!o.TryGetValue(key, out raw) || raw == null)
                return;
            List<object> list = raw as List<object>;
            if (list == null)
                list = new List<object> { raw };
            foreach (object item in list)
            {
                string id = DialogueIds.Normalize(item as string);
                if (id.Length == 0)
                {
                    messages.Add(where + ": " + key + ": must be flag names with letters or digits, ignored");
                    return;
                }
                if (!names.Contains(id))
                    names.Add(id);
            }
        }

        static void Placeholders(string text, bool allowTopic, string where, List<string> messages)
        {
            foreach (string word in TextMacros.Unknown(text, allowTopic))
                messages.Add(where + ": unknown placeholder {" + word + "} (use {player}, {npc}, {town}, {region}" + (allowTopic ? ", {topic}" : "") + ")");
        }

        static void UnknownKeys(Dictionary<string, object> o, string[] known, string where, List<string> messages)
        {
            foreach (string key in FieldReader.UnknownKeys(o, known))
            {
                string suggestion = Typos.Closest(key, known);
                messages.Add(where + ": " + key + ": unknown field" + (suggestion != null ? " (did you mean \"" + suggestion + "\"?)" : "") + ", ignored");
            }
        }
    }
}

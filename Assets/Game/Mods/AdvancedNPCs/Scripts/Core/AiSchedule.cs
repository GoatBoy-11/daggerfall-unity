using System;
using System.Collections.Generic;
using System.Text;

namespace AdvancedNPCs.Core
{
    public class AiScheduleEntry
    {
        public string Package;
        public NpcVariableCondition When;
        public Condition Rules;
        public bool Conditional { get { return When != null || Rules != null; } }
        public int From = -1;
        public int To = -1;
        public Dictionary<string, object> Parameters = new Dictionary<string, object>();
        public bool Includes(int minute)
        {
            return From < 0 || (From < To ? minute >= From && minute < To : minute >= From || minute < To);
        }
    }

    /// <summary>Optional schedules fail independently of NPC definitions. All times are game minutes.</summary>
    public static class AiSchedule
    {
        public static readonly string[] Keys = { "hours", "package", "parameters", "when" };

        public static List<AiScheduleEntry> Read(object raw, out string error) { return Read(raw, out error, null); }

        public static List<AiScheduleEntry> Read(object raw, out string error, NpcDefinition definition)
        {
            error = null;
            List<AiScheduleEntry> result = new List<AiScheduleEntry>();
            if (raw == null) return result;
            List<object> rows = raw as List<object>;
            if (rows == null || rows.Count > 64)
                return Fail("must be an array of at most 64 entries", out error);
            bool fallback = false;
            bool[] occupied = new bool[1440];
            for (int i = 0; i < rows.Count; i++)
            {
                string where = "entry " + (i + 1) + ": ";
                Dictionary<string, object> row = rows[i] as Dictionary<string, object>;
                if (row == null) return Fail(where + "must be an object", out error);
                List<string> unknown = FieldReader.UnknownKeys(row, Keys);
                if (unknown.Count > 0) return Fail(where + "unknown field " + unknown[0], out error);
                AiScheduleEntry entry = new AiScheduleEntry();
                string problem = FieldReader.Text(row, "package", null, out entry.Package);
                if (problem != null || !DefinitionParser.IsValidFolderName(entry.Package))
                    return Fail(where + "package must be a lowercase package filename without .json", out error);
                object when;
                if (row.TryGetValue("when", out when))
                {
                    var clauses = when as Dictionary<string, object>;
                    if (clauses == null || clauses.Count == 0) return Fail(where + "when must be a nonempty condition object", out error);
                    var messages = new List<string>();
                    entry.Rules = ConditionParser.Parse(clauses, where, messages, false);
                    if (entry.Rules.UsesTone) return Fail(where + "tone is not supported in schedules", out error);
                    if (messages.Count > 0) return Fail(messages[0], out error);
                    if (definition != null)
                        foreach (var variables in entry.Rules.VariableConditions)
                            if ((problem = variables.CheckDefinition(definition)) != null) return Fail(where + "when: " + problem, out error);
                }
                object hours;
                if (row.TryGetValue("hours", out hours))
                {
                    List<object> pair = hours as List<object>;
                    if (pair == null || pair.Count != 2 || !Minute(pair[0], out entry.From) || !Minute(pair[1], out entry.To) || entry.From == entry.To)
                        return Fail(where + "hours must be two different HH:mm times (00:00 to 23:59); omit hours for all day", out error);
                    for (int minute = 0; minute < 1440; minute++)
                    {
                        if (!entry.Includes(minute)) continue;
                        if (entry.Conditional) continue;
                        if (occupied[minute]) return Fail(where + "hours overlap another timed entry", out error);
                        occupied[minute] = true;
                    }
                }
                else
                {
                    if (!entry.Conditional && fallback) return Fail(where + "only one unconditional entry may omit hours", out error);
                    if (!entry.Conditional) fallback = true;
                }
                if (row.ContainsKey("parameters"))
                {
                    entry.Parameters = row["parameters"] as Dictionary<string, object>;
                    if (entry.Parameters == null) return Fail(where + "parameters must be an object", out error);
                }
                result.Add(entry);
            }
            return result;
        }

        static List<AiScheduleEntry> Fail(string message, out string error)
        {
            error = message;
            return new List<AiScheduleEntry>();
        }

        static bool Minute(object raw, out int value)
        {
            value = 0;
            string s = raw as string;
            if (s == null || s.Length != 5 || s[2] != ':') return false;
            if (s[0] < '0' || s[0] > '9' || s[1] < '0' || s[1] > '9' ||
                s[3] < '0' || s[3] > '9' || s[4] < '0' || s[4] > '9') return false;
            int hour = (s[0] - '0') * 10 + s[1] - '0';
            int minute = (s[3] - '0') * 10 + s[4] - '0';
            value = hour * 60 + minute;
            return hour < 24 && minute < 60;
        }

        public static AiScheduleEntry Select(IList<AiScheduleEntry> entries, int minute) { return Select(entries, minute, null); }

        public static AiScheduleEntry Select(IList<AiScheduleEntry> entries, int minute, INpcVariableFacts facts)
        {
            if (entries == null || minute < 0 || minute >= 1440) return null;
            foreach (AiScheduleEntry entry in entries)
                if (entry.Conditional && entry.Includes(minute) &&
                    (entry.When == null || entry.When.Holds(facts)) &&
                    (entry.Rules == null || (facts is IDialogueFacts && entry.Rules.Holds((IDialogueFacts)facts)))) return entry;
            AiScheduleEntry fallback = null;
            foreach (AiScheduleEntry entry in entries)
            {
                if (entry.Conditional) continue;
                if (entry.From < 0) fallback = entry;
                else if (entry.Includes(minute)) return entry;
            }
            return fallback;
        }

        public static string Describe(IList<AiScheduleEntry> entries, int minute, INpcVariableFacts facts)
        {
            if (minute < 0 || minute >= 1440) return "Invalid game minute.";
            if (entries == null || entries.Count == 0) return "No schedule; using current AI.";
            AiScheduleEntry selected = Select(entries, minute, facts);
            StringBuilder text = new StringBuilder();
            for (int i = 0; i < entries.Count; i++)
            {
                AiScheduleEntry entry = entries[i];
                string reason = null;
                if (!entry.Includes(minute)) reason = "outside hours";
                else if (entry.When != null && !entry.When.Holds(facts)) reason = "condition unmet: variables";
                else if (entry.Rules != null)
                {
                    IDialogueFacts game = facts as IDialogueFacts;
                    string clause = game != null ? entry.Rules.FirstFailing(game) : "game facts unavailable";
                    if (clause != null) reason = "condition unmet: " + clause;
                }
                if (reason == null)
                    reason = entry == selected ? "selected" : "eligible; overridden by slot " + (entries.IndexOf(selected) + 1);
                text.Append("  slot ").Append(i + 1).Append(": ").Append(entry.Package).Append(" - ").Append(reason).Append('\n');
            }
            if (selected == null) text.Append("  No eligible entry; using current AI.\n");
            return text.ToString().TrimEnd();
        }

        /// <summary>Only replace the schedule after all entries and package references pass validation.</summary>
        public static bool TryReload(NpcDefinition definition, string json, Func<string, AiPackage> find, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(json)) { error = "empty npc.json"; return false; }
            Dictionary<string, object> root;
            try
            {
                List<string> duplicates = new List<string>();
                root = Json.Parse(json, duplicates) as Dictionary<string, object>;
                if (duplicates.Count > 0) { error = "duplicate field " + duplicates[0]; return false; }
            }
            catch (JsonException e) { error = e.Message; return false; }
            if (root == null) { error = "npc.json must be an object"; return false; }
            object raw;
            root.TryGetValue("schedule", out raw);
            List<AiScheduleEntry> candidate = Read(raw, out error, definition);
            if (error != null) return false;
            for (int i = 0; i < candidate.Count; i++)
            {
                AiScheduleEntry entry = candidate[i];
                if (PackagePlan.Build(find(entry.Package), entry.Parameters, find, out error) == null)
                {
                    error = "entry " + (i + 1) + " (" + entry.Package + "): " + error;
                    return false;
                }
            }
            definition.Schedule = candidate;
            return true;
        }

        /// <summary>Identifies this day's occurrence, including the previous evening of a midnight slot.</summary>
        public static long Occurrence(AiScheduleEntry entry, ulong seconds)
        {
            long day = (long)(seconds / 86400);
            int minute = (int)(seconds % 86400 / 60);
            return entry.From > entry.To && minute < entry.To ? day - 1 : day;
        }
    }
}

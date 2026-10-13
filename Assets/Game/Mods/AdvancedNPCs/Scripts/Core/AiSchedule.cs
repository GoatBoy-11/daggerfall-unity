using System;
using System.Collections.Generic;

namespace AdvancedNPCs.Core
{
    public class AiScheduleEntry
    {
        public string Package;
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
        public static readonly string[] Keys = { "hours", "package", "parameters" };

        public static List<AiScheduleEntry> Read(object raw, out string error)
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
                object hours;
                if (row.TryGetValue("hours", out hours))
                {
                    List<object> pair = hours as List<object>;
                    if (pair == null || pair.Count != 2 || !Minute(pair[0], out entry.From) || !Minute(pair[1], out entry.To) || entry.From == entry.To)
                        return Fail(where + "hours must be two different HH:mm times (00:00 to 23:59); omit hours for all day", out error);
                    for (int minute = 0; minute < 1440; minute++)
                    {
                        if (!entry.Includes(minute)) continue;
                        if (occupied[minute]) return Fail(where + "hours overlap another timed entry", out error);
                        occupied[minute] = true;
                    }
                }
                else
                {
                    if (fallback) return Fail(where + "only one entry may omit hours", out error);
                    fallback = true;
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

        public static AiScheduleEntry Select(IList<AiScheduleEntry> entries, int minute)
        {
            if (entries == null || minute < 0 || minute >= 1440) return null;
            AiScheduleEntry fallback = null;
            foreach (AiScheduleEntry entry in entries)
            {
                if (entry.From < 0) fallback = entry;
                else if (entry.Includes(minute)) return entry;
            }
            return fallback;
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

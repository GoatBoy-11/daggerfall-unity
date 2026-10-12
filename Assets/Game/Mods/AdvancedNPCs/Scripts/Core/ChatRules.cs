using System;
using System.Collections.Generic;

namespace AdvancedNPCs.Core
{
    /// <summary>npc.json "conversation" (spec conversations §1).</summary>
    public class ConversationSettings
    {
        public bool Enabled;
        /// <summary>Percent chance of a conversation after greeting another ANPC.</summary>
        public int Chance = 50;
    }

    /// <summary>ANPC conversations (spec conversations): limits, rolls and who may talk. No Unity or DFU types.</summary>
    public static class ChatRules
    {
        public const int MinLines = 4, MaxLines = 8;
        public const double MinPause = 0.4, MaxPause = 1.0;
        public const double CooldownHours = 2.0;
        /// <summary>Metres apart at which a conversation ends (knocked back, teleported).</summary>
        public const float MaxApart = 6f;
        /// <summary>anpc_chat: the partner must be this close (the greeting distance).</summary>
        public const float StartDistance = GreetRules.Distance;

        static readonly string[] Keys = { "enabled", "chance" };

        /// <summary>
        /// Reads "conversation" from an npc.json root: missing, null or false = off; true = on; an object = on unless
        /// "enabled": false. Null, or a problem with its key ("conversation.chance"). Unknown keys only warn.
        /// </summary>
        public static string Read(Dictionary<string, object> root, out string key, out ConversationSettings settings, List<string> warnings)
        {
            settings = new ConversationSettings();
            key = "conversation";
            object raw;
            if (!root.TryGetValue("conversation", out raw) || raw == null)
                return null;
            if (raw is bool)
            {
                settings.Enabled = (bool)raw;
                return null;
            }
            Dictionary<string, object> o = raw as Dictionary<string, object>;
            if (o == null)
                return "must be true, false or an object like { \"enabled\": true, \"chance\": 50 }";
            foreach (string k in FieldReader.UnknownKeys(o, Keys))
                warnings.Add("conversation." + k + ": unknown setting (use " + string.Join(", ", Keys) + "), ignored");
            bool enabled;
            if (FieldReader.Bool(o, "enabled", true, out enabled) != null || (o.ContainsKey("enabled") && o["enabled"] == null))
            {
                key = "conversation.enabled";
                return "must be true or false";
            }
            double chance;
            if (FieldReader.Number(o, "chance", 50, out chance) != null || (o.ContainsKey("chance") && o["chance"] == null) ||
                double.IsNaN(chance) || chance < 0 || chance > 100 || chance != Math.Floor(chance))
            {
                key = "conversation.chance";
                return "must be a whole number from 0 to 100";
            }
            settings.Enabled = enabled;
            settings.Chance = (int)chance;
            return null;
        }

        /// <summary>Lines in a conversation for a roll in [0, 1] (Unity's Random.value includes 1).</summary>
        public static int Lines(double roll01)
        {
            return Math.Min(MaxLines, MinLines + (int)(roll01 * (MaxLines - MinLines + 1)));
        }

        /// <summary>Seconds of silence after a line, for a roll in [0, 1].</summary>
        public static double Pause(double roll01)
        {
            return MinPause + (MaxPause - MinPause) * Math.Max(0.0, Math.Min(1.0, roll01));
        }

        /// <summary>The same key for A–B and B–A.</summary>
        public static string PairKey(string a, string b)
        {
            return string.CompareOrdinal(a, b) <= 0 ? a + "|" + b : b + "|" + a;
        }

        /// <summary>
        /// A clip index out of count, never last when there are two or more (a last outside the pool, after a reload,
        /// counts as none); -1 with no clips.
        /// </summary>
        public static int NextClip(int count, int last, double roll01)
        {
            if (count <= 0)
                return -1;
            if (count == 1)
                return 0;
            if (last < 0 || last >= count)
                return Math.Min(count - 1, (int)(roll01 * count));
            int i = Math.Min(count - 2, (int)(roll01 * (count - 1)));
            return i >= last ? i + 1 : i;
        }

        /// <summary>Why an ANPC cannot join a conversation now, or null (spec conversations §1).</summary>
        public static string Ineligible(bool optedIn, bool alreadyTalking, bool calm, bool customVoice, int talkClips, bool customSprite)
        {
            if (!optedIn)
                return "not opted in";
            if (alreadyTalking)
                return "already talking";
            if (!calm)
                return "not calm";
            if (!customVoice)
                return "voices not ANPC";
            if (talkClips <= 0)
                return "no talk sounds";
            if (!customSprite)
                return "vanilla sprite";
            return null;
        }
    }

    /// <summary>When each pair last started talking (game hours). Kept in memory only, shared by all ANPCs.</summary>
    public class ChatMemory
    {
        readonly Dictionary<string, double> last = new Dictionary<string, double>(StringComparer.Ordinal);

        /// <summary>The pair may talk: never did, or 2 game hours ago, or the clock went back (an earlier save).</summary>
        public bool CanStart(string pair, double nowHours)
        {
            double at;
            return !last.TryGetValue(pair, out at) || nowHours < at || nowHours - at >= ChatRules.CooldownHours;
        }

        public void Started(string pair, double nowHours)
        {
            last[pair] = nowHours;
        }

        public void Clear()
        {
            last.Clear();
        }
    }

    /// <summary>
    /// One conversation's turns (spec conversations §2): the speaker starts a line (NeedsLine, then BeginLine), the
    /// line plays, a pause follows, the other speaks. Over after the last line, when nothing could be played, or on End.
    /// </summary>
    public class ChatSession
    {
        readonly int lines;
        int line = 1;
        bool needsLine = true;
        bool pausing;
        bool over;
        double left;
        /// <summary>Time left below this counts as none (sums of frame times are not exact).</summary>
        const double Epsilon = 1e-6;

        public ChatSession(int lines)
        {
            this.lines = Math.Max(1, lines);
        }

        public int Lines
        {
            get { return lines; }
        }

        /// <summary>The line being spoken or about to be (from 1).</summary>
        public int Line
        {
            get { return line; }
        }

        /// <summary>0: the one who started; 1: the partner.</summary>
        public int Speaker
        {
            get { return (line - 1) % 2; }
        }

        /// <summary>The speaker should start its line now.</summary>
        public bool NeedsLine
        {
            get { return needsLine && !over; }
        }

        /// <summary>A line is playing (not the pause after it).</summary>
        public bool Speaking
        {
            get { return !over && !needsLine && !pausing; }
        }

        public bool Over
        {
            get { return over; }
        }

        /// <summary>The speaker started a clip this long; 0 or less: nothing played, the conversation is over.</summary>
        public void BeginLine(double seconds)
        {
            if (!NeedsLine)
                return;
            if (seconds <= 0)
            {
                End();
                return;
            }
            needsLine = false;
            pausing = false;
            left = seconds;
        }

        /// <summary>Counts the line down, then the pause; time left over from one carries into the next.</summary>
        public void Tick(double dt, double pauseRoll01)
        {
            if (over || needsLine)
                return;
            left -= dt;
            if (left > Epsilon)
                return;
            if (!pausing)
            {
                if (line >= lines)
                {
                    over = true;
                    return;
                }
                pausing = true;
                left += ChatRules.Pause(pauseRoll01);
                if (left > Epsilon)
                    return;
            }
            pausing = false;
            line++;
            needsLine = true;
        }

        public void End()
        {
            over = true;
            needsLine = false;
        }
    }
}

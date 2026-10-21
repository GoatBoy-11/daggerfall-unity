using System;
using System.Collections.Generic;

namespace AdvancedNPCs.Core
{
    /// <summary>How an animal treats the player (spec animal-ai §2).</summary>
    public static class Temperaments
    {
        public const string Prey = "prey", Grazer = "grazer", Territorial = "territorial", Predator = "predator",
            Ambusher = "ambusher", Ambient = "ambient";
        public static readonly string[] All = { Prey, Grazer, Territorial, Predator, Ambusher, Ambient };

        /// <summary>Fights back when hit (the others flee).</summary>
        public static bool Fights(string temperament)
        {
            return temperament == Territorial || temperament == Predator || temperament == Ambusher;
        }
    }

    /// <summary>When an animal is up and about (spec animal-ai §2.2); outside those hours it rests.</summary>
    public static class Activities
    {
        public const string Diurnal = "diurnal", Nocturnal = "nocturnal", Crepuscular = "crepuscular", Always = "always";
        public static readonly string[] All = { Diurnal, Nocturnal, Crepuscular, Always };

        /// <summary>Awake at this hour (0-23).</summary>
        public static bool Active(string activity, int hour)
        {
            switch (activity)
            {
                case Diurnal: return hour >= 6 && hour < 20;
                case Nocturnal: return hour >= 20 || hour < 6;
                case Crepuscular: return (hour >= 4 && hour < 9) || (hour >= 17 && hour < 22);
                default: return true;
            }
        }
    }

    /// <summary>
    /// What an animal is doing about the player. Ints, not an enum: DFU's runtime compiler cannot load a class whose
    /// enum field type is declared in a file it compiles later.
    /// </summary>
    public static class AnimalStates
    {
        public const int Unaware = 0, Alert = 1, Warning = 2, Flee = 3, Shy = 4, Attack = 5;

        public static string Name(int state)
        {
            switch (state)
            {
                case Alert: return "alert";
                case Warning: return "warning";
                case Flee: return "fleeing";
                case Shy: return "shying away";
                case Attack: return "attacking";
                default: return "unaware";
            }
        }
    }

    /// <summary>npc.json "animal" (spec animal-ai §1.5, §2).</summary>
    public class AnimalSettings
    {
        public string Temperament = Temperaments.Grazer;
        public string Activity = Activities.Always;
        /// <summary>Metres at which it notices a visible player (40 % of it for one it only hears).</summary>
        public float Alert;
        /// <summary>Metres at which it acts: flees, warns, attacks or shies away.</summary>
        public float React;
        /// <summary>Metres from home a chase may go before it gives up and goes home; 0 = no limit but losing the player.</summary>
        public float Territory;
        public float WarnSeconds = 3f;
        /// <summary>Predators: % chance to attack a player it has just noticed outside react range.</summary>
        public int BoldChance;
        /// <summary>Seconds between calls (moo, howl), min to max; 0 = never calls.</summary>
        public float CallMin, CallMax;
        /// <summary>Temperaments ("prey") or template ids ("an_hind", "an_rabbit_*") a predator hunts; empty = none.</summary>
        public readonly List<string> Hunts = new List<string>();
        /// <summary>Game hours between hunts (hunger), min to max.</summary>
        public float HungerMinHours = 4f, HungerMaxHours = 10f;
        /// <summary>Members of a spawn group keep near their leader.</summary>
        public bool Herd = true;
    }

    /// <summary>Animal rules (spec animal-ai): parsing, senses, reactions, leash, hunting. No Unity or DFU types.</summary>
    public static class AnimalRules
    {
        /// <summary>Of the alert distance, how far an animal hears a player it cannot see.</summary>
        public const float HearShare = 0.4f;
        /// <summary>The player must be this much past alert range before an alert animal relaxes.</summary>
        public const float RelaxFactor = 1.25f;
        /// <summary>Seconds a grazer looks at the player before it goes back to grazing.</summary>
        public const float LookSeconds = 6f;
        public const float MinAlert = 3f, MinReact = 1.5f;
        /// <summary>Same-group members within this many metres take up an alarm.</summary>
        public const float GroupAlarmRange = 30f;
        /// <summary>Prey and grazers of any kind within this many metres flee with a fleeing animal.</summary>
        public const float CrossAlarmRange = 20f;
        /// <summary>A fleeing animal stops once this far from the threat (or out of sight and twice react range away).</summary>
        public const float FleeDistance = 35f;
        public const float UnseenSeconds = 4f;
        /// <summary>Farthest a group spawns around its first member.</summary>
        public const float GroupSpread = 6f;
        public const int MaxGroup = 8;

        /// <summary>Creature classes an animal may use while calm: no were-creatures, undead or daedra.</summary>
        public static readonly string[] SafeCreatures = { "Rat", "GiantBat", "GrizzlyBear", "SabertoothTiger", "Spider", "GiantScorpion" };

        static readonly string[] Keys = { "temperament", "activity", "senses", "territory", "warnSeconds", "boldChance", "calls", "hunts", "hungerHours", "herd" };
        static readonly string[] SenseKeys = { "alert", "react" };

        /// <summary>Defaults for a temperament: alert, react, territory (metres), bold chance, calls (seconds).</summary>
        public static void Defaults(AnimalSettings s)
        {
            switch (s.Temperament)
            {
                case Temperaments.Prey: s.Alert = 25; s.React = 15; s.Territory = 0; break;
                case Temperaments.Territorial: s.Alert = 18; s.React = 8; s.Territory = 25; break;
                case Temperaments.Predator: s.Alert = 30; s.React = 10; s.Territory = 40; s.BoldChance = 10; break;
                case Temperaments.Ambusher: s.Alert = 6; s.React = 3; s.Territory = 8; break;
                case Temperaments.Ambient: s.Alert = 6; s.React = 3; s.Territory = 0; break;
                default: s.Alert = 10; s.React = 4; s.Territory = 0; break;
            }
            s.CallMin = s.Temperament == Temperaments.Ambusher ? 0 : 45;
            s.CallMax = s.Temperament == Temperaments.Ambusher ? 0 : 150;
        }

        /// <summary>
        /// Reads "animal" from an npc.json root: missing, null or false = not an animal (settings null); true = a grazer
        /// with defaults; an object sets fields. Null, or a problem with its key ("animal.senses.alert"). Unknown keys warn.
        /// </summary>
        public static string Read(Dictionary<string, object> root, out string key, out AnimalSettings settings, List<string> warnings)
        {
            settings = null;
            key = "animal";
            object raw;
            if (!root.TryGetValue("animal", out raw) || raw == null || (raw is bool && !(bool)raw))
                return null;
            AnimalSettings s = new AnimalSettings();
            if (raw is bool)
            {
                Defaults(s);
                settings = s;
                return null;
            }
            Dictionary<string, object> o = raw as Dictionary<string, object>;
            if (o == null)
                return "must be true or an object like { \"temperament\": \"prey\" }";
            foreach (string k in FieldReader.UnknownKeys(o, Keys))
            {
                string hint = Typos.Closest(k, Keys);
                warnings.Add("animal." + k + ": unknown field" + (hint != null ? " (did you mean \"" + hint + "\"?)" : "") + ", ignored");
            }

            string problem;
            string temperament;
            if ((problem = FieldReader.Text(o, "temperament", Temperaments.Grazer, out temperament)) != null)
            { key = "animal.temperament"; return problem; }
            s.Temperament = temperament.Trim().ToLowerInvariant();
            if (Array.IndexOf(Temperaments.All, s.Temperament) < 0)
            { key = "animal.temperament"; return "must be one of " + string.Join(", ", Temperaments.All) + " (got \"" + temperament + "\")"; }
            Defaults(s);

            string activity;
            if ((problem = FieldReader.Text(o, "activity", Activities.Always, out activity)) != null)
            { key = "animal.activity"; return problem; }
            s.Activity = activity.Trim().ToLowerInvariant();
            if (Array.IndexOf(Activities.All, s.Activity) < 0)
            { key = "animal.activity"; return "must be one of " + string.Join(", ", Activities.All) + " (got \"" + activity + "\")"; }

            object senses;
            if (o.TryGetValue("senses", out senses) && senses != null)
            {
                Dictionary<string, object> so = senses as Dictionary<string, object>;
                if (so == null)
                { key = "animal.senses"; return "must be an object like { \"alert\": 25, \"react\": 15 }"; }
                foreach (string k in FieldReader.UnknownKeys(so, SenseKeys))
                    warnings.Add("animal.senses." + k + ": unknown field (use alert, react), ignored");
                double alert, react;
                if ((problem = Metres(so, "alert", s.Alert, 1, 100, out alert)) != null) { key = "animal.senses.alert"; return problem; }
                if ((problem = Metres(so, "react", Math.Min(s.React, alert), 0.5, 100, out react)) != null) { key = "animal.senses.react"; return problem; }
                if (react > alert)
                { key = "animal.senses.react"; return "must not be more than alert (" + FieldReader.Num(alert) + ")"; }
                s.Alert = (float)alert;
                s.React = (float)react;
            }

            double territory;
            if ((problem = Metres(o, "territory", s.Territory, 0, 200, out territory)) != null) { key = "animal.territory"; return problem; }
            s.Territory = (float)territory;
            double warn;
            if ((problem = Metres(o, "warnSeconds", s.WarnSeconds, 0, 30, out warn)) != null) { key = "animal.warnSeconds"; return problem; }
            s.WarnSeconds = (float)warn;
            double bold;
            if ((problem = FieldReader.Number(o, "boldChance", s.BoldChance, out bold)) != null || (o.ContainsKey("boldChance") && o["boldChance"] == null) ||
                double.IsNaN(bold) || bold < 0 || bold > 100 || bold != Math.Floor(bold))
            { key = "animal.boldChance"; return "must be a whole number from 0 to 100"; }
            s.BoldChance = (int)bold;

            object calls;
            if (o.TryGetValue("calls", out calls))
            {
                if (calls == null || (calls is bool && !(bool)calls))
                {
                    s.CallMin = s.CallMax = 0;
                }
                else if (!(calls is bool))
                {
                    double[] range;
                    if (!FieldReader.Numbers(o, "calls", out range) || range == null || range.Length != 2 || !(range[0] >= 5) || range[0] > range[1] || range[1] > 3600)
                    { key = "animal.calls"; return "must be false or [min, max] seconds, 5 <= min <= max <= 3600"; }
                    s.CallMin = (float)range[0];
                    s.CallMax = (float)range[1];
                }
            }

            object hunts;
            if (o.TryGetValue("hunts", out hunts) && hunts != null)
            {
                List<object> list = hunts as List<object>;
                if (list == null && hunts is string)
                    list = new List<object> { hunts };
                if (list == null)
                { key = "animal.hunts"; return "must be a temperament, an id or a list of them"; }
                foreach (object item in list)
                {
                    string h = item as string;
                    if (h == null || h.Trim().Length == 0)
                    { key = "animal.hunts"; return "must be a temperament, an id or a list of them"; }
                    s.Hunts.Add(h.Trim().ToLowerInvariant());
                }
            }

            double[] hunger;
            if (!FieldReader.Numbers(o, "hungerHours", out hunger) || (hunger != null && (hunger.Length != 2 || !(hunger[0] > 0) || hunger[0] > hunger[1] || hunger[1] > 240)))
            { key = "animal.hungerHours"; return "must be [min, max] game hours, 0 < min <= max <= 240"; }
            if (hunger != null)
            {
                s.HungerMinHours = (float)hunger[0];
                s.HungerMaxHours = (float)hunger[1];
            }
            bool herd;
            if ((problem = FieldReader.Bool(o, "herd", true, out herd)) != null) { key = "animal.herd"; return problem; }
            s.Herd = herd;
            settings = s;
            return null;
        }

        static string Metres(Dictionary<string, object> o, string name, double fallback, double min, double max, out double value)
        {
            string problem = FieldReader.Number(o, name, fallback, out value);
            if (problem != null || (o.ContainsKey(name) && o[name] == null) || double.IsNaN(value) || value < min || value > max)
                return "must be a number from " + FieldReader.Num(min) + " to " + FieldReader.Num(max);
            return null;
        }

        /// <summary>An animal's bravery: fighters stand (and flee when badly hurt), the rest run.</summary>
        public static Bravery BraveryOf(string temperament)
        {
            return Temperaments.Fights(temperament) ? Bravery.Prudent : Bravery.Coward;
        }

        /// <summary>
        /// A sense distance for the player's way of moving: sneaking (crouched or slow) halves it, running adds half;
        /// Stealth (0-100) takes up to half off; a resting animal notices at half, a day animal at night at 0.7;
        /// a magically concealed player at a quarter. Never below min.
        /// </summary>
        public static float Sense(float metres, bool sneaking, bool running, int stealth, bool resting, bool dozy, bool concealed, float min)
        {
            float f = metres;
            if (sneaking) f *= 0.5f;
            else if (running) f *= 1.5f;
            f *= 1f - Math.Max(0, Math.Min(100, stealth)) / 200f;
            if (resting) f *= 0.5f;
            if (dozy) f *= 0.7f;
            if (concealed) f *= 0.25f;
            return Math.Max(min, f);
        }

        /// <summary>Within alert range in sight, or within HearShare of it unseen.</summary>
        public static bool Notices(float distance, float alert, bool seen)
        {
            return distance <= (seen ? alert : alert * HearShare);
        }

        /// <summary>
        /// The next state of a calm animal (Unaware, Alert or Warning) for the player now. Flee, Shy and Attack are
        /// actions the caller carries out. stateSeconds: time in the current state; bold: the predator's roll on noticing;
        /// habituated: a grazer that has looked already and the player has not left since.
        /// </summary>
        public static int Next(AnimalSettings s, int state, float distance, float alert, float react, bool noticed,
            float stateSeconds, bool bold, bool habituated)
        {
            // Inside react range it acts when it sees the player, or hears one this close.
            bool close = distance <= react && (noticed || distance <= Math.Max(react, alert * HearShare));
            string t = s.Temperament;
            if (state == AnimalStates.Warning)
            {
                if (distance <= react * 0.5f) return AnimalStates.Attack;
                if (stateSeconds >= s.WarnSeconds)
                    return distance <= react ? AnimalStates.Attack : distance <= alert ? AnimalStates.Alert : AnimalStates.Unaware;
                return distance > alert * RelaxFactor ? AnimalStates.Unaware : AnimalStates.Warning;
            }
            if (close)
            {
                switch (t)
                {
                    case Temperaments.Prey: return AnimalStates.Flee;
                    case Temperaments.Grazer:
                    case Temperaments.Ambient: return AnimalStates.Shy;
                    case Temperaments.Territorial: return AnimalStates.Warning;
                    default: return AnimalStates.Attack;      // predator, ambusher
                }
            }
            if (state == AnimalStates.Alert)
            {
                if (distance > alert * RelaxFactor && stateSeconds > 2f) return AnimalStates.Unaware;
                if (t == Temperaments.Grazer && stateSeconds >= LookSeconds) return AnimalStates.Unaware;
                return AnimalStates.Alert;
            }
            // Unaware
            if (!noticed || t == Temperaments.Ambusher || t == Temperaments.Ambient)
                return AnimalStates.Unaware;
            if (t == Temperaments.Grazer && habituated)
                return AnimalStates.Unaware;
            if (t == Temperaments.Predator && bold)
                return AnimalStates.Attack;
            return AnimalStates.Alert;
        }

        /// <summary>A flight is over: far enough, or out of sight long enough at twice react range (Shy: twice react range).</summary>
        public static bool FleeDone(int state, float distance, float react, bool seen, float unseenSeconds)
        {
            if (state == AnimalStates.Shy)
                return distance >= Math.Max(6f, react * 2f);
            return distance >= FleeDistance || (!seen && unseenSeconds >= UnseenSeconds && distance >= react * 2f);
        }

        /// <summary>A chase gives up: too far from home, or the target is far away.</summary>
        public static bool GivesUp(float fromHome, float territory, float toTarget, float alert)
        {
            if (territory > 0 && fromHome > territory)
                return true;
            return toTarget > Math.Max(alert * 2f, territory + 10f);
        }

        /// <summary>A predator's hunts list takes a prey: by its temperament, its template id, or an id prefix ending in *.</summary>
        public static bool Hunts(AnimalSettings predator, string preyId, string preyTemperament)
        {
            if (predator == null || preyId == null)
                return false;
            string id = preyId.ToLowerInvariant();
            foreach (string h in predator.Hunts)
            {
                if (h == preyTemperament || h == id)
                    return true;
                if (h.EndsWith("*", StringComparison.Ordinal) && id.StartsWith(h.Substring(0, h.Length - 1), StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        /// <summary>Game seconds until the next hunt for a roll in [0, 1].</summary>
        public static double HungerSeconds(AnimalSettings s, double roll01)
        {
            double hours = s.HungerMinHours + (s.HungerMaxHours - s.HungerMinHours) * Math.Max(0.0, Math.Min(1.0, roll01));
            return hours * 3600.0;
        }

        /// <summary>Seconds until the next call for a roll in [0, 1]; 0 = never.</summary>
        public static float CallSeconds(AnimalSettings s, double roll01)
        {
            if (s.CallMax <= 0)
                return 0f;
            return (float)(s.CallMin + (s.CallMax - s.CallMin) * Math.Max(0.0, Math.Min(1.0, roll01)));
        }
    }

    /// <summary>spawn.wilderness "terrain": the ground tile a wilderness spawn needs (spec animal-ai §1.3).</summary>
    public static class TerrainRules
    {
        public const string Dry = "dry", Grass = "grass", Rocky = "rocky", NearWater = "nearwater";
        public static readonly string[] All = { Dry, Grass, Rocky, NearWater };
        public static readonly string[] Display = { "dry", "grass", "rocky", "nearWater" };
        /// <summary>Tilemap samples around the spot searched for water (one sample is about 6 m).</summary>
        public const int ShoreSamples = 4;

        /// <summary>"nearWater" / "near_water" / "Near Water" -> "nearwater"; null if unknown.</summary>
        public static string Canonical(string raw)
        {
            if (raw == null)
                return null;
            string k = raw.Trim().ToLowerInvariant().Replace(" ", "").Replace("_", "");
            return Array.IndexOf(All, k) >= 0 ? k : null;
        }

        /// <summary>DFU terrain tile (tilemap value &amp; 0x3F): 0 water, 1 dirt, 2 grass, 3 stone, 4+ blends.</summary>
        public static bool Accepts(string terrain, int tile, bool waterNear)
        {
            bool dry = tile >= 1 && tile <= 3;
            switch (terrain)
            {
                case Grass: return tile == 2;
                case Rocky: return tile == 3;
                case NearWater: return dry && waterNear;
                default: return dry;
            }
        }

        /// <summary>
        /// How many of a group to spawn: a roll in [0, 1] between min and max, cut to the room left under the template's
        /// max and the overall cap; 0 when there is no room for min.
        /// </summary>
        public static int GroupSize(int min, int max, double roll01, int roomTemplate, int roomCap)
        {
            int n = min + (int)(Math.Max(0.0, Math.Min(0.999999, roll01)) * (max - min + 1));
            n = Math.Min(n, Math.Min(roomTemplate, roomCap));
            return n >= min ? n : 0;
        }
    }
}

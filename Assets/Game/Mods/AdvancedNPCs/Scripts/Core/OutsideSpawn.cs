using System;
using System.Collections.Generic;

namespace AdvancedNPCs.Core
{
    /// <summary>spawn.dungeons (outside-towns spec §3).</summary>
    public class DungeonSpawn
    {
        public int Chance;
        public int CountMin = 1;
        public int CountMax = 1;
        /// <summary>Canonical DungeonTypeNames; empty = every dungeon.</summary>
        public readonly List<string> DungeonTypes = new List<string>();
        public Condition When;

        public bool Matches(string dungeonType)
        {
            return DungeonTypes.Count == 0 || DungeonTypes.Contains(dungeonType);
        }
    }

    /// <summary>spawn.interiors (outside-towns spec §3).</summary>
    public class InteriorSpawn
    {
        public int Chance;
        public int CountMin = 1;
        public int CountMax = 1;
        /// <summary>BuildingKinds keys.</summary>
        public readonly List<string> Buildings = new List<string>();
        public Condition When;

        /// <param name="building">The building's BuildingKinds key ("tavern", "guildhall", "house", ...).</param>
        /// <param name="guild">For guild halls, the guild's key ("fightersguild", ...), else null.</param>
        public bool Matches(string building, string guild)
        {
            if (Buildings.Contains(building))
                return true;
            return building == BuildingKinds.GuildHall && guild != null && Buildings.Contains(guild);
        }
    }

    /// <summary>spawn.wilderness (outside-towns spec §3).</summary>
    public class WildernessSpawn
    {
        public int Chance;
        public int Max = 1;
        public Condition When;
    }

    /// <summary>DFRegion.DungeonTypes names (copied: Core has no DFU references).</summary>
    public static class DungeonTypeNames
    {
        public static readonly string[] All =
        {
            "Crypt", "OrcStronghold", "HumanStronghold", "Prison", "DesecratedTemple", "Mine", "NaturalCave", "Coven",
            "VampireHaunt", "Laboratory", "HarpyNest", "RuinedCastle", "SpiderNest", "GiantStronghold", "DragonsDen",
            "BarbarianStronghold", "VolcanicCaves", "ScorpionNest", "Cemetery",
        };

        /// <summary>"human stronghold" / "Human_Stronghold" -> "HumanStronghold"; null if unknown.</summary>
        public static string Canonical(string raw)
        {
            string k = Squash(raw);
            foreach (string name in All)
            {
                if (name.ToLowerInvariant() == k)
                    return name;
            }
            return null;
        }

        public static string Squash(string raw)
        {
            return raw == null ? "" : raw.Trim().ToLowerInvariant().Replace(" ", "").Replace("_", "").Replace("'", "");
        }
    }

    /// <summary>Building and guild names authors may write in spawn.interiors.buildings, as lowercase keys.</summary>
    public static class BuildingKinds
    {
        public const string GuildHall = "guildhall";
        public const string House = "house";

        /// <summary>Display names, in the order shown in messages.</summary>
        public static readonly string[] Display =
        {
            "Alchemist", "Armorer", "Bank", "Bookseller", "ClothingStore", "FurnitureStore", "GemStore", "GeneralStore",
            "Library", "PawnShop", "WeaponSmith", "Temple", "Tavern", "Palace", "House", "GuildHall",
            "Fighters Guild", "Mages Guild", "Thieves Guild", "Dark Brotherhood", "Knightly Order",
        };

        /// <summary>"Fighters Guild" -> "fightersguild", "general store" -> "generalstore"; null if unknown.</summary>
        public static string Canonical(string raw)
        {
            string k = DungeonTypeNames.Squash(raw);
            foreach (string name in Display)
            {
                if (DungeonTypeNames.Squash(name) == k)
                    return k;
            }
            return null;
        }

        /// <summary>A DFLocation.BuildingTypes name as a key: House1..House6 -> "house", others lowercase.</summary>
        public static string FromBuildingType(string buildingType)
        {
            string k = DungeonTypeNames.Squash(buildingType);
            if (k.StartsWith(House, StringComparison.Ordinal) && k != "houseforsale")
                return House;
            return k;
        }
    }

    /// <summary>Reads spawn.dungeons / spawn.interiors / spawn.wilderness. A bad block is reported and skipped.</summary>
    public static class OutsideSpawnParser
    {
        public static readonly string[] BlockNames = { "dungeons", "interiors", "wilderness" };
        static readonly string[] DungeonKeys = { "chance", "count", "dungeonTypes", "when" };
        static readonly string[] InteriorKeys = { "chance", "count", "buildings", "when" };
        static readonly string[] WildernessKeys = { "chance", "max", "when" };

        public static DungeonSpawn Dungeons(string file, Dictionary<string, object> o, List<string> messages)
        {
            string at = file + ": spawn.dungeons";
            Unknown(at, o, DungeonKeys, messages);
            DungeonSpawn d = new DungeonSpawn();
            string field = "chance";
            string problem = Chance(o, out d.Chance);
            if (problem == null)
            {
                field = "count";
                problem = Count(o, out d.CountMin, out d.CountMax);
            }
            if (problem == null)
            {
                List<string> types;
                if (!Texts(o, "dungeonTypes", out types))
                {
                    field = "dungeonTypes";
                    problem = "must be a list of dungeon types";
                }
                else if (types != null)
                {
                    foreach (string t in types)
                    {
                        string canonical = DungeonTypeNames.Canonical(t);
                        if (canonical == null)
                        {
                            field = "dungeonTypes";
                            problem = "unknown dungeon type \"" + t + "\" (use " + string.Join(", ", DungeonTypeNames.All) + ")";
                            break;
                        }
                        if (!d.DungeonTypes.Contains(canonical))
                            d.DungeonTypes.Add(canonical);
                    }
                }
            }
            if (problem != null)
                return Skip<DungeonSpawn>(at, field, problem, "dungeons", messages);
            d.When = When(o, at, messages);
            return d;
        }

        public static InteriorSpawn Interiors(string file, Dictionary<string, object> o, List<string> messages)
        {
            string at = file + ": spawn.interiors";
            Unknown(at, o, InteriorKeys, messages);
            InteriorSpawn s = new InteriorSpawn();
            string field = "chance";
            string problem = Chance(o, out s.Chance);
            if (problem == null)
            {
                field = "count";
                problem = Count(o, out s.CountMin, out s.CountMax);
            }
            if (problem == null)
            {
                List<string> names;
                if (!Texts(o, "buildings", out names) || names == null)
                {
                    field = "buildings";
                    problem = names == null && o.ContainsKey("buildings") ? "must be a list of building names" : "required";
                }
                else
                {
                    foreach (string n in names)
                    {
                        string key = BuildingKinds.Canonical(n);
                        if (key == null)
                        {
                            field = "buildings";
                            problem = "unknown building \"" + n + "\" (use " + string.Join(", ", BuildingKinds.Display) + ")";
                            break;
                        }
                        if (!s.Buildings.Contains(key))
                            s.Buildings.Add(key);
                    }
                }
            }
            if (problem != null)
                return Skip<InteriorSpawn>(at, field, problem, "interiors", messages);
            s.When = When(o, at, messages);
            return s;
        }

        public static WildernessSpawn Wilderness(string file, Dictionary<string, object> o, List<string> messages)
        {
            string at = file + ": spawn.wilderness";
            Unknown(at, o, WildernessKeys, messages);
            WildernessSpawn w = new WildernessSpawn();
            string problem = Chance(o, out w.Chance);
            string field = "chance";
            if (problem == null)
            {
                object raw;
                if (o.TryGetValue("max", out raw) && raw != null)
                {
                    if (!(raw is double) || (double)raw != Math.Floor((double)raw) || (double)raw < 1 || (double)raw > 10)
                    {
                        field = "max";
                        problem = "must be a whole number 1-10";
                    }
                    else
                    {
                        w.Max = (int)(double)raw;
                    }
                }
            }
            if (problem != null)
                return Skip<WildernessSpawn>(at, field, problem, "wilderness", messages);
            w.When = When(o, at, messages);
            return w;
        }

        static T Skip<T>(string at, string field, string problem, string block, List<string> messages) where T : class
        {
            messages.Add(at + "." + field + ": " + problem + ", " + block + " ignored");
            return null;
        }

        static string Chance(Dictionary<string, object> o, out int chance)
        {
            chance = 0;
            object raw;
            if (!o.TryGetValue("chance", out raw) || raw == null)
                return "required";
            if (!(raw is double) || (double)raw != Math.Floor((double)raw) || (double)raw < 0 || (double)raw > 100)
                return "must be a whole number 0-100";
            chance = (int)(double)raw;
            return null;
        }

        static string Count(Dictionary<string, object> o, out int min, out int max)
        {
            min = 1;
            max = 1;
            double[] count;
            if (!FieldReader.Numbers(o, "count", out count) || (count != null && count.Length != 2))
                return "must be [min, max]";
            if (count == null)
                return null;
            if (count[0] != Math.Floor(count[0]) || count[1] != Math.Floor(count[1]) || count[0] < 1 || count[0] > count[1] || count[1] > 10)
                return "need whole numbers 1 <= min <= max <= 10 (got [" + FieldReader.Num(count[0]) + ", " + FieldReader.Num(count[1]) + "])";
            min = (int)count[0];
            max = (int)count[1];
            return null;
        }

        static Condition When(Dictionary<string, object> o, string at, List<string> messages)
        {
            object raw;
            if (!o.TryGetValue("when", out raw) || raw == null)
                return null;
            Dictionary<string, object> when = raw as Dictionary<string, object>;
            if (when == null)
            {
                messages.Add(at + ".when: must be an object, ignored");
                return null;
            }
            return ConditionParser.Parse(when, at, messages, false, false);
        }

        /// <summary>A text or a list of texts; values is null when the key is missing. False when present but not texts.</summary>
        static bool Texts(Dictionary<string, object> o, string key, out List<string> values)
        {
            values = null;
            object raw;
            if (!o.TryGetValue(key, out raw) || raw == null)
                return true;
            List<object> list = raw as List<object>;
            if (list == null)
            {
                string single = raw as string;
                if (single == null)
                    return false;
                list = new List<object> { single };
            }
            values = new List<string>();
            foreach (object item in list)
            {
                string s = item as string;
                if (s == null || s.Trim().Length == 0)
                {
                    values = null;
                    return false;
                }
                values.Add(s);
            }
            return true;
        }

        static void Unknown(string at, Dictionary<string, object> o, string[] known, List<string> messages)
        {
            foreach (string key in FieldReader.UnknownKeys(o, known))
            {
                string hint = Typos.Closest(key, known);
                messages.Add(at + "." + key + ": unknown field" + (hint != null ? " (did you mean \"" + hint + "\"?)" : "") + ", ignored");
            }
        }
    }
}

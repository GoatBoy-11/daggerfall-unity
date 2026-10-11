using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AdvancedNPCs.Core
{
    public class ParseResult
    {
        public NpcDefinition Definition;
        public string Error;
        public readonly List<string> Warnings = new List<string>();

        public bool Ok
        {
            get { return Definition != null; }
        }
    }

    /// <summary>
    /// Turns one ANPC folder's npc.json (or, for migration, a 1a flat definition file) into a validated
    /// NpcDefinition or a single error line, plus warnings.
    /// </summary>
    public static class DefinitionParser
    {
        static readonly Regex IdPattern = new Regex("^[a-z0-9_]+$");

        static readonly string[] SharedKeys =
        {
            "id", "kind", "race", "baseClass", "gender", "bravery", "fleeHealthPercent", "calmDownHours",
            "crimeOnAttack", "wanderRadius", "nameList", "spriteHeight", "spriteHeightScale", "attitude", "hostileHours", "dialogue", "magic",
            "combat",
        };
        static readonly string[] UniqueKeys = { "name", "location", "position", "portrait" };
        static readonly string[] GenericKeys = { "name", "names", "portrait", "portraits", "spawn" };
        static readonly string[] GenericOnlyKeys = { "names", "portraits", "spawn" };
        static readonly string[] RaceNames = { "Breton", "Redguard", "Nord", "DarkElf" };
        static readonly string[] SpawnKeys = { "locationTypes", "places", "count", "dungeons", "interiors", "wilderness" };
        static readonly string[] PlaceKeys = { "region", "place", "positions" };

        /// <summary>A 1a flat file from StreamingAssets/AdvancedNPCs (the id is inside the file). Used by migration.</summary>
        public static ParseResult Parse(string fileName, string json)
        {
            ParseResult r = new ParseResult();
            Dictionary<string, object> o = Root(fileName, json, r);
            if (o == null)
                return r;

            string problem;
            string id;
            if ((problem = FieldReader.Text(o, "id", null, out id)) != null)
                return Fail(r, fileName, "id", problem);
            if (string.IsNullOrEmpty(id))
                return Fail(r, fileName, "id", "required");
            if (!IdPattern.IsMatch(id))
                return Fail(r, fileName, "id", "use only lowercase letters, digits and underscore (got \"" + id + "\")");

            NpcDefinition d = new NpcDefinition();
            d.Id = id;
            d.Folder = id;
            d.SourceFile = fileName;
            if (!ReadUnique(fileName, o, d, r) || !ReadShared(fileName, o, d, r))
                return r;
            r.Definition = d;
            return r;
        }

        /// <summary>True for names usable as an ANPC folder (and so as an id).</summary>
        public static bool IsValidFolderName(string folder)
        {
            return !string.IsNullOrEmpty(folder) && IdPattern.IsMatch(folder) && folder[0] != '_';
        }

        /// <summary>One ANPC folder's npc.json; the folder name is the id (spec §6, §7).</summary>
        public static ParseResult ParseFolder(string folder, string json)
        {
            ParseResult r = new ParseResult();
            if (!IsValidFolderName(folder))
                return Fail(r, folder, "folder",
                    "use only lowercase letters, digits and underscore, not starting with _ (got \"" + folder + "\")");

            string file = folder + "/npc.json";
            Dictionary<string, object> o = Root(file, json, r);
            if (o == null)
                return r;

            string problem;
            string jsonId;
            if (FieldReader.Text(o, "id", null, out jsonId) == null && !string.IsNullOrEmpty(jsonId) && jsonId != folder)
                r.Warnings.Add(file + ": id: \"" + jsonId + "\" differs from the folder name; using \"" + folder + "\"");

            string rawKind;
            if ((problem = FieldReader.Text(o, "kind", "generic", out rawKind)) != null)
                return Fail(r, file, "kind", problem);
            NpcDefinition d = new NpcDefinition();
            if (string.Equals(rawKind.Trim(), "unique", StringComparison.OrdinalIgnoreCase))
                d.Kind = NpcKind.Unique;
            else if (string.Equals(rawKind.Trim(), "generic", StringComparison.OrdinalIgnoreCase))
                d.Kind = NpcKind.Generic;
            else
                return Fail(r, file, "kind", "must be unique or generic (got \"" + rawKind + "\")");

            d.Id = folder;
            d.Folder = folder;
            d.SourceFile = file;
            bool ok = d.Kind == NpcKind.Unique ? ReadUnique(file, o, d, r) : ReadGeneric(file, o, d, r, o.ContainsKey("kind"));
            if (!ok || !ReadShared(file, o, d, r))
                return r;

            List<string> known = new List<string>(SharedKeys);
            known.AddRange(d.Kind == NpcKind.Unique ? UniqueKeys : GenericKeys);
            foreach (string key in FieldReader.UnknownKeys(o, known))
                r.Warnings.Add(file + ": " + key + ": unknown field, ignored");

            r.Definition = d;
            return r;
        }

        static Dictionary<string, object> Root(string file, string json, ParseResult r)
        {
            if (string.IsNullOrEmpty(json) || json.Trim().Length == 0)
            {
                Fail(r, file, "file", "empty");
                return null;
            }
            object root;
            try
            {
                List<string> repeated = new List<string>();
                root = Json.Parse(json, repeated);
                foreach (string d in repeated)
                    r.Warnings.Add(file + ": " + d + " appears twice in one object; the last one is used");
            }
            catch (JsonException e)
            {
                Fail(r, file, "file", "invalid JSON (" + e.Message + ")");
                return null;
            }
            Dictionary<string, object> o = root as Dictionary<string, object>;
            if (o == null)
                Fail(r, file, "file", "invalid JSON (top level must be an object)");
            return o;
        }

        static bool ReadUnique(string file, Dictionary<string, object> o, NpcDefinition d, ParseResult r)
        {
            foreach (string key in GenericOnlyKeys)
            {
                if (o.ContainsKey(key))
                    return Problem(r, file, key, "belongs to generic templates (\"kind\": \"generic\")");
            }

            string problem;
            string name;
            if ((problem = FieldReader.Text(o, "name", null, out name)) != null)
                return Problem(r, file, "name", problem);
            if (string.IsNullOrEmpty(name))
                return Problem(r, file, "name", "required");
            d.Name = name;

            object locationValue;
            o.TryGetValue("location", out locationValue);
            Dictionary<string, object> location = locationValue as Dictionary<string, object>;
            if (locationValue != null && location == null)
                return Problem(r, file, "location", "must be an object with region and place");
            if (location == null)
                location = new Dictionary<string, object>();

            string region;
            if ((problem = FieldReader.Text(location, "region", null, out region)) != null)
                return Problem(r, file, "location.region", problem);
            if (string.IsNullOrEmpty(region))
                return Problem(r, file, "location.region", "required");
            string place;
            if ((problem = FieldReader.Text(location, "place", null, out place)) != null)
                return Problem(r, file, "location.place", problem);
            if (string.IsNullOrEmpty(place))
                return Problem(r, file, "location.place", "required");

            double[] position;
            if (!FieldReader.Numbers(o, "position", out position) || position == null || position.Length != 3)
                return Problem(r, file, "position", "required, must be [x, y, z]");

            string portrait;
            if ((problem = FieldReader.Text(o, "portrait", "", out portrait)) != null)
                return Problem(r, file, "portrait", problem);
            string normalised = PortraitNames.Normalize(portrait);
            if (normalised.Length > 0)
                d.Portraits.Add(normalised);

            d.Region = region;
            d.Place = place;
            d.X = (float)position[0];
            d.Y = (float)position[1];
            d.Z = (float)position[2];
            return true;
        }

        static bool ReadGeneric(string file, Dictionary<string, object> o, NpcDefinition d, ParseResult r, bool kindGiven)
        {
            // Generic is the default kind: a file that only forgot "kind" gets told how to fix it.
            string uniqueOnly = kindGiven
                ? "belongs to unique ANPCs; generic templates use spawn"
                : "belongs to unique ANPCs — add \"kind\": \"unique\"";
            if (o.ContainsKey("location"))
                return Problem(r, file, "location", uniqueOnly);
            if (o.ContainsKey("position"))
                return Problem(r, file, "position", uniqueOnly);

            string problem;
            string name;
            if ((problem = FieldReader.Text(o, "name", "", out name)) != null)
                return Problem(r, file, "name", problem);
            d.Name = name;
            List<string> names;
            if ((problem = FieldReader.Texts(o, "names", out names)) != null)
                return Problem(r, file, "names", problem);
            if (names != null)
                d.Names.AddRange(names);

            string portrait;
            if ((problem = FieldReader.Text(o, "portrait", "", out portrait)) != null)
                return Problem(r, file, "portrait", problem);
            AddPortrait(d, portrait);
            List<string> portraits;
            if ((problem = FieldReader.Texts(o, "portraits", out portraits)) != null)
                return Problem(r, file, "portraits", problem);
            if (portraits != null)
            {
                foreach (string p in portraits)
                    AddPortrait(d, p);
            }

            d.Spawn = new GenericSpawn();
            Dictionary<string, object> spawn;
            if ((problem = FieldReader.Object(o, "spawn", out spawn)) != null)
                return Problem(r, file, "spawn", problem);
            if (spawn == null)
                return true;
            foreach (string key in FieldReader.UnknownKeys(spawn, SpawnKeys))
            {
                string hint = Typos.Closest(key, SpawnKeys);
                r.Warnings.Add(file + ": spawn." + key + ": unknown field" + (hint != null ? " (did you mean \"" + hint + "\"?)" : "") + ", ignored");
            }
            if (!ReadOutsideBlocks(file, spawn, d.Spawn, r))
                return true;

            List<string> types;
            if ((problem = FieldReader.Texts(spawn, "locationTypes", out types)) != null)
                return Problem(r, file, "spawn.locationTypes", problem);
            if (types != null)
            {
                d.Spawn.LocationTypes.Clear();
                foreach (string t in types)
                {
                    string canonical = LocationTypeNames.Canonical(t);
                    if (canonical == null)
                        return Problem(r, file, "spawn.locationTypes", "unknown location type \"" + t + "\"");
                    d.Spawn.LocationTypes.Add(canonical);
                }
            }

            List<Dictionary<string, object>> places;
            if ((problem = FieldReader.Objects(spawn, "places", out places)) != null)
                return Problem(r, file, "spawn.places", problem);
            if (places != null)
            {
                for (int i = 0; i < places.Count; i++)
                {
                    SpawnPlace p = new SpawnPlace();
                    if (!ReadPlace(file, "spawn.places[" + i + "]", places[i], p, r))
                        return false;
                    d.Spawn.Places.Add(p);
                }
            }

            double[] count;
            if (!FieldReader.Numbers(spawn, "count", out count) || (count != null && count.Length != 2))
                return Problem(r, file, "spawn.count", "must be [min, max]");
            if (count != null)
            {
                if (count[0] != Math.Floor(count[0]) || count[1] != Math.Floor(count[1]) ||
                    count[0] < 0 || count[0] > count[1] || count[1] > 20)
                    return Problem(r, file, "spawn.count", "need whole numbers 0 <= min <= max <= 20 (got [" +
                        FieldReader.Num(count[0]) + ", " + FieldReader.Num(count[1]) + "])");
                d.Spawn.CountMin = (int)count[0];
                d.Spawn.CountMax = (int)count[1];
            }
            return true;
        }

        /// <summary>
        /// spawn.dungeons / interiors / wilderness (outside-towns spec §3). With any of them and no locationTypes or
        /// places, the template is not a town template. Returns false when town fields need not be read.
        /// </summary>
        static bool ReadOutsideBlocks(string file, Dictionary<string, object> spawn, GenericSpawn s, ParseResult r)
        {
            bool anyBlock = false;
            Dictionary<string, object> block;
            if (FieldReader.Object(spawn, "dungeons", out block) != null)
                r.Warnings.Add(file + ": spawn.dungeons: must be an object, ignored");
            else if (block != null)
            {
                anyBlock = true;
                s.Dungeons = OutsideSpawnParser.Dungeons(file, block, r.Warnings);
            }
            if (FieldReader.Object(spawn, "interiors", out block) != null)
                r.Warnings.Add(file + ": spawn.interiors: must be an object, ignored");
            else if (block != null)
            {
                anyBlock = true;
                s.Interiors = OutsideSpawnParser.Interiors(file, block, r.Warnings);
            }
            if (FieldReader.Object(spawn, "wilderness", out block) != null)
                r.Warnings.Add(file + ": spawn.wilderness: must be an object, ignored");
            else if (block != null)
            {
                anyBlock = true;
                s.Wilderness = OutsideSpawnParser.Wilderness(file, block, r.Warnings);
            }
            if (anyBlock && !spawn.ContainsKey("locationTypes") && !spawn.ContainsKey("places"))
            {
                s.HasTownRules = false;
                if (spawn.ContainsKey("count"))
                    r.Warnings.Add(file + ": spawn.count: only for towns (add \"locationTypes\" to spawn in towns too), ignored");
                return false;
            }
            return true;
        }

        static void AddPortrait(NpcDefinition d, string raw)
        {
            string name = PortraitNames.Normalize(raw);
            if (name.Length > 0 && !d.Portraits.Contains(name))
                d.Portraits.Add(name);
        }

        static bool ReadPlace(string file, string field, Dictionary<string, object> o, SpawnPlace p, ParseResult r)
        {
            foreach (string key in FieldReader.UnknownKeys(o, PlaceKeys))
                r.Warnings.Add(file + ": " + field + "." + key + ": unknown field, ignored");

            string problem;
            if ((problem = FieldReader.Text(o, "region", null, out p.Region)) != null)
                return Problem(r, file, field + ".region", problem);
            if (string.IsNullOrEmpty(p.Region))
                return Problem(r, file, field + ".region", "required");
            if ((problem = FieldReader.Text(o, "place", null, out p.Place)) != null)
                return Problem(r, file, field + ".place", problem);
            if (string.IsNullOrEmpty(p.Place))
                return Problem(r, file, field + ".place", "required");

            object raw;
            if (o.TryGetValue("positions", out raw) && raw != null)
            {
                List<object> list = raw as List<object>;
                if (list == null)
                    return Problem(r, file, field + ".positions", "must be a list of [x, y, z]");
                foreach (object item in list)
                {
                    float[] xyz = Xyz(item);
                    if (xyz == null)
                        return Problem(r, file, field + ".positions", "must be a list of [x, y, z]");
                    p.Positions.Add(xyz);
                }
            }
            return true;
        }

        static float[] Xyz(object item)
        {
            List<object> list = item as List<object>;
            if (list == null || list.Count != 3)
                return null;
            float[] xyz = new float[3];
            for (int i = 0; i < 3; i++)
            {
                if (!(list[i] is double))
                    return null;
                xyz[i] = (float)(double)list[i];
            }
            return xyz;
        }

        static bool ReadShared(string file, Dictionary<string, object> o, NpcDefinition d, ParseResult r)
        {
            string problem;
            if ((problem = NpcMagicParser.Read(o, file, r.Warnings, out d.Magic)) != null)
                return Problem(r, file, "magic", problem);

            string rawAttitude;
            if ((problem = FieldReader.Text(o, "attitude", "calm", out rawAttitude)) != null)
                return Problem(r, file, "attitude", problem);
            bool hostile;
            if (string.Equals(rawAttitude.Trim(), "hostile", StringComparison.OrdinalIgnoreCase))
                hostile = true;
            else if (string.Equals(rawAttitude.Trim(), "calm", StringComparison.OrdinalIgnoreCase))
                hostile = false;
            else
                return Problem(r, file, "attitude", "must be calm or hostile (got \"" + rawAttitude + "\")");

            double[] hours;
            if (!FieldReader.Numbers(o, "hostileHours", out hours) || (hours != null && !ValidHours(hours)))
                return Problem(r, file, "hostileHours", "must be [from, to], whole hours 0-23");
            if (hours != null && !hostile)
                return Problem(r, file, "hostileHours", "only with \"attitude\": \"hostile\"");

            string rawClass;
            if ((problem = FieldReader.Text(o, "baseClass", "Spellsword", out rawClass)) != null)
                return Problem(r, file, "baseClass", problem);
            string baseClass = HumanClasses.Canonical(rawClass);
            string creature = baseClass == null ? Creatures.Canonical(rawClass) : null;
            if (baseClass == null && creature == null)
                return Problem(r, file, "baseClass", "unknown class \"" + rawClass + "\"");
            if (creature != null && !hostile)
                return Problem(r, file, "baseClass", "a creature (\"" + creature + "\") needs \"attitude\": \"hostile\"");
            if (creature != null && hours != null)
                return Problem(r, file, "baseClass", "a creature (\"" + creature + "\") is always hostile; remove hostileHours");
            if (creature != null)
                baseClass = creature;

            string rawGender;
            if ((problem = FieldReader.Text(o, "gender", "", out rawGender)) != null)
                return Problem(r, file, "gender", problem);
            string gender;
            if (string.IsNullOrEmpty(rawGender))
                gender = "";
            else if (string.Equals(rawGender, "Male", StringComparison.OrdinalIgnoreCase))
                gender = "Male";
            else if (string.Equals(rawGender, "Female", StringComparison.OrdinalIgnoreCase))
                gender = "Female";
            else
                return Problem(r, file, "gender", "must be Male or Female (got \"" + rawGender + "\")");

            string rawBravery;
            if ((problem = FieldReader.Text(o, "bravery", "Normal", out rawBravery)) != null)
                return Problem(r, file, "bravery", problem);
            Bravery bravery;
            if (!TryParseBravery(rawBravery, out bravery))
                return Problem(r, file, "bravery", "must be Coward, Normal or Brave (got \"" + rawBravery + "\")");

            double flee;
            if ((problem = FieldReader.Number(o, "fleeHealthPercent", 25, out flee)) != null || flee != Math.Floor(flee))
                return Problem(r, file, "fleeHealthPercent", "must be a whole number");
            if (flee < 1 || flee > 99)
                return Problem(r, file, "fleeHealthPercent", "must be 1-99 (got " + FieldReader.Num(flee) + ")");

            double[] calm;
            if (!FieldReader.Numbers(o, "calmDownHours", out calm) || (calm != null && calm.Length != 2))
                return Problem(r, file, "calmDownHours", "must be [min, max]");
            if (calm == null)
                calm = new double[] { 6, 48 };
            if (!(calm[0] > 0) || calm[0] > calm[1])
                return Problem(r, file, "calmDownHours",
                    "need 0 < min <= max (got [" + FieldReader.Num(calm[0]) + ", " + FieldReader.Num(calm[1]) + "])");

            double wander;
            if ((problem = FieldReader.Number(o, "wanderRadius", 8, out wander)) != null)
                return Problem(r, file, "wanderRadius", problem);
            if (wander < 0)
                return Problem(r, file, "wanderRadius", "must be 0 or more (got " + FieldReader.Num(wander) + ")");

            bool crime;
            if ((problem = FieldReader.Bool(o, "crimeOnAttack", true, out crime)) != null)
                return Problem(r, file, "crimeOnAttack", problem);

            string rawRace;
            if ((problem = FieldReader.Text(o, "race", "", out rawRace)) != null)
                return Problem(r, file, "race", problem);
            string race = "";
            if (rawRace.Trim().Length > 0)
            {
                race = CanonicalRace(rawRace);
                if (race == null)
                    return Problem(r, file, "race", "must be Breton, Redguard, Nord or DarkElf (Dunmer) (got \"" + rawRace + "\")");
            }

            d.BaseClass = baseClass;
            d.IsCreature = creature != null;
            d.Hostile = hostile;
            d.HostileFrom = hours != null ? (int)hours[0] : -1;
            d.HostileTo = hours != null ? (int)hours[1] : -1;
            d.Gender = gender;
            d.Bravery = bravery;
            d.FleeHealthPercent = (int)flee;
            d.CalmDownMinHours = (float)calm[0];
            d.CalmDownMaxHours = (float)calm[1];
            d.CrimeOnAttack = crime;
            d.WanderRadius = (float)wander;
            string nameList;
            if ((problem = FieldReader.Text(o, "nameList", "", out nameList)) != null)
                return Problem(r, file, "nameList", problem);

            double spriteHeight;
            if ((problem = FieldReader.Number(o, "spriteHeight", 0, out spriteHeight)) != null ||
                (o.ContainsKey("spriteHeight") && !(spriteHeight > 0)))
                return Problem(r, file, "spriteHeight", "must be a number above 0");

            d.Race = race;
            d.NameList = NameListParser.Normalize(nameList);
            double spriteHeightScale;
            if ((problem = FieldReader.Number(o, "spriteHeightScale", 1, out spriteHeightScale)) != null ||
                !(spriteHeightScale > 0) || (o.ContainsKey("spriteHeightScale") && o["spriteHeightScale"] == null) || double.IsInfinity(spriteHeightScale) ||
                float.IsInfinity((float)spriteHeightScale) || (float)spriteHeightScale <= 0)
                return Problem(r, file, "spriteHeightScale", "must be a finite number above 0");

            d.SpriteHeight = (float)spriteHeight;
            d.SpriteHeightScale = (float)spriteHeightScale;
            string combatKey;
            if ((problem = CombatRules.Read(o, d.IsCreature, out combatKey, out d.HealthScale, out d.DamageScale, out d.AttackSpeed)) != null)
                return Problem(r, file, combatKey, problem);
            ReadDialogueTypes(file, o, d, r);
            return true;
        }

        /// <summary>"dialogue": a type id or a list of them. A bad value only warns: the NPC still loads.</summary>
        static void ReadDialogueTypes(string file, Dictionary<string, object> o, NpcDefinition d, ParseResult r)
        {
            object raw;
            if (!o.TryGetValue("dialogue", out raw) || raw == null)
                return;
            List<object> list = raw as List<object>;
            if (list == null)
                list = new List<object> { raw };
            List<string> ids = new List<string>();
            foreach (object item in list)
            {
                string id = DialogueIds.Normalize(item as string);
                if (id.Length == 0)
                {
                    r.Warnings.Add(file + ": dialogue: must be a dialogue type name or a list of names, ignored");
                    return;
                }
                if (!ids.Contains(id))
                    ids.Add(id);
            }
            d.Dialogue.AddRange(ids);
        }

        static bool ValidHours(double[] hours)
        {
            if (hours.Length != 2)
                return false;
            foreach (double h in hours)
            {
                if (h != Math.Floor(h) || h < 0 || h > 23)
                    return false;
            }
            return true;
        }

        static string CanonicalRace(string raw)
        {
            if (string.Equals(raw.Trim(), "Dunmer", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(raw.Trim(), "Dark Elf", StringComparison.OrdinalIgnoreCase))
                return "DarkElf";
            foreach (string race in RaceNames)
            {
                if (string.Equals(race, raw.Trim(), StringComparison.OrdinalIgnoreCase))
                    return race;
            }
            return null;
        }

        static bool TryParseBravery(string value, out Bravery bravery)
        {
            bravery = Bravery.Normal;
            if (value == null)
                return false;
            foreach (Bravery b in (Bravery[])Enum.GetValues(typeof(Bravery)))
            {
                if (string.Equals(b.ToString(), value.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    bravery = b;
                    return true;
                }
            }
            return false;
        }

        static bool Problem(ParseResult r, string file, string field, string problem)
        {
            r.Error = file + ": " + field + ": " + problem;
            r.Definition = null;
            return false;
        }

        static ParseResult Fail(ParseResult r, string file, string field, string problem)
        {
            Problem(r, file, field, problem);
            return r;
        }
    }
}

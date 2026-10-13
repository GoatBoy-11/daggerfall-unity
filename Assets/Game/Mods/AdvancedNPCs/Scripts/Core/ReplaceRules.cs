using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace AdvancedNPCs.Core
{
    /// <summary>One vanilla flat NPC kind an ANPC can stand in for: a billboard sprite (archive.record) or a faction id.</summary>
    public class ReplaceTarget
    {
        public int Archive = -1;
        public int Record = -1;
        public int Faction = -1;

        public bool Matches(int archive, int record, int faction)
        {
            if (Faction >= 0)
                return faction == Faction;
            return archive == Archive && record == Record;
        }

        public override string ToString()
        {
            return Faction >= 0 ? "faction:" + Faction.ToString(CultureInfo.InvariantCulture)
                : Archive.ToString(CultureInfo.InvariantCulture) + "." + Record.ToString(CultureInfo.InvariantCulture);
        }
    }

    /// <summary>What a generic ANPC replaces, and how much of itself it shows while it stands in.</summary>
    public class ReplaceSettings
    {
        public readonly List<ReplaceTarget> Targets = new List<ReplaceTarget>();
        /// <summary>The talk window shows the ANPC's portrait instead of the vanilla face.</summary>
        public bool Portrait = true;
        /// <summary>The ANPC's own name instead of the vanilla NPC's (quest text keeps the vanilla name).</summary>
        public bool Name;
        /// <summary>Metres it stays within around the flat's spot while calm (walks back after a fight); 0 = no leash.</summary>
        public float StayNear;

        public bool Matches(int archive, int record, int faction)
        {
            foreach (ReplaceTarget t in Targets)
            {
                if (t.Matches(archive, record, faction))
                    return true;
            }
            return false;
        }
    }

    /// <summary>
    /// "replaces" in npc.json (flat replacement spec): a generic ANPC takes the place of vanilla flat NPCs (StaticNPC),
    /// given as sprite ids "177.2" (also "177 2", "177_2") or "faction:1003"; one entry or a list.
    /// "replacePortrait" (default true), "replaceName" (default false) and "stayNear" (metres, default 0) go with it.
    /// When several ANPCs replace the same flat, each flat picks one by its own seed, so every vanilla person keeps
    /// the same stand-in for the whole game. No Unity or DFU types.
    /// </summary>
    public static class ReplaceRules
    {
        public const float MaxStayNear = 50f;
        public static readonly string[] Keys = { "replaces", "replacePortrait", "replaceName", "stayNear" };
        static readonly Regex SpriteId = new Regex(@"^\s*(\d+)\s*[._ ]\s*(\d+)\s*$");
        static readonly Regex FactionId = new Regex(@"^\s*faction\s*:\s*(\d+)\s*$", RegexOptions.IgnoreCase);

        /// <summary>Reads the replacement fields. settings is null when "replaces" is absent. Returns null or a problem (key set).</summary>
        public static string Read(Dictionary<string, object> o, out string key, out ReplaceSettings settings)
        {
            settings = null;
            key = "replaces";
            object raw;
            if (!o.TryGetValue("replaces", out raw) || raw == null)
            {
                foreach (string k in Keys)
                {
                    if (k != "replaces" && o.ContainsKey(k))
                    {
                        key = k;
                        return "only works together with \"replaces\"";
                    }
                }
                return null;
            }
            List<object> items = raw as List<object>;
            if (items == null)
                items = new List<object> { raw };
            if (items.Count == 0)
                return "needs at least one vanilla NPC, like \"177.2\" (sprite) or \"faction:1003\"";
            ReplaceSettings s = new ReplaceSettings();
            foreach (object item in items)
            {
                string text = item as string;
                ReplaceTarget t = text != null ? ParseTarget(text) : null;
                if (t == null)
                    return "\"" + (item == null ? "null" : item.ToString()) + "\" is not a vanilla NPC id: use a sprite like \"177.2\" or \"faction:1003\"";
                s.Targets.Add(t);
            }
            string problem;
            key = "replacePortrait";
            if ((problem = FieldReader.Bool(o, "replacePortrait", true, out s.Portrait)) != null)
                return problem;
            key = "replaceName";
            if ((problem = FieldReader.Bool(o, "replaceName", false, out s.Name)) != null)
                return problem;
            key = "stayNear";
            double near;
            if ((problem = FieldReader.Number(o, "stayNear", 0, out near)) != null || (o.ContainsKey("stayNear") && o["stayNear"] == null) ||
                double.IsNaN(near) || near < 0 || near > MaxStayNear)
                return "must be a number from 0 (no leash) to " + FieldReader.Num(MaxStayNear) + " (metres)";
            s.StayNear = (float)near;
            key = "replaces";
            settings = s;
            return null;
        }

        /// <summary>"177.2", "177 2", "177_2" or "faction:1003"; null if neither.</summary>
        public static ReplaceTarget ParseTarget(string text)
        {
            Match m = FactionId.Match(text);
            int a, b;
            if (m.Success && int.TryParse(m.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out a))
                return new ReplaceTarget { Faction = a };
            m = SpriteId.Match(text);
            if (m.Success && int.TryParse(m.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out a) &&
                int.TryParse(m.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out b))
                return new ReplaceTarget { Archive = a, Record = b };
            return null;
        }

        /// <summary>Every template that replaces this flat, sorted by id (so the pick does not depend on load order).</summary>
        public static List<NpcDefinition> Candidates(IList<NpcDefinition> templates, int archive, int record, int faction)
        {
            List<NpcDefinition> result = new List<NpcDefinition>();
            if (templates == null)
                return result;
            foreach (NpcDefinition t in templates)
            {
                if (t.Replace != null && t.Replace.Matches(archive, record, faction))
                    result.Add(t);
            }
            result.Sort(delegate (NpcDefinition x, NpcDefinition y) { return string.CompareOrdinal(x.Id, y.Id); });
            return result;
        }

        /// <summary>
        /// The key of the person standing in for one flat: unique per flat, the same on every visit. where tells the
        /// town's streets, a building (its key) and the dungeon apart, since flat hashes only differ within one block.
        /// </summary>
        public static string KeyFor(int mapId, string where, int flatHash)
        {
            return "replace@" + mapId.ToString(CultureInfo.InvariantCulture) + "/" + where + "/" + flatHash.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>The template one flat uses, picked by the flat's key: fixed for that flat, random across flats. Null if none.</summary>
        public static NpcDefinition Pick(List<NpcDefinition> candidates, string flatKey)
        {
            if (candidates == null || candidates.Count == 0)
                return null;
            return candidates[(int)(StableHash.Of(flatKey) % (uint)candidates.Count)];
        }
    }
}

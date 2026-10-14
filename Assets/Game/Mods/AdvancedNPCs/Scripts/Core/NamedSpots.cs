using System;
using System.Collections.Generic;

namespace AdvancedNPCs.Core
{
    public class NamedSpot
    {
        public string Id, RelativeTo;
        public string Region, Place, Context = "";
        public PackagePoint Position;
        public double? Facing;
        public int Capacity = 1;
        public readonly List<string> Tags = new List<string>();
        public bool Matches(IList<string> selectors, string region, string place, string context)
        {
            if (Region != null && (!string.Equals(Region,region,StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Place,place,StringComparison.OrdinalIgnoreCase) || !string.Equals(Context,context,StringComparison.OrdinalIgnoreCase))) return false;
            foreach (string s in selectors) if (s == Id || Tags.Contains(s)) return true;
            return false;
        }
    }
    public static class SpotParser
    {
        public static readonly string[] Keys = {"position","relativeTo","location","tags","capacity","facing"};
        public static readonly string[] LocationKeys = {"region","place","context"};
        public static NamedSpot Parse(string id, string json, out string error)
        {
            error = null;
            if (!DefinitionParser.IsValidFolderName(id)) { error = "invalid spot filename"; return null; }
            Dictionary<string,object> o;
            try
            {
                var duplicates = new List<string>();
                o = Json.Parse(json,duplicates) as Dictionary<string,object>;
                if (duplicates.Count > 0) { error = "duplicate field "+duplicates[0]; return null; }
            }
            catch (JsonException e) { error = e.Message; return null; }
            if (o == null || FieldReader.UnknownKeys(o,Keys).Count > 0) { error = "invalid spot fields"; return null; }
            var spot = new NamedSpot {Id=id};
            object raw;
            if (!o.TryGetValue("position",out raw) || (spot.Position=PackageParser.Point(raw,1000000))==null)
            { error = "position must be [x,y,z], each -1000000 to 1000000"; return null; }
            if ((o.ContainsKey("relativeTo") && o["relativeTo"]==null) || FieldReader.Text(o,"relativeTo","home",out spot.RelativeTo)!=null || (spot.RelativeTo!="home" && spot.RelativeTo!="area"))
            { error = "relativeTo must be home or area"; return null; }
            if (o.TryGetValue("location",out raw))
            {
                var location = raw as Dictionary<string,object>;
                if (location==null || FieldReader.UnknownKeys(location,LocationKeys).Count>0 ||
                    FieldReader.Text(location,"region",null,out spot.Region)!=null || string.IsNullOrEmpty(spot.Region) ||
                    FieldReader.Text(location,"place",null,out spot.Place)!=null || string.IsNullOrEmpty(spot.Place) ||
                    (location.ContainsKey("context") && location["context"]==null) || FieldReader.Text(location,"context","",out spot.Context)!=null || spot.Context==null)
                { error = "location needs region/place and optional context"; return null; }
                if (spot.Context=="outdoors") spot.Context="";
                if (spot.Context!="" && spot.Context!="dungeon")
                {
                    uint building;
                    if (!spot.Context.StartsWith("b",StringComparison.Ordinal) || !uint.TryParse(spot.Context.Substring(1),out building))
                    { error = "context must be outdoors, dungeon, or b<buildingKey>"; return null; }
                }
            }
            if (spot.RelativeTo=="area" && spot.Region==null) { error = "area spots require location"; return null; }
            double capacity;
            if (!PackageParser.Number(o,"capacity",1,1,16,out capacity) || capacity!=Math.Floor(capacity))
            { error = "capacity must be an integer 1-16"; return null; }
            spot.Capacity=(int)capacity;
            if (o.ContainsKey("facing"))
            {
                double facing;
                if (!PackageParser.Number(o,"facing",0,0,360,out facing)) { error="facing must be 0-360 degrees"; return null; }
                spot.Facing=facing;
            }
            if (o.ContainsKey("tags"))
            {
                List<string> tags = o["tags"] is string ? new List<string> {(string)o["tags"]} : null;
                string tagError = tags != null ? null : FieldReader.Texts(o,"tags",out tags);
                if (tagError!=null || tags==null || tags.Count>16)
                { error="tags must be a name or list of up to 16 identifiers"; return null; }
                foreach (string tag in tags)
                {
                    if (!DefinitionParser.IsValidFolderName(tag)) { error="invalid spot tag "+tag; return null; }
                    if (!spot.Tags.Contains(tag)) spot.Tags.Add(tag);
                }
            }
            return spot;
        }
    }
    /// <summary>Transient ownership only; reservations never enter save data.</summary>
    public class SpotOccupancy
    {
        readonly Dictionary<string,HashSet<string>> owners = new Dictionary<string,HashSet<string>>(StringComparer.Ordinal);
        public bool TryClaim(string key, string owner, int capacity)
        {
            HashSet<string> people;
            if (!owners.TryGetValue(key,out people)) { people=new HashSet<string>(StringComparer.Ordinal); owners.Add(key,people); }
            if (people.Contains(owner)) return true;
            if (people.Count>=capacity) return false;
            people.Add(owner); return true;
        }
        public int Count(string key) { HashSet<string> people; return owners.TryGetValue(key,out people)?people.Count:0; }
        public void Release(string owner)
        {
            var empty=new List<string>();
            foreach (var p in owners) { p.Value.Remove(owner); if (p.Value.Count==0) empty.Add(p.Key); }
            foreach (string key in empty) owners.Remove(key);
        }
    }
}

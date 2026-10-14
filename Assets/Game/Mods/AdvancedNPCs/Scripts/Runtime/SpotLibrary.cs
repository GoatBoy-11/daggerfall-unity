using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using UnityEngine;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    public class SpotChoice
    {
        public NamedSpot Spot;
        public Vector3 Position;
        public string Key;
        public float Distance;
    }
    public class SpotLibrary
    {
        public const string FolderName = "_Spots";
        public readonly SpotOccupancy Occupancy = new SpotOccupancy();
        readonly Dictionary<string,NamedSpot> spots = new Dictionary<string,NamedSpot>(StringComparer.Ordinal);
        public IEnumerable<NamedSpot> All { get { return spots.Values; } }
        public int Count { get { int count=0; foreach(var s in spots.Values) if(s!=null) count++; return count; } }
        public List<SpotChoice> Candidates(IList<string> selectors,string region,string place,string context,int rootId,Vector3 home,Vector3 current)
        {
            var found=new List<SpotChoice>();
            foreach(var spot in spots.Values)
            {
                if(spot==null || !spot.Matches(selectors,region,place,context)) continue;
                Vector3 local=new Vector3((float)spot.Position.X,(float)spot.Position.Y,(float)spot.Position.Z);
                if(spot.RelativeTo=="home") local+=home;
                string key=rootId.ToString(CultureInfo.InvariantCulture)+"/"+spot.Id+"/"+
                    local.x.ToString("F2",CultureInfo.InvariantCulture)+","+local.y.ToString("F2",CultureInfo.InvariantCulture)+","+local.z.ToString("F2",CultureInfo.InvariantCulture);
                found.Add(new SpotChoice {Spot=spot,Position=local,Key=key,Distance=(local-current).sqrMagnitude});
            }
            found.Sort(delegate(SpotChoice a,SpotChoice b){int d=a.Distance.CompareTo(b.Distance);return d!=0?d:string.CompareOrdinal(a.Spot.Id,b.Spot.Id);});
            return found;
        }
        internal void AddForTest(NamedSpot spot) { spots[spot.Id]=spot; }
        internal void RemoveForTest(string id) { spots.Remove(id); }
        public static SpotLibrary Load(string root)
        {
            var library=new SpotLibrary();
            string folder=Path.Combine(root,FolderName);
            if (!Directory.Exists(folder)) return library;
            try
            {
                var paths=Directory.GetFiles(folder);Array.Sort(paths,StringComparer.Ordinal);
                foreach (string path in paths)
                {
                    if (!string.Equals(Path.GetExtension(path),".json",StringComparison.OrdinalIgnoreCase)) continue;
                    try
                    {
                        string error;
                        string id=Path.GetFileNameWithoutExtension(path);
                        var spot=SpotParser.Parse(id,File.ReadAllText(path),out error);
                        if (spot==null) AdvancedNpcsMod.LogError(FolderName+"/"+id+": "+error);
                        else if (library.spots.ContainsKey(id)) { library.spots[id]=null; AdvancedNpcsMod.LogError(FolderName+"/"+id+": duplicate spot; disabled"); }
                        else library.spots.Add(id,spot);
                    }
                    catch (Exception e) { AdvancedNpcsMod.LogError(path+": "+e.Message); }
                }
            }
            catch (Exception e) { AdvancedNpcsMod.LogError(FolderName+": "+e.Message); }
            return library;
        }
    }
}

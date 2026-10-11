namespace AdvancedNPCs.Core
{
    /// <summary>
    /// One ANPC as it is spawned: a unique ANPC (Key = id) or one generic person planned from a template
    /// (Key = template@mapId#n). Everything random about it comes from Seed, in a fixed order.
    /// </summary>
    public class NpcInstance
    {
        public string Key;
        public NpcDefinition Definition;
        public string Name;
        /// <summary>"Male" or "Female".</summary>
        public string Gender;
        /// <summary>"Breton", "Redguard", "Nord", "DarkElf" or "Khajiit".</summary>
        public string Race;
        /// <summary>Normalised portrait name, or null for a vanilla face.</summary>
        public string PortraitName;
        public int FaceOutfit;
        public int FaceVariant;
        public uint Seed;
        /// <summary>True when X, Y, Z are known (unique ANPCs, spawn.places positions).</summary>
        public bool HasFixedPosition;
        /// <summary>Position relative to the town's origin.</summary>
        public float X;
        public float Y;
        public float Z;
        /// <summary>Index into the town's walkable cell list, or -1.</summary>
        public int CellIndex = -1;
        /// <summary>False for "Random each visit" generic people: their state is never saved.</summary>
        public bool Persistent = true;
        /// <summary>Outside towns: people of one template in one place stand together (index of the group), or -1.</summary>
        public int Group = -1;

        public int FaceRecord()
        {
            return VanillaFaces.FaceRecord(Race, Gender, FaceOutfit, FaceVariant);
        }

        public static NpcInstance ForUnique(NpcDefinition def, string defaultRace)
        {
            NpcInstance i = new NpcInstance();
            i.Key = def.Id;
            i.Definition = def;
            i.Name = def.Name;
            i.Seed = StableHash.Of(def.Id);
            SeededRandom rng = new SeededRandom(i.Seed);
            i.Gender = PickGender(def.Gender, rng);
            i.Race = string.IsNullOrEmpty(def.Race) ? defaultRace : def.Race;
            i.PortraitName = def.Portraits.Count > 0 ? def.Portraits[0] : null;
            i.FaceOutfit = rng.Next(VanillaFaces.OutfitVariants);
            i.FaceVariant = rng.Next(VanillaFaces.FaceVariants);
            i.HasFixedPosition = true;
            i.X = def.X;
            i.Y = def.Y;
            i.Z = def.Z;
            i.Persistent = true;
            return i;
        }

        public static string PickGender(string fixedGender, SeededRandom rng)
        {
            if (!string.IsNullOrEmpty(fixedGender))
                return fixedGender;
            return rng.Next(2) == 0 ? "Male" : "Female";
        }
    }
}

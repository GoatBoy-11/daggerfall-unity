using System.Collections.Generic;

namespace AdvancedNPCs.Core
{
    public enum Bravery
    {
        Coward,
        Normal,
        Brave,
    }

    public enum NpcKind
    {
        Unique,
        Generic,
    }

    /// <summary>A validated ANPC definition (one folder's npc.json) used at runtime.</summary>
    public class NpcDefinition
    {
        public string Id;
        public NpcKind Kind = NpcKind.Unique;
        public string Name = "";
        public string Region;
        public string Place;
        public float X;
        public float Y;
        public float Z;
        public string BaseClass;
        public string Gender;
        /// <summary>"Breton", "Redguard", "Nord", or "" for the region's people.</summary>
        public string Race = "";
        /// <summary>Normalised portrait names (PortraitNames); a unique ANPC has 0 or 1.</summary>
        public readonly List<string> Portraits = new List<string>();
        /// <summary>Generic templates: names to pick from (empty: fixed Name or generated).</summary>
        public readonly List<string> Names = new List<string>();
        public Bravery Bravery;
        public int FleeHealthPercent;
        public float CalmDownMinHours;
        public float CalmDownMaxHours;
        public bool CrimeOnAttack;
        public float WanderRadius;
        /// <summary>Folder name under ANPCs (equals Id).</summary>
        public string Folder = "";
        /// <summary>Path used in messages, e.g. "bram/npc.json".</summary>
        public string SourceFile;
    }
}

namespace AdvancedNPCs.Core
{
    public enum Bravery
    {
        Coward,
        Normal,
        Brave,
    }

    /// <summary>A validated NPC definition used at runtime.</summary>
    public class NpcDefinition
    {
        public string Id;
        public string Name;
        public string Region;
        public string Place;
        public float X;
        public float Y;
        public float Z;
        public string BaseClass;
        public string Gender;
        public Bravery Bravery;
        public int FleeHealthPercent;
        public float CalmDownMinHours;
        public float CalmDownMaxHours;
        public bool CrimeOnAttack;
        public float WanderRadius;
        public string SourceFile;
    }
}

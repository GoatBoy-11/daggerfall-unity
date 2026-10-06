using System;

namespace AdvancedNPCs.Core
{
    public enum Bravery
    {
        Coward,
        Normal,
        Brave,
    }

    [Serializable]
    public class NpcLocationJson
    {
        public string region;
        public string place;
    }

    /// <summary>Raw JSON shape of one definition file. Field names are the JSON keys; initial values are the defaults.</summary>
    [Serializable]
    public class NpcDefinitionJson
    {
        public string id;
        public string name;
        public NpcLocationJson location;
        public float[] position;
        public string baseClass = "Spellsword";
        public string gender = "";
        public string bravery = "Normal";
        public int fleeHealthPercent = 25;
        public float[] calmDownHours = new float[] { 6f, 48f };
        public bool crimeOnAttack = true;
        public float wanderRadius = 8f;
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

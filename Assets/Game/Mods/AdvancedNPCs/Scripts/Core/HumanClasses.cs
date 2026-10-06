using System;

namespace AdvancedNPCs.Core
{
    /// <summary>
    /// Class names allowed for baseClass. Mirrors MobileTypes 128-145 in Assets/Scripts/DaggerfallUnityEnums.cs.
    /// Knight_CityWatch is excluded because DFU gives guards special crime handling.
    /// </summary>
    public static class HumanClasses
    {
        public static readonly string[] Names = new string[]
        {
            "Mage", "Spellsword", "Battlemage", "Sorcerer", "Healer", "Nightblade",
            "Bard", "Burglar", "Rogue", "Acrobat", "Thief", "Assassin",
            "Monk", "Archer", "Ranger", "Barbarian", "Warrior", "Knight",
        };

        /// <summary>Returns the canonical class name for a case-insensitive match, or null.</summary>
        public static string Canonical(string name)
        {
            if (name == null)
                return null;
            foreach (string n in Names)
            {
                if (string.Equals(n, name.Trim(), StringComparison.OrdinalIgnoreCase))
                    return n;
            }
            return null;
        }
    }
}

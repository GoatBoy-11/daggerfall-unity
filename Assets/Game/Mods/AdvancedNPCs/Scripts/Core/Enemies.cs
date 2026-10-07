using System;

namespace AdvancedNPCs.Core
{
    /// <summary>
    /// DFU creatures allowed as baseClass for hostile ANPCs. Mirrors MobileTypes 0-42 in
    /// Assets/Scripts/DaggerfallUnityEnums.cs; Horse_Invalid is left out (it crashes when spawned).
    /// </summary>
    public static class Creatures
    {
        public static readonly string[] Names = new string[]
        {
            "Rat", "Imp", "Spriggan", "GiantBat", "GrizzlyBear", "SabertoothTiger", "Spider", "Orc", "Centaur",
            "Werewolf", "Nymph", "Slaughterfish", "OrcSergeant", "Harpy", "Wereboar", "SkeletalWarrior", "Giant",
            "Zombie", "Ghost", "Mummy", "GiantScorpion", "OrcShaman", "Gargoyle", "Wraith", "OrcWarlord", "FrostDaedra",
            "FireDaedra", "Daedroth", "Vampire", "DaedraSeducer", "VampireAncient", "DaedraLord", "Lich", "AncientLich",
            "Dragonling", "FireAtronach", "IronAtronach", "FleshAtronach", "IceAtronach", "Dragonling_Alternate",
            "Dreugh", "Lamia",
        };

        /// <summary>Returns the canonical creature name for a case-insensitive match, or null.</summary>
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

    /// <summary>When an ANPC is an enemy of the player (enemies spec §5). No Unity or DFU types.</summary>
    public static class EnemyRules
    {
        /// <param name="hostile">The definition's "attitude": "hostile".</param>
        /// <param name="fromHour">hostileHours start (0-23), or -1 for always.</param>
        /// <param name="toHour">hostileHours end, exclusive; equal to fromHour means all day.</param>
        /// <param name="hour">Current in-game hour (0-23).</param>
        /// <param name="forcedOn">Switched to an enemy on the go.</param>
        /// <param name="forcedOff">Switched calm on the go (wins over everything).</param>
        public static bool IsEnemyNow(bool hostile, int fromHour, int toHour, int hour, bool forcedOn, bool forcedOff)
        {
            if (forcedOff)
                return false;
            if (forcedOn)
                return true;
            if (!hostile)
                return false;
            if (fromHour < 0 || toHour < 0 || fromHour == toHour)
                return true;
            if (fromHour < toHour)
                return hour >= fromHour && hour < toHour;
            return hour >= fromHour || hour < toHour;
        }
    }
}

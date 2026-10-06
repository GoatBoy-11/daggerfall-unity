namespace AdvancedNPCs.Core
{
    /// <summary>
    /// Vanilla citizen face records (copy of MobilePersonNPC's tables, MobilePersonNPC.cs:32-39): record =
    /// table[outfit] + face, with 4 outfit variants and 24 faces each. Used for the talk proxy's portrait.
    /// </summary>
    public static class VanillaFaces
    {
        public const int OutfitVariants = 4;
        public const int FaceVariants = 24;

        static readonly int[] MaleRedguard = { 336, 312, 336, 312 };
        static readonly int[] FemaleRedguard = { 144, 144, 120, 96 };
        static readonly int[] MaleNord = { 240, 264, 168, 192 };
        static readonly int[] FemaleNord = { 72, 0, 48, 0 };
        static readonly int[] MaleBreton = { 192, 216, 288, 240 };
        static readonly int[] FemaleBreton = { 72, 72, 24, 72 };

        /// <summary>Unknown races use the Breton table; out-of-range variants are clamped.</summary>
        public static int FaceRecord(string race, string gender, int outfit, int face)
        {
            bool female = gender == "Female";
            int[] table;
            if (race == "Redguard")
                table = female ? FemaleRedguard : MaleRedguard;
            else if (race == "Nord")
                table = female ? FemaleNord : MaleNord;
            else
                table = female ? FemaleBreton : MaleBreton;

            int o = outfit < 0 ? 0 : (outfit >= OutfitVariants ? OutfitVariants - 1 : outfit);
            int f = face < 0 ? 0 : (face >= FaceVariants ? FaceVariants - 1 : face);
            return table[o] + f;
        }
    }
}

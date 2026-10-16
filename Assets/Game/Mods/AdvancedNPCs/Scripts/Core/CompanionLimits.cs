namespace AdvancedNPCs.Core
{
    public static class CompanionLimits
    {
        public const int Default=3;
        // DFU multiple-choice values are indices: 3..10, then unlimited.
        public static int FromSetting(int choice) {return choice==8?0:choice>=0 && choice<8?choice+3:Default;}
        public static bool ConsumesSlot(bool dead,string status,bool exempt)
        {return !dead && !exempt && CompanionCombatRules.Contract(status);}
        public static bool CanRecruit(int maximum,int used,bool exempt)
        {return exempt || maximum==0 || used<maximum;}
    }
}

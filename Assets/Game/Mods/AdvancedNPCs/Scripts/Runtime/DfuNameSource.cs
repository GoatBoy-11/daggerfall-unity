using DaggerfallWorkshop;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Utility;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>DFU's own person names, made repeatable by seeding DFU's random generator for one call.</summary>
    public class DfuNameSource : INameSource
    {
        public string Generate(string race, string gender, uint seed)
        {
            NameHelper.BankTypes bank = NameHelper.BankTypes.Breton;
            if (race == "Redguard")
                bank = NameHelper.BankTypes.Redguard;
            else if (race == "Nord")
                bank = NameHelper.BankTypes.Nord;

            DFRandom.SaveSeed();
            try
            {
                DFRandom.srand(seed);
                return DaggerfallUnity.Instance.NameHelper.FullName(bank, gender == "Female" ? Genders.Female : Genders.Male);
            }
            finally
            {
                DFRandom.RestoreSeed();
            }
        }
    }
}

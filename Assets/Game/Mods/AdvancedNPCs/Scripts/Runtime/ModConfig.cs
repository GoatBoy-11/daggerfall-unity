using UnityEngine;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using AdvancedNPCs.Core;
using DfuModSettings = DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings.ModSettings;

namespace AdvancedNPCs
{
    /// <summary>The mod's settings (spec §8). Changes apply the next time a town is built.</summary>
    public class ModConfig
    {
        public const string Section = "Population";

        // A bool, not a GenericMode field: DFU's runtime compiler cannot load a class whose enum field type is
        // declared in a file it compiles later (TypeLoadException "bad underlying type").
        bool randomEachVisit;

        public GenericMode Mode
        {
            get { return randomEachVisit ? GenericMode.RandomEachVisit : GenericMode.SamePeople; }
        }

        /// <summary>The GenericPeople setting as a switch (the self-test sets it for its own run).</summary>
        public bool RandomEachVisit
        {
            get { return randomEachVisit; }
            set { randomEachVisit = value; }
        }

        public int MaxGenericPerTown = 12;
        public bool Dungeons = true;
        public bool Interiors = true;
        public bool Wilderness = true;
        public int MaxWildernessAround = 4;
        /// <summary>True once values were read from the mod's settings (false: defaults).</summary>
        public bool FromSettings;

        public void Attach(Mod mod)
        {
            if (mod == null || !mod.HasSettings)
            {
                AdvancedNpcsMod.Log("No mod settings found; using defaults (same people every visit, at most 12 per town).");
                return;
            }
            mod.LoadSettingsCallback = Apply;
            mod.LoadSettings();
        }

        public void Apply(DfuModSettings settings, ModSettingsChange change)
        {
            // Each key on its own: settings saved by an older version may lack the newer ones.
            randomEachVisit = Read(delegate { return settings.GetInt(Section, "GenericPeople") == 1; }, false);
            MaxGenericPerTown = Mathf.Clamp(Read(delegate { return settings.GetInt(Section, "MaxGenericPerTown"); }, 12), 0, 30);
            Dungeons = Read(delegate { return settings.GetBool(Section, "Dungeons"); }, true);
            Interiors = Read(delegate { return settings.GetBool(Section, "Interiors"); }, true);
            Wilderness = Read(delegate { return settings.GetBool(Section, "Wilderness"); }, true);
            MaxWildernessAround = Mathf.Clamp(Read(delegate { return settings.GetInt(Section, "MaxWildernessAround"); }, 4), 0, 10);
            FromSettings = true;
            AdvancedNpcsMod.Log("Settings: generic people " + Mode + ", at most " + MaxGenericPerTown + " per town; dungeons " + Dungeons +
                ", interiors " + Interiors + ", wilderness " + Wilderness + " (at most " + MaxWildernessAround + " around you).");
        }

        static T Read<T>(System.Func<T> get, T fallback)
        {
            try
            {
                return get();
            }
            catch (System.Exception)
            {
                return fallback;
            }
        }
    }
}

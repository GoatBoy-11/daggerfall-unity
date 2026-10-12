using UnityEngine;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using AdvancedNPCs.Core;
using DfuModSettings = DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings.ModSettings;

namespace AdvancedNPCs
{
    /// <summary>
    /// The mod's settings (spec §8). Population changes apply the next time a town is built; corpse and sound settings
    /// at once (bodies already lying keep whether they played a death animation).
    /// </summary>
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

        public const string CorpsesSection = "Corpses";
        /// <summary>A dying ANPC with a sprite set plays its death sheet; off: the body lies still at once.</summary>
        public bool DeathAnimation = true;
        /// <summary>A body on its last death frame keeps turning with the view; off: front or back, as a billboard.</summary>
        public bool DirectionalCorpses;

        public const string SoundsSection = "Sounds";
        /// <summary>Whose voice ANPCs use (VoiceModes); read every time a sound would play.</summary>
        public int VoiceMode = VoiceModes.Anpc;
        /// <summary>Footsteps from Sounds folders.</summary>
        public bool Footsteps = true;

        /// <summary>True once values were read from the mod's settings (false: defaults).</summary>
        public bool FromSettings;

        public void Attach(Mod mod)
        {
            if (mod == null || !mod.HasSettings)
            {
                AdvancedNpcsMod.Log("No mod settings found; using defaults (same people every visit, at most 12 per town).");
                return;
            }
            attached = mod;
            mod.LoadSettingsCallback = Apply;
            mod.LoadSettings();
        }

        Mod attached;

        /// <summary>
        /// Reads DFU's current values again (after the self-test forced its own); false without mod settings. Also picks
        /// up a change the player made in the settings window meanwhile.
        /// </summary>
        public bool Reload()
        {
            if (attached == null || !attached.HasSettings)
                return false;
            attached.LoadSettings();
            return true;
        }

        public void Apply(DfuModSettings settings, ModSettingsChange change)
        {
            // Each key on its own: settings saved by an older version may lack the newer ones.
            MissingKeys.Clear();
            randomEachVisit = Read("GenericPeople", delegate { return settings.GetInt(Section, "GenericPeople") == 1; }, false);
            MaxGenericPerTown = Mathf.Clamp(Read("MaxGenericPerTown", delegate { return settings.GetInt(Section, "MaxGenericPerTown"); }, 12), 0, 30);
            Dungeons = Read("Dungeons", delegate { return settings.GetBool(Section, "Dungeons"); }, true);
            Interiors = Read("Interiors", delegate { return settings.GetBool(Section, "Interiors"); }, true);
            Wilderness = Read("Wilderness", delegate { return settings.GetBool(Section, "Wilderness"); }, true);
            MaxWildernessAround = Mathf.Clamp(Read("MaxWildernessAround", delegate { return settings.GetInt(Section, "MaxWildernessAround"); }, 4), 0, 10);
            DeathAnimation = Read("DeathAnimation", delegate { return settings.GetBool(CorpsesSection, "DeathAnimation"); }, true);
            DirectionalCorpses = Read("DirectionalCorpses", delegate { return settings.GetBool(CorpsesSection, "DirectionalCorpses"); }, false);
            VoiceMode = VoiceModes.FromSetting(Read("ANPCSounds", delegate { return settings.GetInt(SoundsSection, "ANPCSounds"); }, VoiceModes.Anpc));
            Footsteps = Read("Footsteps", delegate { return settings.GetBool(SoundsSection, "Footsteps"); }, true);
            if (MissingKeys.Count > 0)
                AdvancedNpcsMod.LogError("Settings without " + string.Join(", ", MissingKeys.ToArray()) + " (modsettings.json out of date); using defaults for them.");
            FromSettings = true;
            AdvancedNpcsMod.Log("Settings: generic people " + Mode + ", at most " + MaxGenericPerTown + " per town; dungeons " + Dungeons +
                ", interiors " + Interiors + ", wilderness " + Wilderness + " (at most " + MaxWildernessAround + " around you); death animation " +
                DeathAnimation + ", directional corpses " + DirectionalCorpses + ", ANPC sounds " + VoiceModeName(VoiceMode) + ", footsteps " + Footsteps + ".");
        }

        public static string VoiceModeName(int mode)
        {
            return mode == VoiceModes.Vanilla ? "Vanilla" : mode == VoiceModes.Off ? "Off" : "ANPC";
        }

        /// <summary>Setting keys the mod reads that were not in its settings (should stay empty; checked by the self-test).</summary>
        public readonly System.Collections.Generic.List<string> MissingKeys = new System.Collections.Generic.List<string>();

        T Read<T>(string key, System.Func<T> get, T fallback)
        {
            try
            {
                return get();
            }
            catch (System.Exception)
            {
                MissingKeys.Add(key);
                return fallback;
            }
        }
    }
}

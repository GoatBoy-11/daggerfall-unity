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
        public const string WildlifeSection = "Wildlife";
        /// <summary>Animals (ANPCs with an "animal" block) appear in the wilderness (spec animal-ai 1.4).</summary>
        public bool Wildlife = true;
        /// <summary>Most wild animals alive around the player; they do not count toward MaxWildernessAround.</summary>
        public int MaxWildlifeAround = 10;
        /// <summary>WildlifeDensity choice: 0 Low (x0.5 chance), 1 Normal, 2 High (x2).</summary>
        public int WildlifeDensity = 1;
        /// <summary>The density setting as a multiplier on a wilderness block's chance.</summary>
        public float WildlifeChanceFactor { get { return WildlifeDensity == 0 ? 0.5f : WildlifeDensity == 2 ? 2f : 1f; } }

        public const string CompanionsSection="Companions";
        /// <summary>Actual limited contract count; 0 means unlimited.</summary>
        public int MaxFollowers=CompanionLimits.Default;
        public bool DisableFollowerBarks;
        /// <summary>Metres behind the player at which a following companion catches up (FollowRecovery).</summary>
        public int CatchUpDistance = FollowRecovery.DefaultDistance;

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
        /// <summary>Footstep volume in percent (0-100), times DFU's sound volume.</summary>
        public int StepsVolume = 100;

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
            Wildlife = Read("Wildlife", delegate { return settings.GetBool(WildlifeSection, "Wildlife"); }, true);
            MaxWildlifeAround = Mathf.Clamp(Read("MaxWildlifeAround", delegate { return settings.GetInt(WildlifeSection, "MaxWildlifeAround"); }, 10), 0, 30);
            WildlifeDensity = Mathf.Clamp(Read("WildlifeDensity", delegate { return settings.GetInt(WildlifeSection, "WildlifeDensity"); }, 1), 0, 2);
            MaxFollowers=CompanionLimits.FromSetting(Read("MaxFollowers",delegate {return settings.GetInt(CompanionsSection,"MaxFollowers");},0));
            DisableFollowerBarks=Read("DisableFollowerBarks",delegate {return settings.GetBool(CompanionsSection,"DisableFollowerBarks");},false);
            CatchUpDistance=Mathf.Clamp(Read("CatchUpDistance",delegate {return settings.GetInt(CompanionsSection,"CatchUpDistance");},FollowRecovery.DefaultDistance),
                FollowRecovery.MinDistance,FollowRecovery.MaxDistance);
            DeathAnimation = Read("DeathAnimation", delegate { return settings.GetBool(CorpsesSection, "DeathAnimation"); }, true);
            DirectionalCorpses = Read("DirectionalCorpses", delegate { return settings.GetBool(CorpsesSection, "DirectionalCorpses"); }, false);
            VoiceMode = VoiceModes.FromSetting(Read("ANPCSounds", delegate { return settings.GetInt(SoundsSection, "ANPCSounds"); }, VoiceModes.Anpc));
            Footsteps = Read("Footsteps", delegate { return settings.GetBool(SoundsSection, "Footsteps"); }, true);
            StepsVolume = Mathf.Clamp(Read("StepsVolume", delegate { return settings.GetInt(SoundsSection, "StepsVolume"); }, 100), 0, 100);
            if (MissingKeys.Count > 0)
                AdvancedNpcsMod.LogError("Settings without " + string.Join(", ", MissingKeys.ToArray()) + " (modsettings.json out of date); using defaults for them.");
            FromSettings = true;
            AdvancedNpcsMod.Log("Settings: generic people " + Mode + ", at most " + MaxGenericPerTown + " per town; dungeons " + Dungeons +
                ", interiors " + Interiors + ", wilderness " + Wilderness + " (at most " + MaxWildernessAround + " around you); wildlife " + Wildlife + " (at most " +
                MaxWildlifeAround + ", density x" + WildlifeChanceFactor + "); death animation " +
                DeathAnimation + ", directional corpses " + DirectionalCorpses + ", ANPC sounds " + VoiceModeName(VoiceMode) + ", footsteps " + Footsteps +
                " (volume " + StepsVolume + "%), follower limit " + (MaxFollowers==0?"Unlimited (Not Recommended)":MaxFollowers.ToString()) + ", catch-up at " + CatchUpDistance + " m.");
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

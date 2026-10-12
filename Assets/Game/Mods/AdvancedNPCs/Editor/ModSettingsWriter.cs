using System.IO;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;

namespace AdvancedNPCs.EditorTools
{
    /// <summary>Writes modsettings.json (spec §8) with DFU's own serializer, so DFU shows a settings window for the mod.</summary>
    public static class ModSettingsWriter
    {
        public const string SettingsPath = "Assets/Game/Mods/AdvancedNPCs/modsettings.json";

        public static void Write()
        {
            // Make(path) of a file that does not exist returns empty settings.
            ModSettingsData data = ModSettingsData.Make("Assets/Game/Mods/AdvancedNPCs/__no_settings__.json");
            data.Version = "1.1";

            Section section = new Section();
            section.Name = "Population";
            section.Description = "Generic Advanced NPCs: townsfolk, and people in dungeons, buildings and the wilderness made from generic templates.";

            MultipleChoiceKey people = new MultipleChoiceKey();
            people.Name = "GenericPeople";
            people.Description = "Same people every visit: each town keeps its generic NPCs and what happens to them is saved. " +
                                 "Random each visit: re-rolled whenever a town loads; nothing about them is saved.";
            people.Options.Add("Same people every visit");
            people.Options.Add("Random each visit");
            people.Value = 0;
            section.Keys.Add(people);

            SliderIntKey max = new SliderIntKey();
            max.Name = "MaxGenericPerTown";
            max.Description = "Most generic NPCs in one town or one building (0 turns them off).";
            max.Min = 0;
            max.Max = 30;
            max.Value = 12;
            section.Keys.Add(max);

            section.Keys.Add(Toggle("Dungeons", "Generic ANPCs with a \"dungeons\" spawn block appear in dungeons."));
            section.Keys.Add(Toggle("Interiors", "Generic ANPCs with an \"interiors\" spawn block appear in taverns, guild halls, temples, shops and houses."));
            section.Keys.Add(Toggle("Wilderness", "Generic ANPCs with a \"wilderness\" spawn block appear around you in the wilderness."));

            SliderIntKey wild = new SliderIntKey();
            wild.Name = "MaxWildernessAround";
            wild.Description = "Most wilderness ANPCs around you at once (0 turns them off).";
            wild.Min = 0;
            wild.Max = 10;
            wild.Value = 4;
            section.Keys.Add(wild);

            data.Sections.Add(section);

            // Corpse and sound settings (spec corpse-sound settings); ModConfig reads them by these names. The version
            // stays 1.1: a player's saved values of the older keys still apply, the new keys start at these defaults.
            Section corpses = new Section();
            corpses.Name = "Corpses";
            corpses.Description = "Bodies of ANPCs with their own sprite sets.";
            corpses.Keys.Add(Toggle("DeathAnimation",
                "Play the death animation before the body lies still. Off: the body appears at once, as in vanilla Daggerfall."));
            corpses.Keys.Add(Toggle("DirectionalCorpses",
                "A body on its last death frame keeps showing the side you see it from. Off: it shows its front or back and always faces you, like vanilla corpses.",
                false));
            data.Sections.Add(corpses);

            Section sounds = new Section();
            sounds.Name = "Sounds";
            sounds.Description = "Voices and footsteps of ANPCs.";
            MultipleChoiceKey voices = new MultipleChoiceKey();
            voices.Name = "ANPCSounds";
            voices.Description = "ANPC: each ANPC's own Sounds folder, Daggerfall's voice where it has none. " +
                                 "Vanilla: Daggerfall's voice for every ANPC. Off: ANPCs make no voice sounds.";
            voices.Options.Add("Vanilla");      // order = VoiceModes values
            voices.Options.Add("ANPC");
            voices.Options.Add("Off");
            voices.Value = 1;
            sounds.Keys.Add(voices);
            sounds.Keys.Add(Toggle("Footsteps", "Footsteps from an ANPC's Sounds folder."));
            data.Sections.Add(sounds);

            data.Save(Path.GetFullPath(SettingsPath)); // Save imports by full path
        }

        static ToggleKey Toggle(string name, string description, bool value = true)
        {
            ToggleKey key = new ToggleKey();
            key.Name = name;
            key.Description = description;
            key.Value = value;
            return key;
        }
    }
}

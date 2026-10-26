using System.IO;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;

namespace AdvancedNPCs.EditorTools
{
    /// <summary>Writes modsettings.json (spec §8) with DFU's own serializer, so DFU shows a settings window for the mod.</summary>
    public static class ModSettingsWriter
    {
        public const string SettingsPath = "Assets/Game/Mods/AdvancedNPCs/modsettings.json";

        /// <summary>Longest tooltip row (characters); DFU's tooltip splits rows on \r only.</summary>
        public const int MaxTooltipRow = 80;

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
            people.Description = "Same people every visit: each town keeps its generic NPCs\r" +
                                 "and what happens to them is saved.\r" +
                                 "Random each visit: re-rolled whenever a town loads;\rnothing about them is saved.";
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
            section.Keys.Add(Toggle("Interiors", "Generic ANPCs with an \"interiors\" spawn block appear in taverns,\rguild halls, temples, shops and houses."));
            section.Keys.Add(Toggle("Wilderness", "Generic ANPCs with a \"wilderness\" spawn block appear\raround you in the wilderness."));

            SliderIntKey wild = new SliderIntKey();
            wild.Name = "MaxWildernessAround";
            wild.Description = "Most wilderness ANPCs around you at once (0 turns them off).";
            wild.Min = 0;
            wild.Max = 10;
            wild.Value = 4;
            section.Keys.Add(wild);

            data.Sections.Add(section);

            // Animals (spec animal-ai 1.4): their own cap, so wild animals never crowd out wilderness people.
            Section wildlife = new Section();
            wildlife.Name = "Wildlife";
            wildlife.Description = "Animal ANPCs (an \"animal\" block) in the wilderness.";
            wildlife.Keys.Add(Toggle("Wildlife", "Animals appear in the wilderness, alone or in herds and packs,\r" +
                "by climate, season and time of day. Farm animals are not affected."));
            SliderIntKey wildMax = new SliderIntKey();
            wildMax.Name = "MaxWildlifeAround";
            wildMax.Description = "Most wild animals alive around you at once (0 turns them off).\r" +
                "They do not count toward the wilderness ANPC limit.";
            wildMax.Min = 0;
            wildMax.Max = 30;
            wildMax.Value = 10;
            wildlife.Keys.Add(wildMax);
            MultipleChoiceKey density = new MultipleChoiceKey();
            density.Name = "WildlifeDensity";
            density.Description = "How often animals turn up in the wilderness.\r" +
                "Low: half as often. High: twice as often.";
            density.Options.Add("Low");
            density.Options.Add("Normal");
            density.Options.Add("High");
            density.Value = 1;
            wildlife.Keys.Add(density);
            data.Sections.Add(wildlife);

            Section companions=new Section();companions.Name="Companions";
            companions.Description="Recruitment, protection, healing potions and catch-up for hired ANPC companions.";
            companions.Keys.Add(Toggle("InvulnerableCompanions",
                "Recruited companions cannot lose health while following or waiting.\rApplies immediately to existing companions. Dismissed NPCs are not protected.\rOff by default. Recruited companions always have 1.5x normal maximum health.",false));
            MultipleChoiceKey followers=new MultipleChoiceKey();followers.Name="MaxFollowers";
            followers.Description="Maximum hired companions, including those waiting elsewhere.\r" +
                "Escort NPCs with no_companion_slot do not count.\r" +
                "Lowering this limit keeps your existing party.\rUnlimited parties may reduce performance.";
            for(int count=3;count<=10;count++)followers.Options.Add(count.ToString());
            followers.Options.Add("Unlimited (Not Recommended)");followers.Value=0;
            companions.Keys.Add(followers);
            SliderIntKey catchUp=new SliderIntKey();catchUp.Name="CatchUpDistance";
            catchUp.Description="Metres a following companion may fall behind before it catches up.\r" +
                "It reappears close to you, behind you if possible, on a spot you cannot see.\r" +
                "It also catches up after 5 seconds farther than 6 m without closing in.\r" +
                "Waiting, fighting or fleeing companions never catch up.";
            catchUp.Min=AdvancedNPCs.Core.FollowRecovery.MinDistance;catchUp.Max=AdvancedNPCs.Core.FollowRecovery.MaxDistance;
            catchUp.Value=AdvancedNPCs.Core.FollowRecovery.DefaultDistance;
            companions.Keys.Add(catchUp);
            companions.Keys.Add(Toggle("DisableFollowerBarks",
                "Disable follower barks.\rSilences cm_ companion speech and hides the individual bark option.\rCombat voices and footsteps keep their own settings.",false));
            companions.Keys.Add(Toggle("AutoHealingPotions",
                "Companions drink their own cheapest healing potion below the health threshold.\rEach potion restores 25% of maximum health, up to full health."));
            SliderIntKey potionThreshold=new SliderIntKey();potionThreshold.Name="HealingPotionThreshold";
            potionThreshold.Description="Use a healing potion when health is below this percentage.\rRequires AutoHealingPotions. Default: 50%.";
            potionThreshold.Min=1;potionThreshold.Max=99;potionThreshold.Value=50;
            companions.Keys.Add(potionThreshold);
            data.Sections.Add(companions);

            // Corpse and sound settings (spec corpse-sound settings); ModConfig reads them by these names. The version
            // stays 1.1: a player's saved values of the older keys still apply, the new keys start at these defaults.
            Section corpses = new Section();
            corpses.Name = "Corpses";
            corpses.Description = "Bodies of ANPCs with their own sprite sets.";
            corpses.Keys.Add(Toggle("DeathAnimation",
                "Play the death animation before the body lies still.\rOff: the body appears at once, as in vanilla Daggerfall."));
            corpses.Keys.Add(Toggle("DirectionalCorpses",
                "A body on its last death frame keeps showing the side you see it from.\rOff: it shows its front or back and always faces you, like vanilla corpses.",
                false));
            data.Sections.Add(corpses);

            Section combat = new Section();
            combat.Name = "Combat";
            combat.Description = "How ANPCs with their own sprite sets look in a fight.";
            combat.Keys.Add(Toggle("BlockAnimations",
                "Play a block animation when an attack on an ANPC does no damage\r" +
                "(a miss or parry, or a full shield block with Physical Combat Overhaul).\r" +
                "Only plays if the ANPC has a block animation (block* sheet)."));
            data.Sections.Add(combat);

            Section sounds = new Section();
            sounds.Name = "Sounds";
            sounds.Description = "Voices and footsteps of ANPCs.";
            MultipleChoiceKey voices = new MultipleChoiceKey();
            voices.Name = "ANPCSounds";
            voices.Description = "ANPC: each ANPC's own Sounds folder, Daggerfall's voice where it has none.\r" +
                                 "Vanilla: Daggerfall's voice for every ANPC.\r" +
                                 "Off: ANPCs make no voice sounds.";
            voices.Options.Add("Vanilla");      // order = VoiceModes values
            voices.Options.Add("ANPC");
            voices.Options.Add("Off");
            voices.Value = 1;
            sounds.Keys.Add(voices);
            sounds.Keys.Add(Toggle("Footsteps", "Play ANPC footsteps"));
            SliderIntKey stepsVolume = new SliderIntKey();
            stepsVolume.Name = "StepsVolume";
            stepsVolume.Description = "Volume of ANPC footsteps, in percent of the sound volume.";
            stepsVolume.Min = 0;
            stepsVolume.Max = 100;
            stepsVolume.Value = 100;
            sounds.Keys.Add(stepsVolume);
            data.Sections.Add(sounds);

            // DFU's tooltips never wrap; a longer row is cut off on screen (user rule: break long tooltips into rows).
            foreach (Section s in data.Sections)
                foreach (Key k in s.Keys)
                    foreach (string row in k.Description.Split('\r'))
                        if (row.Length > MaxTooltipRow)
                            throw new System.InvalidOperationException("Tooltip row of " + k.Name + " is " + row.Length +
                                " characters (most " + MaxTooltipRow + "); break it into rows: " + row);

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

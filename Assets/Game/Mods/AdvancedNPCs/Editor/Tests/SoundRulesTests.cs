using System.Collections.Generic;
using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    /// <summary>Custom ANPC sounds: pool names, settings, pitch, step frames (spec sounds).</summary>
    public class SoundRulesTests
    {
        static Dictionary<string, object> Root(string json)
        {
            return (Dictionary<string, object>)Json.Parse(json);
        }

        [Test]
        public void PoolOf_StripsTheNumberSuffix()
        {
            Assert.AreEqual(SoundPools.Attack, SoundPools.PoolOf("attack_3"));
            Assert.AreEqual(SoundPools.AttackRange, SoundPools.PoolOf("attack_range_2"));
            Assert.AreEqual(SoundPools.AttackRange, SoundPools.PoolOf("attack_range"));
            Assert.AreEqual(SoundPools.Greet, SoundPools.PoolOf("greet"));
            Assert.AreEqual(SoundPools.TalkMale, SoundPools.PoolOf("talk_male_3"));
            Assert.AreEqual(SoundPools.TalkFemale, SoundPools.PoolOf("talk_female"));
            Assert.AreEqual(SoundPools.Step, SoundPools.PoolOf("step_10"));
            Assert.AreEqual(SoundPools.Death, SoundPools.PoolOf("Death_1"));      // case does not matter
        }

        [Test]
        public void PoolOf_UnknownNamesAreNull()
        {
            Assert.IsNull(SoundPools.PoolOf("foo_1"));
            Assert.IsNull(SoundPools.PoolOf("idle"));
            Assert.IsNull(SoundPools.PoolOf("attack_"));
            Assert.IsNull(SoundPools.PoolOf("attack_x"));
            Assert.IsNull(SoundPools.PoolOf(""));
        }

        [Test]
        public void Resolve_RangedFallsBackToAttack()
        {
            Assert.AreEqual(SoundPools.Attack, SoundPools.Resolve(SoundPools.AttackRange, new[] { SoundPools.Attack }));
            Assert.AreEqual(SoundPools.AttackRange, SoundPools.Resolve(SoundPools.AttackRange, new[] { SoundPools.Attack, SoundPools.AttackRange }));
            Assert.IsNull(SoundPools.Resolve(SoundPools.Hit, new[] { SoundPools.Attack }));
            Assert.IsNull(SoundPools.Resolve(SoundPools.Attack, new[] { SoundPools.AttackRange }));   // only ranged falls back
        }

        [Test]
        public void HealSoundsUseCombatChanceAndNeverFallBackToCastOrAttack()
        {
            Assert.AreEqual(SoundPools.Heal, SoundPools.PoolOf("heal"));
            Assert.AreEqual(SoundPools.Heal, SoundPools.PoolOf("Heal_2"));
            Assert.IsNull(SoundPools.PoolOf("heal_extra"));
            Assert.IsTrue(SoundPools.IsCombat(SoundPools.Heal));
            Assert.AreEqual(SoundPools.Heal, SoundPools.Resolve(SoundPools.Heal, new[] { SoundPools.Cast, SoundPools.Heal }));
            Assert.IsNull(SoundPools.Resolve(SoundPools.Heal, new[] { SoundPools.Cast, SoundPools.Attack }));
        }

        [Test]
        public void IsCombat_GroupsThePools()
        {
            Assert.IsTrue(SoundPools.IsCombat(SoundPools.Attack));
            Assert.IsTrue(SoundPools.IsCombat(SoundPools.AttackRange));
            Assert.IsTrue(SoundPools.IsCombat(SoundPools.Cast));
            Assert.IsTrue(SoundPools.IsCombat(SoundPools.Hit));
            Assert.IsFalse(SoundPools.IsCombat(SoundPools.Greet));
            Assert.IsFalse(SoundPools.IsCombat(SoundPools.Death));
            Assert.IsFalse(SoundPools.IsCombat(SoundPools.Step));
        }

        [Test]
        public void Read_Defaults()
        {
            SoundSettings s;
            string key;
            List<string> warnings = new List<string>();
            Assert.IsNull(SoundRules.Read(Root("{}"), out key, out s, warnings));
            Assert.AreEqual(50, s.CombatChance);
            Assert.AreEqual(50, s.GreetChance);
            Assert.AreEqual(15f, s.StepDistance);
            Assert.AreEqual(1f, s.SoundPitch);
            Assert.AreEqual(0f, s.SoundPitchVariation);
            Assert.AreEqual(1f, s.StepPitch);
            Assert.AreEqual(0f, s.StepPitchVariation);
            Assert.IsEmpty(warnings);
        }

        [Test]
        public void Read_AllSettings()
        {
            SoundSettings s;
            string key;
            List<string> warnings = new List<string>();
            Assert.IsNull(SoundRules.Read(Root("{ \"sounds\": { \"combatChance\": 30, \"greetChance\": 100, \"stepDistance\": 8, " +
                "\"soundPitch\": 0.8, \"soundPitchVariation\": 0.05, \"stepPitch\": 1.2, \"stepPitchVariation\": 0.1 } }"), out key, out s, warnings));
            Assert.AreEqual(30, s.CombatChance);
            Assert.AreEqual(100, s.GreetChance);
            Assert.AreEqual(8f, s.StepDistance);
            Assert.AreEqual(0.8f, s.SoundPitch, 1e-6);
            Assert.AreEqual(0.05f, s.SoundPitchVariation, 1e-6);
            Assert.AreEqual(1.2f, s.StepPitch, 1e-6);
            Assert.AreEqual(0.1f, s.StepPitchVariation, 1e-6);
        }

        [TestCase("{ \"sounds\": { \"combatChance\": 101 } }", "sounds.combatChance")]
        [TestCase("{ \"sounds\": { \"greetChance\": -1 } }", "sounds.greetChance")]
        [TestCase("{ \"sounds\": { \"stepDistance\": 0 } }", "sounds.stepDistance")]
        [TestCase("{ \"sounds\": { \"soundPitch\": 0.1 } }", "sounds.soundPitch")]
        [TestCase("{ \"sounds\": { \"stepPitch\": 4 } }", "sounds.stepPitch")]
        [TestCase("{ \"sounds\": { \"soundPitchVariation\": 1.5 } }", "sounds.soundPitchVariation")]
        [TestCase("{ \"sounds\": { \"stepPitchVariation\": \"lots\" } }", "sounds.stepPitchVariation")]
        [TestCase("{ \"sounds\": 5 }", "sounds")]
        public void Read_BadValuesFail(string json, string expectedKey)
        {
            SoundSettings s;
            string key;
            Assert.IsNotNull(SoundRules.Read(Root(json), out key, out s, new List<string>()));
            Assert.AreEqual(expectedKey, key);
        }

        [Test]
        public void Read_UnknownKeyWarns()
        {
            SoundSettings s;
            string key;
            List<string> warnings = new List<string>();
            Assert.IsNull(SoundRules.Read(Root("{ \"sounds\": { \"greetChanse\": 10 } }"), out key, out s, warnings));
            Assert.AreEqual(1, warnings.Count);
            StringAssert.Contains("greetChanse", warnings[0]);
        }

        [Test]
        public void Rolls_UsesThePercent()
        {
            Assert.IsFalse(SoundRules.Rolls(0, 0.0));
            Assert.IsTrue(SoundRules.Rolls(100, 0.999));
            Assert.IsTrue(SoundRules.Rolls(50, 0.49));
            Assert.IsFalse(SoundRules.Rolls(50, 0.5));
        }

        [Test]
        public void Rolls_HundredAlwaysAndZeroNever_AlsoAtTheEndsOfTheRoll()
        {
            // Unity's Random.value includes 1.
            Assert.IsTrue(SoundRules.Rolls(100, 1.0));
            Assert.IsTrue(SoundRules.Rolls(100, 0.0));
            Assert.IsFalse(SoundRules.Rolls(0, 1.0));
            Assert.IsFalse(SoundRules.Rolls(0, 0.0));
        }

        [Test]
        public void Pitch_VariesWithinTheRangeAndNeverReachesZero()
        {
            Assert.AreEqual(1f, SoundRules.Pitch(1f, 0f, 0.9), 1e-6);
            Assert.AreEqual(0.9f, SoundRules.Pitch(1f, 0.1f, 0.0), 1e-6);
            Assert.AreEqual(1.1f, SoundRules.Pitch(1f, 0.1f, 1.0), 1e-6);
            Assert.AreEqual(1f, SoundRules.Pitch(1f, 0.1f, 0.5), 1e-6);
            Assert.AreEqual(SoundRules.MinPitch, SoundRules.Pitch(0.25f, 1f, 0.0), 1e-6);
        }

        [Test]
        public void StepFrames_DefaultFirstAndMiddle()
        {
            CollectionAssert.AreEqual(new[] { 0, 4 }, SoundRules.StepFrames(new SpriteAnimation { Name = "walk", Frames = 8 }));
            CollectionAssert.AreEqual(new[] { 0, 1 }, SoundRules.StepFrames(new SpriteAnimation { Name = "walk", Frames = 3 }));
            CollectionAssert.AreEqual(new[] { 0 }, SoundRules.StepFrames(new SpriteAnimation { Name = "run", Frames = 1 }));
            CollectionAssert.AreEqual(new[] { 2, 6 }, SoundRules.StepFrames(new SpriteAnimation { Name = "run", Frames = 8, StepFrames = new[] { 2, 6 } }));
        }

        [Test]
        public void VanillaVoice_OnlyWithoutACustomPool()
        {
            Assert.IsTrue(SoundRules.VanillaVoice(SoundPools.Attack, false, false));
            Assert.IsFalse(SoundRules.VanillaVoice(SoundPools.Attack, true, false));
            Assert.IsTrue(SoundRules.VanillaVoice(SoundPools.Hit, false, false));
            Assert.IsFalse(SoundRules.VanillaVoice(SoundPools.Hit, true, false));
        }

        [Test]
        public void VanillaVoice_OnlyWhereDfuItselfVoices()
        {
            // DFU grunts on melee attacks and weapon hits only: never on bow shots, spells, damage over time, greetings or steps.
            Assert.IsFalse(SoundRules.VanillaVoice(SoundPools.AttackRange, false, false));
            Assert.IsFalse(SoundRules.VanillaVoice(SoundPools.Hit, false, true));
            Assert.IsFalse(SoundRules.VanillaVoice(SoundPools.Cast, false, false));
            Assert.IsFalse(SoundRules.VanillaVoice(SoundPools.Greet, false, false));
            Assert.IsFalse(SoundRules.VanillaVoice(SoundPools.Step, false, false));
            Assert.IsFalse(SoundRules.VanillaVoice(SoundPools.Death, false, false));
        }

        [Test]
        public void DefaultSteps_OnlyWithoutOwnStepFiles_AndNotOptedOut()
        {
            Assert.IsTrue(SoundRules.UseDefaultSteps(false, true));
            Assert.IsFalse(SoundRules.UseDefaultSteps(true, true));       // its own steps replace the defaults
            Assert.IsFalse(SoundRules.UseDefaultSteps(false, false));     // "defaultSteps": false
            Assert.IsFalse(SoundRules.UseDefaultSteps(true, false));
        }

        [Test]
        public void TalkPool_OwnGenderFirst_ThenPlainTalk()
        {
            string[] both = { SoundPools.TalkMale, SoundPools.TalkFemale };
            string[] all = { SoundPools.TalkMale, SoundPools.TalkFemale, SoundPools.Talk };
            Assert.AreEqual(SoundPools.TalkFemale, SoundPools.TalkPool("Female", both));
            Assert.AreEqual(SoundPools.TalkMale, SoundPools.TalkPool("Male", both));
            Assert.AreEqual(SoundPools.TalkMale, SoundPools.TalkPool("male", all));
            Assert.AreEqual(SoundPools.Talk, SoundPools.TalkPool("Male", new[] { SoundPools.TalkFemale, SoundPools.Talk }));
            Assert.AreEqual(SoundPools.Talk, SoundPools.TalkPool(null, all));
            Assert.IsNull(SoundPools.TalkPool("Male", new[] { SoundPools.TalkFemale }));     // never the other gender's voice
            Assert.IsNull(SoundPools.TalkPool("Female", new string[0]));
        }

        [Test]
        public void StepVolume_DefaultStepsQuieter_ScaledBySetting()
        {
            Assert.AreEqual(1f, SoundRules.StepVolume(false, 100), 1e-5f);
            Assert.AreEqual(0.85f, SoundRules.StepVolume(true, 100), 1e-5f);   // the shared _Sounds steps, 15% quieter
            Assert.AreEqual(0.5f, SoundRules.StepVolume(false, 50), 1e-5f);
            Assert.AreEqual(0.425f, SoundRules.StepVolume(true, 50), 1e-5f);
            Assert.AreEqual(0f, SoundRules.StepVolume(false, 0), 1e-5f);
            Assert.AreEqual(1f, SoundRules.StepVolume(false, 250), 1e-5f);     // clamped to 0-100
            Assert.AreEqual(0f, SoundRules.StepVolume(true, -10), 1e-5f);
        }

        [Test]
        public void Read_DefaultStepsIsOnUnlessTurnedOff()
        {
            string key;
            SoundSettings s;
            Assert.IsNull(SoundRules.Read(Root("{ }"), out key, out s, new List<string>()));
            Assert.IsTrue(s.DefaultSteps);
            Assert.IsNull(SoundRules.Read(Root("{ \"sounds\": { \"defaultSteps\": false } }"), out key, out s, new List<string>()));
            Assert.IsFalse(s.DefaultSteps);
            Assert.IsNotNull(SoundRules.Read(Root("{ \"sounds\": { \"defaultSteps\": \"no\" } }"), out key, out s, new List<string>()));
            Assert.AreEqual("sounds.defaultSteps", key);
            Assert.IsNull(SoundRules.Read(Root("{ \"sounds\": { \"defaultSteps\": null } }"), out key, out s, new List<string>()));
            Assert.IsTrue(s.DefaultSteps);      // null: the default, as for the other optional booleans
        }

        [Test]
        public void VoiceModes_FromTheSettingsChoice()
        {
            Assert.AreEqual(VoiceModes.Vanilla, VoiceModes.FromSetting(0));
            Assert.AreEqual(VoiceModes.Anpc, VoiceModes.FromSetting(1));
            Assert.AreEqual(VoiceModes.Off, VoiceModes.FromSetting(2));
            Assert.AreEqual(VoiceModes.Anpc, VoiceModes.FromSetting(7));      // unknown: the default
            Assert.AreEqual(VoiceModes.Anpc, VoiceModes.FromSetting(-1));
        }

        [Test]
        public void VoiceModes_WhoSpeaks()
        {
            // ANPC: the folder's sounds, DFU's own voice only for an ANPC without any.
            Assert.IsTrue(VoiceModes.CustomVoice(VoiceModes.Anpc));
            Assert.IsTrue(VoiceModes.DfuVoice(VoiceModes.Anpc, false));
            Assert.IsFalse(VoiceModes.DfuVoice(VoiceModes.Anpc, true));
            // Vanilla: DFU's voice for everyone, Sounds folders ignored.
            Assert.IsFalse(VoiceModes.CustomVoice(VoiceModes.Vanilla));
            Assert.IsTrue(VoiceModes.DfuVoice(VoiceModes.Vanilla, true));
            Assert.IsTrue(VoiceModes.DfuVoice(VoiceModes.Vanilla, false));
            // Off: no one.
            Assert.IsFalse(VoiceModes.CustomVoice(VoiceModes.Off));
            Assert.IsFalse(VoiceModes.DfuVoice(VoiceModes.Off, true));
            Assert.IsFalse(VoiceModes.DfuVoice(VoiceModes.Off, false));
        }

        [Test]
        public void VoiceModes_CreatureGrowlGivesWayToAttackSoundsOrSilence()
        {
            Assert.IsTrue(VoiceModes.SilenceCreatureAttack(VoiceModes.Anpc, true));
            Assert.IsFalse(VoiceModes.SilenceCreatureAttack(VoiceModes.Anpc, false));
            Assert.IsFalse(VoiceModes.SilenceCreatureAttack(VoiceModes.Vanilla, true));
            Assert.IsTrue(VoiceModes.SilenceCreatureAttack(VoiceModes.Off, false));
        }

        [Test]
        public void RetireDelay_OutlastsTheLongestClipAtTheLowestPitch()
        {
            Assert.AreEqual(0.5f, SoundRules.RetireDelay(0f), 1e-5);
            Assert.AreEqual(2f / SoundRules.MinPitch + 0.5f, SoundRules.RetireDelay(2f), 1e-4);
        }

        [Test]
        public void NextVoice_TakesTurns()
        {
            Assert.AreEqual(1, SoundRules.NextVoice(0, 3));
            Assert.AreEqual(2, SoundRules.NextVoice(1, 3));
            Assert.AreEqual(0, SoundRules.NextVoice(2, 3));
            Assert.AreEqual(0, SoundRules.NextVoice(0, 1));
        }

        const string SetStart = "{ \"pixelsPerUnit\": 10, \"cellHeight\": 32, \"groundY\": 4, \"fps\": 8, \"animations\": { " +
            "\"idle\": { \"cellWidth\": 16, \"frames\": 2 }, ";

        [Test]
        public void SpriteSet_StepFramesAreReadFromOne()
        {
            SpriteSetResult r = SpriteSetParser.Parse("x/sprites.json", SetStart + "\"walk_1\": { \"cellWidth\": 16, \"frames\": 8, \"stepFrames\": [3, 7] } } }");
            Assert.IsNull(r.Error, r.Error);
            CollectionAssert.AreEqual(new[] { 2, 6 }, r.Set.Animations["walk_1"].StepFrames);
            Assert.IsNull(r.Set.Animations["idle"].StepFrames);
        }

        [TestCase("\"walk\": { \"cellWidth\": 16, \"frames\": 8, \"stepFrames\": [0] }")]
        [TestCase("\"walk\": { \"cellWidth\": 16, \"frames\": 8, \"stepFrames\": [9] }")]
        [TestCase("\"walk\": { \"cellWidth\": 16, \"frames\": 8, \"stepFrames\": [1.5] }")]
        [TestCase("\"walk\": { \"cellWidth\": 16, \"frames\": 8, \"stepFrames\": 3 }")]
        [TestCase("\"attack\": { \"cellWidth\": 16, \"frames\": 8, \"stepFrames\": [1] }")]
        public void SpriteSet_BadStepFramesFail(string sheet)
        {
            SpriteSetResult r = SpriteSetParser.Parse("x/sprites.json", SetStart + sheet + " } }");
            Assert.IsNotNull(r.Error);
            StringAssert.Contains("stepFrames", r.Error);
        }

        [Test]
        public void Definition_ReadsTheSoundsBlock()
        {
            ParseResult r = DefinitionParser.ParseFolder("loud",
                "{ \"kind\": \"unique\", \"name\": \"A\", \"race\": \"Breton\", \"baseClass\": \"Warrior\", " +
                "\"location\": { \"region\": \"Daggerfall\", \"place\": \"Daggerfall\" }, \"position\": [0, 0, 0], " +
                "\"sounds\": { \"greetChance\": 80, \"stepPitch\": 1.1 } }");
            Assert.IsTrue(r.Ok, r.Error);
            Assert.AreEqual(80, r.Definition.Sounds.GreetChance);
            Assert.AreEqual(1.1f, r.Definition.Sounds.StepPitch, 1e-6);
            Assert.AreEqual(50, r.Definition.Sounds.CombatChance);
            foreach (string w in r.Warnings)
                StringAssert.DoesNotContain("sounds", w);
        }
    }
}

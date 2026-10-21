using System.Collections.Generic;
using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    /// <summary>Animal AI and wildlife spawning (spec animal-ai).</summary>
    public class AnimalRulesTests
    {
        static ParseResult Npc(string fields)
        {
            return DefinitionParser.ParseFolder("an_test", "{ \"kind\": \"generic\"" + (fields.Length > 0 ? ", " + fields : "") + " }");
        }

        static AnimalSettings Animal(string block)
        {
            ParseResult r = Npc("\"animal\": " + block);
            Assert.IsTrue(r.Ok, r.Error);
            return r.Definition.Animal;
        }

        static AnimalSettings Of(string temperament)
        {
            AnimalSettings s = new AnimalSettings();
            s.Temperament = temperament;
            AnimalRules.Defaults(s);
            return s;
        }

        // --- parsing

        [Test]
        public void NoBlock_NotAnAnimal()
        {
            Assert.IsNull(Npc("").Definition.Animal);
            Assert.IsNull(Npc("\"animal\": false").Definition.Animal);
        }

        [Test]
        public void True_IsGrazerWithDefaults()
        {
            AnimalSettings s = Animal("true");
            Assert.AreEqual(Temperaments.Grazer, s.Temperament);
            Assert.AreEqual(Activities.Always, s.Activity);
            Assert.AreEqual(10f, s.Alert);
            Assert.AreEqual(4f, s.React);
        }

        [TestCase("prey", 25f, 15f, 0f, 0)]
        [TestCase("territorial", 18f, 8f, 25f, 0)]
        [TestCase("predator", 30f, 10f, 40f, 10)]
        [TestCase("ambusher", 6f, 3f, 8f, 0)]
        [TestCase("ambient", 6f, 3f, 0f, 0)]
        public void Temperament_Defaults(string temperament, float alert, float react, float territory, int bold)
        {
            AnimalSettings s = Animal("{ \"temperament\": \"" + temperament + "\" }");
            Assert.AreEqual(alert, s.Alert);
            Assert.AreEqual(react, s.React);
            Assert.AreEqual(territory, s.Territory);
            Assert.AreEqual(bold, s.BoldChance);
        }

        [Test]
        public void Fields_Override()
        {
            AnimalSettings s = Animal("{ \"temperament\": \"Predator\", \"activity\": \"Nocturnal\", \"senses\": { \"alert\": 40, \"react\": 12 }, " +
                "\"territory\": 60, \"warnSeconds\": 2, \"boldChance\": 25, \"calls\": [30, 90], \"hunts\": [\"prey\", \"AN_Rabbit_*\"], " +
                "\"hungerHours\": [2, 6], \"herd\": false }");
            Assert.AreEqual(Temperaments.Predator, s.Temperament);
            Assert.AreEqual(Activities.Nocturnal, s.Activity);
            Assert.AreEqual(40f, s.Alert);
            Assert.AreEqual(12f, s.React);
            Assert.AreEqual(60f, s.Territory);
            Assert.AreEqual(2f, s.WarnSeconds);
            Assert.AreEqual(25, s.BoldChance);
            Assert.AreEqual(30f, s.CallMin);
            Assert.AreEqual(90f, s.CallMax);
            CollectionAssert.AreEqual(new[] { "prey", "an_rabbit_*" }, s.Hunts);
            Assert.AreEqual(2f, s.HungerMinHours);
            Assert.AreEqual(6f, s.HungerMaxHours);
            Assert.IsFalse(s.Herd);
        }

        [Test]
        public void SmallAlert_CapsTheTemperamentsReact()
        {
            AnimalSettings s = Animal("{ \"temperament\": \"prey\", \"senses\": { \"alert\": 10 } }");
            Assert.AreEqual(10f, s.Alert);
            Assert.AreEqual(10f, s.React);
        }

        [Test]
        public void CallsFalse_NeverCalls()
        {
            AnimalSettings s = Animal("{ \"calls\": false }");
            Assert.AreEqual(0f, AnimalRules.CallSeconds(s, 0.5));
        }

        [TestCase("{ \"temperament\": \"shy\" }", "animal.temperament: must be one of prey, grazer")]
        [TestCase("{ \"activity\": \"noon\" }", "animal.activity: must be one of diurnal")]
        [TestCase("{ \"senses\": { \"alert\": 5, \"react\": 9 } }", "animal.senses.react: must not be more than alert")]
        [TestCase("{ \"senses\": { \"alert\": 0 } }", "animal.senses.alert: must be a number from 1 to 100")]
        [TestCase("{ \"boldChance\": 101 }", "animal.boldChance: must be a whole number")]
        [TestCase("{ \"calls\": [2, 9] }", "animal.calls: must be false or [min, max]")]
        [TestCase("{ \"hungerHours\": [5, 2] }", "animal.hungerHours: must be [min, max]")]
        [TestCase("\"cow\"", "animal: must be true or an object")]
        public void BadBlock_Error(string block, string error)
        {
            ParseResult r = Npc("\"animal\": " + block);
            Assert.IsFalse(r.Ok);
            StringAssert.Contains(error, r.Error);
        }

        [Test]
        public void UnknownField_Warns()
        {
            ParseResult r = Npc("\"animal\": { \"temperment\": \"prey\" }");
            Assert.IsTrue(r.Ok, r.Error);
            Assert.IsTrue(r.Warnings.Exists(delegate (string w) { return w.Contains("animal.temperment: unknown field (did you mean \"temperament\"?)"); }),
                string.Join("\n", r.Warnings.ToArray()));
        }

        [Test]
        public void CalmCreature_OnlyForAnimalsWithSafeClasses()
        {
            Assert.IsTrue(Npc("\"baseClass\": \"GrizzlyBear\", \"animal\": { \"temperament\": \"territorial\" }").Ok);
            Assert.IsTrue(Npc("\"baseClass\": \"SabertoothTiger\", \"animal\": true").Ok);
            ParseResult were = Npc("\"baseClass\": \"Werewolf\", \"animal\": true");
            Assert.IsFalse(were.Ok);
            StringAssert.Contains("a calm animal may use Rat, GiantBat, GrizzlyBear", were.Error);
            Assert.IsFalse(Npc("\"baseClass\": \"GrizzlyBear\"").Ok, "a calm creature without an animal block");
        }

        [Test]
        public void HostileAnimal_Error()
        {
            ParseResult r = Npc("\"attitude\": \"hostile\", \"animal\": true");
            Assert.IsFalse(r.Ok);
            StringAssert.Contains("an animal decides when to attack", r.Error);
        }

        [Test]
        public void Animal_BraveryFromTemperament_UnlessGiven()
        {
            Assert.AreEqual(Bravery.Coward, Npc("\"animal\": { \"temperament\": \"prey\" }").Definition.Bravery);
            Assert.AreEqual(Bravery.Prudent, Npc("\"animal\": { \"temperament\": \"predator\" }").Definition.Bravery);
            Assert.AreEqual(Bravery.Brave, Npc("\"bravery\": \"Brave\", \"animal\": { \"temperament\": \"prey\" }").Definition.Bravery);
        }

        [Test]
        public void Animal_NoTalkingHiringOrGuarding()
        {
            ParseResult r = Npc("\"conversation\": true, \"recruitCost\": 50, \"animal\": true");
            Assert.IsTrue(r.Ok, r.Error);
            Assert.IsFalse(r.Definition.Conversation.Enabled);
            Assert.AreEqual(-1, r.Definition.RecruitCost);
            Assert.AreEqual(2, r.Warnings.FindAll(delegate (string w) { return w.Contains("animals"); }).Count);
            Assert.IsFalse(Npc("\"guard\": true, \"animal\": true").Ok);
        }

        // --- senses

        [Test]
        public void Sense_SneakRunStealthRestConcealed()
        {
            Assert.AreEqual(20f, AnimalRules.Sense(20, false, false, 0, false, false, false, 3), 1e-4);
            Assert.AreEqual(10f, AnimalRules.Sense(20, true, false, 0, false, false, false, 3), 1e-4);
            Assert.AreEqual(30f, AnimalRules.Sense(20, false, true, 0, false, false, false, 3), 1e-4);
            Assert.AreEqual(10f, AnimalRules.Sense(20, false, false, 100, false, false, false, 3), 1e-4);
            Assert.AreEqual(15f, AnimalRules.Sense(20, false, false, 50, false, false, false, 3), 1e-4);
            Assert.AreEqual(10f, AnimalRules.Sense(20, false, false, 0, true, false, false, 3), 1e-4);
            Assert.AreEqual(14f, AnimalRules.Sense(20, false, false, 0, false, true, false, 3), 1e-4);
            Assert.AreEqual(5f, AnimalRules.Sense(20, false, false, 0, false, false, true, 3), 1e-4);
            Assert.AreEqual(3f, AnimalRules.Sense(20, true, false, 100, true, true, true, 3), 1e-4, "never below min");
        }

        [Test]
        public void Notices_SeenFull_HeardFortyPercent()
        {
            Assert.IsTrue(AnimalRules.Notices(20, 25, true));
            Assert.IsFalse(AnimalRules.Notices(20, 25, false));
            Assert.IsTrue(AnimalRules.Notices(10, 25, false));
        }

        // --- reactions

        [TestCase("prey", AnimalStates.Flee)]
        [TestCase("grazer", AnimalStates.Shy)]
        [TestCase("ambient", AnimalStates.Shy)]
        [TestCase("territorial", AnimalStates.Warning)]
        [TestCase("predator", AnimalStates.Attack)]
        [TestCase("ambusher", AnimalStates.Attack)]
        public void Close_ActsByTemperament(string temperament, int expected)
        {
            AnimalSettings s = Of(temperament);
            Assert.AreEqual(expected, AnimalRules.Next(s, AnimalStates.Unaware, s.React - 0.5f, s.Alert, s.React, true, 0, false, false));
        }

        [TestCase("prey", AnimalStates.Alert)]
        [TestCase("grazer", AnimalStates.Alert)]
        [TestCase("territorial", AnimalStates.Alert)]
        [TestCase("predator", AnimalStates.Alert)]
        [TestCase("ambusher", AnimalStates.Unaware)]
        [TestCase("ambient", AnimalStates.Unaware)]
        public void Noticed_OutsideReact(string temperament, int expected)
        {
            AnimalSettings s = Of(temperament);
            Assert.AreEqual(expected, AnimalRules.Next(s, AnimalStates.Unaware, s.React + 1f, s.Alert, s.React, true, 0, false, false));
        }

        [Test]
        public void NotNoticed_InsideReact_ActsOnlyWhenHeard()
        {
            AnimalSettings s = Of(Temperaments.Prey);      // 25 / 15: heard within 10 m
            Assert.AreEqual(AnimalStates.Flee, AnimalRules.Next(s, AnimalStates.Unaware, 14f, 25f, 15f, false, 0, false, false),
                "inside react range it hears the player even unseen");
            AnimalSettings bear = Of(Temperaments.Territorial);   // 18 / 8: heard within 7.2 m, react 8
            Assert.AreEqual(AnimalStates.Warning, AnimalRules.Next(bear, AnimalStates.Unaware, 7.9f, 18f, 8f, false, 0, false, false));
        }

        [Test]
        public void Predator_BoldRoll_AttacksOnNoticing()
        {
            AnimalSettings s = Of(Temperaments.Predator);
            Assert.AreEqual(AnimalStates.Attack, AnimalRules.Next(s, AnimalStates.Unaware, 25f, 30f, 10f, true, 0, true, false));
        }

        [Test]
        public void Warning_ThenAttackIfStillClose_ElseBack()
        {
            AnimalSettings s = Of(Temperaments.Territorial);   // 18 / 8, warn 3 s
            Assert.AreEqual(AnimalStates.Warning, AnimalRules.Next(s, AnimalStates.Warning, 7f, 18f, 8f, true, 1f, false, false));
            Assert.AreEqual(AnimalStates.Attack, AnimalRules.Next(s, AnimalStates.Warning, 7f, 18f, 8f, true, 3f, false, false));
            Assert.AreEqual(AnimalStates.Alert, AnimalRules.Next(s, AnimalStates.Warning, 12f, 18f, 8f, true, 3f, false, false));
            Assert.AreEqual(AnimalStates.Attack, AnimalRules.Next(s, AnimalStates.Warning, 3.9f, 18f, 8f, true, 0.5f, false, false), "too close at once");
            Assert.AreEqual(AnimalStates.Unaware, AnimalRules.Next(s, AnimalStates.Warning, 30f, 18f, 8f, false, 1f, false, false));
        }

        [Test]
        public void Grazer_LooksThenGoesBackToGrazing_AndStaysHabituated()
        {
            AnimalSettings s = Of(Temperaments.Grazer);
            Assert.AreEqual(AnimalStates.Alert, AnimalRules.Next(s, AnimalStates.Alert, 8f, 10f, 4f, true, 2f, false, false));
            Assert.AreEqual(AnimalStates.Unaware, AnimalRules.Next(s, AnimalStates.Alert, 8f, 10f, 4f, true, AnimalRules.LookSeconds, false, false));
            Assert.AreEqual(AnimalStates.Unaware, AnimalRules.Next(s, AnimalStates.Unaware, 8f, 10f, 4f, true, 0f, false, true));
            Assert.AreEqual(AnimalStates.Shy, AnimalRules.Next(s, AnimalStates.Unaware, 3f, 10f, 4f, true, 0f, false, true), "habituated still shies when walked into");
        }

        [Test]
        public void Alert_RelaxesPastAlertRange()
        {
            AnimalSettings s = Of(Temperaments.Prey);
            Assert.AreEqual(AnimalStates.Alert, AnimalRules.Next(s, AnimalStates.Alert, 32f, 25f, 15f, false, 1f, false, false), "not before 2 s");
            Assert.AreEqual(AnimalStates.Unaware, AnimalRules.Next(s, AnimalStates.Alert, 32f, 25f, 15f, false, 3f, false, false));
            Assert.AreEqual(AnimalStates.Alert, AnimalRules.Next(s, AnimalStates.Alert, 28f, 25f, 15f, false, 3f, false, false), "inside the 1.25 margin");
        }

        [Test]
        public void FleeDone_FarOrHidden_ShyShort()
        {
            Assert.IsFalse(AnimalRules.FleeDone(AnimalStates.Flee, 20f, 15f, true, 0f));
            Assert.IsTrue(AnimalRules.FleeDone(AnimalStates.Flee, 36f, 15f, true, 0f));
            Assert.IsTrue(AnimalRules.FleeDone(AnimalStates.Flee, 31f, 15f, false, 5f));
            Assert.IsFalse(AnimalRules.FleeDone(AnimalStates.Flee, 31f, 15f, false, 2f));
            Assert.IsTrue(AnimalRules.FleeDone(AnimalStates.Shy, 8f, 4f, true, 0f));
            Assert.IsFalse(AnimalRules.FleeDone(AnimalStates.Shy, 5f, 2f, true, 0f), "at least 6 m");
        }

        [Test]
        public void GivesUp_PastTerritoryOrFar()
        {
            Assert.IsFalse(AnimalRules.GivesUp(20f, 25f, 5f, 18f));
            Assert.IsTrue(AnimalRules.GivesUp(26f, 25f, 5f, 18f));
            Assert.IsTrue(AnimalRules.GivesUp(5f, 25f, 40f, 18f));
            Assert.IsFalse(AnimalRules.GivesUp(100f, 0f, 30f, 25f), "no territory: only losing the target");
        }

        [Test]
        public void Hunts_ByTemperamentIdOrPrefix()
        {
            AnimalSettings wolf = Of(Temperaments.Predator);
            wolf.Hunts.Add("prey");
            wolf.Hunts.Add("an_hen_*");
            wolf.Hunts.Add("an_goat");
            Assert.IsTrue(AnimalRules.Hunts(wolf, "an_hind", Temperaments.Prey));
            Assert.IsTrue(AnimalRules.Hunts(wolf, "an_hen_barred", Temperaments.Ambient));
            Assert.IsTrue(AnimalRules.Hunts(wolf, "AN_GOAT", Temperaments.Grazer));
            Assert.IsFalse(AnimalRules.Hunts(wolf, "an_cow", Temperaments.Grazer));
            Assert.IsFalse(AnimalRules.Hunts(Of(Temperaments.Predator), "an_hind", Temperaments.Prey), "an empty list hunts nothing");
        }

        [Test]
        public void Hunger_AndCalls_InRange()
        {
            AnimalSettings s = Of(Temperaments.Predator);
            Assert.AreEqual(4 * 3600.0, AnimalRules.HungerSeconds(s, 0), 1e-6);
            Assert.AreEqual(10 * 3600.0, AnimalRules.HungerSeconds(s, 1), 1e-6);
            Assert.AreEqual(45f, AnimalRules.CallSeconds(s, 0), 1e-4);
            Assert.AreEqual(150f, AnimalRules.CallSeconds(s, 1), 1e-4);
            Assert.AreEqual(0f, AnimalRules.CallSeconds(Of(Temperaments.Ambusher), 0.5), "ambushers keep quiet");
        }

        [TestCase("diurnal", 6, true)]
        [TestCase("diurnal", 20, false)]
        [TestCase("nocturnal", 23, true)]
        [TestCase("nocturnal", 3, true)]
        [TestCase("nocturnal", 12, false)]
        [TestCase("crepuscular", 5, true)]
        [TestCase("crepuscular", 12, false)]
        [TestCase("crepuscular", 21, true)]
        [TestCase("always", 13, true)]
        public void Active_Hours(string activity, int hour, bool expected)
        {
            Assert.AreEqual(expected, Activities.Active(activity, hour));
        }

        [Test]
        public void Bravery_FightersPrudent_OthersCoward()
        {
            Assert.AreEqual(Bravery.Prudent, AnimalRules.BraveryOf(Temperaments.Territorial));
            Assert.AreEqual(Bravery.Prudent, AnimalRules.BraveryOf(Temperaments.Ambusher));
            Assert.AreEqual(Bravery.Coward, AnimalRules.BraveryOf(Temperaments.Grazer));
        }

        // --- wilderness groups and terrain

        [Test]
        public void Wilderness_GroupAndTerrain()
        {
            ParseResult r = Npc("\"spawn\": { \"wilderness\": { \"chance\": 20, \"max\": 12, \"group\": [3, 6], \"terrain\": \"near water\" } }");
            Assert.IsTrue(r.Ok, r.Error);
            Assert.AreEqual(0, r.Warnings.Count, string.Join("\n", r.Warnings.ToArray()));
            WildernessSpawn w = r.Definition.Spawn.Wilderness;
            Assert.AreEqual(12, w.Max);
            Assert.AreEqual(3, w.GroupMin);
            Assert.AreEqual(6, w.GroupMax);
            Assert.AreEqual(TerrainRules.NearWater, w.Terrain);
            WildernessSpawn plain = Npc("\"spawn\": { \"wilderness\": { \"chance\": 20 } }").Definition.Spawn.Wilderness;
            Assert.AreEqual(1, plain.GroupMax);
            Assert.AreEqual(TerrainRules.Dry, plain.Terrain);
        }

        [TestCase("\"group\": [0, 2]", "spawn.wilderness.group: must be [min, max]")]
        [TestCase("\"group\": [3, 9]", "spawn.wilderness.group: must be [min, max]")]
        [TestCase("\"terrain\": \"sand\"", "spawn.wilderness.terrain: must be one of dry, grass, rocky, nearWater")]
        public void Wilderness_BadGroupOrTerrain_BlockIgnored(string field, string message)
        {
            ParseResult r = Npc("\"spawn\": { \"wilderness\": { \"chance\": 20, " + field + " } }");
            Assert.IsTrue(r.Ok, r.Error);
            Assert.IsNull(r.Definition.Spawn.Wilderness);
            Assert.IsTrue(r.Warnings.Exists(delegate (string w) { return w.Contains(message); }), string.Join("\n", r.Warnings.ToArray()));
        }

        [Test]
        public void Terrain_Tiles()
        {
            Assert.IsTrue(TerrainRules.Accepts(TerrainRules.Dry, 2, false));
            Assert.IsFalse(TerrainRules.Accepts(TerrainRules.Dry, 0, true), "never water");
            Assert.IsFalse(TerrainRules.Accepts(TerrainRules.Dry, 7, false), "blend tiles are not dry ground");
            Assert.IsTrue(TerrainRules.Accepts(TerrainRules.Grass, 2, false));
            Assert.IsFalse(TerrainRules.Accepts(TerrainRules.Grass, 1, false));
            Assert.IsTrue(TerrainRules.Accepts(TerrainRules.Rocky, 3, false));
            Assert.IsTrue(TerrainRules.Accepts(TerrainRules.NearWater, 1, true));
            Assert.IsFalse(TerrainRules.Accepts(TerrainRules.NearWater, 1, false));
            Assert.AreEqual(TerrainRules.NearWater, TerrainRules.Canonical("Near_Water"));
            Assert.IsNull(TerrainRules.Canonical("mud"));
        }

        [Test]
        public void GroupSize_CutToRoom()
        {
            Assert.AreEqual(3, TerrainRules.GroupSize(3, 6, 0.0, 10, 10));
            Assert.AreEqual(6, TerrainRules.GroupSize(3, 6, 1.0, 10, 10));
            Assert.AreEqual(4, TerrainRules.GroupSize(3, 6, 1.0, 4, 10), "template max");
            Assert.AreEqual(3, TerrainRules.GroupSize(3, 6, 1.0, 10, 3), "overall cap");
            Assert.AreEqual(0, TerrainRules.GroupSize(3, 6, 1.0, 2, 10), "no room for the smallest group");
            Assert.AreEqual(1, TerrainRules.GroupSize(1, 1, 0.5, 1, 1));
        }

        // --- climate condition

        [Test]
        public void Climate_Condition()
        {
            List<string> messages = new List<string>();
            Condition c = ConditionParser.Parse((Dictionary<string, object>)Json.Parse("{ \"climate\": [\"Woodlands\", \"Mountain Woods\"] }"), "f", messages, false);
            Assert.AreEqual(0, messages.Count, string.Join("\n", messages.ToArray()));
            Assert.IsTrue(c.Holds(new FakeFacts { ClimateValue = "woodlands" }));
            Assert.IsTrue(c.Holds(new FakeFacts { ClimateValue = "mountainwoods" }));
            Assert.IsFalse(c.Holds(new FakeFacts { ClimateValue = "desert" }));
            ConditionParser.Parse((Dictionary<string, object>)Json.Parse("{ \"climate\": \"tundra\" }"), "f", messages, false);
            Assert.AreEqual(1, messages.Count);
            StringAssert.Contains("is not one of ocean, desert", messages[0]);
        }

        [Test]
        public void ClimateNames_FromDfuIndex()
        {
            Assert.AreEqual("ocean", ClimateNames.FromIndex(223));
            Assert.AreEqual("woodlands", ClimateNames.FromIndex(231));
            Assert.AreEqual("hauntedwoodlands", ClimateNames.FromIndex(232));
            Assert.AreEqual("", ClimateNames.FromIndex(0));
        }

        // --- disease and sounds

        [Test]
        public void Disease_Parses()
        {
            ParseResult r = Npc("\"disease\": { \"chance\": 20, \"diseases\": [\"Plague\", \"stomach rot\"] }");
            Assert.IsTrue(r.Ok, r.Error);
            Assert.AreEqual(20, r.Definition.Disease.Chance);
            CollectionAssert.AreEqual(new[] { 1, 3 }, r.Definition.Disease.Diseases);
            Assert.AreEqual(5, Npc("\"disease\": { \"diseases\": \"Swamp Rot\" }").Definition.Disease.Chance);
            Assert.IsNull(Npc("").Definition.Disease);
        }

        [TestCase("{ \"chance\": 5 }", "disease.diseases: required")]
        [TestCase("{ \"diseases\": [\"Flu\"] }", "disease.diseases: unknown disease \"Flu\"")]
        [TestCase("{ \"chance\": 150, \"diseases\": [\"Plague\"] }", "disease.chance: must be a whole number")]
        public void Disease_Errors(string block, string error)
        {
            ParseResult r = Npc("\"disease\": " + block);
            Assert.IsFalse(r.Ok);
            StringAssert.Contains(error, r.Error);
        }

        [Test]
        public void AnimalSounds_FallBackToGreet()
        {
            List<string> greetOnly = new List<string> { SoundPools.Greet };
            Assert.AreEqual(SoundPools.Greet, SoundPools.Resolve(SoundPools.Alert, greetOnly));
            Assert.AreEqual(SoundPools.Greet, SoundPools.Resolve(SoundPools.Warn, greetOnly));
            Assert.AreEqual(SoundPools.Greet, SoundPools.Resolve(SoundPools.Call, greetOnly));
            Assert.AreEqual(SoundPools.Call, SoundPools.Resolve(SoundPools.Call, new List<string> { SoundPools.Call, SoundPools.Greet }));
            Assert.AreEqual("call", SoundPools.PoolOf("call_2"));
        }
    }
}

using System.Collections.Generic;
using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class ConditionTests
    {
        readonly List<string> messages = new List<string>();

        Condition When(string json, bool allowTone = true)
        {
            messages.Clear();
            return ConditionParser.Parse((Dictionary<string, object>)Json.Parse(json), "f.json: topic \"T\"", messages, allowTone);
        }

        [TestCase(12, true)]
        [TestCase(18, false)]
        [TestCase(9, true)]
        [TestCase(8, false)]
        public void Hours_Plain(int hour, bool holds)
        {
            Assert.AreEqual(holds, When("{ \"hours\": [9, 18] }").Holds(new FakeFacts { HourValue = hour }));
        }

        [TestCase(23, true)]
        [TestCase(2, true)]
        [TestCase(6, false)]
        [TestCase(12, false)]
        public void Hours_WrapPastMidnight(int hour, bool holds)
        {
            Assert.AreEqual(holds, When("{ \"hours\": [20, 6] }").Holds(new FakeFacts { HourValue = hour }));
        }

        [Test]
        public void Hours_Invalid_IsIgnoredWithMessage()
        {
            Condition c = When("{ \"hours\": [9, 25] }");
            Assert.IsTrue(c.IsEmpty);
            StringAssert.Contains("f.json: topic \"T\": when: hours: must be [from, to]", messages[0]);
        }

        [Test]
        public void Time()
        {
            Condition night = When("{ \"time\": \"night\" }");
            Assert.IsTrue(night.Holds(new FakeFacts { Night = true }));
            Assert.IsFalse(night.Holds(new FakeFacts { Night = false }));
        }

        [Test]
        public void Season_FallMeansAutumn_ListIsAnyOf()
        {
            Condition c = When("{ \"season\": [\"Fall\", \"winter\"] }");
            Assert.IsTrue(c.Holds(new FakeFacts { SeasonValue = "autumn" }));
            Assert.IsTrue(c.Holds(new FakeFacts { SeasonValue = "winter" }));
            Assert.IsFalse(c.Holds(new FakeFacts { SeasonValue = "summer" }));
        }

        [Test]
        public void Weather_RainIncludesStorm()
        {
            Condition rain = When("{ \"weather\": \"rain\" }");
            Assert.IsTrue(rain.Holds(new FakeFacts { Rain = true }));
            Assert.IsTrue(rain.Holds(new FakeFacts { Storm = true }));
            Assert.IsFalse(rain.Holds(new FakeFacts { Snow = true }));
            Condition clear = When("{ \"weather\": \"clear\" }");
            Assert.IsTrue(clear.Holds(new FakeFacts()));
            Assert.IsFalse(clear.Holds(new FakeFacts { Clouds = true }));
        }

        [Test]
        public void RegionAndTown_IgnoreCase()
        {
            Assert.IsTrue(When("{ \"region\": \"daggerfall\" }").Holds(new FakeFacts()));
            Assert.IsTrue(When("{ \"town\": [\"Wayrest\", \" DAGGERFALL \"] }").Holds(new FakeFacts()));
            Assert.IsFalse(When("{ \"town\": \"Wayrest\" }").Holds(new FakeFacts()));
        }

        [Test]
        public void Levels()
        {
            Condition c = When("{ \"minLevel\": 5, \"maxLevel\": 10 }");
            Assert.IsFalse(c.Holds(new FakeFacts { Level = 4 }));
            Assert.IsTrue(c.Holds(new FakeFacts { Level = 5 }));
            Assert.IsTrue(c.Holds(new FakeFacts { Level = 10 }));
            Assert.IsFalse(c.Holds(new FakeFacts { Level = 11 }));
        }

        [Test]
        public void PlayerRace_SpacesIgnored_AndGender()
        {
            Assert.IsTrue(When("{ \"playerRace\": \"Dark Elf\" }").Holds(new FakeFacts { Race = "darkelf" }));
            Assert.IsFalse(When("{ \"playerRace\": \"Nord\" }").Holds(new FakeFacts()));
            Assert.IsTrue(When("{ \"playerGender\": \"Male\" }").Holds(new FakeFacts()));
            Assert.IsFalse(When("{ \"playerGender\": \"Female\" }").Holds(new FakeFacts()));
        }

        [Test]
        public void MinGold()
        {
            Condition c = When("{ \"minGold\": 500 }");
            Assert.IsFalse(c.Holds(new FakeFacts { GoldValue = 499 }));
            Assert.IsTrue(c.Holds(new FakeFacts { GoldValue = 500 }));
        }

        [Test]
        public void HasItem_ListIsAnyOf()
        {
            FakeFacts f = new FakeFacts();
            f.Items.Add("Ruby");
            Assert.IsTrue(When("{ \"hasItem\": [\"Emerald\", \"ruby\"] }").Holds(f));
            Assert.IsFalse(When("{ \"hasItem\": \"Emerald\" }").Holds(f));
        }

        [Test]
        public void Guild_AndMinRank()
        {
            FakeFacts f = new FakeFacts();
            f.Guilds["fightersguild"] = 3;
            Assert.IsTrue(When("{ \"guild\": \"Fighters Guild\" }").Holds(f));
            Assert.IsTrue(When("{ \"guild\": \"fighters_guild\", \"minGuildRank\": 3 }").Holds(f));
            Assert.IsFalse(When("{ \"guild\": \"Fighters Guild\", \"minGuildRank\": 4 }").Holds(f));
            Assert.IsFalse(When("{ \"guild\": \"Mages Guild\" }").Holds(f));
        }

        [Test]
        public void Guild_Unknown_IsIgnoredWithMessage()
        {
            Assert.IsTrue(When("{ \"guild\": \"Bakers Guild\" }").IsEmpty);
            StringAssert.Contains("unknown guild \"Bakers Guild\"", messages[0]);
        }

        [Test]
        public void MinGuildRank_WithoutGuild_Message()
        {
            When("{ \"minGuildRank\": 2 }");
            StringAssert.Contains("minGuildRank: needs \"guild\"", messages[0]);
        }

        [Test]
        public void Reaction_List()
        {
            Condition c = When("{ \"reaction\": [\"likes\", \"loves\"] }");
            Assert.IsTrue(c.Holds(new FakeFacts { ReactionValue = "loves" }));
            Assert.IsFalse(c.Holds(new FakeFacts { ReactionValue = "neutral" }));
        }

        [Test]
        public void Asked_AllOf_NotAsked_NoneOf_AcceptsCaptions()
        {
            FakeFacts f = new FakeFacts();
            f.AskedTopics.Add("any_rumours");
            Condition asked = When("{ \"asked\": \"Any rumours?\" }");
            Assert.IsTrue(asked.Holds(f));
            CollectionAssert.AreEqual(new[] { "any_rumours" }, asked.AskedIds);
            Assert.IsFalse(When("{ \"asked\": [\"any_rumours\", \"ale\"] }").Holds(f));
            Assert.IsFalse(When("{ \"notAsked\": [\"ale\", \"any_rumours\"] }").Holds(f));
            Assert.IsTrue(When("{ \"notAsked\": \"ale\" }").Holds(f));
        }

        [Test]
        public void Flags_AllOf_NotFlags_NoneOf()
        {
            FakeFacts f = new FakeFacts();
            f.Flags.Add("heard_sailor");
            Assert.IsTrue(When("{ \"flags\": \"Heard Sailor\" }").Holds(f));
            Assert.IsFalse(When("{ \"flags\": [\"heard_sailor\", \"found_sailor\"] }").Holds(f));
            Assert.IsFalse(When("{ \"notFlags\": \"heard_sailor\" }").Holds(f));
            Assert.IsTrue(When("{ \"notFlags\": \"found_sailor\" }").Holds(f));
        }

        [Test]
        public void QuestGlobal_NamesAndNumbers()
        {
            FakeFacts f = new FakeFacts();
            f.Globals.Add("LiftedCurse");
            f.Globals.Add("7");
            Condition c = When("{ \"questGlobal\": [\"LiftedCurse\", 7] }");
            Assert.IsTrue(c.Holds(f));
            CollectionAssert.AreEqual(new[] { "LiftedCurse" }, c.QuestGlobals);
            Assert.IsFalse(When("{ \"notQuestGlobal\": 7 }").Holds(f));
            Assert.IsTrue(When("{ \"questGlobal\": 70 }").IsEmpty);
        }

        [Test]
        public void Tone_HoldsWhileUnknown()
        {
            Condition c = When("{ \"tone\": \"blunt\" }");
            Assert.IsTrue(c.UsesTone);
            Assert.IsTrue(c.Holds(new FakeFacts { ToneValue = Tones.Unknown }));
            Assert.IsTrue(c.Holds(new FakeFacts { ToneValue = Tones.Blunt }));
            Assert.IsFalse(c.Holds(new FakeFacts { ToneValue = Tones.Polite }));
        }

        [Test]
        public void Tone_NotAllowed_IsIgnoredWithMessage()
        {
            Assert.IsTrue(When("{ \"tone\": \"blunt\" }", false).IsEmpty);
            StringAssert.Contains("when: tone: only allowed in answers, ignored", messages[0]);
        }

        [Test]
        public void Any_IsOr()
        {
            Condition c = When("{ \"any\": [ { \"time\": \"night\" }, { \"flags\": \"x\", \"asked\": \"y\" } ] }");
            Assert.IsTrue(c.Holds(new FakeFacts { Night = true }));
            Assert.IsFalse(c.Holds(new FakeFacts()));
            FakeFacts f = new FakeFacts();
            f.Flags.Add("x");
            f.AskedTopics.Add("y");
            Assert.IsTrue(c.Holds(f));
            CollectionAssert.AreEqual(new[] { "y" }, c.AskedIds);
        }

        [Test]
        public void Empty_Holds()
        {
            Assert.IsTrue(When("{}").Holds(new FakeFacts()));
            Assert.IsTrue(Condition.Check(null, new FakeFacts()));
        }

        [Test]
        public void UnknownKey_SuggestsAndRestStillApplies()
        {
            Condition c = When("{ \"tme\": \"night\", \"minGold\": 10 }");
            StringAssert.Contains("when: tme: unknown condition (did you mean \"time\"?), ignored", messages[0]);
            Assert.IsFalse(c.Holds(new FakeFacts { GoldValue = 5 }));
            Assert.IsTrue(c.Holds(new FakeFacts { GoldValue = 10 }));
        }

        [Test]
        public void UnknownKey_CaseOnly_Suggests()
        {
            When("{ \"Hours\": [1, 2] }");
            StringAssert.Contains("did you mean \"hours\"?", messages[0]);
        }

        [Test]
        public void BadValue_ListsAllowed()
        {
            When("{ \"season\": \"monsoon\" }");
            StringAssert.Contains("season: \"monsoon\" is not one of spring, summer, autumn, fall, winter, ignored", messages[0]);
        }

        [Test]
        public void FirstFailing_NamesTheKey()
        {
            Condition c = When("{ \"minGold\": 1, \"time\": \"night\" }");
            Assert.AreEqual("minGold", c.FirstFailing(new FakeFacts { Night = true }));
            Assert.AreEqual("time", c.FirstFailing(new FakeFacts { GoldValue = 5 }));
            Assert.IsNull(c.FirstFailing(new FakeFacts { GoldValue = 5, Night = true }));
        }
    }
}

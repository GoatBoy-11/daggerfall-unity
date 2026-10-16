using System;
using System.Collections.Generic;
using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class RoutinePackageTests
    {
        static AiPackage Package(string id,string json)
        { string error;var p=PackageParser.Parse(id,json,out error);Assert.IsNotNull(p,error);return p; }
        static PackageSettings Settings(string json)
        { string error;return PackageParser.Resolve(Package("test",json),null,out error); }
        [Test]
        public void WorkDefaultsAndNamedSelectorsAreSimple()
        {
            var s=Settings("{\"type\":\"work_at\"}");Assert.AreEqual("idle",s.Animation);Assert.IsNull(s.Position);Assert.IsNull(s.Facing);
            s=Settings("{\"type\":\"work_at\",\"parameters\":{\"spot\":\"forge\",\"animation\":\"hammer\",\"facing\":90}}");
            CollectionAssert.AreEqual(new[]{"forge"},s.Spots);Assert.AreEqual(90,s.Facing);Assert.AreEqual("hammer",s.Animation);
            s=Settings("{\"type\":\"travel_to\",\"parameters\":{\"spot\":[\"forge_left\",\"forge_right\"]}}");Assert.AreEqual(2,s.Spots.Count);
        }
        [TestCase("{\"type\":\"work_at\",\"parameters\":{\"position\":[0,0,0],\"spot\":\"forge\"}}")]
        [TestCase("{\"type\":\"work_at\",\"parameters\":{\"facing\":361}}")]
        [TestCase("{\"type\":\"work_at\",\"parameters\":{\"animation\":null}}")]
        [TestCase("{\"type\":\"travel_to\",\"parameters\":{\"target\":[0,0,0],\"spot\":\"forge\"}}")]
        [TestCase("{\"type\":\"guard_spot\",\"parameters\":{\"spot\":[]}}")]
        [TestCase("{\"type\":\"play_animation\",\"parameters\":{\"only_interrupt_at_combat\":1}}")]
        [TestCase("{\"type\":\"play_animation\",\"parameters\":{\"only_interrupt_at_combat\":null}}")]
        public void InvalidRoutineParametersFail(string json)
        { string error;Assert.IsNull(PackageParser.Parse("test",json,out error));Assert.IsNotEmpty(error); }
        [Test]
        public void CombatOnlyAnimationDefaultAndOverridesDoNotMutateDefaults()
        {
            string error;var p=Package("play","{\"type\":\"play_animation\",\"parameters\":{\"only_interrupt_at_combat\":true}}");
            var raw=Json.Parse("{\"only_interrupt_at_combat\":false}") as Dictionary<string,object>;
            Assert.IsFalse(PackageParser.Resolve(p,raw,out error).OnlyInterruptAtCombat);
            Assert.IsTrue(PackageParser.Resolve(p,null,out error).OnlyInterruptAtCombat);
            Assert.IsFalse(Settings("{\"type\":\"play_animation\"}").OnlyInterruptAtCombat);
        }
        [Test]
        public void SequenceResolvesPerStepCopiesAndAutoTravelCompletion()
        {
            string error;var packages=new Dictionary<string,AiPackage>();
            packages["travel"]=Package("travel","{\"type\":\"travel_to\",\"parameters\":{\"target\":[1,0,0]}}");
            packages["work"]=Package("work","{\"type\":\"work_at\"}");
            var sequence=Package("routine","{\"type\":\"sequence\",\"parameters\":{\"loop\":true,\"steps\":[{\"package\":\"travel\"},{\"package\":\"work\",\"parameters\":{\"animation\":\"pray\"},\"duration\":15}]}}");
            var plan=PackagePlan.Build(sequence,null,id=>packages.ContainsKey(id)?packages[id]:null,out error);
            Assert.IsNotNull(plan,error);Assert.IsTrue(plan.Sequence);Assert.IsTrue(plan.Loop);Assert.AreEqual(-1,plan.Steps[0].Duration);
            Assert.AreEqual("pray",plan.Steps[1].Settings.Animation);Assert.AreEqual(15,plan.Steps[1].Duration);
            Assert.IsEmpty(packages["work"].Parameters);
        }
        [TestCase("{\"steps\":[]}")]
        [TestCase("{\"steps\":[{\"package\":\"work\",\"duration\":0}]}")]
        [TestCase("{\"steps\":[{\"package\":\"work\",\"duration\":86401}]}")]
        [TestCase("{\"steps\":[{\"package\":\"work\",\"unknown\":true}]}")]
        [TestCase("{\"steps\":[{\"package\":\"work\"}],\"loop\":1}")]
        public void InvalidSequenceShapeRejected(string parameters)
        { string error;Assert.IsNull(PackageParser.Parse("seq","{\"type\":\"sequence\",\"parameters\":"+parameters+"}",out error)); }
        [Test]
        public void SequenceRejectsMissingDurationMissingPackagesAndCycles()
        {
            string error;var work=Package("work","{\"type\":\"work_at\"}");
            var seq=Package("seq","{\"type\":\"sequence\",\"parameters\":{\"steps\":[{\"package\":\"work\"}]}}");
            Assert.IsNull(PackagePlan.Build(seq,null,id=>work,out error));StringAssert.Contains("duration",error);
            Assert.IsNull(PackagePlan.Build(seq,null,id=>null,out error));StringAssert.Contains("missing",error);
            Assert.IsNull(PackagePlan.Build(seq,null,id=>seq,out error));StringAssert.Contains("nesting",error);
        }
        [Test]
        public void OccupancyAllowsCapacityAndIdempotentOwnershipAndCleansUp()
        {
            var occupancy=new SpotOccupancy();Assert.IsTrue(occupancy.TryClaim("chair","a",1));Assert.IsTrue(occupancy.TryClaim("chair","a",1));
            Assert.IsFalse(occupancy.TryClaim("chair","b",1));occupancy.Release("a");Assert.IsTrue(occupancy.TryClaim("chair","b",1));
            Assert.IsTrue(occupancy.TryClaim("bench","a",2));Assert.IsTrue(occupancy.TryClaim("bench","b",2));Assert.IsFalse(occupancy.TryClaim("bench","c",2));
            occupancy.Release("b");Assert.AreEqual(0,occupancy.Count("chair"));Assert.AreEqual(1,occupancy.Count("bench"));
        }
        [Test]
        public void SpotsSupportHomeAndAreaScopesAndTags()
        {
            string error;var home=SpotParser.Parse("forge","{\"position\":[1,0,2],\"tags\":\"work\"}",out error);
            Assert.IsNotNull(home,error);Assert.AreEqual("home",home.RelativeTo);Assert.IsTrue(home.Matches(new[]{"work"},"Any","Town","b123"));
            var area=SpotParser.Parse("forge","{\"relativeTo\":\"area\",\"position\":[123,2,456],\"location\":{\"region\":\"Daggerfall\",\"place\":\"Daggerfall\",\"context\":\"b123\"}}",out error);
            Assert.IsNotNull(area,error);Assert.IsTrue(area.Matches(new[]{"forge"},"daggerfall","Daggerfall","b123"));
            Assert.IsFalse(area.Matches(new[]{"forge"},"Daggerfall","Daggerfall",""));Assert.IsFalse(area.Matches(new[]{"forge"},"Other","Town","b123"));
        }
        [TestCase("{\"position\":[1,2,3],\"relativeTo\":\"area\"}")]
        [TestCase("{\"position\":[1,2,3],\"relativeTo\":null}")]
        [TestCase("{\"position\":[1,2,3],\"capacity\":0}")]
        [TestCase("{\"position\":[1,2,3],\"capacity\":1.5}")]
        [TestCase("{\"position\":[1,2,3],\"location\":{\"region\":\"A\",\"place\":\"B\",\"context\":null}}")]
        [TestCase("{\"position\":[1,2,3],\"location\":{\"region\":\"A\",\"place\":\"B\",\"context\":\"house\"}}")]
        [TestCase("{\"position\":[1,2,3],\"tags\":[\"Bad\"]}")]
        public void InvalidSpotDefinitionsRejected(string json)
        { string error;Assert.IsNull(SpotParser.Parse("spot",json,out error));Assert.IsNotEmpty(error); }
        [Test]
        public void PrintedSpotJsonRoundTrips()
        {
            string error;var spot=SpotParser.Parse("post",PositionFormat.SpotJson("Daggerfall","Daggerfall","outdoors",12.34f,1,-3),out error);
            Assert.IsNotNull(spot,error);Assert.AreEqual("area",spot.RelativeTo);Assert.AreEqual("",spot.Context);Assert.AreEqual(12.34,spot.Position.X,0.001);
        }
        [Test]
        public void RichScheduleConditionsCombineWeatherHealthOpinionAndFlags()
        {
            string error;var rows=AiSchedule.Read(Json.Parse("[{\"package\":\"work\",\"when\":{\"weather\":\"rain\",\"minOpinion\":20,\"maxHealth\":60,\"flags\":\"hired\"}},{\"package\":\"stand\"}]"),out error);
            Assert.IsNull(error);var facts=new FakeFacts {Rain=true,OpinionValue=25,HealthValue=50};facts.Flags.Add("hired");
            Assert.AreEqual("work",AiSchedule.Select(rows,600,facts).Package);facts.Rain=false;Assert.AreEqual("stand",AiSchedule.Select(rows,600,facts).Package);
            facts.Rain=true;facts.HealthValue=70;Assert.AreEqual("stand",AiSchedule.Select(rows,600,facts).Package);
        }
        [Test]
        public void PersonalConditionsRequireAnNpcEvenThroughToneWrapper()
        {
            var parsed=DialogueParser.Parse("test","{\"topics\":[{\"caption\":\"Hi\",\"answers\":[{\"text\":\"Yes\",\"when\":{\"minOpinion\":0}}]}]}");
            var facts=new FakeFacts {PersonalState=false};
            Assert.IsNull(Conversation.PickAnswer(parsed.File.Topics[0].Answers,facts,null,true));
        }
        [Test]
        public void NestedAnyAndVariableConditionsRetainPriority()
        {
            string error;var rows=AiSchedule.Read(Json.Parse("[{\"package\":\"work\",\"when\":{\"any\":[{\"weather\":\"snow\"},{\"variables\":{\"working\":true}}]}},{\"package\":\"stand\"}]"),out error);
            Assert.IsNull(error);var f=new FakeFacts();f.Variables["working"]=new NpcVariableValue(false);
            Assert.AreEqual("stand",AiSchedule.Select(rows,0,f).Package);f.Snow=true;Assert.AreEqual("work",AiSchedule.Select(rows,0,f).Package);
            f.Snow=false;f.Variables["working"]=new NpcVariableValue(true);Assert.AreEqual("work",AiSchedule.Select(rows,0,f).Package);
        }
        [TestCase("{\"minOpinion\":101}")]
        [TestCase("{\"maxHealth\":101}")]
        [TestCase("{\"minHealth\":-1}")]
        [TestCase("{\"tone\":\"polite\"}")]
        [TestCase("{\"weather\":\"foggy\"}")]
        public void BadScheduleConditionsCannotActivatePackages(string when)
        { string error;Assert.IsEmpty(AiSchedule.Read(Json.Parse("[{\"package\":\"work\",\"when\":"+when+"}]"),out error));Assert.IsNotEmpty(error); }
    }
}

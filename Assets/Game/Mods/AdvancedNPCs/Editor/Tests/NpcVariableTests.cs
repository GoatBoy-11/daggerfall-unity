using System;
using System.Collections.Generic;
using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class NpcVariableTests
    {
        static Dictionary<string, object> Object(string json) { return Json.Parse(json) as Dictionary<string, object>; }
        static NpcDefinition Definition()
        {
            string error;
            return new NpcDefinition {Variables = NpcVariables.Read(Object("{\"following\":false,\"jobs_completed\":0}"), false, out error)};
        }
        static NpcVariableCondition Condition(string json)
        {
            string error; var c = NpcVariableCondition.Read(Object(json), out error);
            Assert.IsNotNull(c, error); return c;
        }
        [Test]
        public void DefaultsAreTypedAndDoNotAllocateSavedOverrides()
        {
            var d=Definition(); var state=new NpcState(); NpcVariableValue v;
            Assert.IsTrue(NpcVariables.TryGet(d,state,"following",out v)); Assert.IsTrue(v.isBoolean); Assert.IsFalse(v.booleanValue);
            Assert.IsTrue(NpcVariables.TryGet(d,state,"jobs_completed",out v)); Assert.IsFalse(v.isBoolean); Assert.AreEqual(0,v.integerValue);
            Assert.IsNull(state.variables); Assert.IsTrue(state.IsDefault());
        }
        [TestCase("{\"a\":\"false\"}")]
        [TestCase("{\"a\":null}")]
        [TestCase("{\"a\":1.5}")]
        [TestCase("{\"a\":2147483648}")]
        [TestCase("{\"a\":-2147483649}")]
        [TestCase("{\"BadName\":0}")]
        [TestCase("{\"1name\":false}")]
        [TestCase("{\"a-b\":false}")]
        public void InvalidDefaultsRejected(string json)
        { string error; Assert.IsNull(NpcVariables.Read(Object(json),false,out error)); Assert.IsNotEmpty(error); }
        [Test]
        public void ValuesAndNamesAtBoundsAreAccepted()
        {
            string error; var values=NpcVariables.Read(Object("{\"min\":-2147483648,\"max\":2147483647}"),false,out error);
            Assert.AreEqual(int.MinValue,values["min"].integerValue); Assert.AreEqual(int.MaxValue,values["max"].integerValue);
            Assert.IsTrue(NpcVariables.ValidName(new string('a',64))); Assert.IsFalse(NpcVariables.ValidName(new string('a',65)));
            Assert.IsFalse(NpcVariables.ValidName(null));
        }
        [Test]
        public void InstancesAreIndependentAndDefinitionDefaultsStayUnchanged()
        {
            var d=Definition(); var a=new NpcState(); var other=new NpcState(); string error; NpcVariableValue v;
            Assert.IsTrue(NpcVariables.Set(d,a,"following",new NpcVariableValue(true),out error));
            Assert.IsTrue(NpcVariables.Add(d,a,"jobs_completed",3,out error));
            Assert.IsTrue(NpcVariables.TryGet(d,other,"following",out v)); Assert.IsFalse(v.booleanValue);
            Assert.AreEqual(0,d.Variables["jobs_completed"].integerValue); Assert.IsFalse(d.Variables["following"].booleanValue);
        }
        [Test]
        public void BadWritesAndIntegerOverflowLeaveStateUntouched()
        {
            var d=Definition();var s=new NpcState();string error;NpcVariableValue v;
            Assert.IsFalse(NpcVariables.Set(d,s,"following",new NpcVariableValue(1),out error));
            Assert.IsFalse(NpcVariables.Set(d,s,"missing",new NpcVariableValue(true),out error));
            Assert.IsFalse(NpcVariables.Add(d,s,"following",1,out error));
            Assert.IsFalse(NpcVariables.Set(d,s,null,new NpcVariableValue(true),out error));
            Assert.IsTrue(NpcVariables.Set(d,s,"jobs_completed",new NpcVariableValue(int.MaxValue),out error));
            Assert.IsFalse(NpcVariables.Add(d,s,"jobs_completed",1,out error));
            Assert.IsTrue(NpcVariables.TryGet(d,s,"jobs_completed",out v));Assert.AreEqual(int.MaxValue,v.integerValue);
        }
        [Test]
        public void ExplicitFalseAndZeroSurviveIndependentSnapshotsAndRestore()
        {
            var d=Definition();var table=new NpcStateTable();var state=table.GetOrCreate("person@1#0");string error;
            NpcVariables.Set(d,state,"following",new NpcVariableValue(false),out error);
            NpcVariables.Set(d,state,"jobs_completed",new NpcVariableValue(0),out error);
            var saved=table.Snapshot(); Assert.IsTrue(saved.ContainsKey("person@1#0"));
            NpcVariables.Set(d,state,"following",new NpcVariableValue(true),out error);
            Assert.IsFalse(saved["person@1#0"].variables["following"].booleanValue);
            table.Restore(saved);saved["person@1#0"].variables["jobs_completed"].integerValue=10;
            Assert.AreEqual(0,table.GetOrCreate("person@1#0").variables["jobs_completed"].integerValue);
            d.Variables["following"]=new NpcVariableValue(true);NpcVariableValue v;
            NpcVariables.TryGet(d,table.GetOrCreate("person@1#0"),"following",out v);Assert.IsFalse(v.booleanValue);
        }
        [Test]
        public void RemovedVariablesRemainSavedAndChangedTypesNeverMatch()
        {
            var d=Definition();var s=new NpcState();string error;NpcVariableValue v;
            NpcVariables.Set(d,s,"following",new NpcVariableValue(true),out error);
            d.Variables.Clear();Assert.IsFalse(NpcVariables.TryGet(d,s,"following",out v));Assert.IsNotNull(s.Clone().variables["following"]);
            d.Variables["following"]=new NpcVariableValue(0);Assert.IsFalse(NpcVariables.TryGet(d,s,"following",out v));
        }
        [TestCase("{\"following\":true}",true)]
        [TestCase("{\"following\":false}",false)]
        [TestCase("{\"jobs_completed\":4}",true)]
        [TestCase("{\"jobs_completed\":{\"gte\":3,\"lte\":5}}",true)]
        [TestCase("{\"jobs_completed\":{\"gt\":4}}",false)]
        [TestCase("{\"jobs_completed\":{\"lt\":5}}",true)]
        [TestCase("{\"jobs_completed\":{\"ne\":4}}",false)]
        [TestCase("{\"missing\":{\"ne\":false}}",false)]
        [TestCase("{\"following\":1}",false)]
        public void ConditionsAreStrictTypedAndAllClausesMustHold(string json,bool expected)
        {
            var f=new FakeFacts();f.Variables["following"]=new NpcVariableValue(true);f.Variables["jobs_completed"]=new NpcVariableValue(4);
            Assert.AreEqual(expected,Condition(json).Holds(f));
        }
        [TestCase("{}")]
        [TestCase("{\"following\":{\"gt\":false}}")]
        [TestCase("{\"a\":{\"equals\":1}}")]
        [TestCase("{\"a\":{}}")]
        [TestCase("{\"a\":\"1\"}")]
        public void BrokenConditionsCannotUnlockAnything(string json)
        { string error;Assert.IsNull(NpcVariableCondition.Read(Object(json),out error));Assert.IsNotEmpty(error); }
        [Test]
        public void DialogueActionsRunOnlyForSelectedResponseAndPreserveGlobalFlags()
        {
            var parsed=DialogueParser.Parse("test","{\"topics\":[{\"caption\":\"Follow\",\"when\":{\"variables\":{\"following\":false}},\"answers\":[{\"text\":\"Yes\",\"setVariables\":{\"following\":true},\"addVariables\":{\"jobs_completed\":1}}]}]}");
            Assert.IsEmpty(parsed.Messages);var f=new FakeFacts();f.Variables["following"]=new NpcVariableValue(false);f.Variables["jobs_completed"]=new NpcVariableValue(0);
            var c=new ComposedDialogue();c.Topics.Add(parsed.File.Topics[0]);Assert.AreEqual(1,Conversation.Visible(c,f,30).Count);
            Assert.IsFalse(f.Variables["following"].booleanValue);DialogueAnswer chosen;
            Conversation.Ask(c.Topics[0],f,null,f,new FakeActions(f),out chosen);
            Assert.IsTrue(f.Variables["following"].booleanValue);Assert.AreEqual(1,f.Variables["jobs_completed"].integerValue);
            Assert.AreEqual(0,Conversation.Visible(c,f,30).Count);Assert.IsEmpty(f.Flags);
        }
        [Test]
        public void VariablesWorkThroughToneUnknownAndNestedAny()
        {
            var parsed=DialogueParser.Parse("test","{\"topics\":[{\"caption\":\"Hi\",\"answers\":[{\"text\":\"Yes\",\"when\":{\"any\":[{\"variables\":{\"following\":true},\"tone\":\"polite\"}]}}]}]}");
            Assert.IsEmpty(parsed.Messages);var f=new FakeFacts();f.Variables["following"]=new NpcVariableValue(true);
            Assert.IsNotNull(Conversation.PickAnswer(parsed.File.Topics[0].Answers,f,null,true));
            f.Variables["following"]=new NpcVariableValue(false);Assert.IsNull(Conversation.PickAnswer(parsed.File.Topics[0].Answers,f,null,true));
        }
        [Test]
        public void ComposedDialogueChecksNestedVariableNamesAndTypes()
        {
            var parsed=DialogueParser.Parse("test","{\"topics\":[{\"caption\":\"Hi\",\"answers\":[\"Yes\"],\"replies\":[{\"text\":\"Do\",\"setVariables\":{\"missing\":true},\"answers\":[{\"text\":\"Ok\",\"when\":{\"variables\":{\"following\":1}}}]}]}]}");
            var c=new ComposedDialogue();c.Topics.Add(parsed.File.Topics[0]);Assert.AreEqual(2,NpcDialogueVariables.Check(c,Definition()).Count);
        }
        [Test]
        public void ConditionalSchedulesOverrideTimedRoutineAndFallBackImmediately()
        {
            string error;var list=AiSchedule.Read(Json.Parse("[{\"package\":\"stand\"},{\"package\":\"follow\",\"when\":{\"variables\":{\"following\":true}}},{\"package\":\"local_patrol\",\"hours\":[\"08:00\",\"18:00\"]}]"),out error,Definition());
            Assert.IsNull(error);var f=new FakeFacts();f.Variables["following"]=new NpcVariableValue(false);
            Assert.AreEqual("local_patrol",AiSchedule.Select(list,600,f).Package);Assert.AreEqual("stand",AiSchedule.Select(list,0,f).Package);
            f.Variables["following"]=new NpcVariableValue(true);Assert.AreEqual("follow",AiSchedule.Select(list,600,f).Package);
            f.Variables["following"]=new NpcVariableValue(false);Assert.AreEqual("local_patrol",AiSchedule.Select(list,600,f).Package);
        }
        [Test]
        public void ConditionalHoursAndPriorityAreDeterministic()
        {
            string error;var list=AiSchedule.Read(Json.Parse("[{\"package\":\"follow\",\"hours\":[\"08:00\",\"18:00\"],\"when\":{\"variables\":{\"following\":true}}},{\"package\":\"guard_spot\",\"when\":{\"variables\":{\"following\":true}}},{\"package\":\"stand\"}]"),out error,Definition());
            Assert.IsNull(error);var f=new FakeFacts();f.Variables["following"]=new NpcVariableValue(true);
            Assert.AreEqual("follow",AiSchedule.Select(list,600,f).Package);Assert.AreEqual("guard_spot",AiSchedule.Select(list,1200,f).Package);
        }
        [Test]
        public void UnknownScheduleVariableProducesWarningAndKeepsNpcLoading()
        {
            var parsed=DefinitionParser.ParseFolder("tester","{\"kind\":\"generic\",\"variables\":{\"following\":false},\"schedule\":[{\"package\":\"follow\",\"when\":{\"variables\":{\"missing\":true}}}]}");
            Assert.IsTrue(parsed.Ok,parsed.Error);Assert.IsEmpty(parsed.Definition.Schedule);Assert.IsNotEmpty(parsed.Warnings);
        }
    }
}

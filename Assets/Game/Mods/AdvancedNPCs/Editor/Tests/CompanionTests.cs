using System.Collections.Generic;
using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class CompanionTests
    {
        [TestCase("0", true)] [TestCase("100", true)] [TestCase("1000000", true)]
        [TestCase("-1", false)] [TestCase("100.5", false)] [TestCase("null", false)]
        [TestCase("1000001", false)] [TestCase("true", false)] [TestCase("\"100\"", false)]
        public void RecruitmentPriceValidates(string price, bool valid)
        {
            ParseResult parsed = DefinitionParser.ParseFolder("mercenary", "{\"kind\":\"generic\",\"recruitCost\":" + price + "}");
            Assert.AreEqual(valid, parsed.Ok, parsed.Error);
            if (valid) Assert.IsEmpty(parsed.Warnings);
        }
        [Test] public void OldDefinitionHasNoCompanionCommands()
        {
            var parsed = DefinitionParser.ParseFolder("ordinary", "{\"kind\":\"generic\"}");
            Assert.IsTrue(parsed.Ok, parsed.Error); Assert.AreEqual(-1, parsed.Definition.RecruitCost);
            var dialogue = new ComposedDialogue(); CompanionDialogue.Append(dialogue, -1); Assert.IsEmpty(dialogue.Topics);
        }
        [TestCase(null,"recruit",true)] [TestCase("following","recruit",false)]
        [TestCase("following","wait",true)] [TestCase("waiting","follow",true)]
        [TestCase("waiting","recruit",false)] [TestCase("dismissed","recruit",true)]
        [TestCase("dismissed","follow",false)] [TestCase(null,"dismiss",false)]
        [TestCase("following","dismiss",true)] [TestCase("invalid","recruit",false)]
        public void ContractCommandsHaveExplicitStates(string status, string command, bool allowed)
        { Assert.AreEqual(allowed, CompanionRecord.Allows(status, command)); }
        [Test] public void RecruitedTransientIdentityAndStateSurviveDeepSnapshot()
        {
            NpcDefinition definition = new NpcDefinition { Id="mercenary" };
            NpcInstance person = new NpcInstance { Key="mercenary@42#0", Definition=definition, Name="Alys", Seed=123,
                Race="Breton", Gender="Female", FaceOutfit=3, FaceVariant=2, PortraitName="alys.png", Persistent=false };
            NpcState state = new NpcState { companion=CompanionRecord.From(person), healthFraction=0.4f, opinion=20,
                variables=new Dictionary<string,NpcVariableValue> {{"paid",new NpcVariableValue(true)}} };
            state.companion.area="42:b7"; state.companion.x=12; state.companion.status="waiting";
            var table=new NpcStateTable(); table.Keep(person.Key,state);
            var saved=table.Snapshot(); state.companion.x=100; state.variables["paid"].booleanValue=false;
            table.Restore(saved); saved[person.Key].companion.status="dismissed";
            NpcState restored=table.GetOrCreate(person.Key); NpcInstance copy=restored.companion.Person(person.Key,definition);
            Assert.IsTrue(table.IsCompanion(person.Key)); Assert.IsTrue(copy.Persistent);
            Assert.AreEqual("Alys",copy.Name); Assert.AreEqual(123u,copy.Seed); Assert.AreEqual("Female",copy.Gender);
            Assert.AreEqual("Breton",copy.Race); Assert.AreEqual("alys.png",copy.PortraitName);
            Assert.AreEqual(3,copy.FaceOutfit); Assert.AreEqual(2,copy.FaceVariant);
            Assert.AreEqual("waiting",restored.companion.status); Assert.AreEqual(12,copy.X);
            Assert.AreEqual(0.4f,restored.healthFraction); Assert.AreEqual(20,restored.opinion);
            Assert.IsTrue(restored.variables["paid"].booleanValue);
        }
        [Test] public void CompanionMagicDoesNotRefillWhenChangingArea()
        {
            var config=new NpcMagicDefinition(); var state=new NpcState();
            NpcMagicRules.Enter(config,state,1,100); state.magicka=17; state.magicCooldown=9;
            state.companion=new CompanionRecord(); var table=new NpcStateTable(); table.Keep("mage",state);
            table.ForgetMagicOutside(2); NpcMagicRules.Enter(config,state,2,101);
            Assert.AreEqual(17,state.magicka); Assert.AreEqual(9,state.magicCooldown);
            Assert.IsNotNull(table.Snapshot()["mage"].companion);
        }
        [Test] public void OlderSavesKeepNormalMagicEntryBehaviour()
        {
            var config=new NpcMagicDefinition(); var state=new NpcState();
            NpcMagicRules.Enter(config,state,1,100); state.magicka=17;
            NpcMagicRules.Enter(config,state,2,101); Assert.AreEqual(config.MaxMagicka,state.magicka);
            Assert.IsNull(state.companion); Assert.IsTrue(state.IsDefault());
        }
        class Commands : FakeActions, ICompanionActions
        {
            public Commands() : base(new FakeFacts()) { }
            public bool Accepted;
            public bool CommandCompanion(string command) { return Accepted; }
        }
        [Test] public void RejectedStaleHireDoesNotClaimSuccessOrMarkAsked()
        {
            var dialogue=new ComposedDialogue(); CompanionDialogue.Append(dialogue,100);
            var facts=new Facts { GoldValue=100 }; var actions=new Commands();
            var topic=Conversation.Visible(dialogue,facts,30)[0]; DialogueAnswer answer;
            string response=Conversation.Ask(topic,facts,null,facts,actions,out answer);
            Assert.AreEqual("That offer is no longer available.",response); Assert.IsNull(answer);
            Assert.IsFalse(facts.Asked(topic.Id)); Assert.IsEmpty(actions.Done);
            actions.Accepted=true;
            response=Conversation.Ask(topic,facts,null,facts,actions,out answer);
            Assert.AreEqual("My blade is yours. Lead the way.",response); Assert.IsTrue(facts.Asked(topic.Id));
        }
        class Facts : FakeFacts, ICompanionFacts
        {
            public string Status;
            public bool CanCommandCompanion(string command) { return CompanionRecord.Allows(Status,command=="offer" ? "recruit" : command) && (command!="recruit" || Gold>=100); }
        }
        [Test] public void OnlyTheAddressedPersonsCurrentCommandsAreOffered()
        {
            var dialogue=new ComposedDialogue(); CompanionDialogue.Append(dialogue,100);
            var facts=new Facts { GoldValue=100 };
            var visible=Conversation.Visible(dialogue,facts,30);
            Assert.AreEqual(1,visible.Count); Assert.AreEqual("Hire you (100 gold)",visible[0].Caption);
            facts.GoldValue=99; Assert.AreEqual("Your hiring fee",Conversation.Visible(dialogue,facts,30)[0].Caption);
            facts.Status="following"; visible=Conversation.Visible(dialogue,facts,30);
            Assert.AreEqual(3,visible.Count); Assert.AreEqual("Wait here",visible[0].Caption);
            facts.Status="waiting"; visible=Conversation.Visible(dialogue,facts,30);
            Assert.AreEqual("Follow me",visible[0].Caption);
        }
    }
}

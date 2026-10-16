using NUnit.Framework;
using AdvancedNPCs.Core;
namespace AdvancedNPCs.Tests
{
    public class CompanionLimitsTests
    {
        [TestCase(0,3)] [TestCase(1,4)] [TestCase(2,5)] [TestCase(3,6)] [TestCase(4,7)]
        [TestCase(5,8)] [TestCase(6,9)] [TestCase(7,10)] [TestCase(8,0)] [TestCase(-1,3)] [TestCase(9,3)]
        public void SettingsMapEveryChoiceAndFailSafe(int choice,int maximum){Assert.AreEqual(maximum,CompanionLimits.FromSetting(choice));}
        [TestCase(false,"following",false,true)] [TestCase(false,"waiting",false,true)]
        [TestCase(false,"dismissed",false,false)] [TestCase(true,"following",false,false)]
        [TestCase(false,"following",true,false)] [TestCase(false,null,false,false)]
        public void OnlyLivingPaidContractsOccupySlots(bool dead,string status,bool exempt,bool expected)
        {Assert.AreEqual(expected,CompanionLimits.ConsumesSlot(dead,status,exempt));}
        [TestCase(3,2,false,true)] [TestCase(3,3,false,false)] [TestCase(3,10,false,false)]
        [TestCase(3,10,true,true)] [TestCase(0,10000,false,true)] [TestCase(10,10,false,false)]
        public void LimitExemptionsAndUnlimited(int maximum,int used,bool exempt,bool expected)
        {Assert.AreEqual(expected,CompanionLimits.CanRecruit(maximum,used,exempt));}
        [TestCase("true",true,true)] [TestCase("false",true,false)] [TestCase("null",false,false)]
        [TestCase("1",false,false)] [TestCase("\"true\"",false,false)] [TestCase("[]",false,false)]
        public void ExemptionIsStrictlyOptionalBoolean(string value,bool valid,bool expected)
        {
            var parsed=DefinitionParser.ParseFolder("escort","{\"kind\":\"generic\",\"no_companion_slot\":"+value+"}");
            Assert.AreEqual(valid,parsed.Ok,parsed.Error);if(valid){Assert.AreEqual(expected,parsed.Definition.NoCompanionSlot);Assert.AreEqual(-1,parsed.Definition.RecruitCost);Assert.IsEmpty(parsed.Warnings);}
        }
        [Test]public void ReusedRosterSnapshotReplacesPreviousMembers()
        {
            var table=new NpcStateTable();var state=new NpcState();table.Keep("person",state);
            var buffer=new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string,NpcState>>();table.CopyCompanionsTo(buffer);Assert.IsEmpty(buffer);
            state.companion=new CompanionRecord();table.CopyCompanionsTo(buffer);Assert.AreEqual(1,buffer.Count);
            table.CopyCompanionsTo(buffer);Assert.AreEqual(1,buffer.Count);table.Remove("person");table.CopyCompanionsTo(buffer);Assert.IsEmpty(buffer);
        }
        [Test]public void MissingFlagIsFalseAndContractCloneKeepsExemption()
        {
            var parsed=DefinitionParser.ParseFolder("ordinary","{\"kind\":\"generic\"}");Assert.IsFalse(parsed.Definition.NoCompanionSlot);
            parsed.Definition.NoCompanionSlot=true;var record=CompanionRecord.From(new NpcInstance{Definition=parsed.Definition});Assert.IsTrue(record.Clone().noCompanionSlot);
            Assert.IsFalse(new CompanionRecord().noCompanionSlot);
        }
    }
}

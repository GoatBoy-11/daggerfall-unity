using NUnit.Framework;
using AdvancedNPCs.Core;
namespace AdvancedNPCs.Tests
{
    [TestFixture]
    public class CompanionMagicTests
    {
        [TestCase("following",true)]
        [TestCase("waiting",true)]
        [TestCase("dismissed",false)]
        public void ContractOverridesEntryRecoveryWithoutRefill(string status,bool timed)
        {
            var d=new NpcMagicDefinition{MaxMagicka=100,RestHours=1};
            var s=new NpcState{companion=new CompanionRecord{status=status}};
            NpcMagicRules.Enter(d,s,1,100);s.magicka=0;s.magicCooldown=5;
            NpcMagicRules.Enter(d,s,2,1900);NpcMagicRules.Recover(d,s,1900,false);
            Assert.AreEqual(timed?50:0,s.magicka);
            Assert.AreEqual(5,s.magicCooldown);Assert.AreEqual("onAreaEntry",d.Recovery);
            Assert.AreEqual(!timed,s.magicRefillsOnEntry);
        }
        [Test]
        public void TimedPartyRecoveryPreservesFractionsAndCombatDelayThroughSave()
        {
            var d=new NpcMagicDefinition{MaxMagicka=100,RestHours=1};var table=new NpcStateTable();
            var s=table.GetOrCreate("mage");s.companion=new CompanionRecord();NpcMagicRules.Enter(d,s,1,100);s.magicka=0;
            NpcMagicRules.Recover(d,s,118,false);Assert.AreEqual(.5,s.magicRemainder,.00001);
            var restore=new NpcStateTable();restore.Restore(table.Snapshot());s=restore.GetOrCreate("mage");
            NpcMagicRules.Recover(d,s,136,false);Assert.AreEqual(1,s.magicka);
            NpcMagicRules.Recover(d,s,3736,true);Assert.AreEqual(1,s.magicka);
            NpcMagicRules.Recover(d,s,3795,false);Assert.AreEqual(1,s.magicka);
            NpcMagicRules.Recover(d,s,3832,false);Assert.AreEqual(2,s.magicka);
        }
        [TestCase(true,false)]
        [TestCase(false,true)]
        public void DeadOrHostileContractCannotReceiveTimedOverride(bool dead,bool hostile)
        {
            var d=new NpcMagicDefinition{MaxMagicka=100,RestHours=1};
            var s=new NpcState{dead=dead,hostile=hostile,companion=new CompanionRecord()};NpcMagicRules.Enter(d,s,1,100);s.magicka=0;
            NpcMagicRules.Recover(d,s,3700,false);Assert.AreEqual(0,s.magicka);
        }
        [Test]
        public void DismissedRestCasterRetainsAuthoredRecovery()
        {
            var d=new NpcMagicDefinition{Recovery="rest",MaxMagicka=100,RestHours=1};
            var s=new NpcState{companion=new CompanionRecord{status="dismissed"}};NpcMagicRules.Enter(d,s,1,100);s.magicka=0;
            NpcMagicRules.Recover(d,s,1900,false);Assert.AreEqual(50,s.magicka);
        }
    }
}

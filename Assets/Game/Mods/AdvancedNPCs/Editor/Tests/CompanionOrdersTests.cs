using NUnit.Framework;
using AdvancedNPCs.Core;
namespace AdvancedNPCs.Tests
{
    public class CompanionOrdersTests
    {
        [TestCase(null,"defensive")] [TestCase("","defensive")] [TestCase("unknown","defensive")]
        [TestCase("defensive","defensive")] [TestCase("aggressive","aggressive")] [TestCase("passive","passive")]
        public void OldOrInvalidStanceDefaultsSafely(string value,string expected)
        { Assert.AreEqual(expected,CompanionOrders.Stance(value)); Assert.AreEqual(expected,new CompanionRecord{stance=value}.Clone().stance); }
        [TestCase("defensive",false,false,false,false,-1)] [TestCase("aggressive",false,false,false,false,3)]
        [TestCase("aggressive",true,false,true,true,-1)] [TestCase("aggressive",true,true,true,true,0)]
        [TestCase("passive",false,false,true,false,1)] [TestCase("defensive",false,false,false,true,2)]
        public void WaitingStillOverridesAggression(string stance,bool waiting,bool self,bool player,bool party,int priority)
        { Assert.AreEqual(priority,CompanionOrders.Priority(stance,waiting,self,player,party)); }
        [TestCase("following",true)] [TestCase("waiting",true)] [TestCase("dismissed",false)] [TestCase(null,false)]
        public void OnlyActiveContractsAcceptTacticalOrders(string status,bool allowed)
        { foreach(string command in new[]{"orders","regroup","defensive","aggressive","passive"}) Assert.AreEqual(allowed,CompanionRecord.Allows(status,command),command); }
        [TestCase("following",true)] [TestCase("waiting",true)]
        [TestCase("dismissed",false)] [TestCase(null,false)]
        public void PushRequiresAnActiveContract(string status,bool expected)
        { Assert.AreEqual(expected,CompanionRecord.Allows(status,"push")); }
        [Test] public void PushWalksAFewStepsThenPausesBeforeFollowing()
        {
            var order=new CompanionPush();order.Begin();
            order.Tick(1,1.5);Assert.IsTrue(order.Moving);
            order.Tick(.7,1);Assert.IsFalse(order.Moving);Assert.IsTrue(order.Active);
            order.Tick(0,10);order.Tick(double.NaN,10);order.Tick(-1,10);
            order.Tick(2.9,0);Assert.IsTrue(order.Active);
            order.Tick(.1,0);Assert.IsFalse(order.Active);
        }
        [Test] public void PushHasABoundedBlockedWalkAndCanBeReissuedOrCancelled()
        {
            var order=new CompanionPush();order.Begin();order.Tick(4,0);
            Assert.IsFalse(order.Moving);order.Tick(3,0);Assert.IsFalse(order.Active);
            order.Begin();order.StopMoving();order.Tick(3,0);Assert.IsFalse(order.Active);
            order.Begin();order.Tick(1,2);order.Begin();order.Tick(1,1);
            Assert.IsTrue(order.Moving);order.Reset();Assert.IsFalse(order.Active);
        }
        [Test] public void BumpingNeedsContinuousPressureAndResetsOnReleaseOrIneligibility()
        {
            var contact=new CompanionPushContact();
            Assert.IsFalse(contact.Tick(.5,true,true));contact.Tick(.1,false,true);
            Assert.IsFalse(contact.Tick(.5,true,true));contact.Tick(.1,true,false);
            Assert.IsFalse(contact.Tick(.5,true,true));Assert.IsTrue(contact.Tick(.25,true,true));
            Assert.IsFalse(contact.Tick(1,true,true));
            contact.Reset();contact.Tick(.5,true,true);contact.Interrupt();
            Assert.IsFalse(contact.Tick(.5,true,true));Assert.IsTrue(contact.Tick(.25,true,true));
        }
        [Test] public void ManualPushAlsoStartsContactCooldownAndPausedTimeCannotTrigger()
        {
            var contact=new CompanionPushContact();contact.Pushed();
            Assert.IsFalse(contact.Tick(5,true,true));Assert.IsFalse(contact.Tick(1,true,true));
            Assert.IsTrue(contact.Tick(.75,true,true));
            contact.Reset();contact.Tick(.5,true,true);
            Assert.IsFalse(contact.Tick(0,true,true));Assert.IsFalse(contact.Tick(double.NaN,true,true));
            Assert.IsFalse(contact.Tick(-1,true,true));Assert.IsTrue(contact.Tick(.25,true,true));
        }
        [Test] public void StancesArePerPersonAndDeepSaved()
        {
            var table=new NpcStateTable();var state=new NpcState{companion=new CompanionRecord{stance="passive",status="waiting"},healthFraction=.4f};
            table.Keep("one",state);table.Keep("two",new NpcState{companion=new CompanionRecord()});
            var saved=table.Snapshot();state.companion.stance="aggressive";table.Restore(saved);saved["one"].companion.stance="aggressive";
            Assert.AreEqual("passive",table.GetOrCreate("one").companion.stance);Assert.AreEqual("waiting",table.GetOrCreate("one").companion.status);
            Assert.AreEqual(.4f,table.GetOrCreate("one").healthFraction);Assert.AreEqual("defensive",table.GetOrCreate("two").companion.stance);
        }
        [Test] public void RegroupUsesActiveTimeAndArrivalWithBoundedBlockedReturn()
        {
            var order=new CompanionRegroup();order.Begin();order.Tick(7,true);Assert.IsTrue(order.Active);
            order.Tick(0,true);order.Tick(double.NaN,true);order.Tick(-4,true);Assert.IsTrue(order.Active);
            order.Tick(1,true);Assert.IsFalse(order.Active);
            order.Begin();order.Tick(8,false);Assert.IsTrue(order.Active);order.Tick(12,false);Assert.IsFalse(order.Active);
            order.Begin();order.Reset();Assert.IsFalse(order.Active);
        }
    }
}

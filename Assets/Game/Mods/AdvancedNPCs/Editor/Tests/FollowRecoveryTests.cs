using System;
using NUnit.Framework;
using AdvancedNPCs.Core;
namespace AdvancedNPCs.Tests
{
    public class FollowRecoveryTests
    {
        [Test] public void DistantFollowerNeedsSustainedActiveTime()
        {
            var r=new FollowRecovery(); r.Tick(20,15,1);
            Assert.IsFalse(r.Due(20)); r.Tick(19,15,1); Assert.IsTrue(r.Due(19));
            r.Tick(10,15,1); Assert.IsFalse(r.Due(10));
        }
        [Test] public void DistanceSettingDecidesTooFar()
        {
            var r=new FollowRecovery(); for(int i=0;i<3;i++) r.Tick(20-i*1.5,25,1);
            Assert.AreEqual(0,r.DistantSeconds); Assert.IsFalse(r.Due(17));
            r=new FollowRecovery(); for(int i=0;i<2;i++) r.Tick(9-i*1.5,8,1);
            Assert.IsFalse(r.Due(7.5)); r.Tick(8.5,8,1); r.Tick(8.2,8,1); Assert.IsTrue(r.Due(8.2));
        }
        [Test] public void ClosingInIsNotStalled()
        {
            var r=new FollowRecovery(); for(int i=0;i<10;i++) r.Tick(14-i*1.05,15,1);
            Assert.AreEqual(0,r.StalledSeconds); Assert.IsFalse(r.Due(4.55));
        }
        [Test] public void MovingWithoutHeadwayIsStalledBelowTheDistance()
        {
            // Sliding along a wall 10 m away: the body moves, the gap does not shrink.
            var r=new FollowRecovery(); for(int i=0;i<5;i++) r.Tick(10+(i%2)*0.6,15,1);
            Assert.IsFalse(r.Due(10)); r.Tick(10.3,15,1); Assert.IsTrue(r.Due(10.3));
        }
        [Test] public void GrowingGapRaisesTheMarkSoLaterHeadwayCounts()
        {
            var r=new FollowRecovery(); r.Tick(7,15,1); r.Tick(9,15,1); r.Tick(11,15,1);
            Assert.AreEqual(2,r.StalledSeconds); r.Tick(9.9,15,1); Assert.AreEqual(0,r.StalledSeconds);
        }
        [Test] public void NearbyFollowerNeverRecovers()
        {
            var r=new FollowRecovery(); for(int i=0;i<10;i++) r.Tick(5.5,15,1);
            Assert.IsFalse(r.Due(5.5)); r.Tick(2,15,1); Assert.AreEqual(0,r.StalledSeconds); Assert.IsFalse(r.Due(8));
        }
        [Test] public void InterruptionsAndInvalidTimeNeverAccumulate()
        {
            var r=new FollowRecovery(); for(int i=0;i<5;i++) r.Tick(40,15,1);
            r.Reset(); r.Tick(40,15,0); r.Tick(40,15,-1); r.Tick(40,15,double.NaN); r.Tick(40,15,double.PositiveInfinity);
            Assert.AreEqual(0,r.StalledSeconds); Assert.AreEqual(0,r.DistantSeconds); Assert.IsFalse(r.Due(40));
        }
        [TestCase(true,true,false,false,false,true)]
        [TestCase(false,true,false,false,false,false)]
        [TestCase(true,false,false,false,false,false)]
        [TestCase(true,true,true,false,false,false)]
        [TestCase(true,true,false,true,false,false)]
        [TestCase(true,true,false,false,true,false)]
        public void CatchupRequiresEverySafetyCondition(bool recruited,bool calm,bool interrupted,bool destination,bool locked,bool allowed)
        { Assert.AreEqual(allowed,FollowRecovery.MayCatchUp(recruited,calm,interrupted,destination,locked)); }
        [TestCase(true,false,false,false,false,false,true)]
        [TestCase(false,false,false,false,false,false,false)]
        [TestCase(true,true,false,false,false,false,false)]
        [TestCase(true,false,true,false,false,false,false)]
        [TestCase(true,false,false,true,false,false,false)]
        [TestCase(true,false,false,false,true,false,false)]
        [TestCase(true,false,false,false,false,true,false)]
        public void OnlyOrdinaryUnlockedClosedDoorsCanOpen(bool ability,bool locked,bool moving,bool open,bool linked,bool special,bool allowed)
        { Assert.AreEqual(allowed,FollowRecovery.MayOpenDoor(ability,locked,moving,open,linked,special)); }
    }
}

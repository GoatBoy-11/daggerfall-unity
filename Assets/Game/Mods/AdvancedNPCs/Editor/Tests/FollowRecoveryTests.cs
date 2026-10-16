using System;
using NUnit.Framework;
using AdvancedNPCs.Core;
namespace AdvancedNPCs.Tests
{
    public class FollowRecoveryTests
    {
        [Test] public void DistantFollowerNeedsSustainedActiveTime()
        {
            var r=new FollowRecovery(); for(int i=0;i<5;i++) r.Tick(40,1,1,1);
            Assert.IsFalse(r.Due(40)); r.Tick(40,1,1,1); Assert.IsTrue(r.Due(40));
            r.Tick(20,1,1,1); Assert.IsFalse(r.Due(20));
        }
        [Test] public void OrdinaryMovementDoesNotCountAsStuck()
        {
            var r=new FollowRecovery(); for(int i=0;i<20;i++) r.Tick(10,1,1,1);
            Assert.AreEqual(0,r.StalledSeconds); Assert.IsFalse(r.Due(10));
        }
        [Test] public void StuckOrCirclingFollowerCanRecoverButNearbyOneCannot()
        {
            var r=new FollowRecovery(); for(int i=0;i<10;i++) r.Tick(8,0,0,1);
            Assert.IsTrue(r.Due(8)); r.Tick(2,0,0,1); Assert.IsFalse(r.Due(2));
            for(int i=0;i<10;i++) r.Tick(8,1,0,1); Assert.IsTrue(r.Due(8));
        }
        [Test] public void InterruptionsAndInvalidTimeNeverAccumulate()
        {
            var r=new FollowRecovery(); for(int i=0;i<5;i++) r.Tick(40,0,0,1);
            r.Reset(); r.Tick(40,0,0,0); r.Tick(40,0,0,-1); r.Tick(40,0,0,double.NaN); r.Tick(40,0,0,double.PositiveInfinity);
            Assert.AreEqual(0,r.StalledSeconds); Assert.AreEqual(0,r.DistantSeconds); Assert.IsFalse(r.Due(40));
        }
        [TestCase(true,true,false,false,false,false,true)]
        [TestCase(false,true,false,false,false,false,false)]
        [TestCase(true,false,false,false,false,false,false)]
        [TestCase(true,true,true,false,false,false,false)]
        [TestCase(true,true,false,true,false,false,false)]
        [TestCase(true,true,false,false,true,false,false)]
        [TestCase(true,true,false,false,false,true,false)]
        public void CatchupRequiresEverySafetyCondition(bool recruited,bool calm,bool interrupted,bool visible,bool destination,bool barrier,bool allowed)
        { Assert.AreEqual(allowed,FollowRecovery.MayCatchUp(recruited,calm,interrupted,visible,destination,barrier)); }
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

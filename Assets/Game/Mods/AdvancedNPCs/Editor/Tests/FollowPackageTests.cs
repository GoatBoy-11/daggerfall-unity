using System;
using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class FollowPackageTests
    {
        [Test]
        public void FollowDefaultsAndNpcOverrides()
        {
            string error;var p=PackageParser.Parse("follow","{\"type\":\"follow\"}",out error);Assert.IsNotNull(p,error);
            var s=PackageParser.Resolve(p,null,out error);Assert.AreEqual("player",s.FollowTarget);Assert.AreEqual(2,s.FollowDistance);Assert.AreEqual(6,s.RunDistance);
            var raw=Json.Parse("{\"target\":\"guard@1#0\",\"distance\":3,\"runDistance\":10}") as System.Collections.Generic.Dictionary<string,object>;
            var custom=PackageParser.Resolve(p,raw,out error);Assert.IsNotNull(custom,error);Assert.AreEqual("guard@1#0",custom.FollowTarget);
            Assert.AreEqual(2,s.FollowDistance);Assert.IsEmpty(p.Parameters);
        }
        [TestCase("{\"target\":null}")]
        [TestCase("{\"target\":\"\"}")]
        [TestCase("{\"target\":true}")]
        [TestCase("{\"distance\":0}")]
        [TestCase("{\"distance\":21}")]
        [TestCase("{\"runDistance\":101}")]
        [TestCase("{\"distance\":6,\"runDistance\":6}")]
        [TestCase("{\"run\":true}")]
        public void InvalidFollowParametersFail(string json)
        { string error;Assert.IsNull(PackageParser.Parse("follow","{\"type\":\"follow\",\"parameters\":"+json+"}",out error));Assert.IsNotEmpty(error); }
        [Test]
        public void DistanceHysteresisPreventsShufflingAtPost()
        {
            var f=new FollowMotion(2,6);Assert.IsFalse(f.Tick(2.3,0,0.1));Assert.IsTrue(f.Tick(2.6,0,0.1));
            Assert.IsTrue(f.Tick(2.3,0.1,0.1));Assert.IsFalse(f.Tick(2,0.1,0.1));Assert.IsFalse(f.Tick(2.4,0,0.1));
        }
        [Test]
        public void RunsOnlyBeyondRunDistanceAndStopsNearLeader()
        {
            var f=new FollowMotion(2,6);Assert.IsTrue(f.Tick(7,0,0.1));Assert.IsTrue(f.Running);
            Assert.IsTrue(f.Tick(5,0.1,0.1));Assert.IsFalse(f.Running);Assert.IsFalse(f.Tick(1.9,0.1,0.1));Assert.IsFalse(f.Running);
        }
        [Test]
        public void RunningHasAResumeMarginToAvoidAnimationFlicker()
        {
            var f=new FollowMotion(2,6);
            f.Tick(6.1,0.1,0.1); Assert.IsTrue(f.Running);
            f.Tick(5.8,0.1,0.1); Assert.IsTrue(f.Running);
            f.Tick(5,0.1,0.1); Assert.IsFalse(f.Running);
            f.Tick(5.8,0.1,0.1); Assert.IsFalse(f.Running);
            f.Tick(6,0.1,0.1); Assert.IsTrue(f.Running);
        }
        [Test]
        public void BlockedMovementWaitsThenRetriesAndPauseDoesNotCount()
        {
            var f=new FollowMotion(2,6);Assert.IsFalse(f.Tick(8,0,10));Assert.IsTrue(f.Blocked);
            Assert.IsFalse(f.Tick(8,0,0));Assert.IsFalse(f.Tick(8,0,1.9));Assert.IsTrue(f.Tick(8,0,0.1));Assert.IsFalse(f.Blocked);
            Assert.IsTrue(f.Tick(8,0.05,5));Assert.IsFalse(f.Tick(8,0,10));Assert.IsFalse(f.Tick(1,0,0));Assert.IsFalse(f.Blocked);
        }
    }
}

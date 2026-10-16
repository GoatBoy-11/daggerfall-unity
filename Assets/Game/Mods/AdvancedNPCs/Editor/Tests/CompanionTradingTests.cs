using System;
using NUnit.Framework;
using AdvancedNPCs.Core;
namespace AdvancedNPCs.Tests
{
    public class CompanionTradingTests
    {
        [TestCase(10,2,10,20,5)]
        [TestCase(10,2,10,30,10)]
        [TestCase(10,2,20,20,0)]
        [TestCase(10,2,21,20,0)]
        [TestCase(10,0,21,20,10)]
        [TestCase(10,0.1,0.9,1,1)]
        [TestCase(10,1,0,0.999,0)]
        [TestCase(10,-1,0,20,0)]
        [TestCase(-1,1,0,20,0)]
        [TestCase(0,1,0,20,0)]
        [TestCase(10,1,-1,20,0)]
        [TestCase(10,1,0,-1,0)]
        [TestCase(2147483647,0.0025,0,1000000000,2147483647)]
        public void CapacityChecks(int count,double unit,double carried,double limit,int expected)
        { Assert.AreEqual(expected,CompanionTradeRules.Maximum(count,unit,carried,limit)); }
        [Test]
        public void NonFiniteWeightNeverFits()
        {
            foreach(double invalid in new[]{double.NaN,double.PositiveInfinity,double.NegativeInfinity})
            {
                Assert.AreEqual(0,CompanionTradeRules.Maximum(10,invalid,0,10));
                Assert.AreEqual(0,CompanionTradeRules.Maximum(10,1,invalid,10));
                Assert.AreEqual(0,CompanionTradeRules.Maximum(10,1,0,invalid));
            }
        }
        [Test]
        public void ContractSnapshotsKeepIndependentBelongings()
        {
            var original=new CompanionRecord {belongings="{native items}",status="waiting"};
            var snapshot=original.Clone();original.belongings="changed";
            Assert.AreEqual("{native items}",snapshot.belongings);Assert.AreEqual("waiting",snapshot.status);
        }
    }
}

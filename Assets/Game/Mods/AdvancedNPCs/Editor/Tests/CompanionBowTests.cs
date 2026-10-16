using NUnit.Framework;
using AdvancedNPCs.Core;
namespace AdvancedNPCs.Tests
{
    public class CompanionBowTests
    {
        [TestCase(false,true,7.99f,false)] [TestCase(false,true,8,true)]
        [TestCase(true,true,6,true)] [TestCase(true,true,5.99f,false)]
        [TestCase(true,false,10,false)] [TestCase(false,false,10,false)]
        [TestCase(true,true,7,true)] [TestCase(false,true,7,false)]
        public void BowNeedsAmmoAndUsesSeparateSwitchDistances(bool drawn,bool ready,float distance,bool expected)
        {Assert.AreEqual(expected,CompanionBowRules.UseBow(drawn,ready,distance));}
        [TestCase(5.99f,false)] [TestCase(6,true)] [TestCase(25,true)] [TestCase(25.01f,false)]
        [TestCase(float.NaN,false)] [TestCase(float.PositiveInfinity,false)]
        public void FireRangeIsBounded(float distance,bool expected)
        {Assert.AreEqual(expected,CompanionBowRules.InRange(distance));}
    }
}

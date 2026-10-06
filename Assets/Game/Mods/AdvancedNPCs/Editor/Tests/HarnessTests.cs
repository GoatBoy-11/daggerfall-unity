using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class HarnessTests
    {
        [Test]
        public void CoreAssemblyIsVisible()
        {
            Assert.AreEqual(3600UL, HostilityRules.SecondsPerHour);
        }
    }
}

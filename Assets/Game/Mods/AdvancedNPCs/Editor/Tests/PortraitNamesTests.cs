using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class PortraitNamesTests
    {
        [TestCase("bram", "bram")]
        [TestCase("Bram.PNG", "bram")]
        [TestCase("  bram.png ", "bram")]
        [TestCase("commoner_1", "commoner_1")]
        [TestCase("", "")]
        public void Normalize(string raw, string expected)
        {
            Assert.AreEqual(expected, PortraitNames.Normalize(raw));
        }

        [Test]
        public void Null_IsEmpty()
        {
            Assert.AreEqual("", PortraitNames.Normalize(null));
        }
    }
}

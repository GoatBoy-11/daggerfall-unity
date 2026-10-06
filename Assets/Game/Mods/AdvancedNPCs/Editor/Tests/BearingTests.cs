using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class BearingTests
    {
        // dx = east, dz = north (DFU world axes)
        [TestCase(0f, 10f, "N")]
        [TestCase(10f, 10f, "NE")]
        [TestCase(10f, 0f, "E")]
        [TestCase(10f, -10f, "SE")]
        [TestCase(0f, -10f, "S")]
        [TestCase(-10f, -10f, "SW")]
        [TestCase(-10f, 0f, "W")]
        [TestCase(-10f, 10f, "NW")]
        [TestCase(-705.7f, -143.6f, "W")]
        [TestCase(0f, 0f, "here")]
        public void Direction(float dx, float dz, string expected)
        {
            Assert.AreEqual(expected, Bearing.Direction(dx, dz));
        }

        [Test]
        public void Describe_GivesDistanceAndDirection()
        {
            Assert.AreEqual("720 m W", Bearing.Describe(-705.7f, -143.6f));
        }
    }
}

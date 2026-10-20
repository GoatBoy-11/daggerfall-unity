using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class HawkSocializeTests
    {
        static PackageSettings Resolve(string type, string parameters, out string error)
        {
            AiPackage package = PackageParser.Parse(type, "{ \"type\": \"" + type + "\"" + (parameters == null ? "" : ", \"parameters\": { " + parameters + " }") + " }", out error);
            return package == null ? null : PackageParser.Resolve(package, null, out error);
        }

        [Test]
        public void HawkDefaultsAndOverrides()
        {
            string error;
            PackageSettings d = Resolve("hawk", null, out error);
            Assert.IsNotNull(d, error);
            Assert.AreEqual("hawk", d.Animation);
            Assert.AreEqual(new double[] { 6, 10, 20 }, new[] { d.Radius, d.IntervalMin, d.IntervalMax });
            PackageSettings o = Resolve("hawk", "\"range\": 10, \"interval\": [5, 5], \"animation\": \"greet_1\"", out error);
            Assert.IsNotNull(o, error);
            Assert.AreEqual(new double[] { 10, 5, 5 }, new[] { o.Radius, o.IntervalMin, o.IntervalMax });
            Assert.AreEqual("greet_1", o.Animation);
            foreach (string bad in new[] { "\"range\": 0", "\"range\": 31", "\"interval\": [0, 5]", "\"interval\": [9, 5]", "\"interval\": 5", "\"radius\": 6" })
                Assert.IsNull(Resolve("hawk", bad, out error), bad);
        }

        [Test]
        public void SocializeDefaultsAndOverrides()
        {
            string error;
            PackageSettings d = Resolve("socialize", null, out error);
            Assert.IsNotNull(d, error);
            Assert.AreEqual(new double[] { 15, 20, 40 }, new[] { d.Radius, d.IntervalMin, d.IntervalMax });
            PackageSettings o = Resolve("socialize", "\"radius\": 8, \"interval\": [30, 60]", out error);
            Assert.IsNotNull(o, error);
            Assert.AreEqual(new double[] { 8, 30, 60 }, new[] { o.Radius, o.IntervalMin, o.IntervalMax });
            foreach (string bad in new[] { "\"radius\": 1", "\"radius\": 51", "\"interval\": [20]", "\"animation\": \"talk\"" })
                Assert.IsNull(Resolve("socialize", bad, out error), bad);
        }

        [Test]
        public void StationaryPackagesNeverMove()
        {
            string error;
            foreach (string type in new[] { "hawk", "socialize" })
                Assert.IsNull(new PackageMotion(Resolve(type, null, out error), () => .5, null).Tick(new PackagePoint(9, 0, 9), 0, 0), type);
        }

        [Test]
        public void HawkIsASoundPool()
        {
            Assert.AreEqual(SoundPools.Hawk, SoundPools.PoolOf("hawk_2"));
        }
    }
}

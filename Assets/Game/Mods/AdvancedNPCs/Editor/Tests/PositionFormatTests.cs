using System.Globalization;
using System.Threading;
using System.Collections.Generic;
using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class PositionFormatTests
    {
        [Test]
        public void FormatsPasteableJson()
        {
            string s = PositionFormat.ToJsonSnippet("Daggerfall", "Daggerfall", 12.5f, 1f, -3.25f);
            Assert.AreEqual(
                "\"location\": { \"region\": \"Daggerfall\", \"place\": \"Daggerfall\" },\n\"position\": [12.5, 1, -3.25]",
                s);
        }

        [Test]
        public void UsesDotUnderCommaCulture()
        {
            CultureInfo old = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                string s = PositionFormat.ToJsonSnippet("R", "P", 12.5f, 0f, 0f);
                StringAssert.Contains("[12.5, 0, 0]", s);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = old;
            }
        }

        [Test]
        public void EscapesQuotesInNames()
        {
            string s = PositionFormat.ToJsonSnippet("R", "The \"Inn\"", 0f, 0f, 0f);
            StringAssert.Contains("\"place\": \"The \\\"Inn\\\"\"", s);
        }

        [Test]
        public void RecordedPatrolRoundTripsThroughPackageParserUnderCommaCulture()
        {
            CultureInfo old = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                var points = new List<PackagePoint> { new PackagePoint(-3.25, 1, 12.5), new PackagePoint(8, 0, -9.5) };
                string json = PositionFormat.PatrolJson(points);
                string error;
                AiPackage package = PackageParser.Parse("recorded_patrol", json, out error);
                Assert.IsNotNull(package, error);
                var settings = PackageParser.Resolve(package, null, out error);
                Assert.AreEqual(2, settings.Points.Count);
                Assert.AreEqual(-3.25, settings.Points[0].X);
                Assert.AreEqual(12.5, settings.Points[0].Z);
                Assert.AreEqual(-9.5, settings.Points[1].Z);
            }
            finally { Thread.CurrentThread.CurrentCulture = old; }
        }
    }
}

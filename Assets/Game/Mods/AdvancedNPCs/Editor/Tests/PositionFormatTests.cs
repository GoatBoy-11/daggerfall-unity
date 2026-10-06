using System.Globalization;
using System.Threading;
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
    }
}

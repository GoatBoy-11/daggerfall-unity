using System.Collections.Generic;
using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    /// <summary>Save format version, "$schema" members and opinion drift over partial days (spec tools-quality).</summary>
    public class ToolsQualityTests
    {
        const ulong Day = 86400;

        [Test]
        public void SaveFormat_KnownVersionsLoadQuietly()
        {
            Assert.AreEqual(1, SaveFormat.Current);
            Assert.IsNull(SaveFormat.Check(0));     // saves from before the version existed
            Assert.IsNull(SaveFormat.Check(1));
        }

        [Test]
        public void SaveFormat_NewerVersionWarns()
        {
            string warning = SaveFormat.Check(2);
            Assert.IsNotNull(warning);
            StringAssert.Contains("format 2", warning);
        }

        [Test]
        public void SchemaMember_IsNotAnUnknownField()
        {
            Dictionary<string, object> o = new Dictionary<string, object> { { "$schema", "../_Schemas/npc.schema.json" }, { "x", 1L } };
            CollectionAssert.AreEqual(new[] { "x" }, FieldReader.UnknownKeys(o, new string[0]));
        }

        [Test]
        public void SchemaMember_ParsesWithoutWarning()
        {
            ParseResult r = DefinitionParser.ParseFolder("schema_npc",
                "{ \"$schema\": \"../_Schemas/npc.schema.json\", \"kind\": \"unique\", \"name\": \"A\", \"race\": \"Breton\", \"baseClass\": \"Warrior\", \"location\": { \"region\": \"Daggerfall\", \"place\": \"Daggerfall\" }, \"position\": [0, 0, 0] }");
            Assert.IsTrue(r.Ok, r.Error);
            foreach (string w in r.Warnings)
                StringAssert.DoesNotContain("$schema", w);
        }

        [Test]
        public void ChangedAt_KeepsPartialDays()
        {
            Assert.AreEqual(0UL, OpinionRules.ChangedAt(10, 0, Day * 9 / 10));      // not a whole day yet: keep the old stamp
            Assert.AreEqual(2 * Day, OpinionRules.ChangedAt(10, 0, 2 * Day + Day / 2));
        }

        [Test]
        public void ChangedAt_StartsFreshFromZero()
        {
            Assert.AreEqual(5UL, OpinionRules.ChangedAt(0, 0, 5));
            Assert.AreEqual(10 * Day, OpinionRules.ChangedAt(3, 0, 10 * Day));     // drifted all the way to 0
            Assert.AreEqual(Day, OpinionRules.ChangedAt(10, 5 * Day, Day));        // a clock that went back
        }

        [Test]
        public void FrequentChanges_StillDrift()
        {
            // +1 every 0.9 days for 10 changes: with partial days kept, the opinion still drifts.
            int opinion = 20;
            ulong at = 0;
            for (int i = 1; i <= 10; i++)
            {
                ulong now = (ulong)i * Day * 9 / 10;
                int next = OpinionRules.Change(opinion, at, now, 1);
                at = OpinionRules.ChangedAt(opinion, at, now);
                opinion = next;
            }
            Assert.Less(opinion, 30);       // 20 + 10 changes - drift of 9 whole days * 2
        }
    }
}

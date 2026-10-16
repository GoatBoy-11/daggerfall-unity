using System;
using System.Collections.Generic;
using NUnit.Framework;
using AdvancedNPCs.Core;
namespace AdvancedNPCs.Tests
{
    public class CompanionBarkTests
    {
        [TestCase("cm_bark")][TestCase("cm_dungeon")][TestCase("cm_inn")][TestCase("cm_hurt")][TestCase("cm_no_magicka")]
        public void OptionalPoolsUseExistingNumberConvention(string pool)
        {
            Assert.AreEqual(pool,SoundPools.PoolOf(pool+"_14"));Assert.AreEqual(pool,SoundPools.PoolOf(pool.ToUpperInvariant()));
            Assert.IsNull(SoundPools.PoolOf(pool+"14"));Assert.IsNull(SoundPools.PoolOf(pool+"_other"));
            Assert.IsNull(SoundPools.Resolve(pool,new[]{SoundPools.Greet,SoundPools.Attack}));
            Assert.IsTrue(CompanionBarkRules.IsBark(pool));
        }
        [TestCase("cm_dungeon")][TestCase("cm_inn")]
        public void ContextHasExactlyTwoThirdsOfTheRoll(string context)
        {
            Assert.AreEqual(context,CompanionBarkRules.Pool(false,context,0));
            Assert.AreEqual(context,CompanionBarkRules.Pool(false,context,2.0/3.0-1e-8));
            Assert.AreEqual("cm_bark",CompanionBarkRules.Pool(false,context,2.0/3.0));
            Assert.AreEqual("cm_bark",CompanionBarkRules.Pool(false,context,1));
            Assert.AreEqual("cm_hurt",CompanionBarkRules.Pool(true,context,0));
            Assert.AreEqual("cm_hurt",CompanionBarkRules.Pool(true,context,1));
        }
        [Test] public void OutdoorsHasNoContextFallbackOrInventedPools()
        { Assert.AreEqual("cm_bark",CompanionBarkRules.Pool(false,null,0));Assert.AreEqual("cm_bark",CompanionBarkRules.Pool(false,"cm_castle",0)); }
        [Test] public void TimerUsesActiveTimeAndResamplesAfterEachBark()
        {
            var clock=new CompanionBarkClock();Assert.IsFalse(clock.Tick(.1,10,20,.5));
            Assert.IsFalse(clock.Tick(0,10,20,0));Assert.IsFalse(clock.Tick(double.NaN,10,20,0));
            Assert.IsFalse(clock.Tick(14,10,20,0));Assert.IsTrue(clock.Tick(1,10,20,0));
            Assert.IsFalse(clock.Tick(9,10,20,1));Assert.IsTrue(clock.Tick(1,10,20,1));
            Assert.IsFalse(clock.Tick(19,10,20,0));Assert.IsTrue(clock.Tick(1,10,20,0));
        }
        [Test] public void DepletionIsAnEdgeWithNoFirstObservationOrZeroSpam()
        {
            var clock=new CompanionBarkClock();Assert.IsFalse(clock.ObserveMagicka(0));Assert.IsFalse(clock.ObserveMagicka(0));
            Assert.IsFalse(clock.ObserveMagicka(1));Assert.IsTrue(clock.ObserveMagicka(0));Assert.IsFalse(clock.ObserveMagicka(0));
            Assert.IsFalse(clock.ObserveMagicka(100));Assert.IsTrue(clock.ObserveMagicka(0));
            clock.Reset(0);Assert.IsFalse(clock.ObserveMagicka(0));
        }
        [Test] public void IntervalEndpointsAreIncluded()
        { Assert.AreEqual(45,CompanionBarkRules.Interval(45,90,0));Assert.AreEqual(90,CompanionBarkRules.Interval(45,90,1)); }
        [TestCase("{}",45,90)]
        [TestCase("{\"sounds\":{\"barkMinSeconds\":1,\"barkMaxSeconds\":2.5}}",1,2.5)]
        [TestCase("{\"sounds\":{\"barkMinSeconds\":120,\"barkMaxSeconds\":120}}",120,120)]
        public void SettingsAreOptionalAndAcceptEqualBounds(string json,double min,double max)
        {
            SoundSettings settings;string key;var warnings=new List<string>();
            Assert.IsNull(SoundRules.Read((Dictionary<string,object>)Json.Parse(json),out key,out settings,warnings));
            Assert.AreEqual(min,settings.BarkMinSeconds);Assert.AreEqual(max,settings.BarkMaxSeconds);Assert.IsEmpty(warnings);
        }
        [TestCase("{\"barkMinSeconds\":0}","sounds.barkMinSeconds")]
        [TestCase("{\"barkMinSeconds\":-2}","sounds.barkMinSeconds")]
        [TestCase("{\"barkMinSeconds\":null}","sounds.barkMinSeconds")]
        [TestCase("{\"barkMinSeconds\":1e100}","sounds.barkMinSeconds")]
        [TestCase("{\"barkMinSeconds\":90,\"barkMaxSeconds\":60}","sounds.barkMaxSeconds")]
        [TestCase("{\"barkMaxSeconds\":\"frequently\"}","sounds.barkMaxSeconds")]
        public void InvalidIntervalsNameTheOffendingField(string sounds,string expected)
        {
            SoundSettings settings;string key;
            Assert.IsNotNull(SoundRules.Read((Dictionary<string,object>)Json.Parse("{\"sounds\":"+sounds+"}"),out key,out settings,new List<string>()));Assert.AreEqual(expected,key);
        }
        [Test] public void MutingSurvivesSnapshotsWithoutChangingOtherContracts()
        {
            var table=new NpcStateTable();var a=table.GetOrCreate("a");a.companion=new CompanionRecord{barksMuted=true};
            table.GetOrCreate("b").companion=new CompanionRecord();var copy=table.Snapshot();
            Assert.IsTrue(copy["a"].companion.barksMuted);Assert.IsFalse(copy["b"].companion.barksMuted);
            copy["a"].companion.barksMuted=false;Assert.IsTrue(a.companion.barksMuted);
            Assert.IsFalse(new CompanionRecord().barksMuted);
        }
    }
}

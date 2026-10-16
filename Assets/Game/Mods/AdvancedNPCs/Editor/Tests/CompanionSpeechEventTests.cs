using NUnit.Framework;
using AdvancedNPCs.Core;
namespace AdvancedNPCs.Tests
{
    public class CompanionSpeechEventTests
    {
        [TestCase("recruit","cm_recruit")][TestCase("wait","cm_wait")][TestCase("follow","cm_follow")]
        [TestCase("push","cm_push")][TestCase("dismiss","cm_dismiss")][TestCase("regroup","cm_regroup")]
        public void CommandNamesAreExplicit(string command,string pool)
        {Assert.AreEqual(pool,CompanionBarkRules.Command(command));}
        [TestCase("orders")][TestCase("passive")][TestCase("trade")][TestCase("unknown")][TestCase(null)]
        public void UnrelatedCommandsDoNotInventSpeech(string command)
        {Assert.IsNull(CompanionBarkRules.Command(command));}
        [TestCase("cm_recruit")][TestCase("cm_wait")][TestCase("cm_follow")][TestCase("cm_dismiss")]
        [TestCase("cm_push")][TestCase("cm_regroup")][TestCase("cm_combat")][TestCase("cm_victory")]
        public void NewPoolsUseNumberSuffixAndAllBarkControls(string pool)
        {
            Assert.AreEqual(pool,SoundPools.PoolOf(pool+"_3"));Assert.IsTrue(CompanionBarkRules.IsBark(pool));
            Assert.IsNull(SoundPools.Resolve(pool,new[]{"cm_bark",SoundPools.Attack,SoundPools.Greet}));
            Assert.IsFalse(SoundPools.IsCombat(pool));
        }
        [TestCase(false,.49,true)][TestCase(false,.5,false)]
        [TestCase(true,.149,true)][TestCase(true,.15,false)][TestCase(true,.4,false)]
        public void PushSpeechHasLowerChanceForContact(bool automatic,double roll,bool expected)
        {Assert.AreEqual(expected,CompanionBarkRules.PushVoice(automatic,roll));}
        [TestCase("cm_push")][TestCase("cm_push_1")][TestCase("CM_PUSH_17")][TestCase("push")][TestCase("push_3")][TestCase("PUSH_17")]
        public void PushSoundNamesJoinOnePool(string name)
        {Assert.AreEqual("cm_push",SoundPools.PoolOf(name));}
        [Test] public void CombatStartsOnceAcrossRepeatedObservationsAndTargetChanges()
        {
            var state=new CompanionBattleSpeech();Assert.IsNull(state.Observe(false,true,false,false));
            Assert.AreEqual("cm_combat",state.Observe(true,false,false,false));
            for(int i=0;i<10;i++)Assert.IsNull(state.Observe(true,false,false,false));
            Assert.IsNull(state.Observe(true,false,true,false));
            Assert.AreEqual("cm_victory",state.Observe(false,true,true,false));
            Assert.IsNull(state.Observe(false,true,true,false));
        }
        [Test] public void LosingALivingEnemyIsNotVictory()
        {
            var state=new CompanionBattleSpeech();state.Observe(true,false,false,false);
            Assert.IsNull(state.Observe(false,true,false,false));Assert.IsNull(state.Observe(false,true,true,false));
            Assert.AreEqual("cm_combat",state.Observe(true,false,false,false));
        }
        [Test] public void RegroupCannotClaimVictoryLater()
        {
            var state=new CompanionBattleSpeech();state.Observe(true,false,false,false);
            Assert.IsNull(state.Observe(false,false,true,true));Assert.IsNull(state.Observe(false,true,true,false));
        }
        [Test] public void ReconstructionHasNoOldBattleToReplay()
        {
            var state=new CompanionBattleSpeech();state.Observe(true,false,false,false);state.Reset();
            Assert.IsNull(state.Observe(false,true,true,false));
        }
        [Test] public void OrdinaryRetreatDoesNotStartAnotherEpisode()
        {
            var state=new CompanionBattleSpeech();state.Observe(true,false,false,false);
            Assert.IsNull(state.Observe(false,false,false,false));Assert.IsNull(state.Observe(true,false,false,false));
        }
    }
}

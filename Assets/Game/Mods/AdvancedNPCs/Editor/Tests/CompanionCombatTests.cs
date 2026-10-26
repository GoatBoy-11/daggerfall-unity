using NUnit.Framework;
using AdvancedNPCs.Core;
namespace AdvancedNPCs.Tests
{
    public class CompanionCombatTests
    {
        [TestCase("following",true)] [TestCase("waiting",true)] [TestCase("dismissed",false)] [TestCase(null,false)]
        public void OnlyCurrentContractsDefend(string status,bool active) { Assert.AreEqual(active,CompanionCombatRules.Contract(status)); }
        [TestCase(true,false,false,false,false,true,true)]
        [TestCase(false,false,false,false,false,true,false)]
        [TestCase(true,true,false,false,false,true,false)]
        [TestCase(true,false,true,false,false,true,false)]
        [TestCase(true,false,false,true,false,true,false)]
        [TestCase(true,false,false,false,true,true,false)]
        [TestCase(true,false,false,false,false,false,false)]
        public void ProtectedPeopleNeverBecomeTargets(bool alive,bool player,bool ally,bool guard,bool quest,bool hostile,bool allowed)
        { Assert.AreEqual(allowed,CompanionCombatRules.Legal(alive,player,ally,guard,quest,hostile)); }
        [TestCase(true,false,false,true)] [TestCase(true,false,true,false)] [TestCase(true,true,true,true)]
        [TestCase(false,true,true,true)] [TestCase(false,true,false,true)] [TestCase(false,false,false,false)]
        public void OnlyTheCityWatchKnightIsAGuardInDungeons(bool team,bool knight,bool dungeon,bool guard)
        { Assert.AreEqual(guard,CompanionCombatRules.Guard(team,knight,dungeon)); }
        [TestCase(false,true,true,false,true,false)]
        [TestCase(false,true,true,false,false,true)]
        [TestCase(false,false,true,false,true,true)]
        [TestCase(false,true,false,false,true,true)]
        [TestCase(false,true,true,true,true,true)]
        [TestCase(true,true,true,true,true,true)]
        [TestCase(true,true,true,false,false,false)]
        [TestCase(false,false,false,false,false,true)]
        public void QuestDefenceRequiresAnUnrestrainedHostileFoeAttackingParty(bool attackable,bool hostile,bool foe,bool restricted,bool party,bool protectedActor)
        { Assert.AreEqual(protectedActor,CompanionCombatRules.QuestProtected(attackable,hostile,foe,restricted,party)); }
        [Test] public void WaitingDefendsSelfOnlyAndFollowingPrioritisesImmediateAttacker()
        {
            Assert.AreEqual(0,CompanionCombatRules.Priority(true,true,true,true));
            Assert.AreEqual(-1,CompanionCombatRules.Priority(true,false,true,true));
            Assert.AreEqual(0,CompanionCombatRules.Priority(false,true,true,true));
            Assert.AreEqual(1,CompanionCombatRules.Priority(false,false,true,true));
            Assert.AreEqual(2,CompanionCombatRules.Priority(false,false,false,true));
            Assert.AreEqual(-1,CompanionCombatRules.Priority(false,false,false,false));
        }
        [Test] public void PursuitIsAnchoredToPlayerOrWaitingPoint()
        {
            Assert.IsTrue(CompanionCombatRules.WithinLeash(false,30,30));
            Assert.IsFalse(CompanionCombatRules.WithinLeash(false,31,5));
            Assert.IsFalse(CompanionCombatRules.WithinLeash(false,5,31));
            Assert.IsTrue(CompanionCombatRules.WithinLeash(true,8,8));
            Assert.IsFalse(CompanionCombatRules.WithinLeash(true,4,9));
        }
        [Test] public void RetreatHasHysteresisWithoutChangingHealth()
        {
            Assert.IsTrue(CompanionCombatRules.Retreat(.24f,false));
            Assert.IsFalse(CompanionCombatRules.Retreat(.3f,false));
            Assert.IsTrue(CompanionCombatRules.Retreat(.3f,true));
            Assert.IsFalse(CompanionCombatRules.Retreat(.4f,true));
        }
        [Test] public void UnseenAndBlockedTargetsExpireInActiveTime()
        {
            var p=new CompanionPursuit(); for(int i=0;i<5;i++) Assert.IsFalse(p.Tick(10,false,1));
            Assert.IsTrue(p.Tick(10,false,1));p.Reset();
            for(int i=0;i<9;i++) Assert.IsFalse(p.Tick(3,true,1)); Assert.IsTrue(p.Tick(3,true,1));
        }
        [Test] public void RealApproachAndMeleeDoNotExpire()
        {
            var p=new CompanionPursuit();for(int i=0;i<20;i++) Assert.IsFalse(p.Tick(40-i,true,1));
            p.Reset();for(int i=0;i<20;i++) Assert.IsFalse(p.Tick(1.5,true,1));
        }
        [Test] public void PausedInvalidTimeAndResetDoNotAdvancePursuit()
        {
            var p=new CompanionPursuit(); for(int i=0;i<100;i++) Assert.IsFalse(p.Tick(10,false,0));
            Assert.IsFalse(p.Tick(10,false,double.NaN));Assert.IsFalse(p.Tick(10,false,double.PositiveInfinity));
            p.Tick(10,false,5);p.Reset();Assert.IsFalse(p.Tick(10,false,1));
        }
        [Test] public void NewContractDoesNotInheritOldWarnings()
        { var h=new CompanionHits();h.Hit();h.Hit();h.Hit();h.Reset();Assert.AreEqual(0,h.Count);Assert.IsFalse(h.Hit()); }
        [Test] public void IsolatedHitExpiresButThreeRecentHitsBreakContract()
        {
            var h=new CompanionHits(); Assert.IsFalse(h.Hit());h.Tick(11);Assert.AreEqual(0,h.Count);
            Assert.IsFalse(h.Hit());h.Tick(1);Assert.IsFalse(h.Hit());h.Tick(1);Assert.IsTrue(h.Hit());
        }
    }
}

using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Questing;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>Owns stance-based target selection for friendly contracts; DFU still performs attacks and damage.</summary>
    [DefaultExecutionOrder(-200)]
    public class NpcCompanionCombat : MonoBehaviour
    {
        // DFU exposes target/positions but these perception values are read-only. Keep its automatic
        // target selection disabled for this one hired actor, so enhanced AI cannot select a bystander.
        static readonly FieldInfo distanceField = typeof(EnemySenses).GetField("distanceToTarget", BindingFlags.Instance|BindingFlags.NonPublic);
        static readonly FieldInfo directionField = typeof(EnemySenses).GetField("directionToTarget", BindingFlags.Instance|BindingFlags.NonPublic);
        static readonly FieldInfo sightField = typeof(EnemySenses).GetField("targetInSight", BindingFlags.Instance|BindingFlags.NonPublic);
        static readonly FieldInfo earField = typeof(EnemySenses).GetField("targetInEarshot", BindingFlags.Instance|BindingFlags.NonPublic);
        readonly Collider[] nearby = new Collider[256];
        readonly RaycastHit[] rays = new RaycastHit[64];
        readonly Dictionary<DaggerfallEntityBehaviour,float> avoided = new Dictionary<DaggerfallEntityBehaviour,float>();
        readonly CompanionPursuit pursuit = new CompanionPursuit();
        readonly CompanionHits hits = new CompanionHits();
        readonly CompanionRegroup regroup = new CompanionRegroup();
        readonly CompanionPush push = new CompanionPush();
        readonly CompanionPushContact pushContact = new CompanionPushContact();
        Vector3 pushPrevious;
        public bool Pushing { get { return push.Active; } }
        public string Stance { get { return CompanionOrders.Stance(brain.State.companion.stance); } }
        public bool Regrouping { get { return regroup.Active; } }
        public bool AllowsAttack { get { return Active && Stance!="passive" && !regroup.Active && Legal(target) && InLeash(target) &&
            brain.CurrentMode==NpcMode.Fighting && !GameManager.Instance.DisableAI && !GameManager.IsGamePaused; } }
        public CompanionBow Bow { get; private set; }
        NpcBrain brain;
        EnemySenses senses;
        EnemyMotor motor;
        EnemyAttack attack;
        EnemyMotor.TakeActionCallback previousAction, companionAction;
        NpcMover mover;
        NpcGait gait;
        MobileUnit mobile;
        DaggerfallEntityBehaviour self, target;
        Transform area;
        DaggerfallInterior interior;
        DaggerfallDungeon dungeon;
        CharacterController body;
        Vector3 waitLocal;
        string status;
        bool owned, oldSenses, returning;
        MobileTeams oldTeam;
        int health;
        float scan;
        FollowNavigation navigation;
        GameObject goal;
        public string Description { get; private set; }
        public bool Active { get { return brain != null && brain.State != null && brain.CurrentMode != NpcMode.Dead &&
            CompanionCombatRules.Contract(brain.State.companion != null ? brain.State.companion.status : null) && !brain.IsEnemy && !brain.State.hostile; } }
        public Vector3 WaitingPosition { get { return area != null ? area.TransformPoint(waitLocal) : transform.position; } }
        public void Init(NpcBrain owner)
        {
            brain=owner; self=owner.EntityBehaviour; motor=owner.Motor; senses=GetComponent<EnemySenses>();
            attack=GetComponent<EnemyAttack>(); mover=GetComponent<NpcMover>(); gait=GetComponent<NpcGait>(); mobile=GetComponentInChildren<MobileUnit>();
            Bow=new CompanionBow(brain,mobile);
            health=self.Entity.CurrentHealth; Description="defensive"; companionAction=Chase; body=GetComponent<CharacterController>();
        }
        void Update() { if(!Active) Release(); else if(owned) Perceive(true); }
        void FixedUpdate() { if (owned && Active) Perceive(); }
        void LateUpdate() { if (owned && Active) Perceive(); }
        void OnDisable() { Release(); }
        void OnDestroy() { Release(); if(goal!=null) Destroy(goal); }
        void Release()
        {
            if (!owned) return;
            Bow.Restore();
            senses.enabled=oldSenses;
            if(motor.TakeActionHandler==companionAction) motor.TakeActionHandler=previousAction;
            if(self.Entity.Team==MobileTeams.PlayerAlly) self.Entity.Team=oldTeam;
            regroup.Reset(); push.Reset(); pushContact.Reset();
            owned=false; target=null; status=null; pursuit.Reset(); hits.Reset(); avoided.Clear(); navigation=null;
        }
        public void Reanchor()
        { area=transform.parent; waitLocal=transform.localPosition; status=null; returning=false; navigation=null; regroup.Reset(); push.Reset(); pushContact.Reset();
            interior=GetComponentInParent<DaggerfallInterior>(); dungeon=GetComponentInParent<DaggerfallDungeon>(); }
        internal void PreserveWaitingPoint(Vector3 position)
        { waitLocal=area!=null?area.InverseTransformPoint(position):transform.localPosition;status="waiting";returning=true; }
        bool Alive(DaggerfallEntityBehaviour e)
        { return e!=null && e.isActiveAndEnabled && e.Entity!=null && e.Entity.CurrentHealth>0; }
        bool Party(DaggerfallEntityBehaviour e)
        {
            if(e==GameManager.Instance.PlayerEntityBehaviour) return true;
            NpcBrain other=e!=null?e.GetComponent<NpcBrain>():null;
            return other!=null && other!=brain && other.State.companion!=null &&
                CompanionCombatRules.Contract(other.State.companion.status) && !other.IsEnemy && !other.State.hostile;
        }
        bool Legal(DaggerfallEntityBehaviour e)
        {
            if(!Alive(e) || e==self) return false;
            NpcBrain other=e.GetComponent<NpcBrain>(); EnemyMotor m=e.GetComponent<EnemyMotor>();
            var quest=e.GetComponent<QuestResourceBehaviour>();
            bool ally=Party(e) || e.Entity.Team==MobileTeams.PlayerAlly;
            EnemyEntity enemy=e.Entity as EnemyEntity;
            bool guard=other==null && (e.Entity.Team==MobileTeams.CityWatch || (enemy!=null && enemy.MobileEnemy.ID==(int)MobileTypes.Knight_CityWatch));
            bool hostile=other!=null?other.IsEnemy || other.AttacksPeople:m!=null && m.IsHostile;
            bool questProtected=false;
            if(quest!=null)
            {
                Foe foe=quest.TargetResource as Foe;
                DaggerfallEntityBehaviour victim=Victim(e);
                questProtected=CompanionCombatRules.QuestProtected(quest.IsAttackableByAI,hostile,foe!=null,
                    foe!=null && (foe.IsRestrained || foe.IsHidden),victim==self || Party(victim));
            }
            // Different loaded areas must never become one combat encounter.
            bool sameArea = e.GetComponentInParent<DaggerfallInterior>()==interior &&
                e.GetComponentInParent<DaggerfallDungeon>()==dungeon;
            return sameArea && CompanionCombatRules.Legal(true,e==GameManager.Instance.PlayerEntityBehaviour,ally,guard,
                questProtected,hostile);
        }
        int sightFrame=-1;
        DaggerfallEntityBehaviour sightTarget;
        Vector3 sightFrom,sightTo;
        bool sightResult;
        public int SightChecks {get;private set;}
        public int PerceptionWrites {get;private set;}
        bool Clear(DaggerfallEntityBehaviour e,bool fresh=false)
        {
            Vector3 from=transform.position+Vector3.up*0.4f,to=e.transform.position+Vector3.up*.4f;
            if(!fresh && sightFrame==Time.frameCount && sightTarget==e && sightFrom.Equals(from) && sightTo.Equals(to))return sightResult;
            sightFrame=Time.frameCount;sightTarget=e;sightFrom=from;sightTo=to;SightChecks++;
            Vector3 delta=to-from;
            int count=Physics.RaycastNonAlloc(from,delta.normalized,rays,delta.magnitude,SpotFinder.Mask,QueryTriggerInteraction.Ignore);
            sightResult=count<rays.Length;
            for(int i=0;i<count && sightResult;i++)
            {
                Collider c=rays[i].collider;
                if(c.transform==transform || c.transform.IsChildOf(transform) || c.GetComponentInParent<DaggerfallEntityBehaviour>()==e) continue;
                sightResult=false;
            }
            return sightResult;
        }
        // Launch checks deliberately bypass the frame cache: a bystander can move between callbacks.
        public bool ClearShot(DaggerfallEntityBehaviour e) {return e!=null && Clear(e,true);}
        public bool AllowsShotHit(DaggerfallEntityBehaviour e) {return AllowsAttack && e==target;}
        // Area effects may also reach other threats permitted by the current stance, never unrelated actors.
        public bool AllowsSpellHit(DaggerfallEntityBehaviour e,DaggerfallEntityBehaviour selected)
        {return AllowsAttack && target==selected && Legal(e) && InLeash(e) && (e==selected || Priority(e)>=0);}
        public bool PartyMember(DaggerfallEntityBehaviour e) {return e==self || Party(e);}
        Vector3 Anchor { get { return status=="waiting" ? WaitingPosition : GameManager.Instance.PlayerObject.transform.position; } }
        bool InLeash(DaggerfallEntityBehaviour e)
        { return CompanionCombatRules.WithinLeash(status=="waiting",Vector3.Distance(transform.position,Anchor),Vector3.Distance(e.transform.position,Anchor)); }
        DaggerfallEntityBehaviour Victim(DaggerfallEntityBehaviour e)
        {
            NpcBrain other=e.GetComponent<NpcBrain>();EnemySenses s=e.GetComponent<EnemySenses>();
            return s!=null && s.Target!=null?s.Target:other!=null?other.Threat:null;
        }
        int Priority(DaggerfallEntityBehaviour e)
        {
            DaggerfallEntityBehaviour victim=Victim(e);
            return CompanionOrders.Priority(Stance,status=="waiting",victim==self,victim==GameManager.Instance.PlayerEntityBehaviour,Party(victim));
        }
        void Perceive(bool takeBowBlow=false)
        {
            self.Entity.Team=MobileTeams.PlayerAlly;
            if(motor.TakeActionHandler!=companionAction)
            { previousAction=motor.TakeActionHandler; motor.TakeActionHandler=companionAction; }
            if(takeBowBlow && Bow.Drawing && (mobile.ShootArrow || mobile.DoMeleeDamage))
            { mobile.ShootArrow=false;mobile.DoMeleeDamage=false;Bow.Fire(); }
            // Never allow EnemyAttack to use a bow as a melee club or to create infinite native arrows.
            if(takeBowBlow && Bow.UsingBow) {mobile.DoMeleeDamage=false;mobile.ShootArrow=false;}
            bool safe=AllowsAttack;
            DaggerfallEntityBehaviour wanted=safe?target:null;
            if(wanted==null || senses.Target!=wanted) { mobile.DoMeleeDamage=false; mobile.ShootArrow=false; }
            senses.Target=wanted; senses.SecondaryTarget=null; senses.LastKnownDoor=null;
            senses.TargetRateOfApproach=0; senses.LastPositionDiff=Vector3.zero;
            Vector3 delta=wanted!=null?wanted.transform.position-transform.position:Vector3.zero;
            bool visible=wanted!=null && Clear(wanted);
            // Read the actual native fields, so another mod's changes are still corrected immediately.
            // Avoid reflection and boxed floats/vectors/bools when the value already matches.
            float distance=delta.magnitude;Vector3 direction=delta.normalized;
            if(senses.DistanceToTarget!=distance){distanceField.SetValue(senses,distance);PerceptionWrites++;}
            if(!senses.DirectionToTarget.Equals(direction)){directionField.SetValue(senses,direction);PerceptionWrites++;}
            if(senses.TargetInSight!=visible){sightField.SetValue(senses,visible);PerceptionWrites++;}
            if(senses.TargetInEarshot){earField.SetValue(senses,false);PerceptionWrites++;}
            senses.DetectedTarget=visible;
            if(wanted!=null)
            {
                senses.LastKnownTargetPos=wanted.transform.position; senses.PredictedTargetPos=wanted.transform.position;
                senses.OldLastKnownTargetPos=wanted.transform.position;
            }
            motor.GiveUpTimer=wanted!=null?200:0;
            attack.enabled=safe; motor.enabled=safe;
            if(brain.CurrentMode==NpcMode.Fighting) mover.enabled=true;
        }
        // The motor's public mod callback retains its gravity, paralysis, knockback and damage handling.
        // Local melee approach reuses follower floor/clearance checks and never requests catch-up.
        void Chase()
        {
            if(!Active || Stance=="passive" || regroup.Active || !Legal(target) || !InLeash(target) || brain.CurrentMode!=NpcMode.Fighting) return;
            Vector3 delta=target.transform.position-transform.position;Vector3 flat=delta;flat.y=0;
            if(flat.sqrMagnitude>.001f) transform.rotation=Quaternion.LookRotation(flat);
            NpcMagic magic=GetComponent<NpcMagic>();
            NpcSprite sprite=GetComponent<NpcSprite>();
            bool castingRange=magic!=null && magic.HoldsRange(delta.magnitude) && Clear(target);
            // A closing foe must change a drawn bow to melee even before its attack animation ends.
            if(!castingRange) Bow.Loadout(delta.magnitude);
            if(mobile.IsPlayingOneShot() || (sprite!=null && sprite.PlayingOneShot))return;
            if(castingRange)return;
            if(Bow.CanHoldRange(delta.magnitude) && Bow.Attack(delta.magnitude,Clear(target)))return;
            float reach=Mathf.Min(attack.MeleeDistance,attack.ClassicMeleeDistanceVsAI)-.1f;
            if(delta.magnitude<=reach) return;
            if(navigation==null) navigation=new FollowNavigation(brain,mover);
            Vector3 step,unused;
            if(navigation.Tick(target.transform,Time.deltaTime,false,out step,out unused))
                mover.MoveToward(step,gait!=null?gait.WalkSpeed:NpcMover.WalkSpeed);
        }
        void EndFight(string reason)
        {
            if(target!=null)
            {
                if(avoided.Count>=16) avoided.Clear();
                avoided[target]=Time.time+CompanionCombatRules.RetrySeconds;
            }
            Bow.Restore();
            target=null; pursuit.Reset(); navigation=null;
            brain.CompanionMode(null,NpcMode.Calm); returning=status=="waiting";
            Description=reason;
            NpcSprite sprite=GetComponent<NpcSprite>(); if(sprite!=null) sprite.CancelAttack();
            mobile.ChangeEnemyState(MobileStates.Idle);
            mobile.DoMeleeDamage=false; mobile.ShootArrow=false;
        }
        public void CancelOrders()
        {
            regroup.Reset(); push.Reset(); pushContact.Reset(); EndFight(Stance+"; "+brain.State.companion.status); avoided.Clear(); scan=0;
            if(owned) Perceive();
        }
        public void SetStance(string value)
        {
            push.Reset(); pushContact.Interrupt();
            // Do not reanchor a waiting person or clear accidental-hit warnings.
            if(Stance==CompanionOrders.Stance(value)) return;
            brain.State.companion.stance=CompanionOrders.Stance(value);
            bool wasRegrouping=regroup.Active;
            EndFight(Stance+"; "+brain.State.companion.status); avoided.Clear(); scan=0;
            if(wasRegrouping) brain.CompanionMode(null,NpcMode.Fleeing);
            if(owned) Perceive();
        }
        public void Regroup()
        {
            push.Reset(); pushContact.Interrupt(); EndFight("regrouping with player"); regroup.Begin();
            brain.CompanionMode(null,NpcMode.Fleeing);
            if(owned) Perceive();
        }
        public bool PushAway(bool automatic=false)
        {
            if(!Active || !owned || brain.CurrentMode!=NpcMode.Calm || regroup.Active) return false;
            push.Begin(); pushContact.Pushed(); pushPrevious=transform.localPosition;
            var voice=GetComponent<NpcVoice>();if(voice!=null)voice.OnCompanionPush(automatic);
            navigation=new FollowNavigation(brain,mover);
            var chat=brain.GetComponent<NpcChat>();if(chat!=null)chat.End(true);
            return true;
        }
        // Motor intent remains nonzero while the player's controller is blocked by a companion.
        // Require capsule proximity, vertical overlap, intent towards this person and no intervening wall.
        internal bool PlayerPressing()
        {
            var gm=GameManager.Instance;var player=gm.PlayerObject;
            var playerBody=player.GetComponent<CharacterController>();var playerMotor=gm.PlayerMotor;
            if(playerBody==null || !playerBody.enabled || playerMotor==null || !playerMotor.enabled || playerMotor.FreezeMotor>0 ||
                playerMotor.CancelMovement || body==null || !body.enabled) return false;
            Vector3 delta=body.bounds.center-playerBody.bounds.center;delta.y=0;
            float radius=body.radius*Mathf.Max(transform.lossyScale.x,transform.lossyScale.z)+
                playerBody.radius*Mathf.Max(player.transform.lossyScale.x,player.transform.lossyScale.z);
            if(delta.sqrMagnitude>(radius+.15f)*(radius+.15f) || delta.sqrMagnitude<.0001f ||
                body.bounds.min.y>=playerBody.bounds.max.y-.1f || playerBody.bounds.min.y>=body.bounds.max.y-.1f) return false;
            Vector3 intent=playerMotor.MoveDirection;intent.y=0;
            if(intent.sqrMagnitude<.04f || Vector3.Dot(intent.normalized,delta.normalized)<.7f) return false;
            float y=(Mathf.Max(body.bounds.min.y,playerBody.bounds.min.y)+Mathf.Min(body.bounds.max.y,playerBody.bounds.max.y))*.5f;
            Vector3 from=playerBody.bounds.center;from.y=y;
            int count=Physics.RaycastNonAlloc(from,delta.normalized,rays,delta.magnitude,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            if(count==rays.Length) return false;
            for(int i=0;i<count;i++)
            {
                var hit=rays[i].collider;
                if(hit.transform==transform || hit.transform.IsChildOf(transform) || hit.transform==player.transform || hit.transform.IsChildOf(player.transform))continue;
                return false;
            }
            return true;
        }
        void AutomaticPush()
        {
            bool eligible=brain.CurrentMode==NpcMode.Calm && !push.Active && !brain.IsTurret &&
                !self.Entity.IsParalyzed && DaggerfallUI.UIManager.TopWindow==DaggerfallUI.Instance.DaggerfallHUD;
            if(pushContact.Tick(Time.deltaTime,eligible && PlayerPressing(),eligible)) PushAway(true);
        }
        bool TickPush()
        {
            if(!push.Active) return false;
            Vector3 travelled=transform.localPosition-pushPrevious;travelled.y=0;
            pushPrevious=transform.localPosition;
            push.Tick(Time.deltaTime,travelled.magnitude);
            if(push.Moving && navigation==null) navigation=new FollowNavigation(brain,mover);
            Vector3 step;
            if(brain.EntityBehaviour.Entity.IsParalyzed) { mover.Stop(); return true; }
            if(push.Moving && navigation.StepAway(GameManager.Instance.PlayerObject.transform.position,out step))
                mover.MoveToward(step,gait!=null?gait.WalkSpeed:NpcMover.WalkSpeed);
            else { push.StopMoving(); mover.Stop(); }
            // Waiting companions keep their new spot, including after combat/save/load.
            if(status=="waiting") { waitLocal=transform.localPosition; returning=false; }
            Description="moving aside";
            return true;
        }
        void Travel(Vector3 destination)
        {
            if(brain.EntityBehaviour.Entity.IsParalyzed) { mover.Stop(); return; }
            if(goal==null) { goal=new GameObject("ANPC companion return position"); goal.transform.SetParent(area,false); }
            goal.transform.position=destination-Vector3.up*(transform.position.y-body.bounds.min.y);
            if(navigation==null) navigation=new FollowNavigation(brain,mover);
            Vector3 step,unused;
            if(navigation.Tick(goal.transform,Time.deltaTime,false,out step,out unused)) mover.MoveToward(step,gait!=null?gait.RunSpeed:NpcMover.RunSpeed);
            else mover.Stop();
        }
        public bool Tick()
        {
            if(!Active) { Release(); return false; }
            if(!owned)
            {
                if(distanceField==null || directionField==null || sightField==null || earField==null)
                { Description="combat integration unavailable"; return false; }
                owned=true; oldSenses=senses.enabled; senses.enabled=false; oldTeam=self.Entity.Team;
                Reanchor(); health=self.Entity.CurrentHealth;
            }
            string now=brain.State.companion.status;
            if(status!=now || area!=transform.parent)
            { area=transform.parent; waitLocal=transform.localPosition; status=now; EndFight(Stance+"; "+now); scan=0; }
            int current=self.Entity.CurrentHealth;
            if(current<health && motor.IsHostile)
            {
                bool betrayal=hits.Hit(); brain.ChangeOpinion(-5);
                if(betrayal)
                {
                    brain.State.companion.status="dismissed";
                    Release(); brain.CompanionBetrayed();
                    DaggerfallUI.AddHUDText(brain.DisplayName+": Our contract is over!"); return false;
                }
                DaggerfallUI.AddHUDText(brain.DisplayName+": Watch your weapon!");
            }
            health=current; motor.IsHostile=false; self.Entity.Team=MobileTeams.PlayerAlly;
            if(GameManager.Instance.DisableAI || GameManager.IsGamePaused) { pushContact.Interrupt(); Perceive(); mover.Stop(); return true; }
            hits.Tick(Time.deltaTime);
            if(regroup.Active)
            {
                Vector3 player=GameManager.Instance.PlayerObject.transform.position;
                bool arrived=Vector3.Distance(transform.position,player)<=2;
                regroup.Tick(Time.deltaTime,arrived);
                brain.CompanionMode(null,NpcMode.Fleeing); Description="regrouping with player"; Perceive();
                if(!arrived) Travel(player); else mover.Stop();
                if(!regroup.Active) { brain.CompanionMode(null,NpcMode.Calm); returning=status=="waiting"; navigation=null; scan=0; }
                return true;
            }
            if(target!=null)
            {
                if(!Legal(target) || !InLeash(target)) EndFight("returning; threat ended or pursuit limit");
                else
                {
                    float gap=Vector3.Distance(transform.position,target.transform.position);
                    bool visible=Clear(target);
                    NpcMagic magic=GetComponent<NpcMagic>();
                    bool holdingRange=visible && (Bow.CanHoldRange(gap) || (magic!=null && magic.HoldsRange(gap)));
                    if(holdingRange)pursuit.Reset();
                    if(pursuit.Tick(holdingRange?1.5f:gap,visible,Time.deltaTime)) EndFight("returning; enemy unreachable");
                }
            }
            if(target==null && (brain.CurrentMode==NpcMode.Fighting || brain.CurrentMode==NpcMode.Fleeing)) EndFight("returning; target lost");
            scan-=Time.deltaTime;
            if(scan<=0)
            {
                scan=0.3f;
                DaggerfallEntityBehaviour best=target;
                int rank=best!=null?Priority(best):int.MaxValue;
                if(rank<0) rank=3; // retain an engaged enemy until it is no longer legal/reachable
                float distance=best!=null?Vector3.Distance(transform.position,best.transform.position):float.MaxValue;
                int count=Physics.OverlapSphereNonAlloc(transform.position,Mathf.Min(20,brain.Definition.AggroRange),nearby,SpotFinder.Mask,QueryTriggerInteraction.Ignore);
                for(int i=0;i<count;i++)
                {
                    DaggerfallEntityBehaviour candidate=nearby[i].GetComponentInParent<DaggerfallEntityBehaviour>();
                    float until;
                    if(!Legal(candidate) || !InLeash(candidate) || (avoided.TryGetValue(candidate,out until) && Time.time<until)) continue;
                    int priority=Priority(candidate); if(priority<0 || priority>rank) continue;
                    float d=Vector3.Distance(transform.position,candidate.transform.position);
                    if(priority==rank && ((target!=null && best==target) || d>=distance)) continue; // keep an engaged peer; otherwise choose nearest
                    if(!Clear(candidate)) continue;
                    best=candidate; rank=priority; distance=d;
                }
                if(best!=target) { target=best; pursuit.Reset(); navigation=null; }
            }
            if(target!=null)
            {
                push.Reset(); pushContact.Interrupt();
                float fraction=(float)current/self.Entity.MaxHealth;
                bool retreat=Stance=="passive" || CompanionCombatRules.Retreat(fraction,brain.CurrentMode==NpcMode.Fleeing) || brain.CompanionWouldFlee();
                NpcMode wanted=retreat?NpcMode.Fleeing:NpcMode.Fighting;
                if(brain.CurrentMode!=wanted) navigation=null;
                brain.CompanionMode(target,wanted);
                Description=retreat?"retreating towards "+(status=="waiting"?"waiting position":"player"):"defending against "+target.name;
                Perceive();
                if(retreat)
                {
                    if(Vector3.Distance(transform.position,Anchor)>2) Travel(Anchor);
                    else mover.Stop();
                }
                return true;
            }
            Perceive();
            AutomaticPush(); if(TickPush()) return true;
            if(returning && status=="waiting")
            {
                if(Vector3.Distance(transform.position,WaitingPosition)>0.8f) { Description="returning to waiting position"; Travel(WaitingPosition); return true; }
                returning=false; navigation=null; Description=Stance+"; waiting";
            }
            Description=Stance+"; "+status;
            // The ordinary package driver owns following/standing after the combat decision.
            return false;
        }
    }
}

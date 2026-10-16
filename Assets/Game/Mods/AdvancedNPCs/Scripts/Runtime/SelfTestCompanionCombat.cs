using System;
using System.Collections;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Utility;
using AdvancedNPCs.Core;
namespace AdvancedNPCs
{
    public partial class SelfTest
    {
        public const string CompanionCombatSuite="companioncombat";
        NpcBrain CombatPerson(DaggerfallLocation location, NpcDefinition definition, string key, Vector3 position, bool recruited)
        {
            ids.Add(key);
            Vector3 local=location.transform.InverseTransformPoint(position);
            var person=new NpcInstance {Key=key,Definition=definition,Name=key,Race="Breton",Gender="Female",Seed=333,
                Persistent=true,HasFixedPosition=true,X=local.x,Y=local.y,Z=local.z};
            var state=new NpcState(); if(recruited) {state.companion=CompanionRecord.From(person);state.companion.area="outdoors";mod.States.Keep(key,state);}
            return spawner.SpawnChecked(person,state,location.transform,recruited);
        }
        void AimCombat(NpcBrain attacker,DaggerfallEntityBehaviour victim)
        {
            attacker.GetComponent<EnemyAttack>().enabled=false;attacker.Motor.enabled=false;
            attacker.EntityBehaviour.Entity.DeserializeSpellbook(new DaggerfallWorkshop.Game.MagicAndEffects.EffectBundleSettings[0]);
            var s=attacker.GetComponent<EnemySenses>();s.enabled=false;s.Target=victim;s.SecondaryTarget=null;s.LastKnownTargetPos=victim.transform.position;
            s.PredictedTargetPos=victim.transform.position;attacker.Motor.GiveUpTimer=200;
        }
        IEnumerator CompanionCombatSteps(DaggerfallLocation location,Transform player)
        {
            GameManager gm=GameManager.Instance;
            Vector3 oldPosition=player.position;Quaternion oldRotation=player.rotation,oldView=gm.MainCamera.transform.rotation;
            bool look=gm.PlayerMouseLook.enabled,oldAI=gm.DisableAI,enhanced=DaggerfallUnity.Settings.EnhancedCombatAI,infighting=DaggerfallUnity.Settings.EnemyInfighting;
            gm.PlayerMouseLook.enabled=false;
            Vector3 origin=oldPosition+Vector3.up*24;
            var geometry=new GameObject("ANPC companion combat verification");geometry.transform.SetParent(location.transform,false);
            NavigationBox(geometry.transform,"combat floor",origin-Vector3.up*.15f,new Vector3(110,.3f,50));
            NpcBrain actor=null,foe=null,second=null,neutral=null,ally=null;
            GameObject vanilla=null,watch=null,barrier=null;
            try
            {
                var definition=mod.Catalog.Generics.Find(delegate(NpcDefinition d){return d.Id=="ironclad_rogue_1";});
                if(definition==null){Fail("combat rogue setup","rogue missing");yield break;}
                NavigationPlayerAt(player,origin,Vector3.right);
                actor=CombatPerson(location,definition,"selftest_combat_rogue",origin+new Vector3(-2,1,0),true);
                var hostile=TestDefinition("selftest_companion_foe",Bravery.Brave,location,player,0);hostile.RecruitCost=-1;hostile.CrimeOnAttack=false;hostile.BaseClass="Warrior";hostile.Bravery=Bravery.Normal;hostile.FleeChance=0;
                foe=CombatPerson(location,hostile,"selftest_companion_foe",origin+new Vector3(8,1,0),false);
                neutral=CombatPerson(location,TestDefinition("selftest_companion_neutral",Bravery.Normal,location,player,0),"selftest_companion_neutral",origin+new Vector3(3,1,5),false);
                ally=CombatPerson(location,definition,"selftest_companion_ally",origin+new Vector3(-4,1,6),true);
                yield return new WaitForSeconds(1);
                mod.Companions.Command(ally,"wait");
                // Keep enemies in place so perception and actual melee can be checked independently.
                foe.GetComponent<EnemyAttack>().enabled=false;
                Check("recruited rogue remains calm around neutral people",actor.CurrentMode==NpcMode.Calm && actor.Threat==null,actor.Describe());
                foe.SwitchHostile(true);AimCombat(foe,gm.PlayerEntityBehaviour);foe.GetComponent<EnemyAttack>().enabled=false;
                yield return new WaitForSeconds(2);
                Check("rogue defends player against a hostile attacker",actor.CurrentMode==NpcMode.Fighting && actor.Threat==foe.EntityBehaviour,actor.Describe());
                Check("contract uses controlled senses and allied team",!actor.GetComponent<EnemySenses>().enabled && actor.EntityBehaviour.Entity.Team==MobileTeams.PlayerAlly,actor.Describe());
                int foeHealth=foe.EntityBehaviour.Entity.CurrentHealth;
                float deadline=Time.time+14;
                while(Time.time<deadline && foe.EntityBehaviour.Entity.CurrentHealth==foeHealth) yield return null;
                Check("rogue deals real DFU melee damage to selected enemy",foe.EntityBehaviour.Entity.CurrentHealth<foeHealth,"before="+foeHealth+" now="+foe.EntityBehaviour.Entity.CurrentHealth+"; "+actor.Describe());
                // Both AI modes must keep a neutral injected target from receiving the pending melee frame.
                int neutralHealth=neutral.EntityBehaviour.Entity.CurrentHealth,playerHealth=gm.PlayerEntity.CurrentHealth;
                Vector3 neutralBefore=neutral.transform.position;
                neutral.Teleport(actor.transform.position+actor.transform.forward*1.2f);
                foreach(bool mode in new[]{false,true})
                {
                    DaggerfallUnity.Settings.EnhancedCombatAI=mode;DaggerfallUnity.Settings.EnemyInfighting=true;
                    actor.GetComponent<EnemySenses>().Target=neutral.EntityBehaviour;
                    actor.GetComponentInChildren<MobileUnit>().DoMeleeDamage=true;
                    yield return new WaitForSeconds(.35f);
                    Check("protected target rejected with enhanced AI="+mode,neutral.EntityBehaviour.Entity.CurrentHealth==neutralHealth && actor.Threat!=neutral.EntityBehaviour,actor.Describe());
                }
                neutral.Teleport(neutralBefore);
                Check("combat never selects or damages player",actor.Threat!=gm.PlayerEntityBehaviour && gm.PlayerEntity.CurrentHealth==playerHealth,actor.Describe());
                foe.EntityBehaviour.Entity.CurrentHealth=0;yield return new WaitForSeconds(.7f);
                Check("enemy death clears fight and resumes follow",actor.CurrentMode==NpcMode.Calm && actor.Threat==null && actor.PackageDescription.Contains("following"),actor.Describe());
                NpcBrain.Discard(foe);foe=null;
                foe=CombatPerson(location,hostile,"selftest_companion_partyfoe",origin+new Vector3(0,1,6),false);
                yield return new WaitForSeconds(.4f);foe.SwitchHostile(true);AimCombat(foe,ally.EntityBehaviour);
                yield return new WaitForSeconds(.7f);
                Check("following companion defends another hired companion",actor.Threat==foe.EntityBehaviour,actor.Describe());
                foe.EntityBehaviour.Entity.CurrentHealth=0;yield return new WaitForSeconds(.4f);NpcBrain.Discard(foe);foe=null;
                // Only neutral anger at the player must not become a party fight.
                PlayerHit(neutral,gm.PlayerEntityBehaviour,1);yield return new WaitForSeconds(.7f);
                Check("player assault on a neutral is not assisted",actor.Threat!=neutral.EntityBehaviour,actor.Describe());
                NpcBrain.Discard(neutral);neutral=null;
                foe=CombatPerson(location,hostile,"selftest_companion_foe2",origin+new Vector3(9,1,0),false);
                second=CombatPerson(location,hostile,"selftest_companion_foe3",origin+new Vector3(8,1,6),false);
                yield return new WaitForSeconds(.5f);
                foe.SwitchHostile(true);AimCombat(foe,gm.PlayerEntityBehaviour);foe.GetComponent<EnemyAttack>().enabled=false;
                yield return new WaitForSeconds(.6f);
                var held=actor.Threat;
                second.SwitchHostile(true);AimCombat(second,gm.PlayerEntityBehaviour);second.GetComponent<EnemyAttack>().enabled=false;
                yield return new WaitForSeconds(.6f);
                Check("equally important threats do not cause target thrashing",actor.Threat==held,actor.Describe());
                // Direct attack takes precedence even over the player's existing enemy.
                actor.GetComponent<EnemyAttack>().MeleeTimer=30;
                yield return new WaitForSeconds(2); // let any preceding swing finish, then isolate target priorities
                AimCombat(foe,gm.PlayerEntityBehaviour);
                AimCombat(second,actor.EntityBehaviour);actor.Motor.MakeEnemyHostileToAttacker(second.EntityBehaviour);
                yield return new WaitForSeconds(.7f);
                Check("immediate attacker takes priority over player threat",actor.Threat==second.EntityBehaviour,actor.Describe());
                actor.GetComponent<EnemyAttack>().MeleeTimer=0;
                second.Teleport(origin+Vector3.right*45);foe.SwitchHostile(false);
                yield return new WaitForSeconds(.7f);
                Check("pursuit stops outside player leash",actor.CurrentMode==NpcMode.Calm && actor.Threat==null,actor.Describe());
                NpcBrain.Discard(foe);foe=null;NpcBrain.Discard(second);second=null;
                actor.Teleport(origin+new Vector3(-2,1,0));yield return new WaitForSeconds(.3f);
                mod.Companions.Command(actor,"wait");yield return new WaitForSeconds(.3f);
                Vector3 waited=actor.CompanionCombat.WaitingPosition;
                foe=CombatPerson(location,hostile,"selftest_companion_waitfoe",origin+new Vector3(5,1,0),false);
                yield return new WaitForSeconds(.5f);foe.SwitchHostile(true);AimCombat(foe,gm.PlayerEntityBehaviour);foe.GetComponent<EnemyAttack>().enabled=false;
                yield return new WaitForSeconds(.7f);
                Check("waiting companion does not chase player fights",actor.CurrentMode==NpcMode.Calm && actor.Threat==null,actor.Describe());
                AimCombat(foe,actor.EntityBehaviour);actor.Motor.MakeEnemyHostileToAttacker(foe.EntityBehaviour);
                yield return new WaitForSeconds(1.5f);
                Check("waiting companion defends herself locally",actor.CurrentMode==NpcMode.Fighting && actor.Threat==foe.EntityBehaviour,actor.Describe());
                Vector3 waitingRecord=new Vector3(actor.State.companion.x,actor.State.companion.y,actor.State.companion.z);
                yield return new WaitForSeconds(1);
                Check("fighting does not move the saved waiting anchor",Vector3.Distance(waited,actor.CompanionCombat.WaitingPosition)<.05f && Vector3.Distance(waitingRecord,new Vector3(actor.State.companion.x,actor.State.companion.y,actor.State.companion.z))<.05f,actor.Describe());
                mod.Companions.Reparent(actor,geometry.transform);
                yield return new WaitForSeconds(.7f);
                Check("ownership changes preserve waiting point during defence",Vector3.Distance(waited,actor.CompanionCombat.WaitingPosition)<.05f,actor.Describe());
                foe.EntityBehaviour.Entity.CurrentHealth=0;yield return new WaitForSeconds(5);
                Check("after defence she returns to waiting point",actor.CurrentMode==NpcMode.Calm && Vector3.Distance(actor.transform.position,waited)<1.2f && actor.State.companion.status=="waiting",actor.Describe());
                NpcBrain.Discard(foe);foe=null;
                mod.Companions.Command(actor,"follow");
                foe=CombatPerson(location,hostile,"selftest_companion_retreatfoe",origin+new Vector3(9,1,0),false);
                yield return new WaitForSeconds(.5f);foe.SwitchHostile(true);AimCombat(foe,gm.PlayerEntityBehaviour);foe.GetComponent<EnemyAttack>().enabled=false;
                actor.EntityBehaviour.Entity.CurrentHealth=Math.Max(1,actor.EntityBehaviour.Entity.MaxHealth/5);
                int injured=actor.EntityBehaviour.Entity.CurrentHealth;
                yield return new WaitForSeconds(.7f);
                Check("injured companion retreats without healing",actor.CurrentMode==NpcMode.Fleeing && actor.EntityBehaviour.Entity.CurrentHealth==injured,actor.Describe());
                foe.EntityBehaviour.Entity.CurrentHealth=0;yield return new WaitForSeconds(.5f);NpcBrain.Discard(foe);foe=null;
                actor.EntityBehaviour.Entity.CurrentHealth=actor.EntityBehaviour.Entity.MaxHealth;
                NavigationPlayerAt(player,origin,Vector3.right);actor.Teleport(origin+new Vector3(-2,1,0));
                vanilla=GameObjectHelper.CreateEnemy("selftest vanilla orc",MobileTypes.Orc,location.transform.InverseTransformPoint(origin+new Vector3(7,1,0)),MobileGender.Unspecified,location.transform,MobileReactions.Hostile);
                // Keep this perception/save fixture alive through real allied melee swings.
                var vanillaEntity=vanilla.GetComponent<DaggerfallEntityBehaviour>().Entity;
                vanillaEntity.MaxHealth=100000;vanillaEntity.CurrentHealth=100000;
                yield return new WaitForSeconds(.4f);
                var vs=vanilla.GetComponent<EnemySenses>();vs.enabled=false;vs.Target=gm.PlayerEntityBehaviour;
                vanilla.GetComponent<EnemyMotor>().enabled=false;vanilla.GetComponent<EnemyMotor>().IsHostile=true;vanilla.GetComponent<EnemyAttack>().enabled=false;
                yield return new WaitForSeconds(.7f);
                Check("companion also defends against vanilla enemies",actor.Threat==vanilla.GetComponent<DaggerfallEntityBehaviour>(),actor.Describe());
                gm.DisableAI=true;Vector3 frozen=actor.transform.position;
                yield return new WaitForSeconds(1);
                Check("disabled AI freezes companion combat",Vector3.Distance(frozen,actor.transform.position)<.2f && !actor.GetComponent<EnemyAttack>().enabled,actor.Describe());gm.DisableAI=false;
                // Saving during combat must retain health/contract and reconstruct a fresh safe target.
                actor.CaptureState();int savedHealth=actor.EntityBehaviour.Entity.CurrentHealth;
                var save=new NpcSaveDataInterface(mod.States,mod.Placed,mod.Flags,mod.RaiseStateRestored);
                string encoded=SaveLoadManager.Serialize(typeof(NpcSaveData),save.GetSaveData());
                save.RestoreSaveData(SaveLoadManager.Deserialize(typeof(NpcSaveData),encoded));
                yield return new WaitForSeconds(1.5f);actor=NpcBrain.Find("selftest_combat_rogue");
                Check("combat save restoration preserves contract health and reacquires threat",actor!=null && actor.State.companion.status=="following" &&
                    !actor.State.hostile && actor.EntityBehaviour.Entity.CurrentHealth==savedHealth && actor.Threat==vanilla.GetComponent<DaggerfallEntityBehaviour>(),(actor!=null?actor.Describe():"missing")+"; saved health="+savedHealth+"; foe health="+vanillaEntity.CurrentHealth);
                if(actor==null) yield break;
                // Break sight after engagement. No hidden catch-up is allowed while fighting.
                Vector3 between=(actor.transform.position+vanilla.transform.position)*.5f;
                barrier=NavigationBox(geometry.transform,"combat sight barrier",new Vector3(between.x,origin.y+2,between.z),new Vector3(.5f,4,40));
                yield return new WaitForSeconds(7);
                Check("unreachable enemy is released instead of chased indefinitely",actor.CurrentMode==NpcMode.Calm && actor.Threat==null,actor.Describe());
                UnityEngine.Object.Destroy(barrier);barrier=null;UnityEngine.Object.Destroy(vanilla);vanilla=null;
                watch=GameObjectHelper.CreateEnemy("selftest uninvolved guard",MobileTypes.Orc,location.transform.InverseTransformPoint(origin+new Vector3(5,1,3)),MobileGender.Male,location.transform,MobileReactions.Hostile);
                watch.GetComponent<DaggerfallEntityBehaviour>().Entity.Team=MobileTeams.CityWatch;
                watch.GetComponent<EnemyMotor>().enabled=false;watch.GetComponent<EnemyAttack>().enabled=false;
                watch.GetComponent<EnemySenses>().enabled=false;watch.GetComponent<EnemySenses>().Target=gm.PlayerEntityBehaviour;
                watch.GetComponent<EnemyMotor>().enabled=false;watch.GetComponent<EnemyMotor>().IsHostile=true;watch.GetComponent<EnemyAttack>().enabled=false;
                yield return new WaitForSeconds(.7f);
                Check("guard-team hostility does not drag companion into player crimes",actor.Threat!=watch.GetComponent<DaggerfallEntityBehaviour>(),actor.Describe());
                UnityEngine.Object.Destroy(watch);watch=null;
                int before=actor.EntityBehaviour.Entity.CurrentHealth;
                PlayerHit(actor,gm.PlayerEntityBehaviour,1);yield return new WaitForSeconds(.3f);
                Check("one accidental player hit keeps contract without healing",actor.State.companion.status=="following" && !actor.State.hostile && actor.EntityBehaviour.Entity.CurrentHealth==before-1,actor.Describe());
                PlayerHit(actor,gm.PlayerEntityBehaviour,1);yield return new WaitForSeconds(.3f);
                PlayerHit(actor,gm.PlayerEntityBehaviour,1);yield return new WaitForSeconds(.3f);
                Check("three recent damaging player hits end contract explicitly",actor.State.companion.status=="dismissed" && actor.State.hostile && actor.Threat==gm.PlayerEntityBehaviour,actor.Describe());
                Check("ending contract restores vanilla senses",actor.GetComponent<EnemySenses>().enabled,actor.Describe());
                Check("ending contract restores previous motor action",actor.Motor.TakeActionHandler!=null && actor.Motor.TakeActionHandler.Method.Name!="Chase",actor.Describe());
            }
            finally
            {
                NpcBrain.Discard(actor);NpcBrain.Discard(foe);NpcBrain.Discard(second);NpcBrain.Discard(neutral);NpcBrain.Discard(ally);
                UnityEngine.Object.Destroy(vanilla);UnityEngine.Object.Destroy(watch);UnityEngine.Object.Destroy(barrier);
                UnityEngine.Object.Destroy(geometry);gm.DisableAI=oldAI;DaggerfallUnity.Settings.EnhancedCombatAI=enhanced;DaggerfallUnity.Settings.EnemyInfighting=infighting;
                player.position=oldPosition;player.rotation=oldRotation;gm.MainCamera.transform.rotation=oldView;gm.PlayerMouseLook.enabled=look;
            }
        }
    }
}

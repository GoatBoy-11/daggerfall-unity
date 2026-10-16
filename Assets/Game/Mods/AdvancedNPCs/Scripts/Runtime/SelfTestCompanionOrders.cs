using System;
using System.Collections;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Serialization;
using AdvancedNPCs.Core;
namespace AdvancedNPCs
{
    public partial class SelfTest
    {
        public const string CompanionOrdersSuite="orders";
        IEnumerator CompanionOrdersSteps(DaggerfallLocation location,Transform player)
        {
            var gm=GameManager.Instance;
            Vector3 oldPosition=player.position;Quaternion oldRotation=player.rotation,oldView=gm.MainCamera.transform.rotation;
            bool look=gm.PlayerMouseLook.enabled,oldAI=gm.DisableAI;
            gm.PlayerMouseLook.enabled=false;
            Vector3 origin=oldPosition+Vector3.up*24;
            var geometry=new GameObject("ANPC orders verification");geometry.transform.SetParent(location.transform,false);
            NavigationBox(geometry.transform,"orders floor",origin-Vector3.up*.15f,new Vector3(110,.3f,50));
            NpcBrain actor=null,foe=null,ally=null,neutral=null;
            GameObject guard=null;
            try
            {
                var definition=mod.Catalog.Generics.Find(delegate(NpcDefinition d){return d.Id=="ironclad_rogue_1";});
                if(definition==null){Fail("orders setup","rogue missing");yield break;}
                NavigationPlayerAt(player,origin,Vector3.right);
                actor=CombatPerson(location,definition,"selftest_orders_rogue",origin+new Vector3(-2,1,0),true);
                ally=CombatPerson(location,definition,"selftest_orders_ally",origin+new Vector3(-2,1,6),true);
                var hostile=TestDefinition("selftest_orders_enemy",Bravery.Normal,location,player,0);
                hostile.RecruitCost=-1;hostile.CrimeOnAttack=false;hostile.BaseClass="Warrior";hostile.FleeChance=0;
                foe=CombatPerson(location,hostile,"selftest_orders_enemy",origin+new Vector3(8,1,0),false);
                neutral=CombatPerson(location,hostile,"selftest_orders_neutral",origin+new Vector3(8,1,5),false);
                yield return new WaitForSeconds(.6f);
                // Push through the actual menu, then verify walking, AI pause, wait anchors and cancellation.
                gm.DisableAI=true;
                CompanionOrdersWindow.Open(actor);yield return new WaitForSecondsRealtime(.2f);
                var pushMenu=DaggerfallUI.UIManager.TopWindow as CompanionOrdersWindow;
                int pushIndex=-1;
                if(pushMenu!=null) for(int i=0;i<pushMenu.ListBox.Count;i++)
                    if(pushMenu.ListBox.GetItem(i).textLabel.Text=="Push") pushIndex=i;
                Check("calm companion menu offers Push",pushIndex>=0,actor.Describe());
                if(pushIndex>=0) {pushMenu.ListBox.SelectedIndex=pushIndex;pushMenu.ListBox.UseSelectedItem();}
                Vector3 pushStart=actor.transform.position;
                yield return new WaitForSeconds(.4f);
                Check("disabled AI freezes Push",actor.CompanionCombat.Pushing && Vector3.Distance(pushStart,actor.transform.position)<.2f,actor.Describe());
                gm.DisableAI=false;yield return new WaitForSeconds(2.5f);
                Check("Push walks away and briefly stays clear without changing Follow",actor.CompanionCombat.Pushing && actor.State.companion.status=="following" &&
                    Vector3.Distance(pushStart,actor.transform.position)>2 && Vector3.Distance(actor.transform.position,player.position)>3.5f,actor.Describe());
                yield return new WaitForSeconds(4);
                Check("Push finishes and following resumes",!actor.CompanionCombat.Pushing && Vector3.Distance(actor.transform.position,player.position)<3,actor.Describe());
                mod.Companions.Command(actor,"wait");
                pushStart=actor.transform.position;
                Check("waiting companion accepts Push",mod.Companions.Command(actor,"push"),actor.Describe());
                yield return new WaitForSeconds(6);
                Check("Push updates a waiting companion's anchor",actor.State.companion.status=="waiting" &&
                    Vector3.Distance(pushStart,actor.transform.position)>2 && Vector3.Distance(actor.CompanionCombat.WaitingPosition,actor.transform.position)<.2f,actor.Describe());
                // An outward wall blocks every route rather than allowing a shove through geometry.
                Vector3 wallAt=actor.transform.position-Vector3.right*.8f;
                var pushWall=NavigationBox(geometry.transform,"push wall",wallAt,new Vector3(.3f,4,20));
                pushStart=actor.transform.position;
                mod.Companions.Command(actor,"push");yield return new WaitForSeconds(5);
                Check("Push respects a blocking wall and expires",!actor.CompanionCombat.Pushing &&
                    actor.transform.position.x>wallAt.x+.15f && Vector3.Distance(pushStart,actor.transform.position)<1,actor.Describe());
                UnityEngine.Object.Destroy(pushWall);
                Check("Push can be repeated",mod.Companions.Command(actor,"push"),actor.Describe());
                mod.Companions.Command(actor,"follow");
                Check("Follow cancels Push immediately",!actor.CompanionCombat.Pushing && actor.State.companion.status=="following",actor.Describe());
                actor.Teleport(origin+new Vector3(-2,1,0));
                mod.Companions.Command(ally,"wait");foe.SwitchHostile(true);AimCombat(foe,neutral.EntityBehaviour);
                yield return new WaitForSeconds(.6f);
                Check("default defensive ignores an unrelated hostile fight",actor.CompanionCombat.Stance=="defensive" && actor.Threat==null,actor.Describe());
                mod.Companions.Command(actor,"aggressive");
                yield return new WaitForSeconds(.6f);
                Check("aggressive acquires visible eligible unrelated hostile",actor.CurrentMode==NpcMode.Fighting && actor.Threat==foe.EntityBehaviour,actor.Describe());
                Check("stance is per companion",ally.CompanionCombat.Stance=="defensive",ally.Describe());
                mod.Companions.Command(actor,"defensive");
                yield return new WaitForSeconds(.6f);
                Check("switching to defensive cancels unrelated pursuit",actor.CurrentMode==NpcMode.Calm && actor.Threat==null,actor.Describe());
                AimCombat(foe,gm.PlayerEntityBehaviour);yield return new WaitForSeconds(.6f);
                Check("defensive still protects the player",actor.Threat==foe.EntityBehaviour,actor.Describe());
                actor.EntityBehaviour.Entity.CurrentHealth=Math.Max(1,actor.EntityBehaviour.Entity.MaxHealth/2);
                int injured=actor.EntityBehaviour.Entity.CurrentHealth,gold=gm.PlayerEntity.GoldPieces;
                var mobile=actor.GetComponentInChildren<MobileUnit>();mobile.DoMeleeDamage=true;mobile.ShootArrow=true;
                mod.Companions.Command(actor,"passive");
                Check("passive immediately clears a pending swing and shot",!mobile.DoMeleeDamage && !mobile.ShootArrow && !actor.GetComponent<NpcSprite>().BlowPending && !actor.GetComponent<EnemyAttack>().enabled,actor.Describe());
                yield return new WaitForSeconds(.6f);
                Check("passive retreats from party danger without attacking",actor.CurrentMode==NpcMode.Fleeing && !actor.GetComponent<EnemyAttack>().enabled && actor.GetComponent<EnemySenses>().Target==null,actor.Describe());
                Check("tactics preserve health gold and movement order",actor.EntityBehaviour.Entity.CurrentHealth==injured && gm.PlayerEntity.GoldPieces==gold && actor.State.companion.status=="following",actor.Describe());
                mod.Companions.Command(actor,"defensive");yield return new WaitForSeconds(.5f);
                bool regroupAccepted=mod.Companions.Command(actor,"regroup");
                Check("regroup cancels pursuit immediately",regroupAccepted && actor.Threat==null && actor.CompanionCombat.Regrouping && !actor.GetComponent<EnemyAttack>().enabled,actor.Describe());
                yield return new WaitForSeconds(1);
                Check("regroup suppresses reacquisition under pressure",actor.Threat==null && actor.CompanionCombat.Regrouping && actor.State.companion.stance=="defensive",actor.Describe());
                gm.DisableAI=true;Vector3 frozen=actor.transform.position;
                yield return new WaitForSeconds(8.5f);
                Check("disabled AI freezes regroup movement and active clock",actor.CompanionCombat.Regrouping && Vector3.Distance(frozen,actor.transform.position)<.2f,actor.Describe());gm.DisableAI=false;
                yield return new WaitForSeconds(8);
                Check("completed regroup resumes saved defensive behaviour",!actor.CompanionCombat.Regrouping && actor.Threat==foe.EntityBehaviour,actor.Describe());
                // Combat interaction opens native orders without asking this actor to talk.
                bool waitedInCombat=mod.Companions.Command(actor,"wait");
                Check("wait is available during combat and cancels pursuit",waitedInCombat && actor.State.companion.status=="waiting" && actor.Threat==null,actor.Describe());
                bool followedAgain=mod.Companions.Command(actor,"follow");
                Check("follow changes movement without changing stance or health",followedAgain && actor.State.companion.status=="following" && actor.State.companion.stance=="defensive" && actor.EntityBehaviour.Entity.CurrentHealth==injured,actor.Describe());
                gm.DisableAI=true;actor.Teleport(origin+new Vector3(-2,1,0));gm.DisableAI=false;
                mod.Companions.Command(actor,"passive");mod.Companions.Command(actor,"defensive");
                yield return new WaitForSeconds(.3f);gm.DisableAI=true;
                var talk=actor.GetComponent<NpcTalk>();
                talk.HandleActivate(PlayerActivateModes.Steal,1);
                Check("steal activation does not open orders",!(DaggerfallUI.UIManager.TopWindow is CompanionOrdersWindow),actor.Describe());
                talk.HandleActivate(PlayerActivateModes.Grab,50);
                Check("distant activation does not open orders",!(DaggerfallUI.UIManager.TopWindow is CompanionOrdersWindow),actor.Describe());
                talk.HandleActivate(PlayerActivateModes.Info,1);
                Check("info shows saved stance and health condition",talk.LastMessage.Contains("defensive") && talk.LastMessage.Contains("Wounded"),talk.LastMessage);
                talk.HandleActivate(PlayerActivateModes.Grab,1);yield return new WaitForSecondsRealtime(.3f);
                var menu=DaggerfallUI.UIManager.TopWindow as CompanionOrdersWindow;
                Check("combat activation opens native orders menu",menu!=null && menu.IsSetup && actor.CurrentMode==NpcMode.Fighting,actor.Describe());
                if(menu==null) yield break;
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(Application.persistentDataPath,"ANPC-orders.png"));
                yield return new WaitForSecondsRealtime(.2f);
                menu.ListBox.SelectedIndex=2;menu.ListBox.UseSelectedItem();
                Check("native picker applies passive without conversation or charge",actor.State.companion.stance=="passive" && gm.PlayerEntity.GoldPieces==gold && !(DaggerfallUI.UIManager.TopWindow is CompanionOrdersWindow),actor.Describe());
                for(int i=0;i<3;i++)
                {
                    CompanionOrdersWindow.Open(actor);yield return new WaitForSecondsRealtime(.1f);
                    menu=DaggerfallUI.UIManager.TopWindow as CompanionOrdersWindow;if(menu!=null) menu.CancelWindow();
                    yield return new WaitForSecondsRealtime(.1f);
                }
                Check("repeated open and Escape leaves no orders window",!(DaggerfallUI.UIManager.TopWindow is CompanionOrdersWindow) && actor.State.companion.stance=="passive",actor.Describe());
                gm.DisableAI=false;foe.EntityBehaviour.Entity.CurrentHealth=0;yield return new WaitForSeconds(.5f);
                NpcBrain.Discard(foe);foe=null;
                actor.Teleport(origin+new Vector3(-5,1,0));yield return new WaitForSeconds(.3f);mod.Companions.Command(actor,"wait");yield return new WaitForSeconds(.3f);
                Vector3 anchor=actor.CompanionCombat.WaitingPosition;
                mod.Companions.Command(actor,"aggressive");mod.Companions.Command(actor,"regroup");
                yield return new WaitForSeconds(2);
                Check("waiting regroup approaches player without overwriting waiting point",actor.State.companion.status=="waiting" && Vector3.Distance(anchor,actor.CompanionCombat.WaitingPosition)<.05f && Vector3.Distance(actor.transform.position,anchor)>1,actor.Describe());
                yield return new WaitForSeconds(9);
                Check("waiting companion returns after temporary regroup",actor.State.companion.status=="waiting" && Vector3.Distance(actor.transform.position,anchor)<1.2f && actor.CompanionCombat.Stance=="aggressive",actor.Describe());
                // Real DFU serializer, including a previous-format record with its stance field absent.
                var oldRecord=(CompanionRecord)SaveLoadManager.Deserialize(typeof(CompanionRecord),"{\"status\":\"waiting\"}");
                Check("missing stance in legacy save defaults to defensive",CompanionOrders.Stance(oldRecord.stance)=="defensive","legacy stance="+oldRecord.stance);
                mod.Companions.Command(actor,"passive");mod.Companions.Command(actor,"regroup");
                actor.CaptureState();var save=new NpcSaveDataInterface(mod.States,mod.Placed,mod.Flags,mod.RaiseStateRestored);
                string encoded=SaveLoadManager.Serialize(typeof(NpcSaveData),save.GetSaveData());
                var restored=(NpcSaveData)SaveLoadManager.Deserialize(typeof(NpcSaveData),encoded);
                Check("save format records stance without transient regroup data",restored.Version==SaveFormat.Current && encoded.Contains("passive") && !encoded.Contains("regroup"),"version="+restored.Version);
                // Stale UI selection must not modify the replacement person or pop an unrelated screen.
                NavigationPlayerAt(player,actor.transform.position+Vector3.right,Vector3.left);
                CompanionOrdersWindow.Open(actor);yield return new WaitForSecondsRealtime(.2f);
                menu=DaggerfallUI.UIManager.TopWindow as CompanionOrdersWindow;
                save.RestoreSaveData(restored);yield return new WaitForSecondsRealtime(1);
                actor=NpcBrain.Find("selftest_orders_rogue");
                if(menu!=null) {menu.ListBox.SelectedIndex=1;menu.ListBox.UseSelectedItem();}
                Check("load preserves stance health and waiting point and clears regroup",actor!=null && actor.State.companion.stance=="passive" && actor.State.companion.status=="waiting" && !actor.CompanionCombat.Regrouping && actor.EntityBehaviour.Entity.CurrentHealth==injured && Vector3.Distance(anchor,actor.CompanionCombat.WaitingPosition)<.2f,actor!=null?actor.Describe():"missing");
                Check("stale menu cannot issue orders to restored actor",actor!=null && actor.State.companion.stance=="passive" && !(DaggerfallUI.UIManager.TopWindow is CompanionOrdersWindow),actor!=null?actor.Describe():"missing");
                if(actor==null) yield break;
                // Dialogue access opens the same menu after the talk window closes.
                NavigationPlayerAt(player,actor.transform.position+Vector3.right,Vector3.left);
                yield return OpenTalk(actor);yield return UseTopic("Companion orders");yield return new WaitForSecondsRealtime(.3f);
                Check("calm dialogue opens orders after closing conversation",DaggerfallUI.UIManager.TopWindow is CompanionOrdersWindow && !mod.Topics.Active,actor.Describe());
                DaggerfallUI.Instance.PopToHUD();
                mod.Companions.Command(actor,"follow");mod.Companions.Command(actor,"aggressive");
                guard=DaggerfallWorkshop.Utility.GameObjectHelper.CreateEnemy("orders guard fixture",MobileTypes.Orc,location.transform.InverseTransformPoint(origin+new Vector3(4,1,3)),MobileGender.Male,location.transform,MobileReactions.Hostile);
                yield return new WaitForSeconds(.3f);
                guard.GetComponent<DaggerfallEntityBehaviour>().Entity.Team=MobileTeams.CityWatch;
                guard.GetComponent<EnemySenses>().enabled=false;guard.GetComponent<EnemySenses>().Target=gm.PlayerEntityBehaviour;
                guard.GetComponent<EnemyMotor>().enabled=false;guard.GetComponent<EnemyMotor>().IsHostile=true;guard.GetComponent<EnemyAttack>().enabled=false;
                yield return new WaitForSeconds(.5f);
                Check("aggression still excludes guards civilians and allies",actor.Threat==null,actor.Describe());
                ally=NpcBrain.Find("selftest_orders_ally");ally.SwitchHostile(true);
                Check("hostile contract rejects orders and menu",!mod.Companions.Command(ally,"passive") && !CompanionOrdersWindow.Open(ally),ally.Describe());
                ally.EntityBehaviour.Entity.CurrentHealth=0;
                Check("dead contract rejects orders",!mod.Companions.Command(ally,"regroup"),ally.Describe());
                // Walk into a waiting companion using native movement input.
                mod.Companions.Command(actor,"wait");
                var playerBody=player.GetComponent<CharacterController>();var actorBody=actor.GetComponent<CharacterController>();
                yield return new WaitForSeconds(.2f);
                Vector3 beside=actor.transform.position+Vector3.right*(playerBody.radius+actorBody.radius+.03f);
                beside.y=actorBody.bounds.min.y+.02f;NavigationPlayerAt(player,beside,Vector3.left);
                player.rotation=Quaternion.LookRotation(Vector3.left);
                yield return new WaitForSeconds(.9f);
                Check("standing beside companion does not auto-push",!actor.CompanionCombat.Pushing,actor.Describe());
                Vector3 contactStart=actor.transform.position;float contactUntil=Time.time+1.4f;bool bumped=false;
                while(Time.time<contactUntil)
                {
                    InputManager.Instance.ApplyVerticalForce(1);InputManager.Instance.AddAction(InputManager.Actions.MoveForwards);
                    bumped |= actor.CompanionCombat.Pushing;yield return null;
                }
                InputManager.Instance.ClearAllActions();gm.PlayerMotor.CancelMovement=true;
                yield return new WaitForSeconds(2);
                Check("sustained walking into companion triggers the same Push",bumped && actor.CompanionCombat.Pushing &&
                    Vector3.Distance(contactStart,actor.transform.position)>1.5f && actor.State.companion.status=="waiting",actor.Describe());
                actor.Teleport(origin+new Vector3(-2,1,0));mod.Companions.Command(actor,"follow");
                NavigationPlayerAt(player,origin,Vector3.right);player.rotation=oldRotation;
                yield return new WaitForSeconds(.3f);
                mod.Companions.Command(actor,"dismiss");
                Check("dismissed contract rejects tactical orders and menu",!mod.Companions.Command(actor,"aggressive") && !CompanionOrdersWindow.Open(actor),actor.Describe());
            }
            finally
            {
                InputManager.Instance.ClearAllActions();gm.PlayerMotor.CancelMovement=true;
                DaggerfallUI.Instance.PopToHUD();
                NpcBrain.Discard(actor);NpcBrain.Discard(foe);NpcBrain.Discard(NpcBrain.Find("selftest_orders_ally"));NpcBrain.Discard(neutral);
                UnityEngine.Object.Destroy(geometry);UnityEngine.Object.Destroy(guard);gm.DisableAI=oldAI;
                player.position=oldPosition;player.rotation=oldRotation;gm.MainCamera.transform.rotation=oldView;gm.PlayerMouseLook.enabled=look;
            }
        }
    }
}

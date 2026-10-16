using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Questing;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Utility;
using AdvancedNPCs.Core;
namespace AdvancedNPCs
{
    public partial class SelfTest
    {
        public const string CompanionPlaytestSuite="companionbugs";
        NpcBrain PlaytestPerson(Transform parent,NpcDefinition definition,string key,Vector3 position)
        {
            ids.Add(key);Vector3 p=parent.InverseTransformPoint(position);
            return spawner.SpawnChecked(new NpcInstance {Key=key,Definition=definition,Name="Inn mercenary test",Race="Breton",Gender="Female",Seed=333,
                Persistent=false,HasFixedPosition=true,X=p.x,Y=p.y,Z=p.z},new NpcState(),parent);
        }
        IEnumerator CompanionPlaytestSteps(DaggerfallLocation location,Transform player)
        {
            var gm=GameManager.Instance;var pee=gm.PlayerEnterExit;
            Vector3 oldPosition=player.position;Quaternion oldRotation=player.rotation,oldView=gm.MainCamera.transform.rotation;
            bool look=gm.PlayerMouseLook.enabled,oldAI=gm.DisableAI;int gold=gm.PlayerEntity.GoldPieces;
            gm.PlayerMouseLook.enabled=false;gm.PlayerEntity.GoldPieces=Math.Max(gold,1000);
            NpcBrain actor=null,neutral=null;GameObject geometry=null,ghost=null;
            var keyField=typeof(DaggerfallUI).GetField("lastKeyCode",BindingFlags.NonPublic|BindingFlags.Instance);
            var questField=typeof(QuestResourceBehaviour).GetField("targetResource",BindingFlags.NonPublic|BindingFlags.Instance);
            bool oldKeyboard=false;ListBox topics=null;
            try
            {
                var definition=mod.Catalog.Generics.Find(delegate(NpcDefinition d){return d.Id=="ironclad_rogue_1";});
                if(definition==null) {Fail("playtest setup","rogue missing");yield break;}
                DaggerfallStaticDoors doors;StaticDoor door;
                if(!FindBuildingDoor(location,"tavern",out doors,out door)) {Fail("playtest inn setup","no tavern");yield break;}
                pee.TransitionInterior(doors.transform,door,false);yield return new WaitForSecondsRealtime(1);
                Transform room=pee.Interior.transform;Vector3 origin=player.position+Vector3.up*24;
                geometry=new GameObject("ANPC fresh hire floor");geometry.transform.SetParent(room,false);
                NavigationBox(geometry.transform,"fresh hire floor",origin-Vector3.up*.15f,new Vector3(60,.3f,30));
                NavigationPlayerAt(player,origin,Vector3.right);
                actor=PlaytestPerson(room,definition,"selftest_fresh_inn_hire",origin+new Vector3(-2,1,0));
                yield return new WaitForSeconds(.6f);
                Check("inn fixture starts as an unscheduled wandering non-companion",actor.State.companion==null && definition.Schedule.Count==0,actor.Describe());
                yield return OpenTalk(actor);yield return UseTopic("Hire you (100 gold)");DaggerfallUI.Instance.PopToHUD();
                yield return new WaitForSecondsRealtime(.4f);
                Check("fresh hire selects follow before any area change",actor.State.companion!=null && actor.PackageDescription.Contains("companion following"),actor.Describe());
                Vector3 hired=actor.transform.position;NavigationPlayerAt(player,origin+Vector3.right*8,Vector3.left);
                yield return new WaitForSeconds(5);
                Check("fresh hire walks to player in same inn",pee.Interior.transform==room && Vector3.Distance(actor.transform.position,player.position)<4 && Vector3.Distance(hired,actor.transform.position)>3,actor.Describe());
                NpcBrain.Discard(actor);mod.States.Remove("selftest_fresh_inn_hire");actor=null;UnityEngine.Object.Destroy(geometry);geometry=null;
                pee.TransitionExterior(false);yield return new WaitForSecondsRealtime(1);
                origin=player.position+Vector3.up*24;geometry=new GameObject("ANPC companion bugs floor");geometry.transform.SetParent(location.transform,false);
                NavigationBox(geometry.transform,"companion bugs floor",origin-Vector3.up*.15f,new Vector3(70,.3f,35));
                NavigationPlayerAt(player,origin,Vector3.right);
                actor=CombatPerson(location,definition,"selftest_bug_rogue",origin+new Vector3(-2,1,0),true);
                yield return new WaitForSeconds(.5f);gm.DisableAI=true;
                // Warm the persistent vanilla window, matching normal play after hiring through dialogue.
                yield return OpenTalk(actor);yield return new WaitForSecondsRealtime(.2f);
                int greetingLines=mod.Topics.ConversationLines().Count;
                topics=TopicListBox(DaggerfallUI.Instance.TalkWindow);oldKeyboard=topics.AlwaysAcceptKeyboardInput;topics.AlwaysAcceptKeyboardInput=true;
                DaggerfallUI.Instance.PopToHUD();yield return null;
                for(int repeat=0;repeat<3;repeat++)
                {
                    CompanionOrdersWindow.Open(actor);yield return new WaitForSecondsRealtime(.15f);
                    var menu=DaggerfallUI.UIManager.TopWindow as CompanionOrdersWindow;
                    if(menu==null){Fail("Talk input fixture","orders missing");yield break;}
                    menu.ListBox.SelectedIndex=BarkMenuIndex(menu,"Talk");menu.ListBox.AlwaysAcceptKeyboardInput=true;
                    keyField.SetValue(DaggerfallUI.Instance,KeyCode.Return);
                    menu.ListBox.Update();
                    Check("Talk waits for selection input to finish #"+repeat,DaggerfallUI.UIManager.TopWindow==menu,"top="+DaggerfallUI.UIManager.TopWindow.GetType().Name);
                    keyField.SetValue(DaggerfallUI.Instance,KeyCode.None);
                    yield return new WaitForSecondsRealtime(.25f);
                    Check("Talk opens vanilla dialogue after release #"+repeat,DaggerfallUI.UIManager.TopWindow==DaggerfallUI.Instance.TalkWindow,"top="+DaggerfallUI.UIManager.TopWindow.GetType().Name);
                    var currentTopics=DaggerfallUI.Instance.TalkWindow.GetType().GetField("listCurrentTopics",BindingFlags.NonPublic|BindingFlags.Instance);
                    Check("Talk opens without selecting or expanding a topic #"+repeat,mod.Topics.ConversationLines().Count==greetingLines &&
                        object.ReferenceEquals(currentTopics.GetValue(DaggerfallUI.Instance.TalkWindow),TalkManager.Instance.ListTopicLocation),
                        "lines="+mod.Topics.ConversationLines().Count+"; expected="+greetingLines);
                    // A genuine subsequent topic choice must still be accepted.
                    int answered=mod.Topics.AnsweredCount;
                    CallWindow(DaggerfallUI.Instance.TalkWindow,"SetTalkModeTellMeAbout");yield return null;
                    yield return UseTopic("Companion orders");yield return new WaitForSecondsRealtime(.2f);
                    Check("new input still selects requested topic #"+repeat,mod.Topics.AnsweredCount==answered+1 && DaggerfallUI.UIManager.TopWindow is CompanionOrdersWindow,"answered="+mod.Topics.AnsweredCount);
                    DaggerfallUI.Instance.PopToHUD();yield return null;
                }
                topics.AlwaysAcceptKeyboardInput=oldKeyboard;gm.DisableAI=false;
                ghost=GameObjectHelper.CreateEnemy("ANPC night ghost fixture",MobileTypes.Ghost,location.transform.InverseTransformPoint(origin+new Vector3(6,1,0)),MobileGender.Unspecified,location.transform,MobileReactions.Hostile);
                var quest=ghost.AddComponent<QuestResourceBehaviour>();quest.enabled=false;
                var resource=new Foe(new Quest(),"Foe _ghost_ is Ghost");questField.SetValue(quest,resource);
                yield return new WaitForSeconds(.4f);
                var enemy=ghost.GetComponent<DaggerfallEntityBehaviour>();enemy.Entity.DeserializeSpellbook(new DaggerfallWorkshop.Game.MagicAndEffects.EffectBundleSettings[0]);
                var senses=ghost.GetComponent<EnemySenses>();senses.enabled=false;senses.Target=gm.PlayerEntityBehaviour;
                ghost.GetComponent<EnemyMotor>().enabled=false;ghost.GetComponent<EnemyMotor>().IsHostile=true;ghost.GetComponent<EnemyAttack>().enabled=false;
                enemy.Entity.MaxHealth=enemy.Entity.CurrentHealth=1000;
                Check("night ghost fixture has default quest AI protection",!quest.IsAttackableByAI && quest.TargetResource is Foe,"quest-protected="+!quest.IsAttackableByAI);
                yield return new WaitForSeconds(.7f);
                Check("defensive companion engages quest ghost attacking player",actor.Threat==enemy && actor.CurrentMode==NpcMode.Fighting,actor.Describe());
                mod.Companions.Command(actor,"aggressive");yield return new WaitForSeconds(.6f);
                Check("aggressive companion engages quest ghost attacking player",actor.Threat==enemy && actor.CurrentMode==NpcMode.Fighting,actor.Describe());
                float until=Time.time+12;
                while(enemy.Entity.CurrentHealth==1000 && Time.time<until) yield return null;
                Check("companion uses real DFU attack against quest ghost",enemy.Entity.CurrentHealth<1000 && actor.Threat==enemy,actor.Describe()+"; ghost HP="+enemy.Entity.CurrentHealth);
                Check("companion does not rewrite global quest AI protection",!quest.IsAttackableByAI,"flag="+quest.IsAttackableByAI);
                // Restrained foes must remain protected even with a stale hostile flag/target.
                resource.SetRestrained();yield return new WaitForSeconds(.5f);
                Check("restrained quest enemy is released",actor.Threat==null,actor.Describe());
                resource.ClearRestrained();senses.Target=null;yield return new WaitForSeconds(.6f);
                Check("aggressive does not initiate against unrelated protected quest foe",actor.Threat==null,actor.Describe());
                senses.Target=gm.PlayerEntityBehaviour;UnityEngine.Object.Destroy(quest);yield return new WaitForSeconds(.5f);
                mod.Companions.Command(actor,"defensive");yield return new WaitForSeconds(.6f);
                Check("ordinary non-quest ghost remains eligible",actor.Threat==enemy,actor.Describe());
            }
            finally
            {
                keyField.SetValue(DaggerfallUI.Instance,KeyCode.None);if(topics!=null)topics.AlwaysAcceptKeyboardInput=oldKeyboard;
                DaggerfallUI.Instance.PopToHUD();NpcBrain.Discard(actor);NpcBrain.Discard(neutral);UnityEngine.Object.Destroy(ghost);UnityEngine.Object.Destroy(geometry);
                gm.DisableAI=oldAI;gm.PlayerEntity.GoldPieces=gold;
                if(pee.IsPlayerInsideBuilding)pee.TransitionExterior(false);
                player.position=oldPosition;player.rotation=oldRotation;gm.MainCamera.transform.rotation=oldView;gm.PlayerMouseLook.enabled=look;
            }
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using AdvancedNPCs.Core;
namespace AdvancedNPCs
{
    public partial class SelfTest
    {
        public const string CompanionLimitsSuite="limits";
        static bool TooManyPopup()
        {
            var box=DaggerfallUI.UIManager.TopWindow as DaggerfallMessageBox;if(box==null)return false;
            var label=typeof(DaggerfallMessageBox).GetField("label",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(box) as MultiFormatTextLabel;
            return label!=null && label.TextLabels.Exists(delegate(TextLabel line){return line.Text=="Too many followers!";});
        }
        IEnumerator CompanionLimitsSteps(DaggerfallLocation location,Transform player)
        {
            var gm=GameManager.Instance;int oldLimit=mod.Config.MaxFollowers,gold=gm.PlayerEntity.GoldPieces;
            bool oldAI=gm.DisableAI,look=gm.PlayerMouseLook.enabled;Vector3 oldPosition=player.position;Quaternion oldRotation=player.rotation,oldView=gm.MainCamera.transform.rotation;
            var actors=new List<NpcBrain>();var temporary=new List<string>();const string escortKey="selftest_limit_escort";
            var floor=new GameObject("ANPC follower limit verification");floor.transform.SetParent(location.transform,false);
            Vector3 origin=oldPosition+Vector3.up*24;NavigationBox(floor.transform,"limit floor",origin-Vector3.up*.15f,new Vector3(60,.3f,50));
            try
            {
                if(mod.Companions.UsedSlots!=0){Fail("limit fixture requires no existing hired companions","use the dedicated AtDaggerfall fixture");yield break;}
                gm.DisableAI=true;gm.PlayerMouseLook.enabled=false;mod.Config.MaxFollowers=3;gm.PlayerEntity.GoldPieces=10000;
                Check("new follower settings load through native mod settings",mod.Config.FromSettings && !mod.Config.MissingKeys.Contains("MaxFollowers"),"limit before fixture="+oldLimit);
                var def=mod.Catalog.Generics.Find(delegate(NpcDefinition d){return d.Id=="ironclad_rogue_1";});
                for(int i=0;i<4;i++)actors.Add(CombatPerson(location,def,"selftest_limit_"+i,origin+new Vector3(i*2,1,0),false));
                yield return new WaitForSecondsRealtime(.5f);
                for(int i=0;i<3;i++)Check("normal recruit fills slot "+(i+1),mod.Companions.Command(actors[i],"recruit") && mod.Companions.UsedSlots==i+1,actors[i].Describe());
                var candidate=actors[3];int beforeGold=gm.PlayerEntity.GoldPieces;
                Check("full party keeps hire topic available for explicit refusal",mod.Companions.CanCommand(candidate,"recruit"),candidate.Describe());
                bool rejected=!mod.Companions.Command(candidate,"recruit");yield return null;
                Check("full party shows exact popup without charging or hiring",rejected && TooManyPopup() && gm.PlayerEntity.GoldPieces==beforeGold && candidate.State.companion==null,"slots="+mod.Companions.UsedSlots);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(Application.persistentDataPath,"ANPC-limits.png"));yield return new WaitForSecondsRealtime(.2f);DaggerfallUI.Instance.PopToHUD();
                NavigationPlayerAt(player,candidate.transform.position+Vector3.forward,Vector3.back);yield return OpenTalk(candidate);yield return UseTopic("Hire you (100 gold)");yield return null;
                Check("real dialogue refuses full party without success or gold change",TooManyPopup() && candidate.State.companion==null && gm.PlayerEntity.GoldPieces==beforeGold,"gold="+gm.PlayerEntity.GoldPieces);DaggerfallUI.Instance.PopToHUD();
                mod.Companions.Command(actors[0],"wait");actors[0].State.companion.area="selftest:unloaded";NpcBrain.Discard(actors[0]);actors[0]=null;yield return new WaitForSecondsRealtime(.3f);
                Check("waiting companion in unloaded area retains one slot",mod.Companions.UsedSlots==3 && NpcBrain.Find("selftest_limit_0")==null,"slots="+mod.Companions.UsedSlots);
                var escort=TestDefinition(escortKey,Bravery.Brave,location,player,0);escort.RecruitCost=25;escort.NoCompanionSlot=true;mod.Catalog.ById.Add(escortKey,escort);
                var escortActor=CombatPerson(location,escort,escortKey,origin+new Vector3(0,1,4),false);actors.Add(escortActor);yield return new WaitForSecondsRealtime(.3f);
                beforeGold=gm.PlayerEntity.GoldPieces;
                Check("exempt escort joins full party and still pays authored fee",mod.Companions.Command(escortActor,"recruit") && mod.Companions.UsedSlots==3 && gm.PlayerEntity.GoldPieces==beforeGold-25 && escortActor.State.companion.noCompanionSlot,escortActor.Describe());
                mod.Config.MaxFollowers=0;
                Check("unlimited accepts additional ordinary followers",mod.Companions.Command(candidate,"recruit") && mod.Companions.UsedSlots==4,candidate.Describe());
                mod.Config.MaxFollowers=3;
                Check("lowering limit keeps existing contracts and orders",mod.Companions.Command(candidate,"wait") && mod.Companions.Command(candidate,"follow") && mod.Companions.UsedSlots==4,candidate.Describe());
                mod.Companions.Command(actors[1],"dismiss");
                Check("dismissal frees a slot",mod.Companions.UsedSlots==3,"slots="+mod.Companions.UsedSlots);
                beforeGold=gm.PlayerEntity.GoldPieces;Check("rehire respects current cap",!mod.Companions.Command(actors[1],"recruit") && gm.PlayerEntity.GoldPieces==beforeGold,"gold="+beforeGold);DaggerfallUI.Instance.PopToHUD();
                actors[2].EntityBehaviour.Entity.CurrentHealth=0;yield return new WaitForSecondsRealtime(.5f);
                Check("death frees a slot",mod.Companions.UsedSlots==2,"slots="+mod.Companions.UsedSlots);
                Check("freed slot permits one paid rehire",mod.Companions.Command(actors[1],"recruit") && mod.Companions.UsedSlots==3,actors[1].Describe());
                mod.Config.MaxFollowers=10;
                while(mod.Companions.UsedSlots<10)
                {
                    string key="selftest_limit_waiting_"+temporary.Count;temporary.Add(key);ids.Add(key);
                    mod.States.Keep(key,new NpcState{companion=new CompanionRecord{template=def.Id,status="waiting",area="selftest:unloaded"}});
                }
                var probe=CombatPerson(location,def,"selftest_limit_probe",origin+new Vector3(4,1,4),false);actors.Add(probe);yield return new WaitForSecondsRealtime(.3f);
                Check("ten-follower choice enforces exact cap",mod.Companions.UsedSlots==10 && !mod.Companions.Command(probe,"recruit") && probe.State.companion==null,"slots="+mod.Companions.UsedSlots);DaggerfallUI.Instance.PopToHUD();
                var save=new NpcSaveDataInterface(mod.States,mod.Placed,mod.Flags,mod.RaiseStateRestored);string encoded=SaveLoadManager.Serialize(typeof(NpcSaveData),save.GetSaveData());
                save.RestoreSaveData(SaveLoadManager.Deserialize(typeof(NpcSaveData),encoded));yield return new WaitForSecondsRealtime(1);
                Check("save load retains slot usage and escort exemption",mod.Companions.UsedSlots==10 && mod.States.GetOrCreate(escortKey).companion.noCompanionSlot,"slots="+mod.Companions.UsedSlots);
                mod.Catalog.ById.Remove(escortKey);
                Check("missing escort definition retains saved slot exemption",mod.Companions.UsedSlots==10,"slots="+mod.Companions.UsedSlots);mod.Catalog.ById.Add(escortKey,escort);
                var nativeMod=DaggerfallWorkshop.Game.Utility.ModSupport.ModManager.Instance.GetMod("Advanced NPCs");
                var window=new DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings.ModSettingsWindow(DaggerfallUI.UIManager,nativeMod,true);
                DaggerfallUI.UIManager.PushWindow(window);yield return new WaitForSecondsRealtime(.3f);
                var data=typeof(DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings.ModSettingsWindow).GetField("settings",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(window) as DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings.ModSettingsData;
                DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings.MultipleChoiceKey choice;
                bool options=data.TryGetKey("Companions","MaxFollowers",out choice) && choice.Options.Count==9 && choice.Options[0]=="3" && choice.Options[7]=="10" && choice.Options[8]=="Unlimited (Not Recommended)";
                Check("native settings window exposes ordered limits and unlimited label",options,"options="+(choice!=null?string.Join(",",choice.Options.ToArray()):"missing"));
                var controls=typeof(DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings.ModSettingsWindow).GetField("uiControls",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(window) as Dictionary<DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings.Key,BaseScreenComponent>;
                foreach(var control in controls)if(control.Key.Name=="MaxFollowers")((HorizontalSlider)control.Value).Value=8;
                yield return new WaitForSecondsRealtime(.2f);ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(Application.persistentDataPath,"ANPC-follower-settings.png"));yield return new WaitForSecondsRealtime(.2f);DaggerfallUI.Instance.PopToHUD();
                var legacy=SaveLoadManager.Deserialize(typeof(CompanionRecord),"{\"status\":\"following\"}") as CompanionRecord;
                Check("legacy companion records default to ordinary slot",legacy!=null && !legacy.noCompanionSlot,"legacy exemption="+(legacy!=null && legacy.noCompanionSlot));
                Check("follower setting and exemption retain save format five",SaveFormat.Current==5 && !def.NoCompanionSlot,"version="+SaveFormat.Current);
            }
            finally
            {
                DaggerfallUI.Instance.PopToHUD();foreach(var actor in actors)if(actor!=null){mod.States.Remove(actor.Id);NpcBrain.Discard(actor);}
                foreach(string id in ids)if(id.StartsWith("selftest_limit")){mod.States.Remove(id);NpcBrain.Discard(NpcBrain.Find(id));}
                mod.Catalog.ById.Remove(escortKey);UnityEngine.Object.Destroy(floor);mod.Config.MaxFollowers=oldLimit;gm.PlayerEntity.GoldPieces=gold;gm.DisableAI=oldAI;gm.PlayerMouseLook.enabled=look;
                player.position=oldPosition;player.rotation=oldRotation;gm.MainCamera.transform.rotation=oldView;
            }
        }
    }
}

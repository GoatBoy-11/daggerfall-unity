using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using AdvancedNPCs.Core;
namespace AdvancedNPCs
{
    public partial class SelfTest
    {
        public const string CompanionEventsSuite="barkevents";
        IEnumerator CompanionEventsSteps(DaggerfallLocation location,Transform player)
        {
            var gm=GameManager.Instance;bool ai=gm.DisableAI,look=gm.PlayerMouseLook.enabled,mute=mod.Config.DisableFollowerBarks;
            int mode=mod.Config.VoiceMode;Vector3 position=player.position;Quaternion rotation=player.rotation,view=gm.MainCamera.transform.rotation;
            const string folder="selftest_speech_events";
            string root=Path.Combine(Application.temporaryCachePath,folder+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
            var actors=new List<NpcBrain>();var floor=new GameObject("ANPC event speech verification");floor.transform.SetParent(location.transform,false);
            Vector3 origin=position+Vector3.up*24;NavigationBox(floor.transform,"event floor",origin-Vector3.up*.15f,new Vector3(75,.3f,40));
            try
            {
                gm.DisableAI=true;gm.PlayerMouseLook.enabled=false;mod.Config.DisableFollowerBarks=false;mod.Config.VoiceMode=VoiceModes.Anpc;
                NavigationPlayerAt(player,origin+new Vector3(0,1,2),Vector3.back);
                string[] pools={CompanionBarkRules.Recruit,CompanionBarkRules.Wait,CompanionBarkRules.Follow,CompanionBarkRules.Dismiss,CompanionBarkRules.Regroup,CompanionBarkRules.Push,CompanionBarkRules.Combat,CompanionBarkRules.Victory};
                foreach(string pool in pools)File.WriteAllBytes(Path.Combine(root,pool+"_1.wav"),ToneWav(2f));
                File.WriteAllBytes(Path.Combine(root,"push.wav"),ToneWav(2f));
                File.WriteAllBytes(Path.Combine(root,"push_7.wav"),ToneWav(2f));
                File.WriteAllBytes(Path.Combine(root,"cm_bark_1.wav"),ToneWav(2f));File.WriteAllBytes(Path.Combine(root,"step_1.wav"),ToneWav(2f));
                var sounds=SoundLibrary.IndexSet(folder+"/Sounds",root);mod.Sounds.Add(folder,sounds);
                var definition=TestDefinition(folder,Bravery.Brave,location,player,0);definition.RecruitCost=0;definition.BaseClass="Warrior";
                definition.Sounds=new SoundSettings{CombatChance=0,GreetChance=0,BarkMinSeconds=45,BarkMaxSeconds=45};mod.Catalog.ById.Add(folder,definition);
                var actor=CombatPerson(location,definition,"selftest_event_actor",origin+new Vector3(0,1,0),false);actors.Add(actor);
                yield return new WaitForSecondsRealtime(.5f);float deadline=Time.realtimeSinceStartup+20;
                while(!sounds.IsReady && Time.realtimeSinceStartup<deadline)yield return null;
                var voice=actor.GetComponent<NpcVoice>();
                Check("optional event sound pools decode with normal suffixes",sounds.IsReady && sounds.Clips.Count==10,sounds.Summary());
                Check("push and cm_push filenames load into the same random voice pool",sounds.Files[CompanionBarkRules.Push].Count==3 &&
                    sounds.Clips[CompanionBarkRules.Push].Count==3,sounds.Summary());
                Check("unhired NPC cannot play event speech",!voice.Play(CompanionBarkRules.Recruit),actor.Status);
                gm.PauseGame(true);gm.DisableAI=false;
                Check("successful recruit queues an acknowledgement",mod.Companions.Command(actor,"recruit"),actor.Status);
                yield return new WaitForSecondsRealtime(.15f);
                Check("command acknowledgement waits through paused UI",voice.Played(CompanionBarkRules.Recruit)==0,"recruit="+voice.Played(CompanionBarkRules.Recruit));
                gm.PauseGame(false);yield return new WaitForSeconds(.2f);
                Check("recruit acknowledgement plays once after UI closes",voice.Played(CompanionBarkRules.Recruit)==1 && voice.LastSpeechPool==CompanionBarkRules.Recruit,"last="+voice.LastSpeechPool);
                // Use a known passing roll for both paths; probability boundaries are covered by Core tests.
                var pushRandom=UnityEngine.Random.state;int pushSeed=0;
                for(;pushSeed<1000;pushSeed++){UnityEngine.Random.InitState(pushSeed);if(UnityEngine.Random.value<.15f)break;}
                gm.PauseGame(true);UnityEngine.Random.InitState(pushSeed);
                Check("menu Push queues its optional voice",mod.Companions.Command(actor,"push"),actor.Describe());
                yield return new WaitForSecondsRealtime(.1f);
                Check("Push voice waits until menu closes",voice.Played(CompanionBarkRules.Push)==0,"push count");
                gm.PauseGame(false);yield return new WaitForSeconds(.2f);
                Check("manual Push plays cm_push after a passing roll",voice.Played(CompanionBarkRules.Push)==1 && voice.LastSpeechPool==CompanionBarkRules.Push,"push="+voice.Played(CompanionBarkRules.Push));
                actor.Teleport(actor.transform.position);UnityEngine.Random.InitState(pushSeed);
                Check("automatic Push shares the voice event",actor.CompanionCombat.PushAway(true),actor.Describe());
                yield return new WaitForSeconds(.2f);
                Check("automatic Push plays cm_push after a passing roll",voice.Played(CompanionBarkRules.Push)==2,"push="+voice.Played(CompanionBarkRules.Push));
                actor.Teleport(actor.transform.position);
                actor.State.companion.barksMuted=true;voice.BarksChanged();UnityEngine.Random.InitState(pushSeed);
                actor.CompanionCombat.PushAway(true);yield return new WaitForSeconds(.2f);
                Check("muting suppresses Push voices without suppressing movement",voice.Played(CompanionBarkRules.Push)==2 && actor.CompanionCombat.Pushing,"push="+voice.Played(CompanionBarkRules.Push));
                actor.State.companion.barksMuted=false;voice.BarksChanged();actor.Teleport(actor.transform.position);UnityEngine.Random.state=pushRandom;
                Check("failed repeat hire cannot repeat recruit speech",!mod.Companions.Command(actor,"recruit"),actor.Status);
                yield return new WaitForSeconds(.1f);Check("failed command leaves event count unchanged",voice.Played(CompanionBarkRules.Recruit)==1,"recruit="+voice.Played(CompanionBarkRules.Recruit));
                voice.OnSheetFrame(new SpriteAnimation{Frames=2},0);AudioSource step=null;
                foreach(var source in actor.GetComponentsInChildren<AudioSource>())if(source.gameObject.name=="AnpcSteps" && source.isPlaying)step=source;
                CompanionOrdersWindow.Open(actor);yield return new WaitForSecondsRealtime(.2f);
                var menu=DaggerfallUI.UIManager.TopWindow as CompanionOrdersWindow;int index=BarkMenuIndex(menu,"Wait here");
                Check("event speech uses actual native companion menu",index>=0,"index="+index);
                if(index>=0){menu.ListBox.SelectedIndex=index;menu.ListBox.UseSelectedItem();}else DaggerfallUI.Instance.PopToHUD();
                yield return new WaitForSecondsRealtime(.2f);
                Check("wait acknowledgement follows a successful menu order",actor.State.companion.status=="waiting" && voice.Played(CompanionBarkRules.Wait)==1,"wait="+voice.Played(CompanionBarkRules.Wait));
                Check("acknowledgement is one speech source and leaves footsteps alone",voice.SpeechSourcesPlaying==1 && step!=null && step.isPlaying,"speech="+voice.SpeechSourcesPlaying);
                gm.PauseGame(true);mod.Companions.Command(actor,"follow");yield return new WaitForSecondsRealtime(.15f);
                Check("follow speech is deferred while paused",voice.Played(CompanionBarkRules.Follow)==0,"follow="+voice.Played(CompanionBarkRules.Follow));
                gm.PauseGame(false);yield return new WaitForSeconds(.2f);
                Check("resume-follow command acknowledges once",voice.Played(CompanionBarkRules.Follow)==1,"follow="+voice.Played(CompanionBarkRules.Follow));
                int waits=voice.Played(CompanionBarkRules.Wait),follows=voice.Played(CompanionBarkRules.Follow);
                gm.PauseGame(true);mod.Companions.Command(actor,"wait");mod.Companions.Command(actor,"follow");gm.PauseGame(false);yield return new WaitForSeconds(.2f);
                Check("rapid queued commands speak only the latest valid response",voice.Played(CompanionBarkRules.Wait)==waits && voice.Played(CompanionBarkRules.Follow)==follows+1,"wait/follow counts");
                mod.Config.DisableFollowerBarks=true;mod.Companions.Command(actor,"wait");yield return null;mod.Config.DisableFollowerBarks=false;yield return new WaitForSeconds(.15f);
                Check("globally muted command acknowledgements do not queue for later",voice.Played(CompanionBarkRules.Wait)==waits,"wait="+voice.Played(CompanionBarkRules.Wait));
                follows=voice.Played(CompanionBarkRules.Follow);actor.State.companion.barksMuted=true;voice.BarksChanged();mod.Companions.Command(actor,"follow");yield return null;
                bool allBlocked=true;foreach(string pool in pools)allBlocked &= !voice.Play(pool);
                Check("individual mute covers every new event pool",allBlocked,"new pools muted");
                actor.State.companion.barksMuted=false;voice.BarksChanged();yield return new WaitForSeconds(.15f);
                Check("individual unmute does not replay a command",voice.Played(CompanionBarkRules.Follow)==follows,"follow="+follows);
                mod.Companions.Command(actor,"regroup");yield return new WaitForSeconds(.2f);
                Check("regroup command has its own optional acknowledgement",voice.Played(CompanionBarkRules.Regroup)==1,"regroup="+voice.Played(CompanionBarkRules.Regroup));
                actor.CompanionCombat.CancelOrders();mod.Companions.Command(actor,"passive");mod.Companions.Command(actor,"defensive");
                var hostile=TestDefinition(folder+"_foe",Bravery.Brave,location,player,0);hostile.BaseClass="Warrior";hostile.RecruitCost=-1;hostile.CrimeOnAttack=false;hostile.FleeChance=0;
                var foe=CombatPerson(location,hostile,"selftest_event_foe",actor.transform.position+Vector3.right*8,false);actors.Add(foe);yield return new WaitForSeconds(.3f);
                foe.EntityBehaviour.Entity.MaxHealth=100000;foe.EntityBehaviour.Entity.CurrentHealth=100000;foe.SwitchHostile(true);AimCombat(foe,gm.PlayerEntityBehaviour);
                deadline=Time.realtimeSinceStartup+4;while(voice.Played(CompanionBarkRules.Combat)==0 && Time.realtimeSinceStartup<deadline)yield return null;
                Check("entering a real defensive fight triggers a battle line",voice.Played(CompanionBarkRules.Combat)==1 && actor.CurrentMode==NpcMode.Fighting,actor.Status);
                yield return new WaitForSeconds(.6f);
                Check("ongoing combat does not repeat its entry line",voice.Played(CompanionBarkRules.Combat)==1,"combat="+voice.Played(CompanionBarkRules.Combat));
                foe.EntityBehaviour.Entity.CurrentHealth=0;yield return new WaitForSeconds(.6f);
                Check("confirmed dead target triggers one victory line",voice.Played(CompanionBarkRules.Victory)==1 && actor.CurrentMode==NpcMode.Calm,"victory="+voice.Played(CompanionBarkRules.Victory));
                yield return new WaitForSeconds(.25f);Check("remaining calm does not repeat victory",voice.Played(CompanionBarkRules.Victory)==1,"victory="+voice.Played(CompanionBarkRules.Victory));
                var second=CombatPerson(location,hostile,"selftest_event_foe_two",actor.transform.position+Vector3.right*7,false);actors.Add(second);yield return new WaitForSeconds(.3f);
                second.EntityBehaviour.Entity.MaxHealth=100000;second.EntityBehaviour.Entity.CurrentHealth=100000;second.SwitchHostile(true);AimCombat(second,gm.PlayerEntityBehaviour);
                deadline=Time.realtimeSinceStartup+4;while(voice.Played(CompanionBarkRules.Combat)<2 && Time.realtimeSinceStartup<deadline)yield return null;
                Check("a later encounter can speak a fresh battle line",voice.Played(CompanionBarkRules.Combat)==2,"combat="+voice.Played(CompanionBarkRules.Combat));
                second.SwitchHostile(false);yield return new WaitForSeconds(.5f);
                Check("losing a living target does not claim victory",voice.Played(CompanionBarkRules.Victory)==1 && actor.CurrentMode==NpcMode.Calm,"victory="+voice.Played(CompanionBarkRules.Victory));
                second.SwitchHostile(true);AimCombat(second,gm.PlayerEntityBehaviour);yield return new WaitForSeconds(.7f);
                mod.Companions.Command(actor,"regroup");second.EntityBehaviour.Entity.CurrentHealth=0;yield return new WaitForSeconds(.4f);
                Check("regrouping does not turn a lost fight into a victory bark",voice.Played(CompanionBarkRules.Victory)==1,"victory="+voice.Played(CompanionBarkRules.Victory));
                actor.CompanionCombat.CancelOrders();mod.Companions.Command(actor,"passive");yield return new WaitForSeconds(.2f);NavigationPlayerAt(player,actor.transform.position+Vector3.forward,Vector3.back);
                gm.PauseGame(true);bool dismissed=mod.Companions.Command(actor,"dismiss");yield return new WaitForSecondsRealtime(.1f);
                Check("dismissal ends contract immediately and defers its one goodbye",dismissed && !voice.BarksEnabled && voice.Played(CompanionBarkRules.Dismiss)==0,"dismiss="+voice.Played(CompanionBarkRules.Dismiss));
                gm.PauseGame(false);yield return new WaitForSeconds(.2f);
                Check("authorized goodbye survives the end of its contract",voice.Played(CompanionBarkRules.Dismiss)==1 && voice.LastSpeechPool==CompanionBarkRules.Dismiss && voice.SpeechSourcesPlaying==1,"last="+voice.LastSpeechPool);
                Check("dismissed actor cannot trigger other companion speech",!voice.Play(CompanionBarkRules.Bark) && !voice.Play(CompanionBarkRules.Dismiss),actor.Status);
                gm.PauseGame(true);yield return new WaitForSecondsRealtime(.1f);Check("goodbye speech also pauses without being discarded",voice.SpeechSourcesPlaying==0,"sources="+voice.SpeechSourcesPlaying);
                gm.PauseGame(false);yield return new WaitForSeconds(.15f);Check("goodbye speech resumes on the same actor",voice.SpeechSourcesPlaying==1,"sources="+voice.SpeechSourcesPlaying);
                yield return new WaitForSeconds(2.1f);Check("completed goodbye leaves no repeating companion speech",voice.SpeechSourcesPlaying==0 && voice.Played(CompanionBarkRules.Dismiss)==1,"dismiss="+voice.Played(CompanionBarkRules.Dismiss));
                int recruited=voice.Played(CompanionBarkRules.Recruit);
                gm.PauseGame(true);bool rehired=mod.Companions.Command(actor,"recruit");mod.Config.DisableFollowerBarks=true;yield return new WaitForSecondsRealtime(.1f);
                mod.Config.DisableFollowerBarks=false;gm.PauseGame(false);yield return new WaitForSeconds(.15f);
                Check("muting a queued acknowledgement cancels it permanently",rehired && voice.Played(CompanionBarkRules.Recruit)==recruited,"recruit="+voice.Played(CompanionBarkRules.Recruit));
                actor.State.companion.barksMuted=true;voice.BarksChanged();bool mutedDismiss=mod.Companions.Command(actor,"dismiss");yield return new WaitForSeconds(.15f);
                Check("individual mute also suppresses the authorized goodbye",mutedDismiss && voice.Played(CompanionBarkRules.Dismiss)==1,"dismiss="+voice.Played(CompanionBarkRules.Dismiss));
            }
            finally
            {
                gm.PauseGame(false);DaggerfallUI.Instance.PopToHUD();
                foreach(var actor in actors)if(actor!=null){string key=actor.Id;NpcBrain.Discard(actor);mod.States.Remove(key);}
                mod.Sounds.Remove(folder);mod.Catalog.ById.Remove(folder);UnityEngine.Object.Destroy(floor);
                gm.DisableAI=ai;gm.PlayerMouseLook.enabled=look;mod.Config.DisableFollowerBarks=mute;mod.Config.VoiceMode=mode;
                player.position=position;player.rotation=rotation;gm.MainCamera.transform.rotation=view;
                if(Directory.Exists(root))Directory.Delete(root,true);
            }
        }
    }
}

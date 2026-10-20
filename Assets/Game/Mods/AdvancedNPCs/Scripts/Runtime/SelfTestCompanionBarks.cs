using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.UserInterface;
using AdvancedNPCs.Core;
namespace AdvancedNPCs
{
    public partial class SelfTest
    {
        public const string CompanionBarksSuite="barks";
        static int BarkMenuIndex(CompanionOrdersWindow menu,string prefix)
        {
            if(menu==null)return -1;
            for(int i=0;i<menu.ListBox.Count;i++)if(menu.ListBox.GetItem(i).textLabel.Text.StartsWith(prefix,StringComparison.Ordinal))return i;
            return -1;
        }
        IEnumerator CompanionBarksSteps(DaggerfallLocation location,Transform player)
        {
            var gm=GameManager.Instance;bool oldAI=gm.DisableAI,oldMute=mod.Config.DisableFollowerBarks,look=gm.PlayerMouseLook.enabled;
            int mode=mod.Config.VoiceMode;bool footsteps=mod.Config.Footsteps;
            Vector3 position=player.position;Quaternion rotation=player.rotation,view=gm.MainCamera.transform.rotation;
            string root=Path.Combine(Application.temporaryCachePath,"anpc_companion_barks_"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);var actors=new List<NpcBrain>();
            var floor=new GameObject("ANPC companion speech verification");floor.transform.SetParent(location.transform,false);
            Vector3 origin=position+Vector3.up*24;NavigationBox(floor.transform,"speech floor",origin-Vector3.up*.15f,new Vector3(40,.3f,30));
            const string folder="selftest_companion_voice";
            try
            {
                gm.DisableAI=true;gm.PlayerMouseLook.enabled=false;mod.Config.VoiceMode=VoiceModes.Anpc;mod.Config.DisableFollowerBarks=false;mod.Config.Footsteps=true;
                NavigationPlayerAt(player,origin+new Vector3(0,1,2),Vector3.back);
                foreach(string pool in new[]{CompanionBarkRules.Bark,CompanionBarkRules.Inn,CompanionBarkRules.Dungeon,CompanionBarkRules.Hurt,CompanionBarkRules.NoMagicka,SoundPools.Greet,SoundPools.Talk,SoundPools.Step,SoundPools.Attack,SoundPools.Heal})
                    File.WriteAllBytes(Path.Combine(root,pool+"_2.wav"),ToneWav(3f));
                var set=SoundLibrary.IndexSet(folder+"/Sounds",root);mod.Sounds.Add(folder,set);
                var def=TestDefinition(folder,Bravery.Brave,location,player,0);def.RecruitCost=0;
                def.Sounds=new SoundSettings{CombatChance=100,GreetChance=100,BarkMinSeconds=1,BarkMaxSeconds=1};mod.Catalog.ById.Add(folder,def);
                var actor=CombatPerson(location,def,"selftest_speech_a",origin+new Vector3(0,1,0),false);actors.Add(actor);
                var other=CombatPerson(location,def,"selftest_speech_b",origin+new Vector3(3,1,0),true);actors.Add(other);
                yield return new WaitForSecondsRealtime(.7f);
                var voice=actor.GetComponent<NpcVoice>();var otherVoice=other.GetComponent<NpcVoice>();
                float soundDeadline=Time.realtimeSinceStartup+15f;
                while(!set.IsReady && Time.realtimeSinceStartup<soundDeadline)yield return null;
                Check("companion pools are indexed and lazily decoded",set.IsReady && set.Clips.ContainsKey(CompanionBarkRules.NoMagicka) && set.Files.ContainsKey(CompanionBarkRules.Dungeon),set.Summary());
                Check("ordinary uncontracted ANPC never plays companion-only speech",!voice.Play(CompanionBarkRules.Bark) && !voice.BarksEnabled,actor.Status);
                Check("recruitment enables optional companion sounds",mod.Companions.Command(actor,"recruit") && voice.BarksEnabled,actor.Status);
                mod.Companions.Command(actor,"wait");mod.Companions.Command(other,"wait");
                Check("new bark setting is present and defaults enabled",!mod.Config.MissingKeys.Contains("DisableFollowerBarks"),"settings keys");
                def.Sounds.CombatChance=def.Sounds.GreetChance=0;
                Check("barks use their own schedule rather than combat or greet chance",voice.Play(CompanionBarkRules.Bark),"bark count="+voice.Played(CompanionBarkRules.Bark));
                var first=voice.LastVoice;voice.Play(CompanionBarkRules.Inn);
                Check("new companion speech interrupts its preceding clip",(!first.isPlaying || voice.FadingOut(first)) && voice.SpeechSourcesPlaying==1 && voice.LastSpeechPool==CompanionBarkRules.Inn,"sources="+voice.SpeechSourcesPlaying);
                otherVoice.Play(CompanionBarkRules.Bark);
                Check("different companions may speak at the same time",voice.SpeechSourcesPlaying==1 && otherVoice.SpeechSourcesPlaying==1,"own="+voice.SpeechSourcesPlaying+", other="+otherVoice.SpeechSourcesPlaying);
                voice.OnSheetFrame(new SpriteAnimation{Frames=2},0);
                AudioSource step=null;foreach(var source in actor.GetComponentsInChildren<AudioSource>())if(source.gameObject.name=="AnpcSteps" && source.isPlaying)step=source;
                def.Sounds.CombatChance=def.Sounds.GreetChance=100;
                voice.Play(SoundPools.Attack);
                Check("combat speech interrupts barks without cutting footsteps",step!=null && step.isPlaying && voice.SpeechSourcesPlaying==1 && voice.LastSpeechPool==SoundPools.Attack,"step="+(step!=null));
                voice.PlayTalk();voice.Play(CompanionBarkRules.Hurt);
                Check("companion bark interrupts talk on the same speech channel",!voice.TalkPlaying && voice.SpeechSourcesPlaying==1 && voice.LastSpeechPool==CompanionBarkRules.Hurt,"talk="+voice.TalkPlaying);
                voice.Play(SoundPools.Greet);voice.Play(SoundPools.Heal);
                Check("healing speech interrupts greeting and releases greeting hold",!voice.PlayingGreet && voice.SpeechSourcesPlaying==1 && voice.LastSpeechPool==SoundPools.Heal,"greet="+voice.PlayingGreet);
                actor.State.companion.barksMuted=true;voice.BarksChanged();voice.Play(SoundPools.Attack);
                Check("individual mute preserves ordinary combat speech",!voice.Play(CompanionBarkRules.Hurt) && voice.LastSpeechPool==SoundPools.Attack,"last="+voice.LastSpeechPool);
                actor.State.companion.barksMuted=false;voice.BarksChanged();
                var entity=actor.EntityBehaviour.Entity;entity.CurrentMagicka=1;gm.DisableAI=false;yield return null;yield return null;
                int empty=voice.Played(CompanionBarkRules.NoMagicka);entity.CurrentMagicka=0;yield return null;yield return null;
                Check("running out of magicka triggers exactly one situational line",voice.Played(CompanionBarkRules.NoMagicka)==empty+1,"before="+empty+", after="+voice.Played(CompanionBarkRules.NoMagicka));
                yield return new WaitForSeconds(.3f);
                Check("remaining at zero never repeats no-magicka event",voice.Played(CompanionBarkRules.NoMagicka)==empty+1,"empty lines="+voice.Played(CompanionBarkRules.NoMagicka));
                entity.CurrentMagicka=1;yield return null;entity.CurrentMagicka=0;yield return null;yield return null;
                Check("refilling then spending the last magicka re-arms the event",voice.Played(CompanionBarkRules.NoMagicka)==empty+2,"empty lines="+voice.Played(CompanionBarkRules.NoMagicka));
                entity.CurrentHealth=Math.Max(1,entity.MaxHealth/5);int hurt=voice.Played(CompanionBarkRules.Hurt);int barks=voice.Played(CompanionBarkRules.Bark);
                yield return new WaitForSeconds(1.15f);
                Check("low health replaces the scheduled ordinary bark",voice.Played(CompanionBarkRules.Hurt)>hurt && voice.Played(CompanionBarkRules.Bark)==barks,"hurt="+voice.Played(CompanionBarkRules.Hurt));
                entity.CurrentHealth=entity.MaxHealth;
                hurt=voice.Played(CompanionBarkRules.Hurt);barks=voice.Played(CompanionBarkRules.Bark);gm.PauseGame(true);yield return new WaitForSecondsRealtime(1.3f);
                Check("pause freezes bark timer and speech playback",voice.Played(CompanionBarkRules.Hurt)==hurt && voice.Played(CompanionBarkRules.Bark)==barks && voice.SpeechSourcesPlaying==0,"sources="+voice.SpeechSourcesPlaying);
                gm.PauseGame(false);yield return null;
                Check("resume continues the interrupted speech source",voice.SpeechSourcesPlaying==1,"sources="+voice.SpeechSourcesPlaying);
                mod.Config.DisableFollowerBarks=true;yield return null;
                Check("global disable immediately stops active bark speech",voice.SpeechSourcesPlaying==0 && !voice.BarksEnabled && !voice.Play(CompanionBarkRules.NoMagicka),"sources="+voice.SpeechSourcesPlaying);
                entity.CurrentMagicka=1;yield return null;entity.CurrentMagicka=0;yield return null;empty=voice.Played(CompanionBarkRules.NoMagicka);
                mod.Config.DisableFollowerBarks=false;yield return null;
                Check("unmuting does not replay a depleted-while-muted event",voice.Played(CompanionBarkRules.NoMagicka)==empty,"empty lines="+empty);
                barks=voice.Played(CompanionBarkRules.Bark);yield return new WaitForSeconds(.3f);
                Check("unmuting starts a fresh interval without an immediate bark",voice.Played(CompanionBarkRules.Bark)==barks,"barks="+barks);
                yield return new WaitForSeconds(.85f);
                Check("healthy outdoor companion uses periodic standard bark",voice.Played(CompanionBarkRules.Bark)>barks && NpcVoice.BarkContext()==null,"barks="+voice.Played(CompanionBarkRules.Bark));
                gm.DisableAI=true;yield return null;
                NavigationPlayerAt(player,actor.transform.position+Vector3.forward,Vector3.back);
                CompanionOrdersWindow.Open(actor);yield return new WaitForSecondsRealtime(.2f);
                var menu=DaggerfallUI.UIManager.TopWindow as CompanionOrdersWindow;int index=BarkMenuIndex(menu,"Turn barks off");
                Check("native companion menu offers individual bark switch",index>=0,"index="+index);
                if(index>=0){menu.ListBox.SelectedIndex=index;menu.ListBox.UseSelectedItem();}else DaggerfallUI.Instance.PopToHUD();
                Check("native selection mutes the exact companion and stops its bark",actor.State.companion.barksMuted && !other.State.companion.barksMuted && !voice.BarksEnabled,"muted="+actor.State.companion.barksMuted);
                var saved=(NpcSaveData)new NpcSaveDataInterface(mod.States,mod.Placed,mod.Flags,mod.RaiseStateRestored).GetSaveData();
                var copy=(NpcSaveData)SaveLoadManager.Deserialize(typeof(NpcSaveData),SaveLoadManager.Serialize(typeof(NpcSaveData),saved));
                Check("native mod save serialization retains mute per contract",copy.States[actor.Id].companion.barksMuted && !copy.States[other.Id].companion.barksMuted,"mute save");
                CompanionOrdersWindow.Open(actor);yield return new WaitForSecondsRealtime(.2f);menu=DaggerfallUI.UIManager.TopWindow as CompanionOrdersWindow;
                index=BarkMenuIndex(menu,"Turn barks on");if(index>=0){menu.ListBox.SelectedIndex=index;menu.ListBox.UseSelectedItem();}else DaggerfallUI.Instance.PopToHUD();
                Check("native menu can re-enable companion barks",!actor.State.companion.barksMuted && voice.BarksEnabled,"index="+index);
                CompanionOrdersWindow.Open(actor);yield return new WaitForSecondsRealtime(.2f);
                mod.Config.DisableFollowerBarks=true;yield return null;yield return null;
                Check("live global-setting change closes a stale individual bark menu",!(DaggerfallUI.UIManager.TopWindow is CompanionOrdersWindow),"stale menu");
                CompanionOrdersWindow.Open(actor);yield return new WaitForSecondsRealtime(.2f);menu=DaggerfallUI.UIManager.TopWindow as CompanionOrdersWindow;
                Check("global disable hides the individual bark option",menu!=null && BarkMenuIndex(menu,"Turn barks")==-1,"menu="+(menu!=null));DaggerfallUI.Instance.PopToHUD();
                mod.Config.DisableFollowerBarks=false;voice.Play(CompanionBarkRules.Bark);gm.PauseGame(false);
                NpcVoice.ForceVanillaVoice=true;
                var native=typeof(NpcVoice).GetMethod("VanillaGrunt",BindingFlags.Instance|BindingFlags.NonPublic);
                native.Invoke(voice,new object[]{true,false,100});
                Check("native fallback speech shares companion interruption channel",voice.LastSpeechPool=="vanilla_attack" && voice.SpeechSourcesPlaying==1,"last="+voice.LastSpeechPool);
                NpcVoice.ForceVanillaVoice=false;
                mod.Config.VoiceMode=VoiceModes.Off;
                Check("voice-off mode also silences companion barks",!voice.Play(CompanionBarkRules.Bark),"voice mode off");
                mod.Config.VoiceMode=VoiceModes.Vanilla;
                Check("vanilla-only voice mode does not play custom barks",!voice.Play(CompanionBarkRules.Bark),"voice mode vanilla");
                mod.Config.VoiceMode=VoiceModes.Anpc;
                var clips=set.Clips[CompanionBarkRules.Hurt];set.Clips.Remove(CompanionBarkRules.Hurt);
                Check("missing optional pool stays silent without fallback",!voice.Play(CompanionBarkRules.Hurt),"missing hurt");set.Clips[CompanionBarkRules.Hurt]=clips;
                voice.Play(CompanionBarkRules.Bark);actor.gameObject.SetActive(false);
                Check("area teardown stops speech without leaving a detached bark",voice.SpeechSourcesPlaying==0,"sources="+voice.SpeechSourcesPlaying);
                string bodyKey=actor.Id;NpcBrain.Discard(actor);actors[0]=null;
                float restoreDeadline=Time.realtimeSinceStartup+5f;
                do {yield return null;actor=NpcBrain.Find(bodyKey);}while(actor==null && Time.realtimeSinceStartup<restoreDeadline);
                if(actor==null){Fail("speech body restoration","missing companion");yield break;}
                actors[0]=actor;yield return new WaitForSecondsRealtime(.3f);voice=actor.GetComponent<NpcVoice>();
                // A new greeting is valid; only old companion barks/depletion must be absent.
                Check("rebuilt companion starts a fresh bark clock without replaying depletion",!CompanionBarkRules.IsBark(voice.LastSpeechPool) && voice.Played(CompanionBarkRules.Bark)==0 && voice.Played(CompanionBarkRules.NoMagicka)==0,"last="+voice.LastSpeechPool+", barks="+voice.Played(CompanionBarkRules.Bark)+", depletion="+voice.Played(CompanionBarkRules.NoMagicka));
                bool dismissed=mod.Companions.Command(actor,"dismiss");
                Check("dismissed ANPC cannot use companion sound pools",dismissed && !voice.Play(CompanionBarkRules.Bark) && !voice.BarksEnabled,actor.Status);
            }
            finally
            {
                gm.PauseGame(false);DaggerfallUI.Instance.PopToHUD();NpcVoice.ForceVanillaVoice=false;
                foreach(var actor in actors)if(actor!=null){string key=actor.Id;NpcBrain.Discard(actor);mod.States.Remove(key);}
                mod.Sounds.Remove(folder);mod.Catalog.ById.Remove(folder);UnityEngine.Object.Destroy(floor);
                gm.DisableAI=oldAI;gm.PlayerMouseLook.enabled=look;mod.Config.VoiceMode=mode;mod.Config.DisableFollowerBarks=oldMute;mod.Config.Footsteps=footsteps;
                player.position=position;player.rotation=rotation;gm.MainCamera.transform.rotation=view;
                if(Directory.Exists(root))Directory.Delete(root,true);
            }
        }
    }
}

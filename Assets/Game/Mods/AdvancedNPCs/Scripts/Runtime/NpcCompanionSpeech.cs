using UnityEngine;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using AdvancedNPCs.Core;
namespace AdvancedNPCs
{
    public partial class NpcVoice
    {
        string pendingCommandPool,pendingCommandStatus;
        CompanionRecord pendingCommandContract,dismissalContract;
        AudioSource dismissalSource;
        readonly CompanionBattleSpeech battleSpeech=new CompanionBattleSpeech();
        DaggerfallEntity battleFoe;
        bool SpeechControls
        {
            get {return brain!=null && brain.State!=null && brain.State.companion!=null && brain.isActiveAndEnabled &&
                !brain.State.dead && brain.CurrentMode!=NpcMode.Dead && !brain.IsEnemy && !brain.State.hostile &&
                !brain.State.companion.barksMuted && AdvancedNpcsMod.Instance!=null &&
                !AdvancedNpcsMod.Instance.Config.DisableFollowerBarks && CustomVoiceOn;}
        }
        bool CompanionSpeech {get {return CompanionSpeaker || dismissalSource!=null;}}
        bool FinishingDismissal
        {
            get {return dismissalSource!=null && SpeechControls && brain.State.companion==dismissalContract &&
                dismissalContract.status=="dismissed" && lastSpeechPool==CompanionBarkRules.Dismiss &&
                (dismissalSource.isPlaying || speechPaused);}
        }
        void ClearPendingCommand()
        {pendingCommandPool=null;pendingCommandStatus=null;pendingCommandContract=null;}
        void ResetCompanionSpeech()
        {ClearPendingCommand();battleSpeech.Reset();battleFoe=null;}
        bool CurrentSpeechArea(CompanionRecord contract)
        {
            string area;Transform parent;
            return NpcBrain.Find(brain.Id)==brain &&
                (transform.position-GameManager.Instance.PlayerObject.transform.position).sqrMagnitude<=SoundRules.MaxAudibleDistance*SoundRules.MaxAudibleDistance &&
                CompanionRoster.CurrentArea(out area,out parent) && area==contract.area;
        }
        // Called only after the roster has successfully executed the exact command.
        internal void OnCompanionCommand(string command)
        {
            string pool=CompanionBarkRules.Command(command);
            if(pool==null)return;
            ClearPendingCommand();
            if(!SpeechControls || (!CompanionSpeaker && command!="dismiss") || Playable(pool)==null)return;
            pendingCommandPool=pool;pendingCommandContract=brain.State.companion;
            pendingCommandStatus=pendingCommandContract.status;
        }
        internal void OnCompanionPush(bool automatic)
        {
            if(CompanionBarkRules.PushVoice(automatic,Random.value)) OnCompanionCommand("push");
        }
        void ValidatePendingCommand()
        {
            if(pendingCommandPool!=null && (!SpeechControls || brain.State.companion!=pendingCommandContract ||
                pendingCommandContract.status!=pendingCommandStatus))ClearPendingCommand();
        }
        void DeliverCompanionCommand()
        {
            if(pendingCommandPool==null || GameManager.IsGamePaused || GameManager.Instance.DisableAI || DaggerfallUI.UIManager.TopWindow!=DaggerfallUI.Instance.DaggerfallHUD)return;
            string pool=pendingCommandPool;CompanionRecord contract=pendingCommandContract;
            ClearPendingCommand();
            if(!SpeechControls || brain.State.companion!=contract || !CurrentSpeechArea(contract))return;
            bool played;
            if(pool==CompanionBarkRules.Dismiss && contract.status=="dismissed")
            {
                // One authorized goodbye may finish after the contract ends. Ordinary dismissed-NPC bark calls stay blocked.
                AudioClip clip=set!=null?set.Pick(pool):null;
                if(clip==null || voices==null)return;
                StopSpeech();
                dismissalSource=Speech(clip,pool,SoundRules.Pitch(settings.SoundPitch,settings.SoundPitchVariation,Random.value));
                dismissalContract=contract;Count(pool);played=true;
            }
            else played=Play(pool);
            if(played)barkClock.Schedule(settings.BarkMinSeconds,settings.BarkMaxSeconds,Random.value);
        }
        void ObserveCompanionBattle()
        {
            if(!CompanionSpeaker || !brain.isActiveAndEnabled || brain.IsEnemy || brain.State.hostile)
            {battleSpeech.Reset();battleFoe=null;return;}
            bool fighting=brain.CurrentMode==NpcMode.Fighting,calm=brain.CurrentMode==NpcMode.Calm;
            bool regrouping=brain.CompanionCombat!=null && brain.CompanionCombat.Regrouping;
            if(fighting && brain.Threat!=null && brain.Threat.Entity!=null)battleFoe=brain.Threat.Entity;
            string pool=battleSpeech.Observe(fighting,calm,battleFoe!=null && battleFoe.CurrentHealth<=0,regrouping);
            if(calm || regrouping)battleFoe=null;
            // Observe while muted too, so restoring audio never replays an old combat transition.
            if(pool!=null && BarksEnabled && !GameManager.Instance.DisableAI && CurrentSpeechArea(brain.State.companion) && Play(pool))
                barkClock.Schedule(settings.BarkMinSeconds,settings.BarkMaxSeconds,Random.value);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.MagicAndEffects;
using DaggerfallWorkshop.Game.Utility;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>
    /// An ANPC's own sounds (spec sounds): plays its Sounds folder's pools on greet, attack, cast, hit and death,
    /// footsteps while walking or running, and DFU's voice grunts itself for events the folder has no sound for.
    /// </summary>
    public partial class NpcVoice : MonoBehaviour
    {
        LoadedSoundSet set;
        SoundSettings settings;
        NpcBrain brain;
        NpcSprite sprite;
        NpcGait gait;
        MobileUnit mobile;
        EnemySounds enemySounds;
        DaggerfallEntityBehaviour behaviour;
        EntityEffectManager effects;
        AudioSource[] voices;
        AudioSource[] steps;
        int nextVoice;
        int nextStep;
        AudioSource lastVoice;
        int magicFrame = -100;
        Races voiceRace = Races.None;
        bool acquired;
        bool started;
        bool gone;
        bool haltPending;
        LoadedSoundSet defaultsOffered;     // the library's shared _Sounds (steps, eat, drink)
        LoadedSoundSet heldDefaults;        // ... held while this ANPC uses any of them
        AudioSource mealSource;
        bool defaultsAcquired;
        int lastHealth = -1;
        MobileStates lastState;
        float stepTimer;
        readonly Dictionary<string, int> played = new Dictionary<string, int>();
        int lastTalk = -1;
        AudioSource talkSource;
        AudioSource greetSource;
        readonly CompanionBarkClock barkClock=new CompanionBarkClock();
        string lastSpeechPool;
        bool speechPaused;
        bool companionClip;     // the last line started as companion speech (paused with the UI)
        bool CompanionSpeaker { get { return brain!=null && brain.State!=null && brain.State.companion!=null && CompanionCombatRules.Contract(brain.State.companion.status); } }
        public bool BarksEnabled { get { return CompanionSpeaker && SpeechControls; } }
        public string LastSpeechPool { get { return lastSpeechPool; } }
        /// <summary>Voice sources speaking now; one fading out after being stopped (50 ms) no longer counts.</summary>
        public int SpeechSourcesPlaying { get { int count=0;if(voices!=null)foreach(var source in voices)if(source!=null && source.isPlaying && !FadingOut(source))count++;return count; } }
        /// <summary>The source was stopped and is fading out.</summary>
        public bool FadingOut(AudioSource source) { return source!=null && stopping.Contains(source); }
        public void BarksChanged()
        {
            if(CompanionBarkRules.IsBark(lastSpeechPool))StopSpeech();
            barkClock.Reset(behaviour!=null && behaviour.Entity!=null?behaviour.Entity.CurrentMagicka:0);
        }
        void StopSpeech()
        {
            if(voices!=null)foreach(var source in voices)FadeOut(source,false);
            talkSource=greetSource=null;lastSpeechPool=null;speechPaused=false;dismissalSource=null;dismissalContract=null;
        }
        AudioSource Speech(AudioClip clip,string pool,float pitch)
        {
            if(CompanionSpeech)StopSpeech();
            AudioSource source=FreeVoice();
            source.pitch=pitch;source.PlayOneShot(clip,DaggerfallUnity.Settings.SoundVolume);
            lastVoice=source;lastSpeechPool=pool;companionClip=CompanionSpeech;return source;
        }
        const float CutFadeSeconds = 0.05f;
        readonly Dictionary<AudioSource, Coroutine> fading = new Dictionary<AudioSource, Coroutine>();
        readonly HashSet<AudioSource> stopping = new HashSet<AudioSource>();   // fading out (to stop or pause)

        /// <summary>
        /// Stops or pauses a sound after a 50 ms fade (real time, so also while the game is paused): cutting a sound
        /// mid-wave clicks loudly. An inactive ANPC (being switched off) cuts at once; Unity silences it anyway.
        /// </summary>
        void FadeOut(AudioSource s, bool pause)
        {
            if (s == null || stopping.Contains(s))
                return;
            Coroutine fadeIn;
            if (fading.TryGetValue(s, out fadeIn))
            {
                StopCoroutine(fadeIn);      // stopped while fading back in after a pause: fade out from where it is
                fading.Remove(s);
            }
            if (!s.isPlaying || !isActiveAndEnabled)
            {
                if (pause) s.Pause(); else s.Stop();
                s.volume = 1f;
                return;
            }
            stopping.Add(s);
            fading[s] = StartCoroutine(Fade(s, pause));
        }

        System.Collections.IEnumerator Fade(AudioSource s, bool pause)
        {
            float start = s.volume;
            for (float t = 0f; t < CutFadeSeconds && s != null; t += Time.unscaledDeltaTime)
            {
                s.volume = start * (1f - t / CutFadeSeconds);
                yield return null;
            }
            if (s == null)
                yield break;
            if (pause) s.Pause(); else s.Stop();
            s.volume = 1f;
            fading.Remove(s);
            stopping.Remove(s);
        }

        /// <summary>Unpauses a sound with a short fade in (a pause fade still running just stops: it never went quiet).</summary>
        void Resume(AudioSource s)
        {
            if (s == null)
                return;
            Coroutine c;
            if (fading.TryGetValue(s, out c))
            {
                StopCoroutine(c);
                fading.Remove(s);
                stopping.Remove(s);
                s.volume = 1f;
                return;
            }
            s.volume = 0f;
            s.UnPause();
            fading[s] = StartCoroutine(FadeIn(s));
        }

        System.Collections.IEnumerator FadeIn(AudioSource s)
        {
            for (float t = 0f; t < CutFadeSeconds && s != null; t += Time.unscaledDeltaTime)
            {
                s.volume = t / CutFadeSeconds;
                yield return null;
            }
            if (s == null)
                yield break;
            s.volume = 1f;
            fading.Remove(s);
        }

        /// <summary>The next voice source not playing and not fading out (else the next in turn, cut at once).</summary>
        AudioSource FreeVoice()
        {
            for (int i = 0; i < voices.Length; i++)
            {
                AudioSource candidate = voices[nextVoice];
                nextVoice = SoundRules.NextVoice(nextVoice, voices.Length);
                if (!candidate.isPlaying && !fading.ContainsKey(candidate))
                {
                    candidate.volume = 1f;
                    return candidate;
                }
            }
            AudioSource source = voices[nextVoice];
            nextVoice = SoundRules.NextVoice(nextVoice, voices.Length);
            Coroutine running;
            if (fading.TryGetValue(source, out running))
            {
                StopCoroutine(running);
                fading.Remove(source);
                stopping.Remove(source);
            }
            source.Stop();
            source.volume = 1f;
            return source;
        }

        void OnDisable()
        {
            CancelHalt(); if(CompanionSpeaker || CompanionBarkRules.IsBark(lastSpeechPool))StopSpeech();barkClock.Reset(-1);ResetCompanionSpeech();
            fading.Clear();     // coroutines end with the object: no fade is left half way
            stopping.Clear();
            if(voices!=null)foreach(var source in voices)if(source!=null)source.volume=1f;
        }

        public static string BarkContext()
        {
            var pee=GameManager.Instance.PlayerEnterExit;
            if(pee.IsPlayerInsideDungeon)return CompanionBarkRules.Dungeon;
            if(pee.IsPlayerInsideBuilding && pee.BuildingType.ToString()=="Tavern")return CompanionBarkRules.Inn;
            return null;
        }
        void CompanionBarks()
        {
            int mana=behaviour.Entity.CurrentMagicka;
            bool depleted=barkClock.ObserveMagicka(mana);
            bool enabled=BarksEnabled && !brain.IsEnemy && !brain.State.hostile && !gone && !GameManager.Instance.DisableAI;
            if(!enabled)
            {
                if(CompanionBarkRules.IsBark(lastSpeechPool) && !FinishingDismissal)StopSpeech();
                barkClock.Reset(mana);return;
            }
            if((transform.position-GameManager.Instance.PlayerObject.transform.position).sqrMagnitude>SoundRules.MaxAudibleDistance*SoundRules.MaxAudibleDistance)
            {barkClock.Reset(mana);return;}
            // Only sample randomness and construct an area key when an utterance is due.
            if(!depleted && barkClock.NeedsSchedule)
            {barkClock.Schedule(settings.BarkMinSeconds,settings.BarkMaxSeconds,Random.value);return;}
            if(!depleted && !barkClock.Advance(Time.deltaTime))return;
            barkClock.Schedule(settings.BarkMinSeconds,settings.BarkMaxSeconds,Random.value);
            string area;Transform parent;
            if(!CompanionRoster.CurrentArea(out area,out parent) || brain.State.companion.area!=area)return;
            if(depleted) {Play(CompanionBarkRules.NoMagicka);return;}
            bool hurt=behaviour.Entity.MaxHealth>0 && (float)behaviour.Entity.CurrentHealth/behaviour.Entity.MaxHealth<.25f;
            // Ordinary chatter waits for calm; injured companions may call for help during a fight.
            if(!hurt && brain.CurrentMode!=NpcMode.Calm)return;
            Play(CompanionBarkRules.Pool(hurt,BarkContext(),Random.value));
        }

        /// <summary>The ANPC's sound set (anpc_info).</summary>
        public LoadedSoundSet Set
        {
            get { return set; }
        }

        /// <summary>Volume scale the last footstep played at, DFU's sound volume included (self-test).</summary>
        public float LastStepVolume { get; private set; }

        /// <summary>How often a pool's event played a sound (self-test); steps count as "step".</summary>
        public int Played(string pool)
        {
            int n;
            return played.TryGetValue(pool, out n) ? n : 0;
        }

        /// <summary>The race DFU picked for this ANPC's grunts; the enemy's own lookup gets Races.None (silence).</summary>
        public Races VoiceRace
        {
            get { return voiceRace; }
        }

        /// <summary>Self-test: DFU's grunt replays every time (no Combat Voices setting, no chance roll).</summary>
        public static bool ForceVanillaVoice;

        /// <summary>The audio source of the last voice sound (self-test).</summary>
        public AudioSource LastVoice
        {
            get { return lastVoice; }
        }

        static AudioClip silence;
        static System.Reflection.FieldInfo attackClipField;
        static bool attackClipFieldLooked;
        bool creatureAttackSilenced;

        /// <summary>A creature's own attack growl is replaced by the folder's attack sounds (self-test).</summary>
        public bool CreatureAttackSilenced
        {
            get { return creatureAttackSilenced; }
        }

        /// <summary>The "ANPC sounds" setting (VoiceModes), read live.</summary>
        static int Mode
        {
            get
            {
                AdvancedNpcsMod mod = AdvancedNpcsMod.Instance;
                return mod != null && mod.Config != null ? mod.Config.VoiceMode : VoiceModes.Anpc;
            }
        }

        static bool FootstepsOn
        {
            get
            {
                AdvancedNpcsMod mod = AdvancedNpcsMod.Instance;
                return mod == null || mod.Config == null || mod.Config.Footsteps;
            }
        }

        static int StepsVolumePercent
        {
            get
            {
                AdvancedNpcsMod mod = AdvancedNpcsMod.Instance;
                return mod != null && mod.Config != null ? mod.Config.StepsVolume : 100;
            }
        }

        /// <summary>Where its footsteps come from: "own", "default" or "none" (anpc_info, self-test).</summary>
        public string StepsSource
        {
            get
            {
                if (OwnSteps)
                    return set.IsReady && !set.Clips.ContainsKey(SoundPools.Step) ? "own (none could be loaded)" : "own";
                return StepDefaults != null ? "default" : "none";
            }
        }

        bool OwnSteps
        {
            get { return set != null && set.Files.ContainsKey(SoundPools.Step); }
        }

        /// <summary>
        /// Every ANPC has one; sounds is null for an ANPC without a Sounds folder. defaultSteps: the shared _Sounds steps
        /// (null: none), used when it has no step files of its own (spec default steps).
        /// </summary>
        public void Init(LoadedSoundSet sounds, SoundSettings soundSettings, LoadedSoundSet defaultSteps)
        {
            set = sounds;
            settings = soundSettings ?? new SoundSettings();
            defaultsOffered = defaultSteps;
            ChooseSteps();
            enemySounds = GetComponent<EnemySounds>();
            if (enemySounds != null && enemySounds.RaceForSounds != Races.None)
            {
                // DFU picked a random voice in Awake. Kept here; EnemySounds gets it back only while DFU voices the ANPC
                // itself (SyncVoiceRace), else the mod plays it where the folder has no sound (spec sounds §E).
                voiceRace = enemySounds.RaceForSounds;
                enemySounds.RaceForSounds = Races.None;
                SyncVoiceRace();
            }
            voices = MakeSources("AnpcVoice", SoundRules.VoiceSources);
            steps = MakeSources("AnpcSteps", SoundRules.StepSources);
        }

        /// <summary>
        /// Creatures (orcs, ...) growl on half their attacks through EnemySounds' cached attack clip, which DFU fills
        /// only while it is empty: a silent clip there gives the attack to the folder's own sounds (spec sounds §E) once
        /// they have loaded. Emptied again, DFU's growl comes back (no attack sound could be loaded, or another set).
        /// </summary>
        void SyncCreatureAttack()
        {
            bool silenceIt = VoiceModes.SilenceCreatureAttack(Mode, Playable(SoundPools.Attack) != null);
            if (enemySounds == null || silenceIt == creatureAttackSilenced)
                return;
            if (!attackClipFieldLooked)
            {
                attackClipFieldLooked = true;
                attackClipField = typeof(EnemySounds).GetField("attackClip",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (attackClipField == null)
                    AdvancedNpcsMod.Log("EnemySounds.attackClip not found: a creature's own attack growl stays.");
            }
            if (attackClipField == null)
                return;
            if (silenceIt && silence == null)
                silence = AudioClip.Create("AnpcSilence", 1, 1, 22050, false);
            attackClipField.SetValue(enemySounds, silenceIt ? silence : null);
            creatureAttackSilenced = silenceIt;
        }

        /// <summary>Uses another sound set (anpc_reload_assets); null leaves only DFU's own grunts.</summary>
        public void Swap(LoadedSoundSet next)
        {
            if (gone)
                return;     // dead: holds nothing any more
            if (acquired && set != null)
                set.Release();
            acquired = false;
            set = next;
            if (started && set != null)     // Start already ran (also for an ANPC that had no Sounds): hold the new set now
            {
                set.Acquire();
                acquired = true;
            }
            ChooseSteps();
        }

        /// <summary>Uses other shared steps (anpc_reload_assets re-read _Sounds); null: none.</summary>
        public void SwapDefaultSteps(LoadedSoundSet next)
        {
            defaultsOffered = next;
            ChooseSteps();
        }

        /// <summary>The shared steps it plays: none of its own and npc.json allows them.</summary>
        LoadedSoundSet StepDefaults
        {
            get { return heldDefaults != null && heldDefaults.Files.ContainsKey(SoundPools.Step) && SoundRules.UseDefaultSteps(OwnSteps, settings.DefaultSteps) ? heldDefaults : null; }
        }

        /// <summary>A shared eat/drink pool it has no files of its own for.</summary>
        bool UsesMealDefaults(LoadedSoundSet defaults)
        {
            foreach (string pool in new[] { SoundPools.Eat, SoundPools.Drink })
                if (defaults.Files.ContainsKey(pool) && (set == null || !set.Files.ContainsKey(pool))) return true;
            return false;
        }

        /// <summary>The shared _Sounds when it uses their steps or eat/drink sounds; held while it is in the world.</summary>
        void ChooseSteps()
        {
            LoadedSoundSet want = defaultsOffered != null && ((defaultsOffered.Files.ContainsKey(SoundPools.Step) &&
                SoundRules.UseDefaultSteps(OwnSteps, settings.DefaultSteps)) || UsesMealDefaults(defaultsOffered)) ? defaultsOffered : null;
            if (want == heldDefaults)
                return;
            if (defaultsAcquired)
                heldDefaults.Release();
            defaultsAcquired = false;
            heldDefaults = want;
            if (started && !gone && heldDefaults != null)
            {
                heldDefaults.Acquire();
                defaultsAcquired = true;
            }
        }

        /// <summary>
        /// Eat package: an eat or drink cycle began. Its own pool, else the shared _Sounds one; no chance roll. Skipped
        /// while the last bite or gulp still plays, so they never overlap. Muted only by the Off voice setting.
        /// </summary>
        public bool PlayMeal(string pool)
        {
            if (gone || voices == null || Mode == VoiceModes.Off || (brain != null && brain.CurrentMode == NpcMode.Dead))
                return false;
            if (mealSource != null && mealSource.isPlaying)
                return false;
            LoadedSoundSet source = set != null && set.Files.ContainsKey(pool) ? set : heldDefaults;
            AudioClip clip = source != null ? source.Pick(pool) : null;
            if (clip == null)
                return false;
            if (mealSource == null)
                mealSource = MakeSources("AnpcMeal", 1)[0];
            mealSource.pitch = SoundRules.Pitch(settings.SoundPitch, settings.SoundPitchVariation, Random.value);
            mealSource.PlayOneShot(clip, DaggerfallUnity.Settings.SoundVolume);
            Count(pool);
            return true;
        }

        /// <summary>Sources that take turns, so overlapping one-shots each keep their own pitch.</summary>
        AudioSource[] MakeSources(string name, int count)
        {
            AudioSource[] sources = new AudioSource[count];
            for (int i = 0; i < count; i++)
            {
                GameObject go = new GameObject(name);
                go.transform.SetParent(transform, false);
                sources[i] = go.AddComponent<AudioSource>();
                Configure(sources[i]);
            }
            return sources;
        }

        static void Configure(AudioSource a)
        {
            a.playOnAwake = false;
            a.spatialBlend = 1f;
            a.rolloffMode = AudioRolloffMode.Linear;
            a.minDistance = 1f;
            a.maxDistance = SoundRules.MaxAudibleDistance;
            a.dopplerLevel = 0f;
        }

        /// <summary>Holds the set only once really in the world (Unity skips Start/OnDestroy on never-active objects).</summary>
        void Start()
        {
            brain = GetComponent<NpcBrain>();
            sprite = GetComponent<NpcSprite>();
            gait = GetComponent<NpcGait>();
            behaviour = GetComponent<DaggerfallEntityBehaviour>();
            DaggerfallEnemy enemy = GetComponent<DaggerfallEnemy>();
            mobile = enemy != null ? enemy.MobileUnit : null;
            effects = GetComponent<EntityEffectManager>();
            if (effects != null)
                effects.OnAssignBundle += OnAssignBundle;
            EntityEffectBroker.OnNewMagicRound += OnNewMagicRound;
            started = true;
            if (set != null && !acquired)
            {
                set.Acquire();
                acquired = true;
            }
            if (heldDefaults != null && !defaultsAcquired)
            {
                heldDefaults.Acquire();
                defaultsAcquired = true;
            }
        }

        void OnDestroy()
        {
            if (effects != null)
                effects.OnAssignBundle -= OnAssignBundle;
            EntityEffectBroker.OnNewMagicRound -= OnNewMagicRound;
            ReleaseAll();
        }

        void ReleaseAll()
        {
            if (acquired && set != null)
                set.Release();
            acquired = false;
            if (defaultsAcquired && heldDefaults != null)
                heldDefaults.Release();
            defaultsAcquired = false;
        }

        // Spells and damage over time hurt in the frame a bundle lands or a magic round runs; DFU voices no grunt for them.
        void OnAssignBundle(LiveEffectBundle bundle)
        {
            // A weapon's strike enchantment or poison lands in the frame of the weapon hit DFU itself voices.
            if (bundle != null && (bundle.castByItem != null || bundle.bundleType == BundleTypes.Poison))
                return;
            magicFrame = Time.frameCount;
        }

        void OnNewMagicRound()
        {
            if (effects != null && effects.EffectCount > 0)
                magicFrame = Time.frameCount;
        }

        /// <summary>
        /// The pool that plays for an event, among those with loaded clips: while the set loads, or when every file of a
        /// pool failed to decode, the event has no pool (DFU's own grunt stands in where DFU has one).
        /// </summary>
        string Playable(string pool)
        {
            return set != null ? SoundPools.Resolve(pool, set.Clips.Keys) : null;
        }

        /// <summary>Plays a random sound of the event's pool, rolling its chance; false when nothing played.</summary>
        public bool Play(string pool)
        {
            bool bark=CompanionBarkRules.IsBark(pool);
            if(bark && (!BarksEnabled || brain.IsEnemy || brain.State.hostile))return false;
            if(CompanionSpeaker && GameManager.IsGamePaused)return false;
            string playable = Playable(pool);
            if (playable == null || voices == null || !VoiceModes.CustomVoice(Mode))
                return false;
            // Animal alert, warning and call sounds are paced by the animal itself (spec animal-ai §2.3): no chance roll.
            if (pool != SoundPools.Death && pool != SoundPools.Halt && pool != SoundPools.Hawk && !bark &&
                pool != SoundPools.Alert && pool != SoundPools.Warn && pool != SoundPools.Call)
            {
                int chance = SoundPools.IsCombat(pool) ? settings.CombatChance : settings.GreetChance;
                if (!SoundRules.Rolls(chance, Random.value))
                    return false;
            }
            AudioClip clip = set.Pick(playable);
            if (clip == null)
                return false;
            AudioSource voice = Speech(clip,pool,SoundRules.Pitch(settings.SoundPitch,settings.SoundPitchVariation,Random.value));
            if (pool == SoundPools.Greet)
                greetSource = voice;
            Count(pool);
            return true;
        }

        /// <summary>Pursuit warning; a configured custom clip gets time to load before falling back.</summary>
        public void PlayHalt() { haltPending = true; DeliverHalt(); }
        public void CancelHalt() { haltPending = false; }
        void DeliverHalt()
        {
            if (!haltPending || gone || !isActiveAndEnabled) return;
            if (Mode == VoiceModes.Off) { haltPending = false; return; }
            if (VoiceModes.CustomVoice(Mode) && set != null && set.Files.ContainsKey(SoundPools.Halt) && !set.IsReady) return;
            haltPending = false;
            if (Play(SoundPools.Halt)) return;
            DaggerfallAudioSource source = GetComponent<DaggerfallAudioSource>();
            if (source != null) { source.PlayOneShot(SoundClips.Halt); Count("vanilla_halt"); }
        }

        /// <summary>Loaded talk clips (spec conversations §1).</summary>
        public int TalkClips
        {
            get
            {
                string pool = TalkPoolName;
                List<AudioClip> clips;
                return pool != null && set.Clips.TryGetValue(pool, out clips) ? clips.Count : 0;
            }
        }

        /// <summary>Its talk pool: talk_male / talk_female for its gender, else talk; null without one (SoundPools.TalkPool).</summary>
        public string TalkPoolName
        {
            get
            {
                if (set == null)
                    return null;
                string gender = brain != null && brain.Instance != null ? brain.Instance.Gender : null;
                return SoundPools.TalkPool(gender, set.Clips.Keys);
            }
        }

        /// <summary>The "ANPC sounds" setting lets the folder's voices play.</summary>
        public bool CustomVoiceOn
        {
            get { return VoiceModes.CustomVoice(Mode); }
        }

        /// <summary>
        /// Plays a talk clip with no chance roll, not the one before when there are more (spec conversations §2).
        /// Returns its seconds at the pitch used; 0 when nothing played.
        /// </summary>
        public float PlayTalk()
        {
            if (voices == null || gone || !CustomVoiceOn || TalkClips == 0)
                return 0f;
            List<AudioClip> clips = set.Clips[TalkPoolName];
            lastTalk = ChatRules.NextClip(clips.Count, lastTalk, Random.value);
            AudioClip clip = clips[lastTalk];
            if (clip == null)
                return 0f;
            AudioSource voice = Speech(clip,SoundPools.Talk,SoundRules.Pitch(settings.SoundPitch,settings.SoundPitchVariation,Random.value));
            talkSource = voice;
            Count(SoundPools.Talk);
            return clip.length / Mathf.Max(SoundRules.MinPitch, voice.pitch);
        }

        /// <summary>A greet sound is still playing (a conversation's first line waits for it).</summary>
        public bool PlayingGreet
        {
            get { return greetSource != null && greetSource.isPlaying; }
        }

        /// <summary>A talk clip is still playing (self-test).</summary>
        public bool TalkPlaying
        {
            get { return talkSource != null && talkSource.isPlaying; }
        }

        /// <summary>Cuts the talk clip short (a conversation ended early).</summary>
        public void StopTalk()
        {
            FadeOut(talkSource, false);
            talkSource = null;
        }

        /// <summary>
        /// Death: plays the death sound from its own short-lived object (the ANPC is hidden), then lets go of the set
        /// (a dead ANPC makes no more sounds; the clip outlives the set's unload delay).
        /// </summary>
        public void OnDied()
        {
            if(CompanionSpeaker || CompanionBarkRules.IsBark(lastSpeechPool))StopSpeech();
            ResetCompanionSpeech();
            PlayDeath();
            gone = true;
            ReleaseAll();
        }

        void PlayDeath()
        {
            if (Playable(SoundPools.Death) == null || !VoiceModes.CustomVoice(Mode))
                return;
            AudioClip clip = set.Pick(SoundPools.Death);
            if (clip == null)
                return;
            GameObject go = new GameObject("AnpcDeathSound");
            go.transform.position = transform.position;
            AudioSource a = go.AddComponent<AudioSource>();
            Configure(a);
            a.pitch = SoundRules.Pitch(settings.SoundPitch, settings.SoundPitchVariation, Random.value);
            a.volume = DaggerfallUnity.Settings.SoundVolume;
            a.clip = clip;
            a.Play();
            Destroy(go, clip.length / a.pitch + 0.2f);
            Count(SoundPools.Death);
        }

        /// <summary>The sprite showed a new frame of a walk or run sheet: a footfall on its step frames.</summary>
        public void OnSheetFrame(SpriteAnimation anim, int frame)
        {
            if (System.Array.IndexOf(SoundRules.StepFrames(anim), frame) >= 0)
                Step();
        }

        void Step()
        {
            // Its own step files, else the shared defaults; its own replace the defaults even while they load.
            LoadedSoundSet stepDefaults = StepDefaults;
            LoadedSoundSet source = OwnSteps ? set : stepDefaults;
            if (!FootstepsOn || source == null || steps == null || brain == null || brain.CurrentMode == NpcMode.Dead)
                return;
            if (behaviour != null && behaviour.Entity != null && behaviour.Entity.IsParalyzed)
                return;
            GameObject player = GameManager.Instance.PlayerObject;
            if (player == null || Vector3.Distance(player.transform.position, transform.position) > settings.StepDistance)
                return;
            AudioClip clip = source.Pick(SoundPools.Step);
            if (clip == null)
                return;
            AudioSource step = steps[nextStep];
            nextStep = SoundRules.NextVoice(nextStep, steps.Length);
            step.pitch = SoundRules.Pitch(settings.StepPitch, settings.StepPitchVariation, Random.value);
            LastStepVolume = DaggerfallUnity.Settings.SoundVolume * SoundRules.StepVolume(source == stepDefaults, StepsVolumePercent);
            step.PlayOneShot(clip, LastStepVolume);
            Count(SoundPools.Step);
            if (source == stepDefaults)
                Count("default_step");
        }

        void Count(string pool)
        {
            int n;
            played.TryGetValue(pool, out n);
            played[pool] = n + 1;
        }

        void Update()
        {
            if (brain == null || behaviour == null || behaviour.Entity == null || brain.CurrentMode == NpcMode.Dead)
                return;
            DeliverHalt();
            ValidatePendingCommand();
            if(CompanionSpeech && GameManager.IsGamePaused)
            {
                if(!SpeechControls && CompanionBarkRules.IsBark(lastSpeechPool))StopSpeech();
                // Only lines spoken as a companion pause with the UI: cutting a greeting already playing when the
                // player hires (talk window open) clicked loudly.
                if(!speechPaused && companionClip && voices!=null) {foreach(var source in voices)FadeOut(source,true);speechPaused=true;}
                return;
            }
            if(speechPaused && voices!=null) {foreach(var source in voices)Resume(source);speechPaused=false;}
            DeliverCompanionCommand();
            SyncVoiceRace();
            SyncCreatureAttack();

            // Attacks: each one DFU starts (melee or bow).
            MobileStates s = mobile != null ? mobile.EnemyState : MobileStates.Idle;
            if (s != lastState)
            {
                NpcRanged ranged = GetComponent<NpcRanged>();
                if (s == MobileStates.PrimaryAttack && ranged != null && ranged.Throwing)
                    OnEvent(SoundPools.AttackRange, true, false, 20, false);
                else if (s == MobileStates.PrimaryAttack)
                    OnEvent(SoundPools.Attack, true, false, 20, false);
                else if (s == MobileStates.RangedAttack1 || s == MobileStates.RangedAttack2)
                    OnEvent(SoundPools.AttackRange, true, false, 20, false);
            }
            lastState = s;

            // Hits: health dropped and the ANPC lives. Magic may have struck this frame or, before this Update ran, the last.
            int health = behaviour.Entity.CurrentHealth;
            if (lastHealth >= 0 && health < lastHealth && health > 0)
                OnEvent(SoundPools.Hit, false, lastHealth - health >= behaviour.Entity.MaxHealth / 4, 40, Time.frameCount - magicFrame <= 1);
            lastHealth = health;
            ObserveCompanionBattle();
            CompanionBarks();

            // Footsteps of an ANPC without a walk/run sheet: a timer while it moves.
            bool sheetSteps = sprite != null && sprite.Set != null;
            Gait g = gait != null ? gait.Current : Gait.Still;
            if (!sheetSteps && g != Gait.Still)
            {
                stepTimer -= Time.deltaTime;
                if (stepTimer <= 0f)
                {
                    Step();
                    stepTimer = g == Gait.Run ? SoundRules.RunStepSeconds : SoundRules.WalkStepSeconds;
                }
            }
            else
            {
                stepTimer = 0f;
            }
        }

        /// <summary>
        /// DFU voices the ANPC itself (its race back in EnemySounds) with the Vanilla setting, or with ANPC and no Sounds
        /// folder; otherwise the race stays here: the mod plays the folder's sounds and stands in for DFU, or (Off) nothing.
        /// </summary>
        void SyncVoiceRace()
        {
            if (enemySounds == null)
                return;
            bool dfu = !CompanionSpeech && VoiceModes.DfuVoice(Mode, set != null);
            if (dfu && enemySounds.RaceForSounds == Races.None && voiceRace != Races.None)
            {
                enemySounds.RaceForSounds = voiceRace;
            }
            else if (!dfu && enemySounds.RaceForSounds != Races.None)
            {
                voiceRace = enemySounds.RaceForSounds;      // DFU may have swapped a male high elf's voice for a wood elf's
                enemySounds.RaceForSounds = Races.None;
            }
        }

        /// <summary>DFU voices this ANPC itself now (self-test).</summary>
        public bool DfuVoicing
        {
            get { return enemySounds != null && enemySounds.RaceForSounds != Races.None; }
        }

        /// <summary>The folder's sound for the event, or else DFU's own grunt where DFU voices one (spec sounds §E).</summary>
        void OnEvent(string pool, bool attack, bool heavy, int vanillaChance, bool magicDamage)
        {
            if(CompanionSpeech && Mode!=VoiceModes.Off && (set==null || Mode==VoiceModes.Vanilla))
            { if(SoundRules.VanillaVoice(pool,false,magicDamage))VanillaGrunt(attack,heavy,vanillaChance);return; }
            if (set == null || !VoiceModes.CustomVoice(Mode))
                return;     // DFU voices it itself, or voices are off
            bool custom = Playable(pool) != null;
            if (custom)
                Play(pool);
            else if (SoundRules.VanillaVoice(pool, custom, magicDamage))
                VanillaGrunt(attack, heavy, vanillaChance);
        }

        /// <summary>DFU's own grunt (same setting, chance, race and gender), for an event the folder has no sound for.</summary>
        void VanillaGrunt(bool attack, bool heavy, int chance)
        {
            bool force = ForceVanillaVoice && SelfTest.Running;
            if (enemySounds == null || voiceRace == Races.None || (!DaggerfallUnity.Settings.CombatVoices && !force))
                return;
            EnemyEntity entity = behaviour.Entity as EnemyEntity;
            if (entity == null || entity.EntityType != EntityTypes.EnemyClass || (!force && !Dice100.SuccessRoll(chance)))
                return;
            Genders gender = mobile != null && (mobile.Enemy.Gender == MobileGender.Male || entity.MobileEnemy.ID == (int)MobileTypes.Knight_CityWatch)
                ? Genders.Male : Genders.Female;
            if(CompanionSpeech)
            {
                // Native combat speech uses a separate ANPC voice source; native swing/footstep effects are never stopped.
                if(gender==Genders.Male && voiceRace==Races.HighElf)voiceRace=Races.WoodElf;
                var sound=attack?DaggerfallEntity.GetRaceGenderAttackSound(voiceRace,gender):DaggerfallEntity.GetRaceGenderPainSound(voiceRace,gender,heavy);
                AudioClip clip=DaggerfallUnity.Instance.SoundReader.GetAudioClip((int)sound);
                if(clip!=null) {Speech(clip,attack?"vanilla_attack":"vanilla_hit",1f+Random.Range(0,.3f));Count(attack?"vanilla_attack":"vanilla_hit");}
                return;
            }
            enemySounds.RaceForSounds = voiceRace;
            enemySounds.PlayCombatVoice(gender, attack, heavy);
            voiceRace = enemySounds.RaceForSounds;      // DFU may swap a male high elf's voice for a wood elf's
            enemySounds.RaceForSounds = Races.None;
            Count(attack ? "vanilla_attack" : "vanilla_hit");
        }
    }
}

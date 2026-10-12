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
    public class NpcVoice : MonoBehaviour
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
        LoadedSoundSet defaultsOffered;     // the library's shared steps
        LoadedSoundSet stepDefaults;        // ... when this ANPC uses them
        bool defaultsAcquired;
        int lastHealth = -1;
        MobileStates lastState;
        float stepTimer;
        readonly Dictionary<string, int> played = new Dictionary<string, int>();
        int lastTalk = -1;
        AudioSource talkSource;

        /// <summary>The ANPC's sound set (anpc_info).</summary>
        public LoadedSoundSet Set
        {
            get { return set; }
        }

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

        /// <summary>Where its footsteps come from: "own", "default" or "none" (anpc_info, self-test).</summary>
        public string StepsSource
        {
            get
            {
                if (OwnSteps)
                    return set.IsReady && !set.Clips.ContainsKey(SoundPools.Step) ? "own (none could be loaded)" : "own";
                return stepDefaults != null ? "default" : "none";
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

        /// <summary>The shared steps when it has none of its own and npc.json allows them; held while it is in the world.</summary>
        void ChooseSteps()
        {
            LoadedSoundSet want = defaultsOffered != null && SoundRules.UseDefaultSteps(OwnSteps, settings.DefaultSteps) ? defaultsOffered : null;
            if (want == stepDefaults)
                return;
            if (defaultsAcquired)
                stepDefaults.Release();
            defaultsAcquired = false;
            stepDefaults = want;
            if (started && !gone && stepDefaults != null)
            {
                stepDefaults.Acquire();
                defaultsAcquired = true;
            }
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
            if (stepDefaults != null && !defaultsAcquired)
            {
                stepDefaults.Acquire();
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
            if (defaultsAcquired && stepDefaults != null)
                stepDefaults.Release();
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
            string playable = Playable(pool);
            if (playable == null || voices == null || !VoiceModes.CustomVoice(Mode))
                return false;
            if (pool != SoundPools.Death)
            {
                int chance = SoundPools.IsCombat(pool) ? settings.CombatChance : settings.GreetChance;
                if (!SoundRules.Rolls(chance, Random.value))
                    return false;
            }
            AudioClip clip = set.Pick(playable);
            if (clip == null)
                return false;
            AudioSource voice = voices[nextVoice];
            nextVoice = SoundRules.NextVoice(nextVoice, voices.Length);
            voice.pitch = SoundRules.Pitch(settings.SoundPitch, settings.SoundPitchVariation, Random.value);
            voice.PlayOneShot(clip, DaggerfallUnity.Settings.SoundVolume);
            lastVoice = voice;
            Count(pool);
            return true;
        }

        /// <summary>Loaded talk clips (spec conversations §1).</summary>
        public int TalkClips
        {
            get
            {
                List<AudioClip> clips;
                return set != null && set.Clips.TryGetValue(SoundPools.Talk, out clips) ? clips.Count : 0;
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
            List<AudioClip> clips = set.Clips[SoundPools.Talk];
            lastTalk = ChatRules.NextClip(clips.Count, lastTalk, Random.value);
            AudioClip clip = clips[lastTalk];
            if (clip == null)
                return 0f;
            AudioSource voice = voices[nextVoice];
            nextVoice = SoundRules.NextVoice(nextVoice, voices.Length);
            voice.pitch = SoundRules.Pitch(settings.SoundPitch, settings.SoundPitchVariation, Random.value);
            voice.PlayOneShot(clip, DaggerfallUnity.Settings.SoundVolume);
            lastVoice = voice;
            talkSource = voice;
            Count(SoundPools.Talk);
            return clip.length / Mathf.Max(SoundRules.MinPitch, voice.pitch);
        }

        /// <summary>A talk clip is still playing (self-test).</summary>
        public bool TalkPlaying
        {
            get { return talkSource != null && talkSource.isPlaying; }
        }

        /// <summary>Cuts the talk clip short (a conversation ended early).</summary>
        public void StopTalk()
        {
            if (talkSource != null)
                talkSource.Stop();
            talkSource = null;
        }

        /// <summary>
        /// Death: plays the death sound from its own short-lived object (the ANPC is hidden), then lets go of the set
        /// (a dead ANPC makes no more sounds; the clip outlives the set's unload delay).
        /// </summary>
        public void OnDied()
        {
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
            step.PlayOneShot(clip, DaggerfallUnity.Settings.SoundVolume);
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
            SyncVoiceRace();
            SyncCreatureAttack();

            // Attacks: each one DFU starts (melee or bow).
            MobileStates s = mobile != null ? mobile.EnemyState : MobileStates.Idle;
            if (s != lastState)
            {
                if (s == MobileStates.PrimaryAttack)
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
            bool dfu = VoiceModes.DfuVoice(Mode, set != null);
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
            enemySounds.RaceForSounds = voiceRace;
            enemySounds.PlayCombatVoice(gender, attack, heavy);
            voiceRace = enemySounds.RaceForSounds;      // DFU may swap a male high elf's voice for a wood elf's
            enemySounds.RaceForSounds = Races.None;
            Count(attack ? "vanilla_attack" : "vanilla_hit");
        }
    }
}

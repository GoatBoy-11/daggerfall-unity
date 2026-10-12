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
        int lastHealth = -1;
        MobileStates lastState;
        float stepTimer;
        readonly Dictionary<string, int> played = new Dictionary<string, int>();

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

        public void Init(LoadedSoundSet sounds, SoundSettings soundSettings)
        {
            set = sounds;
            settings = soundSettings ?? new SoundSettings();
            enemySounds = GetComponent<EnemySounds>();
            if (enemySounds != null && enemySounds.RaceForSounds != Races.None)
            {
                // DFU picked a random voice in Awake; the mod plays it itself where the folder has no sound (spec sounds §E).
                voiceRace = enemySounds.RaceForSounds;
                enemySounds.RaceForSounds = Races.None;
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
            bool silenceIt = Playable(SoundPools.Attack) != null;
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
            bool started = acquired;
            if (acquired && set != null)
                set.Release();
            acquired = false;
            set = next;
            if (started && set != null)
            {
                set.Acquire();
                acquired = true;
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
            if (set != null && !acquired)
            {
                set.Acquire();
                acquired = true;
            }
        }

        void OnDestroy()
        {
            if (effects != null)
                effects.OnAssignBundle -= OnAssignBundle;
            EntityEffectBroker.OnNewMagicRound -= OnNewMagicRound;
            if (acquired && set != null)
                set.Release();
            acquired = false;
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
            if (playable == null || voices == null)
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

        /// <summary>
        /// Death: plays the death sound from its own short-lived object (the ANPC is hidden), then lets go of the set
        /// (a dead ANPC makes no more sounds; the clip outlives the set's unload delay).
        /// </summary>
        public void OnDied()
        {
            PlayDeath();
            if (acquired && set != null)
                set.Release();
            acquired = false;
        }

        void PlayDeath()
        {
            if (Playable(SoundPools.Death) == null)
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
            if (Playable(SoundPools.Step) == null || steps == null || brain == null || brain.CurrentMode == NpcMode.Dead)
                return;
            if (behaviour != null && behaviour.Entity != null && behaviour.Entity.IsParalyzed)
                return;
            GameObject player = GameManager.Instance.PlayerObject;
            if (player == null || Vector3.Distance(player.transform.position, transform.position) > settings.StepDistance)
                return;
            AudioClip clip = set.Pick(SoundPools.Step);
            if (clip == null)
                return;
            AudioSource step = steps[nextStep];
            nextStep = SoundRules.NextVoice(nextStep, steps.Length);
            step.pitch = SoundRules.Pitch(settings.StepPitch, settings.StepPitchVariation, Random.value);
            step.PlayOneShot(clip, DaggerfallUnity.Settings.SoundVolume);
            Count(SoundPools.Step);
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

        /// <summary>The folder's sound for the event, or else DFU's own grunt where DFU voices one (spec sounds §E).</summary>
        void OnEvent(string pool, bool attack, bool heavy, int vanillaChance, bool magicDamage)
        {
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

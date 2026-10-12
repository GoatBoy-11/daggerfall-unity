using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
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
        AudioSource voice;
        AudioSource steps;
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
            voice = MakeSource("AnpcVoice");
            steps = MakeSource("AnpcSteps");
        }

        AudioSource MakeSource(string name)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            AudioSource a = go.AddComponent<AudioSource>();
            Configure(a);
            return a;
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
            if (set != null && !acquired)
            {
                set.Acquire();
                acquired = true;
            }
        }

        void OnDestroy()
        {
            if (acquired && set != null)
                set.Release();
            acquired = false;
        }

        bool HasPool(string pool)
        {
            return set != null && SoundPools.Resolve(pool, set.Pools) != null;
        }

        /// <summary>Plays a random sound of the event's pool, rolling its chance; false when nothing played.</summary>
        public bool Play(string pool)
        {
            if (!HasPool(pool))
                return false;
            if (pool != SoundPools.Death)
            {
                int chance = SoundPools.IsCombat(pool) ? settings.CombatChance : settings.GreetChance;
                if (!SoundRules.Rolls(chance, Random.value))
                    return false;
            }
            AudioClip clip = set.Pick(SoundPools.Resolve(pool, set.Pools));
            if (clip == null || voice == null)
                return false;
            voice.pitch = SoundRules.Pitch(settings.SoundPitch, settings.SoundPitchVariation, Random.value);
            voice.PlayOneShot(clip, DaggerfallUnity.Settings.SoundVolume);
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
            if (!HasPool(SoundPools.Death))
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
            if (!HasPool(SoundPools.Step) || steps == null || brain == null || brain.CurrentMode == NpcMode.Dead)
                return;
            if (behaviour != null && behaviour.Entity != null && behaviour.Entity.IsParalyzed)
                return;
            GameObject player = GameManager.Instance.PlayerObject;
            if (player == null || Vector3.Distance(player.transform.position, transform.position) > settings.StepDistance)
                return;
            AudioClip clip = set.Pick(SoundPools.Step);
            if (clip == null)
                return;
            steps.pitch = SoundRules.Pitch(settings.StepPitch, settings.StepPitchVariation, Random.value);
            steps.PlayOneShot(clip, DaggerfallUnity.Settings.SoundVolume);
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

            // Attacks: each one DFU starts (melee or bow).
            MobileStates s = mobile != null ? mobile.EnemyState : MobileStates.Idle;
            if (s != lastState)
            {
                if (s == MobileStates.PrimaryAttack)
                    OnEvent(SoundPools.Attack, true, false, 20);
                else if (s == MobileStates.RangedAttack1 || s == MobileStates.RangedAttack2)
                    OnEvent(SoundPools.AttackRange, true, false, 20);
            }
            lastState = s;

            // Hits: health dropped and the ANPC lives.
            int health = behaviour.Entity.CurrentHealth;
            if (lastHealth >= 0 && health < lastHealth && health > 0)
                OnEvent(SoundPools.Hit, false, lastHealth - health >= behaviour.Entity.MaxHealth / 4, 40);
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

        /// <summary>The folder's sound for the event, or else DFU's own grunt (spec sounds §E).</summary>
        void OnEvent(string pool, bool attack, bool heavy, int vanillaChance)
        {
            if (SoundRules.VanillaVoice(HasPool(pool)))
                VanillaGrunt(attack, heavy, vanillaChance);
            else
                Play(pool);
        }

        /// <summary>DFU's own grunt (same setting, chance, race and gender), for an event the folder has no sound for.</summary>
        void VanillaGrunt(bool attack, bool heavy, int chance)
        {
            if (enemySounds == null || voiceRace == Races.None || !DaggerfallUnity.Settings.CombatVoices)
                return;
            EnemyEntity entity = behaviour.Entity as EnemyEntity;
            if (entity == null || entity.EntityType != EntityTypes.EnemyClass || !Dice100.SuccessRoll(chance))
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

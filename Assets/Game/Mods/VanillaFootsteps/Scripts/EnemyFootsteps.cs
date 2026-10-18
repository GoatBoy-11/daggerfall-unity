using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;

namespace VanillaFootsteps
{
    /// <summary>
    /// Footsteps of a vanilla enemy that walks: on the first and middle frame of its walk animation while it really
    /// moves on the ground. Flyers beat their wings softly instead, once per loop of their flying animation (hovering
    /// too). Swimmers, ghosts and levitating enemies are silent; so are ANPCs, which have footsteps of their own.
    /// </summary>
    public class EnemyFootsteps : MonoBehaviour
    {
        DaggerfallEnemy enemy;
        EnemyMotor motor;
        CharacterController controller;
        StepVoice voice;
        StepProfile profile;
        int profileFor = -1;
        bool profileFlying;
        int lastFrame = -1;
        Vector3 lastPosition;
        float lastMovedAt = -100f;
        float stepTimer;

        /// <summary>Walking (or flying) in earshot with its kind's sounds on (vfootsteps).</summary>
        public bool Audible { get; private set; }

        /// <summary>A flyer: its wings beat instead of its feet stepping (vfootsteps).</summary>
        public bool Flying { get; private set; }

        void Start()
        {
            if (IsAnpc(gameObject))
            {
                Destroy(this);
                return;
            }
            enemy = GetComponent<DaggerfallEnemy>();
            motor = GetComponent<EnemyMotor>();
            controller = GetComponent<CharacterController>();
            voice = new StepVoice(transform);
            lastPosition = transform.position;
        }

        /// <summary>An Advanced NPC: ANPC plays its own footsteps (and its default ones) for it.</summary>
        public static bool IsAnpc(GameObject go)
        {
            foreach (MonoBehaviour b in go.GetComponents<MonoBehaviour>())
            {
                if (b == null)
                    continue;
                string name = b.GetType().Name;
                if (name == "NpcVoice" || name == "NpcBrain")
                    return true;
            }
            return false;
        }

        void Update()
        {
            Audible = false;
            VanillaFootstepsMod mod = VanillaFootstepsMod.Instance;
            MobileUnit mobile = enemy != null ? enemy.MobileUnit : null;
            if (mod == null || mobile == null || !mobile.IsSetup)
                return;

            // Moving: its position changed lately (DFU plays the walk animation while an enemy only turns, too).
            Vector3 delta = transform.position - lastPosition;
            delta.y = 0f;
            lastPosition = transform.position;
            if (FootstepRules.IsWalking(delta.magnitude, Time.deltaTime))
                lastMovedAt = Time.time;
            bool moving = Time.time - lastMovedAt <= FootstepRules.MovingGraceSeconds;

            int id = mobile.Enemy.ID;
            MobileBehaviour behaviour = mobile.Enemy.Behaviour;      // a Daedra Seducer turns into a flyer
            Flying = behaviour == MobileBehaviour.Flying;
            if (Flying)
            {
                UpdateWings(mod, mobile, id);
                return;
            }

            // Human affinity too: classes that other mods (Enemy Expansion, say) add have ids beyond 146.
            bool human = FootstepRules.IsClass(id) || mobile.Enemy.Affinity == MobileAffinity.Human;
            bool kindOn = human ? mod.ClassEnemies : mod.Monsters;
            bool onFeet = behaviour != MobileBehaviour.Spectral &&
                          behaviour != MobileBehaviour.Aquatic && (motor == null || !motor.IsLevitating) &&
                          (controller == null || controller.isGrounded);
            bool walking = kindOn && onFeet && moving && mobile.EnemyState == MobileStates.Move && !mobile.FreezeAnims;
            if (!walking)
            {
                stepTimer = 0f;
                lastFrame = -1;
                return;
            }
            Audible = mod.InEarshot(transform.position);
            UseProfile(id, false);

            // Vanilla billboards: a footfall on the walk animation's first and middle frame.
            DaggerfallMobileUnit billboard = mobile as DaggerfallMobileUnit;
            MobileAnimation[] anims = mobile.Summary.StateAnims;
            if (billboard != null && anims != null && anims.Length > 0)
            {
                int frame = billboard.CurrentFrame;
                if (frame != lastFrame && lastFrame != -1 && FootstepRules.IsFootfall(frame, anims[0].NumFrames))
                    voice.Step(profile);
                lastFrame = frame;
                return;
            }

            // Another mod's mobile unit (a 3D model, say): a step every half second.
            stepTimer -= Time.deltaTime;
            if (stepTimer <= 0f)
            {
                voice.Step(profile);
                stepTimer = FootstepRules.WalkStepSeconds;
            }
        }

        /// <summary>A flyer's wings beat on the first frame of each loop of its flying animation, moving or hovering.</summary>
        void UpdateWings(VanillaFootstepsMod mod, MobileUnit mobile, int id)
        {
            MobileStates state = mobile.EnemyState;
            bool flapping = mod.WingBeats && (state == MobileStates.Move || state == MobileStates.Idle) && !mobile.FreezeAnims;
            if (!flapping)
            {
                stepTimer = 0f;
                lastFrame = -1;
                return;
            }
            Audible = mod.InEarshot(transform.position);
            UseProfile(id, true);

            DaggerfallMobileUnit billboard = mobile as DaggerfallMobileUnit;
            if (billboard != null)
            {
                int frame = billboard.CurrentFrame;
                if (frame != lastFrame && lastFrame != -1 && FootstepRules.IsWingBeat(frame))
                    voice.Wing(profile);
                lastFrame = frame;
                return;
            }

            stepTimer -= Time.deltaTime;
            if (stepTimer <= 0f)
            {
                voice.Wing(profile);
                stepTimer = FootstepRules.WingBeatSeconds;
            }
        }

        void UseProfile(int id, bool flying)
        {
            if (profileFor == id && profileFlying == flying)
                return;
            profile = flying ? FootstepRules.ForFlyer(id) : FootstepRules.ForEnemy(id);
            profileFor = id;
            profileFlying = flying;
        }
    }
}

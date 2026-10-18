using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;

namespace VanillaFootsteps
{
    /// <summary>
    /// Footsteps of a vanilla enemy that walks: on the first and middle frame of its walk animation while it really
    /// moves on the ground. Flyers, swimmers, ghosts and levitating enemies are silent; so are ANPCs, which have
    /// footsteps of their own.
    /// </summary>
    public class EnemyFootsteps : MonoBehaviour
    {
        DaggerfallEnemy enemy;
        EnemyMotor motor;
        CharacterController controller;
        StepVoice voice;
        StepProfile profile;
        int profileFor = -1;
        int lastFrame = -1;
        Vector3 lastPosition;
        float lastMovedAt = -100f;
        float stepTimer;

        /// <summary>Walking in earshot with its kind's footsteps on (vfootsteps).</summary>
        public bool Audible { get; private set; }

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
            bool kindOn = FootstepRules.IsClass(id) ? mod.ClassEnemies : mod.Monsters;
            MobileBehaviour behaviour = mobile.Enemy.Behaviour;      // a Daedra Seducer turns into a flyer
            bool onFeet = behaviour != MobileBehaviour.Flying && behaviour != MobileBehaviour.Spectral &&
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
            if (profileFor != id)
            {
                profile = FootstepRules.ForEnemy(id);
                profileFor = id;
            }

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
    }
}

using System.Reflection;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;

namespace StepSounds
{
    /// <summary>
    /// Footsteps of a wandering townsperson (city watch included): on the first and middle frame of its walk animation
    /// while it walks. DFU keeps the billboard's frame private, so it is read by reflection; a replaced asset (another
    /// mod's model) steps every half second instead.
    /// </summary>
    public class TownspersonFootsteps : MonoBehaviour
    {
        static FieldInfo frameField, animsField;
        static bool fieldsLooked;

        MobilePersonNPC npc;
        MobilePersonMotor motor;
        StepVoice voice;
        int lastFrame = -1;
        Vector3 lastPosition;
        float lastMovedAt = -100f;
        float stepTimer;

        /// <summary>Walking in earshot with townspeople's footsteps on (step_sounds).</summary>
        public bool Audible { get; private set; }

        void Start()
        {
            npc = GetComponent<MobilePersonNPC>();
            motor = npc != null && npc.Motor != null ? npc.Motor : GetComponent<MobilePersonMotor>();
            voice = new StepVoice(transform);
            lastPosition = transform.position;
            if (!fieldsLooked)
            {
                fieldsLooked = true;
                frameField = typeof(MobilePersonBillboard).GetField("currentFrame", BindingFlags.NonPublic | BindingFlags.Instance);
                animsField = typeof(MobilePersonBillboard).GetField("stateAnims", BindingFlags.NonPublic | BindingFlags.Instance);
                if (frameField == null || animsField == null)
                    StepSoundsMod.Log("MobilePersonBillboard frame fields not found: townspeople step every " +
                                            FootstepRules.WalkStepSeconds + " s instead of on their walk frames.");
            }
        }

        void OnEnable()
        {
            // A pooled townsperson comes back somewhere else: no step for the jump.
            lastPosition = transform.position;
            lastFrame = -1;
            lastMovedAt = -100f;
        }

        void Update()
        {
            Audible = false;
            StepSoundsMod mod = StepSoundsMod.Instance;
            MobilePersonAsset asset = npc != null && npc.Asset != null ? npc.Asset : (motor != null ? motor.MobileAsset : null);
            if (mod == null || motor == null || asset == null)
                return;

            Vector3 delta = transform.position - lastPosition;
            delta.y = 0f;
            lastPosition = transform.position;
            if (FootstepRules.IsWalking(delta.magnitude, Time.deltaTime))
                lastMovedAt = Time.time;
            bool moving = Time.time - lastMovedAt <= FootstepRules.MovingGraceSeconds;

            bool walking = mod.Townspeople && moving && !asset.IsIdle &&
                           motor.CurrentState == MobilePersonMotor.MobileStates.MovingForward;
            if (!walking)
            {
                stepTimer = 0f;
                lastFrame = -1;
                return;
            }
            Audible = mod.InEarshot(transform.position);

            MobilePersonBillboard billboard = asset as MobilePersonBillboard;
            if (billboard != null && frameField != null && animsField != null)
            {
                MobileAnimation[] anims = animsField.GetValue(billboard) as MobileAnimation[];
                if (anims != null && anims.Length > 0)
                {
                    int frame = (int)frameField.GetValue(billboard);
                    if (frame != lastFrame && lastFrame != -1 && FootstepRules.IsFootfall(frame, anims[0].NumFrames))
                        voice.Step(FootstepRules.Person);
                    lastFrame = frame;
                    return;
                }
            }

            stepTimer -= Time.deltaTime;
            if (stepTimer <= 0f)
            {
                voice.Step(FootstepRules.Person);
                stepTimer = FootstepRules.WalkStepSeconds;
            }
        }
    }
}

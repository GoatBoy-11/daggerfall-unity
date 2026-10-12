using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>
    /// Running and turrets (spec run/greet/turret §3). Decides the gait each frame after DFU's EnemyMotor has moved
    /// the controller; while chasing at a run it adds (runSpeed - 1) of that movement on top, so DFU keeps its
    /// pathing, doors and attacks. A turret (no walk and no run sheet) is moved back to its spot every frame.
    /// </summary>
    public class NpcGait : MonoBehaviour
    {
        const float MovingSpeed = 0.15f;     // m/s above which the ANPC counts as moving (as NpcSprite)

        NpcBrain brain;
        NpcDefinition def;
        EnemySenses senses;
        EnemyMotor motor;
        CharacterController controller;
        MobileUnit mobile;
        DaggerfallEntityBehaviour behaviour;
        bool turret;
        Vector3 pinLocal;
        Vector3 lastLocal;
        Gait gait = Gait.Still;
        float boosted;

        public Gait Current
        {
            get { return gait; }
        }

        public bool IsTurret
        {
            get { return turret; }
        }

        /// <summary>Metres added on top of DFU's motor so far (self-test).</summary>
        public float Boosted
        {
            get { return boosted; }
        }

        /// <summary>DFU's enemy move speed for this ANPC, m/s.</summary>
        public float WalkSpeed
        {
            get
            {
                int live = behaviour != null && behaviour.Entity != null ? behaviour.Entity.Stats.LiveSpeed : 50;
                return GaitRules.WalkSpeed(live);
            }
        }

        /// <summary>Running speed (walk speed when it cannot run), m/s.</summary>
        public float RunSpeed
        {
            get { return def != null ? GaitRules.RunSpeed(WalkSpeed, def.RunSpeed, def.CanRun) : WalkSpeed; }
        }

        /// <summary>Call after the sprite (if any) has its set, so a turret can be recognised.</summary>
        public void Init(NpcDefinition definition)
        {
            def = definition;
            brain = GetComponent<NpcBrain>();
            senses = GetComponent<EnemySenses>();
            motor = GetComponent<EnemyMotor>();
            controller = GetComponent<CharacterController>();
            mobile = GetComponentInChildren<MobileUnit>();
            behaviour = GetComponent<DaggerfallEntityBehaviour>();
            NpcSprite sprite = GetComponent<NpcSprite>();
            turret = sprite != null && sprite.ChosenSet != null && SpriteStates.IsStationary(sprite.ChosenSet.Set);
            Repin();
        }

        /// <summary>Remembers where it stands now (spawn, Teleport). Town-local: floating-origin shifts are not movement.</summary>
        public void Repin()
        {
            pinLocal = transform.localPosition;
            lastLocal = pinLocal;
        }

        void LateUpdate()
        {
            if (brain == null || def == null || brain.CurrentMode == NpcMode.Dead || controller == null || !controller.enabled)
                return;
            Vector3 local = transform.localPosition;
            Vector3 step = local - lastLocal;
            step.y = 0;
            if (GaitRules.IsJump(step.magnitude))
            {
                // DFU's floating origin moved the ANPC itself (wilderness ANPCs are children of StreamingTarget):
                // the spot moves with the world, and none of it is running.
                pinLocal += step;
                lastLocal = local;
                return;
            }
            if (turret)
            {
                gait = Gait.Still;
                Vector3 back = pinLocal - local;
                back.y = 0;
                if (back.sqrMagnitude > 1e-8f)
                    controller.Move(ToWorld(back));
                lastLocal = transform.localPosition;
                return;
            }

            Vector3 moved = local - lastLocal;
            moved.y = 0;
            float dt = Time.deltaTime;
            bool moving = dt > 0 && moved.magnitude / dt > MovingSpeed;
            if (brain.CurrentMode == NpcMode.Fighting)
            {
                gait = GaitRules.Fighting(senses != null && senses.TargetInSight, senses != null ? senses.DistanceToTarget : 0f,
                    gait == Gait.Run, def.CanRun);
                if (gait == Gait.Run && moving && (motor == null || motor.KnockbackSpeed <= 0) &&
                    (mobile == null || !mobile.IsPlayingOneShot()))
                {
                    Vector3 extra = moved * (def.RunSpeed - 1f);
                    controller.Move(ToWorld(extra));
                    boosted += extra.magnitude;
                }
            }
            else if (brain.CurrentMode == NpcMode.Fleeing)
            {
                gait = GaitRules.Fleeing(moving, def.CanRun);
            }
            else
            {
                gait = GaitRules.Calm(moving);
            }
            lastLocal = transform.localPosition;
        }

        Vector3 ToWorld(Vector3 localDelta)
        {
            return transform.parent != null ? transform.parent.TransformVector(localDelta) : localDelta;
        }
    }
}

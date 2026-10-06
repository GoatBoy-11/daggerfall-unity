using UnityEngine;
using DaggerfallWorkshop;

namespace AdvancedNPCs
{
    /// <summary>Simple direct movement used while the vanilla EnemyMotor is switched off (calm and fleeing).</summary>
    [RequireComponent(typeof(CharacterController))]
    public class NpcMover : MonoBehaviour
    {
        public const float WalkSpeed = 1.5f;
        public const float RunSpeed = 5f;

        CharacterController controller;
        MobileUnit mobile;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            mobile = GetComponentInChildren<MobileUnit>();
        }

        public void Stop()
        {
            if (controller.enabled)
                controller.SimpleMove(Vector3.zero); // keeps gravity applied while standing
            SetAnim(MobileStates.Idle);
        }

        public void MoveToward(Vector3 worldPos, float speed)
        {
            Move(worldPos - transform.position, speed);
        }

        public void MoveAway(Vector3 worldPos, float speed)
        {
            Move(transform.position - worldPos, speed);
        }

        void Move(Vector3 direction, float speed)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                Stop();
                return;
            }
            direction.Normalize();
            transform.rotation = Quaternion.LookRotation(direction);
            if (controller.enabled)
                controller.SimpleMove(direction * speed); // SimpleMove applies gravity
            SetAnim(MobileStates.Move);
        }

        void SetAnim(MobileStates state)
        {
            if (mobile != null && mobile.EnemyState != state)
                mobile.ChangeEnemyState(state);
        }
    }
}

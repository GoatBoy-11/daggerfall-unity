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
        bool ducked;
        float standingHeight, duckUntil;
        Vector3 standingCenter;
        readonly Collider[] overhead = new Collider[32];
        public bool DuckingForDoor { get { return ducked; } }
        public void DuckForDoor()
        {
            if (controller.height <= 1.65f) return;
            if (!ducked) { standingHeight = controller.height; standingCenter = controller.center; ducked = true; }
            float change = controller.height - 1.65f;
            controller.center -= Vector3.up * change * 0.5f;
            controller.height = 1.65f;
            duckUntil = Time.time + 0.8f;
        }
        void LateUpdate()
        {
            if (!ducked || Time.time < duckUntil) return;
            float top = transform.TransformPoint(standingCenter).y + standingHeight * 0.5f;
            float bottom = controller.bounds.max.y;
            Vector3 center = new Vector3(transform.position.x, (top + bottom) * 0.5f, transform.position.z);
            int count = Physics.OverlapBoxNonAlloc(center, new Vector3(Mathf.Max(0.1f, controller.radius - 0.03f),
                Mathf.Max(0.01f, (top - bottom) * 0.5f - 0.02f), Mathf.Max(0.1f, controller.radius - 0.03f)), overhead,
                Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == overhead.Length) return;
            for (int i = 0; i < count; i++)
                if (!overhead[i].transform.IsChildOf(transform) && overhead[i].transform != transform) return;
            RestorePosture();
        }
        void RestorePosture()
        { if (ducked) { controller.height = standingHeight; controller.center = standingCenter; ducked = false; } }
        void OnDisable() { RestorePosture(); }

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            mobile = GetComponentInChildren<MobileUnit>();
        }

        public void Stop()
        {
            if (controller.enabled)
                controller.SimpleMove(Vector3.zero); // keeps gravity applied while standing
            // A calm healer stands still while its vanilla casting pose completes.
            if (mobile == null || mobile.EnemyState != MobileStates.Spell || !mobile.IsPlayingOneShot())
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

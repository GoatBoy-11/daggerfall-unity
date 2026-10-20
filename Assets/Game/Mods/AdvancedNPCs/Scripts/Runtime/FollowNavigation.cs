using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>Bounded player breadcrumbs and local steering. Only a paid follower may catch up, onto a spot out of sight.</summary>
    public class FollowNavigation
    {
        readonly NpcBrain brain;
        readonly NpcMover mover;
        readonly CharacterController body;
        readonly MobileUnit mobile;
        readonly List<Vector3> trail = new List<Vector3>();
        readonly List<Collider> barriers = new List<Collider>();
        readonly FollowRecovery recovery = new FollowRecovery();
        readonly RaycastHit[] hits = new RaycastHit[64];
        static readonly float[] angles = { 40, 70, 100, 135, 170 };
        Vector3 previousLeader, waypoint;
        Transform currentLeader;
        public bool CatchupReady { get; private set; }
        bool observed;
        float nextPlan, nextCatchup, detourUntil;
        int side = 1;
        public string Status { get; private set; }
        public FollowNavigation(NpcBrain brain, NpcMover mover)
        {
            this.brain = brain; this.mover = mover;
            body = brain.GetComponent<CharacterController>(); mobile = brain.GetComponentInChildren<MobileUnit>();
            Status = "following";
        }
        public void Suspend() { recovery.Reset(); nextPlan = 0; observed = false; trail.Clear(); }
        public void Reset() { Suspend(); trail.Clear(); barriers.Clear(); observed = false; }
        Vector3 Feet { get { Vector3 p = brain.transform.position; p.y = body.bounds.min.y + 0.03f; return p; } }
        bool Mine(Collider c) { return c.transform == brain.transform || c.transform.IsChildOf(brain.transform); }
        bool Person(Collider c) { return c.GetComponentInParent<DaggerfallEntityBehaviour>() != null; }
        bool CanOpen { get { return mobile != null && mobile.Enemy.CanOpenDoors; } }
        static Vector3 Flat(Vector3 v) { v.y = 0; return v; }

        bool Protected(Collider c)
        {
            var special = c.GetComponentInParent<DaggerfallActionDoorSpecial>();
            if (special != null) return !special.IsOpen;
            var door = c.GetComponentInParent<DaggerfallActionDoor>();
            return door != null && !door.IsOpen && (door.IsLocked || !CanOpen || door.GetComponent<DaggerfallAction>() != null);
        }
        void Remember(Collider c)
        { if (Protected(c) && !barriers.Contains(c) && barriers.Count < 16) barriers.Add(c); }
        /// <summary>Only a closed locked (or magically held) door on the line between blocks catch-up; walking memory does not.</summary>
        bool LockedDoorTo(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            int count = Physics.RaycastNonAlloc(from + Vector3.up, delta.normalized, hits, delta.magnitude, SpotFinder.Mask, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return true;
            for (int i = 0; i < count; i++)
            {
                var door = hits[i].collider.GetComponentInParent<DaggerfallActionDoor>();
                if (door != null && door.IsLocked && !door.IsOpen) return true;
            }
            return false;
        }
        bool Ground(Vector3 from, Vector3 wanted, out Vector3 feet)
        {
            feet = wanted;
            int count = Physics.RaycastNonAlloc(wanted + Vector3.up * 0.7f, Vector3.down, hits, 1.4f, SpotFinder.Mask, QueryTriggerInteraction.Ignore);
            float closest = float.MaxValue; bool found = false;
            if (count == hits.Length) return false;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i]; if (Mine(hit.collider) || Person(hit.collider)) continue;
                if (Vector3.Angle(hit.normal, Vector3.up) > body.slopeLimit || hit.distance >= closest) continue;
                closest = hit.distance; feet = hit.point + Vector3.up * 0.03f; found = true;
            }
            return found && Mathf.Abs(feet.y - from.y) <= Mathf.Max(0.4f, body.stepOffset + 0.08f);
        }
        bool Segment(Vector3 from, Vector3 to, bool openDoors)
        {
            Vector3 delta = to - from; float distance = delta.magnitude;
            if (distance < 0.05f) return true;
            if (distance > 24) return false;
            // Ignore only passable ordinary doors while planning; interact only in immediate walking range.
            float radius = Mathf.Max(0.15f, body.radius - 0.02f);
            float height = Mathf.Min(body.height, 1.65f);
            Vector3 bottom = from + Vector3.up * (radius + 0.24f);
            Vector3 top = from + Vector3.up * Mathf.Max(radius + 0.24f, height - radius);
            // The player is the destination in normal following, but is an obstacle when approaching an enemy/NPC.
            int travelMask = currentLeader != null && currentLeader != GameManager.Instance.PlayerObject.transform ? Physics.DefaultRaycastLayers : SpotFinder.Mask;
            int count = Physics.CapsuleCastNonAlloc(bottom, top, radius, delta.normalized, hits, distance, travelMask, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return false;
            bool blocked = false;
            for (int i = 0; i < count; i++)
            {
                Collider c = hits[i].collider;
                if (Mine(c) || (currentLeader != null && (c.transform == currentLeader || c.transform.IsChildOf(currentLeader)))) continue;
                var door = c.GetComponentInParent<DaggerfallActionDoor>();
                if (door != null && !door.IsLocked && CanOpen && door.GetComponent<DaggerfallAction>() == null && (!pushing || door.IsOpen))
                {
                    if (openDoors && Vector3.Distance(Feet, c.ClosestPoint(Feet)) <= 1.8f)
                    {
                        mover.DuckForDoor();
                        if (FollowRecovery.MayOpenDoor(CanOpen, door.IsLocked, door.IsMoving, door.IsOpen, false, false))
                        { door.SetOpen(true); Status = "opening door"; }
                    }
                    continue;
                }
                if (hits[i].point.y <= from.y + body.stepOffset + 0.04f) continue;
                Remember(c); blocked = true;
            }
            if (blocked) return false;
            Vector3 ground = from;
            int samples = Mathf.CeilToInt(distance / 0.45f);
            for (int i = 1; i <= samples; i++)
            {
                Vector3 wanted = Vector3.Lerp(from, to, (float)i / samples);
                wanted.y = ground.y;
                Vector3 next; if (!Ground(ground, wanted, out next)) return false;
                ground = next;
            }
            return Mathf.Abs(ground.y - to.y) < 0.6f;
        }
        void Observe(Vector3 leader)
        {
            if (observed && Vector3.Distance(previousLeader, leader) > 24) Reset(); // teleport or floating-origin shift
            if (!observed) { previousLeader = leader; observed = true; }
            if (trail.Count == 0 || Vector3.Distance(trail[trail.Count - 1], leader) >= 0.75f)
            {
                trail.Add(leader); if (trail.Count > 128) trail.RemoveAt(0);
            }
        }
        bool Visible(Vector3 feet)
        {
            Camera camera = GameManager.Instance.MainCamera;
            if (camera == null) return true; // never teleport when visibility cannot be established
            Bounds bounds = new Bounds(feet + Vector3.up * body.height * 0.5f, new Vector3(body.radius * 2, body.height, body.radius * 2));
            if (!GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(camera), bounds)) return false;
            for (int sample = 0; sample < 5; sample++)
            {
                Vector3 target = bounds.center;
                if (sample == 1) target = feet + Vector3.up * 0.15f;
                if (sample == 2) target = feet + Vector3.up * (body.height - 0.1f);
                if (sample == 3) target += camera.transform.right * body.radius;
                if (sample == 4) target -= camera.transform.right * body.radius;
                Vector3 delta = target - camera.transform.position;
                int count = Physics.RaycastNonAlloc(camera.transform.position, delta.normalized, hits, delta.magnitude, SpotFinder.Mask, QueryTriggerInteraction.Ignore);
                if (count == hits.Length) return true;
                bool occluded = false;
                for (int i = 0; i < count; i++) if (!Mine(hits[i].collider) && !Person(hits[i].collider)) { occluded = true; break; }
                if (!occluded) return true;
            }
            return false;
        }
        bool TryCatchUp(Vector3 leader, float gap, out Vector3 destination)
        {
            destination = Vector3.zero;
            CompanionRecord companion = brain.State.companion;
            bool paid = companion != null && companion.status == "following";
            if (!paid || !recovery.Due(gap) || Time.time < nextCatchup) return false;
            nextCatchup = Time.time + 1;
            // The follower may be in view; the arrival spot must not be (CompanionNear only accepts hidden spots).
            if (!FollowRecovery.MayCatchUp(paid, brain.CurrentMode == NpcMode.Calm, GameManager.IsGamePaused, false, LockedDoorTo(Feet, leader))) return false;
            Camera camera = GameManager.Instance.MainCamera;
            Vector3 back = -Flat(camera != null ? camera.transform.forward : GameManager.Instance.PlayerObject.transform.forward).normalized;
            Vector3 feet;
            if (!SpotFinder.CompanionNear(leader, leader + back * 2.5f, out feet, delegate(Vector3 p) { return !Visible(p); })) return false;
            destination = feet + Vector3.up * (brain.transform.position.y - body.bounds.min.y);
            return true;
        }
        bool pushing;
        /// <summary>Only outward steps with floor and body clearance; never catches up or opens doors.</summary>
        public bool StepAway(Vector3 player, out Vector3 step)
        {
            step=brain.transform.position;
            Vector3 from=Feet, away=Flat(from-player).normalized;
            if(away.sqrMagnitude<0.001f) away=Flat(GameManager.Instance.PlayerObject.transform.forward).normalized;
            currentLeader=null; pushing=true;
            try
            {
                // Side steps clear a jamb; every candidate still increases distance from the player.
                for(int i=0;i<5;i++)
                {
                    float angle=i==0?0:i==1?40:i==2?-40:i==3?70:-70;
                    Vector3 wanted=from+Quaternion.Euler(0,angle,0)*away*0.6f, ground;
                    if(!Ground(from,wanted,out ground) || !Segment(from,ground,false)) continue;
                    step=ground;return true;
                }
                return false;
            }
            finally { pushing=false; }
        }
        public bool ArrivedThroughClearSpace(Transform leader)
        {
            Vector3 from = Feet + Vector3.up;
            Vector3 to = leader.position;
            int count = Physics.RaycastNonAlloc(from, (to-from).normalized, hits, Vector3.Distance(from,to), SpotFinder.Mask, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return false;
            for (int i=0; i<count; i++) if (!Mine(hits[i].collider) && !Person(hits[i].collider)) return false;
            return true;
        }
        public bool Tick(Transform leader, float dt, bool mayCatchUp, out Vector3 step, out Vector3 catchup)
        {
            step = brain.transform.position; catchup = Vector3.zero; CatchupReady = false; currentLeader = leader;
            if (GameManager.Instance.DisableAI) { Suspend(); Status = "AI disabled"; return false; }
            CharacterController leaderBody = leader.GetComponent<CharacterController>();
            Vector3 target = leader.position; if (leaderBody != null) target.y = leaderBody.bounds.min.y + 0.03f;
            Observe(target);
            float gap = Vector3.Distance(brain.transform.position, leader.position);
            recovery.Tick(gap, AdvancedNpcsMod.Instance.Config.CatchUpDistance, dt);
            previousLeader = target;
            if (mayCatchUp && TryCatchUp(target, gap, out catchup)) { Status = "catching up"; CatchupReady = true; return false; }
            if (Time.time < nextPlan && Vector3.Distance(Feet, waypoint) > 0.25f) { step = waypoint; return true; }
            nextPlan = Time.time + 0.2f;
            Vector3 from = Feet;
            Vector3 goal = target;
            bool direct = Segment(from, target, false);
            if (!direct)
            {
                // Follow a route the leader actually walked. Search a bounded number of recent samples.
                int stride = Mathf.Max(1, trail.Count / 12);
                for (int i = trail.Count - 1; i >= 0; i -= stride)
                    if (Vector3.Distance(from, trail[i]) > 0.4f && Segment(from, trail[i], false)) { goal = trail[i]; break; }
            }
            Vector3 desired = Flat(goal - from).normalized;
            barriers.RemoveAll(delegate(Collider c) { return c == null || !c.enabled || !c.gameObject.activeInHierarchy || !Protected(c); });
            foreach (Collider barrier in barriers)
            {
                if (barrier == null || !Protected(barrier)) continue;
                Vector3 towardsDoor = Flat(barrier.ClosestPoint(from) - from);
                if (towardsDoor.magnitude < 1.8f && Vector3.Dot(towardsDoor.normalized, desired) > 0.25f)
                { Status = "waiting at protected door"; return false; }
            }
            Vector3 wanted = from + desired * 0.75f;
            Vector3 ground;
            if (Ground(from, wanted, out ground) && Segment(from, ground, true))
            { waypoint = ground; step = waypoint; Status = direct ? "following clear route" : "following recent route"; return true; }
            // Keep a detour side briefly so the NPC does not alternate left/right at the same obstacle.
            if (Time.time > detourUntil) { side = (brain.Instance.Seed & 1) == 0 ? 1 : -1; detourUntil = Time.time + 3; }
            for (int pass = 0; pass < 2; pass++)
                foreach (float angle in angles)
                {
                    float turn = angle * side * (pass == 0 ? 1 : -1);
                    Vector3 direction = Quaternion.Euler(0, turn, 0) * desired;
                    wanted = from + direction * 0.75f;
                    if (!Ground(from, wanted, out ground) || !Segment(from, ground, true)) continue;
                    waypoint = ground; step = waypoint; Status = "detouring around obstacle"; return true;
                }
            Status = barriers.Count > 0 ? "waiting at protected door" : "blocked; looking for a safe route";
            return false;
        }
    }
}

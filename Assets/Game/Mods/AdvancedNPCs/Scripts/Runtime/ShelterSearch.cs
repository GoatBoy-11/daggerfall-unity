using System;
using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>Local cover only: authored spots first, then at most 65 sampled points on a clear walking segment.</summary>
    public class ShelterSearch
    {
        readonly NpcBrain brain;
        readonly NpcMover mover;
        readonly CharacterController body;
        readonly PackageSettings settings;
        readonly GameFacts facts;
        readonly RaycastHit[] hits = new RaycastHit[64];
        readonly Collider[] overlaps = new Collider[32];
        readonly HashSet<string> blocked = new HashSet<string>();
        SpotLibrary library;
        SpotChoice reservation;
        PackageMotion motion;
        float searchLeft, checkLeft, blockedLeft;
        public string Status { get; private set; }
        public bool AtShelter { get; private set; }
        public bool Running { get; private set; }
        Vector3 Feet { get { Vector3 p = brain.transform.position; p.y = body.bounds.min.y + 0.03f; return p; } }
        float Height { get { return Mathf.Max(1.6f, body.height); } }
        float Radius { get { return Mathf.Max(0.3f, body.radius); } }
        bool Mine(Collider c) { return c.transform == brain.transform || c.transform.IsChildOf(brain.transform); }

        public ShelterSearch(NpcBrain brain, NpcMover mover, PackageSettings settings, GameFacts facts)
        {
            this.brain = brain; this.mover = mover; this.settings = settings; this.facts = facts;
            body = brain.GetComponent<CharacterController>();
            Status = "awaiting shelter search";
        }

        public void Release()
        {
            if (library != null) library.Occupancy.Release(brain.Id);
            library = null; reservation = null; motion = null;
            AtShelter = Running = false; searchLeft = checkLeft = 0;
        }

        internal bool Covered(Vector3 feet)
        {
            Vector3[] offsets = { Vector3.zero, Vector3.right * Radius, Vector3.left * Radius, Vector3.forward * Radius, Vector3.back * Radius };
            foreach (Vector3 offset in offsets)
            {
                // Cast down onto roof fronts: many DFU exterior meshes have no underside triangles.
                int count = Physics.RaycastNonAlloc(feet + offset + Vector3.up * (Height + 8), Vector3.down, hits, 7.95f, SpotFinder.Mask, QueryTriggerInteraction.Ignore);
                if (count == hits.Length) return false;
                bool roof = false;
                for (int i = 0; i < count; i++)
                    if (SpotFinder.IsFixed(hits[i].collider) && hits[i].collider.GetComponentInParent<Billboard>() == null && hits[i].normal.y > 0.5f) roof = true;
                if (!roof) return false;
            }
            return true;
        }

        internal bool Reachable(Vector3 wanted, out Vector3 feet)
        {
            Vector3 anchor = Feet;
            feet = wanted;
            if (!Covered(wanted)) return false;
            if (!SpotFinder.Reachable(anchor, wanted, out feet)) return false;
            feet += Vector3.up * 0.03f;
            if (!Covered(feet)) return false;
            int count = Physics.OverlapCapsuleNonAlloc(feet + Vector3.up * Radius, feet + Vector3.up * Mathf.Max(Radius, Height - Radius), Radius,
                overlaps, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) return false;
            for (int i = 0; i < count; i++)
                if (!Mine(overlaps[i]) && (SpotFinder.IsFixed(overlaps[i]) || overlaps[i].GetComponentInParent<DaggerfallEntityBehaviour>() != null)) return false;
            Vector3 delta = feet - anchor;
            if (delta.magnitude > settings.Radius + 0.5) return false;
            // shortcut: only straight accessible routes, add a pathfinder when routes around corners are required.
            count = Physics.CapsuleCastNonAlloc(anchor + Vector3.up * (Radius + 0.3f), anchor + Vector3.up * Mathf.Max(Radius + 0.3f, Height - Radius),
                Radius, delta.normalized, hits, delta.magnitude, SpotFinder.Mask, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return false;
            for (int i = 0; i < count; i++)
                if (SpotFinder.IsFixed(hits[i].collider) && hits[i].point.y > anchor.y + 0.3f) return false;
            Vector3 ground = anchor;
            int steps = Mathf.CeilToInt(delta.magnitude / 0.75f);
            for (int i = 1; i <= steps; i++)
            {
                Vector3 sample = Vector3.Lerp(anchor, feet, (float)i / steps); sample.y = ground.y;
                count = Physics.RaycastNonAlloc(sample + Vector3.up * 0.7f, Vector3.down, hits, 1.4f, SpotFinder.Mask, QueryTriggerInteraction.Ignore);
                if (count == hits.Length) return false;
                float nearest = float.MaxValue; Vector3 next = ground;
                for (int h = 0; h < count; h++)
                    if (SpotFinder.IsFixed(hits[h].collider) && hits[h].normal.y >= Mathf.Cos(body.slopeLimit * Mathf.Deg2Rad) && hits[h].distance < nearest)
                    { nearest = hits[h].distance; next = hits[h].point + Vector3.up * 0.03f; }
                if (nearest == float.MaxValue || Mathf.Abs(next.y - ground.y) > Mathf.Max(0.4f, body.stepOffset + 0.08f)) return false;
                ground = next;
            }
            return true;
        }

        bool Claim(SpotLibrary spots, SpotChoice choice, Vector3 world)
        {
            Vector3 feet;
            if (blocked.Contains(choice.Key) || !Reachable(world, out feet) || !spots.Occupancy.TryClaim(choice.Key, brain.Id, choice.Spot.Capacity)) return false;
            choice.Position = brain.transform.parent != null ? brain.transform.parent.InverseTransformPoint(feet) : feet;
            library = spots; reservation = choice; checkLeft = 2;
            settings.Target = new PackagePoint(choice.Position.x, choice.Position.y, choice.Position.z);
            motion = new PackageMotion(settings, delegate { return 0; });
            return true;
        }

        bool Automatic(SpotLibrary spots, Vector3 world)
        {
            Vector3 local = brain.transform.parent != null ? brain.transform.parent.InverseTransformPoint(world) : world;
            string key = (brain.transform.parent != null ? brain.transform.parent.GetInstanceID() : 0) + "/shelter/" +
                Mathf.FloorToInt(local.x / 1.5f) + "/" + Mathf.FloorToInt(local.y / 1.5f) + "/" + Mathf.FloorToInt(local.z / 1.5f);
            return Claim(spots, new SpotChoice { Key = key, Spot = new NamedSpot { Id = "automatic cover" } }, world);
        }

        public bool Tick(SpotLibrary spots, Vector3 home, ulong seconds, float dt)
        {
            AtShelter = Running = false;
            if (facts.Indoors || spots == null) { Release(); Status = "shelter requires outdoors; current AI"; return false; }
            blockedLeft -= dt;
            if (blockedLeft <= 0) { blocked.Clear(); blockedLeft = 30; }
            checkLeft -= dt; searchLeft -= dt;
            if (reservation != null && checkLeft <= 0)
            {
                Vector3 ignored;
                Vector3 world = brain.transform.parent != null ? brain.transform.parent.TransformPoint(reservation.Position) : reservation.Position;
                checkLeft = 2;
                if (!Reachable(world, out ignored)) { blocked.Add(reservation.Key); Release(); searchLeft = 10; }
            }
            if (reservation == null)
            {
                if (searchLeft > 0) return false;
                searchLeft = 10;
                bool found = Automatic(spots, Feet);
                if (!found && !brain.IsTurret)
                {
                    string region = facts.Region, place = facts.Town;
                    var town = brain.GetComponentInParent<DaggerfallLocation>();
                    if (town != null) { region = town.Summary.RegionName; place = town.Summary.LocationName; }
                    int tried = 0;
                    foreach (SpotChoice choice in spots.Candidates(settings.Spots, region, place, "", brain.transform.parent != null ? brain.transform.parent.GetInstanceID() : 0, home, brain.transform.localPosition))
                    {
                        if (choice.Distance > settings.Radius * settings.Radius || tried++ >= 16) break;
                        Vector3 world = brain.transform.parent != null ? brain.transform.parent.TransformPoint(choice.Position) : choice.Position;
                        if (Claim(spots, choice, world)) { found = true; break; }
                    }
                    for (int ring = 1; ring <= 4 && !found; ring++)
                        for (int angle = 0; angle < 16 && !found; angle++)
                        {
                            float a = angle * Mathf.PI / 8;
                            Vector3 offset = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (float)settings.Radius * ring / 4;
                            found = Automatic(spots, Feet + offset);
                        }
                }
                if (!found) { Status = "no reachable shelter; current AI (retrying after 10 active seconds)"; return false; }
            }
            Vector3 current = brain.transform.parent != null ? brain.transform.parent.InverseTransformPoint(Feet) : Feet;
            PackagePoint target = motion.Tick(new PackagePoint(current.x, current.y, current.z), seconds, dt);
            if (motion.Failed) { blocked.Add(reservation.Key); Release(); searchLeft = 10; Status = "shelter route blocked; current AI (retrying)"; return false; }
            AtShelter = target == null;
            Status = (AtShelter ? "sheltered at " : "seeking shelter at ") + reservation.Spot.Id;
            if (AtShelter) mover.Stop();
            else
            {
                Running = settings.Run && brain.Instance.Definition.CanRun;
                Vector3 local = reservation.Position;
                mover.MoveToward(brain.transform.parent != null ? brain.transform.parent.TransformPoint(local) : local,
                    Running ? NpcMover.WalkSpeed * brain.Instance.Definition.RunSpeed : NpcMover.WalkSpeed);
            }
            return true;
        }
    }
}

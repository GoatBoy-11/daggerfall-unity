using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Items;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>
    /// Candidate points from DFU's markers and floor/clearance checks (outside-towns spec §5, spike findings).
    /// Candidates are returned in the parent's local space, sorted, so a place gives the same list every visit.
    /// </summary>
    public static class SpotFinder
    {
        const float PersonRadius = 0.3f;

        /// <summary>Everything but the player's own collider (the spike's ground ray hit it).</summary>
        public static int Mask
        {
            get { return ~(1 << LayerMask.NameToLayer("Player")) & Physics.DefaultRaycastLayers; }
        }

        /// <summary>Every editor marker except ladders, plus DFU's people (inactive ones too: closed buildings hide them).</summary>
        public static List<float[]> InteriorCandidates(DaggerfallInterior interior)
        {
            List<float[]> points = new List<float[]>();
            foreach (DaggerfallInterior.InteriorEditorMarker m in interior.Markers)
            {
                if (m.gameObject == null || m.type == DaggerfallInterior.InteriorMarkerTypes.LadderBottom ||
                    m.type == DaggerfallInterior.InteriorMarkerTypes.LadderTop)
                    continue;
                points.Add(Local(interior.transform, m.gameObject.transform.position));
            }
            foreach (StaticNPC npc in interior.GetComponentsInChildren<StaticNPC>(true))
                points.Add(Local(interior.transform, npc.transform.position));
            SpotPicker.Sort(points);
            return points;
        }

        /// <summary>Door centres of the interior, in world space.</summary>
        public static List<Vector3> InteriorDoors(DaggerfallInterior interior)
        {
            List<Vector3> doors = new List<Vector3>();
            foreach (DaggerfallStaticDoors coll in interior.GetComponentsInChildren<DaggerfallStaticDoors>())
            {
                foreach (StaticDoor d in coll.Doors)
                    doors.Add(coll.transform.TransformPoint(d.buildingMatrix.MultiplyPoint3x4(d.centre)));
            }
            return doors;
        }

        /// <summary>DFU's random and fixed enemies and its random treasure: always on walkable dungeon floor.</summary>
        public static List<float[]> DungeonCandidates(DaggerfallDungeon dungeon)
        {
            List<float[]> points = new List<float[]>();
            foreach (Transform t in dungeon.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "Random Enemies" && t.name != "Fixed Enemies")
                    continue;
                foreach (Transform child in t)
                    points.Add(Local(dungeon.transform, child.position));
            }
            foreach (DaggerfallLoot loot in dungeon.GetComponentsInChildren<DaggerfallLoot>(true))
            {
                if (loot.ContainerType == LootContainerTypes.RandomTreasure)
                    points.Add(Local(dungeon.transform, loot.transform.position));
            }
            SpotPicker.Sort(points);
            return points;
        }

        /// <summary>
        /// Where a person can stand near anchor (world space): the wanted point if reachable, else one of eight points
        /// 1.5 m around the anchor, else the anchor itself. Reachable = no wall between, floor within 3 m below,
        /// room for a person.
        /// </summary>
        public static bool Near(Vector3 anchor, Vector3 wanted, out Vector3 feet)
        {
            if (Reachable(anchor, wanted, out feet))
                return true;
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f * Mathf.Deg2Rad;
                if (Reachable(anchor, anchor + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 1.5f, out feet))
                    return true;
            }
            return Reachable(anchor, anchor, out feet);
        }

        static bool Reachable(Vector3 anchor, Vector3 target, out Vector3 feet)
        {
            feet = target;
            Vector3 from = anchor + Vector3.up;
            Vector3 to = target + Vector3.up;
            if ((to - from).sqrMagnitude > 0.0001f && Physics.Linecast(from, to, Mask, QueryTriggerInteraction.Ignore))
                return false;
            RaycastHit floor;
            if (!Physics.Raycast(to, Vector3.down, out floor, 3f, Mask, QueryTriggerInteraction.Ignore))
                return false;
            feet = floor.point;
            return !Physics.CheckCapsule(feet + Vector3.up * 0.5f, feet + Vector3.up * 1.6f, PersonRadius, Mask, QueryTriggerInteraction.Ignore);
        }

        static float[] Local(Transform parent, Vector3 world)
        {
            Vector3 l = parent.InverseTransformPoint(world);
            return new float[] { l.x, l.y, l.z };
        }

        public static Vector3 World(Transform parent, float[] local)
        {
            return parent.TransformPoint(new Vector3(local[0], local[1], local[2]));
        }
    }
}

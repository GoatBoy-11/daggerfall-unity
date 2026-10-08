using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Utility;
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

        // DFU editor flats (TextureReader.EditorFlatsTextureArchive) that mark monster and treasure spots (RDBLayout).
        const int RandomMonsterMarker = 15;
        const int FixedMonsterMarker = 16;
        const int RandomTreasureMarker = 19;

        /// <summary>
        /// DFU's random-monster, fixed-monster and random-treasure markers: always on walkable dungeon floor. They are
        /// laid out whether or not DFU imports enemies, so a dungeon gives the same spots on entry and after a load
        /// (a load rebuilds it without enemies and puts saved enemies back where they had moved to).
        /// </summary>
        public static List<float[]> DungeonCandidates(DaggerfallDungeon dungeon)
        {
            List<float[]> points = new List<float[]>();
            foreach (Billboard flat in dungeon.GetComponentsInChildren<Billboard>(true))
            {
                if (flat.Summary.Archive != TextureReader.EditorFlatsTextureArchive)
                    continue;
                int record = flat.Summary.Record;
                if (record == RandomMonsterMarker || record == FixedMonsterMarker || record == RandomTreasureMarker)
                    points.Add(Local(dungeon.transform, flat.transform.position));
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

        /// <summary>
        /// Only fixed geometry decides a spot: monsters, people and loot move (and after a load DFU puts them back
        /// where they had wandered to), so the same place must give the same spots whoever happens to stand there.
        /// </summary>
        static bool Reachable(Vector3 anchor, Vector3 target, out Vector3 feet)
        {
            feet = target;
            Vector3 from = anchor + Vector3.up;
            Vector3 to = target + Vector3.up;
            Vector3 along = to - from;
            if (along.sqrMagnitude > 0.0001f && AnyFixed(Physics.RaycastAll(from, along.normalized, along.magnitude, Mask, QueryTriggerInteraction.Ignore)))
                return false;
            RaycastHit floor;
            if (!NearestFixed(Physics.RaycastAll(to, Vector3.down, 3f, Mask, QueryTriggerInteraction.Ignore), out floor))
                return false;
            feet = floor.point;
            foreach (Collider c in Physics.OverlapCapsule(feet + Vector3.up * 0.5f, feet + Vector3.up * 1.6f, PersonRadius, Mask, QueryTriggerInteraction.Ignore))
            {
                if (IsFixed(c))
                    return false;
            }
            return true;
        }

        static bool IsFixed(Collider c)
        {
            return c.GetComponentInParent<DaggerfallEntityBehaviour>() == null && c.GetComponentInParent<DaggerfallLoot>() == null &&
                   c.GetComponentInParent<NpcBrain>() == null;
        }

        static bool AnyFixed(RaycastHit[] hits)
        {
            foreach (RaycastHit h in hits)
            {
                if (IsFixed(h.collider))
                    return true;
            }
            return false;
        }

        static bool NearestFixed(RaycastHit[] hits, out RaycastHit nearest)
        {
            nearest = new RaycastHit();
            bool found = false;
            foreach (RaycastHit h in hits)
            {
                if (IsFixed(h.collider) && (!found || h.distance < nearest.distance))
                {
                    nearest = h;
                    found = true;
                }
            }
            return found;
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

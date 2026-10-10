using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;

namespace AdvancedNPCs
{
    /// <summary>A saved visit counter distinguishes a real area entry from loading that same visit.</summary>
    public class NpcMagicWorld : MonoBehaviour
    {
        public string Area = "";
        public long Visit;
        public Transform AreaRoot { get; private set; }
        const string OutdoorsPrefix = "out:";
        DaggerfallLocation lastTown;

        void Update() { Refresh(); }

        public bool Refresh()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null || !gm.IsPlayingGame()) return false;
            PlayerEnterExit pee = gm.PlayerEnterExit;
            string area;
            if (pee.IsPlayerInsideDungeon)
            {
                if (pee.Dungeon == null) return false;
                AreaRoot = pee.Dungeon.transform;
                area = pee.Dungeon.Summary.ID + ":dungeon";
            }
            else if (pee.IsPlayerInsideBuilding)
            {
                if (pee.Interior == null) return false;
                AreaRoot = pee.Interior.transform;
                area = gm.PlayerGPS.CurrentMapID + ":b" + pee.Interior.EntryDoor.buildingKey;
            }
            else
            {
                // The outdoors around a town is one area: walking past its edge (a chase out of the gate) is not a new
                // visit, and town casters keep casting there. A new visit starts in another town, after a building or
                // dungeon, or once the town has been unloaded (walked far away, fast travel).
                AreaRoot = null;
                if (gm.PlayerGPS.IsPlayerInLocationRect)
                {
                    DaggerfallLocation town = gm.StreamingWorld.CurrentPlayerLocationObject;
                    if (town == null) return false;
                    lastTown = town;
                }
                else if (lastTown == null)
                    lastTown = LoadedTown(Area);
                area = lastTown != null ? OutdoorsPrefix + lastTown.Summary.MapID : "wilderness";
            }
            if (area != Area)
            {
                Area = area;
                Visit++;
                AdvancedNpcsMod.Instance.States.ForgetMagicOutside(Visit);
            }
            return true;
        }

        /// <summary>After a load outside the town's edge: the still-loaded town the saved outdoor area names, or null.</summary>
        static DaggerfallLocation LoadedTown(string area)
        {
            if (area == null || !area.StartsWith(OutdoorsPrefix)) return null;
            foreach (DaggerfallLocation town in FindObjectsOfType<DaggerfallLocation>())
                if (OutdoorsPrefix + town.Summary.MapID == area) return town;
            return null;
        }

        public bool Contains(NpcMagic magic)
        {
            if (AreaRoot != null) return magic.transform.IsChildOf(AreaRoot);
            // Outdoors: town people and wilderness people alike.
            return magic.GetComponentInParent<DaggerfallInterior>() == null &&
                magic.GetComponentInParent<DaggerfallDungeon>() == null;
        }
    }
}

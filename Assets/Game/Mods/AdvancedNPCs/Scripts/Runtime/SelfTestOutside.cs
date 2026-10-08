using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DaggerfallConnect;
using DaggerfallConnect.Utility;
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Utility;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>Self-test steps for spawning outside towns (outside-towns spec §9): a tavern, a dungeon, the wilderness.</summary>
    public partial class SelfTest
    {
        readonly List<NpcDefinition> outsideTemplates = new List<NpcDefinition>();

        IEnumerator OutsideSteps(DaggerfallLocation location)
        {
            GameManager gm = GameManager.Instance;
            PlayerEnterExit pee = gm.PlayerEnterExit;
            // The re-entry and load checks need "Same people every visit"; the player's own setting comes back afterwards.
            bool randomBefore = mod.Config.RandomEachVisit;
            mod.Config.RandomEachVisit = false;
            NpcDefinition patron = OutsideTemplate("selftest_patron",
                "{ \"baseClass\": \"Bard\", \"wanderRadius\": 0, \"spawn\": { \"interiors\": { \"buildings\": [\"Tavern\"], \"chance\": 100, \"count\": [2, 2] } } }");
            NpcDefinition bandit = OutsideTemplate("selftest_bandit",
                "{ \"baseClass\": \"Warrior\", \"wanderRadius\": 0, \"spawn\": { \"dungeons\": { \"chance\": 100, \"count\": [3, 3] }, \"wilderness\": { \"chance\": 100, \"max\": 1 } } }");
            if (patron == null || bandit == null)
            {
                mod.Config.RandomEachVisit = randomBefore;
                yield break;
            }

            // --- A tavern in this town.
            DaggerfallStaticDoors doorOwner;
            StaticDoor door;
            if (!FindBuildingDoor(location, "tavern", out doorOwner, out door))
            {
                Fail("people appear in a matching building", "no tavern in " + location.Summary.LocationName);
            }
            else
            {
                pee.TransitionInterior(doorOwner.transform, door, false);
                for (int i = 0; i < 120 && !pee.IsPlayerInsideBuilding; i++)
                    yield return null;
                yield return null;
                List<NpcBrain> patrons = WithPrefix("selftest_patron@");
                Check("people appear in a matching building (chance 100, count 2)", patrons.Count == 2,
                    Describe(patrons.ToArray()) + "; planner: " + Join(mod.Indoors.LastExplain));
                List<Vector3> doors = pee.Interior != null ? SpotFinder.InteriorDoors(pee.Interior) : new List<Vector3>();
                float doorDistance = Closest(patrons, doors);
                Check("no one appears within 2 m of a door", patrons.Count > 0 && doorDistance >= 2f,
                    "closest " + doorDistance.ToString("0.00") + " m from " + doors.Count + " door(s)");

                Dictionary<string, Vector3> first = new Dictionary<string, Vector3>();
                foreach (NpcBrain b in patrons)
                    first[b.Id] = b.transform.localPosition;
                string deadKey = patrons.Count > 0 ? patrons[0].Id : null;
                if (patrons.Count > 0)
                    PlayerHit(patrons[0], gm.PlayerEntityBehaviour, 100000);
                yield return new WaitForSecondsRealtime(0.5f);

                pee.TransitionExterior(false);
                for (int i = 0; i < 120 && pee.IsPlayerInsideBuilding; i++)
                    yield return null;
                yield return new WaitForSecondsRealtime(0.3f);
                pee.TransitionInterior(doorOwner.transform, door, false);
                for (int i = 0; i < 120 && !pee.IsPlayerInsideBuilding; i++)
                    yield return null;
                yield return null;
                List<NpcBrain> again = WithPrefix("selftest_patron@");
                bool samePlace = again.Count == 1 && first.ContainsKey(again[0].Id) &&
                                 Vector3.Distance(first[again[0].Id], again[0].transform.localPosition) < 0.05f;
                Check("same people at the same spots on re-entry; a killed one stays dead", samePlace && deadKey != null && NpcBrain.Find(deadKey) == null,
                    "before " + string.Join(", ", new List<string>(first.Keys).ToArray()) + "; now " + Describe(again.ToArray()));

                if (again.Count > 0)
                {
                    yield return LookAt(again[0].transform.position, "selftest-tavern-patron.png");
                    AdvancedNpcsMod.Log(Prefix + "LOOK tavern patron screenshot saved");
                }

                string error;
                NpcBrain placedBrain = mod.SpawnInFront(patron, mod.Catalog.Generics, out error);
                string placedKey = placedBrain != null ? placedBrain.Id : null;
                if (placedKey != null)
                    ids.Add(placedKey);
                Check("anpc_spawn places a person inside a building", placedKey != null && placedKey.Contains(":b") && mod.Placed.Find(placedKey) != null,
                    error ?? placedKey);

                // A load inside: the save data goes through DFU's serializer, then the mod restores like a real load.
                NpcSaveData data = (NpcSaveData)new NpcSaveDataInterface(mod.States, mod.Placed, mod.Flags, delegate { }).GetSaveData();
                NpcSaveData back = SaveLoadManager.Deserialize(typeof(NpcSaveData), SaveLoadManager.Serialize(typeof(NpcSaveData), data)) as NpcSaveData;
                new NpcSaveDataInterface(mod.States, mod.Placed, mod.Flags, mod.RaiseStateRestored).RestoreSaveData(back);
                yield return null;
                yield return null;
                int objects = CountObjects("selftest_patron@");
                List<NpcBrain> afterLoad = WithPrefix("selftest_patron@");
                Check("after a load inside a building its people come back once", afterLoad.Count == 2 && objects == 2,
                    "brains " + Describe(afterLoad.ToArray()) + ", objects " + objects);

                if (placedKey != null)
                    mod.RemovePlaced(placedKey);
                pee.TransitionExterior(false);
                for (int i = 0; i < 120 && pee.IsPlayerInsideBuilding; i++)
                    yield return null;
                yield return new WaitForSecondsRealtime(0.3f);
            }

            // --- A dungeon of this region.
            DFLocation dungeonLocation;
            if (!FindDungeon(out dungeonLocation))
            {
                Fail("a dungeon group appears together", "no dungeon in this region");
            }
            else
            {
                pee.StartDungeonInterior(dungeonLocation);
                for (int i = 0; i < 120 && !pee.IsPlayerInsideDungeon; i++)
                    yield return null;
                yield return null;
                List<NpcBrain> bandits = WithPrefix("selftest_bandit@");
                DaggerfallDungeon dungeon = pee.Dungeon;
                GameObject marker = dungeon != null ? (dungeon.StartMarker != null ? dungeon.StartMarker : dungeon.EnterMarker) : null;
                Vector3 entrance = marker != null ? marker.transform.position : gm.PlayerObject.transform.position;
                float spread = 0f;
                foreach (NpcBrain a in bandits)
                    foreach (NpcBrain b in bandits)
                        spread = Mathf.Max(spread, Vector3.Distance(a.transform.position, b.transform.position));
                Check("a dungeon group appears together (chance 100, count 3)", bandits.Count == 3 && spread <= 10f,
                    dungeonLocation.Name + ": " + Describe(bandits.ToArray()) + ", spread " + spread.ToString("0.0") + " m; planner: " + Join(mod.Indoors.LastExplain));
                float entranceDistance = Closest(bandits, new List<Vector3> { entrance });
                Check("no one appears within 20 m of the dungeon entrance", bandits.Count > 0 && entranceDistance >= 20f,
                    "closest " + entranceDistance.ToString("0.0") + " m");
                if (bandits.Count > 0)
                {
                    yield return LookAt(bandits[0].transform.position, "selftest-dungeon-group.png");
                    AdvancedNpcsMod.Log(Prefix + "LOOK dungeon group screenshot saved (" + dungeonLocation.Name + ")");
                }
                pee.TransitionDungeonExterior(false);
                for (int i = 0; i < 180 && pee.IsPlayerInsideDungeon; i++)
                    yield return null;
                yield return new WaitForSecondsRealtime(1f);
            }

            // --- The wilderness: travel to a nearby map pixel with no location, on land.
            DFPosition here = gm.PlayerGPS.CurrentMapPixel;
            DFPosition target;
            if (!FindWilderness(here, out target))
            {
                Fail("a wilderness encounter appears out of view 40-80 m away", "no wilderness map pixel near " + here.X + "," + here.Y);
            }
            else
            {
                gm.StreamingWorld.TeleportToCoordinates(target.X, target.Y, StreamingWorld.RepositionMethods.Origin);
                float until = Time.realtimeSinceStartup + 20f;
                yield return new WaitForSecondsRealtime(2f);
                while (Time.realtimeSinceStartup < until && (!gm.StreamingWorld.IsReady || gm.StreamingWorld.IsInit))
                    yield return null;
                yield return new WaitForSecondsRealtime(2f);
                Transform player = gm.PlayerObject.transform;
                Vector3 view = gm.MainCamera.transform.forward;
                NpcBrain w = mod.Wilderness.SpawnNear(bandit);
                float distance = w != null ? Vector3.Distance(Flat(w.transform.position), Flat(player.position)) : -1f;
                bool hidden = w != null && EncounterRules.OutOfView(player.position.x, player.position.z, view.x, view.z, w.transform.position.x, w.transform.position.z);
                Check("a wilderness encounter appears out of view 40-80 m away", w != null && distance >= 39.5f && distance <= 80.5f && hidden,
                    "in location rect " + gm.PlayerGPS.IsPlayerInLocationRect + ", distance " + distance.ToString("0.0") + ", out of view " + hidden);
                if (w != null)
                {
                    Vector3 back = -view;
                    back.y = 0;
                    w.transform.position = player.position + back.normalized * 200f;
                    yield return new WaitForSecondsRealtime(1.5f);
                }
                Check("a wilderness person far behind and out of view goes away", w == null || mod.Wilderness.Alive.Count == 0,
                    "still alive: " + mod.Wilderness.Alive.Count);

                DaggerfallUI.MessageBox("Advanced NPCs self-test");
                yield return null;
                bool blocked = !EncounterRules.MayRoll(gm.PlayerGPS.IsPlayerInLocationRect, gm.IsPlayerOnHUD);
                DaggerfallUI.UIManager.PopWindow();
                Check("no wilderness rolls while a window (rest, travel, ...) is open", blocked, "on HUD " + gm.IsPlayerOnHUD);
            }

            foreach (NpcDefinition t in outsideTemplates)
                mod.Catalog.Generics.Remove(t);
            mod.Config.RandomEachVisit = randomBefore;
        }

        NpcDefinition OutsideTemplate(string id, string json)
        {
            ParseResult r = DefinitionParser.ParseFolder(id, json);
            Check("test template " + id + " parses", r.Ok && r.Warnings.Count == 0, r.Error + " " + string.Join("; ", r.Warnings.ToArray()));
            if (!r.Ok)
                return null;
            mod.Catalog.Generics.Add(r.Definition);
            outsideTemplates.Add(r.Definition);
            return r.Definition;
        }

        List<NpcBrain> WithPrefix(string prefix)
        {
            List<NpcBrain> found = NpcBrain.All().FindAll(delegate (NpcBrain b) { return b != null && b.Id.StartsWith(prefix, StringComparison.Ordinal); });
            foreach (NpcBrain b in found)
            {
                if (!ids.Contains(b.Id))
                    ids.Add(b.Id);
            }
            return found;
        }

        static int CountObjects(string prefix)
        {
            int n = 0;
            foreach (NpcBrain b in UnityEngine.Object.FindObjectsOfType<NpcBrain>())
            {
                if (b.Id.StartsWith(prefix, StringComparison.Ordinal))
                    n++;
            }
            return n;
        }

        static float Closest(List<NpcBrain> people, List<Vector3> points)
        {
            float best = float.MaxValue;
            foreach (NpcBrain b in people)
                foreach (Vector3 p in points)
                    best = Mathf.Min(best, Vector3.Distance(Flat(b.transform.position), Flat(p)));
            return best;
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0;
            return v;
        }

        static bool FindBuildingDoor(DaggerfallLocation town, string building, out DaggerfallStaticDoors owner, out StaticDoor door)
        {
            owner = null;
            door = new StaticDoor();
            BuildingDirectory dir = town.GetComponent<BuildingDirectory>();
            if (dir == null)
                return false;
            foreach (DaggerfallStaticDoors coll in town.StaticDoorCollections)
            {
                foreach (StaticDoor d in coll.Doors)
                {
                    BuildingSummary s;
                    if (d.doorType == DoorTypes.Building && dir.GetBuildingSummary(d.buildingKey, out s) &&
                        BuildingKinds.FromBuildingType(s.BuildingType.ToString()) == building)
                    {
                        owner = coll;
                        door = d;
                        return true;
                    }
                }
            }
            return false;
        }

        static bool FindDungeon(out DFLocation found)
        {
            found = new DFLocation();
            PlayerGPS gps = GameManager.Instance.PlayerGPS;
            DFRegion region = gps.CurrentRegion;
            for (int i = 0; i < region.MapTable.Length; i++)
            {
                if (region.MapTable[i].DungeonType == DFRegion.DungeonTypes.NoDungeon)
                    continue;
                DFLocation loc;
                if (DaggerfallUnity.Instance.ContentReader.GetLocation(gps.CurrentRegionIndex, i, out loc) && loc.HasDungeon)
                {
                    found = loc;
                    return true;
                }
            }
            return false;
        }

        static bool FindWilderness(DFPosition from, out DFPosition target)
        {
            target = from;
            ContentReader reader = DaggerfallUnity.Instance.ContentReader;
            int[][] offsets = { new[] { 0, -3 }, new[] { 3, 0 }, new[] { -3, 0 }, new[] { 0, 3 }, new[] { 3, -3 }, new[] { -3, -3 }, new[] { 4, 4 }, new[] { -4, 4 } };
            foreach (int[] o in offsets)
            {
                int x = from.X + o[0];
                int y = from.Y + o[1];
                if (reader.HasLocation(x, y) || reader.MapFileReader.GetClimateIndex(x, y) == (int)MapsFile.Climates.Ocean)
                    continue;
                target = new DFPosition(x, y);
                return true;
            }
            return false;
        }

        /// <summary>Puts the camera 4 m from a point, facing it, then saves a screenshot next to Player.log (developer look).</summary>
        IEnumerator LookAt(Vector3 point, string file)
        {
            GameManager gm = GameManager.Instance;
            Transform player = gm.PlayerObject.transform;
            Vector3 back = Flat(player.position - point);
            back = back.sqrMagnitude > 0.01f ? back.normalized : Vector3.forward;
            Vector3 stand = point + back * 4f;
            RaycastHit wall;
            if (Physics.Linecast(point + Vector3.up, stand + Vector3.up, out wall, SpotFinder.Mask, QueryTriggerInteraction.Ignore))
                stand = wall.point - back * 0.5f - Vector3.up;
            CharacterController controller = gm.PlayerObject.GetComponent<CharacterController>();
            if (controller != null)
                controller.enabled = false;
            player.position = stand + Vector3.up * 1f;
            if (controller != null)
                controller.enabled = true;
            gm.PlayerMouseLook.SetFacing(-back);
            yield return new WaitForSecondsRealtime(0.7f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(Application.persistentDataPath, file));
            yield return new WaitForSecondsRealtime(0.5f);
        }
    }
}

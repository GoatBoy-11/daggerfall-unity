using System.Collections;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Weather;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    public partial class SelfTest
    {
        IEnumerator ShelterSteps(DaggerfallLocation location, Transform player)
        {
            DiscardTestPeople();
            var geometry = new GameObject("ANPC shelter verification");
            geometry.transform.SetParent(location.transform, false);
            Vector3 origin = player.position + Vector3.up * 24;
            var floor = NavigationBox(geometry.transform, "shelter floor", origin - Vector3.up * 0.15f, new Vector3(48, 0.3f, 48));
            NpcBrain actor = null;
            ShelterSearch probe = null;
            WeatherManager weather = GameManager.Instance.WeatherManager;
            WeatherType previousWeather = weather.PlayerWeather.WeatherType;
            bool previousPolling = weather.UpdateWeatherFromClimateArray;
            string error;
            AiPackage package = PackageParser.Parse("selftest_shelter_package", "{\"type\":\"seek_shelter\"}", out error);
            AiPackage stand = PackageParser.Parse("selftest_shelter_stand", "{\"type\":\"stand\"}", out error);
            mod.Packages.AddForTest(package); mod.Packages.AddForTest(stand);
            const string spotId = "selftest_shelter_post";
            try
            {
                weather.UpdateWeatherFromClimateArray = false;
                var definition = TestDefinition("selftest_shelter", Bravery.Prudent, location, player, OutOfGreeting);
                definition.BaseClass = "Warrior"; definition.WanderRadius = 0;
                definition.Schedule.Add(new AiScheduleEntry { Package = stand.Id });
                actor = SpawnDefinition(definition, location);
                yield return Settle;
                NavigationActorAt(actor, origin);
                yield return Settle;
                var facts = new GameFacts(actor.State, mod.Flags, actor.DisplayName, null, definition, actor, true);
                probe = new ShelterSearch(actor, actor.GetComponent<NpcMover>(), PackageParser.Resolve(package, null, out error), facts);
                Vector3 target = origin + Vector3.right * 6;
                Vector3 found;
                Check("shelter rejects open sky", !probe.Covered(origin), origin.ToString());
                var roof = NavigationBox(geometry.transform, "shelter roof", target + Vector3.up * 4, new Vector3(6, 0.3f, 6));
                yield return null;
                Check("shelter detects a broad solid roof and accessible floor", probe.Reachable(target, out found), "feet=" + found);
                var wall = NavigationBox(geometry.transform, "shelter blocking wall", origin + new Vector3(3, 2, 0), new Vector3(0.4f, 4, 8));
                yield return null;
                Check("shelter rejects cover behind a wall", !probe.Reachable(target, out found), target.ToString());
                UnityEngine.Object.Destroy(wall); yield return null;
                roof.transform.localScale = new Vector3(0.2f, 0.3f, 6);
                yield return null;
                Check("shelter requires cover across the NPC width", !probe.Covered(target), target.ToString());
                roof.transform.localScale = new Vector3(6, 0.3f, 6);
                roof.transform.position = target + Vector3.up;
                yield return null;
                Check("shelter rejects insufficient headroom", !probe.Reachable(target, out found), target.ToString());
                roof.transform.position = target + Vector3.up * 4;
                roof.GetComponent<Collider>().isTrigger = true;
                yield return null;
                Check("shelter ignores trigger-only cover", !probe.Covered(target), target.ToString());
                roof.GetComponent<Collider>().isTrigger = false;
                floor.GetComponent<Collider>().enabled = false;
                yield return null;
                Check("shelter rejects cover without reachable ground", !probe.Reachable(target, out found), target.ToString());
                floor.GetComponent<Collider>().enabled = true;
                roof.SetActive(false); yield return null;
                Check("no shelter releases control to ordinary AI", !probe.Tick(mod.Spots, actor.HomeLocal, 0, 0) && probe.Status.Contains("no reachable shelter"), probe.Status);
                probe.Release(); roof.SetActive(true); yield return null;
                Check("automatic shelter search finds nearby cover", probe.Tick(mod.Spots, actor.HomeLocal, 0, 0) && probe.Status.Contains("automatic cover"), probe.Status);
                probe.Release();

                Vector3 local = location.transform.InverseTransformPoint(target);
                var spot = new NamedSpot { Id = spotId, RelativeTo = "area", Region = location.Summary.RegionName, Place = location.Summary.LocationName,
                    Position = new PackagePoint(local.x, local.y, local.z), Capacity = 1 };
                spot.Tags.Add("shelter"); mod.Spots.AddForTest(spot);
                Check("shelter prefers a matching authored spot", probe.Tick(mod.Spots, actor.HomeLocal, 0, 0) && probe.Status.Contains(spotId), probe.Status);
                probe.Release();
                var choices = mod.Spots.Candidates(new[] { spotId }, spot.Region, spot.Place, "", location.transform.GetInstanceID(), actor.HomeLocal, actor.transform.localPosition);
                mod.Spots.Occupancy.TryClaim(choices[0].Key, "selftest_other_shelter_user", 1);
                Check("occupied authored shelter uses automatic alternatives", probe.Tick(mod.Spots, actor.HomeLocal, 0, 0) && probe.Status.Contains("automatic cover"), probe.Status);
                probe.Release(); mod.Spots.Occupancy.Release("selftest_other_shelter_user");

                definition.Schedule = AiSchedule.Read(Json.Parse("[{\"package\":\"" + package.Id + "\",\"when\":{\"outdoors\":true,\"bad_weather\":true}},{\"package\":\"" + stand.Id + "\"}]"), out error);
                weather.SetWeather(WeatherType.Rain);
                yield return new WaitForSeconds(6);
                Check("rain activates shelter package and NPC walks to its post", actor.PackageDescription.Contains("sheltered at " + spotId) &&
                    Vector3.Distance(actor.transform.position, target + Vector3.up) < 2, actor.Describe());
                Check("shelter reserves its authored post", mod.Spots.Occupancy.Count(choices[0].Key) == 1, actor.PackageDescription);
                actor.SwitchHostile(true); yield return Settle;
                Check("combat interrupts shelter and releases its post", mod.Spots.Occupancy.Count(choices[0].Key) == 0 && actor.CurrentMode != NpcMode.Calm, actor.Describe());
                actor.SwitchHostile(false); yield return Settle;
                Check("calm NPC shelters again", actor.PackageDescription.Contains("sheltered at"), actor.Describe());
                weather.SetWeather(WeatherType.Sunny); yield return Settle;
                Check("clearing weather restores the ordinary scheduled package", actor.PackageDescription.Contains(stand.Id) && mod.Spots.Occupancy.Count(choices[0].Key) == 0, actor.Describe());
                Check("bad weather condition excludes overcast alone", !ConditionParser.Parse((System.Collections.Generic.Dictionary<string, object>)Json.Parse("{\"bad_weather\":true}"), "test", new System.Collections.Generic.List<string>(), false).Holds(facts), "clear weather");

                // Probe real exterior meshes independently of the artificial platform above them.
                Vector3 townFeet = player.position; townFeet.y = player.GetComponent<CharacterController>().bounds.min.y + 0.03f;
                bool realCover = false; Vector3 actual = townFeet;
                for (int x = -32; x <= 32 && !realCover; x += 2)
                    for (int z = -32; z <= 32 && !realCover; z += 2)
                    {
                        Vector3 candidate = townFeet + new Vector3(x, 0, z);
                        if (!probe.Covered(candidate) || !SpotFinder.Reachable(candidate, candidate, out found)) continue;
                        NavigationActorAt(actor, found);
                        realCover = probe.Reachable(found, out actual);
                    }
                Check("shelter detection works on actual town exterior geometry", realCover, "town=" + location.Summary.LocationName + "; feet=" + actual);
            }
            finally
            {
                if (probe != null) probe.Release();
                NpcBrain.Discard(actor);
                mod.Spots.Occupancy.Release("selftest_other_shelter_user"); mod.Spots.RemoveForTest(spotId);
                mod.Packages.RemoveForTest(package.Id); mod.Packages.RemoveForTest(stand.Id);
                weather.SetWeather(previousWeather); weather.UpdateWeatherFromClimateArray = previousPolling;
                UnityEngine.Object.Destroy(geometry);
            }
        }
    }
}

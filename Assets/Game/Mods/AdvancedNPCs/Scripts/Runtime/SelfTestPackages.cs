using System.Collections;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    public partial class SelfTest
    {
        public const string PackageSuite = "packages";

        void CheckScheduleAreaFacts(NpcBrain actor)
        {
            var facts = new GameFacts(actor.State,mod.Flags,actor.DisplayName,null,actor.Instance.Definition,actor);
            string error;
            var entries = AiSchedule.Read(Json.Parse("[{\"package\":\"wander_civilian\",\"when\":{\"outdoors\":true}},{\"package\":\"stand\",\"when\":{\"indoors\":true}}]"),out error);
            Transform parent = actor.transform.parent;
            GameObject building = new GameObject("selftest_condition_building"), dungeon = new GameObject("selftest_condition_dungeon");
            building.AddComponent<DaggerfallInterior>().enabled = false;
            dungeon.AddComponent<DaggerfallDungeon>().enabled = false;
            try
            {
                Check("outdoor schedule condition selects civilian wandering in town", !facts.Indoors && AiSchedule.Select(entries,0,facts).Package=="wander_civilian",error ?? actor.Describe());
                actor.transform.SetParent(building.transform,true);
                Check("building condition follows NPC area while player stays outside", facts.Indoors && !GameManager.Instance.PlayerEnterExit.IsPlayerInside && AiSchedule.Select(entries,0,facts).Package=="stand",actor.Id);
                actor.transform.SetParent(dungeon.transform,true);
                Check("dungeon condition counts as indoors", facts.Indoors && AiSchedule.Select(entries,0,facts).Package=="stand",actor.Id);
                actor.transform.SetParent(null,true);
                Check("wilderness condition counts as outdoors without a town parent", !facts.Indoors && AiSchedule.Select(entries,0,facts).Package=="wander_civilian",actor.Id);
            }
            finally
            {
                actor.transform.SetParent(parent,true);
                UnityEngine.Object.Destroy(building); UnityEngine.Object.Destroy(dungeon);
            }
        }

        IEnumerator CivilianPackageSteps(DaggerfallLocation location, Transform player)
        {
            DiscardTestPeople();
            string error;
            AiPackage package = PackageParser.Parse("selftest_civilian", "{\"type\":\"wander_civilian\"}", out error);
            mod.Packages.AddForTest(package);
            mod.Sprites.Add("selftest_civilian", TestSpriteSet(false, "idle_1", "idle_2", "pray_1", "play_2", "emote_3", "dance_4", "walk", "hit", "attack", "greet", "talk", "cast"));
            try
            {
                NpcDefinition d = TestDefinition("selftest_civilian", Bravery.Brave, location, player, OutOfGreeting);
                d.BaseClass = "Warrior";
                d.Schedule.Add(new AiScheduleEntry {Package=package.Id, Parameters=new System.Collections.Generic.Dictionary<string,object> {{"radius",0.0}}});
                var requests = mod.Packages.AnimationRequests(d);
                bool allFamilies = true;
                foreach (string family in PackageAnimations.CivilianFamilies) allFamilies &= requests.Contains(family);
                Check("civilian schedules request every activity family for sprite loading", allFamilies, string.Join(",",requests.ToArray()));
                NpcBrain actor = SpawnDefinition(d, location);
                yield return Settle;
                CheckScheduleAreaFacts(actor);
                NpcSprite sprite = actor.GetComponent<NpcSprite>();
                Vector3 home = actor.transform.localPosition;
                bool idle = false, activity = false;
                int cycles = sprite.PackageCycles;
                float until = Time.time + 4;
                while (Time.time < until)
                {
                    if (sprite.CurrentState == NpcSprite.PackageState)
                    {
                        idle |= PackageAnimations.Matches("idle",sprite.CurrentAnimation);
                        activity |= !PackageAnimations.Matches("idle",sprite.CurrentAnimation);
                    }
                    yield return null;
                }
                Check("civilian stops animate complete idle or activity cycles", (idle || activity) && sprite.PackageCycles > cycles,
                    "idle="+idle+", activity="+activity+", cycles="+(sprite.PackageCycles-cycles));
                Check("zero-radius civilian stays at home", Vector3.Distance(home,actor.transform.localPosition)<0.25f,actor.Describe());
                cycles = sprite.PackageCycles;
                actor.HoldStill(0.6f);
                yield return new WaitForSeconds(0.2f);
                Check("civilian animation yields to ordinary holds without advancing cycles", !actor.CivilianAnimation && sprite.CurrentState!=NpcSprite.PackageState && sprite.PackageCycles==cycles,sprite.CurrentState);
                yield return new WaitForSeconds(0.6f);
                Check("civilian animation resumes after hold", actor.CivilianAnimation && sprite.CurrentState==NpcSprite.PackageState,actor.Describe());
                actor.HoldStill(0.5f);
                bool greeted = sprite.PlayGreet();
                yield return new WaitForSeconds(0.1f);
                Check("civilian animation yields to greet", greeted && sprite.CurrentState==SpriteStates.Greet,sprite.CurrentState);
                yield return new WaitForSeconds(0.6f);
                actor.HoldStill(0.5f); sprite.Talk(0.5f);
                yield return new WaitForSeconds(0.1f);
                Check("civilian animation yields to talk", sprite.CurrentState==SpriteStates.Talk,sprite.CurrentState);
                yield return new WaitForSeconds(0.6f);
                actor.HoldStill(0.5f); sprite.PlayCast(true);
                yield return new WaitForSeconds(0.1f);
                Check("civilian animation yields to healing", sprite.CurrentState==SpriteStates.Heal || sprite.CurrentState==SpriteStates.Cast,sprite.CurrentState);
                yield return new WaitForSeconds(0.6f);
                PlayerHit(actor,GameManager.Instance.PlayerEntityBehaviour,1);
                yield return null; yield return null;
                Check("hurt and combat interrupt civilian routines", !actor.CivilianAnimation && actor.CurrentMode!=NpcMode.Calm && sprite.CurrentState!=NpcSprite.PackageState,actor.Describe());
                actor.SwitchHostile(false);
                yield return new WaitForSeconds(0.8f);
                Check("civilian resumes when combat ends", actor.CurrentMode==NpcMode.Calm && actor.CivilianAnimation,actor.Describe());
                d.Schedule.Clear();
                yield return Settle;
                Check("ending civilian schedule releases its animation", !actor.CivilianAnimation && sprite.CurrentState!=NpcSprite.PackageState,actor.Describe());
                NpcBrain.Discard(actor);

                mod.Sprites.Remove("selftest_civilian");
                mod.Sprites.Add("selftest_civilian", TestSpriteSet(false,"idle_1","idle_2","walk"));
                d.Schedule.Add(new AiScheduleEntry {Package=package.Id, Parameters=new System.Collections.Generic.Dictionary<string,object> {{"radius",0.0}}});
                actor=SpawnDefinition(d,location); yield return Settle;
                sprite=actor.GetComponent<NpcSprite>();
                yield return new WaitForSeconds(1f);
                Check("civilian with no activity sheets falls back to idle", sprite.CurrentState==NpcSprite.PackageState && PackageAnimations.Matches("idle",sprite.CurrentAnimation),actor.Describe());

                var grid=location.GetComponent<DaggerfallWorkshop.Game.Utility.CityNavigation>();
                var origin=grid.WorldToNavGridPosition(grid.SceneToWorldPosition(player.position));
                DaggerfallConnect.Utility.DFPosition tile;
                bool found=grid.GetRandomSpawnPosition(origin,out tile,12,100);
                Check("civilian navigation test finds a walkable town tile",found,"walkable tile="+found);
                if(found)
                {
                    actor.Teleport(grid.WorldToScenePosition(grid.NavGridToWorldPosition(tile))+Vector3.up);
                    d.Schedule[0]=new AiScheduleEntry {Package=package.Id};
                    yield return Settle;
                    home=actor.transform.localPosition;
                    float farthest=0; until=Time.time+15;
                    while(Time.time<until)
                    {
                        Vector3 gap=actor.transform.localPosition-home;gap.y=0;
                        farthest=Mathf.Max(farthest,gap.magnitude);
                        yield return null;
                    }
                    Check("civilian walks the town grid within its larger radius",farthest>3 && farthest<=40.5f && !actor.PackageDescription.Contains("current AI:"),"farthest="+farthest+"; "+actor.Describe());
                }
                NpcBrain.Discard(actor);
                d.Folder = "selftest_civilian_vanilla";
                d.Schedule[0] = new AiScheduleEntry {Package=package.Id, Parameters=new System.Collections.Generic.Dictionary<string,object> {{"radius",0.0}}};
                actor = SpawnDefinition(d,location);
                yield return Settle;
                Check("civilian without custom sheets uses an ordinary idle hold", actor.GetComponent<NpcSprite>()==null && actor.CivilianAnimation,actor.Describe());
                NpcBrain.Discard(actor);
            }
            finally
            {
                mod.Packages.RemoveForTest(package.Id);
                mod.Sprites.Remove("selftest_civilian");
            }
        }

        IEnumerator PackageSteps(DaggerfallLocation location, Transform player)
        {
            IEnumerator shelters = ShelterSteps(location, player);
            while (shelters.MoveNext()) yield return shelters.Current;
            IEnumerator civilians = CivilianPackageSteps(location, player);
            while (civilians.MoveNext()) yield return civilians.Current;
            DiscardTestPeople();
            string error;
            AiPackage stand = PackageParser.Parse("selftest_pkg_stand", "{\"type\":\"stand\"}", out error);
            Vector3 direction = OpenDirection(player, 16f);
            Vector3 localDirection = location.transform.InverseTransformDirection(direction);
            string target = "[" + FieldReader.Num(localDirection.x * 4) + ",0," + FieldReader.Num(localDirection.z * 4) + "]";
            AiPackage travel = PackageParser.Parse("selftest_pkg_travel",
                "{\"type\":\"travel_to\",\"parameters\":{\"target\":" + target + "}}", out error);
            mod.Packages.AddForTest(stand);
            mod.Packages.AddForTest(travel);
            try
            {
                NpcDefinition d = TestDefinition("selftest_packages", Bravery.Prudent, location, player, OutOfGreeting);
                d.BaseClass = "Warrior";
                d.WanderRadius = 8;
                d.Schedule.Add(new AiScheduleEntry { Package = stand.Id });
                NpcBrain brain = SpawnDefinition(d, location);
                yield return Settle;
                brain.Teleport(player.position + direction * 7f);
                yield return Settle;
                Vector3 home = brain.transform.position;
                yield return new WaitForSeconds(1f);
                Check("stand package owns routine movement", Vector3.Distance(home, brain.transform.position) < 0.2f &&
                    brain.PackageDescription.Contains(stand.Id), brain.PackageDescription);

                brain.Leash = 0.3f;
                d.WanderRadius = 0;
                d.Schedule[0] = new AiScheduleEntry { Package = travel.Id };
                yield return new WaitForSeconds(0.4f);
                brain.HoldStill(1f);
                Vector3 held = brain.transform.position;
                yield return new WaitForSeconds(0.5f);
                Check("package respects greeting/casting holds", Vector3.Distance(held, brain.transform.position) < 0.25f, brain.PackageDescription);
                yield return new WaitForSeconds(4f);
                float moved = Vector3.Distance(home, brain.transform.position);
                Check("travel package overrides wander and leash", moved > 2f, "moved=" + moved + "; " + brain.PackageDescription);
                Check("package leaves default settings untouched", d.WanderRadius == 0 && brain.Leash == 0.3f, "radius=" + d.WanderRadius + ", leash=" + brain.Leash);
                Check("anpc_info exposes package", brain.Describe().Contains("package: slot 1: " + travel.Id), brain.Describe());

                // A missing selected package must release to the old leash instead of disabling this NPC.
                d.Schedule[0] = new AiScheduleEntry { Package = "selftest_missing_package" };
                yield return new WaitForSeconds(4f);
                Check("missing package restores original leash AI", Vector3.Distance(home, brain.transform.position) < 1f &&
                    brain.PackageDescription.Contains("current AI"), brain.PackageDescription);

                d.Schedule[0] = new AiScheduleEntry { Package = stand.Id };
                yield return Settle;
                brain.SwitchHostile(true);
                yield return Settle;
                Check("combat interrupts packages", brain.CurrentMode != NpcMode.Calm && !brain.PackageRunning, brain.Status);
                brain.SwitchHostile(false);
                yield return Settle;
                Check("calm resumes current package", brain.CurrentMode == NpcMode.Calm && brain.PackageDescription.Contains(stand.Id), brain.Status);
                NpcBrain.Discard(brain);

                NpcBrain ordinary = Make("selftest_packages_ordinary", Bravery.Normal, location, player, OutOfGreeting);
                yield return Settle;
                Check("NPC without schedule retains current AI", ordinary.PackageDescription == "current AI (no schedule)", ordinary.PackageDescription);
                IEnumerator tools = ScheduleToolSteps(ordinary, stand, location, player);
                while (tools.MoveNext()) yield return tools.Current;
                NpcBrain.Discard(ordinary);
            }
            finally
            {
                mod.Packages.RemoveForTest(stand.Id);
                mod.Packages.RemoveForTest(travel.Id);
            }
            IEnumerator animations = AnimationPackageSteps(location, player);
            while (animations.MoveNext())
                yield return animations.Current;
            IEnumerator guards = GuardPackageSteps(location, player);
            while (guards.MoveNext())
                yield return guards.Current;
            IEnumerator followers = VariableFollowSteps(location, player);
            while (followers.MoveNext())
                yield return followers.Current;
            IEnumerator routines = RoutineSteps(location, player);
            while (routines.MoveNext())
                yield return routines.Current;
        }

        IEnumerator ScheduleToolSteps(NpcBrain actor, AiPackage stand, DaggerfallLocation location, Transform player)
        {
            NpcDefinition definition = actor.Instance.Definition;
            string directory = System.IO.Path.Combine(AnpcFiles.Root, definition.Folder);
            if (System.IO.Directory.Exists(directory))
            {
                Check("schedule tools use an unused temporary folder", false, directory);
                yield break;
            }
            string path = System.IO.Path.Combine(directory, AnpcFiles.NpcFile);
            mod.Catalog.Generics.Add(definition);
            try
            {
                System.IO.Directory.CreateDirectory(directory);
                NpcState state = actor.State;
                System.IO.File.WriteAllText(path, "{\"schedule\":[{\"package\":\"" + stand.Id + "\"}]}");
                string result = AdvancedNpcsMod.ReloadSchedulesCommand(definition.Folder);
                yield return Settle;
                Check("schedule reload activates the first routine without respawning", result.Contains("Reloaded 1") &&
                    actor.PackageDescription.Contains(stand.Id) && object.ReferenceEquals(state, actor.State), result + "; " + actor.Describe());
                Check("schedule diagnostic reports selected slot and current activity", AdvancedNpcsMod.ScheduleCommand(actor.Id).Contains(stand.Id + " - selected"), actor.PackageDescription);
                var previous = definition.Schedule;
                System.IO.File.WriteAllText(path, "{\"schedule\":[{\"package\":\"selftest_missing_package\"}]}");
                result = AdvancedNpcsMod.ReloadSchedulesCommand(definition.Folder);
                yield return Settle;
                Check("invalid schedule replacement leaves the running routine intact", result.Contains("previous schedule retained") &&
                    object.ReferenceEquals(previous, definition.Schedule) && actor.PackageDescription.Contains(stand.Id), result);
                System.IO.File.Delete(path);
                result = AdvancedNpcsMod.ReloadSchedulesCommand(definition.Folder);
                Check("missing schedule file retains the previous routine", result.Contains("previous schedule retained") && object.ReferenceEquals(previous, definition.Schedule), result);
                CheckBlockedRouteRetries(actor);

                result = AdvancedNpcsMod.RouteCommand("start", actor.Id);
                Check("route recorder captures the first waypoint", result.Contains("Recorded point 1"), result);
                Check("route recorder requires two points", AdvancedNpcsMod.RouteCommand("finish").Contains("at least two"), "single point rejected");
                Check("route recorder does not overwrite unfinished work", AdvancedNpcsMod.RouteCommand("start", actor.Id).Contains("already being recorded"), "recording retained");
                result = AdvancedNpcsMod.RouteCommand("add");
                string json = AdvancedNpcsMod.RouteCommand("finish");
                string error;
                AiPackage recorded = PackageParser.Parse("recorded_patrol", json, out error);
                Check("route recorder exports valid patrol JSON", result.Contains("Recorded point 2") && recorded != null, error ?? json);
                if (recorded != null)
                {
                    PackageSettings settings = PackageParser.Resolve(recorded, null, out error);
                    Vector3 expected = location.transform.InverseTransformPoint(player.position) - actor.HomeLocal;
                    Check("recorded points are offsets from the NPC home", settings.Points.Count == 2 &&
                        System.Math.Abs(settings.Points[0].X - expected.x) < 0.01 && System.Math.Abs(settings.Points[0].Z - expected.z) < 0.01, json);
                }
                Check("finished recording releases its temporary state", AdvancedNpcsMod.RouteCommand("add").Contains("Start a route"), "no active recording");

                System.IO.File.WriteAllText(path, "{\"schedule\":[]}");
                result = AdvancedNpcsMod.ReloadSchedulesCommand(definition.Folder);
                yield return Settle;
                Check("schedule reload can remove the last routine", result.Contains("Reloaded 1") && actor.PackageDescription == "current AI (no schedule)" && object.ReferenceEquals(state, actor.State), result + "; " + actor.PackageDescription);
            }
            finally
            {
                AdvancedNpcsMod.RouteCommand("cancel");
                mod.Catalog.Generics.Remove(definition);
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
                if (System.IO.Directory.Exists(directory)) System.IO.Directory.Delete(directory);
            }
        }

        void CheckBlockedRouteRetries(NpcBrain actor)
        {
            Vector3 offset = actor.transform.localPosition - actor.HomeLocal;
            string error;
            AiPackage blocked = PackageParser.Parse("selftest_blocked_route", "{\"type\":\"travel_to\",\"parameters\":{\"target\":[" +
                FieldReader.Num(offset.x) + ",100," + FieldReader.Num(offset.z) + "]}}", out error);
            mod.Packages.AddForTest(blocked);
            var previous = actor.Instance.Definition.Schedule;
            var driver = new NpcPackageDriver(actor, actor.GetComponent<NpcMover>());
            try
            {
                actor.Instance.Definition.Schedule = new System.Collections.Generic.List<AiScheduleEntry> { new AiScheduleEntry { Package = blocked.Id } };
                ulong seconds = DaggerfallUnity.Instance.WorldTime.Now.ToSeconds();
                driver.Select(mod.Packages, seconds);
                driver.Tick(actor.HomeLocal, seconds, 0);
                bool blockedFirst = !driver.Tick(actor.HomeLocal, seconds, 10) && driver.Description.Contains("retry 1/2");
                bool waited = !driver.Tick(actor.HomeLocal, seconds, 14);
                bool retried = driver.Tick(actor.HomeLocal, seconds, 1) && driver.Description.Contains("retrying blocked target (1/2)");
                Check("blocked coordinate package yields during delay and retries", blockedFirst && waited && retried, driver.Description);
                driver.Tick(actor.HomeLocal, seconds, 10);
                bool second = driver.Tick(actor.HomeLocal, seconds, 15) && driver.Description.Contains("retrying blocked target (2/2)");
                bool exhausted = !driver.Tick(actor.HomeLocal, seconds, 10) && driver.Description.Contains("2 retries exhausted");
                Check("coordinate retries stop after two attempts", second && exhausted && !driver.Tick(actor.HomeLocal, seconds, 100), driver.Description);
            }
            finally
            {
                driver.Dispose();
                actor.Instance.Definition.Schedule = previous;
                mod.Packages.RemoveForTest(blocked.Id);
            }
        }
    }
}

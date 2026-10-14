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

        IEnumerator PackageSteps(DaggerfallLocation location, Transform player)
        {
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
    }
}

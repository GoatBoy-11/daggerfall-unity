using System.Collections;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>Self-test: running, turrets and greetings (spec run/greet/turret §7).</summary>
    public partial class SelfTest
    {
        IEnumerator GaitSteps(DaggerfallLocation location, Transform player)
        {
            DaggerfallEntityBehaviour playerBehaviour = GameManager.Instance.PlayerEntityBehaviour;
            // Every test ANPC stands along one open line from the player: in sight, on ground, nothing in between.
            Vector3 open = OpenDirection(player, 16f);
            mod.Sprites.Add("selftest_runner", TestSpriteSet(false, "idle", "walk", "run", "attack", "death"));
            mod.Sprites.Add("selftest_turret", TestSpriteSet(false, "idle", "attack", "death"));

            // A hostile runner far away runs (gait, sheet, boost); near the player it walks.
            NpcDefinition runnerDef = TestDefinition("selftest_runner", Bravery.Brave, location, player, 0f);
            runnerDef.BaseClass = "Warrior";
            NpcBrain runner = SpawnDefinition(runnerDef, location);
            yield return Settle;
            runner.Teleport(player.position + open * 14f);
            runner.SwitchHostile(true);
            NpcGait runnerGait = runner.GetComponent<NpcGait>();
            NpcSprite runnerSprite = runner.GetComponent<NpcSprite>();
            bool sawRun = false, sawRunSheet = false;
            float until = Time.time + 3f;
            while (Time.time < until && !(sawRun && sawRunSheet))
            {
                sawRun |= runnerGait != null && runnerGait.Current == Gait.Run;
                sawRunSheet |= runnerSprite != null && runnerSprite.CurrentAnimation == "run";
                yield return null;
            }
            Check("far hostile ANPC runs with its run sheet and a speed boost", sawRun && sawRunSheet && runnerGait.Boosted > 0f,
                "run=" + sawRun + ", sheet=" + (runnerSprite != null ? runnerSprite.CurrentAnimation : "none") +
                ", boosted=" + (runnerGait != null ? runnerGait.Boosted : -1f) + ", " + runner.Status);
            until = Time.time + 6f;
            string trace = "";
            float nextTrace = 0f;
            EnemySenses runnerSenses = runner.GetComponent<EnemySenses>();
            while (Time.time < until && Vector3.Distance(runner.transform.position, player.position) > 2.5f)
            {
                if (Time.time >= nextTrace)
                {
                    nextTrace = Time.time + 0.5f;
                    trace += " [" + Vector3.Distance(runner.transform.position, player.position).ToString("0.0") + "m sight=" +
                        runnerSenses.TargetInSight + " target=" + (runnerSenses.Target == playerBehaviour ? "player" : runnerSenses.Target != null ? runnerSenses.Target.name : "none") +
                        " " + runnerGait.Current + "]";
                }
                yield return null;
            }
            yield return new WaitForSeconds(0.3f);
            Check("near the player it no longer runs", runnerGait != null && runnerGait.Current != Gait.Run,
                "gait=" + (runnerGait != null ? runnerGait.Current.ToString() : "none") + ", distance=" +
                Vector3.Distance(runner.transform.position, player.position) + trace);
            runner.SwitchHostile(false);
            NpcBrain.Discard(runner);

            // canRun false: far away it still walks.
            NpcDefinition walkerDef = TestDefinition("selftest_walker", Bravery.Brave, location, player, 0f);
            walkerDef.Folder = "selftest_runner";
            walkerDef.CanRun = false;
            NpcBrain walker = SpawnDefinition(walkerDef, location);
            yield return Settle;
            walker.Teleport(player.position + open * 14f);
            walker.SwitchHostile(true);
            NpcGait walkerGait = walker.GetComponent<NpcGait>();
            bool walkerRan = false;
            until = Time.time + 1.5f;
            while (Time.time < until)
            {
                walkerRan |= walkerGait != null && walkerGait.Current == Gait.Run;
                yield return null;
            }
            Check("canRun false never runs", walkerGait != null && !walkerRan && walkerGait.Boosted == 0f,
                "boosted=" + (walkerGait != null ? walkerGait.Boosted : -1f));
            walker.SwitchHostile(false);
            NpcBrain.Discard(walker);

            // A fleeing coward runs at run speed.
            NpcDefinition cowardDef = TestDefinition("selftest_coward", Bravery.Coward, location, player, 2f);
            cowardDef.Folder = "selftest_runner";
            NpcBrain coward = SpawnDefinition(cowardDef, location);
            yield return Settle;
            PlayerHit(coward, playerBehaviour, 1);
            yield return new WaitForSeconds(0.3f);
            NpcGait cowardGait = coward.GetComponent<NpcGait>();
            Vector3 from = coward.transform.localPosition;
            yield return new WaitForSeconds(0.5f);
            Vector3 fled = coward.transform.localPosition - from;
            fled.y = 0;
            float fleeSpeed = fled.magnitude / 0.5f;
            Check("a fleeing ANPC runs at run speed", coward.CurrentMode == NpcMode.Fleeing && cowardGait != null &&
                cowardGait.Current == Gait.Run && fleeSpeed > cowardGait.RunSpeed * 0.8f,
                coward.Status + ", speed " + fleeSpeed + " (run " + (cowardGait != null ? cowardGait.RunSpeed : -1f) + ")");
            NpcBrain.Discard(coward);

            // A turret (no walk, no run sheet) stays put while hostile and still faces the player; a world shift does
            // not move it. Only horizontal drift counts: it may still settle onto the ground.
            NpcDefinition turretDef = TestDefinition("selftest_turret", Bravery.Brave, location, player, 0f);
            NpcBrain turret = SpawnDefinition(turretDef, location);
            yield return Settle;
            turret.Teleport(player.position + open * 6f);
            // Turned 80 degrees from the player: inside DFU's view cone, so it notices and must turn on the spot.
            turret.transform.rotation = Quaternion.LookRotation(Quaternion.Euler(0f, 80f, 0f) * -open);
            yield return new WaitForSeconds(0.5f);
            Vector3 spot = turret.transform.localPosition;
            turret.SwitchHostile(true);
            yield return new WaitForSeconds(3f);
            yield return new WaitForEndOfFrame();     // DFU's motor steps in FixedUpdate; the pin puts it back in LateUpdate
            Vector3 drift = turret.transform.localPosition - spot;
            drift.y = 0;
            Vector3 toPlayer = player.position - turret.transform.position;
            toPlayer.y = 0;
            float facing = Vector3.Angle(turret.transform.forward, toPlayer);
            NpcGait turretGait = turret.GetComponent<NpcGait>();
            Check("a turret does not move but faces its target", turretGait != null && turretGait.IsTurret && drift.magnitude < 0.05f && facing < 30f,
                "drift=" + drift.magnitude + ", facing off by " + facing + ", " + turret.Status + ", target=" +
                (turret.GetComponent<EnemySenses>().Target == playerBehaviour ? "player" : turret.GetComponent<EnemySenses>().Target != null ? turret.GetComponent<EnemySenses>().Target.name : "none") +
                ", sight=" + turret.GetComponent<EnemySenses>().TargetInSight + ", detected=" + turret.GetComponent<EnemySenses>().DetectedTarget +
                ", motor=" + turret.Motor.enabled + ", distance=" + Vector3.Distance(turret.transform.position, player.position));
            Transform town = turret.transform.parent;
            town.position += new Vector3(500f, 0f, 0f);
            yield return null;
            yield return new WaitForEndOfFrame();
            Vector3 shifted = turret.transform.localPosition - spot;
            town.position -= new Vector3(500f, 0f, 0f);
            shifted.y = 0;
            Check("world shift does not boost or unpin", shifted.magnitude < 0.05f, "moved " + shifted.magnitude);
            turret.SwitchHostile(false);
            NpcBrain.Discard(turret);

            mod.Sprites.Remove("selftest_runner");
            mod.Sprites.Remove("selftest_turret");
            yield return Settle;
        }

        /// <summary>
        /// A flat direction from the player with a clear line for at least `need` metres and ground under its far end;
        /// the player's forward if it qualifies, else the first of 16 directions that does (forward if none).
        /// </summary>
        static Vector3 OpenDirection(Transform player, float need)
        {
            Vector3 forward = player.forward;
            forward.y = 0;
            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            for (int i = 0; i < 16; i++)
            {
                Vector3 dir = Quaternion.Euler(0f, i * 22.5f, 0f) * forward;
                if (Physics.Raycast(player.position, dir, need, ~0, QueryTriggerInteraction.Ignore))
                    continue;
                if (!Physics.Raycast(player.position + dir * need, Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore))
                    continue;
                return dir;
            }
            AdvancedNpcsMod.Log("selftest: no open direction around the player; using forward.");
            return forward;
        }
    }
}

using System.Collections;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>Self-test: personal opinion of the player and update LOD (spec opinion-lod §C).</summary>
    public partial class SelfTest
    {
        IEnumerator OpinionSteps(DaggerfallLocation location, Transform player)
        {
            DaggerfallEntityBehaviour playerBehaviour = GameManager.Instance.PlayerEntityBehaviour;
            DiscardTestPeople();
            Vector3 open = OpenDirection(player, 16f);

            // Hitting a calm ANPC: it thinks less of the player, and so does a calm onlooker.
            NpcBrain victim = SpawnDefinition(TestDefinition("selftest_op_victim", Bravery.Normal, location, player, 0f), location);
            NpcBrain onlooker = SpawnDefinition(TestDefinition("selftest_op_onlooker", Bravery.Normal, location, player, 0f), location);
            yield return Settle;
            victim.Teleport(player.position + open * 3f);
            onlooker.Teleport(player.position + open * 7f);
            FaceToward(onlooker, victim.transform.position);
            yield return Settle;
            PlayerHit(victim, playerBehaviour, 1);
            yield return null;
            yield return null;
            Check("hitting a calm ANPC lowers its opinion by 25", victim.Opinion == OpinionRules.Assault, victim.Status);
            Check("a calm onlooker thinks 10 less of the player", onlooker.Opinion == OpinionRules.Witness, onlooker.Status);
            NpcBrain.Discard(victim);
            NpcBrain.Discard(onlooker);

            // The dialogue action "opinion" changes it (through the same facts object conversations use).
            NpcBrain talker = SpawnDefinition(TestDefinition("selftest_op_talker", Bravery.Normal, location, player, 0f), location);
            yield return Settle;
            new GameFacts(talker.State, mod.Flags, talker.DisplayName, null).ChangeOpinion(15);
            Check("the reply action \"opinion\" changes it", talker.Opinion == 15, talker.Status);
            NpcBrain.Discard(talker);

            // A high personal opinion makes a calm ANPC greet the player without any test override.
            mod.Sprites.Add("selftest_op_greeter", TestSpriteSet(false, "idle", "walk", "greet", "death"));
            NpcGreeter.ReactionOverride = null;
            NpcGreeter.ChanceOverride = 1f;
            try
            {
                NpcBrain fan = SpawnDefinition(TestDefinition("selftest_op_greeter", Bravery.Normal, location, player, 0f), location);
                fan.Teleport(player.position + open * 9f);     // out of greeting range while its opinion changes
                yield return new WaitForSeconds(0.8f);
                fan.ChangeOpinion(100);
                fan.Teleport(player.position + open * 3f);     // a new encounter
                NpcSprite fanSprite = fan.GetComponent<NpcSprite>();
                float until = Time.time + 2f;
                while (Time.time < until && (fanSprite == null || fanSprite.GreetStarts == 0))
                    yield return null;
                Check("an ANPC that likes the player greets them", fanSprite != null && fanSprite.GreetStarts == 1,
                    "greets=" + (fanSprite != null ? fanSprite.GreetStarts : -1) + ", " + fan.Status);
                NpcBrain.Discard(fan);
            }
            finally
            {
                NpcGreeter.ChanceOverride = -1f;
                mod.Sprites.Remove("selftest_op_greeter");
            }

            // LOD: a far ANPC's picture changes frame less often than a near one, but still animates.
            mod.Sprites.Add("selftest_lod", TestSpriteSet(false, "idle", "walk", "death"));
            NpcBrain near = SpawnDefinition(TestDefinition("selftest_lod", Bravery.Normal, location, player, 0f), location);
            NpcDefinition farDef = TestDefinition("selftest_lod_far", Bravery.Normal, location, player, 0f);
            farDef.Folder = "selftest_lod";
            NpcBrain far = SpawnDefinition(farDef, location);
            yield return Settle;
            near.Teleport(player.position + open * 5f);
            far.Teleport(player.position + open * 150f);
            yield return Settle;
            NpcSprite nearSprite = near.GetComponent<NpcSprite>();
            NpcSprite farSprite = far.GetComponent<NpcSprite>();
            int nearBefore = nearSprite.UpdatesRun, farBefore = farSprite.UpdatesRun, farFramesBefore = farSprite.FrameChanges;
            yield return new WaitForSeconds(3f);
            int nearRuns = nearSprite.UpdatesRun - nearBefore, farRuns = farSprite.UpdatesRun - farBefore;
            int farChanges = farSprite.FrameChanges - farFramesBefore;
            Check("a far ANPC is updated less often but still animates", farRuns * 2 < nearRuns && farChanges > 0,
                "updates near " + nearRuns + ", far " + farRuns + "; far frame changes " + farChanges);
            NpcBrain.Discard(near);
            NpcBrain.Discard(far);
            mod.Sprites.Remove("selftest_lod");
            DiscardTestPeople();
            yield return Settle;
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    public partial class SelfTest
    {
        IEnumerator GuardPackageSteps(DaggerfallLocation location, Transform player)
        {
            DiscardTestPeople();
            string error;
            AiPackage package = PackageParser.Parse("selftest_guard_spot", "{\"type\":\"guard_spot\"}", out error);
            mod.Packages.AddForTest(package);
            mod.Sprites.Add("selftest_guard_spot", TestSpriteSet(false, "idle", "idle_1", "idle_2", "walk", "attack", "cast", "greet"));
            mod.Sprites.Add("selftest_guard_stationary", TestSpriteSet(false, "idle_1", "idle_2"));
            try
            {
                NpcDefinition d = TestDefinition("selftest_guard_spot", Bravery.Coward, location, player, OutOfGreeting);
                d.BaseClass = "Warrior";
                d.WanderRadius = 8;
                d.Schedule.Add(new AiScheduleEntry {Package = package.Id});
                NpcBrain actor = SpawnDefinition(d, location);
                yield return Settle;
                NpcSprite sprite = actor.GetComponent<NpcSprite>();
                Vector3 post = actor.transform.localPosition;
                yield return new WaitForSeconds(0.6f);
                Check("guard holds current post and idle_1 frame zero", sprite != null && sprite.CurrentState == NpcSprite.GuardState &&
                    sprite.CurrentAnimation == "idle_1" && sprite.CurrentFrame == 0 && Vector3.Distance(post, actor.transform.localPosition) < 0.2f,
                    actor.Describe());
                Check("guard overrides bravery without changing NPC definition", actor.EffectiveBravery == Bravery.Brave && d.Bravery == Bravery.Coward,
                    "effective=" + actor.EffectiveBravery + ", configured=" + d.Bravery);

                actor.transform.localPosition += Vector3.right * 3;
                yield return new WaitForSeconds(3f);
                Check("guard returns to captured post after displacement", Vector3.Distance(post, actor.transform.localPosition) < 0.8f, actor.Describe());

                d.Schedule[0] = new AiScheduleEntry {Package = package.Id, Parameters = new Dictionary<string, object> {
                    {"idleInterval", new List<object> {0.4,0.4}} }};
                bool animated = false, rested = false;
                float until = Time.time + 2.5f;
                while (Time.time < until)
                {
                    animated |= sprite.CurrentState == NpcSprite.GuardState && sprite.CurrentFrame > 0;
                    rested |= animated && sprite.CurrentState == NpcSprite.GuardState && sprite.CurrentAnimation == "idle_1" && sprite.CurrentFrame == 0;
                    yield return null;
                }
                Check("guard plays occasional idle cycles and restores frozen pose", animated && rested, "animated=" + animated + ", rested=" + rested);
                sprite.PlayCast(true);
                actor.HoldStill(0.6f);
                yield return new WaitForSeconds(0.1f);
                Check("guard animation yields to healing", sprite.CurrentState == SpriteStates.Heal || sprite.CurrentState == SpriteStates.Cast, sprite.CurrentState);
                yield return new WaitForSeconds(0.8f);
                Check("guard resumes after healing hold", sprite.CurrentState == NpcSprite.GuardState, sprite.CurrentState);

                actor.SwitchHostile(true);
                yield return Settle;
                Check("Coward fights while guarding and visual routine yields", actor.CurrentMode == NpcMode.Fighting && actor.GuardAnimation == null,
                    actor.Status);
                d.Schedule.Clear();
                yield return Settle;
                Check("slot end restores Coward bravery during combat", actor.EffectiveBravery == Bravery.Coward && actor.CurrentMode == NpcMode.Fleeing,
                    actor.Describe());
                d.Schedule.Add(new AiScheduleEntry {Package = package.Id});
                yield return Settle;
                Check("starting guard slot changes existing fleeing NPC to fighting", actor.EffectiveBravery == Bravery.Brave && actor.CurrentMode == NpcMode.Fighting,
                    actor.Status);
                actor.SwitchHostile(false);
                yield return new WaitForSeconds(1f);
                Vector3 fixedPost = actor.transform.localPosition + Vector3.right * 3;
                d.Schedule[0] = new AiScheduleEntry {Package = package.Id, Parameters = new Dictionary<string, object> {
                    {"position", new List<object> {(double)fixedPost.x,(double)fixedPost.y,(double)fixedPost.z}} }};
                yield return new WaitForSeconds(3f);
                Check("guard walks to configured absolute area position", Vector3.Distance(fixedPost, actor.transform.localPosition) < 0.8f,
                    "post=" + fixedPost + ", actor=" + actor.transform.localPosition);
                actor.SwitchHostile(true);
                yield return Settle;
                actor.transform.localPosition = fixedPost + Vector3.right * 3;
                actor.SwitchHostile(false);
                yield return new WaitForSeconds(3f);
                Check("guard returns to configured post after combat", Vector3.Distance(fixedPost, actor.transform.localPosition) < 0.8f,
                    "post=" + fixedPost + ", actor=" + actor.transform.localPosition);
                d.Schedule.Clear();
                yield return Settle;
                Check("ending guard releases frozen pose and override", actor.GuardAnimation == null && actor.EffectiveBravery == Bravery.Coward &&
                    sprite.CurrentState != NpcSprite.GuardState, actor.Describe());
                NpcBrain.Discard(actor);

                NpcDefinition stationary = TestDefinition("selftest_guard_stationary", Bravery.Prudent, location, player, OutOfGreeting);
                stationary.Schedule.Add(new AiScheduleEntry {Package = package.Id});
                NpcBrain fixedActor = SpawnDefinition(stationary, location);
                yield return Settle;
                Check("stationary sprite set can guard its current post", fixedActor.EffectiveBravery == Bravery.Brave &&
                    fixedActor.GetComponent<NpcSprite>().CurrentState == NpcSprite.GuardState, fixedActor.Describe());
                NpcBrain.Discard(fixedActor);
            }
            finally
            {
                mod.Packages.RemoveForTest(package.Id);
                mod.Sprites.Remove("selftest_guard_spot");
                mod.Sprites.Remove("selftest_guard_stationary");
            }
        }
    }
}

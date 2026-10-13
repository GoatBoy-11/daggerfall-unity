using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>
    /// Self-test: own ranged attacks (creature throws, projectile* images, projectileDamage). Also runs alone as the
    /// "ranged" suite (anpc_selftest ranged, Tools~/selftest-ranged.sh) for a short run.
    /// </summary>
    public partial class SelfTest
    {
        public const string RangedSuite = "ranged";

        IEnumerator RangedSteps(DaggerfallLocation location, Transform player)
        {
            DiscardTestPeople();

            // The installed orc_weakling example: its throw sheet (attack_ranged_1) and its rock (projectile.png).
            List<LoadedSpriteSet> orcSets = mod.Sprites.For("orc_weakling");
            LoadedSpriteSet orc = orcSets.Count > 0 ? orcSets[0] : null;
            SpriteAnimation throwSheet = null;
            Check("orc_weakling has an attack_ranged_1 sheet and a projectile", orc != null &&
                orc.Set.Animations.TryGetValue("attack_ranged_1", out throwSheet) &&
                SpriteStates.StateOf(throwSheet.Name) == SpriteStates.RangedAttack && orc.Set.Projectiles.Count > 0,
                orc == null ? "orc_weakling not installed" : "sheets: " + string.Join(", ", new List<string>(orc.Set.Animations.Keys).ToArray()) +
                "; projectiles " + orc.Set.Projectiles.Count);

            // A human class keeps DFU's own arrow unless it has projectile images or a damage override.
            NpcDefinition archer = TestDefinition("selftest_archer", Bravery.Normal, location, player, 0f);
            archer.RangedAtDistance = true;
            bool plainBow = !NpcRanged.Wanted(archer, null);
            archer.ProjectileDamageMin = archer.ProjectileDamageMax = 2;
            Check("own ranged attack only when needed", plainBow && NpcRanged.Wanted(archer, null), "plain bow wanted it: " + !plainBow);

            // Override without a sprite set: DFU's arrow model flies.
            NpcBrain archerBrain = SpawnDefinition(archer, location);
            yield return Settle;
            NpcRanged archerRanged = archerBrain.GetComponent<NpcRanged>();
            int flyingBefore = NpcProjectile.Flying.Count;
            if (archerRanged != null)
                archerRanged.Fire();
            NpcProjectile arrow = NpcProjectile.Flying.Count > flyingBefore ? NpcProjectile.Flying[NpcProjectile.Flying.Count - 1] : null;
            Check("projectileDamage without projectile images flies DFU's arrow", archerRanged != null && arrow != null && !arrow.Custom,
                archerRanged == null ? "no NpcRanged" : arrow == null ? "nothing flew" : "a custom sprite flew");
            NpcBrain.Discard(archerBrain);

            // A creature throws its projectile image at its foe; the action frame of attack_range fires it, and each
            // hit does exactly projectileDamage.
            Vector3 open = OpenDirection(player, 17f);
            LoadedSpriteSet set = TestSpriteSet(false, "idle", "walk", "attack", "attack_range");
            set.Set.Animations["attack_range"].ActionFrame = 1;
            SpriteAnimation rock = SpriteSetParser.SingleFrameProjectile("projectile", 8, 8);
            set.Set.Projectiles[rock.Name] = rock;
            set.Sheets[rock.Name] = SolidTexture(8, 8, new Color32(120, 110, 100, 255));
            mod.Sprites.Add("selftest_thrower", set);
            NpcDefinition throwerDef = TestDefinition("selftest_thrower", Bravery.Brave, location, player, 0f);
            throwerDef.BaseClass = "Orc";
            throwerDef.IsCreature = true;
            throwerDef.RangedAtDistance = true;
            throwerDef.ProjectileDamageMin = throwerDef.ProjectileDamageMax = 3;
            NpcBrain thrower = SpawnDefinition(throwerDef, location);
            NpcBrain target = SpawnDefinition(TestDefinition("selftest_target", Bravery.Brave, location, player, 0f), location);
            yield return Settle;
            target.Teleport(player.position + open * 4f);
            thrower.Teleport(player.position + open * 16f);
            yield return Settle;
            thrower.Motor.MakeEnemyHostileToAttacker(target.EntityBehaviour);
            yield return new WaitForSeconds(0.5f);
            NpcRanged ranged = thrower.GetComponent<NpcRanged>();
            NpcSprite sprite = thrower.GetComponent<NpcSprite>();
            int health = target.EntityBehaviour.Entity.CurrentHealth;
            bool sawSheet = false, sawRock = false;
            for (int attempt = 0; attempt < 8 && ranged != null && target.EntityBehaviour.Entity.CurrentHealth == health; attempt++)
            {
                thrower.Teleport(player.position + open * 16f);    // out of club reach: only throws can hurt
                ranged.Throw();
                for (float t = 0f; t < 1.5f; t += Time.deltaTime)
                {
                    sawSheet |= sprite != null && sprite.CurrentState == SpriteStates.RangedAttack;
                    foreach (NpcProjectile p in NpcProjectile.Flying)
                        sawRock |= p.Custom;
                    yield return null;
                }
            }
            int lost = health - target.EntityBehaviour.Entity.CurrentHealth;
            Check("a creature throws its projectile image", ranged != null && ranged.Shots > 0 && sawSheet && sawRock,
                ranged == null ? "no NpcRanged" : "shots " + ranged.Shots + ", sheet " + sawSheet + ", sprite flew " + sawRock);
            Check("a thrown hit does projectileDamage", lost > 0 && lost % 3 == 0, "target lost " + lost + " health; " + thrower.Status);
            NpcBrain.Discard(thrower);
            NpcBrain.Discard(target);
            mod.Sprites.Remove("selftest_thrower");
        }
    }
}

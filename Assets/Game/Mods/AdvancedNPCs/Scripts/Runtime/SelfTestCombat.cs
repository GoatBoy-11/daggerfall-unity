using System.Collections;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>Self-test: retaliation on a miss, bravery, brave patrols (spec lazy-sprites §B-D).</summary>
    public partial class SelfTest
    {
        IEnumerator CombatSteps(DaggerfallLocation location, Transform player)
        {
            DaggerfallEntityBehaviour playerBehaviour = GameManager.Instance.PlayerEntityBehaviour;
            DiscardTestPeople();
            Vector3 open = OpenDirection(player, 16f);
            mod.Sprites.Add("selftest_post", TestSpriteSet(false, "idle", "attack", "death"));

            // A swing that misses: DFU calls MakeEnemyHostileToAttacker without any damage.
            NpcBrain victim = SpawnDefinition(TestDefinition("selftest_missed", Bravery.Normal, location, player, 0f), location);
            NpcBrain swinger = SpawnDefinition(TestDefinition("selftest_swinger", Bravery.Normal, location, player, 0f), location);
            yield return Settle;
            victim.Teleport(player.position + open * 5f);
            swinger.Teleport(player.position + open * 6.5f);
            yield return Settle;
            int before = victim.EntityBehaviour.Entity.CurrentHealth;
            victim.Motor.MakeEnemyHostileToAttacker(swinger.EntityBehaviour);
            yield return null;
            yield return null;
            Check("a calm ANPC fights back when an attack misses", victim.CurrentMode == NpcMode.Fighting &&
                victim.Threat == swinger.EntityBehaviour && victim.EntityBehaviour.Entity.CurrentHealth == before, victim.Status);
            NpcBrain.Discard(victim);
            NpcBrain.Discard(swinger);

            // Bravery at low health, decided when the fight starts.
            IEnumerator c = BraveryCase(location, player, open, Bravery.Normal, 100, 0.05f, NpcMode.Fleeing, "Normal flees below half the line when fleeChance is 100");
            while (c.MoveNext()) yield return c.Current;
            c = BraveryCase(location, player, open, Bravery.Normal, 0, 0.10f, NpcMode.Fighting, "Normal fights on when fleeChance is 0");
            while (c.MoveNext()) yield return c.Current;
            c = BraveryCase(location, player, open, Bravery.Normal, 100, 0.20f, NpcMode.Fighting, "Normal fights above half the line");
            while (c.MoveNext()) yield return c.Current;
            c = BraveryCase(location, player, open, Bravery.Prudent, 0, 0.20f, NpcMode.Fleeing, "Prudent flees below fleeHealthPercent");
            while (c.MoveNext()) yield return c.Current;

            // Brave patrol: an enemy post (no walk sheet: cannot move) beyond aggroRange is ignored; within it, attacked.
            NpcBrain post = SpawnDefinition(TestDefinition("selftest_post", Bravery.Brave, location, player, 0f), location);
            NpcDefinition braveDef = TestDefinition("selftest_patrol", Bravery.Brave, location, player, 0f);
            braveDef.AggroRange = 5f;
            NpcBrain brave = SpawnDefinition(braveDef, location);
            yield return Settle;
            post.Teleport(player.position + open * 14f);
            brave.Teleport(player.position + open * 6f);
            post.SwitchHostile(true);
            yield return new WaitForSeconds(1.5f);
            Check("a brave ANPC ignores an enemy beyond its aggroRange", brave.CurrentMode == NpcMode.Calm, brave.Status);
            brave.Teleport(player.position + open * 10f);

            // Within range but in a switched-off town (its GameObject inactive, as DFU leaves towns out of reach): no foe.
            GameObject offTown = new GameObject("selftest_patrol_off_town");
            offTown.SetActive(false);
            Transform postHome = post.transform.parent;
            post.transform.SetParent(offTown.transform, true);
            yield return new WaitForSeconds(1.5f);
            Check("a brave ANPC ignores an enemy in a switched-off town", brave.CurrentMode == NpcMode.Calm, brave.Status);
            post.transform.SetParent(postHome, true);
            Object.Destroy(offTown);

            float until = Time.time + 3f;
            while (Time.time < until && brave.CurrentMode == NpcMode.Calm)
                yield return null;
            Check("a brave ANPC attacks an enemy it sees within range", brave.CurrentMode == NpcMode.Fighting &&
                brave.Threat == post.EntityBehaviour, brave.Status);
            post.SwitchHostile(false);
            NpcBrain.Discard(post);
            NpcBrain.Discard(brave);

            // Only angry at the player (hit by the player, not an enemy): not a foe.
            NpcBrain angry = SpawnDefinition(TestDefinition("selftest_angry", Bravery.Brave, location, player, 0f), location);
            NpcBrain watcher = SpawnDefinition(TestDefinition("selftest_watcher", Bravery.Brave, location, player, 0f), location);
            yield return Settle;
            angry.Teleport(player.position + open * 3f);
            watcher.Teleport(player.position + open * 7f);
            yield return Settle;
            PlayerHit(angry, playerBehaviour, 1);
            yield return new WaitForSeconds(1.5f);
            Check("a brave ANPC ignores an ANPC only angry at the player", watcher.CurrentMode == NpcMode.Calm, watcher.Status);
            NpcBrain.Discard(angry);
            NpcBrain.Discard(watcher);

            mod.Sprites.Remove("selftest_post");
            DiscardTestPeople();
            yield return Settle;
        }

        IEnumerator BraveryCase(DaggerfallLocation location, Transform player, Vector3 open, Bravery bravery, int fleeChance,
            float health, NpcMode expected, string name)
        {
            NpcDefinition d = TestDefinition("selftest_bravery", bravery, location, player, 0f);
            d.FleeChance = fleeChance;
            NpcBrain b = SpawnDefinition(d, location);
            NpcBrain other = SpawnDefinition(TestDefinition("selftest_bravery_foe", Bravery.Normal, location, player, 0f), location);
            yield return Settle;
            b.Teleport(player.position + open * 5f);
            other.Teleport(player.position + open * 7f);
            yield return Settle;
            DaggerfallEntity e = b.EntityBehaviour.Entity;
            e.CurrentHealth = Mathf.Max(1, Mathf.RoundToInt(e.MaxHealth * health));
            yield return null;
            b.Motor.MakeEnemyHostileToAttacker(other.EntityBehaviour);
            yield return null;
            yield return null;
            Check(name, b.CurrentMode == expected, b.Status);
            NpcBrain.Discard(b);
            NpcBrain.Discard(other);
            yield return null;
        }
    }
}

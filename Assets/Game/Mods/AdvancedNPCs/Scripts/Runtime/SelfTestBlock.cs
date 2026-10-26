using System;
using System.Collections;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Formulas;
using DaggerfallWorkshop.Game.Items;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>Self-test steps for block sheets (an attack that did no damage). Run alone: suite "block".</summary>
    public partial class SelfTest
    {
        public const string BlockSuite = "block";

        IEnumerator BlockSteps(DaggerfallLocation location, Transform playerTransform)
        {
            DiscardTestPeople();
            DaggerfallEntityBehaviour player = GameManager.Instance.PlayerEntityBehaviour;
            Check("block_2 is a block sheet; a set without one has no fallback",
                SpriteStates.StateOf("block_2") == SpriteStates.Block && SpriteStates.Variants(TestSpriteSet(false).Set, SpriteStates.Block).Count == 0,
                "state of block_2=" + SpriteStates.StateOf("block_2"));

            mod.Sprites.Add("selftest_blocker", TestSpriteSet(false, "idle", "walk", "death", "hit", "attack", "block_1", "block_2"));
            mod.Sprites.Add("selftest_noblock", TestSpriteSet(false));
            NpcBrain blocker = SpawnDefinition(TestDefinition("selftest_blocker", Bravery.Normal, location, playerTransform, -1f), location);
            NpcBrain plain = SpawnDefinition(TestDefinition("selftest_noblock", Bravery.Normal, location, playerTransform, 1f), location);
            yield return Settle;
            NpcSprite blocks = blocker.GetComponent<NpcSprite>();
            NpcSprite stays = plain.GetComponent<NpcSprite>();
            if (blocks == null || stays == null)
            {
                Fail("block: setup", "test ANPCs have no NpcSprite");
                yield break;
            }

            AttackOutcomeHook.ClearForTest();
            yield return null;
            Check("DFU's own attack formula is wrapped", AttackOutcomeHook.Active && AttackOutcomeHook.Previous == null,
                "active=" + AttackOutcomeHook.Active + ", previous=" + AttackOutcomeHook.Previous);

            // Another combat mod (Physical Combat and Armor Overhaul, say) registers its formula later: it still decides the damage.
            int calls = 0, fakeDamage = 0;
            Func<DaggerfallEntity, DaggerfallEntity, bool, int, DaggerfallUnityItem, int> fake =
                delegate (DaggerfallEntity a, DaggerfallEntity t, bool back, int time, DaggerfallUnityItem w) { calls++; return fakeDamage; };
            FormulaHelper.RegisterOverride(AttackOutcomeHook.Provider("selftest combat mod", 100000), "CalculateAttackDamage", fake);
            yield return null;
            int blocksBefore = blocks.BlockStarts, staysBefore = stays.BlockStarts, hitsBefore = blocks.HitStarts;
            int dealt = FormulaHelper.CalculateAttackDamage(player.Entity, blocker.EntityBehaviour.Entity, false, 0, null);
            FormulaHelper.CalculateAttackDamage(player.Entity, plain.EntityBehaviour.Entity, false, 0, null);
            yield return null;
            yield return null;
            Check("a later mod's attack formula is wrapped and still decides the damage",
                AttackOutcomeHook.Active && AttackOutcomeHook.Previous == (Delegate)fake && calls == 2 && dealt == 0,
                "active=" + AttackOutcomeHook.Active + ", calls=" + calls + ", damage=" + dealt);
            Check("an attack that does no damage plays a block sheet",
                blocks.BlockStarts == blocksBefore + 1 && blocks.CurrentState == SpriteStates.Block && blocks.HitStarts == hitsBefore,
                "block starts " + (blocks.BlockStarts - blocksBefore) + ", now " + blocks.CurrentState);
            Check("a set without block sheets keeps its pose", stays.BlockStarts == staysBefore && stays.CurrentState != SpriteStates.Block,
                "now " + stays.CurrentState);

            yield return new WaitForSeconds(0.5f);
            fakeDamage = 3;
            blocksBefore = blocks.BlockStarts;
            FormulaHelper.CalculateAttackDamage(player.Entity, blocker.EntityBehaviour.Entity, false, 0, null);
            yield return null;
            yield return null;
            Check("an attack that does damage plays no block sheet", blocks.BlockStarts == blocksBefore, "now " + blocks.CurrentState);

            yield return new WaitForSeconds(0.5f);
            fakeDamage = 0;
            blocksBefore = blocks.BlockStarts;
            mod.Config.BlockAnimations = false;
            FormulaHelper.CalculateAttackDamage(player.Entity, blocker.EntityBehaviour.Entity, false, 0, null);
            yield return null;
            yield return null;
            mod.Config.BlockAnimations = true;
            Check("with the BlockAnimations setting off, no block sheet plays", blocks.BlockStarts == blocksBefore, "now " + blocks.CurrentState);

            PlayerHit(blocker, player, 1);
            yield return null;
            yield return null;
            fakeDamage = 0;
            blocksBefore = blocks.BlockStarts;
            FormulaHelper.CalculateAttackDamage(player.Entity, blocker.EntityBehaviour.Entity, false, 0, null);
            yield return null;
            yield return null;
            Check("a block does not cut a hit sheet short", blocks.BlockStarts == blocksBefore && blocks.CurrentState == SpriteStates.Hit,
                "now " + blocks.CurrentState);

            AttackOutcomeHook.ClearForTest();
            yield return null;
            int vanilla = FormulaHelper.CalculateAttackDamage(player.Entity, plain.EntityBehaviour.Entity, false, 0, null);
            Check("with the other mod's formula gone, DFU's own runs through the wrapper",
                AttackOutcomeHook.Active && AttackOutcomeHook.Previous == null && vanilla >= 0, "damage=" + vanilla);

            NpcBrain.Discard(blocker);
            NpcBrain.Discard(plain);
            mod.Sprites.Remove("selftest_blocker");
            mod.Sprites.Remove("selftest_noblock");
        }
    }
}

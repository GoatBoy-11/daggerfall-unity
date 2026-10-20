using System.Reflection;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Formulas;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.MagicAndEffects;
using DaggerfallWorkshop.Game.Questing;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>Guard enforcement interrupts routines without changing the actor into a disposable native guard.</summary>
    public class NpcGuard
    {
        static readonly MethodInfo nativeHit = typeof(EnemyAttack).GetMethod("ApplyDamageToNonPlayer", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly MethodInfo nativeStart = typeof(EnemyAttack).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic);
        readonly NpcBrain brain;
        readonly EnemySenses senses;
        readonly EnemyAttack attack;
        readonly NpcMover mover;
        readonly NpcGait gait;
        readonly MobileUnit mobile;
        readonly EnemySounds sounds;
        DaggerfallEntityBehaviour target;
        FollowNavigation navigation;
        GameObject lastSeen;
        readonly CompanionPursuit pursuit = new CompanionPursuit();
        DaggerfallEntityBehaviour avoided;
        float retryAt, scan, haltWait;
        bool oldSenses;
        public bool OwnsTarget { get { return target != null; } }
        public string Description { get { return OwnsTarget ? "pursuing " + target.name : "on duty"; } }

        public NpcGuard(NpcBrain owner)
        {
            brain = owner;
            senses = owner.GetComponent<EnemySenses>();
            attack = owner.GetComponent<EnemyAttack>();
            mover = owner.GetComponent<NpcMover>();
            gait = owner.GetComponent<NpcGait>();
            mobile = owner.GetComponentInChildren<MobileUnit>();
            sounds = owner.GetComponent<EnemySounds>();
            // EnemyAttack can still be waiting for Start when the brain first disables it.
            if (nativeStart != null) nativeStart.Invoke(attack, null);
        }

        bool SameArea(DaggerfallEntityBehaviour other)
        {
            if (other == GameManager.Instance.PlayerEntityBehaviour)
            {
                PlayerEnterExit area = GameManager.Instance.PlayerEnterExit;
                return !area.IsPlayerInsideDungeon && brain.GetComponentInParent<DaggerfallDungeon>() == null &&
                    brain.GetComponentInParent<DaggerfallInterior>() == (area.IsPlayerInsideBuilding ? area.Interior : null);
            }
            return other.GetComponentInParent<DaggerfallInterior>() == brain.GetComponentInParent<DaggerfallInterior>() &&
                other.GetComponentInParent<DaggerfallDungeon>() == brain.GetComponentInParent<DaggerfallDungeon>();
        }
        bool Legal(DaggerfallEntityBehaviour other)
        {
            if (other == null || !other.isActiveAndEnabled || other.Entity == null || other.Entity.CurrentHealth <= 0 || !SameArea(other)) return false;
            PlayerEntity player = GameManager.Instance.PlayerEntity;
            if (other == GameManager.Instance.PlayerEntityBehaviour)
                return !player.Arrested && GuardRules.ValidCrime((int)player.CrimeCommitted) && !GameManager.Instance.PlayerEnterExit.IsPlayerInsideDungeon;
            NpcBrain suspect = other.GetComponent<NpcBrain>();
            // Quest actors and party members need their owning system to explicitly resolve them.
            return suspect != null && suspect != brain && !suspect.Definition.Guard && suspect.State.companion == null &&
                other.GetComponent<QuestResourceBehaviour>() == null && nativeHit != null &&
                GuardRules.Wanted(suspect.State, GameManager.Instance.PlayerGPS.CurrentRegionIndex);
        }
        DaggerfallEntityBehaviour Find()
        {
            DaggerfallEntityBehaviour best = GameManager.Instance.PlayerEntityBehaviour;
            float distance = brain.Definition.AggroRange;
            if ((best == avoided && Time.time < retryAt) || !Legal(best) || Vector3.Distance(brain.transform.position, best.transform.position) > distance || !brain.CanSee(best)) best = null;
            if (best != null) return best;
            foreach (NpcBrain suspect in NpcBrain.All())
            {
                DaggerfallEntityBehaviour other = suspect.EntityBehaviour;
                if ((other == avoided && Time.time < retryAt) || !Legal(other)) continue;
                float gap = Vector3.Distance(brain.transform.position, other.transform.position);
                if (gap <= distance && brain.CanSee(other)) { best = other; distance = gap; }
            }
            return best;
        }
        public void Release()
        {
            if (navigation == null) return;
            navigation.Suspend(); navigation = null;
            if (lastSeen != null) Object.Destroy(lastSeen);
            pursuit.Reset();
            NpcVoice voice = brain.GetComponent<NpcVoice>();
            if (voice != null) voice.CancelHalt();
            target = null;
            senses.enabled = oldSenses;
            senses.Target = null;
            mobile.DoMeleeDamage = false; mobile.ShootArrow = false;
            NpcSprite sprite = brain.GetComponent<NpcSprite>();
            if (sprite != null) sprite.CancelAttack();
            brain.Motor.IsHostile = brain.State.hostile || brain.IsEnemy;
            if (brain.CurrentMode != NpcMode.Dead && brain.EntityBehaviour.Entity.CurrentHealth > 0)
            {
                if (brain.IsEnemy)
                {
                    brain.Motor.enabled = attack.enabled = true; mover.enabled = false;
                    senses.Target = GameManager.Instance.PlayerEntityBehaviour;
                }
                else brain.CompanionMode(null, NpcMode.Calm);
            }
        }
        public bool CanDemandPlayer
        {
            get
            {
                DaggerfallEntityBehaviour player = GameManager.Instance.PlayerEntityBehaviour;
                return brain.isActiveAndEnabled && !brain.IsEnemy && Legal(player) && brain.CanSee(player) &&
                    Vector3.Distance(brain.transform.position, player.transform.position) <= PlayerActivate.MobileNPCActivationDistance;
            }
        }
        public bool TargetsPlayer { get { return target != null && target == GameManager.Instance.PlayerEntityBehaviour; } }
        void Halt()
        {
            // Vanilla repeats every 3-9 seconds; ANPC guards call less often.
            haltWait = Random.Range(15f, 30f);
            NpcVoice voice = brain.GetComponent<NpcVoice>();
            if (voice != null) voice.PlayHalt();
        }
        public bool Tick()
        {
            if (brain.IsEnemy || brain.CurrentMode == NpcMode.Dead || !brain.isActiveAndEnabled ||
                (brain.CompanionCombat != null && brain.CompanionCombat.Active)) { Release(); return false; }
            if (navigation != null && !Legal(target)) Release();
            if (navigation == null)
            {
                if (GameManager.IsGamePaused || GameManager.Instance.DisableAI) return false;
                scan -= Time.deltaTime;
                if (scan > 0) return false;
                scan = AggroRules.CheckInterval;
                target = Find();
                if (target == null) return false;
                oldSenses = senses.enabled;
                navigation = new FollowNavigation(brain, mover);
                pursuit.Reset(); attack.MeleeTimer = 0;
                lastSeen = new GameObject("ANPC guard last seen position");
                lastSeen.transform.SetParent(brain.transform.parent, false);
                lastSeen.transform.position = target.transform.position;
                brain.State.hostile = false; brain.State.hostileUntil = 0;
                brain.CompanionMode(target, NpcMode.Fighting);
                Halt();
                AdvancedNpcsMod.Log(brain.Id + ": pursues wanted " + target.name + ".");
            }
            brain.Motor.enabled = false; attack.enabled = false; mover.enabled = true;
            brain.Motor.IsHostile = TargetsPlayer;
            senses.enabled = false; senses.Target = target; senses.SecondaryTarget = null;
            if (GameManager.IsGamePaused || GameManager.Instance.DisableAI || brain.EntityBehaviour.Entity.IsParalyzed)
            { mobile.DoMeleeDamage = false; mobile.ShootArrow = false; return true; }
            bool visible = brain.CanSee(target);
            if (visible) lastSeen.transform.position = target.transform.position;
            float gap = Vector3.Distance(brain.transform.position, target.transform.position);
            float reach = target == GameManager.Instance.PlayerEntityBehaviour ? attack.MeleeDistance : Mathf.Min(attack.MeleeDistance, attack.ClassicMeleeDistanceVsAI);
            if (pursuit.Tick(gap <= reach ? 1.5f : gap, visible, Time.deltaTime) || gap > AggroRules.GiveUpDistance(brain.Definition.AggroRange))
            { avoided = target; retryAt = Time.time + CompanionCombatRules.RetrySeconds; Release(); return true; }
            haltWait -= Time.deltaTime;
            if (haltWait <= 0) Halt();
            bool blow = mobile.DoMeleeDamage;
            mobile.DoMeleeDamage = false; mobile.ShootArrow = false;
            attack.MeleeTimer = Mathf.Max(0, attack.MeleeTimer - Time.deltaTime);
            if (visible && gap <= reach)
            {
                Vector3 facing = target.transform.position - brain.transform.position; facing.y = 0;
                if (facing.sqrMagnitude > 0.001f) brain.transform.rotation = Quaternion.LookRotation(facing);
                if (!mobile.IsPlayingOneShot()) mover.Stop();
                if (target == GameManager.Instance.PlayerEntityBehaviour)
                {
                    if (!GuardArrest.Demand(brain)) return true;
                }
                if (blow) Strike();
                if (!Legal(target)) { Release(); return true; }
                NpcSprite sprite = brain.GetComponent<NpcSprite>();
                if (attack.MeleeTimer <= 0 && !mobile.IsPlayingOneShot() && (sprite == null || !sprite.PlayingOneShot))
                {
                    mobile.ChangeEnemyState(MobileStates.PrimaryAttack);
                    if (sounds != null) sounds.PlayAttackSound();
                    attack.ResetMeleeTimer();
                }
            }
            else if (!brain.IsTurret && !mobile.IsPlayingOneShot())
            {
                Vector3 step, unused;
                // Never teleport a pursuing guard to catch its target.
                if (navigation.Tick(visible ? target.transform : lastSeen.transform, Time.deltaTime, false, out step, out unused))
                    mover.MoveToward(step, gait != null ? gait.RunSpeed : NpcMover.RunSpeed);
                else mover.Stop();
            }
            return true;
        }
        void Strike()
        {
            if (!Legal(target) || !brain.CanSee(target)) return;
            EnemyEntity entity = brain.EntityBehaviour.Entity as EnemyEntity;
            DaggerfallUnityItem weapon = entity.ItemEquipTable.GetItem(EquipSlots.RightHand);
            if (target != GameManager.Instance.PlayerEntityBehaviour)
            {
                if (weapon != null && target.Entity.MinMetalToHit > (WeaponMaterialTypes)weapon.NativeMaterialValue) weapon = null;
                // Native hit rolls, enchantments, blood, knockback and retaliation stay together.
                nativeHit.Invoke(attack, new object[] { weapon, brain.transform.forward, false });
                return;
            }
            PlayerEntity player = GameManager.Instance.PlayerEntity;
            int damage = FormulaHelper.CalculateAttackDamage(entity, player, false, 0, weapon);
            player.TallySkill(DaggerfallConnect.DFCareer.Skills.Dodging, 1);
            if (damage > 0 && entity.IsMagicallyConcealedNormalPower)
                EntityEffectManager.BreakNormalPowerConcealmentEffects(brain.EntityBehaviour);
            EntityEffectManager effects = brain.GetComponent<EntityEffectManager>();
            if (damage > 0 && weapon != null && weapon.IsEnchanted && effects != null)
                damage = effects.DoItemEnchantmentPayloads(EnchantmentPayloadFlags.Strikes, weapon, entity.Items, player.EntityBehaviour, damage);
            if (damage <= 0) { if (sounds != null) sounds.PlayMissSound(weapon); return; }
            if (player.CurrentHealth <= damage && player.SurrenderToCityGuards(false)) return;
            GameManager.Instance.PlayerObject.SendMessage("RemoveHealth", damage);
            if (weapon == null) weapon = entity.ItemEquipTable.GetItem(EquipSlots.LeftHand);
            GameManager.Instance.PlayerObject.SendMessage(weapon != null ? "PlayWeaponHitSound" : "PlayWeaponlessHitSound");
        }
    }
}

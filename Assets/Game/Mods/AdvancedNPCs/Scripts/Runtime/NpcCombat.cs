using System.Reflection;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>Native enemy with a health guard for the optional recruited-companion protection.</summary>
    public class NpcEnemyEntity : EnemyEntity
    {
        public NpcEnemyEntity(DaggerfallEntityBehaviour behaviour, EnemyEntity original) : base(behaviour)
        {
            // Preserve DFU's rolled stats, loot and mod changes without generating a second kit.
            for(System.Type type=typeof(EnemyEntity);type!=typeof(object);type=type.BaseType)
                foreach(var field in type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly))
                    if(field.Name!="equipTable" && !typeof(System.Delegate).IsAssignableFrom(field.FieldType))
                        field.SetValue(this,field.GetValue(original));
            equipTable.DeserializeEquipTable(original.ItemEquipTable.SerializeEquipTable(),items);
        }
        public override int SetHealth(int amount,bool restoreMode=false)
        {
            if(!restoreMode && amount<currentHealth && currentHealth>0)
            {
                var mod=AdvancedNpcsMod.Instance;
                var brain=EntityBehaviour.GetComponent<NpcBrain>();
                if(mod!=null && mod.Config.InvulnerableCompanions && brain!=null && brain.State!=null &&
                    !brain.State.dead && !brain.State.hostile && !brain.IsEnemy && brain.State.companion!=null &&
                    CompanionCombatRules.Contract(brain.State.companion.status)) return currentHealth;
            }
            return base.SetHealth(amount,restoreMode);
        }
    }

    /// <summary>
    /// Applies an ANPC's optional "combat" tuning (CombatRules): scaled max health and creature melee damage at
    /// spawn, and a faster melee rhythm while it lives (shortens each wait DFU's EnemyAttack sets after a swing).
    /// </summary>
    public class NpcCombat : MonoBehaviour
    {
        static readonly FieldInfo MobileEnemyField = typeof(EnemyEntity).GetField("mobileEnemy", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly MethodInfo SetMobileEnemy = typeof(MobileUnit).GetProperty("Enemy").GetSetMethod(true);

        EnemyAttack attack;
        float attackSpeed = 1f;
        float lastTimer;

        public static void ApplyCompanionHealth(NpcBrain brain)
        {
            var entity=brain.EntityBehaviour.Entity;
            int maximum=brain.State.companion.HealthForContract(entity.MaxHealth);
            if(maximum==entity.MaxHealth || entity.CurrentHealth<=0)return;
            float fraction=HealthRules.Fraction(entity.CurrentHealth,entity.MaxHealth);
            entity.MaxHealth=maximum;
            entity.SetHealth(HealthRules.Restore(entity.MaxHealth,fraction),true);
        }

        /// <summary>Call right after GameObjectHelper.CreateEnemy, before the brain restores saved health.</summary>
        public static void Apply(GameObject go, NpcDefinition def, string key)
        {
            if (!def.HasCombatTuning)
                return;
            DaggerfallEntityBehaviour behaviour = go.GetComponent<DaggerfallEntityBehaviour>();
            EnemyEntity entity = behaviour != null ? behaviour.Entity as EnemyEntity : null;
            if (entity == null)
                return;
            string report = key + ": combat";
            if (def.HealthScale != 1f)
            {
                int before = entity.MaxHealth;
                entity.MaxHealth = CombatRules.ScaleHealth(before, def.HealthScale);
                entity.CurrentHealth = entity.MaxHealth;
                report += " health " + before + "->" + entity.MaxHealth;
            }
            if (def.DamageScale != 1f && MobileEnemyField != null)
            {
                MobileEnemy m = (MobileEnemy)MobileEnemyField.GetValue(entity);
                string before = m.MinDamage + "-" + m.MaxDamage;
                CombatRules.ScaleDamage(m.MinDamage, m.MaxDamage, def.DamageScale, out m.MinDamage, out m.MaxDamage);
                CombatRules.ScaleDamage(m.MinDamage2, m.MaxDamage2, def.DamageScale, out m.MinDamage2, out m.MaxDamage2);
                CombatRules.ScaleDamage(m.MinDamage3, m.MaxDamage3, def.DamageScale, out m.MinDamage3, out m.MaxDamage3);
                MobileEnemyField.SetValue(entity, m);
                report += ", damage " + before + "->" + m.MinDamage + "-" + m.MaxDamage;
            }
            if (def.AttackSpeed != 1f)
            {
                NpcCombat pace = go.AddComponent<NpcCombat>();
                pace.attack = go.GetComponent<EnemyAttack>();
                pace.attackSpeed = def.AttackSpeed;
                report += ", attack speed x" + def.AttackSpeed;
            }
            AdvancedNpcsMod.Log(report + ".");
        }

        /// <summary>
        /// DFU gives every human class a bow (RangedAttack1) that EnemyMotor fires at a distant target. Unless npc.json
        /// asks for it ("combat": { "rangedAtDistance": true }) the bow is taken away here, before EnemyMotor.Start
        /// reads it. Spells keep working: they use HasSpellAnimation, not RangedAttack1.
        /// </summary>
        public static void ApplyRanged(GameObject go, NpcDefinition def)
        {
            if (def.RangedAtDistance || SetMobileEnemy == null)
                return;
            MobileUnit mobile = go.GetComponentInChildren<MobileUnit>();
            if (mobile == null || !mobile.Enemy.HasRangedAttack1)
                return;
            MobileEnemy m = mobile.Enemy;
            m.HasRangedAttack1 = false;
            SetMobileEnemy.Invoke(mobile, new object[] { m });
        }

        /// <summary>The NPC may shoot DFU's bow (self-test).</summary>
        public static bool HasBow(GameObject go)
        {
            MobileUnit mobile = go.GetComponentInChildren<MobileUnit>();
            return mobile != null && mobile.Enemy.HasRangedAttack1 && (!mobile.Enemy.CastsMagic || mobile.Enemy.HasRangedAttack2);
        }

        void LateUpdate()
        {
            if (attack == null)
                return;
            float timer = CombatRules.PacedMeleeTimer(lastTimer, attack.MeleeTimer, attackSpeed);
            if (timer != attack.MeleeTimer)
                attack.MeleeTimer = timer;
            lastTimer = timer;
        }
    }
}

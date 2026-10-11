using System.Reflection;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>
    /// Applies an ANPC's optional "combat" tuning (CombatRules): scaled max health and creature melee damage at
    /// spawn, and a faster melee rhythm while it lives (shortens each wait DFU's EnemyAttack sets after a swing).
    /// </summary>
    public class NpcCombat : MonoBehaviour
    {
        static readonly FieldInfo MobileEnemyField = typeof(EnemyEntity).GetField("mobileEnemy", BindingFlags.Instance | BindingFlags.NonPublic);

        EnemyAttack attack;
        float attackSpeed = 1f;
        float lastTimer;

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

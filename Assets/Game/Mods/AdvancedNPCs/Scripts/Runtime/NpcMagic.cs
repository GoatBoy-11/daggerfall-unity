using System.Collections.Generic;
using UnityEngine;
using DaggerfallConnect;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Formulas;
using DaggerfallWorkshop.Game.MagicAndEffects;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>Owns configured ANPC casting. Actual effects, missiles and costs remain DFU's.</summary>
    public class NpcMagic : MonoBehaviour
    {
        NpcBrain brain;
        NpcMagicDefinition config;
        NpcState state;
        DaggerfallEntityBehaviour behaviour;
        EntityEffectManager effects;
        EnemySenses senses;
        EnemyAttack attack;
        MobileUnit mobile;
        readonly List<EffectBundleSettings> spells = new List<EffectBundleSettings>();
        readonly List<EffectBundleSettings> candidates = new List<EffectBundleSettings>();
        bool entered;
        bool wasInCombat;
        float retry;
        public int CastCount { get; private set; }
        public int SpellCount { get { return spells.Count; } }

        public void Init(NpcBrain owner)
        {
            brain = owner;
            config = brain.Instance.Definition.Magic;
            state = brain.State;
            behaviour = GetComponent<DaggerfallEntityBehaviour>();
            effects = GetComponent<EntityEffectManager>();
            senses = GetComponent<EnemySenses>();
            attack = GetComponent<EnemyAttack>();
            mobile = GetComponentInChildren<MobileUnit>();
            // Empty the vanilla book immediately, before EnemyMotor.Start/FixedUpdate can use inherited spells.
            // The configured book stays private so both classic and enhanced AI cannot bypass our cooldown.
            behaviour.Entity.DeserializeSpellbook(new EffectBundleSettings[0]);
            behaviour.Entity.MaxMagicka = config.MaxMagicka;
            effects.UsePlayerCharacterSkillsForEnemyMagicCost = false;
            for (int skill = (int)DFCareer.Skills.Destruction; skill <= (int)DFCareer.Skills.Mysticism; skill++)
                behaviour.Entity.Skills.SetPermanentSkillValue((DFCareer.Skills)skill, (short)config.Skill);
            spells.AddRange(NpcSpellLibrary.Resolve(brain.Instance.Definition));
        }

        public void Capture()
        {
            if (entered && behaviour != null && behaviour.Entity != null)
                state.magicka = behaviour.Entity.CurrentMagicka;
        }

        void OnDisable() { Capture(); }

        void LateUpdate()
        {
            NpcMagicWorld world = AdvancedNpcsMod.Instance.MagicWorld;
            if (brain == null || brain.CurrentMode == NpcMode.Dead || !world.Refresh() || !world.Contains(this)) return;
            ulong now = DaggerfallUnity.Instance.WorldTime.Now.ToSeconds();
            Capture();
            bool returning = !entered || state.magicVisit != world.Visit;
            NpcMagicRules.Enter(config, state, world.Visit, now);
            if (returning)
            {
                NpcMagicRules.Recover(config, state, now, false);
                wasInCombat = false;
            }
            entered = true;
            bool combat = (brain.CurrentMode == NpcMode.Fighting || brain.CurrentMode == NpcMode.Fleeing) &&
                senses.Target != null && senses.DetectedTarget;
            NpcMagicRules.Recover(config, state, now, combat);
            behaviour.Entity.CurrentMagicka = state.magicka;
            state.magicCooldown = Mathf.Max(0, state.magicCooldown - Time.deltaTime);
            retry = Mathf.Max(0, retry - Time.deltaTime);
            if (combat && !wasInCombat)
                state.magicCooldown = Mathf.Max(state.magicCooldown, Random.Range(1f, 2.5f));
            wasInCombat = combat;
            if (state.magicCooldown > 0 || retry > 0 || !combat || brain.CurrentMode != NpcMode.Fighting ||
                GameManager.Instance.DisableAI || behaviour.Entity.IsParalyzed || behaviour.Entity.IsSilenced ||
                mobile.IsPlayingOneShot() || effects.HasReadySpell) return;
            retry = 0.25f;
            SelectCandidates();
            if (candidates.Count > 0) TryCast(candidates[Random.Range(0, candidates.Count)]);
        }

        public int Cost(EffectBundleSettings spell)
        {
            return FormulaHelper.CalculateTotalEffectCosts(spell.Effects, spell.TargetType, behaviour.Entity,
                spell.MinimumCastingCost).spellPointCost;
        }

        void SelectCandidates()
        {
            candidates.Clear();
            bool urgent = behaviour.Entity.CurrentHealth < behaviour.Entity.MaxHealth * 0.4f;
            foreach (EffectBundleSettings spell in spells)
            {
                if (!NpcMagicRules.CanAfford(config.UsesMagicka, behaviour.Entity.CurrentMagicka, Cost(spell))) continue;
                bool self = spell.TargetType == TargetTypes.CasterOnly;
                bool healing = HasEffect(spell, "Heal-Health") || HasEffect(spell, "Regenerate");
                if (self)
                {
                    if (healing && behaviour.Entity.CurrentHealth >= behaviour.Entity.MaxHealth * 0.6f) continue;
                    if (AlreadyActive(effects, spell)) continue;
                }
                else
                {
                    if (senses.Target == null || senses.Target.Entity.CurrentHealth <= 0 || !senses.TargetInSight) continue;
                    bool near = spell.TargetType == TargetTypes.ByTouch || spell.TargetType == TargetTypes.AreaAroundCaster;
                    if (near && senses.DistanceToTarget > attack.MeleeDistance) continue;
                    if (!near && (senses.DistanceToTarget > 25f || !brain.Motor.HasClearPathToShootProjectile(25f, DaggerfallMissile.ArmLength, 0.45f))) continue;
                    if (AlreadyActive(senses.Target.GetComponent<EntityEffectManager>(), spell)) continue;
                }
                // Heal first at critical health, otherwise randomly mix affordable, useful spells.
                if (urgent && self && healing)
                {
                    candidates.Clear();
                    candidates.Add(spell);
                    return;
                }
                candidates.Add(spell);
            }
        }

        /// <summary>Final checks and immediate release keep the affordability check and deduction together.</summary>
        public bool TryCast(EffectBundleSettings spell)
        {
            if (!entered || brain.CurrentMode != NpcMode.Fighting || behaviour.Entity.CurrentHealth <= 0 ||
                state.magicCooldown > 0 || effects.HasReadySpell || behaviour.Entity.IsParalyzed || behaviour.Entity.IsSilenced ||
                !NpcMagicRules.CanAfford(config.UsesMagicka, behaviour.Entity.CurrentMagicka, Cost(spell))) return false;
            // DFU's no-cost switch also bypasses silence, so enforce silence ourselves above.
            effects.UsePlayerCharacterSkillsForEnemyMagicCost = false;
            if (!effects.SetReadySpell(new EntityEffectBundle(spell, behaviour), !config.UsesMagicka)) return false;
            effects.CastReadySpell();
            // Only caster classes have DFU spell frames; the Spell state on any other class throws in DaggerfallMobileUnit.
            if (mobile.Enemy.HasSpellAnimation && mobile.Enemy.SpellAnimFrames != null)
                mobile.ChangeEnemyState(MobileStates.Spell);
            else
            {
                NpcSprite sprite = GetComponent<NpcSprite>();
                if (sprite != null)
                    sprite.PlayCast();
            }
            attack.ResetMeleeTimer();
            state.magicCooldown = Random.Range(config.CooldownMin, config.CooldownMax);
            Capture();
            CastCount++;
            return true;
        }

        static bool HasEffect(EffectBundleSettings spell, string key)
        {
            foreach (EffectEntry effect in spell.Effects) if (effect.Key == key) return true;
            return false;
        }

        static bool AlreadyActive(EntityEffectManager manager, EffectBundleSettings spell)
        {
            if (manager == null) return false;
            foreach (EffectEntry wanted in spell.Effects)
            {
                bool active = false;
                foreach (LiveEffectBundle bundle in manager.EffectBundles)
                    foreach (IEntityEffect effect in bundle.liveEffects)
                        if (effect.Key == wanted.Key && effect.RoundsRemaining > 0) active = true;
                if (!active) return false;
            }
            return true;
        }
    }
}

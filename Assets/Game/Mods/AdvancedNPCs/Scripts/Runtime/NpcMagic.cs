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
        bool Companion { get { return brain != null && brain.CompanionCombat != null && brain.CompanionCombat.Active; } }
        readonly NpcHealingDefinition companionHealing = new NpcHealingDefinition();
        NpcHealingDefinition Healing { get { return config.Healing ?? (Companion ? companionHealing : null); } }
        EffectBundleSettings[] inheritedBook;
        bool inheritedPlayerCosts;
        bool entered;
        bool wasInCombat;
        float retry;
        Collider[] nearby = new Collider[32];
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
            if (config == null)
            {
                // Class spells remain native outside the contract. Capture the complete book for dismissal,
                // but only permit the same supported combat effects as explicitly configured casters.
                inheritedBook = behaviour.Entity.SerializeSpellbook();
                inheritedPlayerCosts = effects.UsePlayerCharacterSkillsForEnemyMagicCost;
                config = new NpcMagicDefinition { Recovery = "rest", MaxMagicka = behaviour.Entity.MaxMagicka };
                foreach (EffectBundleSettings spell in inheritedBook)
                    if (NpcSpellLibrary.Supported(spell)) spells.Add(spell);
                if (!state.magicInitialized)
                {
                    state.magicka = behaviour.Entity.CurrentMagicka;
                    state.magicInitialized = true;
                    state.magicUpdatedAt = DaggerfallUnity.Instance.WorldTime.Now.ToSeconds();
                    state.magicRestAfter = state.magicUpdatedAt;
                }
            }
            // Empty the vanilla book immediately, before EnemyMotor.Start/FixedUpdate can use inherited spells.
            // The configured book stays private so both classic and enhanced AI cannot bypass our cooldown.
            behaviour.Entity.DeserializeSpellbook(new EffectBundleSettings[0]);
            behaviour.Entity.MaxMagicka = config.MaxMagicka;
            effects.UsePlayerCharacterSkillsForEnemyMagicCost = false;
            if (inheritedBook == null)
            {
                for (int skill = (int)DFCareer.Skills.Destruction; skill <= (int)DFCareer.Skills.Mysticism; skill++)
                    behaviour.Entity.Skills.SetPermanentSkillValue((DFCareer.Skills)skill, (short)config.Skill);
                spells.AddRange(NpcSpellLibrary.Resolve(brain.Instance.Definition));
            }
        }

        public void Capture()
        {
            if (entered && behaviour != null && behaviour.Entity != null)
                state.magicka = behaviour.Entity.CurrentMagicka;
        }

        public static void EnsureCompanion(NpcBrain owner, bool hiring = false)
        {
            if (owner.GetComponent<NpcMagic>() != null || owner.CompanionCombat == null || !owner.CompanionCombat.Active ||
                owner.EntityBehaviour.Entity.SpellbookCount() == 0) return;
            if (hiring)
            {
                // A live native caster may have spent mana since dismissal. Respawn restores the saved pool;
                // recruitment instead adopts the actual person in front of the player, with no off-contract credit.
                owner.State.magicka = owner.EntityBehaviour.Entity.CurrentMagicka;
                owner.State.magicInitialized = true;
                owner.State.magicUpdatedAt = DaggerfallUnity.Instance.WorldTime.Now.ToSeconds();
            }
            owner.gameObject.AddComponent<NpcMagic>().Init(owner);
        }

        // A new hire starts timed recovery now, without granting old off-contract time or a refill.
        public void BeginContract()
        {
            Capture();
            state.magicUpdatedAt = DaggerfallUnity.Instance.WorldTime.Now.ToSeconds();
        }

        void OnDisable() { Capture(); }

        void LateUpdate()
        {
            if (inheritedBook != null && !Companion)
            {
                Capture();
                behaviour.Entity.DeserializeSpellbook(inheritedBook);
                effects.UsePlayerCharacterSkillsForEnemyMagicCost = inheritedPlayerCosts;
                enabled = false;
                Destroy(this);
                return;
            }
            NpcMagicWorld world = AdvancedNpcsMod.Instance.MagicWorld;
            if (brain == null || brain.CurrentMode == NpcMode.Dead || GameManager.IsGamePaused || !world.Refresh() || !world.Contains(this)) return;
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
                (Companion ? brain.Threat != null : senses.Target != null && senses.DetectedTarget);
            NpcMagicRules.Recover(config, state, now, combat);
            behaviour.Entity.CurrentMagicka = state.magicka;
            state.magicCooldown = Mathf.Max(0, state.magicCooldown - Time.deltaTime);
            retry = Mathf.Max(0, retry - Time.deltaTime);
            if (combat && !wasInCombat)
                state.magicCooldown = Mathf.Max(state.magicCooldown, Random.Range(1f, 2.5f));
            wasInCombat = combat;
            NpcSprite sprite = GetComponent<NpcSprite>();
            if (state.magicCooldown > 0 || retry > 0 || GameManager.Instance.DisableAI || !CanRelease() ||
                mobile.IsPlayingOneShot() || (sprite != null && sprite.PlayingOneShot)) return;
            retry = 0.25f;
            candidates.Clear();
            if (Companion)
            {
                CompanionDecision(combat);
                return;
            }
            if (combat && brain.CurrentMode == NpcMode.Fighting) SelectCandidates();
            // Preserve emergency self healing before spending this shared cooldown on someone else.
            if (behaviour.Entity.CurrentHealth < behaviour.Entity.MaxHealth * 0.4f && candidates.Count > 0 &&
                candidates[0].TargetType == TargetTypes.CasterOnly && IsHealing(candidates[0]))
            {
                TryCast(candidates[0]);
                return;
            }
            EffectBundleSettings healingSpell;
            DaggerfallEntityBehaviour recipient;
            if (FindHealing(out healingSpell, out recipient) && TryHeal(healingSpell, recipient)) return;
            if (candidates.Count > 0) TryCast(candidates[Random.Range(0, candidates.Count)]);
        }

        bool PartySupport(DaggerfallEntityBehaviour recipient)
        {
            if (!Companion || recipient == null || !recipient.isActiveAndEnabled || recipient.Entity == null || recipient.Entity.CurrentHealth <= 0 ||
                !brain.CompanionCombat.PartyMember(recipient) || !SameHealingArea(recipient)) return false;
            return recipient == behaviour || (Vector3.Distance(transform.position, recipient.transform.position) <= NpcHealingRules.Range && brain.CanSee(recipient));
        }

        void CompanionDecision(bool combat)
        {
            // Emergency self care also works while retreating; all support uses this same mana and cooldown.
            if (behaviour.Entity.CurrentHealth < behaviour.Entity.MaxHealth * .4f)
                foreach (EffectBundleSettings spell in spells)
                    if (NpcSpellLibrary.IsSelfHealing(spell) && TrySelfHeal(spell)) return;
            EffectBundleSettings heal;
            DaggerfallEntityBehaviour recipient;
            if (FindHealing(out heal, out recipient) && TryHeal(heal, recipient)) return;
            foreach (EffectBundleSettings spell in spells)
                if (NpcSpellLibrary.IsSelfHealing(spell) && TrySelfHeal(spell)) return;
            if (!combat) return; // Do not spend protective buffs repeatedly during ordinary travel.
            foreach (EffectBundleSettings spell in spells)
            {
                if (!NpcSpellLibrary.IsProtection(spell)) continue;
                if (TryBuff(spell, behaviour) || TryBuff(spell, GameManager.Instance.PlayerEntityBehaviour)) return;
                int count = Physics.OverlapSphereNonAlloc(transform.position, NpcHealingRules.Range, nearby, ~0, QueryTriggerInteraction.Ignore);
                while (count == nearby.Length && nearby.Length < 512)
                {
                    nearby = new Collider[nearby.Length * 2];
                    count = Physics.OverlapSphereNonAlloc(transform.position, NpcHealingRules.Range, nearby, ~0, QueryTriggerInteraction.Ignore);
                }
                for (int i = 0; i < count; i++)
                    if (TryBuff(spell, nearby[i].GetComponentInParent<DaggerfallEntityBehaviour>())) return;
            }
            if (!brain.CompanionCombat.AllowsAttack) return;
            foreach (EffectBundleSettings spell in spells)
                if (spell.TargetType != TargetTypes.CasterOnly && OffensiveUseful(spell)) candidates.Add(spell);
            if (candidates.Count > 0) TryCast(candidates[Random.Range(0, candidates.Count)]);
        }

        bool TrySelfHeal(EffectBundleSettings spell)
        {
            if (!CanRelease() || !NpcSpellLibrary.IsSelfHealing(spell) || !CanHeal(behaviour) || AlreadyActive(effects, spell) ||
                !NpcMagicRules.CanAfford(config.UsesMagicka, behaviour.Entity.CurrentMagicka, Cost(spell))) return false;
            if (config.UsesMagicka) behaviour.Entity.DecreaseMagicka(Cost(spell));
            effects.AssignBundle(new EntityEffectBundle(spell, behaviour), AssignBundleFlags.BypassSavingThrows);
            FinishCast(true);
            return true;
        }

        public bool TryBuff(EffectBundleSettings spell, DaggerfallEntityBehaviour recipient)
        {
            if (!CanRelease() || !NpcSpellLibrary.IsProtection(spell) || !PartySupport(recipient) ||
                brain.Threat == null || AlreadyActive(recipient.GetComponent<EntityEffectManager>(), spell)) return false;
            EffectBundleSettings support = recipient == behaviour ? spell : SupportSpell(spell);
            int cost = Cost(support);
            if (!NpcMagicRules.CanAfford(config.UsesMagicka, behaviour.Entity.CurrentMagicka, cost)) return false;
            EntityEffectManager manager = recipient.GetComponent<EntityEffectManager>();
            if (manager == null) return false;
            if (config.UsesMagicka) behaviour.Entity.DecreaseMagicka(cost);
            support.TargetType = TargetTypes.CasterOnly;
            manager.AssignBundle(new EntityEffectBundle(support, behaviour), AssignBundleFlags.BypassSavingThrows);
            FinishCast(false);
            return true;
        }

        bool OffensiveUseful(EffectBundleSettings spell)
        {
            if (!NpcSpellLibrary.Supported(spell) || spell.TargetType == TargetTypes.CasterOnly || !brain.CompanionCombat.AllowsAttack ||
                !NpcMagicRules.CanAfford(config.UsesMagicka, behaviour.Entity.CurrentMagicka, Cost(spell)) || senses.Target == null ||
                AlreadyActive(senses.Target.GetComponent<EntityEffectManager>(), spell)) return false;
            float distance = Vector3.Distance(transform.position, senses.Target.transform.position);
            if (brain.CompanionCombat.Bow.CanHoldRange(distance)) return false;
            bool near = spell.TargetType == TargetTypes.ByTouch || spell.TargetType == TargetTypes.AreaAroundCaster;
            if (distance > (near ? Mathf.Min(NpcHealingRules.Range, attack.MeleeDistance) : 25f)) return false;
            return brain.CompanionCombat.ClearShot(senses.Target);
        }

        bool TryCompanionSpell(EffectBundleSettings spell)
        {
            if (NpcSpellLibrary.IsSelfHealing(spell)) return TrySelfHeal(spell);
            if (NpcSpellLibrary.IsProtection(spell)) return TryBuff(spell, behaviour);
            if (!CanRelease() || !OffensiveUseful(spell)) return false;
            if (!CompanionSpellMissile.Launch(brain, effects, spell, senses.Target)) return false;
            if (config.UsesMagicka) behaviour.Entity.DecreaseMagicka(Cost(spell));
            effects.PlayCastSound(behaviour, effects.GetCastSoundID(spell.ElementType));
            FinishCast(false);
            return true;
        }

        float rangeCheck;
        int rangeMana;
        DaggerfallEntityBehaviour rangeTarget;
        bool rangeReady;
        // Cache planning for a quarter second; every actual release still checks resources and sight afresh.
        public bool HoldsRange(float distance)
        {
            if (!Companion || !brain.CompanionCombat.AllowsAttack || distance < 3f || distance > 25f ||
                brain.CompanionCombat.Bow.CanHoldRange(distance) || behaviour.Entity.IsSilenced || behaviour.Entity.IsParalyzed) return false;
            if (Time.time < rangeCheck && rangeMana == behaviour.Entity.CurrentMagicka && rangeTarget == senses.Target) return rangeReady;
            rangeCheck = Time.time + .25f;
            rangeMana = behaviour.Entity.CurrentMagicka;
            rangeTarget = senses.Target;
            rangeReady = false;
            foreach (EffectBundleSettings spell in spells)
                if ((spell.TargetType == TargetTypes.SingleTargetAtRange || spell.TargetType == TargetTypes.AreaAtRange) &&
                    NpcMagicRules.CanAfford(config.UsesMagicka, rangeMana, Cost(spell)) &&
                    !AlreadyActive(senses.Target.GetComponent<EntityEffectManager>(), spell)) { rangeReady = true; break; }
            return rangeReady;
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

        bool CanRelease()
        {
            return entered && !brain.EnforcingLaw && !GameManager.IsGamePaused && !brain.PackageOnlyCombatInterrupts &&
                (!Companion || !brain.CompanionCombat.Regrouping) &&
                (brain.CurrentMode == NpcMode.Fighting || brain.CurrentMode == NpcMode.Calm || (Companion && brain.CurrentMode == NpcMode.Fleeing)) &&
                behaviour.Entity.CurrentHealth > 0 && state.magicCooldown <= 0 && !effects.HasReadySpell &&
                !behaviour.Entity.IsParalyzed && !behaviour.Entity.IsSilenced;
        }

        /// <summary>Normal spells remain combat-only and release through DFU.</summary>
        public bool TryCast(EffectBundleSettings spell)
        {
            if (Companion) return TryCompanionSpell(spell);
            if ((brain.CompanionCombat!=null && brain.CompanionCombat.Active && !brain.CompanionCombat.AllowsAttack) ||
                !CanRelease() || brain.CurrentMode != NpcMode.Fighting ||
                !NpcMagicRules.CanAfford(config.UsesMagicka, behaviour.Entity.CurrentMagicka, Cost(spell))) return false;
            effects.UsePlayerCharacterSkillsForEnemyMagicCost = false;
            if (!effects.SetReadySpell(new EntityEffectBundle(spell, behaviour), !config.UsesMagicka)) return false;
            effects.CastReadySpell();
            FinishCast(IsHealing(spell));
            return true;
        }

        /// <summary>A copy is costed as a touch spell; the original caster-only spell is never changed.</summary>
        public EffectBundleSettings SupportSpell(EffectBundleSettings spell)
        {
            spell.TargetType = TargetTypes.ByTouch;
            return spell;
        }

        public bool TryHeal(EffectBundleSettings spell, DaggerfallEntityBehaviour recipient)
        {
            if (!CanRelease() || !NpcSpellLibrary.IsHealthHeal(spell) || !CanHeal(recipient)) return false;
            EffectBundleSettings support = recipient == behaviour ? spell : SupportSpell(spell);
            int cost = Cost(support);
            if (!NpcMagicRules.CanAfford(config.UsesMagicka, behaviour.Entity.CurrentMagicka, cost)) return false;
            EntityEffectManager recipientEffects = recipient.GetComponent<EntityEffectManager>();
            if (recipientEffects == null) return false;
            // Direct touch delivery keeps EnemySenses.Target (and physical attacks) pointed at the foe.
            // Willing recipients receive the same restorative effect as a self heal. DFU's magnitude
            // remains based on the actual healer, but beneficial healing must not be saved against or reflected.
            if (config.UsesMagicka) behaviour.Entity.DecreaseMagicka(cost);
            EffectBundleSettings delivery = support;
            delivery.TargetType = TargetTypes.CasterOnly;
            recipientEffects.AssignBundle(new EntityEffectBundle(delivery, behaviour), AssignBundleFlags.BypassSavingThrows);
            FinishCast(true);
            return true;
        }

        bool CanHeal(DaggerfallEntityBehaviour recipient)
        {
            NpcHealingDefinition h = Healing;
            if (Companion)
            {
                if (h == null || !PartySupport(recipient) || !NpcHealingRules.NeedsHealing(recipient.Entity.CurrentHealth, recipient.Entity.MaxHealth, h.HealthThreshold)) return false;
                if (recipient == behaviour) return true;
                if ((brain.CurrentMode != NpcMode.Fighting && brain.CurrentMode != NpcMode.Fleeing) && !h.OutsideCombat) return false;
                return recipient == GameManager.Instance.PlayerEntityBehaviour ? h.Player == "friendly" : h.Allies;
            }
            if (h == null || recipient == null || recipient == behaviour || !recipient.isActiveAndEnabled ||
                recipient.Entity == null || !NpcHealingRules.NeedsHealing(recipient.Entity.CurrentHealth, recipient.Entity.MaxHealth, h.HealthThreshold) ||
                (brain.CurrentMode != NpcMode.Fighting && !h.OutsideCombat) ||
                Vector3.Distance(transform.position, recipient.transform.position) > NpcHealingRules.Range ||
                !SameHealingArea(recipient) ||
                senses.Target == recipient || !brain.CanSee(recipient)) return false;
            if (recipient == GameManager.Instance.PlayerEntityBehaviour)
                return h.Player == "friendly" && NpcHealingRules.FriendlyPlayer(brain.IsEnemy, state.hostile,
                    NpcGreeter.PlayerReactionBand(brain.Opinion));
            if (!h.Allies) return false;
            EnemySenses otherSenses = recipient.GetComponent<EnemySenses>();
            bool fightingUs = otherSenses != null && otherSenses.Target == behaviour;
            NpcBrain other = recipient.GetComponent<NpcBrain>();
            if (other != null)
                return other.CurrentMode != NpcMode.Dead && NpcHealingRules.Allies(brain.IsEnemy, state.hostile,
                    other.IsEnemy, other.State.hostile, fightingUs || brain.Threat == recipient || other.Threat == behaviour);
            // Vanilla allies are explicit player-side teams; strangers and unrelated creatures are not allies.
            EnemyMotor otherMotor = recipient.GetComponent<EnemyMotor>();
            return !brain.IsEnemy && !state.hostile && !fightingUs && otherMotor != null && !otherMotor.IsHostile &&
                (recipient.Entity.Team == MobileTeams.CityWatch || recipient.Entity.Team == MobileTeams.PlayerAlly);
        }

        bool SameHealingArea(DaggerfallEntityBehaviour recipient)
        {
            NpcMagicWorld world = AdvancedNpcsMod.Instance.MagicWorld;
            if (!world.Contains(this)) return false;
            // The player stays under the global player root, even while visiting an interior or dungeon.
            if (recipient == GameManager.Instance.PlayerEntityBehaviour) return true;
            return GetComponentInParent<DaggerfallInterior>() == recipient.GetComponentInParent<DaggerfallInterior>() &&
                GetComponentInParent<DaggerfallDungeon>() == recipient.GetComponentInParent<DaggerfallDungeon>();
        }

        bool FindHealing(out EffectBundleSettings spell, out DaggerfallEntityBehaviour recipient)
        {
            spell = new EffectBundleSettings();
            recipient = null;
            if (Healing == null) return false;
            bool found = false;
            foreach (EffectBundleSettings candidate in spells)
            {
                if (!NpcSpellLibrary.IsHealthHeal(candidate) ||
                    !NpcMagicRules.CanAfford(config.UsesMagicka, behaviour.Entity.CurrentMagicka, Cost(SupportSpell(candidate)))) continue;
                spell = candidate;
                found = true;
                break;
            }
            if (!found) return false;
            float fraction = 1f;
            float distance = float.MaxValue;
            if (Companion) ConsiderHealing(behaviour, ref recipient, ref fraction, ref distance);
            ConsiderHealing(GameManager.Instance.PlayerEntityBehaviour, ref recipient, ref fraction, ref distance);
            int count = Physics.OverlapSphereNonAlloc(transform.position, NpcHealingRules.Range, nearby, ~0, QueryTriggerInteraction.Ignore);
            while (count == nearby.Length && nearby.Length < 512)
            {
                nearby = new Collider[nearby.Length * 2];
                count = Physics.OverlapSphereNonAlloc(transform.position, NpcHealingRules.Range, nearby, ~0, QueryTriggerInteraction.Ignore);
            }
            for (int i = 0; i < count; i++)
                ConsiderHealing(nearby[i].GetComponentInParent<DaggerfallEntityBehaviour>(), ref recipient, ref fraction, ref distance);
            return recipient != null;
        }

        void ConsiderHealing(DaggerfallEntityBehaviour other, ref DaggerfallEntityBehaviour best, ref float fraction, ref float distance)
        {
            if (!CanHeal(other) || other.GetComponent<EntityEffectManager>() == null) return;
            float health = (float)other.Entity.CurrentHealth / other.Entity.MaxHealth;
            float d = (transform.position - other.transform.position).sqrMagnitude;
            if (health < fraction || (health == fraction && d < distance))
            {
                best = other;
                fraction = health;
                distance = d;
            }
        }

        static bool IsHealing(EffectBundleSettings spell)
        {
            return HasEffect(spell, "Heal-Health") || HasEffect(spell, "Regenerate");
        }

        void FinishCast(bool healing)
        {
            if (Companion) brain.CompanionCombat.Bow.Cancel();
            mobile.DoMeleeDamage = false;
            mobile.ShootArrow = false;
            // Custom sets choose heal -> cast -> attack -> none; vanilla casters use their spell frames.
            if (mobile.Enemy.HasSpellAnimation && mobile.Enemy.SpellAnimFrames != null)
                mobile.ChangeEnemyState(MobileStates.Spell);
            NpcSprite sprite = GetComponent<NpcSprite>();
            if (sprite != null) sprite.PlayCast(healing);
            if (brain.CurrentMode == NpcMode.Calm)
                brain.HoldStill(Mathf.Max(1f, sprite != null ? sprite.CastSeconds : 0f));
            attack.ResetMeleeTimer();
            NpcVoice castVoice = GetComponent<NpcVoice>();
            if (castVoice != null) castVoice.Play(healing ? SoundPools.Heal : SoundPools.Cast);
            state.magicCooldown = Random.Range(config.CooldownMin, config.CooldownMax);
            Capture();
            CastCount++;
        }

        static bool HasEffect(EffectBundleSettings spell, string key)
        {
            foreach (EffectEntry effect in spell.Effects) if (effect.Key == key) return true;
            return false;
        }

        static bool AlreadyActive(EntityEffectManager manager, EffectBundleSettings spell)
        {
            if (manager == null) return false;
            LiveEffectBundle[] bundles = manager.EffectBundles;
            foreach (EffectEntry wanted in spell.Effects)
            {
                bool active = false;
                foreach (LiveEffectBundle bundle in bundles)
                    foreach (IEntityEffect effect in bundle.liveEffects)
                        if (effect.Key == wanted.Key && effect.RoundsRemaining > 0) active = true;
                if (!active) return false;
            }
            return true;
        }
    }
}

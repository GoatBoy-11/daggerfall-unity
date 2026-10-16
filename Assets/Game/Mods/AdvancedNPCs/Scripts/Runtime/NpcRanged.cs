using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>
    /// An ANPC's own ranged attack ("combat": { "rangedAtDistance": true }): flies NpcProjectile instead of DFU's arrow
    /// when it needs to, that is for a creature (DFU gives creatures no ranged attack, so it throws here), for a set with
    /// projectile* images or for a "projectileDamage" override. A human class without either keeps DFU's own arrow and
    /// gets no NpcRanged at all.
    /// </summary>
    public class NpcRanged : MonoBehaviour
    {
        /// <summary>DFU's bow: a 1 in 32 chance per classic update while the target is in range and in front.</summary>
        const float ThrowChance = 1f / 32f;
        const float FacingDegrees = 22.5f;

        MobileUnit mobile;
        EnemySenses senses;
        DaggerfallEntityBehaviour behaviour;
        NpcBrain brain;
        NpcSprite sprite;
        bool creature;
        bool throwing;
        int shots;

        /// <summary>Damage of a hit, min to max; 0 = DFU's own (EnemyAttack.BowDamage).</summary>
        public int DamageMin;
        public int DamageMax;

        /// <summary>Whether an ANPC needs its own ranged attack (see the class summary).</summary>
        public static bool Wanted(NpcDefinition def, LoadedSpriteSet set)
        {
            return def.RangedAtDistance && (def.IsCreature || def.ProjectileDamageMax > 0 || (set != null && set.Set.Projectiles.Count > 0));
        }

        public void Init(NpcDefinition def)
        {
            creature = def.IsCreature;
            DamageMin = def.ProjectileDamageMin;
            DamageMax = def.ProjectileDamageMax;
            Find();
        }

        void Find()
        {
            DaggerfallEnemy enemy = GetComponent<DaggerfallEnemy>();
            mobile = enemy != null ? enemy.MobileUnit : null;
            senses = GetComponent<EnemySenses>();
            behaviour = GetComponent<DaggerfallEntityBehaviour>();
            brain = GetComponent<NpcBrain>();
            sprite = GetComponent<NpcSprite>();
        }

        /// <summary>A creature's current attack is a throw, not a swing.</summary>
        public bool Throwing
        {
            get { return throwing; }
        }

        /// <summary>Projectiles fired so far (self-test).</summary>
        public int Shots
        {
            get { return shots; }
        }

        void Start()
        {
            if (sprite == null)
                sprite = GetComponent<NpcSprite>();
        }

        void Update()
        {
            if (brain != null && brain.EnforcingLaw) return;
            if(brain!=null && brain.CompanionCombat!=null && brain.CompanionCombat.Active)return;
            if (!creature || mobile == null || senses == null)
                return;
            if (throwing && mobile.EnemyState != MobileStates.PrimaryAttack)
                throwing = false;
            if (throwing || mobile.IsPlayingOneShot() || !GameManager.ClassicUpdate)
                return;
            if (behaviour == null || behaviour.Entity == null || behaviour.Entity.IsParalyzed)
                return;
            if (brain != null && brain.CurrentMode != NpcMode.Fighting)
                return;
            // EnemyMotor.DoRangedAttack's rules for a bow, which DFU never applies to creatures.
            if (senses.Target == null || !senses.TargetInSight || !senses.DetectedTarget ||
                !CombatRules.InThrowRange(senses.DistanceToTarget) || !senses.TargetIsWithinYawAngle(FacingDegrees, senses.LastKnownTargetPos))
                return;
            if (Random.value >= ThrowChance)
                return;
            Throw();
        }

        /// <summary>Starts a throw: the creature's attack state (its melee blow is turned into the projectile).</summary>
        public void Throw()
        {
            if (mobile == null)
                return;
            throwing = true;
            mobile.ChangeEnemyState(MobileStates.PrimaryAttack);
        }

        /// <summary>
        /// Without a drawn sprite set nothing times the blow, so DFU's own signal (raised by its animation before
        /// LateUpdate, taken by EnemyAttack.Update the frame after) becomes the projectile here.
        /// </summary>
        void LateUpdate()
        {
            if (mobile == null || (sprite != null && sprite.HasSet))
                return;
            TakeVanillaBlow();
        }

        /// <summary>Turns DFU's shot (or a throw's melee blow) into this projectile; true if there was one.</summary>
        public bool TakeVanillaBlow()
        {
            if (mobile.ShootArrow || (throwing && mobile.DoMeleeDamage))
            {
                mobile.ShootArrow = false;
                mobile.DoMeleeDamage = false;
                Fire();
                return true;
            }
            return false;
        }

        /// <summary>Flies one projectile at the current target: a random projectile* image of the set, else DFU's arrow.</summary>
        public void Fire()
        {
            if (brain != null && brain.EnforcingLaw) return;
            if(brain!=null && brain.CompanionCombat!=null && brain.CompanionCombat.Active)
            {brain.CompanionCombat.Bow.Fire();return;}
            if (senses == null || behaviour == null)
                Find();
            if (senses == null || behaviour == null)
                return;
            shots++;
            NpcProjectile.Launch(behaviour, senses, PickLook(), DamageMin, DamageMax);
            // A bow twangs; a thrown rock is heard through the ANPC's attack_range voice instead.
            if (!creature)
            {
                DaggerfallAudioSource audio = GetComponent<DaggerfallAudioSource>();
                if (audio != null)
                    audio.PlayOneShot((int)SoundClips.ArrowShoot, 1, 1.0f);
            }
        }

        ProjectileLook PickLook()
        {
            LoadedSpriteSet set = sprite != null ? sprite.Set : null;
            if (set == null || !set.IsReady)
                return null;
            List<SpriteAnimation> ready = new List<SpriteAnimation>();
            foreach (SpriteAnimation p in set.Set.Projectiles.Values)
            {
                if (set.Sheets.ContainsKey(p.Name))
                    ready.Add(p);
            }
            if (ready.Count == 0)
                return null;
            ready.Sort(delegate (SpriteAnimation x, SpriteAnimation y) { return string.CompareOrdinal(x.Name, y.Name); });
            SpriteAnimation pick = ready[Random.Range(0, ready.Count)];
            ProjectileLook look = new ProjectileLook();
            look.Anim = pick;
            look.Texture = set.Sheets[pick.Name];
            look.Material = sprite.MaterialTemplate;
            // Same scale as the person: world size per Blender unit = worldPerPixel * the set's pixelsPerUnit.
            float ppu = pick.PixelsPerUnit > 0 ? pick.PixelsPerUnit : set.Set.PixelsPerUnit;
            look.WorldPerPixel = sprite.WorldPerPixel * set.Set.PixelsPerUnit / ppu;
            look.Fps = pick.Fps > 0 ? pick.Fps : set.Set.Fps;
            return look;
        }
    }
}

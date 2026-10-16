using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using DaggerfallConnect;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Formulas;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Utility;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>A projectile* image to fly, with the scale of the person who threw it (null: DFU's arrow model).</summary>
    public class ProjectileLook
    {
        public SpriteAnimation Anim;
        public Texture2D Texture;
        /// <summary>The thrower's sprite material (DFU's billboard shader, lighting and fog); copied.</summary>
        public Material Material;
        public float WorldPerPixel;
        public int Fps;
    }

    /// <summary>
    /// One ANPC ranged shot (NpcRanged): flies straight like DFU's arrow (DaggerfallMissile: same speed, aim, start
    /// point and range), drawn as a projectile* sprite or DFU's arrow model. Hitting the shooter's target deals DFU's
    /// own bow damage (EnemyAttack.BowDamage), or with "projectileDamage" DFU's to-hit roll and the given amount.
    /// </summary>
    public class NpcProjectile : MonoBehaviour
    {
        const float Speed = 25f;                    // DaggerfallMissile.MovementSpeed
        const float MaxDistance = 2048 * MeshReader.GlobalScale + 10f;
        const int ArrowModel = 99800;
        static int layerMask = -1;
        static readonly RaycastHit[] flightHits=new RaycastHit[64];
        static readonly FieldInfo DamageField = typeof(EnemyAttack).GetField("damage", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly MethodInfo SendDamageToPlayer = typeof(EnemyAttack).GetMethod("SendDamageToPlayer", BindingFlags.Instance | BindingFlags.NonPublic);

        static readonly MethodInfo CompanionDamage=typeof(EnemyAttack).GetMethod("ApplyDamageToNonPlayer",BindingFlags.Instance|BindingFlags.NonPublic);
        public static bool CompanionDamageAvailable {get{return CompanionDamage!=null;}}
        NpcCompanionCombat companion;
        DaggerfallEntityBehaviour shotTarget;
        DaggerfallUnityItem shotBow,spentArrow;
        public void BindCompanion(NpcCompanionCombat owner,DaggerfallEntityBehaviour target,DaggerfallUnityItem bow,DaggerfallUnityItem arrow)
        {
            companion=owner;shotTarget=target;shotBow=bow;spentArrow=arrow;
            // Follow the loaded area's floating-origin shifts, but never the shooter's later movement.
            transform.SetParent(caster.transform.parent,true);
        }
        DaggerfallEntityBehaviour caster;
        EnemySenses senses;
        int damageMin, damageMax;
        Vector3 direction;
        float travelled;
        ProjectileLook look;
        GameObject quad;
        Material material;
        float time;

        /// <summary>Live projectiles (self-test).</summary>
        public static readonly List<NpcProjectile> Flying = new List<NpcProjectile>();

        public bool Custom
        {
            get { return look != null; }
        }

        public Vector3 Direction
        {
            get { return direction; }
        }

        public static NpcProjectile Launch(DaggerfallEntityBehaviour caster, EnemySenses senses, ProjectileLook look, int damageMin, int damageMax)
        {
            if (layerMask == -1)
                layerMask = Physics.DefaultRaycastLayers & ~(1 << LayerMask.NameToLayer("Automap"));
            GameObject go = new GameObject(look != null ? "AnpcProjectile " + look.Anim.Name : "AnpcProjectile arrow");
            NpcProjectile p = go.AddComponent<NpcProjectile>();
            p.caster = caster;
            p.senses = senses;
            p.look = look;
            p.damageMin = damageMin;
            p.damageMax = damageMax;
            Transform t = caster.transform;
            p.direction = Aim(t, senses);
            // DaggerfallMissile's arrow start: in front of the shooter, a third of its height up (where LOS is checked).
            Vector3 start = t.position + t.forward * 0.6f;
            CharacterController controller = caster.GetComponent<CharacterController>();
            if (controller != null)
                start.y += controller.height / 3f;
            go.transform.position = start;
            try {p.Dress();}
            catch {Destroy(go);throw;}
            Flying.Add(p);
            return p;
        }

        /// <summary>DaggerfallMissile.GetAimDirection for an enemy's arrow.</summary>
        static Vector3 Aim(Transform caster, EnemySenses senses)
        {
            if (senses.Target == null || senses.LastKnownTargetPos == EnemySenses.ResetPlayerPos)
                return caster.forward;
            Vector3 predicted = DaggerfallUnity.Settings.EnhancedCombatAI ? senses.PredictNextTargetPos(Speed) : senses.LastKnownTargetPos;
            Vector3 aim = predicted == EnemySenses.ResetPlayerPos ? caster.forward : (predicted - caster.position).normalized;
            if (senses.Target != null && senses.Target.EntityType == EntityTypes.Player && GameManager.Instance.PlayerMotor.IsCrouching)
                aim += Vector3.down * 0.05f;
            return aim.normalized;
        }

        void Dress()
        {
            if (look == null)
            {
                GameObject arrow = GameObjectHelper.CreateDaggerfallMeshGameObject(ArrowModel, transform, ignoreCollider: true);
                arrow.transform.localPosition = Vector3.zero;
                arrow.transform.rotation = Quaternion.LookRotation(direction);
                return;
            }
            quad = NpcSprite.CreateQuad(transform, "AnpcProjectileSprite");
            MeshRenderer r = quad.GetComponent<MeshRenderer>();
            material = look.Material != null ? new Material(look.Material) : new Material(Shader.Find("Sprites/Default"));
            material.mainTexture = NpcSprite.Filtered(look.Texture);
            material.mainTextureScale = new Vector2(1f / look.Anim.Frames, 1f / look.Anim.Rows);
            r.sharedMaterial = material;
            r.receiveShadows = false;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            quad.transform.localScale = new Vector3(look.Anim.CellWidth * look.WorldPerPixel, look.Anim.CellHeight * look.WorldPerPixel, 1f);
            Draw();
        }

        void Update()
        {
            if(companion!=null && GameManager.IsGamePaused)return;
            if (caster == null || (spentArrow!=null && (companion==null || !companion.AllowsShotHit(shotTarget))))
            {
                Destroy(gameObject);
                return;
            }
            float step = Speed * Time.deltaTime;
            Vector3 from = transform.position;
            RaycastHit hit;
            if (FirstHit(from, step, out hit))
            {
                transform.position = hit.point;
                Impact(hit);
                return;
            }
            transform.position = from + direction * step;
            travelled += step;
            if (travelled > MaxDistance)
            {
                Destroy(gameObject);
                return;
            }
            time += Time.deltaTime;
            Draw();
        }

        /// <summary>The nearest solid thing along this frame's flight that is not the shooter.</summary>
        bool FirstHit(Vector3 from, float step, out RaycastHit best)
        {
            best = new RaycastHit();
            int count=Physics.RaycastNonAlloc(from,direction,flightHits,step,layerMask,QueryTriggerInteraction.Ignore);
            // NonAlloc truncation is unordered. A dense collision frame must still find the nearest solid thing.
            RaycastHit[] hits=count==flightHits.Length?Physics.RaycastAll(from,direction,step,layerMask,QueryTriggerInteraction.Ignore):flightHits;
            if(hits!=flightHits)count=hits.Length;
            bool found = false;
            for(int i=0;i<count;i++)
            {
                RaycastHit h=hits[i];
                if (h.collider.transform.IsChildOf(caster.transform) || (found && h.distance >= best.distance))
                    continue;
                best = h;
                found = true;
            }
            return found;
        }

        void Draw()
        {
            if (quad == null)
                return;
            Camera cam = Camera.main;
            int row = 0;
            if (cam != null)
            {
                quad.transform.rotation = Quaternion.LookRotation(cam.transform.forward, cam.transform.up);
                if (look.Anim.Rows > 1)
                {
                    // Rows by view around the flight, as a person's rows by view around its facing: front = coming at you.
                    Vector3 toCamera = cam.transform.position - transform.position;
                    row = SpriteDirections.Row(toCamera.x, toCamera.z, direction.x, direction.z);
                }
            }
            int frame = look.Anim.Frames > 1 ? (int)(time * Mathf.Max(1, look.Fps)) % look.Anim.Frames : 0;
            int rows = look.Anim.Rows;
            material.mainTextureOffset = new Vector2((float)frame / look.Anim.Frames, (float)(rows - 1 - row) / rows);
        }

        void Impact(RaycastHit hit)
        {
            DaggerfallEntityBehaviour target = hit.collider.GetComponentInParent<DaggerfallEntityBehaviour>();
            if(spentArrow!=null)
            {
                try {if(companion!=null && target==shotTarget && companion.AllowsShotHit(target))CompanionHit(target);}
                catch(System.Exception exception) {AdvancedNpcsMod.LogError("Companion arrow impact ended: "+exception.Message);}
            }
            else if (target != null && senses != null && target == senses.Target)
                Damage(target);
            else if (DaggerfallUnity.Instance != null && DaggerfallUnity.Instance.SoundReader != null)
            {
                AudioClip clip = DaggerfallUnity.Instance.SoundReader.GetAudioClip((int)SoundClips.ArrowHit);
                if (clip != null)
                    AudioSource.PlayClipAtPoint(clip, hit.point, DaggerfallUnity.Settings.SoundVolume);
            }
            Destroy(gameObject);
        }

        /// <summary>DFU's arrow damage (as DaggerfallMissile does: only to the shooter's target), or the override.</summary>
        void Damage(DaggerfallEntityBehaviour target)
        {
            EnemyAttack attack = caster.GetComponent<EnemyAttack>();
            EnemyEntity entity = caster.Entity as EnemyEntity;
            if (attack == null || entity == null || target.Entity == null)
                return;
            if (damageMax <= 0)
            {
                int arrows = Arrows(target.Entity);
                attack.BowDamage(direction);
                if (look != null)
                    TakeBackArrow(target.Entity, arrows);       // BowDamage leaves an arrow with the target; not a rock
                return;
            }
            DaggerfallUnityItem weapon = entity.ItemEquipTable.GetItem(EquipSlots.RightHand);
            // DFU's own roll decides hit or miss (skills, armour, luck); the override sets the amount.
            bool hits = FormulaHelper.CalculateAttackDamage(entity, target.Entity, false, 0, weapon) > 0;
            int damage = hits ? CombatRules.RollProjectileDamage(damageMin, damageMax, Random.value) : 0;
            EnemySounds sounds = caster.GetComponent<EnemySounds>();
            if (target == GameManager.Instance.PlayerEntityBehaviour)
            {
                GameManager.Instance.PlayerEntity.TallySkill(DFCareer.Skills.Dodging, 1);
                if (damage > 0 && DamageField != null && SendDamageToPlayer != null)
                {
                    DamageField.SetValue(attack, damage);
                    SendDamageToPlayer.Invoke(attack, null);    // DFU's own: health and hit sound
                }
                else if (damage <= 0 && sounds != null)
                    sounds.PlayMissSound(weapon);
                return;
            }
            EnemyEntity targetEntity = target.Entity as EnemyEntity;
            EnemySounds targetSounds = target.GetComponent<EnemySounds>();
            if (damage > 0)
            {
                if (targetSounds != null)
                    targetSounds.PlayHitSound(weapon);
                EnemyBlood blood = target.GetComponent<EnemyBlood>();
                CharacterController controller = target.GetComponent<CharacterController>();
                if (blood != null && controller != null && targetEntity != null)
                    blood.ShowBloodSplash(targetEntity.MobileEnemy.BloodIndex, target.transform.position + controller.center + Vector3.up * (controller.height / 8f));
                target.Entity.DecreaseHealth(damage);
            }
            else if (sounds != null)
                sounds.PlayMissSound(weapon);
            EnemyMotor motor = target.GetComponent<EnemyMotor>();
            if (motor != null)
                motor.MakeEnemyHostileToAttacker(caster);
        }

        void CompanionHit(DaggerfallEntityBehaviour target)
        {
            if(CompanionDamage==null || !(target.Entity is EnemyEntity) || shotBow==null || senses==null)return;
            var current=senses.Target;
            try
            {
                // The native method supplies hit rolls, enchantment strikes, sounds, blood and retaliation.
                // Pass the launch weapon explicitly; its RightHand slot may now hold the melee fallback.
                senses.Target=target;
                if(target.Entity.MinMetalToHit<=(WeaponMaterialTypes)shotBow.NativeMaterialValue)
                    CompanionDamage.Invoke(caster.GetComponent<EnemyAttack>(),new object[]{shotBow,direction,true});
                target.Entity.Items.AddItem(spentArrow,noStack:true);
                spentArrow=null;
            }
            finally {senses.Target=current;}
        }
        static int Arrows(DaggerfallEntity e)
        {
            int n = 0;
            foreach (DaggerfallUnityItem item in e.Items.SearchItems(ItemGroups.Weapons, (int)Weapons.Arrow))
                n += item.stackCount;
            return n;
        }

        static void TakeBackArrow(DaggerfallEntity e, int before)
        {
            if (Arrows(e) <= before)
                return;
            List<DaggerfallUnityItem> arrows = e.Items.SearchItems(ItemGroups.Weapons, (int)Weapons.Arrow);
            DaggerfallUnityItem item = arrows[arrows.Count - 1];
            if (item.stackCount > 1)
                item.stackCount--;
            else
                e.Items.RemoveItem(item);
        }

        void OnDestroy()
        {
            Flying.Remove(this);
            if (material != null)
                Destroy(material);
        }
    }
}

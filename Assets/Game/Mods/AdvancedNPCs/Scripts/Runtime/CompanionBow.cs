using System;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Items;
using AdvancedNPCs.Core;
namespace AdvancedNPCs
{
    /// <summary>One contract's manually selected bow and previous melee kit. No global AI or inventory hooks.</summary>
    public class CompanionBow
    {
        readonly NpcBrain brain;
        readonly MobileUnit mobile;
        float nextShot, retryAt;
        bool released;
        public bool Drawing { get; private set; }
        public int Shots { get; private set; }
        public CompanionBow(NpcBrain owner,MobileUnit unit) {brain=owner;mobile=unit;}
        public static bool IsBow(DaggerfallUnityItem item)
        {return item!=null && item.ItemGroup==ItemGroups.Weapons && (item.TemplateIndex==(int)Weapons.Short_Bow || item.TemplateIndex==(int)Weapons.Long_Bow);}
        public static DaggerfallUnityItem Ammo(DaggerfallEntity entity)
        {
            for(int i=0;i<entity.Items.Count;i++)
            {
                var item=entity.Items.GetItem(i);
                if(item.ItemGroup==ItemGroups.Weapons && item.TemplateIndex==(int)Weapons.Arrow && item.stackCount>0 && !item.IsEquipped && CompanionTrade.Blocked(item)==null)return item;
            }
            return null;
        }
        public bool UsingBow {get{return IsBow(brain.EntityBehaviour.Entity.ItemEquipTable.GetItem(EquipSlots.RightHand));}}
        DaggerfallUnityItem Owned(ulong uid)
        {var item=uid!=0?brain.EntityBehaviour.Entity.Items.GetItem(uid):null;return item!=null && item.currentCondition>0 && item.stackCount==1?item:null;}
        public bool CanHoldRange(float distance)
        {return UsingBow && brain.EntityBehaviour.Entity.ItemEquipTable.GetItem(EquipSlots.RightHand).currentCondition>0 && Ammo(brain.EntityBehaviour.Entity)!=null && CompanionBowRules.InRange(distance);}
        public void Cancel() {Drawing=false;released=false;}
        public void Restore()
        {
            Cancel();
            var inventory=brain.GetComponent<NpcCompanionInventory>();
            if(inventory!=null && inventory.Ready && brain.EntityBehaviour.Entity.CurrentHealth>0 && IsBow(Owned(inventory.Bow)))Switch(Owned(inventory.Bow),null);
        }
        // Native bow-left-hand is a player preference. Enemy attacks require RightHand; never change the global setting.
        public static void EquipPrimary(DaggerfallEntity entity,DaggerfallUnityItem item)
        {
            var table=entity.ItemEquipTable;
            table.UnequipItem(EquipSlots.RightHand);
            if(IsBow(item))table.UnequipItem(EquipSlots.LeftHand);
            if(table.EquipItem(item,true,false)==null || !item.IsEquipped)throw new InvalidOperationException("Primary weapon could not be equipped.");
            if(IsBow(item) && item.EquipSlot==EquipSlots.LeftHand)
            {
                var slots=table.SerializeEquipTable();slots[(int)EquipSlots.LeftHand]=0;slots[(int)EquipSlots.RightHand]=item.UID;
                table.DeserializeEquipTable(slots,entity.Items);
            }
            if(table.GetItem(EquipSlots.RightHand)!=item)throw new InvalidOperationException("Primary weapon slot was not selected.");
        }
        bool Switch(DaggerfallUnityItem weapon,DaggerfallUnityItem shield)
        {
            var entity=brain.EntityBehaviour.Entity;var table=entity.ItemEquipTable;
            if(table.GetItem(EquipSlots.RightHand)==weapon)return true;
            if(Time.time<retryAt)return false;
            var slots=table.SerializeEquipTable();var armor=(sbyte[])entity.ArmorValues.Clone();
            try
            {
                var sprite=brain.GetComponent<NpcSprite>();if(sprite!=null)sprite.CancelAttack();
                mobile.DoMeleeDamage=false;mobile.ShootArrow=false;mobile.ChangeEnemyState(MobileStates.Idle);Cancel();
                if(weapon==null)table.UnequipItem(EquipSlots.RightHand);else EquipPrimary(entity,weapon);
                if(shield!=null && shield.IsShield && (weapon==null || ItemEquipTable.GetItemHands(weapon)!=ItemHands.Both))table.EquipItem(shield,true,false);
                CompanionEquipment.RecalculateArmor(brain);NpcCompanionInventory.For(brain).Invalidate();return true;
            }
            catch(Exception exception)
            {
                CompanionEquipment.StopAll(entity);table.DeserializeEquipTable(slots,entity.Items);entity.ArmorValues=armor;
                try{CompanionEquipment.ResumeEffects(entity);}catch(Exception){}
                retryAt=Time.time+5;AdvancedNpcsMod.LogError(brain.Id+": bow equipment retained: "+exception.Message);return false;
            }
        }
        public void Loadout(float distance)
        {
            var inventory=brain.GetComponent<NpcCompanionInventory>();if(inventory==null || !inventory.Ready || inventory.Bow==0)return;
            var bow=Owned(inventory.Bow);
            bool use=CompanionBowRules.UseBow(UsingBow,IsBow(bow) && NpcProjectile.CompanionDamageAvailable && Ammo(brain.EntityBehaviour.Entity)!=null,distance);
            if(use)Switch(bow,null);
            else
            {
                var melee=Owned(inventory.Melee);if(melee!=null && (melee.ItemGroup!=ItemGroups.Weapons || IsBow(melee)))melee=null;
                Switch(melee,Owned(inventory.Shield));
            }
            if(!mobile.IsPlayingOneShot() && (brain.GetComponent<NpcSprite>()==null || !brain.GetComponent<NpcSprite>().PlayingOneShot))Cancel();
        }
        public bool Attack(float distance,bool clear)
        {
            if(!CanHoldRange(distance) || !clear)return false;
            if(Time.time>=nextShot && !mobile.IsPlayingOneShot() && (brain.GetComponent<NpcSprite>()==null || !brain.GetComponent<NpcSprite>().PlayingOneShot))
            {
                Drawing=true;released=false;nextShot=Time.time+CompanionBowRules.ShotSeconds;
                // PrimaryAttack exists on every base mobile, including ones without a native ranged animation.
                mobile.ChangeEnemyState(MobileStates.PrimaryAttack);
            }
            return true;
        }
        public bool Fire()
        {
            if(!Drawing || released)return false;
            released=true;
            var combat=brain.CompanionCombat;var entity=brain.EntityBehaviour.Entity;var senses=brain.GetComponent<EnemySenses>();
            if(!combat.AllowsShotHit(senses.Target) || entity.IsParalyzed || !UsingBow || senses.Target==null || !CanHoldRange(senses.DistanceToTarget) || !combat.ClearShot(senses.Target))return false;
            var arrow=Ammo(entity);if(arrow==null || !NpcProjectile.CompanionDamageAvailable)return false;
            try
            {
                // Preserve the actual arrow's custom class, condition and metadata; split only our own stack.
                var data=arrow.GetSaveData();data.uid=DaggerfallUnity.NextUID;data.stackCount=1;
                var spent=NpcCompanionInventory.RestoreItems(new[]{data}).GetItem(0);
                if(spent.GetType()!=arrow.GetType())return false;
                var projectile=NpcProjectile.Launch(brain.EntityBehaviour,senses,null,0,0);
                projectile.BindCompanion(combat,senses.Target,entity.ItemEquipTable.GetItem(EquipSlots.RightHand),spent);
                if(arrow.stackCount>1)arrow.stackCount--;else entity.Items.RemoveItem(arrow);
                Shots++;nextShot=Mathf.Max(nextShot,Time.time+CompanionBowRules.ShotSeconds);
            }
            catch(Exception exception){AdvancedNpcsMod.LogError(brain.Id+": arrow could not be released: "+exception.Message);return false;}
            // A sound/custom save callback failure after release must not refund or repeat a fired arrow.
            try {var audio=brain.GetComponent<DaggerfallAudioSource>();if(audio!=null)audio.PlayOneShot((int)SoundClips.ArrowShoot,1,1);}
            catch(Exception exception){AdvancedNpcsMod.LogError(brain.Id+": bow sound: "+exception.Message);}
            NpcCompanionInventory.For(brain).Invalidate();
            return true;
        }
    }
}

using System;
using DaggerfallConnect.FallExe;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.MagicAndEffects;
namespace AdvancedNPCs
{
    /// <summary>Manual equipment for the exact nearby contract. Native slots and effect callbacks remain authoritative.</summary>
    public static class CompanionEquipment
    {
        public static string Reason(NpcBrain brain,DaggerfallUnityItem item)
        {
            try {return Validate(brain,item);}
            catch(Exception) {return "Custom equipment data could not be read.";}
        }
        static string Validate(NpcBrain brain,DaggerfallUnityItem item)
        {
            if(!CompanionTrade.Available(brain))return "Equipment requires a nearby, calm hired companion.";
            if(item==null || brain.EntityBehaviour.Entity.Items.GetItem(item.UID)!=item)return "This item is no longer in the companion's pack.";
            if(item.IsEquipped)return null; // Removing worn gear is always allowed, including broken gear.
            if(item.currentCondition<1)return "Broken gear cannot be equipped.";
            string blocked=CompanionTrade.Blocked(item);if(blocked!=null)return blocked;
            if(item.ItemGroup==ItemGroups.Weapons && item.TemplateIndex==(int)Weapons.Arrow)return "Arrows are ammunition, not worn equipment.";
            var entity=brain.EntityBehaviour.Entity;var career=entity.Career;
            if(item.ItemGroup==ItemGroups.MensClothing && brain.Instance.Gender=="Female" || item.ItemGroup==ItemGroups.WomensClothing && brain.Instance.Gender=="Male")
                return "This clothing does not fit the companion.";
            bool prohibited=false;
            if(item.ItemGroup==ItemGroups.Armor)
            {
                if(item.IsShield && ((1<<(item.TemplateIndex-(int)Armor.Buckler) & (int)career.ForbiddenShields)!=0))prohibited=true;
                else if(!item.IsShield && ((1<<(item.NativeMaterialValue>>8) & (int)career.ForbiddenArmors)!=0))prohibited=true;
                else if((item.NativeMaterialValue>>8)==2 && ((1<<(item.NativeMaterialValue&255) & (int)career.ForbiddenMaterials)!=0))prohibited=true;
            }
            else if(item.ItemGroup==ItemGroups.Weapons)
                prohibited=(item.GetWeaponSkillUsed() & (int)career.ForbiddenProficiencies)!=0 || (1<<item.NativeMaterialValue & (int)career.ForbiddenMaterials)!=0;
            if(prohibited)return "The companion's class cannot use this equipment.";
            EquipSlots slot=entity.ItemEquipTable.GetEquipSlot(item);
            if(slot==EquipSlots.None || (int)slot<0 || (int)slot>=entity.ItemEquipTable.SerializeEquipTable().Length)return "This item cannot be worn.";
            if(item.ItemGroup==ItemGroups.Weapons && item.GetEquipSlot()!=EquipSlots.None && item.GetEquipSlot()!=EquipSlots.RightHand)
                return "Companions use a single primary weapon.";
            return null;
        }
        public static string SlotName(EquipSlots slot)
        {
            if(slot==EquipSlots.RightHand)return "primary hand";
            if(slot==EquipSlots.LeftHand)return "off hand";
            string name=System.Text.RegularExpressions.Regex.Replace(slot.ToString(),"([a-z])([A-Z])","$1 $2");
            return name.TrimEnd('0','1').ToLowerInvariant();
        }
        public static bool SetEquipped(NpcBrain brain,ulong uid,bool equip,out string message)
        {
            message=null;
            if(!CompanionTrade.Available(brain)){message="Equipment requires a nearby, calm hired companion.";return false;}
            var entity=brain.EntityBehaviour.Entity;var item=entity.Items.GetItem(uid);
            message=Reason(brain,item);if(message!=null)return false;
            if(item.IsEquipped==equip){message=equip?"Already equipped.":"Already in the pack.";return false;}
            var inventory=NpcCompanionInventory.For(brain);var table=entity.ItemEquipTable;
            ulong[] previous=table.SerializeEquipTable();sbyte[] previousArmor=(sbyte[])entity.ArmorValues.Clone();
            ulong previousBow=inventory.Bow,previousMelee=inventory.Melee,previousShield=inventory.Shield;
            bool previousManaged=inventory.ManagedEquipment;int previousCount=item.stackCount;DaggerfallUnityItem piece=null;
            string previousSaved=brain.State.companion.belongings;
            try
            {
                if(!equip)table.UnequipItem(item.EquipSlot);
                else
                {
                    var worn=item;
                    if(item.stackCount>1)
                    {
                        // Native EquipItem splits from PlayerEntity.Items. Never pass a companion stack to it.
                        var data=item.GetSaveData();data.uid=DaggerfallUnity.NextUID;data.stackCount=1;
                        piece=NpcCompanionInventory.RestoreItems(new[]{data}).GetItem(0);
                        if(piece.GetType()!=item.GetType())throw new InvalidOperationException("Custom stack type could not be preserved.");
                        entity.Items.AddItem(piece,noStack:true);
                        if(entity.Items.GetItem(piece.UID)!=piece)throw new InvalidOperationException("Split item was not accepted.");
                        item.stackCount--;worn=piece;
                    }
                    // Native enemies attack with RightHand. Replacing a primary weapon must not put it in an unused offhand.
                    var primary=table.GetItem(EquipSlots.RightHand);var shield=table.GetItem(EquipSlots.LeftHand);
                    if(worn.ItemGroup==ItemGroups.Weapons)
                    {
                        if(CompanionBow.IsBow(worn))
                        {
                            if(primary!=null && !CompanionBow.IsBow(primary))inventory.Melee=primary.UID;
                            if(shield!=null && shield.IsShield)inventory.Shield=shield.UID;
                            inventory.Bow=worn.UID;
                        }
                        else {inventory.Bow=0;inventory.Melee=worn.UID;inventory.Shield=0;}
                        CompanionBow.EquipPrimary(entity,worn);
                    }
                    else
                    {
                        // Our enemy bow stays in RightHand even when the player's setting reports LeftOnly.
                        if(worn.IsShield && CompanionBow.IsBow(table.GetItem(EquipSlots.RightHand)))table.UnequipItem(EquipSlots.RightHand);
                        if(table.EquipItem(worn,true,false)==null || !worn.IsEquipped)throw new InvalidOperationException("The item could not be equipped.");
                    }
                }
                if(!equip && item.UID==inventory.Bow || inventory.Bow!=0 && !CompanionBow.IsBow(table.GetItem(EquipSlots.RightHand)))inventory.Bow=0;
                inventory.ManagedEquipment=true;RecalculateArmor(brain);
                var sprite=brain.GetComponent<NpcSprite>();if(sprite!=null)sprite.CancelAttack();
                message=item.LongName+(equip?" equipped.":" moved to the pack.");inventory.Invalidate();inventory.Capture(false);return true;
            }
            catch(Exception exception)
            {
                // Restore ownership and equipment after a failing custom-item callback. Neither inventory loses an item.
                StopAll(entity);
                if(piece!=null && entity.Items.GetItem(piece.UID)==piece)entity.Items.RemoveItem(piece);
                item.stackCount=previousCount;
                table.DeserializeEquipTable(previous,entity.Items);entity.ArmorValues=previousArmor;
                inventory.ManagedEquipment=previousManaged;inventory.Bow=previousBow;inventory.Melee=previousMelee;inventory.Shield=previousShield;brain.State.companion.belongings=previousSaved;
                try {ResumeEffects(entity);}catch(Exception retry){AdvancedNpcsMod.LogError(brain.Id+": equipment effect rollback: "+retry.Message);}
                message="Equipment change failed; belongings were retained.";AdvancedNpcsMod.LogError(brain.Id+": "+exception.Message);return false;
            }
        }
        public static void RecalculateArmor(NpcBrain brain)
        {
            var entity=brain.EntityBehaviour.Entity;var enemy=entity as EnemyEntity;
            int baseline=brain.EntityBehaviour.EntityType==EntityTypes.EnemyClass?100:enemy!=null?enemy.MobileEnemy.ArmorValue*5:100;
            for(int i=0;i<entity.ArmorValues.Length;i++)entity.ArmorValues[i]=(sbyte)baseline;
            foreach(var item in entity.ItemEquipTable.EquipTable)
                if(item!=null && item.currentCondition>0)entity.UpdateEquippedArmorValues(item,true);
        }
        public static void StopAll(DaggerfallEntity entity)
        {
            int count=entity.ItemEquipTable.SerializeEquipTable().Length;
            for(int i=0;i<count;i++)
            {
                var item=entity.ItemEquipTable.GetItem((EquipSlots)i);if(item==null)continue;
                // Native Unequip clears the slot before callbacks. A bad custom payload must not prevent the remaining cleanup.
                try {entity.ItemEquipTable.UnequipItem((EquipSlots)i);}
                catch(Exception exception){AdvancedNpcsMod.LogError("Equipment effect cleanup for item "+item.UID+": "+exception.Message);}
            }
        }
        public static void ResumeEffects(DaggerfallEntity entity)
        {
            var manager=entity.EntityBehaviour.GetComponent<EntityEffectManager>();if(manager==null)return;
            foreach(var item in entity.ItemEquipTable.EquipTable)
                if(item!=null && item.IsEnchanted && item.currentCondition>0)
                    manager.DoItemEnchantmentPayloads(EnchantmentPayloadFlags.Equipped|EnchantmentPayloadFlags.Held,item);
        }
    }
}

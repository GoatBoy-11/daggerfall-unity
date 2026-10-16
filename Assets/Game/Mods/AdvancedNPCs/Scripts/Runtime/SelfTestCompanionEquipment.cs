using System;
using System.Collections;
using UnityEngine;
using DaggerfallConnect;
using DaggerfallConnect.FallExe;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.MagicAndEffects;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Utility;
namespace AdvancedNPCs
{
    public class CompanionEquipmentTestItem : DaggerfallUnityItem
    {
        public static int ThrowAfter;
        public CompanionEquipmentTestItem():base(ItemGroups.Weapons,0) {}
        public override ItemData_v1 GetSaveData(){var data=base.GetSaveData();data.className="ANPCEquipmentFixture";return data;}
        public override EquipSlots GetEquipSlot(){if(ThrowAfter>0 && --ThrowAfter==0)throw new InvalidOperationException("Intentional equipment fixture failure.");return EquipSlots.None;}
    }
    public partial class SelfTest
    {
        public const string CompanionEquipmentSuite="equipment";
        static string EquipmentArmor(DaggerfallEntity entity){return string.Join(",",Array.ConvertAll(entity.ArmorValues,delegate(sbyte value){return value.ToString();}));}
        IEnumerator CompanionEquipmentSteps(DaggerfallLocation location,Transform player)
        {
            var gm=GameManager.Instance;var pe=gm.PlayerEntity;
            Vector3 oldPosition=player.position;Quaternion oldRotation=player.rotation,oldView=gm.MainCamera.transform.rotation;
            bool oldAI=gm.DisableAI,look=gm.PlayerMouseLook.enabled;int gold=pe.GoldPieces;
            var playerItems=pe.Items.SerializeItems();var playerWorn=pe.ItemEquipTable.SerializeEquipTable();
            NpcBrain actor=null;GameObject ghost=null;
            var floor=new GameObject("ANPC equipment verification");floor.transform.SetParent(location.transform,false);
            Vector3 origin=oldPosition+Vector3.up*24;NavigationBox(floor.transform,"equipment floor",origin-Vector3.up*.15f,new Vector3(50,.3f,30));
            ItemCollection.RegisterCustomItem("ANPCEquipmentFixture",typeof(CompanionEquipmentTestItem));
            try
            {
                gm.DisableAI=true;gm.PlayerMouseLook.enabled=false;NavigationPlayerAt(player,origin,Vector3.right);
                var definition=mod.Catalog.Generics.Find(delegate(AdvancedNPCs.Core.NpcDefinition d){return d.Id=="ironclad_rogue_1";});
                actor=CombatPerson(location,definition,"selftest_equipment",origin+new Vector3(-2,1,0),true);
                yield return new WaitForSecondsRealtime(.5f);
                var entity=actor.EntityBehaviour.Entity;var items=entity.Items;var table=entity.ItemEquipTable;
                CompanionEquipment.StopAll(entity);items.Clear();entity.Stats.SetPermanentStatValue(0,100);
                var career=entity.Career;var forbiddenArmor=career.ForbiddenArmors;var forbiddenMaterial=career.ForbiddenMaterials;var forbiddenWeapons=career.ForbiddenProficiencies;var forbiddenShields=career.ForbiddenShields;
                // Isolate native equipment mechanics; restrictions are explicitly exercised below.
                career.ForbiddenArmors=0;career.ForbiddenMaterials=0;career.ForbiddenProficiencies=0;career.ForbiddenShields=0;
                var blade=ItemBuilder.CreateWeapon(Weapons.Longsword,WeaponMaterialTypes.Silver);pe.Items.AddItem(blade,noStack:true);string message;
                Check("giving a weapon stores it without auto equip",CompanionTrade.Transfer(actor,true,blade.UID,1,out message) && !blade.IsEquipped,message);
                float weight=items.GetWeight();
                Check("manual equip uses exact supplied primary weapon",CompanionEquipment.SetEquipped(actor,blade.UID,true,out message) && table.GetItem(EquipSlots.RightHand)==blade && blade.UID==table.GetItem(EquipSlots.RightHand).UID,message);
                Check("equipping preserves carried weight and ownership",items.GetWeight()==weight && items.Contains(blade.UID) && !pe.Items.Contains(blade.UID),"weight="+items.GetWeight());
                Check("repeated equip is idempotent",!CompanionEquipment.SetEquipped(actor,blade.UID,true,out message) && table.GetItem(EquipSlots.RightHand)==blade,message);
                var axe=ItemBuilder.CreateWeapon(Weapons.War_Axe,WeaponMaterialTypes.Steel);items.AddItem(axe,noStack:true);
                var shield=ItemBuilder.CreateArmor(Genders.Female,Races.Breton,Armor.Round_Shield,ArmorMaterialTypes.Iron);items.AddItem(shield,noStack:true);
                Check("shield occupies offhand with one-handed primary",CompanionEquipment.SetEquipped(actor,shield.UID,true,out message) && table.GetItem(EquipSlots.LeftHand)==shield && table.GetItem(EquipSlots.RightHand)==blade,message);
                Check("two-handed weapon returns both previous hands to pack",CompanionEquipment.SetEquipped(actor,axe.UID,true,out message) && table.GetItem(EquipSlots.RightHand)==axe && table.GetItem(EquipSlots.LeftHand)==null && !blade.IsEquipped && !shield.IsEquipped && items.Contains(blade.UID) && items.Contains(shield.UID),message);
                Check("shield safely replaces two-handed primary",CompanionEquipment.SetEquipped(actor,shield.UID,true,out message) && table.GetItem(EquipSlots.LeftHand)==shield && table.GetItem(EquipSlots.RightHand)==null && !axe.IsEquipped,message);
                CompanionEquipment.SetEquipped(actor,blade.UID,true,out message);
                var second=ItemBuilder.CreateWeapon(Weapons.Dagger,WeaponMaterialTypes.Iron);items.AddItem(second,noStack:true);
                Check("replacement weapon always becomes primary instead of unused offhand",CompanionEquipment.SetEquipped(actor,second.UID,true,out message) && table.GetItem(EquipSlots.RightHand)==second && table.GetItem(EquipSlots.LeftHand)==shield && !blade.IsEquipped,message);
                var helm=ItemBuilder.CreateArmor(Genders.Female,Races.Breton,Armor.Helm,ArmorMaterialTypes.Leather);items.AddItem(helm,noStack:true);
                var boots=ItemBuilder.CreateArmor(Genders.Female,Races.Breton,Armor.Boots,ArmorMaterialTypes.Leather);items.AddItem(boots,noStack:true);
                CompanionEquipment.SetEquipped(actor,shield.UID,false,out message);
                string bare=EquipmentArmor(entity);
                Check("manual gear uses unarmored baseline without hidden enemy armor",Array.TrueForAll(entity.ArmorValues,delegate(sbyte value){return value==100;}),bare);
                Check("armor affects native head protection",CompanionEquipment.SetEquipped(actor,helm.UID,true,out message) && EquipmentArmor(entity)!=bare,message);
                string head=EquipmentArmor(entity);
                Check("boots affect native protection including last equip slot",CompanionEquipment.SetEquipped(actor,boots.UID,true,out message) && EquipmentArmor(entity)!=head,message);
                CompanionEquipment.SetEquipped(actor,boots.UID,false,out message);
                Check("armor replacement and removal do not accumulate protection",CompanionEquipment.SetEquipped(actor,helm.UID,false,out message) && EquipmentArmor(entity)==bare,message);
                for(int i=0;i<4;i++){CompanionEquipment.SetEquipped(actor,shield.UID,true,out message);CompanionEquipment.SetEquipped(actor,shield.UID,false,out message);}
                Check("repeated shield cycles do not drift armor values",EquipmentArmor(entity)==bare,EquipmentArmor(entity));
                var broken=ItemBuilder.CreateWeapon(Weapons.Dagger,WeaponMaterialTypes.Iron);broken.currentCondition=0;items.AddItem(broken,noStack:true);
                Check("broken item rejected without displacing primary",!CompanionEquipment.SetEquipped(actor,broken.UID,true,out message) && table.GetItem(EquipSlots.RightHand)==second,message);
                var arrow=ItemBuilder.CreateWeapon(Weapons.Arrow,WeaponMaterialTypes.Iron);items.AddItem(arrow,noStack:true);
                Check("ammunition cannot be equipped as a weapon",!CompanionEquipment.SetEquipped(actor,arrow.UID,true,out message),message);
                var bow=ItemBuilder.CreateWeapon(Weapons.Long_Bow,WeaponMaterialTypes.Iron);items.AddItem(bow,noStack:true);
                Check("bow can be manually equipped with exact melee fallback",CompanionEquipment.SetEquipped(actor,bow.UID,true,out message) && table.GetItem(EquipSlots.RightHand)==bow && NpcCompanionInventory.For(actor).Melee==second.UID,message);
                CompanionEquipment.SetEquipped(actor,second.UID,true,out message);
                career.ForbiddenMaterials=(DFCareer.MaterialFlags)(1<<(int)WeaponMaterialTypes.Silver);
                Check("native class material restrictions enforced",!CompanionEquipment.SetEquipped(actor,blade.UID,true,out message),message);career.ForbiddenMaterials=0;
                career.ForbiddenArmors=(DFCareer.ArmorFlags)(1<<(helm.NativeMaterialValue>>8));
                Check("native class armor restrictions enforced",!CompanionEquipment.SetEquipped(actor,helm.UID,true,out message),message);career.ForbiddenArmors=0;
                var feminine=ItemBuilder.CreateItem(ItemGroups.WomensClothing,(int)WomensClothing.Short_shirt);items.AddItem(feminine,noStack:true);
                var masculine=ItemBuilder.CreateItem(ItemGroups.MensClothing,(int)MensClothing.Short_shirt);items.AddItem(masculine,noStack:true);
                // DFU's generic enemy Entity.Gender can differ from the ANPC's authored/saved identity.
                var nativeGender=entity.Gender;entity.Gender=Genders.Male;
                Check("female companion clothing uses saved ANPC identity",CompanionEquipment.SetEquipped(actor,feminine.UID,true,out message) && feminine.IsEquipped,message);
                Check("incompatible clothing is rejected despite generic enemy gender",!CompanionEquipment.SetEquipped(actor,masculine.UID,true,out message) && feminine.IsEquipped,message);
                CompanionEquipment.SetEquipped(actor,feminine.UID,false,out message);items.RemoveItem(feminine);items.RemoveItem(masculine);entity.Gender=nativeGender;
                var custom=new CompanionEquipmentTestItem();custom.stackCount=4;custom.currentCondition=17;items.AddItem(custom,noStack:true);
                string playerBefore=SaveLoadManager.Serialize(typeof(ItemData_v1[]),pe.Items.SerializeItems());
                Check("equipping custom stack splits only companion inventory",CompanionEquipment.SetEquipped(actor,custom.UID,true,out message) && custom.stackCount==3 && table.GetItem(EquipSlots.RightHand) is CompanionEquipmentTestItem && table.GetItem(EquipSlots.RightHand).UID!=custom.UID && table.GetItem(EquipSlots.RightHand).stackCount==1 && table.GetItem(EquipSlots.RightHand).currentCondition==17 && SaveLoadManager.Serialize(typeof(ItemData_v1[]),pe.Items.SerializeItems())==playerBefore,message);
                var customWorn=table.GetItem(EquipSlots.RightHand);ulong customUid=customWorn.UID;
                Check("worn item cannot be taken until unequipped",!CompanionTrade.Transfer(actor,false,customUid,1,out message),message);
                Check("unequipping retains exact native item",CompanionEquipment.SetEquipped(actor,customUid,false,out message) && items.GetItem(customUid)==customWorn && !customWorn.IsEquipped,message);
                Check("unequipped gear can be taken back",CompanionTrade.Transfer(actor,false,customUid,1,out message) && pe.Items.GetItem(customUid)==customWorn,message);
                CompanionEquipment.SetEquipped(actor,blade.UID,true,out message);
                CompanionEquipmentTestItem.ThrowAfter=3;
                Check("failed native custom equip rolls back both hands and stack",!CompanionEquipment.SetEquipped(actor,custom.UID,true,out message) && table.GetItem(EquipSlots.RightHand)==blade && custom.stackCount==3 && !custom.IsEquipped,message);
                CompanionEquipmentTestItem.ThrowAfter=0;
                var ring=ItemBuilder.CreateItem(ItemGroups.Jewellery,(int)Jewellery.Ring);
                ring.SetEnchantments(new[]{new EnchantmentSettings{Version=1,EffectKey="EnhancesSkill",ClassicType=EnchantmentTypes.EnhancesSkill,ClassicParam=(short)DFCareer.Skills.LongBlade}},entity);items.AddItem(ring,noStack:true);
                int skill=entity.Skills.GetLiveSkillValue(DFCareer.Skills.LongBlade),playerSkill=pe.Skills.GetLiveSkillValue(DFCareer.Skills.LongBlade);
                bool ringEquipped=CompanionEquipment.SetEquipped(actor,ring.UID,true,out message);yield return new WaitForSecondsRealtime(.2f);
                Check("native held enchantment applies to companion",ringEquipped && entity.Skills.GetLiveSkillValue(DFCareer.Skills.LongBlade)==skill+15 && pe.Skills.GetLiveSkillValue(DFCareer.Skills.LongBlade)==playerSkill,"skill="+skill+" -> "+entity.Skills.GetLiveSkillValue(DFCareer.Skills.LongBlade)+"; player="+playerSkill+" -> "+pe.Skills.GetLiveSkillValue(DFCareer.Skills.LongBlade));
                bool ringRemoved=CompanionEquipment.SetEquipped(actor,ring.UID,false,out message);yield return new WaitForSecondsRealtime(.2f);
                Check("unequip removes native held enchantment",ringRemoved && entity.Skills.GetLiveSkillValue(DFCareer.Skills.LongBlade)==skill,message);
                CompanionEquipment.SetEquipped(actor,ring.UID,true,out message);CompanionEquipment.SetEquipped(actor,helm.UID,true,out message);CompanionEquipment.SetEquipped(actor,boots.UID,true,out message);
                string armorBefore=EquipmentArmor(entity);int itemCount=items.Count;
                var save=new NpcSaveDataInterface(mod.States,mod.Placed,mod.Flags,mod.RaiseStateRestored);string encoded=SaveLoadManager.Serialize(typeof(NpcSaveData),save.GetSaveData());
                var stale=actor;save.RestoreSaveData(SaveLoadManager.Deserialize(typeof(NpcSaveData),encoded));yield return new WaitForSecondsRealtime(1);
                actor=NpcBrain.Find("selftest_equipment");entity=actor.EntityBehaviour.Entity;items=entity.Items;table=entity.ItemEquipTable;
                Check("save restores chosen gear exact UIDs and ownership",table.GetItem(EquipSlots.RightHand).UID==blade.UID && items.GetItem(helm.UID).IsEquipped && items.GetItem(boots.UID).IsEquipped && items.Count==itemCount,actor.Describe());
                Check("save restores derived armor without doubling",EquipmentArmor(entity)==armorBefore,EquipmentArmor(entity));
                Check("save resumes held item enchantment once",entity.Skills.GetLiveSkillValue(DFCareer.Skills.LongBlade)==entity.Skills.GetPermanentSkillValue(DFCareer.Skills.LongBlade)+15,"skill="+entity.Skills.GetLiveSkillValue(DFCareer.Skills.LongBlade));
                Check("stale actor cannot change replacement equipment",!CompanionEquipment.SetEquipped(stale,blade.UID,false,out message) && table.GetItem(EquipSlots.RightHand).UID==blade.UID,message);
                NavigationPlayerAt(player,actor.transform.position+Vector3.right,Vector3.left);
                CompanionTradeWindow.Open(actor);yield return new WaitForSecondsRealtime(.2f);var window=DaggerfallUI.UIManager.TopWindow as CompanionTradeWindow;
                int row=0;for(int i=0;i<items.Count;i++)if(items.GetItem(i).UID==helm.UID)row=i;
                window.TakeList.SelectedIndex=row;window.EquipmentButton.TriggerMouseClick();window.EquipmentButton.TriggerMouseClick();
                Check("equipment UI changes selected gear once per input",!items.GetItem(helm.UID).IsEquipped && table.GetItem(EquipSlots.RightHand).UID==blade.UID,actor.Describe());
                yield return new WaitForSecondsRealtime(.2f);window.EquipmentButton.TriggerMouseClick();
                Check("equipment UI can equip the same exact item again",items.GetItem(helm.UID).IsEquipped,actor.Describe());
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(Application.persistentDataPath,"ANPC-equipment.png"));yield return new WaitForSecondsRealtime(.2f);DaggerfallUI.Instance.PopToHUD();
                ghost=GameObjectHelper.CreateEnemy("ANPC equipment ghost",MobileTypes.Ghost,location.transform.InverseTransformPoint(actor.transform.position+Vector3.right*3),MobileGender.Unspecified,location.transform,MobileReactions.Hostile);
                yield return new WaitForSecondsRealtime(.2f);var target=ghost.GetComponent<DaggerfallEntityBehaviour>().Entity;
                entity.Skills.SetPermanentSkillValue(DFCareer.Skills.LongBlade,100);entity.Skills.SetPermanentSkillValue(DFCareer.Skills.ShortBlade,100);
                int silverDamage=0,ironDamage=0;
                for(int i=0;i<64;i++)silverDamage+=DaggerfallWorkshop.Game.Formulas.FormulaHelper.CalculateAttackDamage(entity,target,false,0,table.GetItem(EquipSlots.RightHand));
                CompanionEquipment.SetEquipped(actor,second.UID,true,out message);
                for(int i=0;i<64;i++)ironDamage+=DaggerfallWorkshop.Game.Formulas.FormulaHelper.CalculateAttackDamage(entity,target,false,0,table.GetItem(EquipSlots.RightHand));
                Check("native damage formula uses equipped weapon material against ghost",silverDamage>0 && ironDamage==0,"silver="+silverDamage+" iron="+ironDamage);
                actor.Teleport(actor.transform.position+Vector3.right*30);
                Check("distance blocks equipment changes",!CompanionEquipment.SetEquipped(actor,second.UID,false,out message),message);
                NavigationPlayerAt(player,actor.transform.position+Vector3.right,Vector3.left);
                actor.CompanionMode(gm.PlayerEntityBehaviour,NpcMode.Fighting);
                Check("combat blocks equipment changes",!CompanionEquipment.SetEquipped(actor,second.UID,false,out message),message);
                actor.CompanionMode(null,NpcMode.Calm);mod.Companions.Command(actor,"dismiss");
                Check("dismissed contracts reject equipment",!CompanionEquipment.SetEquipped(actor,second.UID,false,out message),message);
                pe.GoldPieces=1000;mod.Companions.Command(actor,"recruit");
                Check("rehire retains chosen equipment",table.GetItem(EquipSlots.RightHand).UID==second.UID,actor.Describe());
                actor.EntityBehaviour.Entity.CurrentHealth=0;yield return new WaitForSecondsRealtime(.7f);
                Check("death sends worn gear to native corpse once",actor.EntityBehaviour.CorpseLootContainer!=null && actor.EntityBehaviour.CorpseLootContainer.Items.Contains(second.UID) && !items.Contains(second.UID),actor.Describe());
                var corpse=actor.EntityBehaviour.CorpseLootContainer;
                Check("corpse accessories and boots are no longer marked worn",corpse!=null && corpse.Items.GetItem(ring.UID)!=null && !corpse.Items.GetItem(ring.UID).IsEquipped && corpse.Items.GetItem(boots.UID)!=null && !corpse.Items.GetItem(boots.UID).IsEquipped,"ring="+ring.UID);
                career.ForbiddenArmors=forbiddenArmor;career.ForbiddenMaterials=forbiddenMaterial;career.ForbiddenProficiencies=forbiddenWeapons;career.ForbiddenShields=forbiddenShields;
            }
            finally
            {
                CompanionEquipmentTestItem.ThrowAfter=0;DaggerfallUI.Instance.PopToHUD();NpcBrain.Discard(actor);UnityEngine.Object.Destroy(ghost);UnityEngine.Object.Destroy(floor);
                pe.ItemEquipTable.Clear();pe.Items.DeserializeItems(playerItems);pe.ItemEquipTable.DeserializeEquipTable(playerWorn,pe.Items);pe.GoldPieces=gold;
                gm.DisableAI=oldAI;gm.PlayerMouseLook.enabled=look;player.position=oldPosition;player.rotation=oldRotation;gm.MainCamera.transform.rotation=oldView;
            }
        }
    }
}

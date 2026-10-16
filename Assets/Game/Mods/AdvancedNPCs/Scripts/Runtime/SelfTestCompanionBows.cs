using System;
using System.Collections;
using UnityEngine;
using DaggerfallConnect;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.Serialization;
using AdvancedNPCs.Core;
namespace AdvancedNPCs
{
    public class CompanionBowTestArrow : DaggerfallUnityItem
    {
        public CompanionBowTestArrow():base(ItemGroups.Weapons,18) {currentCondition=0;}
        public override ItemData_v1 GetSaveData(){var data=base.GetSaveData();data.className="ANPCBowArrowFixture";return data;}
    }
    public partial class SelfTest
    {
        public const string CompanionBowsSuite="bows";
        static int BowAmmo(NpcBrain brain)
        {int count=0;foreach(var item in brain.EntityBehaviour.Entity.Items.SearchItems(ItemGroups.Weapons,(int)Weapons.Arrow))count+=item.stackCount;return count;}
        IEnumerator CompanionBowsSteps(DaggerfallLocation location,Transform player)
        {
            var gm=GameManager.Instance;Vector3 oldPosition=player.position;Quaternion oldRotation=player.rotation,oldView=gm.MainCamera.transform.rotation;
            bool oldAI=gm.DisableAI,look=gm.PlayerMouseLook.enabled,left=DaggerfallUnity.Settings.BowLeftHandWithSwitching;
            NpcBrain actor=null,foe=null,neutral=null,native=null,pose=null;
            var floor=new GameObject("ANPC bow verification");floor.transform.SetParent(location.transform,false);
            Vector3 origin=oldPosition+Vector3.up*24;NavigationBox(floor.transform,"bow floor",origin-Vector3.up*.15f,new Vector3(90,.3f,50));
            try
            {
                gm.DisableAI=true;gm.PlayerMouseLook.enabled=false;NavigationPlayerAt(player,origin+Vector3.forward*4,Vector3.right);
                var definition=mod.Catalog.Generics.Find(delegate(NpcDefinition d){return d.Id=="ironclad_rogue_1";});
                actor=CombatPerson(location,definition,"selftest_bow_rogue",origin+new Vector3(-2,1,0),true);
                var hostile=TestDefinition("selftest_bow_foe",Bravery.Brave,location,player,0);hostile.RecruitCost=-1;hostile.CrimeOnAttack=false;hostile.BaseClass="Warrior";hostile.FleeChance=0;
                foe=CombatPerson(location,hostile,"selftest_bow_foe",origin+new Vector3(10,1,0),false);
                neutral=CombatPerson(location,TestDefinition("selftest_bow_neutral",Bravery.Brave,location,player,0),"selftest_bow_neutral",origin+new Vector3(5,1,8),false);
                yield return new WaitForSecondsRealtime(.6f);
                var entity=actor.EntityBehaviour.Entity;var items=entity.Items;var table=entity.ItemEquipTable;
                CompanionEquipment.StopAll(entity);items.Clear();entity.Stats.SetPermanentStatValue(0,100);entity.Skills.SetPermanentSkillValue(DFCareer.Skills.Archery,100);entity.Skills.SetPermanentSkillValue(DFCareer.Skills.LongBlade,100);
                var blade=ItemBuilder.CreateWeapon(Weapons.Longsword,WeaponMaterialTypes.Silver);items.AddItem(blade,noStack:true);
                var shield=ItemBuilder.CreateArmor(Genders.Female,Races.Breton,Armor.Round_Shield,ArmorMaterialTypes.Iron);items.AddItem(shield,noStack:true);
                var bow=ItemBuilder.CreateWeapon(Weapons.Long_Bow,WeaponMaterialTypes.Silver);items.AddItem(bow,noStack:true);
                var arrows=ItemBuilder.CreateWeapon(Weapons.Arrow,WeaponMaterialTypes.Iron);arrows.stackCount=24;arrows.shortName="ANPC finite arrows";items.AddItem(arrows,noStack:true);
                NavigationPlayerAt(player,actor.transform.position+Vector3.forward,Vector3.back);string message;
                CompanionEquipment.SetEquipped(actor,blade.UID,true,out message);CompanionEquipment.SetEquipped(actor,shield.UID,true,out message);
                DaggerfallUnity.Settings.BowLeftHandWithSwitching=true;
                Check("manual bow uses primary hand regardless of player preference",CompanionEquipment.SetEquipped(actor,bow.UID,true,out message) && table.GetItem(EquipSlots.RightHand)==bow && table.GetItem(EquipSlots.LeftHand)==null && DaggerfallUnity.Settings.BowLeftHandWithSwitching,message);
                var inventory=NpcCompanionInventory.For(actor);
                Check("bow remembers exact melee weapon and shield in own pack",inventory.Bow==bow.UID && inventory.Melee==blade.UID && inventory.Shield==shield.UID && items.Contains(blade.UID) && !blade.IsEquipped,actor.Describe());
                Check("manual shield stows bow with alternate bow-hand setting",CompanionEquipment.SetEquipped(actor,shield.UID,true,out message) && table.GetItem(EquipSlots.RightHand)==null && table.GetItem(EquipSlots.LeftHand)==shield && inventory.Bow==0,message);
                CompanionEquipment.SetEquipped(actor,blade.UID,true,out message);CompanionEquipment.SetEquipped(actor,bow.UID,true,out message);
                actor.CompanionCombat.Bow.Loadout(5);
                Check("close enemy switches to selected melee kit",table.GetItem(EquipSlots.RightHand)==blade && table.GetItem(EquipSlots.LeftHand)==shield,actor.Describe());
                actor.CompanionCombat.Bow.Loadout(7);
                Check("middle distance does not thrash melee kit",table.GetItem(EquipSlots.RightHand)==blade,actor.Describe());
                actor.CompanionCombat.Bow.Loadout(8);
                Check("safe range restores selected bow and stows shield",table.GetItem(EquipSlots.RightHand)==bow && !shield.IsEquipped,actor.Describe());
                actor.CompanionCombat.Bow.Loadout(7);
                Check("middle distance retains drawn bow",table.GetItem(EquipSlots.RightHand)==bow,actor.Describe());
                actor.CompanionCombat.Bow.Restore();actor.CompanionCombat.Bow.Loadout(5);
                // Save the selected bow and fallbacks, then reconstruct the hired actor through the real mod restore hook.
                var save=new NpcSaveDataInterface(mod.States,mod.Placed,mod.Flags,mod.RaiseStateRestored);
                string encoded=SaveLoadManager.Serialize(typeof(NpcSaveData),save.GetSaveData());save.RestoreSaveData(SaveLoadManager.Deserialize(typeof(NpcSaveData),encoded));
                yield return new WaitForSecondsRealtime(1);
                actor=NpcBrain.Find("selftest_bow_rogue");entity=actor.EntityBehaviour.Entity;items=entity.Items;table=entity.ItemEquipTable;inventory=NpcCompanionInventory.For(actor);
                bow=items.GetItem(bow.UID);blade=items.GetItem(blade.UID);shield=items.GetItem(shield.UID);
                Check("save restore preserves bow melee shield UIDs and finite ammo",inventory.Bow==bow.UID && inventory.Melee==blade.UID && inventory.Shield==shield.UID && BowAmmo(actor)==24 && table.GetItem(EquipSlots.RightHand)==bow,actor.Describe());
                mod.Companions.Reparent(actor,floor.transform);yield return new WaitForSecondsRealtime(.2f);
                mod.Companions.Reparent(actor,location.transform);yield return new WaitForSecondsRealtime(.2f);
                Check("area ownership transfer preserves bow and fallback identities",inventory.Bow==bow.UID && inventory.Melee==blade.UID && inventory.Shield==shield.UID && table.GetItem(EquipSlots.RightHand)==bow,actor.Describe());
                entity.Skills.SetPermanentSkillValue(DFCareer.Skills.Archery,100);entity.Skills.SetPermanentSkillValue(DFCareer.Skills.LongBlade,100);
                foe=CombatPerson(location,hostile,"selftest_bow_foe",origin+new Vector3(10,1,0),false);
                neutral=CombatPerson(location,TestDefinition("selftest_bow_neutral",Bravery.Brave,location,player,0),"selftest_bow_neutral",origin+new Vector3(5,1,8),false);
                yield return new WaitForSecondsRealtime(.5f);
                foe.EntityBehaviour.Entity.MaxHealth=1000;foe.EntityBehaviour.Entity.CurrentHealth=1000;
                for(int i=0;i<foe.EntityBehaviour.Entity.ArmorValues.Length;i++)foe.EntityBehaviour.Entity.ArmorValues[i]=100;
                neutral.enabled=false;neutral.GetComponent<EnemyMotor>().enabled=false;neutral.GetComponent<EnemyAttack>().enabled=false;neutral.GetComponent<EnemySenses>().enabled=false;
                foe.SwitchHostile(true);foe.enabled=false;AimCombat(foe,gm.PlayerEntityBehaviour);
                gm.DisableAI=false;float deadline=Time.time+15;
                while(Time.time<deadline && foe.EntityBehaviour.Entity.CurrentHealth==1000){AimCombat(foe,gm.PlayerEntityBehaviour);yield return null;}
                int shots=actor.CompanionCombat.Bow.Shots;
                Check("hired rogue fires animated arrows and deals native damage",shots>0 && foe.EntityBehaviour.Entity.CurrentHealth<1000,"shots="+shots+" hp="+foe.EntityBehaviour.Entity.CurrentHealth+"; "+actor.Describe());
                Check("each released shot consumes exactly one owned arrow",BowAmmo(actor)==24-shots,"arrows="+BowAmmo(actor)+" shots="+shots);
                Check("ranged attack holds distance instead of charging melee",Vector3.Distance(actor.transform.position,foe.transform.position)>=6 && actor.CompanionCombat.Bow.UsingBow,actor.Describe());
                var recovered=foe.EntityBehaviour.Entity.Items.SearchItems(ItemGroups.Weapons,(int)Weapons.Arrow);
                Check("impact retains finite arrow metadata on original enemy",recovered.Exists(delegate(DaggerfallUnityItem item){return item.shortName=="ANPC finite arrows";}),"target arrows="+recovered.Count);
                // Use the normal action-frame path to release a shot, then move its chosen enemy close before impact.
                int before=BowAmmo(actor);
                Check("one animation cannot release duplicate arrows",!actor.CompanionCombat.Bow.Fire() && BowAmmo(actor)==before,"arrows="+BowAmmo(actor));
                deadline=Time.time+5;while(Time.time<deadline && actor.CompanionCombat.Bow.Shots==shots)yield return null;
                foe.Teleport(actor.transform.position+actor.transform.forward*3);AimCombat(foe,gm.PlayerEntityBehaviour);yield return new WaitForSeconds(.4f);
                Check("enemy closing during arrow flight triggers melee fallback",table.GetItem(EquipSlots.RightHand)==blade && table.GetItem(EquipSlots.LeftHand)==shield,actor.Describe());
                foe.Teleport(origin+new Vector3(10,1,0));AimCombat(foe,gm.PlayerEntityBehaviour);yield return new WaitForSeconds(.5f);
                Check("opening range switches back to same bow",table.GetItem(EquipSlots.RightHand)==bow,actor.Describe());
                int protectedHealth=neutral.EntityBehaviour.Entity.CurrentHealth;
                neutral.Teleport((actor.transform.position+foe.transform.position)*.5f);neutral.GetComponent<EnemyMotor>().enabled=false;
                yield return new WaitForSeconds(.3f);before=BowAmmo(actor);
                actor.CompanionCombat.Bow.Cancel();actor.CompanionCombat.Bow.Attack(12,true);bool blocked=actor.CompanionCombat.Bow.Fire();
                Check("ally or bystander blocking shot prevents release and ammo loss",!blocked && BowAmmo(actor)==before && neutral.EntityBehaviour.Entity.CurrentHealth==protectedHealth,"arrows="+BowAmmo(actor));
                neutral.Teleport(origin+new Vector3(5,1,8));yield return new WaitForSeconds(.2f);
                before=BowAmmo(actor);actor.CompanionCombat.SetStance("passive");yield return new WaitForSeconds(.5f);
                Check("passive stance suppresses pending bow damage and consumption",BowAmmo(actor)==before && actor.CurrentMode!=NpcMode.Fighting,actor.Describe());
                actor.CompanionCombat.SetStance("defensive");AimCombat(foe,gm.PlayerEntityBehaviour);yield return new WaitForSeconds(.5f);
                actor.CompanionCombat.Regroup();before=BowAmmo(actor);yield return new WaitForSeconds(.5f);
                Check("regroup cancels archery",BowAmmo(actor)==before && actor.CompanionCombat.Regrouping,actor.Describe());
                gm.DisableAI=true;actor.CompanionCombat.CancelOrders();actor.Teleport(origin+new Vector3(-2,1,0));
                foreach(var item in items.SearchItems(ItemGroups.Weapons,(int)Weapons.Arrow))items.RemoveItem(item);
                actor.CompanionCombat.Bow.Loadout(12);
                Check("empty quiver uses same melee fallback",table.GetItem(EquipSlots.RightHand)==blade && table.GetItem(EquipSlots.LeftHand)==shield && BowAmmo(actor)==0,actor.Describe());
                items.RemoveItem(blade);table.UnequipItem(EquipSlots.RightHand);actor.CompanionCombat.Bow.Loadout(12);
                Check("missing fallback does not duplicate or borrow another item",table.GetItem(EquipSlots.RightHand)==null && !gm.PlayerEntity.Items.Contains(blade.UID),actor.Describe());
                var refill=ItemBuilder.CreateWeapon(Weapons.Arrow,WeaponMaterialTypes.Iron);refill.stackCount=1;items.AddItem(refill,noStack:true);actor.CompanionCombat.Bow.Loadout(12);
                Check("one replacement arrow re-enables selected bow",table.GetItem(EquipSlots.RightHand)==bow && BowAmmo(actor)==1,actor.Describe());
                bow.currentCondition=0;actor.CompanionCombat.Bow.Loadout(12);
                Check("broken bow switches to available melee or fists",!actor.CompanionCombat.Bow.UsingBow && BowAmmo(actor)==1,actor.Describe());bow.currentCondition=10;
                actor.CompanionCombat.Bow.Restore();
                actor.CompanionMode(null,NpcMode.Calm);NavigationPlayerAt(player,actor.transform.position+Vector3.forward,Vector3.back);
                Check("manual bow removal disables automatic bow selection",CompanionEquipment.SetEquipped(actor,bow.UID,false,out message) && inventory.Bow==0,message);
                actor.CompanionCombat.Bow.Loadout(12);Check("removed bow stays in pack",!bow.IsEquipped,actor.Describe());
                actor.CompanionCombat.SetStance("passive");
                var plain=TestDefinition("selftest_bow_native",Bravery.Brave,location,player,0);plain.RecruitCost=100;
                native=CombatPerson(location,plain,"selftest_bow_native",origin+new Vector3(-2,1,-5),true);
                yield return new WaitForSecondsRealtime(.5f);var nativeEntity=native.EntityBehaviour.Entity;
                CompanionEquipment.StopAll(nativeEntity);nativeEntity.Items.Clear();nativeEntity.Skills.SetPermanentSkillValue(DFCareer.Skills.Archery,100);
                var shortBow=ItemBuilder.CreateWeapon(Weapons.Short_Bow,WeaponMaterialTypes.Silver);nativeEntity.Items.AddItem(shortBow,noStack:true);
                ItemCollection.RegisterCustomItem("ANPCBowArrowFixture",typeof(CompanionBowTestArrow));
                var customAmmo=new CompanionBowTestArrow();customAmmo.stackCount=2;customAmmo.shortName="ANPC custom arrows";nativeEntity.Items.AddItem(customAmmo,noStack:true);
                NavigationPlayerAt(player,native.transform.position+Vector3.forward,Vector3.back);
                bool nativeEquip=CompanionEquipment.SetEquipped(native,shortBow.UID,true,out message);
                Check("short bow supports companion without custom sprite sheets",nativeEquip && (native.GetComponent<NpcSprite>()==null || !native.GetComponent<NpcSprite>().HasSet),message);
                AimCombat(foe,gm.PlayerEntityBehaviour);gm.DisableAI=false;deadline=Time.time+8;
                while(Time.time<deadline && native.CompanionCombat.Bow.Shots==0)yield return null;
                yield return new WaitForSeconds(.7f);gm.DisableAI=true;
                Check("native animation releases finite custom arrow stack",native.CompanionCombat.Bow.Shots>0 && BowAmmo(native)==2-native.CompanionCombat.Bow.Shots && nativeEntity.Items.GetItem(customAmmo.UID) is CompanionBowTestArrow,"shots="+native.CompanionCombat.Bow.Shots+" arrows="+BowAmmo(native));
                var customRecovered=foe.EntityBehaviour.Entity.Items.SearchItems(ItemGroups.Weapons,(int)Weapons.Arrow);
                Check("custom arrow class and metadata survive impact",customRecovered.Exists(delegate(DaggerfallUnityItem item){return item is CompanionBowTestArrow && item.shortName=="ANPC custom arrows";}),"target arrow stacks="+customRecovered.Count);
                foreach(string sheet in new[]{"attack_range","attack_ranged","attack_range_1","attack_ranged_1"})
                {
                    string key="selftest_bow_pose_"+sheet;var sheets=TestSpriteSet(false,"idle","attack",sheet);mod.Sprites.Add(key,sheets);
                    pose=CombatPerson(location,TestDefinition(key,Bravery.Brave,location,player,0),key,origin+new Vector3(0,1,-8),false);
                    yield return new WaitForSecondsRealtime(.2f);pose.enabled=false;pose.Motor.enabled=false;pose.GetComponent<EnemyAttack>().enabled=false;pose.GetComponent<EnemySenses>().enabled=false;
                    var poseEntity=pose.EntityBehaviour.Entity;CompanionEquipment.StopAll(poseEntity);poseEntity.Items.Clear();
                    var poseBow=ItemBuilder.CreateWeapon(Weapons.Short_Bow,WeaponMaterialTypes.Iron);poseEntity.Items.AddItem(poseBow,noStack:true);CompanionBow.EquipPrimary(poseEntity,poseBow);
                    var poseSprite=pose.GetComponent<NpcSprite>();var poseMobile=pose.GetComponentInChildren<MobileUnit>();
                    poseMobile.ChangeEnemyState(MobileStates.PrimaryAttack);yield return null;yield return null;
                    Check("equipped bow attack selects "+sheet,poseSprite.CurrentAnimation==sheet,"animation="+poseSprite.CurrentAnimation);
                    poseSprite.CancelAttack();poseMobile.ChangeEnemyState(MobileStates.Idle);yield return null;
                    poseEntity.ItemEquipTable.UnequipItem(EquipSlots.RightHand);var poseBlade=ItemBuilder.CreateWeapon(Weapons.Longsword,WeaponMaterialTypes.Iron);poseEntity.Items.AddItem(poseBlade,noStack:true);CompanionBow.EquipPrimary(poseEntity,poseBlade);
                    poseMobile.ChangeEnemyState(MobileStates.PrimaryAttack);yield return null;yield return null;
                    Check("melee equipment uses ordinary attack beside "+sheet,poseSprite.CurrentAnimation=="attack","animation="+poseSprite.CurrentAnimation);
                    NpcBrain.Discard(pose);pose=null;
                }
                Check("bow tactics remain mod scoped",!definition.RangedAtDistance && gm.PlayerEntity.GodMode,"base ranged="+definition.RangedAtDistance);
            }
            finally
            {
                DaggerfallUI.Instance.PopToHUD();NpcBrain.Discard(actor);NpcBrain.Discard(foe);NpcBrain.Discard(neutral);NpcBrain.Discard(native);NpcBrain.Discard(pose);UnityEngine.Object.Destroy(floor);
                DaggerfallUnity.Settings.BowLeftHandWithSwitching=left;gm.DisableAI=oldAI;gm.PlayerMouseLook.enabled=look;player.position=oldPosition;player.rotation=oldRotation;gm.MainCamera.transform.rotation=oldView;
            }
        }
    }
}

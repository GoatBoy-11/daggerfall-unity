using System;
using System.Collections;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.UserInterface;
using AdvancedNPCs.Core;
namespace AdvancedNPCs
{
    public partial class SelfTest
    {
        public const string CompanionPotionsSuite="potions";
        IEnumerator CompanionPotionsSteps(DaggerfallLocation location,Transform player)
        {
            var gm=GameManager.Instance;var pe=gm.PlayerEntity;
            var originalItems=pe.Items.SerializeItems();var originalEquipment=pe.ItemEquipTable.SerializeEquipTable();
            Vector3 oldPosition=player.position;bool oldAI=gm.DisableAI,look=gm.PlayerMouseLook.enabled;
            bool automatic=mod.Config.AutoHealingPotions;int threshold=mod.Config.HealingPotionThreshold,gold=pe.GoldPieces;
            NpcBrain actor=null,foe=null;var geometry=new GameObject("ANPC potion verification");geometry.transform.SetParent(location.transform,false);
            Vector3 origin=oldPosition+Vector3.up*24;
            NavigationBox(geometry.transform,"potion floor",origin-Vector3.up*.15f,new Vector3(50,.3f,30));
            try
            {
                gm.DisableAI=true;gm.PlayerMouseLook.enabled=false;NavigationPlayerAt(player,origin,Vector3.right);
                var definition=mod.Catalog.Generics.Find(delegate(NpcDefinition d){return d.Id=="ironclad_rogue_1";});
                actor=CombatPerson(location,definition,"selftest_potions",origin+new Vector3(-2,1,0),false);
                yield return new WaitForSecondsRealtime(.5f);pe.GoldPieces=1000;
                Check("potion settings load through native settings",mod.Config.FromSettings && !mod.Config.MissingKeys.Contains("AutoHealingPotions") && !mod.Config.MissingKeys.Contains("HealingPotionThreshold"),"threshold="+threshold);
                Check("potion defaults are enabled at 50 percent",new ModConfig().AutoHealingPotions && new ModConfig().HealingPotionThreshold==50,"defaults");
                Check("potion fixture recruits",mod.Companions.Command(actor,"recruit"),actor.Describe());
                var entity=actor.EntityBehaviour.Entity;var inventory=NpcCompanionInventory.For(actor);
                var sprite=actor.GetComponent<NpcSprite>();sprite.enabled=false;
                entity.ItemEquipTable.Clear();entity.Items.Clear();pe.ItemEquipTable.Clear();pe.Items.Clear();
                entity.MaxHealth=200;entity.CurrentHealth=40;
                string message;
                Check("empty inventory consumes nothing",!NpcCompanionInventory.GiveHealingPotion(actor,out message) && entity.CurrentHealth==40,message);
                var costly=ItemBuilder.CreatePotion(4937012);var cheap=ItemBuilder.CreatePotion(4975678,3);
                var stamina=ItemBuilder.CreatePotion(221871);stamina.value=1;
                pe.Items.AddItem(costly,noStack:true);pe.Items.AddItem(cheap,noStack:true);pe.Items.AddItem(stamina,noStack:true);
                Check("cheapest healing effect wins over inventory order and other potions",NpcCompanionInventory.CheapestHealingPotion(pe.Items)==cheap,"selected potion");
                Check("one potion restores 25 percent of max health",NpcCompanionInventory.GiveHealingPotion(actor,out message) && entity.CurrentHealth==90 && cheap.stackCount==2 && pe.Items.Contains(costly.UID),message);
                entity.CurrentHealth=190;
                Check("healing caps at max health",NpcCompanionInventory.GiveHealingPotion(actor,out message) && entity.CurrentHealth==200 && cheap.stackCount==1,message);
                Check("full health does not waste potion",!NpcCompanionInventory.GiveHealingPotion(actor,out message) && cheap.stackCount==1,message);
                entity.MaxHealth=101;entity.CurrentHealth=20;
                Check("fractional healing rounds up and removes last potion",NpcCompanionInventory.GiveHealingPotion(actor,out message) && entity.CurrentHealth==46 && !pe.Items.Contains(cheap.UID),message);
                costly.LinkQuestItem(1,null);
                Check("quest healing potions are protected",NpcCompanionInventory.CheapestHealingPotion(pe.Items)==null && !NpcCompanionInventory.GiveHealingPotion(actor,out message),message);
                costly.MakePermanent();
                actor.CompanionMode(gm.PlayerEntityBehaviour,NpcMode.Fighting);
                Check("manual healing is available during combat",NpcCompanionInventory.GiveHealingPotion(actor,out message) && entity.CurrentHealth==72,message);
                actor.CompanionMode(null,NpcMode.Calm);entity.MaxHealth=100;entity.CurrentHealth=49;
                var carried=ItemBuilder.CreatePotion(4975678,8);entity.Items.AddItem(carried,noStack:true);
                mod.Config.AutoHealingPotions=false;gm.DisableAI=false;
                yield return new WaitForSeconds(1.2f);
                Check("toggle off prevents automatic consumption",entity.CurrentHealth==49 && carried.stackCount==8,"health="+entity.CurrentHealth);
                mod.Config.AutoHealingPotions=true;mod.Config.HealingPotionThreshold=50;entity.CurrentHealth=50;
                yield return new WaitForSeconds(1.2f);
                Check("exactly 50 percent does not trigger",entity.CurrentHealth==50 && carried.stackCount==8,"health="+entity.CurrentHealth);
                entity.CurrentHealth=49;
                yield return new WaitForSeconds(1.2f);
                Check("normal companion tick consumes own potion below 50 percent",entity.CurrentHealth==74 && carried.stackCount==7,"health="+entity.CurrentHealth);
                inventory.UseHealingPotion();inventory.UseHealingPotion();
                Check("automatic use does not consume extra potions in one frame",carried.stackCount==7,"count="+carried.stackCount);
                mod.Config.HealingPotionThreshold=80;entity.CurrentHealth=79;
                yield return new WaitForSeconds(1.2f);
                Check("changed threshold applies immediately",entity.CurrentHealth==100 && carried.stackCount==6,"health="+entity.CurrentHealth);
                mod.Config.HealingPotionThreshold=30;entity.CurrentHealth=40;
                yield return new WaitForSeconds(1.2f);
                Check("lower threshold is respected",entity.CurrentHealth==40 && carried.stackCount==6,"health="+entity.CurrentHealth);
                gm.DisableAI=true;entity.CurrentHealth=20;
                yield return new WaitForSeconds(1.2f);
                inventory.UseHealingPotion();
                Check("disabled AI does not consume potions",entity.CurrentHealth==20 && carried.stackCount==6,"count="+carried.stackCount);
                gm.DisableAI=false;gm.PauseGame(true);inventory.UseHealingPotion();
                Check("paused game does not consume potions",entity.CurrentHealth==20 && carried.stackCount==6,"count="+carried.stackCount);
                gm.DisableAI=true;gm.PauseGame(false);mod.Config.HealingPotionThreshold=50;
                var playerPotion=ItemBuilder.CreatePotion(4975678,2);pe.Items.AddItem(playerPotion,noStack:true);
                Check("companion menu opens",CompanionOrdersWindow.Open(actor),actor.Describe());
                yield return new WaitForSecondsRealtime(.2f);
                var menu=DaggerfallUI.UIManager.TopWindow as CompanionOrdersWindow;
                var list=menu!=null?menu.ListBox:null;
                int heal=-1;
                if(list!=null)for(int i=0;i<list.Count;i++)if(list.GetItem(i).textLabel.Text=="Give Healing Potion")heal=i;
                Check("companion menu includes Give Healing Potion",heal>=0,"index="+heal);
                if(heal>=0){list.SelectedIndex=heal;list.UseSelectedItem();}
                Check("menu selection consumes player potion and heals once",entity.CurrentHealth==45 && playerPotion.stackCount==1 && carried.stackCount==6,"health="+entity.CurrentHealth);
                DaggerfallUI.Instance.PopToHUD();
                var snapshot=SaveLoadManager.Deserialize(typeof(CompanionInventoryData),actor.State.companion.belongings) as CompanionInventoryData;
                Check("consumed inventory and health are captured for saving",snapshot!=null && NpcCompanionInventory.RestoreItems(snapshot.items).GetItem(carried.UID).stackCount==6 && Mathf.Abs(actor.State.healthFraction-.45f)<.001f,"snapshot");
                mod.Config.AutoHealingPotions=false;sprite.enabled=true;
                yield return null;
                var voice=actor.GetComponent<NpcVoice>();int drinks=voice.Played(SoundPools.Drink);
                entity.CurrentHealth=20;
                CompanionOrdersWindow.Open(actor);yield return new WaitForSecondsRealtime(.2f);
                Check("potion heals while the companion menu is still open",NpcCompanionInventory.GiveHealingPotion(actor,out message) && entity.CurrentHealth==45,message);
                yield return new WaitForSecondsRealtime(.2f);
                Check("potion drink waits for companion menu exit",sprite.PotionDrinkBusy && !sprite.DrinkingPotion && voice.Played(SoundPools.Drink)==drinks,sprite.CurrentState);
                DaggerfallUI.Instance.PopToHUD();
                float until=Time.realtimeSinceStartup+3;
                while(!sprite.DrinkingPotion && Time.realtimeSinceStartup<until)yield return null;
                Check("menu exit starts a loaded drink animation with its drink sound",sprite.DrinkingPotion && PackageAnimations.Matches("drink",sprite.CurrentAnimation) && voice.Played(SoundPools.Drink)==drinks+1,sprite.CurrentAnimation);
                float duration=(float)sprite.Set.Set.Animations[sprite.CurrentAnimation].Frames/sprite.Set.Set.Fps;
                Vector3 drinkingAt=actor.transform.position;
                yield return new WaitForSeconds(duration+.25f);
                Check("potion drink plays one full cycle and returns to normal",!sprite.PotionDrinkBusy && sprite.CurrentState!=NpcSprite.PotionDrinkState && voice.Played(SoundPools.Drink)==drinks+1,sprite.CurrentState);
                Check("companion stands still while drinking",Vector3.Distance(drinkingAt,actor.transform.position)<.1f,actor.Describe());
                foe=CombatPerson(location,TestDefinition("selftest_potion_foe",Bravery.Brave,location,player,0),"selftest_potion_foe",origin+new Vector3(6,1,0),false);
                yield return new WaitForSecondsRealtime(.5f);
                foe.SwitchHostile(true);AimCombat(foe,actor.EntityBehaviour);
                mod.Companions.Command(actor,"aggressive");gm.DisableAI=false;
                until=Time.realtimeSinceStartup+4;
                while(actor.CurrentMode!=NpcMode.Fighting && Time.realtimeSinceStartup<until)yield return null;
                Check("potion animation fixture enters a real companion fight",actor.CurrentMode==NpcMode.Fighting && actor.Threat==foe.EntityBehaviour,actor.Describe());
                mod.Config.AutoHealingPotions=true;entity.CurrentHealth=49;drinks=voice.Played(SoundPools.Drink);
                until=Time.realtimeSinceStartup+5;
                while(!sprite.DrinkingPotion && Time.realtimeSinceStartup<until)yield return null;
                Check("automatic combat healing plays drink animation and sound",sprite.DrinkingPotion && entity.CurrentHealth==74 && voice.Played(SoundPools.Drink)==drinks+1,actor.Describe());
                Check("drinking prevents simultaneous native attacks",!actor.CompanionCombat.AllowsAttack && !actor.GetComponent<EnemyAttack>().enabled,sprite.CurrentState);
                mod.Config.AutoHealingPotions=false;gm.DisableAI=true;
                yield return new WaitForSeconds(duration+.25f);
                UnityEngine.Object.Destroy(foe.gameObject);foe=null;
                actor.CompanionMode(null,NpcMode.Calm);actor.CompanionCombat.CancelOrders();
                entity.CurrentHealth=20;
                var noDrink=PackageAnimations.Variants(sprite.Set.Set,"drink");
                foreach(var animation in noDrink)sprite.Set.Set.Animations.Remove(animation.Name);
                try
                {
                    drinks=voice.Played(SoundPools.Drink);pe.Items.AddItem(ItemBuilder.CreatePotion(4975678),noStack:true);
                    Check("companion without drink animation still heals normally",NpcCompanionInventory.GiveHealingPotion(actor,out message) && entity.CurrentHealth==45,message);
                    yield return new WaitForSecondsRealtime(.2f);
                    Check("missing drink animation plays no animation or drink sound",!sprite.PotionDrinkBusy && sprite.CurrentState!=NpcSprite.PotionDrinkState && voice.Played(SoundPools.Drink)==drinks,sprite.CurrentState);
                    gm.DisableAI=false;mod.Config.AutoHealingPotions=true;entity.CurrentHealth=49;
                    yield return new WaitForSeconds(1.2f);
                    Check("missing drink animation preserves automatic potion healing",entity.CurrentHealth==74 && !sprite.PotionDrinkBusy && voice.Played(SoundPools.Drink)==drinks,"health="+entity.CurrentHealth);
                }
                finally { foreach(var animation in noDrink)sprite.Set.Set.Animations.Add(animation.Name,animation); }
                gm.DisableAI=true;mod.Config.AutoHealingPotions=false;
                // The menu test used the player's last potion; retain one for the ownership guards below.
                playerPotion=ItemBuilder.CreatePotion(4975678);pe.Items.AddItem(playerPotion,noStack:true);
                int remaining=carried.stackCount;
                actor.Teleport(origin+Vector3.right*30);
                Check("distant actor cannot receive player potion",!NpcCompanionInventory.GiveHealingPotion(actor,out message) && playerPotion.stackCount==1,message);
                NavigationPlayerAt(player,actor.transform.position+Vector3.right,Vector3.left);
                mod.Companions.Command(actor,"dismiss");gm.DisableAI=false;inventory.UseHealingPotion();
                Check("dismissed actor cannot use either potion source",!NpcCompanionInventory.GiveHealingPotion(actor,out message) && playerPotion.stackCount==1 && carried.stackCount==remaining,message);
            }
            finally
            {
                gm.DisableAI=true;DaggerfallUI.Instance.PopToHUD();gm.PauseGame(false);
                if(actor!=null)UnityEngine.Object.Destroy(actor.gameObject);
                if(foe!=null)UnityEngine.Object.Destroy(foe.gameObject);
                pe.ItemEquipTable.Clear();pe.Items.Clear();pe.Items.DeserializeItems(originalItems);pe.ItemEquipTable.DeserializeEquipTable(originalEquipment,pe.Items);
                pe.GoldPieces=gold;mod.Config.AutoHealingPotions=automatic;mod.Config.HealingPotionThreshold=threshold;
                UnityEngine.Object.Destroy(geometry);player.position=oldPosition;gm.DisableAI=oldAI;gm.PlayerMouseLook.enabled=look;
            }
        }
    }
}

using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using DaggerfallConnect.Save;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.Serialization;
using AdvancedNPCs.Core;
namespace AdvancedNPCs
{
    // Registered only by this opt-in test. Exercises DFU's actual custom-item serialization path.
    public class CompanionTradingTestItem : DaggerfallUnityItem
    {
        public CompanionTradingTestItem():base(ItemGroups.Weapons,0) {}
        public override ItemData_v1 GetSaveData() {var data=base.GetSaveData();data.className="ANPCTradingFixture";return data;}
    }
    public partial class SelfTest
    {
        public const string CompanionTradingSuite="trading";
        IEnumerator CompanionTradingSteps(DaggerfallLocation location,Transform player)
        {
            var gm=GameManager.Instance;var pe=gm.PlayerEntity;
            Vector3 oldPosition=player.position;bool oldAI=gm.DisableAI,look=gm.PlayerMouseLook.enabled;
            int gold=pe.GoldPieces,strength=pe.Stats.GetPermanentStatValue(0);float multiplier=pe.IncreasedWeightAllowanceMultiplier;
            var originalItems=pe.Items.SerializeItems();var originalEquipment=pe.ItemEquipTable.SerializeEquipTable();
            NpcBrain actor=null;var geometry=new GameObject("ANPC trading verification");geometry.transform.SetParent(location.transform,false);
            Vector3 origin=oldPosition+Vector3.up*24;NavigationBox(geometry.transform,"trading floor",origin-Vector3.up*.15f,new Vector3(50,.3f,30));
            ItemCollection.RegisterCustomItem("ANPCTradingFixture",typeof(CompanionTradingTestItem));
            try
            {
                gm.DisableAI=true;gm.PlayerMouseLook.enabled=false;NavigationPlayerAt(player,origin,Vector3.right);
                var definition=mod.Catalog.Generics.Find(delegate(NpcDefinition d){return d.Id=="ironclad_rogue_1";});
                actor=CombatPerson(location,definition,"selftest_trading",origin+new Vector3(-2,1,0),false);
                yield return new WaitForSecondsRealtime(.5f);pe.GoldPieces=1000;
                Check("fresh hire seeds native belongings once",mod.Companions.Command(actor,"recruit") && NpcCompanionInventory.For(actor).Ready && !string.IsNullOrEmpty(actor.State.companion.belongings),actor.Describe());
                var entity=actor.EntityBehaviour.Entity;var items=entity.Items;
                string initial=actor.State.companion.belongings;NpcCompanionInventory.For(actor).Capture();
                Check("repeated capture never generates a second kit",initial==actor.State.companion.belongings,"items="+items.Count);
                entity.ItemEquipTable.Clear();items.Clear();pe.ItemEquipTable.Clear();pe.Items.Clear();pe.GoldPieces=0;
                entity.Stats.SetPermanentStatValue(0,20);pe.Stats.SetPermanentStatValue(0,100);
                Check("capacity uses live native Strength getter",entity.MaxEncumbrance==Mathf.Floor(entity.Stats.LiveStrength*1.5f),"capacity="+entity.MaxEncumbrance);
                var blade=ItemBuilder.CreateItem(ItemGroups.Weapons,(int)Weapons.Dagger);blade.weightInKg=12;blade.currentCondition=17;pe.Items.AddItem(blade,noStack:true);
                ulong bladeUid=blade.UID;string message;
                Check("give moves exact UID and condition",CompanionTrade.Transfer(actor,true,bladeUid,1,out message) && items.GetItem(bladeUid)==blade && !pe.Items.Contains(bladeUid) && blade.currentCondition==17,message);
                Check("take returns exact item object",CompanionTrade.Transfer(actor,false,bladeUid,1,out message) && pe.Items.GetItem(bladeUid)==blade && !items.Contains(bladeUid),message);
                Check("invalid quantities and stale item IDs do not mutate inventories",!CompanionTrade.Transfer(actor,true,bladeUid,0,out message) && !CompanionTrade.Transfer(actor,true,bladeUid,-1,out message) && !CompanionTrade.Transfer(actor,true,ulong.MaxValue,1,out message) && pe.Items.GetItem(bladeUid)==blade,message);
                items.AddItem(blade,noStack:true);
                Check("duplicate UID is rejected without removing either owner",!CompanionTrade.Transfer(actor,true,bladeUid,1,out message) && pe.Items.Contains(bladeUid) && items.Contains(bladeUid),message);
                items.RemoveItem(blade);
                var stack=new CompanionTradingTestItem();stack.stackCount=10;stack.weightInKg=2;stack.currentCondition=19;pe.Items.AddItem(stack,noStack:true);
                Check("split preserves native custom type condition and totals",CompanionTrade.Transfer(actor,true,stack.UID,3,out message) && stack.stackCount==7 && items.Count==1 && items.GetItem(0) is CompanionTradingTestItem && items.GetItem(0).currentCondition==19 && items.GetItem(0).stackCount==3,message);
                var split=items.GetItem(0);ulong splitUid=split.UID;
                Check("split allocates a different UID",splitUid!=stack.UID,"split="+splitUid);
                Check("over-capacity quantity leaves both owners unchanged",!CompanionTrade.Transfer(actor,true,stack.UID,100,out message) && stack.stackCount==7 && items.GetItem(splitUid).stackCount==3,message);
                entity.Stats.SetPermanentStatValue(0,4);
                Check("exact capacity refuses extra positive weight",CompanionTrade.Maximum(actor,true,stack.UID,out message)==0,"weight="+items.GetWeight()+" max="+entity.MaxEncumbrance);
                entity.Stats.SetPermanentStatValue(0,2);
                Check("Strength loss keeps overloaded belongings",items.GetItem(splitUid).stackCount==3 && CompanionTrade.Maximum(actor,true,stack.UID,out message)==0,message);
                var zero=ItemBuilder.CreateItem(ItemGroups.Weapons,(int)Weapons.Dagger);zero.weightInKg=0;pe.Items.AddItem(zero,noStack:true);
                Check("overloaded owner may receive weightless item",CompanionTrade.Transfer(actor,true,zero.UID,1,out message),message);
                entity.SetIncreasedWeightAllowanceMultiplier(10);
                Check("native increased allowance is respected",CompanionTrade.Maximum(actor,true,stack.UID,out message)>0,"capacity="+entity.MaxEncumbrance);
                typeof(DaggerfallWorkshop.Game.Entity.DaggerfallEntity).GetProperty("IncreasedWeightAllowanceMultiplier").SetValue(entity,0f,null);entity.Stats.SetPermanentStatValue(0,100);
                pe.Stats.SetPermanentStatValue(0,1);pe.GoldPieces=1000;
                Check("player capacity includes wallet gold",CompanionTrade.Maximum(actor,false,splitUid,out message)==0,"carried="+pe.CarriedWeight);
                pe.Stats.SetPermanentStatValue(0,100);pe.GoldPieces=40;
                Check("give gold debits wallet once",CompanionTrade.Transfer(actor,true,0,15,out message) && pe.GoldPieces==25,message);
                DaggerfallUnityItem coin=null;for(int i=0;i<items.Count;i++)if(items.GetItem(i).ItemGroup==ItemGroups.Currency)coin=items.GetItem(i);
                Check("take gold credits wallet without inventory gold duplicate",coin!=null && CompanionTrade.Transfer(actor,false,coin.UID,15,out message) && pe.GoldPieces==40 && !items.Contains(coin.UID),message);
                var equipped=ItemBuilder.CreateItem(ItemGroups.Weapons,(int)Weapons.Dagger);items.AddItem(equipped,noStack:true);entity.ItemEquipTable.EquipItem(equipped,true,false);
                Check("worn gear stays equipped and cannot be taken",!CompanionTrade.Transfer(actor,false,equipped.UID,1,out message) && equipped.IsEquipped && items.Contains(equipped.UID),message);
                var quest=ItemBuilder.CreateItem(ItemGroups.Weapons,(int)Weapons.Dagger);var qdata=quest.GetSaveData();qdata.isQuestItem=true;quest=new DaggerfallUnityItem(qdata);pe.Items.AddItem(quest,noStack:true);
                Check("quest items keep their owner",!CompanionTrade.Transfer(actor,true,quest.UID,1,out message) && pe.Items.Contains(quest.UID),message);
                var summoned=ItemBuilder.CreateItem(ItemGroups.Weapons,(int)Weapons.Dagger);summoned.TimeForItemToDisappear=1;
                pe.Items.AddItem(summoned,noStack:true);
                Check("summoned gear cannot be handed to companion",!CompanionTrade.Transfer(actor,true,summoned.UID,1,out message),message);
                pe.Items.RemoveItem(summoned);
                var transport=ItemBuilder.CreateItem(ItemGroups.Transportation,(int)Transportation.Horse);pe.Items.AddItem(transport,noStack:true);
                Check("transportation cannot become companion baggage",!CompanionTrade.Transfer(actor,true,transport.UID,1,out message),message);
                pe.Items.RemoveItem(quest);pe.Items.RemoveItem(transport);
                entity.Stats.SetPermanentStatValue(0,71);actor.CaptureState();
                var snapshot=(CompanionInventoryData)SaveLoadManager.Deserialize(typeof(CompanionInventoryData),actor.State.companion.belongings);
                Check("native item snapshot keeps custom class equipment and base stats",snapshot.stats[0]==71 && Array.IndexOf(snapshot.equipment,equipped.UID)>=0 && NpcCompanionInventory.RestoreItems(snapshot.items).GetItem(splitUid) is CompanionTradingTestItem,"items="+snapshot.items.Length);
                string armor=string.Join(",",Array.ConvertAll(entity.ArmorValues,delegate(sbyte v){return v.ToString();}));
                var missing=split.GetSaveData();missing.className="MissingFixtureMod";bool refused=false;
                try {NpcCompanionInventory.RestoreItems(new[]{missing});}catch(InvalidOperationException){refused=true;}
                Check("missing custom item cannot silently become a vanilla item",refused,"missing class rejected");
                CompanionOrdersWindow.Open(actor);yield return new WaitForSecondsRealtime(.2f);
                var menu=DaggerfallUI.UIManager.TopWindow as CompanionOrdersWindow;
                if(menu!=null){menu.ListBox.SelectedIndex=BarkMenuIndex(menu,"Give / Take belongings");menu.ListBox.UseSelectedItem();}
                yield return new WaitForSecondsRealtime(.4f);
                var window=DaggerfallUI.UIManager.TopWindow as CompanionTradeWindow;
                Check("calm companion menu opens two-sided trading window",window!=null && window.GiveList.Count>0 && window.TakeList.Count>0,"window="+DaggerfallUI.UIManager.TopWindow);
                if(window==null)yield break;
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(Application.persistentDataPath,"ANPC-trading.png"));yield return new WaitForSecondsRealtime(.2f);
                // Gold is the first Give row. Repeated native use opens one quantity prompt without transferring.
                int beforeGold=pe.GoldPieces;window.GiveList.SelectedIndex=0;window.GiveList.UseSelectedItem();window.GiveList.UseSelectedItem();
                yield return new WaitForSecondsRealtime(.2f);
                var prompt=window.QuantityWindow;
                Check("stack selection waits for quantity without auto transfer",prompt!=null && pe.GoldPieces==beforeGold,"gold="+pe.GoldPieces);
                if(prompt!=null)prompt.CancelWindow();yield return new WaitForSecondsRealtime(.2f);
                Check("cancel quantity preserves both inventories",pe.GoldPieces==beforeGold && DaggerfallUI.UIManager.TopWindow==window,"gold="+pe.GoldPieces);
                window.GiveList.UseSelectedItem();yield return new WaitForSecondsRealtime(.2f);prompt=window.QuantityWindow;
                if(prompt!=null)typeof(DaggerfallWorkshop.Game.UserInterfaceWindows.DaggerfallInputMessageBox).GetMethod("ReturnPlayerInputEvent",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(prompt,new object[]{prompt,"5"});
                Check("quantity confirmation performs one checked transfer",pe.GoldPieces==beforeGold-5,"gold="+pe.GoldPieces);
                yield return new WaitForSecondsRealtime(.2f);window.GiveList.SelectedIndex=0;window.GiveList.UseSelectedItem();yield return new WaitForSecondsRealtime(.2f);
                prompt=window.QuantityWindow;beforeGold=pe.GoldPieces;entity.Stats.SetPermanentStatValue(0,1);
                if(prompt!=null)typeof(DaggerfallWorkshop.Game.UserInterfaceWindows.DaggerfallInputMessageBox).GetMethod("ReturnPlayerInputEvent",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(prompt,new object[]{prompt,"5"});
                Check("pending quantity rechecks changed carrying capacity",pe.GoldPieces==beforeGold,"gold="+pe.GoldPieces);
                DaggerfallUI.Instance.PopToHUD();entity.Stats.SetPermanentStatValue(0,71);actor.CaptureState();
                var save=new NpcSaveDataInterface(mod.States,mod.Placed,mod.Flags,mod.RaiseStateRestored);
                string encoded=SaveLoadManager.Serialize(typeof(NpcSaveData),save.GetSaveData());
                var stale=actor;CompanionTradeWindow.Open(actor);yield return new WaitForSecondsRealtime(.2f);
                save.RestoreSaveData(SaveLoadManager.Deserialize(typeof(NpcSaveData),encoded));yield return new WaitForSecondsRealtime(1);
                actor=NpcBrain.Find("selftest_trading");
                Check("mod save restore retains native items equipment and permanent Strength",actor!=null && actor.EntityBehaviour.Entity.Items.GetItem(splitUid) is CompanionTradingTestItem && actor.EntityBehaviour.Entity.Items.GetItem(splitUid).stackCount==3 && actor.EntityBehaviour.Entity.Items.GetItem(equipped.UID).IsEquipped && actor.EntityBehaviour.Entity.Stats.GetPermanentStatValue(0)==71,"restored="+(actor!=null));
                Check("save restores native armor values",actor!=null && string.Join(",",Array.ConvertAll(actor.EntityBehaviour.Entity.ArmorValues,delegate(sbyte v){return v.ToString();}))==armor,"armor="+armor);
                Check("stale actor cannot transfer after load",!CompanionTrade.Transfer(stale,true,0,1,out message) && !(DaggerfallUI.UIManager.TopWindow is CompanionTradeWindow),message);
                if(actor==null)yield break;
                NavigationPlayerAt(player,actor.transform.position+Vector3.right,Vector3.left);
                actor.Teleport(actor.transform.position+Vector3.right*30);
                Check("distant companion cannot trade",!CompanionTrade.Transfer(actor,true,0,1,out message),message);
                NavigationPlayerAt(player,actor.transform.position+Vector3.right,Vector3.left);
                mod.Companions.Command(actor,"dismiss");
                Check("dismissed companion cannot trade",!CompanionTrade.Transfer(actor,true,0,1,out message),message);
                pe.GoldPieces=1000;mod.Companions.Command(actor,"recruit");
                Check("rehire keeps existing belongings",actor.EntityBehaviour.Entity.Items.Contains(splitUid),actor.Describe());
                actor.CompanionMode(gm.PlayerEntityBehaviour,NpcMode.Fighting);
                Check("combat blocks trading",!CompanionTrade.Transfer(actor,true,0,1,out message),message);
                actor.EntityBehaviour.Entity.CurrentHealth=0;yield return new WaitForSecondsRealtime(.7f);
                var corpse=actor.EntityBehaviour.CorpseLootContainer;
                Check("native corpse takes sole ownership of carried items",corpse!=null && corpse.Items.Contains(splitUid) && !actor.EntityBehaviour.Entity.Items.Contains(splitUid) && actor.State.companion.belongings==null,"corpse="+(corpse!=null));
                Check("dead contract cannot trade",!CompanionTrade.Transfer(actor,false,splitUid,1,out message),message);
            }
            finally
            {
                DaggerfallUI.Instance.PopToHUD();NpcBrain.Discard(actor);UnityEngine.Object.Destroy(geometry);
                pe.ItemEquipTable.Clear();pe.Items.DeserializeItems(originalItems);pe.ItemEquipTable.DeserializeEquipTable(originalEquipment,pe.Items);
                pe.GoldPieces=gold;pe.Stats.SetPermanentStatValue(0,strength);pe.SetIncreasedWeightAllowanceMultiplier(multiplier);
                gm.DisableAI=oldAI;gm.PlayerMouseLook.enabled=look;player.position=oldPosition;
            }
        }
    }
}

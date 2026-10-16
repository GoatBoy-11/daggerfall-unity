using System;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Banking;
using DaggerfallWorkshop.Game.Items;
using AdvancedNPCs.Core;
namespace AdvancedNPCs
{
    /// <summary>Single-threaded, checked exchange; never mutates quest ownership or globally replaces inventory UI.</summary>
    public static class CompanionTrade
    {
        // Released DFU uses a constant; newer builds expose the item-template-based property.
        static readonly System.Reflection.PropertyInfo goldWeight=typeof(DaggerfallBankManager).GetProperty("goldPieceWeightInKg");
        static double GoldWeight { get { return goldWeight!=null?Convert.ToDouble(goldWeight.GetValue(null,null)):DaggerfallBankManager.goldUnitWeightInKg; } }
        public static bool Available(NpcBrain brain)
        { return CompanionOrdersWindow.Available(brain) && brain.CurrentMode==NpcMode.Calm && NpcCompanionInventory.For(brain).Ready; }
        public static string Blocked(DaggerfallUnityItem item)
        {
            if(item.IsEquipped) return "Unequip this item before trading it.";
            if(item.IsQuestItem) return "Quest items must stay with their owner.";
            if(item.IsSummoned) return "Summoned items cannot be traded.";
            if(item.ItemGroup==ItemGroups.Transportation) return "Transportation cannot be carried by a companion.";
            return null;
        }
        public static ItemCollection Source(NpcBrain brain,bool give)
        { return give?GameManager.Instance.PlayerEntity.Items:brain.EntityBehaviour.Entity.Items; }
        public static int Maximum(NpcBrain brain,bool give,ulong uid,out string problem)
        {
            problem=null;
            if(!Available(brain)) { problem="Trading requires a nearby, calm hired companion.";return 0; }
            var player=GameManager.Instance.PlayerEntity;
            var item=Source(brain,give).GetItem(uid);
            int count;double weight;
            if(give && uid==0) {count=player.GoldPieces;weight=GoldWeight;}
            else
            {
                if(item==null) {problem="This item is no longer available.";return 0;}
                problem=Blocked(item);if(problem!=null)return 0;
                count=item.stackCount;weight=item.EffectiveUnitWeightInKg();
                if(!give && item.ItemGroup==ItemGroups.Currency) {count=Math.Min(count,int.MaxValue-player.GoldPieces);weight=GoldWeight;}
            }
            double carried=give?brain.EntityBehaviour.Entity.Items.GetWeight():player.CarriedWeight;
            double capacity=give?brain.EntityBehaviour.Entity.MaxEncumbrance:player.MaxEncumbrance;
            int maximum=CompanionTradeRules.Maximum(count,weight,carried,capacity);
            if(maximum==0) problem=count<=0?"Nothing available to transfer.":"The recipient cannot carry any more weight.";
            return maximum;
        }
        public static bool Transfer(NpcBrain brain,bool give,ulong uid,int quantity,out string message)
        {
            int maximum=Maximum(brain,give,uid,out message);
            if(quantity<=0 || quantity>maximum) { if(message==null)message="That quantity no longer fits. Check the recipient's load.";return false; }
            var player=GameManager.Instance.PlayerEntity;var source=Source(brain,give);
            var target=give?brain.EntityBehaviour.Entity.Items:player.Items;
            var item=source.GetItem(uid);
            bool wallet=give && uid==0,coins=!give && item.ItemGroup==ItemGroups.Currency;
            DaggerfallUnityItem piece=null;
            try
            {
                if(wallet) piece=ItemBuilder.CreateGoldPieces(quantity);
                else if(!coins)
                {
                    if(target.Contains(uid)) {message="This item is already in the recipient's inventory.";return false;}
                    if(quantity==item.stackCount) piece=item;
                    else
                    {
                        // Native SplitStack/Clone lose custom subclasses and some item data. Restore a full native record instead.
                        var data=item.GetSaveData();data.uid=DaggerfallUnity.NextUID;data.stackCount=quantity;
                        piece=NpcCompanionInventory.RestoreItems(new[]{data}).GetItem(0);
                        if(piece.GetType()!=item.GetType()) throw new InvalidOperationException("Custom stack type could not be preserved.");
                    }
                }
                // Construct/validate before touching either owner. Keep distinct stacks so condition and custom data survive.
                if(piece!=null) target.AddItem(piece,noStack:true);
                if(wallet) player.GoldPieces-=quantity;
                else
                {
                    if(quantity==item.stackCount) source.RemoveItem(item);
                    else item.stackCount-=quantity;
                    if(coins) player.GoldPieces+=quantity;
                    if(give && player.LightSource==item) player.LightSource=null;
                }
            }
            catch(Exception exception)
            {
                if(piece!=null && target.Contains(piece.UID)) target.RemoveItem(piece);
                message="Item transfer could not preserve its data. Nothing transferred.";
                AdvancedNpcsMod.LogError(brain.Id+": "+exception.Message);return false;
            }
            var inventory=NpcCompanionInventory.For(brain);inventory.Invalidate();inventory.Capture(false);
            message=quantity+" "+(wallet?"gold":item.LongName)+(give?" given.":" taken.");return true;
        }
    }
}

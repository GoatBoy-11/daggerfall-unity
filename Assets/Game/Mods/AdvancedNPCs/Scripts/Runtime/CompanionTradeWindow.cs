using System;
using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
namespace AdvancedNPCs
{
    /// <summary>Companion-only exchange. Selection records UIDs; every confirmation checks the live owners again.</summary>
    public class CompanionTradeWindow : DaggerfallPopupWindow
    {
        readonly NpcBrain brain;
        readonly Panel panel=new Panel();
        readonly ListBox giveList=new ListBox(),takeList=new ListBox();
        readonly List<ulong> giveIds=new List<ulong>(),takeIds=new List<ulong>();
        readonly VerticalScrollBar giveScroll=new VerticalScrollBar(),takeScroll=new VerticalScrollBar();
        TextLabel playerLoad,companionLoad,status,detail;
        Button equipmentButton;
        int actionFrame=-1,pendingFrame;
        bool pending,pendingGive,awaitingRelease;
        ulong pendingUid;
        float nextRefresh;
        DaggerfallInputMessageBox quantityWindow;
        public Button EquipmentButton { get { return equipmentButton; } }
        public ListBox GiveList { get { return giveList; } }
        public ListBox TakeList { get { return takeList; } }
        public DaggerfallInputMessageBox QuantityWindow { get { return quantityWindow; } }
        public static bool Open(NpcBrain person)
        {
            if(!CompanionTrade.Available(person))
            {
                var inventory=person!=null?person.GetComponent<NpcCompanionInventory>():null;
                DaggerfallUI.AddHUDText(inventory!=null && !inventory.Ready?inventory.Problem:"Trading requires a nearby, calm hired companion.");return false;
            }
            DaggerfallUI.UIManager.PushWindow(new CompanionTradeWindow(person));return true;
        }
        public CompanionTradeWindow(NpcBrain person):base(DaggerfallUI.UIManager,DaggerfallUI.UIManager.TopWindow) { brain=person; }
        protected override void Setup()
        {
            base.Setup();
            panel.Size=new Vector2(306,194);panel.HorizontalAlignment=HorizontalAlignment.Center;panel.VerticalAlignment=VerticalAlignment.Middle;
            DaggerfallUI.Instance.SetDaggerfallPopupStyle(DaggerfallUI.PopupStyle.Parchment,panel);NativePanel.Components.Add(panel);
            Label(0,0,"Companion belongings",280);
            Label(0,14,"You",132);Label(148,14,brain.DisplayName,132);
            playerLoad=Label(0,25,"",132);companionLoad=Label(148,25,"",132);
            SetupList(giveList,giveScroll,0);SetupList(takeList,takeScroll,148);
            giveList.OnUseSelectedItem+=Give;takeList.OnUseSelectedItem+=Take;
            giveList.OnSelectItem+=delegate {Describe(true);};takeList.OnSelectItem+=delegate {Describe(false);};
            AddButton(0,105,128,"Give >",delegate {Give();});AddButton(148,105,128,"< Take",delegate {Take();});
            Label(0,122,"Worn gear counts in load.",132);
            equipmentButton=AddButton(148,120,128,"Equip / Unequip",delegate {ChangeEquipment();});
            equipmentButton.Hotkey=DaggerfallShortcut.GetBinding(DaggerfallShortcut.Buttons.InventoryEquip);
            detail=Label(0,136,"Select companion gear to equip it.",282);status=Label(0,147,"Select an item; use Give / Take.",282);
            AddButton(105,158,72,"Done",delegate {CloseWindow();});
            Refresh();
        }
        TextLabel Label(float x,float y,string text,float width)
        {
            var label=new TextLabel {Position=new Vector2(x,y),Text=Fit(text,width)};panel.Components.Add(label);return label;
        }
        static string Fit(string text,float width)
        {
            if(DaggerfallUI.DefaultFont.CalculateTextWidth(text,Vector2.one)<=width) return text;
            while(text.Length>0 && DaggerfallUI.DefaultFont.CalculateTextWidth(text+"...",Vector2.one)>width) text=text.Substring(0,text.Length-1);
            return text+"...";
        }
        Button AddButton(float x,float y,float width,string text,Action action)
        {
            var button=DaggerfallUI.AddButton(new Rect(x,y,width,12),panel);button.Label.Text=text;
            button.BackgroundColor=new Color(0,0,0,.15f);button.OnMouseClick+=delegate {action();};return button;
        }
        void SetupList(ListBox list,VerticalScrollBar scroll,float x)
        {
            list.Position=new Vector2(x,38);list.Size=new Vector2(126,64);list.RowsDisplayed=8;panel.Components.Add(list);
            scroll.Position=new Vector2(x+129,38);scroll.Size=new Vector2(5,64);scroll.DisplayUnits=8;
            scroll.OnScroll+=delegate {list.ScrollIndex=scroll.ScrollIndex;};panel.Components.Add(scroll);
        }
        void Fill(ListBox list,List<ulong> ids,bool give)
        {
            int selected=list.SelectedIndex,offset=list.ScrollIndex;list.ClearItems();ids.Clear();
            var source=CompanionTrade.Source(brain,give);
            if(give && GameManager.Instance.PlayerEntity.GoldPieces>0) {ids.Add(0);list.AddItem(Fit(GameManager.Instance.PlayerEntity.GoldPieces+" gold",126));}
            for(int i=0;i<source.Count;i++)
            {
                var item=source.GetItem(i);ids.Add(item.UID);
                list.AddItem(Fit((item.IsEquipped?"[worn] ":"")+(item.stackCount>1?item.stackCount+" ":"")+item.LongName,126));
            }
            if(list.Count>0) list.SelectedIndex=Math.Max(0,Math.Min(selected,list.Count-1));
            list.ScrollIndex=offset;
        }
        public void Refresh()
        {
            Fill(giveList,giveIds,true);Fill(takeList,takeIds,false);Loads();
        }
        void Loads()
        {
            var player=GameManager.Instance.PlayerEntity;var companion=brain.EntityBehaviour.Entity;
            playerLoad.Text=Fit(player.CarriedWeight.ToString("0.##")+" / "+player.MaxEncumbrance.ToString("0.##")+" kg",132);
            companionLoad.Text=Fit(companion.Items.GetWeight().ToString("0.##")+" / "+companion.MaxEncumbrance.ToString("0.##")+" kg",132);
        }
        void Describe(bool give)
        {
            if(detail==null)return;
            var list=give?giveList:takeList;var ids=give?giveIds:takeIds;int index=list.SelectedIndex;
            if(index<0 || index>=ids.Count)return;
            var item=CompanionTrade.Source(brain,give).GetItem(ids[index]);
            string problem;int maximum=CompanionTrade.Maximum(brain,give,ids[index],out problem);
            detail.Text=Fit(item==null?"Gold coins":item.LongName+" ("+item.currentCondition+"/"+item.maxCondition+")",282);
            if(!give && item!=null)
            {
                if(equipmentButton!=null)equipmentButton.Label.Text=item.IsEquipped?"Unequip":"Equip";
                string gearProblem=CompanionEquipment.Reason(brain,item);
                if(item.IsEquipped)problem="Worn on "+CompanionEquipment.SlotName(item.EquipSlot)+". Unequip before taking.";
                else if(gearProblem==null)problem="In pack. Equip to use; Take returns it to you.";
            }
            if(status!=null)status.Text=Fit(problem??("Can transfer up to "+maximum+"."),282);
        }
        public void ChangeEquipment()
        {
            if(DaggerfallUI.UIManager.TopWindow!=this || pending || awaitingRelease || actionFrame==Time.frameCount)return;
            actionFrame=Time.frameCount;awaitingRelease=true;
            int index=takeList.SelectedIndex;if(index<0 || index>=takeIds.Count)return;
            if(!CompanionTrade.Available(brain))return;
            ulong id=takeIds[index];var item=CompanionTrade.Source(brain,false).GetItem(id);if(item==null)return;
            string message;CompanionEquipment.SetEquipped(brain,id,!item.IsEquipped,out message);Refresh();status.Text=Fit(message,282);
        }
        public void Give() {Choose(true);}
        public void Take() {Choose(false);}
        void Choose(bool give)
        {
            if(DaggerfallUI.UIManager.TopWindow!=this || pending || awaitingRelease || actionFrame==Time.frameCount) return;
            actionFrame=Time.frameCount;awaitingRelease=true;
            var list=give?giveList:takeList;var ids=give?giveIds:takeIds;int index=list.SelectedIndex;
            if(index<0 || index>=ids.Count)return;
            string problem;int maximum=CompanionTrade.Maximum(brain,give,ids[index],out problem);
            if(maximum<=0){status.Text=Fit(problem,282);return;}
            var item=CompanionTrade.Source(brain,give).GetItem(ids[index]);
            int available=item!=null?item.stackCount:GameManager.Instance.PlayerEntity.GoldPieces;
            if(available>1) {pending=true;pendingGive=give;pendingUid=ids[index];pendingFrame=Time.frameCount;return;}
            Commit(give,ids[index],1);
        }
        void Commit(bool give,ulong uid,int quantity)
        {
            string message;CompanionTrade.Transfer(brain,give,uid,quantity,out message);
            Refresh();status.Text=Fit(message,282);actionFrame=Time.frameCount;awaitingRelease=true;
        }
        void AskQuantity()
        {
            pending=false;string problem;int maximum=CompanionTrade.Maximum(brain,pendingGive,pendingUid,out problem);
            if(maximum<=0){status.Text=Fit(problem,282);return;}
            quantityWindow=new CompanionQuantityWindow(brain,this);
            quantityWindow.SetTextBoxLabel("How many? (max "+maximum+")");quantityWindow.TextBox.Numeric=true;
            quantityWindow.TextBox.MaxCharacters=10;quantityWindow.TextBox.Text=maximum.ToString();
            quantityWindow.OnGotUserInput+=GotQuantity;quantityWindow.Show();
        }
        void GotQuantity(DaggerfallInputMessageBox sender,string text)
        {
            if(sender!=quantityWindow)return;
            sender.OnGotUserInput-=GotQuantity;quantityWindow=null;
            int amount;
            if(int.TryParse(text,out amount) && DaggerfallUI.UIManager.TopWindow==this)Commit(pendingGive,pendingUid,amount);
            else status.Text="Enter a positive whole quantity.";
        }
        static void Sync(ListBox list,VerticalScrollBar scroll)
        {scroll.TotalUnits=list.Count;if(scroll.DraggingThumb)list.ScrollIndex=scroll.ScrollIndex;else scroll.ScrollIndex=list.ScrollIndex;}
        public override void Update()
        {
            if(!CompanionTrade.Available(brain))
            {if(DaggerfallUI.UIManager.TopWindow==this)CloseWindow();return;}
            if(quantityWindow!=null && DaggerfallUI.UIManager.TopWindow==this)
            {quantityWindow.OnGotUserInput-=GotQuantity;quantityWindow=null;Refresh();}
            if(pending && Time.frameCount>pendingFrame && !CompanionOrdersWindow.SelectionInputActive()) {AskQuantity();return;}
            if(awaitingRelease && Time.frameCount>actionFrame && !CompanionOrdersWindow.SelectionInputActive())awaitingRelease=false;
            base.Update();Sync(giveList,giveScroll);Sync(takeList,takeScroll);
            if(Time.unscaledTime>=nextRefresh){Loads();nextRefresh=Time.unscaledTime+.25f;}
        }
        public override void OnPop()
        {
            if(quantityWindow!=null)quantityWindow.OnGotUserInput-=GotQuantity;
            giveList.OnUseSelectedItem-=Give;takeList.OnUseSelectedItem-=Take;base.OnPop();
        }
    }
    public class CompanionQuantityWindow : DaggerfallInputMessageBox
    {
        readonly NpcBrain brain;
        public CompanionQuantityWindow(NpcBrain person,CompanionTradeWindow previous):base(DaggerfallUI.UIManager,previous){brain=person;}
        public override void Update() {if(!CompanionTrade.Available(brain)){CloseWindow();return;}base.Update();}
    }
}

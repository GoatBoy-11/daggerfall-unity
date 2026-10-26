using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using AdvancedNPCs.Core;
namespace AdvancedNPCs
{
    /// <summary>A native keyboard/mouse picker. Every selection rechecks this exact actor and contract.</summary>
    public class CompanionOrdersWindow : DaggerfallListPickerWindow
    {
        readonly NpcBrain brain;
        readonly List<string> commands=new List<string>();
        string pendingWindow;
        int selectionFrame;
        bool barkOptionVisible;
        public static bool SelectionInputActive()
        {
            // Native TalkWindow.OnPush updates its topic controls synchronously. Do not give them
            // the selecting mouse/controller press or Return repeat from this picker.
            for(int button=0;button<3;button++)
                if(InputManager.Instance.GetMouseButton(button) || InputManager.Instance.GetMouseButtonDown(button) || InputManager.Instance.GetMouseButtonUp(button)) return true;
            return InputManager.Instance.GetBackButton() || InputManager.Instance.GetBackButtonUp() || DaggerfallUI.Instance.LastKeyCode!=KeyCode.None || Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter) || Input.GetKey(KeyCode.Space);
        }
        public static bool Available(NpcBrain person)
        {
            var mod=AdvancedNpcsMod.Instance;
            if(mod==null || DaggerfallWorkshop.Game.Serialization.SaveLoadManager.Instance.LoadInProgress || !mod.Companions.CanCommand(person,"orders")) return false;
            string area; Transform parent;
            return CompanionRoster.CurrentArea(out area,out parent) && person.State.companion.area==area &&
                Vector3.Distance(person.transform.position,GameManager.Instance.PlayerObject.transform.position)<=PlayerActivate.MobileNPCActivationDistance+0.75f;
        }
        public static bool Open(NpcBrain person)
        {
            if(!Available(person)) return false;
            if(DaggerfallUI.UIManager.TopWindow is CompanionOrdersWindow) return true;
            NpcChat chat=person.GetComponent<NpcChat>(); if(chat!=null && chat.Talking) chat.End(true);
            DaggerfallUI.UIManager.PushWindow(new CompanionOrdersWindow(person)); return true;
        }
        public CompanionOrdersWindow(NpcBrain person) : base(DaggerfallUI.UIManager,DaggerfallUI.UIManager.TopWindow)
        { brain=person; OnItemPicked+=Picked; }
        protected override void Setup()
        {
            base.Setup();
            listBox.Position=new Vector2(26,37);listBox.Size=new Vector2(138,64);listBox.RowsDisplayed=8;
            Label(10,brain.DisplayName);
            string stance=CompanionOrders.Stance(brain.State.companion.stance);
            Label(22,char.ToUpperInvariant(stance[0])+stance.Substring(1)+" / "+CompanionOrders.Condition((float)brain.EntityBehaviour.Entity.CurrentHealth/brain.EntityBehaviour.Entity.MaxHealth));
            Add("defensive","Defensive"+(stance=="defensive"?" (current)":""));
            Add("aggressive","Aggressive"+(stance=="aggressive"?" (current)":""));
            Add("passive","Passive"+(stance=="passive"?" (current)":""));
            Add("regroup","Disengage and regroup");
            Add(brain.State.companion.status=="waiting"?"follow":"wait",brain.State.companion.status=="waiting"?"Follow me (waiting)":"Wait here (following)");
            Add("heal","Give Healing Potion");
            if(brain.CurrentMode==NpcMode.Calm) { Add("push","Push");Add("talk","Talk");Add("trade","Give / Take belongings");Add("dismiss","Dismiss"); }
            barkOptionVisible=!AdvancedNpcsMod.Instance.Config.DisableFollowerBarks;
            if(barkOptionVisible) Add("barks",brain.State.companion.barksMuted?"Turn barks on":"Turn barks off");
            Add("close","Close");
        }
        void Label(float y,string text)
        {
            while(text.Length>1 && DaggerfallUI.DefaultFont.CalculateTextWidth(text,Vector2.one)>138) text=text.Substring(0,text.Length-1);
            var label=new TextLabel {Position=new Vector2(26,y),Text=text};
            pickerPanel.Components.Add(label);
        }
        void Add(string command,string caption) { commands.Add(command);listBox.AddItem(caption); }
        public override void Update()
        {
            if(!Available(brain) || (IsSetup && barkOptionVisible==AdvancedNpcsMod.Instance.Config.DisableFollowerBarks)) { CloseWindow();return; }
            if(pendingWindow!=null && Time.frameCount>selectionFrame && !SelectionInputActive())
            {
                string destination=pendingWindow;pendingWindow=null;CloseWindow();
                if(destination=="talk") brain.GetComponent<NpcTalk>().TryTalk(); else CompanionTradeWindow.Open(brain);return;
            }
            base.Update();
        }
        void Picked(int index,string caption)
        {
            if(DaggerfallUI.UIManager.TopWindow!=this || pendingWindow!=null) return;
            if(!Available(brain) || index<0 || index>=commands.Count) { CloseWindow();return; }
            string command=commands[index];
            if(command=="talk" || command=="trade") { pendingWindow=command;selectionFrame=Time.frameCount;return; }
            CloseWindow();
            if(command=="close") return;
            if(command=="heal")
            {
                string message;NpcCompanionInventory.GiveHealingPotion(brain,out message);
                DaggerfallUI.AddHUDText(message);return;
            }
            if(command=="barks")
            {
                // Recheck the setting as well as this exact live contract when the menu selection executes.
                if(AdvancedNpcsMod.Instance.Config.DisableFollowerBarks || !Available(brain))return;
                brain.State.companion.barksMuted=!brain.State.companion.barksMuted;
                var voice=brain.GetComponent<NpcVoice>();if(voice!=null)voice.BarksChanged();
                brain.CaptureState();DaggerfallUI.AddHUDText(brain.DisplayName+": barks "+(brain.State.companion.barksMuted?"off.":"on."));return;
            }
            if(AdvancedNpcsMod.Instance.Companions.Command(brain,command))
                DaggerfallUI.AddHUDText(brain.DisplayName+": "+(command=="push"?"Moving aside.":command=="regroup"?"Regrouping.":command=="dismiss"?"Contract ended.":command=="wait"?"Waiting here.":command=="follow"?"Following you.":"Stance: "+command+"."));
        }
        public override void OnPop() { OnItemPicked-=Picked; base.OnPop(); }
    }
}

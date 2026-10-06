using System.Reflection;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>
    /// Lets the player talk to an ANPC through DFU's citizen talk window (spec §9.2). TalkManager only talks to
    /// MobilePersonNPC/StaticNPC targets, so a hidden child carries a MobilePersonNPC "talk proxy" with the
    /// ANPC's name, race, gender and vanilla face; a PNG portrait replaces the face after the window opens.
    /// No UIWindowFactory override, so talk-window replacement mods keep working.
    /// </summary>
    public class NpcTalk : MonoBehaviour, IPlayerActivable
    {
        static readonly FieldInfo TexturePortraitField =
            typeof(DaggerfallTalkWindow).GetField("texturePortrait", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo PanelPortraitField =
            typeof(DaggerfallTalkWindow).GetField("panelPortrait", BindingFlags.Instance | BindingFlags.NonPublic);
        static bool warnedNoPortraitFields;

        MobilePersonNPC proxy;
        NpcBrain brain;
        Texture2D portrait;
        string displayName;

        /// <summary>The last mid-screen text this ANPC showed (read by the self-test).</summary>
        public string LastMessage { get; private set; }

        public MobilePersonNPC Proxy
        {
            get { return proxy; }
        }

        public Texture2D Portrait
        {
            get { return portrait; }
            set { portrait = value; }
        }

        public void Init(NpcInstance instance, Texture2D portraitTexture)
        {
            GameObject proxyObject = new GameObject("TalkProxy");
            proxyObject.transform.SetParent(transform, false);
            proxy = proxyObject.AddComponent<MobilePersonNPC>();
            proxy.NameNPC = instance.Name;
            proxy.Race = ToRace(instance.Race);
            proxy.Gender = instance.Gender == "Female" ? Genders.Female : Genders.Male;
            proxy.PersonFaceRecordId = instance.FaceRecord();
            displayName = instance.Name;
            portrait = portraitTexture;
            brain = GetComponent<NpcBrain>();
        }

        /// <summary>Called by DFU's PlayerActivate after its own enemy handling when the player clicks this ANPC.</summary>
        public void Activate(RaycastHit hit)
        {
            HandleActivate(GameManager.Instance.PlayerActivate.CurrentMode, hit.distance);
        }

        public void HandleActivate(PlayerActivateModes mode, float distance)
        {
            if (mode == PlayerActivateModes.Steal)
                return; // vanilla enemy pickpocketing handles this
            if (mode == PlayerActivateModes.Info)
            {
                Say("You see " + displayName + "."); // replaces vanilla's "You see a <class>."
                return;
            }
            if (distance > PlayerActivate.MobileNPCActivationDistance)
                return;
            TryTalk();
        }

        /// <summary>Opens the talk window if the ANPC is calm. Returns false if it refused.</summary>
        public bool TryTalk()
        {
            if (brain != null && brain.CurrentMode != NpcMode.Calm)
            {
                Say(displayName + " will not talk to you now.");
                return false;
            }
            TalkManager.Instance.TalkToMobileNPC(proxy);
            if (portrait != null)
                ApplyPortrait(DaggerfallUI.Instance.TalkWindow, portrait);
            return true;
        }

        /// <summary>The portrait texture the talk window currently shows (for the self-test).</summary>
        public static Texture2D CurrentTalkPortrait()
        {
            if (TexturePortraitField == null || DaggerfallUI.Instance.TalkWindow == null)
                return null;
            return TexturePortraitField.GetValue(DaggerfallUI.Instance.TalkWindow) as Texture2D;
        }

        void Say(string text)
        {
            LastMessage = text;
            DaggerfallUI.SetMidScreenText(text);
        }

        static void ApplyPortrait(DaggerfallTalkWindow window, Texture2D texture)
        {
            if (window == null || TexturePortraitField == null)
            {
                if (!warnedNoPortraitFields)
                {
                    warnedNoPortraitFields = true;
                    AdvancedNpcsMod.Log("The talk window has no portrait fields (replaced by another mod?); ANPCs show vanilla faces.");
                }
                return;
            }
            texture.filterMode = DaggerfallUI.Instance.GlobalFilterMode;
            TexturePortraitField.SetValue(window, texture);
            // On the first conversation the window has not run Setup yet: its panel does not exist and Setup
            // builds it from texturePortrait. Later conversations reuse the panel, so replace its texture too.
            Panel panel = PanelPortraitField == null ? null : PanelPortraitField.GetValue(window) as Panel;
            if (panel != null)
                panel.BackgroundTexture = texture;
        }

        static Races ToRace(string race)
        {
            if (race == "Redguard")
                return Races.Redguard;
            if (race == "Nord")
                return Races.Nord;
            return Races.Breton;
        }
    }
}

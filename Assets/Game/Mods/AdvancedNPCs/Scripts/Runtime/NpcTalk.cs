using System.Reflection;
using System.Collections.Generic;
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
        static readonly FieldInfo PopupRowsField =
            typeof(PopupText).GetField("textRows", BindingFlags.Instance | BindingFlags.NonPublic);

        MobilePersonNPC proxy;
        NpcBrain brain;
        Texture2D portrait;
        string displayName;
        string portraitFile;

        /// <summary>The last mid-screen text this ANPC showed (read by the self-test).</summary>
        public string LastMessage { get; private set; }

        public MobilePersonNPC Proxy
        {
            get { return proxy; }
        }

        /// <summary>Portrait file name (in _Portraits) this person shows, or null for a vanilla face.</summary>
        public string PortraitFile
        {
            get { return portraitFile; }
        }

        public Texture2D Portrait
        {
            get { return portrait; }
            set { portrait = value; }
        }

        public void Init(NpcInstance instance, Texture2D portraitTexture, string portraitFileName)
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
            portraitFile = portraitFileName;
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
            RemoveVanillaYouSee();
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
            // The face shown at the first talk is kept for the rest of the game (spec v2.1 §6.2).
            if (portraitFile != null && brain != null && brain.Instance != null && brain.Instance.Persistent &&
                string.IsNullOrEmpty(brain.State.portrait))
                brain.State.portrait = portraitFile;
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

        /// <summary>
        /// PlayerActivate already showed vanilla's "You see a &lt;class&gt;." as a HUD popup for this click (Info, Grab
        /// and Talk modes). It names the base class, not the ANPC, so take it back out of the popup rows.
        /// </summary>
        static void RemoveVanillaYouSee()
        {
            LinkedList<TextLabel> rows = PopupRows();
            if (rows == null || rows.Count == 0 || rows.Last.Value == null)
                return;
            string text = rows.Last.Value.Text;
            if (StartsLike(text, TextManager.Instance.GetLocalizedText("youSeeA")) ||
                StartsLike(text, TextManager.Instance.GetLocalizedText("youSeeAn")))
                rows.RemoveLast();
        }

        /// <summary>The HUD popup lines currently shown (for the self-test).</summary>
        public static List<string> PopupLines()
        {
            List<string> lines = new List<string>();
            LinkedList<TextLabel> rows = PopupRows();
            if (rows != null)
            {
                foreach (TextLabel row in rows)
                {
                    if (row != null)
                        lines.Add(row.Text);
                }
            }
            return lines;
        }

        static LinkedList<TextLabel> PopupRows()
        {
            DaggerfallHUD hud = DaggerfallUI.Instance.DaggerfallHUD;
            if (hud == null || hud.PopupText == null || PopupRowsField == null)
                return null;
            return PopupRowsField.GetValue(hud.PopupText) as LinkedList<TextLabel>;
        }

        /// <summary>True if text starts with the template's part before "%s" (e.g. "You see a ").</summary>
        static bool StartsLike(string text, string template)
        {
            if (text == null || string.IsNullOrEmpty(template))
                return false;
            int at = template.IndexOf("%s");
            string prefix = at >= 0 ? template.Substring(0, at) : template;
            return prefix.Length > 0 && text.StartsWith(prefix);
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

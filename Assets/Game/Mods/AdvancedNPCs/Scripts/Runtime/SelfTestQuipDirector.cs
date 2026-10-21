using UnityEngine;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;

namespace AdvancedNPCs
{
    /// <summary>
    /// Self-test stand-in for Wayside Denizens' StandardNpcQuipDirector: while its private enableQuips is on (the field
    /// TalkQuips switches), it closes every talk window the moment it opens.
    /// </summary>
    public class SelfTestQuipDirector : MonoBehaviour
    {
        bool enableQuips = true;

        public bool QuipsOn { get { return enableQuips; } }

        void OnEnable() { DaggerfallUI.UIManager.OnWindowChange += Changed; }

        void OnDisable() { DaggerfallUI.UIManager.OnWindowChange -= Changed; }

        void Changed(object sender, System.EventArgs e)
        {
            if (enableQuips && DaggerfallUI.UIManager.TopWindow is DaggerfallTalkWindow)
                DaggerfallUI.UIManager.PopWindow();
        }
    }
}

using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.UserInterfaceWindows;

namespace AdvancedNPCs
{
    /// <summary>One player surrender encounter shared by every ANPC guard and native guards.</summary>
    public static class GuardArrest
    {
        static PlayerEntity player;
        static PlayerEntity.Crimes crime;
        static int region;
        static bool demanded, refused;
        static DaggerfallMessageBox prompt;
        static float nextCheck;
        public static void Reset()
        {
            DaggerfallMessageBox previous = prompt;
            prompt = null; player = null; demanded = refused = false;
            if (previous != null) previous.CloseWindow();
        }
        public static void Refresh()
        {
            if (player == null) return;
            PlayerEntity current = GameManager.Instance.PlayerEntity;
            if (player != null && (player != current || current.Arrested || current.CrimeCommitted != crime ||
                GameManager.Instance.PlayerGPS.CurrentRegionIndex != region)) { Reset(); return; }
            if (prompt != null || Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + 0.5f;
            foreach (NpcBrain actor in NpcBrain.All())
                if (actor.isActiveAndEnabled && actor.Guard != null && actor.Guard.TargetsPlayer) return;
            if (GameManager.Instance.HowManyEnemiesOfType(MobileTypes.Knight_CityWatch, true) == 0)
            { current.HaveShownSurrenderToGuardsDialogue = false; Reset(); }
        }
        public static bool Demand(NpcBrain guard)
        {
            if (guard == null || guard.Guard == null || !guard.Guard.CanDemandPlayer) return false;
            Refresh();
            PlayerEntity current = GameManager.Instance.PlayerEntity;
            if (current.Arrested || current.CrimeCommitted == PlayerEntity.Crimes.None || GameManager.IsGamePaused) return false;
            if (player == null) { player = current; crime = current.CrimeCommitted; region = GameManager.Instance.PlayerGPS.CurrentRegionIndex; }
            if (demanded) { current.HaveShownSurrenderToGuardsDialogue = true; return refused && prompt == null; }
            // A native guard already charged the reputation cost and offered surrender.
            if (current.HaveShownSurrenderToGuardsDialogue) { demanded = refused = true; return true; }
            demanded = true;
            current.HaveShownSurrenderToGuardsDialogue = true;
            current.LowerRepForCrime();
            prompt = new DaggerfallMessageBox(DaggerfallUI.UIManager);
            prompt.SetTextTokens(DaggerfallUnity.Instance.TextProvider.GetRSCTokens(15));
            prompt.ParentPanel.BackgroundColor = Color.clear;
            prompt.AddButton(DaggerfallMessageBox.MessageBoxButtons.Yes);
            prompt.AddButton(DaggerfallMessageBox.MessageBoxButtons.No);
            prompt.OnButtonClick += Answer;
            prompt.Show();
            return false;
        }
        static void Answer(DaggerfallMessageBox sender, DaggerfallMessageBox.MessageBoxButtons button)
        {
            bool active = sender == prompt;
            if (active) prompt = null;
            sender.CloseWindow();
            if (!active || player == null || player.Arrested || player.CrimeCommitted != crime ||
                GameManager.Instance.PlayerGPS.CurrentRegionIndex != region) return;
            if (button == DaggerfallMessageBox.MessageBoxButtons.Yes) player.SurrenderToCityGuards(true);
            else refused = true;
        }
    }
}

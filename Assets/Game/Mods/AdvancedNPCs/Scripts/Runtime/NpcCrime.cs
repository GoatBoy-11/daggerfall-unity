using System;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>
    /// How Advanced NPCs report crimes against them. The self-test swaps Report for a recorder
    /// so it can check crimes without guards spawning.
    /// </summary>
    public static class NpcCrime
    {
        public static Action<PlayerEntity.Crimes> Report = PunishPlayer;

        public static bool TryCrime(string text, out PlayerEntity.Crimes crime)
        {
            return Enum.TryParse<PlayerEntity.Crimes>(text, true, out crime) &&
                (crime == PlayerEntity.Crimes.None || GuardRules.ValidCrime((int)crime));
        }

        /// <summary>Explicit guilt for one live ANPC; hostility and team alone are not offences.</summary>
        public static bool ReportNpc(string key, PlayerEntity.Crimes crime)
        {
            NpcBrain offender = NpcBrain.Find(key);
            if (offender == null || offender.CurrentMode == NpcMode.Dead || offender.Definition.Guard ||
                !offender.Instance.Persistent || offender.State.companion != null || (crime != PlayerEntity.Crimes.None && !GuardRules.ValidCrime((int)crime))) return false;
            AdvancedNpcsMod.Instance.States.Keep(offender.Id, offender.State);
            offender.State.wantedCrime = (int)crime;
            offender.State.wantedRegion = crime != PlayerEntity.Crimes.None ? GameManager.Instance.PlayerGPS.CurrentRegionIndex : 0;
            return true;
        }

        public static void PunishPlayer(PlayerEntity.Crimes crime)
        {
            PlayerEntity player = GameManager.Instance.PlayerEntity;
            player.CrimeCommitted = crime;
            player.SpawnCityGuards(true);
        }
    }
}

using System;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;

namespace AdvancedNPCs
{
    /// <summary>
    /// How Advanced NPCs report crimes against them. The self-test swaps Report for a recorder
    /// so it can check crimes without guards spawning.
    /// </summary>
    public static class NpcCrime
    {
        public static Action<PlayerEntity.Crimes> Report = PunishPlayer;

        public static void PunishPlayer(PlayerEntity.Crimes crime)
        {
            PlayerEntity player = GameManager.Instance.PlayerEntity;
            player.CrimeCommitted = crime;
            player.SpawnCityGuards(true);
        }
    }
}

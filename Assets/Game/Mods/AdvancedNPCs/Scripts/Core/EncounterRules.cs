using System;

namespace AdvancedNPCs.Core
{
    /// <summary>Wilderness encounters (outside-towns spec §2.4, §5): when to roll, where to appear, when to go.</summary>
    public static class EncounterRules
    {
        public const int RollMinutes = 10;
        public const float MinDistance = 40f;
        public const float MaxDistance = 80f;
        public const float DespawnDistance = 150f;
        /// <summary>Points at least this many degrees from the view direction count as out of view.</summary>
        public const float HiddenAngle = 70f;

        /// <summary>Rolls due since the last one: 1 after 10 in-game minutes, never more (time jumps never burst-spawn).</summary>
        public static int DueRolls(ulong lastRollMinute, ulong nowMinute)
        {
            return nowMinute >= lastRollMinute + RollMinutes ? 1 : 0;
        }

        /// <summary>The last roll time to keep: now, if the clock went back (an earlier save was loaded), else last.</summary>
        public static ulong RollClock(ulong lastRollMinute, ulong nowMinute)
        {
            return nowMinute < lastRollMinute ? nowMinute : lastRollMinute;
        }

        /// <summary>Only outside every location, with nothing but the HUD open (no rest, travel or other window).</summary>
        public static bool MayRoll(bool inLocationRect, bool onHud)
        {
            return !inLocationRect && onHud;
        }

        /// <summary>
        /// A point 40-80 m from the player, at least 70° away from the view direction (viewX, viewZ need not be
        /// normalised). u1 and u2 are random numbers in [0, 1).
        /// </summary>
        public static float[] RingPoint(float px, float pz, float viewX, float viewZ, double u1, double u2)
        {
            double heading = Math.Atan2(viewZ, viewX);
            double spread = (360.0 - 2 * HiddenAngle) * Math.PI / 180.0;
            double angle = heading + (HiddenAngle * Math.PI / 180.0 + 0.01) + u1 * (spread - 0.02);
            double r = MinDistance + u2 * (MaxDistance - MinDistance);
            return new float[] { (float)(px + Math.Cos(angle) * r), (float)(pz + Math.Sin(angle) * r) };
        }

        /// <summary>Ahead spawns while the player moves (playtest 2026-10-21): this far along the way.</summary>
        public const float AheadMin = 85f, AheadMax = 130f;
        /// <summary>Half the cone around the direction of travel ahead spawns fall in, in degrees.</summary>
        public const float AheadSpread = 60f;
        /// <summary>An ahead spot in plain sight is used only this far away (a pop-in there is a few pixels).</summary>
        public const float AheadVisibleMin = 105f;

        /// <summary>
        /// A point 85-130 m ahead of a moving player, within 60 degrees of the direction of travel (dirX, dirZ need not be
        /// normalised). Spots behind a walking player are useless: the player leaves them behind unseen.
        /// </summary>
        public static float[] AheadPoint(float px, float pz, float dirX, float dirZ, double u1, double u2)
        {
            double heading = Math.Atan2(dirZ, dirX);
            double angle = heading + (u1 * 2.0 - 1.0) * AheadSpread * Math.PI / 180.0;
            double r = AheadMin + u2 * (AheadMax - AheadMin);
            return new float[] { (float)(px + Math.Cos(angle) * r), (float)(pz + Math.Sin(angle) * r) };
        }

        public static bool OutOfView(float px, float pz, float viewX, float viewZ, float x, float z)
        {
            double dx = x - px, dz = z - pz;
            double len = Math.Sqrt(dx * dx + dz * dz) * Math.Sqrt(viewX * viewX + viewZ * viewZ);
            if (len < 1e-6)
                return false;
            double cos = (dx * viewX + dz * viewZ) / len;
            return cos <= Math.Cos(HiddenAngle * Math.PI / 180.0);
        }

        public static bool CanSpawn(int aliveOfTemplate, int max, int aliveTotal, int totalCap)
        {
            return aliveOfTemplate < max && aliveTotal < totalCap;
        }

        public static bool ShouldDespawn(float distance, bool inView)
        {
            return distance > DespawnDistance && !inView;
        }
    }
}

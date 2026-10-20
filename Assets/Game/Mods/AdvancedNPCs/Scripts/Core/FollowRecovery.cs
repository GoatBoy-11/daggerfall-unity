using System;
namespace AdvancedNPCs.Core
{
    /// <summary>Active follow time only. Interruptions reset this clock, pauses freeze it; no scene or saved state.</summary>
    public class FollowRecovery
    {
        /// <summary>CatchUpDistance setting (metres): default and slider range.</summary>
        public const int DefaultDistance = 15, MinDistance = 8, MaxDistance = 50;
        /// <summary>Seconds at or beyond the distance; seconds without 1 m headway beyond StallGap.</summary>
        public const double DistantDelay = 2, StallDelay = 5, StallGap = 6, Headway = 1;
        double mark = -1, distance = DefaultDistance;
        public double StalledSeconds { get; private set; }
        public double DistantSeconds { get; private set; }
        public void Reset() { mark = -1; StalledSeconds = DistantSeconds = 0; }
        /// <summary>
        /// Headway means the gap shrank by Headway since the mark; a growing gap raises the mark. Moving without
        /// closing in (sliding along a wall, circling a corner, trailing a faster leader) counts as stalled.
        /// </summary>
        public void Tick(double gap, double tooFar, double seconds)
        {
            if (seconds <= 0 || double.IsNaN(seconds) || double.IsInfinity(seconds)) return;
            distance = tooFar;
            if (gap <= 3) { Reset(); return; }
            DistantSeconds = gap >= distance ? DistantSeconds + seconds : 0;
            if (mark < 0 || gap <= mark - Headway) { mark = gap; StalledSeconds = 0; return; }
            mark = Math.Max(mark, gap);
            StalledSeconds += seconds;
        }
        public bool Due(double gap) { return (gap > StallGap && StalledSeconds >= StallDelay) || (gap >= distance && DistantSeconds >= DistantDelay); }
        /// <summary>The arrival spot must be hidden (destinationVisible); the follower itself may be in view.</summary>
        public static bool MayCatchUp(bool recruitedFollowing, bool calm, bool interrupted, bool destinationVisible, bool lockedDoor)
        { return recruitedFollowing && calm && !interrupted && !destinationVisible && !lockedDoor; }
        public static bool MayOpenDoor(bool canOpen, bool locked, bool moving, bool open, bool linkedAction, bool special)
        { return canOpen && !locked && !moving && !open && !linkedAction && !special; }
    }
}

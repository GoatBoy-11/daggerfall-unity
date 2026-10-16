using System;
namespace AdvancedNPCs.Core
{
    /// <summary>Active follow time only. Interruptions reset this clock; no scene or saved state.</summary>
    public class FollowRecovery
    {
        double window, travelled, leaderTravelled, startGap;
        public double StalledSeconds { get; private set; }
        public double DistantSeconds { get; private set; }
        public void Reset() { window = travelled = leaderTravelled = startGap = StalledSeconds = DistantSeconds = 0; }
        public void Tick(double gap, double moved, double leaderMoved, double seconds)
        {
            if (seconds <= 0 || double.IsNaN(seconds) || double.IsInfinity(seconds)) return;
            if (gap <= 3) { Reset(); return; }
            DistantSeconds = gap >= 30 ? DistantSeconds + seconds : 0;
            if (window == 0) startGap = gap;
            window += seconds; travelled += Math.Max(0, moved); leaderTravelled += Math.Max(0, leaderMoved);
            if (window < 1) return;
            bool noProgress = travelled < 0.2 || (leaderTravelled < 0.2 && gap > 5 && gap >= startGap - 0.25);
            StalledSeconds = noProgress ? StalledSeconds + window : 0;
            window = travelled = leaderTravelled = 0;
        }
        public bool Due(double gap) { return (gap > 5 && StalledSeconds >= 10) || (gap >= 30 && DistantSeconds >= 6); }
        public static bool MayCatchUp(bool recruitedFollowing, bool calm, bool interrupted, bool followerVisible, bool destinationVisible, bool protectedBarrier)
        { return recruitedFollowing && calm && !interrupted && !followerVisible && !destinationVisible && !protectedBarrier; }
        public static bool MayOpenDoor(bool canOpen, bool locked, bool moving, bool open, bool linkedAction, bool special)
        { return canOpen && !locked && !moving && !open && !linkedAction && !special; }
    }
}

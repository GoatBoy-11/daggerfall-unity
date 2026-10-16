using System;

namespace AdvancedNPCs.Core
{
    /// <summary>Distance hysteresis and bounded obstacle retries; no teleporting or shared progress.</summary>
    public class FollowMotion
    {
        readonly double distance, runDistance;
        bool moving;
        double stuck, retry;
        public bool Running { get; private set; }
        public bool Blocked { get; private set; }
        public FollowMotion(double distance, double runDistance) { this.distance = distance; this.runDistance = runDistance; }
        public bool Tick(double separation, double moved, double seconds)
        {
            bool wasRunning = Running;
            Running = false;
            if (seconds < 0 || double.IsNaN(seconds) || double.IsInfinity(seconds)) seconds = 0;
            if (separation <= distance)
            { moving = false; stuck = 0; retry = 0; Blocked = false; return false; }
            if (Blocked)
            {
                retry += seconds;
                if (retry < 2) return false;
                Blocked = false; retry = 0; stuck = 0;
            }
            if (!moving && separation <= distance + 0.5) return false;
            moving = true;
            if (moved >= 0.01) stuck = 0;
            else stuck += seconds;
            if (stuck >= 10) { Blocked = true; retry = 0; return false; }
            Running = separation >= runDistance || (wasRunning && separation > Math.Max(distance + 0.5, runDistance - 1));
            return true;
        }
    }
}

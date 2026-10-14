using System;

namespace AdvancedNPCs.Core
{
    /// <summary>Per-NPC transient progress. Targets are offsets from home. Tick only while routine AI owns movement.</summary>
    public class PackageMotion
    {
        readonly PackageSettings settings;
        readonly Func<double> random;
        PackagePoint target;
        int point;
        double waitUntil;
        double bestDistance = double.MaxValue;
        double stuckSeconds;
        public bool Failed { get; private set; }
        public int PointIndex { get { return point; } }
        public bool Running { get { return settings.Run; } }

        public PackageMotion(PackageSettings settings, Func<double> random)
        {
            this.settings = settings;
            this.random = random;
        }

        /// <summary>Returns a movement target, or null to stand. gameSeconds drives waits; activeSeconds drives stall detection.</summary>
        public PackagePoint Tick(PackagePoint current, double gameSeconds, double activeSeconds)
        {
            if (Failed || settings.Type == "stand" || settings.Type == "play_animation" || settings.Type == "follow") return null;
            if (settings.Type == "travel_to" || settings.Type == "guard_spot" || settings.Type == "work_at")
                target = settings.Target;
            else if (target == null)
            {
                if (gameSeconds < waitUntil) return null;
                if (settings.Type == "patrol") target = settings.Points[point];
                else
                {
                    double angle = random() * Math.PI * 2;
                    double distance = Math.Sqrt(random()) * settings.Radius;
                    target = new PackagePoint(settings.Center.X + Math.Cos(angle) * distance, settings.Center.Y,
                        settings.Center.Z + Math.Sin(angle) * distance);
                }
                bestDistance = double.MaxValue;
                stuckSeconds = 0;
            }

            double dx = target.X - current.X, dz = target.Z - current.Z;
            double horizontal = Math.Sqrt(dx * dx + dz * dz);
            double distanceToTarget = Math.Sqrt(dx * dx + dz * dz + Math.Pow(target.Y - current.Y, 2));
            if (horizontal <= 0.5 && Math.Abs(target.Y - current.Y) <= 2)
            {
                stuckSeconds = 0;
                bestDistance = double.MaxValue;
                if (settings.Type != "travel_to" && settings.Type != "guard_spot" && settings.Type != "work_at")
                {
                    target = null;
                    waitUntil = gameSeconds + settings.Pause;
                    if (settings.Type == "patrol") point = (point + 1) % settings.Points.Count;
                }
                return null;
            }
            if (distanceToTarget < bestDistance - 0.1)
            {
                bestDistance = distanceToTarget;
                stuckSeconds = 0;
            }
            else stuckSeconds += Math.Max(0, activeSeconds);
            if (stuckSeconds >= 10)
            {
                Failed = true;
                return null;
            }
            return target;
        }
    }
}

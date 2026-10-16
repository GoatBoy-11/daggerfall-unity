using System;

namespace AdvancedNPCs.Core
{
    /// <summary>Per-NPC transient progress. Targets are offsets from home. Tick only while routine AI owns movement.</summary>
    public class PackageMotion
    {
        readonly PackageSettings settings;
        readonly Func<double> random;
        readonly Func<PackagePoint, PackagePoint> chooseTarget;
        PackagePoint target;
        int point;
        double waitUntil;
        double bestDistance = double.MaxValue;
        double stuckSeconds;
        public bool Failed { get; private set; }
        public int PointIndex { get { return point; } }
        public bool Running { get { return settings.Run; } }

        public PackageMotion(PackageSettings settings, Func<double> random, Func<PackagePoint, PackagePoint> chooseTarget = null)
        {
            this.settings = settings;
            this.random = random;
            this.chooseTarget = chooseTarget;
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
                else if (chooseTarget != null) target = chooseTarget(current);
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

            if (target == null) return null;
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
                if (settings.Type == "wander_civilian") target = null;
                else Failed = true;
                return null;
            }
            return target;
        }

        /// <summary>Like vanilla citizens: keep heading, favour better surfaces, occasionally change direction.</summary>
        public static int CivilianDirection(int[] weights, int previous, double shuffle, double downgrade, double choice)
        {
            int best = 0;
            foreach (int weight in weights) best = Math.Max(best, weight);
            if (best == 0) return -1;
            bool forward = previous >= 0 && previous < weights.Length && weights[previous] > 0 && shuffle >= 0.025;
            if (forward && (weights[previous] >= best || downgrade < 0.2)) return previous;
            int minimum = forward ? best : 1;
            int count = 0;
            foreach (int weight in weights) if (weight >= minimum) count++;
            int pick = Math.Min(count - 1, Math.Max(0, (int)(choice * count)));
            for (int i = 0; i < weights.Length; i++) if (weights[i] >= minimum && pick-- == 0) return i;
            return -1;
        }
    }
}

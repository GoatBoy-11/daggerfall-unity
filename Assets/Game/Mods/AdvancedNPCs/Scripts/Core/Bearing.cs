using System;

namespace AdvancedNPCs.Core
{
    /// <summary>Human-readable direction and distance for console output. dx = east, dz = north (DFU world axes).</summary>
    public static class Bearing
    {
        static readonly string[] Points = new string[] { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

        public static string Direction(float dx, float dz)
        {
            if (Math.Abs(dx) < 0.5f && Math.Abs(dz) < 0.5f)
                return "here";
            double degrees = Math.Atan2(dx, dz) * 180.0 / Math.PI; // 0 = north, 90 = east
            if (degrees < 0)
                degrees += 360.0;
            int index = (int)Math.Round(degrees / 45.0) % 8;
            return Points[index];
        }

        public static string Describe(float dx, float dz)
        {
            double distance = Math.Sqrt(dx * dx + dz * dz);
            return Math.Round(distance).ToString(System.Globalization.CultureInfo.InvariantCulture) + " m " + Direction(dx, dz);
        }
    }
}

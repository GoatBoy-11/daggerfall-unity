namespace AdvancedNPCs.Core
{
    /// <summary>What an ANPC's legs do this frame. Top-level: DFU's runtime compiler cannot load nested enums.</summary>
    public enum Gait { Still, Walk, Run }

    /// <summary>
    /// Running while chasing or fleeing (spec run/greet/turret §2). DFU's motor chases at walk speed; a running ANPC
    /// moves runSpeed times as fast. No Unity or DFU types.
    /// </summary>
    public static class GaitRules
    {
        /// <summary>A chaser starts running when its target is farther than this (metres)...</summary>
        public const float RunStart = 6f;
        /// <summary>...and walks again when closer than this, so it does not flicker between the two.</summary>
        public const float RunStop = 4f;
        public const float DefaultRunSpeed = 1.2f;
        public const double MinRunSpeed = 1, MaxRunSpeed = 2;

        /// <summary>DFU's enemy move speed in m/s: (Speed + PlayerSpeedChanger.dfWalkBase) * MeshReader.GlobalScale.</summary>
        public static float WalkSpeed(int liveSpeed)
        {
            return (liveSpeed + 150f) / 40f;
        }

        public static float RunSpeed(float walkSpeed, float runSpeed, bool canRun)
        {
            return canRun ? walkSpeed * runSpeed : walkSpeed;
        }

        public static Gait Fighting(bool targetInSight, float distance, bool wasRunning, bool canRun)
        {
            if (!canRun || !targetInSight)
                return Gait.Walk;
            if (distance > RunStart)
                return Gait.Run;
            if (wasRunning && distance >= RunStop)
                return Gait.Run;
            return Gait.Walk;
        }

        public static Gait Fleeing(bool moving, bool canRun)
        {
            if (!moving)
                return Gait.Still;
            return canRun ? Gait.Run : Gait.Walk;
        }

        public static Gait Calm(bool moving)
        {
            return moving ? Gait.Walk : Gait.Still;
        }
    }
}

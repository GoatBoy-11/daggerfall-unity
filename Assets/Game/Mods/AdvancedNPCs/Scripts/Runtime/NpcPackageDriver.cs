using System;
using UnityEngine;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>Routine-only movement. The brain keeps combat, conversations, casting and turret restrictions.</summary>
    public class NpcPackageDriver
    {
        readonly NpcBrain brain;
        readonly NpcMover mover;
        readonly System.Random random;
        PackageLibrary library;
        AiScheduleEntry active;
        PackageMotion motion;
        long occurrence = long.MinValue;
        ulong lastGameSeconds;
        bool hasTime;
        bool failed;
        string detail = "current AI";
        public bool Running { get; private set; }
        public string Description { get { return detail; } }

        public NpcPackageDriver(NpcBrain brain, NpcMover mover)
        {
            this.brain = brain;
            this.mover = mover;
            random = new System.Random((int)brain.Instance.Seed);
        }

        /// <summary>Refresh even while interrupted, so diagnostics and slot changes follow the clock.</summary>
        public void Select(PackageLibrary currentLibrary, ulong seconds)
        {
            AiScheduleEntry wanted = AiSchedule.Select(brain.Instance.Definition.Schedule, (int)(seconds % 86400 / 60));
            long period = wanted == null ? long.MinValue : AiSchedule.Occurrence(wanted, seconds);
            bool jump = hasTime && (seconds < lastGameSeconds || seconds - lastGameSeconds > 60);
            lastGameSeconds = seconds;
            hasTime = true;
            if (library == currentLibrary && active == wanted && occurrence == period && !jump) return;
            library = currentLibrary;
            active = wanted;
            occurrence = period;
            failed = false;
            motion = null;
            Running = false;
            if (active == null) { detail = "current AI (no scheduled package)"; return; }
            string problem;
            PackageSettings settings = PackageParser.Resolve(library != null ? library.Find(active.Package) : null, active.Parameters, out problem);
            int slot = brain.Instance.Definition.Schedule.IndexOf(active) + 1;
            detail = "slot " + slot + ": " + active.Package;
            if (settings == null)
            {
                Fail(problem);
                return;
            }
            motion = new PackageMotion(settings, random.NextDouble);
            detail += " (" + settings.Type + ")";
        }

        public void Pause() { Running = false; }

        /// <summary>True owns routine movement, false releases to the original AI.</summary>
        public bool Tick(Vector3 homeLocal, ulong seconds, float dt)
        {
            Running = false;
            if (motion == null || failed) return false;
            try
            {
                Vector3 offset = brain.transform.localPosition - homeLocal;
                PackagePoint target = motion.Tick(new PackagePoint(offset.x, offset.y, offset.z), seconds, dt);
                if (motion.Failed)
                {
                    Fail("target unreachable (no progress for 10 active seconds)");
                    return false;
                }
                if (target == null) mover.Stop();
                else
                {
                    Vector3 local = homeLocal + new Vector3((float)target.X, (float)target.Y, (float)target.Z);
                    Vector3 world = brain.transform.parent != null ? brain.transform.parent.TransformPoint(local) : local;
                    Running = motion.Running && brain.Instance.Definition.CanRun;
                    float speed = NpcMover.WalkSpeed;
                    if (Running) speed *= brain.Instance.Definition.RunSpeed;
                    mover.MoveToward(world, speed);
                }
                return true;
            }
            catch (Exception e)
            {
                Fail(e.Message);
                return false;
            }
        }

        void Fail(string reason)
        {
            failed = true;
            Running = false;
            detail += "; current AI: " + reason;
            AdvancedNpcsMod.LogError(brain.Id + ": package " + detail);
        }
    }
}

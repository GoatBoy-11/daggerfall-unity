using System;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
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
        FollowMotion follow;
        Transform followLeader;
        Vector3 followPrevious;
        readonly NpcGait gait;
        readonly GameFacts scheduleFacts;
        readonly DaggerfallInterior areaInterior;
        readonly DaggerfallDungeon areaDungeon;
        readonly DaggerfallLocation areaTown;
        string activity = "";
        PackageSettings settings;
        PackagePlan plan;
        int stepIndex;
        double stepElapsed;
        bool completed, stepReady, guardDuty;
        SpotLibrary spotLibrary, heldLibrary;
        SpotChoice reservation;
        readonly System.Collections.Generic.HashSet<string> blockedSpots = new System.Collections.Generic.HashSet<string>();
        float spotRetry, blockedRetry;
        double? activityFacing;
        public bool AtWorkPost { get; private set; }
        LoadedSpriteSet spriteVersion;
        long occurrence = long.MinValue;
        ulong lastGameSeconds;
        bool hasTime;
        bool failed;
        string detail = "current AI";
        public bool Running { get; private set; }
        public bool OnlyCombatInterrupts { get { return settings != null && settings.Type == "play_animation" && !failed && settings.OnlyInterruptAtCombat; } }
        public bool PlaysAnimation { get { return settings != null && (settings.Type == "play_animation" || settings.Type == "work_at"); } }
        public bool Guarding { get { return settings != null && settings.Type == "guard_spot" && !failed && guardDuty; } }
        public bool AtGuardPost { get; private set; }
        public PackageSettings GuardSettings { get { return Guarding && AtGuardPost ? settings : null; } }
        public bool Following { get { return settings != null && settings.Type == "follow" && !failed; } }
        public bool AllowsStationary { get { return PlaysAnimation || Guarding || Following || (settings != null && settings.Spots.Count > 0); } }
        public string Animation { get { return PlaysAnimation && !failed && (settings.Type != "work_at" || AtWorkPost) ? settings.Animation : null; } }
        public string Description { get { return detail + (plan != null && plan.Sequence && !completed ? "; step " + (stepIndex+1) + "/" + plan.Steps.Count + ": " + plan.Steps[stepIndex].Package : "") + activity; } }

        public NpcPackageDriver(NpcBrain brain, NpcMover mover)
        {
            this.brain = brain;
            this.mover = mover;
            gait = brain.GetComponent<NpcGait>();
            scheduleFacts = new GameFacts(brain.State, AdvancedNpcsMod.Instance.Flags, brain.DisplayName, null, brain.Instance.Definition, brain, true);
            areaInterior = brain.GetComponentInParent<DaggerfallInterior>();
            areaDungeon = brain.GetComponentInParent<DaggerfallDungeon>();
            areaTown = brain.GetComponentInParent<DaggerfallLocation>();
            random = new System.Random((int)brain.Instance.Seed);
        }

        /// <summary>Refresh even while interrupted, so diagnostics and slot changes follow the clock.</summary>
        public void Select(PackageLibrary currentLibrary, ulong seconds)
        {
            AiScheduleEntry wanted = AiSchedule.Select(brain.Instance.Definition.Schedule, (int)(seconds % 86400 / 60), scheduleFacts);
            long period = wanted == null ? long.MinValue : AiSchedule.Occurrence(wanted, seconds);
            bool jump = hasTime && (seconds < lastGameSeconds || seconds - lastGameSeconds > 60);
            lastGameSeconds = seconds;
            hasTime = true;
            NpcSprite currentSprite = brain.GetComponent<NpcSprite>();
            LoadedSpriteSet currentSet = currentSprite != null ? currentSprite.ChosenSet : null;
            bool assetsChanged = currentSet != spriteVersion;
            spriteVersion = currentSet;
            SpotLibrary currentSpots = AdvancedNpcsMod.Instance.Spots;
            if (library == currentLibrary && spotLibrary == currentSpots && active == wanted && occurrence == period && !jump && !(PlaysAnimation && assetsChanged)) return;
            ReleaseSpot();
            spotLibrary = currentSpots;
            library = currentLibrary;
            active = wanted;
            occurrence = period;
            failed = false;
            motion = null;
            follow = null;
            followLeader = null;
            activity = "";
            settings = null;
            plan = null;
            completed = false;
            blockedSpots.Clear();
            NpcSprite sprite = brain.GetComponent<NpcSprite>();
            if (sprite != null) sprite.ResetPackageAnimation();
            Running = false;
            AtGuardPost = false;
            if (active == null) { detail = "current AI (no scheduled package)"; return; }
            string problem;
            plan = PackagePlan.Build(library != null ? library.Find(active.Package) : null, active.Parameters,
                delegate(string id) { return library != null ? library.Find(id) : null; }, out problem);
            int slot = brain.Instance.Definition.Schedule.IndexOf(active) + 1;
            detail = "slot " + slot + ": " + active.Package;
            if (plan == null) { Fail(problem); return; }
            detail += " (" + (plan.Sequence ? "sequence" : plan.Steps[0].Settings.Type) + ")";
            BeginStep(0);
        }

        void BeginStep(int index)
        {
            ReleaseSpot();
            stepIndex = index; stepElapsed = 0; stepReady = false;
            settings = plan.Steps[index].Settings;
            activity = ""; spotRetry = 0; blockedRetry = 15; blockedSpots.Clear();
            AtWorkPost = false; AtGuardPost = false;
            activityFacing = settings.Facing;
            guardDuty = settings.Type == "guard_spot" && settings.Spots.Count == 0;
            if (settings.Type == "guard_spot" || settings.Type == "work_at")
            {
                Vector3 current = brain.transform.localPosition;
                settings.Target = settings.Position ?? new PackagePoint(current.x,current.y,current.z);
            }
            motion = new PackageMotion(settings,random.NextDouble);
            follow = Following ? new FollowMotion(settings.FollowDistance,settings.RunDistance) : null;
            followLeader = null; followPrevious = brain.transform.localPosition;
            NpcSprite sprite = brain.GetComponent<NpcSprite>();
            if (sprite != null) sprite.ResetPackageAnimation();
        }

        void ReleaseSpot()
        {
            if (heldLibrary != null) heldLibrary.Occupancy.Release(brain.Id);
            if (reservation != null) activity = "; spot released; awaiting reservation";
            heldLibrary = null; reservation = null; spotRetry = 0;
        }
        public void Dispose() { ReleaseSpot(); settings = null; plan = null; }
        public void Suspend() { ReleaseSpot(); Running=false; AtWorkPost=false; AtGuardPost=false; }

        public void Pause()
        {
            Running = false; AtGuardPost = false; AtWorkPost = false;
            if (brain.CurrentMode != NpcMode.Calm) ReleaseSpot();
        }

        /// <summary>True owns routine movement, false releases to the original AI.</summary>
        public bool Tick(Vector3 homeLocal, ulong seconds, float dt)
        {
            if (completed) { mover.Stop(); return true; }
            bool controlled = TickLeaf(homeLocal,seconds,dt);
            if (controlled && plan != null && plan.Sequence && stepReady)
            {
                double duration = plan.Steps[stepIndex].Duration;
                stepElapsed += dt;
                if (duration < 0 || stepElapsed >= duration)
                {
                    if (stepIndex+1 < plan.Steps.Count) BeginStep(stepIndex+1);
                    else if (plan.Loop) BeginStep(0);
                    else
                    {
                        ReleaseSpot(); settings=null; completed=true;
                        Running=false; activity="; sequence completed, holding position";
                    }
                }
            }
            return controlled;
        }
        bool TickLeaf(Vector3 homeLocal, ulong seconds, float dt)
        {
            Running = false; stepReady = false;
            if (motion == null || failed) return false;
            try
            {
                if (Following) return TickFollow(dt);
                if (settings.Spots.Count > 0 && !ClaimSpot(homeLocal,dt)) return false;
                if (PlaysAnimation)
                {
                    NpcSprite sprite = brain.GetComponent<NpcSprite>();
                    if (sprite != null && sprite.Pending) { mover.Stop(); return true; }
                    if (sprite == null || !sprite.CanPlayPackageAnimation(settings.Animation))
                    {
                        Fail("no usable animation sheet matching " + settings.Animation);
                        return false;
                    }
                    if (settings.Type == "play_animation") { stepReady=true; mover.Stop(); return true; }
                }
                bool absolute = settings.Type == "guard_spot" || settings.Type == "work_at" || settings.Spots.Count > 0;
                Vector3 offset = absolute ? brain.transform.localPosition : brain.transform.localPosition - homeLocal;
                PackagePoint target = motion.Tick(new PackagePoint(offset.x, offset.y, offset.z), seconds, dt);
                if (motion.Failed)
                {
                    if (reservation != null)
                    {
                        blockedSpots.Add(reservation.Key); ReleaseSpot(); guardDuty=false;
                        activity="; unreachable named spot, trying alternatives; current AI";
                        spotRetry=0; return false;
                    }
                    Fail("target unreachable (no progress for 10 active seconds)");
                    return false;
                }
                AtGuardPost = Guarding && target == null;
                AtWorkPost = settings.Type == "work_at" && target == null;
                stepReady = target == null || (settings.Type != "work_at" && settings.Type != "guard_spot" && settings.Type != "travel_to");
                if (target == null)
                {
                    mover.Stop();
                    if (AtWorkPost && activityFacing.HasValue)
                    {
                        Quaternion localRotation=Quaternion.Euler(0,(float)activityFacing.Value,0);
                        brain.transform.rotation=brain.transform.parent!=null ? brain.transform.parent.rotation*localRotation : localRotation;
                    }
                }
                else
                {
                    if (brain.IsTurret)
                    {
                        Fail("stationary sprite set cannot walk to activity position");
                        return false;
                    }
                    Vector3 local = new Vector3((float)target.X, (float)target.Y, (float)target.Z);
                    if (!absolute) local += homeLocal;
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

        bool ClaimSpot(Vector3 homeLocal,float dt)
        {
            if (reservation != null) return true;
            guardDuty=false;
            blockedRetry-=dt;
            if(blockedRetry<=0) { blockedSpots.Clear(); blockedRetry=15; }
            spotRetry-=dt;
            if (spotRetry>0) return false;
            spotRetry=1;
            string region=scheduleFacts.Region,place=scheduleFacts.Town,context="";
            if(areaDungeon!=null) { region=areaDungeon.Summary.RegionName;place=areaDungeon.Summary.LocationName;context="dungeon"; }
            else if(areaInterior!=null)
            {
                var info=IndoorSpawner.InteriorPlace(areaInterior);
                if(info!=null) { region=info.Region;place=info.Place;context=info.Context; }
            }
            else if(areaTown!=null) { region=areaTown.Summary.RegionName;place=areaTown.Summary.LocationName; }
            if(spotLibrary!=null)
                foreach(var choice in spotLibrary.Candidates(settings.Spots,region,place,context,
                    brain.transform.parent!=null?brain.transform.parent.GetInstanceID():0,homeLocal,brain.transform.localPosition))
                {
                    if(blockedSpots.Contains(choice.Key) || !spotLibrary.Occupancy.TryClaim(choice.Key,brain.Id,choice.Spot.Capacity)) continue;
                    heldLibrary=spotLibrary;reservation=choice;
                    settings.Target=new PackagePoint(choice.Position.x,choice.Position.y,choice.Position.z);
                    activityFacing=settings.Facing ?? choice.Spot.Facing;
                    motion=new PackageMotion(settings,random.NextDouble);
                    guardDuty=settings.Type=="guard_spot";
                    activity="; using spot "+choice.Spot.Id;
                    return true;
                }
            activity="; no free matching spot; current AI (retrying)";
            return false;
        }

        bool PlayerInArea()
        {
            GameManager gm = GameManager.Instance;
            PlayerEnterExit pee = gm.PlayerEnterExit;
            if (areaInterior != null) return pee.IsPlayerInsideBuilding && pee.Interior == areaInterior;
            if (areaDungeon != null) return pee.IsPlayerInsideDungeon && pee.Dungeon == areaDungeon;
            if (pee.IsPlayerInside) return false;
            return areaTown == null || !gm.PlayerGPS.IsPlayerInLocationRect || gm.StreamingWorld.CurrentPlayerLocationObject == areaTown;
        }
        bool TickFollow(float dt)
        {
            if (brain.IsTurret) { Fail("stationary sprite set cannot follow"); return false; }
            Transform leader = null;
            if (settings.FollowTarget == "player")
            {
                if (PlayerInArea() && GameManager.Instance.PlayerObject != null) leader = GameManager.Instance.PlayerObject.transform;
            }
            else
            {
                NpcBrain other = NpcBrain.Find(settings.FollowTarget);
                if (other != null && other != brain && other.isActiveAndEnabled && other.CurrentMode != NpcMode.Dead && other.transform.parent == brain.transform.parent)
                    leader = other.transform;
            }
            if (leader != followLeader)
            {
                followLeader = leader;
                follow = new FollowMotion(settings.FollowDistance, settings.RunDistance);
                followPrevious = brain.transform.localPosition;
            }
            if (leader == null)
            {
                activity = "; waiting for target " + settings.FollowTarget + " in this area";
                mover.Stop();
                return true;
            }
            Vector3 local = brain.transform.localPosition;
            Vector3 moved = local - followPrevious;
            moved.y = 0;
            followPrevious = local;
            Vector3 gap = leader.position - brain.transform.position;
            float vertical = Mathf.Abs(gap.y);
            gap.y = 0;
            if (vertical > 4 || gap.magnitude > 1000)
            {
                activity = "; waiting for target on reachable ground";
                mover.Stop();
                return true;
            }
            stepReady = true;
            bool move = follow.Tick(gap.magnitude, moved.magnitude, dt);
            activity = follow.Blocked ? "; blocked, retrying" : "; following " + settings.FollowTarget;
            if (!move) mover.Stop();
            else
            {
                Running = follow.Running && brain.Instance.Definition.CanRun;
                float speed = gait != null ? (Running ? gait.RunSpeed : gait.WalkSpeed) :
                    (Running ? NpcMover.RunSpeed : NpcMover.WalkSpeed);
                mover.MoveToward(leader.position - gap.normalized * (float)settings.FollowDistance, speed);
            }
            return true;
        }

        void Fail(string reason)
        {
            ReleaseSpot();
            failed = true;
            AtGuardPost = false;
            Running = false;
            detail += "; current AI: " + reason;
            AdvancedNpcsMod.LogError(brain.Id + ": package " + detail);
        }
    }
}

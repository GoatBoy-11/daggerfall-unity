using System;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility;
using DaggerfallConnect.Utility;
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
        ShelterSearch shelter;
        CityNavigation cityNavigation;
        int civilianDirection = -1;
        bool civilianPaused;
        float civilianIdleLeft;
        bool StopsToAct { get { return settings != null && (settings.Type == "wander_civilian" || settings.Type == "chore"); } }
        public bool CivilianPaused { get { return StopsToAct && civilianPaused && !failed; } }
        FollowMotion follow;
        FollowNavigation navigation;
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
        public bool Guarding { get { return settings != null && settings.Type == "guard_spot" && !failed && guardDuty && (motion == null || !motion.Failed); } }
        public bool AtGuardPost { get; private set; }
        public PackageSettings GuardSettings { get { return Guarding && AtGuardPost ? settings : null; } }
        public bool Following { get { return settings != null && settings.Type == "follow" && !failed; } }
        public bool Eating { get { return settings != null && settings.Type == "eat" && !failed; } }
        bool Hawking { get { return settings != null && settings.Type == "hawk" && !failed; } }
        float hawkWait, hawkShowLeft;
        string hawkAnimation;
        NpcChat socialPartner;
        float socialWait, socialApproach;
        public PackageSettings MealSettings { get { return Eating ? settings : null; } }
        public bool AllowsStationary { get { return PlaysAnimation || Guarding || Following || Eating || Hawking || shelter != null || (settings != null && settings.Spots.Count > 0); } }
        public string Animation
        {
            get
            {
                if (Hawking) return hawkShowLeft > 0 ? hawkAnimation : null;   // only while calling out
                return PlaysAnimation && !failed && (settings.Type != "work_at" || AtWorkPost) ? settings.Animation : null;
            }
        }
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
        static readonly AiScheduleEntry companionFollow = new AiScheduleEntry { Package = "follow" };
        static readonly AiScheduleEntry companionStand = new AiScheduleEntry { Package = "stand" };

        public void Select(PackageLibrary currentLibrary, ulong seconds)
        {
            CompanionRecord companion = brain.State.companion;
            AiScheduleEntry wanted = companion != null ? (companion.status == "following" ? companionFollow : companionStand) :
                AiSchedule.Select(brain.Instance.Definition.Schedule, (int)(seconds % 86400 / 60), scheduleFacts);
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
            shelter = null;
            follow = null;
            navigation = null;
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
            AiPackage selected = companion != null ? new AiPackage { Id = active.Package, Type = active.Package, Parameters = new System.Collections.Generic.Dictionary<string, object>() } :
                library != null ? library.Find(active.Package) : null;
            plan = PackagePlan.Build(selected, active.Parameters,
                delegate(string id) { return library != null ? library.Find(id) : null; }, out problem);
            int slot = brain.Instance.Definition.Schedule.IndexOf(active) + 1;
            detail = companion != null ? "companion " + companion.status : "slot " + slot + ": " + active.Package;
            if (plan == null) { Fail(problem); return; }
            detail += " (" + (plan.Sequence ? "sequence" : plan.Steps[0].Settings.Type) + ")";
            BeginStep(0);
        }

        void BeginStep(int index)
        {
            ReleaseSpot();
            stepIndex = index; stepElapsed = 0; stepReady = false;
            settings = plan.Steps[index].Settings;
            shelter = settings.Type == "seek_shelter" ? new ShelterSearch(brain, mover, settings, scheduleFacts) : null;
            activity = ""; spotRetry = 0; blockedRetry = 15; blockedSpots.Clear();
            AtWorkPost = false; AtGuardPost = false;
            civilianPaused = false; civilianIdleLeft = 0; civilianDirection = -1;
            hawkWait = (float)random.NextDouble() * 2; hawkShowLeft = 0;
            socialPartner = null; socialWait = 2 + (float)random.NextDouble() * 4; socialApproach = 0;
            cityNavigation = settings.Type == "wander_civilian" && areaTown != null && areaInterior == null && areaDungeon == null ? areaTown.GetComponent<CityNavigation>() : null;
            activityFacing = settings.Facing;
            guardDuty = settings.Type == "guard_spot" && settings.Spots.Count == 0;
            if (settings.Type == "guard_spot" || settings.Type == "work_at")
            {
                Vector3 current = brain.transform.localPosition;
                settings.Target = settings.Position ?? new PackagePoint(current.x,current.y,current.z);
            }
            motion = new PackageMotion(settings,random.NextDouble, cityNavigation != null ? (Func<PackagePoint, PackagePoint>)CivilianTarget : null);
            follow = Following ? new FollowMotion(settings.FollowDistance,settings.RunDistance) : null;
            followLeader = null; followPrevious = brain.transform.localPosition;
            navigation = Following ? new FollowNavigation(brain, mover) : null;
            NpcSprite sprite = brain.GetComponent<NpcSprite>();
            if (sprite != null) sprite.ResetPackageAnimation();
        }

        void ReleaseSpot()
        {
            if (shelter != null) shelter.Release();
            if (heldLibrary != null) heldLibrary.Occupancy.Release(brain.Id);
            if (reservation != null) activity = "; spot released; awaiting reservation";
            heldLibrary = null; reservation = null; spotRetry = 0;
        }
        public void Dispose() { ReleaseSpot(); navigation = null; settings = null; plan = null; }
        public void InterruptFollow() { if (navigation != null) navigation.Suspend(); }
        public void Suspend() { ReleaseSpot(); InterruptFollow(); Running=false; AtWorkPost=false; AtGuardPost=false; }

        public void Pause()
        {
            Running = false; AtGuardPost = false; AtWorkPost = false;
            if (brain.CurrentMode != NpcMode.Calm) { ReleaseSpot(); InterruptFollow(); }
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
                if (shelter != null)
                {
                    bool controlled = shelter.Tick(spotLibrary, homeLocal, seconds, dt);
                    activity = "; " + shelter.Status; Running = shelter.Running; stepReady = shelter.AtShelter;
                    return controlled;
                }
                if (motion.Failed && motion.CanRetry && settings.Spots.Count == 0)
                {
                    if (!motion.Retry(dt)) return false;
                    activity = "; retrying blocked target (" + motion.RetryCount + "/2)";
                }
                NpcSprite civilianSprite = StopsToAct ? brain.GetComponent<NpcSprite>() : null;
                if (civilianSprite != null && civilianSprite.Pending) { mover.Stop(); return true; }
                if (civilianPaused)
                {
                    stepReady = true;
                    civilianIdleLeft = Mathf.Max(0, civilianIdleLeft - dt);
                    if (civilianIdleLeft > 0 || (civilianSprite != null && !civilianSprite.CivilianPauseFinished)) { mover.Stop(); return true; }
                    civilianPaused = false;
                    if (civilianSprite != null) civilianSprite.ResetPackageAnimation();
                }
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
                if (settings.Type == "socialize") return TickSocialize(dt);
                if (settings.Type == "hawk")
                {
                    stepReady = true; mover.Stop();
                    hawkShowLeft = Mathf.Max(0, hawkShowLeft - dt);
                    hawkWait -= dt;
                    if (hawkWait <= 0) TryHawk();
                    return true;
                }
                if (settings.Type == "eat")
                {
                    // Stays put; the sprite plays the meal loop. Without eat/drink/idle sheets (or vanilla sprites) it just stands.
                    NpcSprite sprite = brain.GetComponent<NpcSprite>();
                    activity = sprite != null && sprite.Pending ? "; sprites loading" : sprite != null && sprite.CanEat() ? "" : "; no eat, drink or idle sheets: standing";
                    stepReady = true; mover.Stop(); return true;
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
                    AtGuardPost = false; AtWorkPost = false;
                    if (motion.CanRetry)
                    {
                        activity = "; target blocked; current AI (retry " + (motion.RetryCount + 1) + "/2 after 15 active seconds)";
                        return false;
                    }
                    Fail("target unreachable (no progress for 10 active seconds; 2 retries exhausted)");
                    return false;
                }
                AtGuardPost = Guarding && target == null;
                AtWorkPost = settings.Type == "work_at" && target == null;
                stepReady = target == null || (settings.Type != "work_at" && settings.Type != "guard_spot" && settings.Type != "travel_to");
                if (target == null)
                {
                    if (settings.Type == "chore")
                    {
                        // Every stop is a chore; vanilla sprites (no sheets) stand a few seconds instead.
                        civilianPaused = true;
                        if (civilianSprite == null || !civilianSprite.BeginChorePause(settings)) civilianIdleLeft = 2 + (float)random.NextDouble() * 4;
                    }
                    else if (settings.Type == "wander_civilian" && (cityNavigation == null || settings.Radius == 0 || random.NextDouble() < 0.25))
                    {
                        civilianPaused = true;
                        // Vanilla sprites have no named activity sheets; keep their normal standing animation.
                        if (civilianSprite == null || !civilianSprite.BeginCivilianPause()) civilianIdleLeft = 2 + (float)random.NextDouble() * 4;
                    }
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

        float SocialInterval() { return (float)(settings.IntervalMin + (settings.IntervalMax - settings.IntervalMin) * random.NextDouble()); }

        /// <summary>
        /// Socialize: every interval, walk up to the nearest ANPC within radius that can talk now and start a conversation
        /// (NpcChat holds both still while it lasts). Gives up on a partner after 15 active seconds of approach.
        /// </summary>
        bool TickSocialize(float dt)
        {
            stepReady = true;
            NpcChat chat = brain.GetComponent<NpcChat>();
            if (chat == null) { mover.Stop(); activity = "; no conversation setup"; return true; }
            if (chat.Talking) { socialPartner = null; socialWait = SocialInterval(); activity = "; talking"; return true; }
            socialWait -= dt;
            if (socialPartner == null)
            {
                mover.Stop();
                if (socialWait > 0) return true;
                socialWait = 3f;
                string why = chat.WhyNot();
                if (why != null) { activity = "; cannot talk: " + why; return true; }
                float best = (float)settings.Radius;
                foreach (NpcChat other in UnityEngine.Object.FindObjectsOfType<NpcChat>())
                {
                    if (other.transform.parent != brain.transform.parent || !chat.CanTalkWith(other)) continue;
                    float d = Vector3.Distance(other.transform.position, brain.transform.position);
                    if (d <= best) { best = d; socialPartner = other; }
                }
                socialApproach = 0;
                if (socialPartner == null) { activity = "; nobody to talk to within " + settings.Radius + " m"; return true; }
            }
            if (socialPartner == null || !chat.CanTalkWith(socialPartner)) { socialPartner = null; socialWait = 3f; mover.Stop(); return true; }
            Vector3 to = socialPartner.transform.position - brain.transform.position; to.y = 0f;
            if (to.magnitude <= 1.6f)
            {
                mover.Stop();
                string why = chat.TryStart(socialPartner, false);
                activity = why == null ? "; started a conversation with " + socialPartner.Brain.DisplayName : "; could not start: " + why;
                socialPartner = null; socialWait = why == null ? 1f : SocialInterval();
                return true;
            }
            socialApproach += dt;
            if (socialApproach > 15f || brain.IsTurret)
            {
                activity = "; could not reach " + socialPartner.Brain.DisplayName;
                socialPartner = null; socialWait = SocialInterval(); mover.Stop();
                return true;
            }
            activity = "; walking over to " + socialPartner.Brain.DisplayName;
            mover.MoveToward(socialPartner.transform.position, NpcMover.WalkSpeed);
            return true;
        }

        /// <summary>Hawk: the player within range and in sight gets a call (voice line and hawk sheet), facing them.</summary>
        void TryHawk()
        {
            hawkWait = 1f;     // nobody to call to: look again in a second
            GameObject player = GameManager.Instance.PlayerObject;
            DaggerfallWorkshop.Game.Entity.DaggerfallEntityBehaviour target = GameManager.Instance.PlayerEntityBehaviour;
            if (player == null || target == null || Vector3.Distance(player.transform.position, brain.transform.position) > settings.Radius ||
                !brain.CanSee(target))
                return;
            Vector3 to = player.transform.position - brain.transform.position; to.y = 0f;
            if (to.sqrMagnitude > 0.001f) brain.transform.rotation = Quaternion.LookRotation(to);
            NpcVoice voice = brain.GetComponent<NpcVoice>();
            bool spoke = voice != null && voice.Play(SoundPools.Hawk);
            NpcSprite sprite = brain.GetComponent<NpcSprite>();
            // Its own hawk sheet, else greet, else talk; none: it only turns and calls.
            hawkAnimation = null; hawkShowLeft = 0f;
            if (sprite != null)
                foreach (string name in new[] { settings.Animation, PackageAnimations.HawkFallbacks[0], PackageAnimations.HawkFallbacks[1] })
                {
                    float seconds = sprite.PackageCycleSeconds(name);
                    if (seconds > 0) { hawkAnimation = name; hawkShowLeft = seconds; break; }
                }
            hawkWait = (float)(settings.IntervalMin + (settings.IntervalMax - settings.IntervalMin) * random.NextDouble());
            activity = spoke ? "; called out to the player" : "; called out (no hawk sounds)";
        }

        PackagePoint CivilianTarget(PackagePoint current)
        {
            Vector3 home = brain.transform.localPosition - new Vector3((float)current.X, (float)current.Y, (float)current.Z);
            Vector3 center = home + new Vector3((float)settings.Center.X, (float)settings.Center.Y, (float)settings.Center.Z);
            Vector3 currentGap = brain.transform.localPosition - center; currentGap.y = 0;
            DFPosition grid = cityNavigation.WorldToNavGridPosition(cityNavigation.SceneToWorldPosition(brain.transform.position));
            int[] dx = { 0, 0, 1, -1 }, dz = { -1, 1, 0, 0 }, weights = new int[4];
            Vector3[] targets = new Vector3[4];
            for (int i = 0; i < 4; i++)
            {
                int x = grid.X + dx[i], z = grid.Y + dz[i];
                int weight = cityNavigation.GetNavGridWeightLocal(x, z);
                if (weight == 0 || cityNavigation.HasFlags(x, z, CityNavigation.TileFlags.Occupied)) continue;
                Vector3 world = cityNavigation.WorldToScenePosition(cityNavigation.NavGridToWorldPosition(new DFPosition(x, z)));
                Vector3 local = brain.transform.parent != null ? brain.transform.parent.InverseTransformPoint(world) : world;
                Vector3 gap = local - center; gap.y = 0;
                if (gap.sqrMagnitude > settings.Radius * settings.Radius && gap.sqrMagnitude >= currentGap.sqrMagnitude) continue;
                Vector3 delta = world - brain.transform.position; delta.y = 0;
                if (Physics.Raycast(brain.transform.position, delta.normalized, delta.magnitude, SpotFinder.Mask, QueryTriggerInteraction.Ignore)) continue;
                local.y = brain.transform.localPosition.y;
                targets[i] = local - home;
                weights[i] = weight;
            }
            civilianDirection = PackageMotion.CivilianDirection(weights, civilianDirection, random.NextDouble(), random.NextDouble(), random.NextDouble());
            if (civilianDirection < 0) return null;
            Vector3 target = targets[civilianDirection];
            return new PackagePoint(target.x, target.y, target.z);
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
                navigation = new FollowNavigation(brain, mover);
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
            if ((vertical > 4 && brain.State.companion == null) || gap.magnitude > 1000)
            {
                activity = "; waiting for target on reachable ground";
                mover.Stop();
                return true;
            }
            stepReady = true;
            float separation = brain.State.companion != null ? Vector3.Distance(leader.position, brain.transform.position) : gap.magnitude;
            if (separation <= settings.FollowDistance + 0.5 && !navigation.ArrivedThroughClearSpace(leader))
                separation = (float)settings.FollowDistance + 0.6f;
            bool move = follow.Tick(separation, brain.State.companion != null ? 0.02f : moved.magnitude, dt);
            activity = follow.Blocked ? "; blocked, retrying" : "; following " + settings.FollowTarget;
            if (!move) mover.Stop();
            else
            {
                Running = follow.Running && brain.Instance.Definition.CanRun;
                float speed = gait != null ? (Running ? gait.RunSpeed : gait.WalkSpeed) :
                    (Running ? NpcMover.RunSpeed : NpcMover.WalkSpeed);
                Vector3 step, catchup;
                bool walking = navigation.Tick(leader, dt, settings.FollowTarget == "player", out step, out catchup);
                activity = "; following " + settings.FollowTarget + "; " + navigation.Status;
                if (navigation.CatchupReady)
                {
                    AdvancedNpcsMod.Log(brain.Id + ": companion caught up.");
                    brain.Teleport(catchup); // disposes this package driver; return without touching it again
                    return true;
                }
                if (walking) mover.MoveToward(step, speed); else mover.Stop();
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

using System.Collections.Generic;
using UnityEngine;
using DaggerfallConnect;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Utility;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>
    /// Animal behaviour on top of NpcBrain (spec animal-ai §2-3): notices the player by sight, hearing and stealth, then
    /// flees, shies away, warns or attacks by its temperament; gives up a chase past its territory; rests outside its
    /// active hours; calls now and then; keeps with its group's leader; predators hunt prey animals when hungry and feed
    /// on the kill. The brain still owns the modes (Calm, Fighting, Fleeing): this only starts and ends them.
    /// </summary>
    public class NpcAnimal : MonoBehaviour
    {
        const float NearCheck = 0.25f;
        const float FarCheck = 1f;
        const float FarDistance = 60f;
        const float HerdSeconds = 3f;
        const float HuntRetrySeconds = 30f;
        const double EscapedHuntHours = 1.0;
        const float FeedMin = 20f, FeedMax = 40f;
        const float FeedReach = 1.3f;
        const float WarnSoundSeconds = 1.5f;
        const float CallRange = SoundRules.MaxAudibleDistance;     // a call farther away would play silently
        const float TurnDegreesPerSecond = 360f;

        static readonly List<NpcAnimal> live = new List<NpcAnimal>();

        /// <summary>Every animal in the world now.</summary>
        public static List<NpcAnimal> All
        {
            get
            {
                live.RemoveAll(delegate (NpcAnimal a) { return a == null; });
                return new List<NpcAnimal>(live);
            }
        }

        AnimalSettings s;
        NpcBrain brain;
        NpcVoice voice;
        NpcSprite sprite;
        System.Random rng;

        /// <summary>The group's first member (set by the wilderness spawner); null for a leader or a loner.</summary>
        public NpcBrain Leader;

        int state = AnimalStates.Unaware;
        float stateTime;
        bool noticedBefore;
        bool bold;
        bool habituated;
        float unseen;
        float tickIn;
        float elapsed;
        float warnSoundIn;
        float callIn;
        float herdIn;
        float huntIn;
        double hungryAt;
        NpcAnimal prey;
        bool feeding;
        bool atFood;
        float feedLeft;
        Vector3 feedAt;
        bool resting;
        bool active = true;
        bool night;
        string animation;
        int lastMode = (int)NpcMode.Calm;    // an int: DFU cannot load an enum field declared in a later file
        float alertNow, reactNow;

        public AnimalSettings Settings { get { return s; } }
        public NpcBrain Brain { get { return brain; } }
        /// <summary>AnimalStates value of its dealing with the player.</summary>
        public int State { get { return state; } }
        public bool Resting { get { return resting; } }
        public bool Feeding { get { return feeding; } }
        /// <summary>The animal it hunts now, or null.</summary>
        public NpcAnimal Prey { get { return prey; } }
        /// <summary>Alert and react distances after the player's stealth, the last time it looked.</summary>
        public float AlertNow { get { return alertNow; } }
        public float ReactNow { get { return reactNow; } }

        /// <summary>The sheet family the sprite should play while calm and still, or null for the brain's own choice.</summary>
        public string Animation
        {
            get { return brain != null && brain.CurrentMode == NpcMode.Calm && Holding ? animation : null; }
        }

        bool Holding
        {
            get { return state == AnimalStates.Alert || state == AnimalStates.Warning || resting || (feeding && atFood); }
        }

        public void Init(AnimalSettings settings)
        {
            s = settings;
        }

        void Start()
        {
            brain = GetComponent<NpcBrain>();
            voice = GetComponent<NpcVoice>();
            sprite = GetComponent<NpcSprite>();
            rng = new System.Random(brain != null && brain.Instance != null ? (int)brain.Instance.Seed ^ System.Environment.TickCount : System.Environment.TickCount);
            callIn = AnimalRules.CallSeconds(s, rng.NextDouble());
            // Hunger starts anywhere in its cycle, so predators met together do not all hunt at once.
            hungryAt = Now() + AnimalRules.HungerSeconds(s, rng.NextDouble()) * rng.NextDouble();
            tickIn = (float)rng.NextDouble() * NearCheck;
        }

        void OnEnable() { if (!live.Contains(this)) live.Add(this); FloatingOrigin.OnPositionUpdate += OnOriginShift; }
        void OnDisable() { live.Remove(this); FloatingOrigin.OnPositionUpdate -= OnOriginShift; }

        /// <summary>The world moved under the floating origin: the kill it walks to moved with it.</summary>
        void OnOriginShift(Vector3 offset)
        {
            feedAt += offset;
        }

        /// <summary>Called by the brain each calm frame: true when the animal moved (or held) it itself.</summary>
        public bool ControlsCalm(NpcMover mover)
        {
            if (s == null || brain == null)
                return false;
            if (Holding)
            {
                mover.Stop();
                if (state == AnimalStates.Alert || state == AnimalStates.Warning)
                    TurnTo(GameManager.Instance.PlayerObject.transform.position);
                else if (feeding)
                    TurnTo(feedAt);
                return true;
            }
            if (feeding)
            {
                Vector3 flat = feedAt - transform.position;
                flat.y = 0f;
                if (flat.magnitude <= FeedReach)
                {
                    atFood = true;
                    mover.Stop();
                    return true;
                }
                mover.MoveToward(feedAt, NpcMover.WalkSpeed);
                return true;
            }
            return false;
        }

        void TurnTo(Vector3 point)
        {
            Vector3 to = point - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.01f)
                return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), TurnDegreesPerSecond * Time.deltaTime);
        }

        void Update()
        {
            if (s == null || brain == null || brain.EntityBehaviour == null || brain.CurrentMode == NpcMode.Dead || GameManager.IsGamePaused)
                return;
            float dt = Time.deltaTime;
            stateTime += dt;
            elapsed += dt;
            if (state == AnimalStates.Warning && brain.CurrentMode == NpcMode.Calm)
            {
                warnSoundIn -= dt;
                if (warnSoundIn <= 0f)
                {
                    warnSoundIn = WarnSoundSeconds;
                    Play(SoundPools.Warn);
                }
            }
            tickIn -= dt;
            if (tickIn > 0f)
                return;
            GameObject playerObject = GameManager.Instance.PlayerObject;
            float d = playerObject != null ? Vector3.Distance(playerObject.transform.position, transform.position) : float.MaxValue;
            tickIn = d > FarDistance ? FarCheck : NearCheck;
            float step = elapsed;
            elapsed = 0f;
            Tick(step, d);
        }

        void Tick(float step, float d)
        {
            DaggerfallEntityBehaviour player = GameManager.Instance.PlayerEntityBehaviour;
            if (player == null)
                return;
            NpcMode mode = brain.CurrentMode;
            if ((int)mode != lastMode)
            {
                if (lastMode == (int)NpcMode.Calm && mode == NpcMode.Fleeing)
                    CrossAlarm(brain.Threat);
                if (mode == NpcMode.Calm)
                    Enter(AnimalStates.Unaware);
                lastMode = (int)mode;
            }
            if (mode == NpcMode.Fleeing)
            {
                FleeTick(step);
                return;
            }
            if (mode == NpcMode.Fighting)
            {
                FightTick(player, d);
                return;
            }
            CalmTick(step, player, d);
        }

        void FleeTick(float step)
        {
            DaggerfallEntityBehaviour threat = brain.Threat;
            if (threat == null || threat.Entity == null || threat.Entity.CurrentHealth <= 0)
            {
                Calm("its threat is gone");
                return;
            }
            float to = Vector3.Distance(transform.position, threat.transform.position);
            bool seen = brain.CanSee(threat);
            unseen = seen ? 0f : unseen + step;
            int kind = state == AnimalStates.Shy ? AnimalStates.Shy : AnimalStates.Flee;
            if (AnimalRules.FleeDone(kind, to, Mathf.Max(reactNow, s.React), seen, unseen))
                Calm("safe at " + to.ToString("0") + " m");
        }

        void FightTick(DaggerfallEntityBehaviour player, float d)
        {
            if (brain.Threat != player)
                return;     // a hunt or a fight with a creature: the brain ends it when the foe dies or is far
            if (AnimalRules.GivesUp(FromHome(), s.Territory, d, s.Alert))
                Calm("gives up the chase " + FromHome().ToString("0") + " m from home");
        }

        void Calm(string why)
        {
            brain.AnimalCalm();
            if (state == AnimalStates.Flee || state == AnimalStates.Shy || state == AnimalStates.Attack)
                habituated = true;
            Enter(AnimalStates.Unaware);
            lastMode = (int)NpcMode.Calm;
            unseen = 0f;
            AdvancedNpcsMod.Log(brain.Id + ": calm again (" + why + ").");
        }

        float FromHome()
        {
            Vector3 home = transform.parent != null ? transform.parent.TransformPoint(brain.HomeLocal) : brain.HomeLocal;
            Vector3 flat = home - transform.position;
            flat.y = 0f;
            return flat.magnitude;
        }

        void CalmTick(float step, DaggerfallEntityBehaviour player, float d)
        {
            WorldTimeFacts();
            if (prey != null)
                EndHunt();
            if (feeding)
            {
                if (atFood)
                    feedLeft -= step;
                if (feedLeft <= 0f)
                {
                    feeding = false;
                    atFood = false;
                }
            }
            resting = !active && state == AnimalStates.Unaware && !feeding;

            // Senses: the player's way of moving, stealth and magic change how far it notices.
            PlayerMotor pm = GameManager.Instance.PlayerMotor;
            // DFU counts standing still as slow: only crouching or creeping along is sneaking here.
            bool sneaking = pm != null && (pm.IsCrouching || (!pm.IsStandingStill && pm.IsMovingLessThanHalfSpeed));
            bool running = pm != null && pm.IsRunning;
            int stealth = GameManager.Instance.PlayerEntity.Skills.GetLiveSkillValue(DFCareer.Skills.Stealth);
            bool concealed = GameManager.Instance.PlayerEntity.IsMagicallyConcealed;
            bool dozy = night && s.Activity == Activities.Always;
            alertNow = AnimalRules.Sense(s.Alert, sneaking, running, stealth, resting, dozy, concealed, AnimalRules.MinAlert);
            reactNow = Mathf.Min(alertNow, AnimalRules.Sense(s.React, sneaking, running, stealth, resting, dozy, concealed, AnimalRules.MinReact));
            bool seen = d <= alertNow && brain.CanSee(player);
            bool noticed = AnimalRules.Notices(d, alertNow, seen);
            if (habituated && d > alertNow * AnimalRules.RelaxFactor)
                habituated = false;
            if (noticed && !noticedBefore)
                bold = s.Temperament == Temperaments.Predator && rng.Next(100) < s.BoldChance;
            noticedBefore = noticed;

            int next = AnimalRules.Next(s, state, d, alertNow, reactNow, noticed, stateTime, bold, habituated);
            if (next == AnimalStates.Flee || next == AnimalStates.Shy)
            {
                Enter(next);
                feeding = false;
                AdvancedNpcsMod.Log(brain.Id + ": " + (next == AnimalStates.Shy ? "shies away from" : "flees from") + " the player at " + d.ToString("0.0") + " m.");
                brain.AnimalFlee(player);
                lastMode = (int)NpcMode.Fleeing;
                Alarm(next, player);
                CrossAlarm(player);
                return;
            }
            if (next == AnimalStates.Attack)
            {
                AttackPlayer(bold && d > reactNow ? "boldly" : "at " + d.ToString("0.0") + " m");
                Alarm(next, player);
                return;
            }
            if (next != state)
            {
                if (state == AnimalStates.Alert && next == AnimalStates.Unaware && s.Temperament == Temperaments.Grazer)
                    habituated = true;
                Enter(next);
                if (next == AnimalStates.Alert)
                    Play(SoundPools.Alert);
                else if (next == AnimalStates.Warning)
                    warnSoundIn = 0f;
            }
            if (state == AnimalStates.Unaware && !resting && active)
            {
                Calls(step, d);
                if (!feeding && prey == null)
                {
                    FleeFromHunters();
                    if (brain.CurrentMode == NpcMode.Calm)
                        Hunt(step);
                }
            }
            Herd(step);
            animation = PickAnimation();
        }

        void WorldTimeFacts()
        {
            DaggerfallDateTime now = DaggerfallUnity.Instance.WorldTime.Now;
            active = Activities.Active(s.Activity, now.Hour);
            night = now.IsNight;
        }

        void AttackPlayer(string how)
        {
            Enter(AnimalStates.Attack);
            feeding = false;
            AdvancedNpcsMod.Log(brain.Id + ": attacks the player " + how + ".");
            brain.AnimalAttackPlayer();
            lastMode = (int)NpcMode.Fighting;
        }

        void Enter(int next)
        {
            state = next;
            stateTime = 0f;
        }

        /// <summary>Its group (same spawn group and parent) within 30 m takes up its alarm: fighters join an attack, the rest flee.</summary>
        void Alarm(int action, DaggerfallEntityBehaviour threat)
        {
            int group = brain.Instance != null ? brain.Instance.Group : -1;
            if (group < 0)
                return;
            foreach (NpcAnimal other in All)
            {
                if (other == this || other.brain == null || other.brain.Instance == null || other.brain.Instance.Group != group ||
                    other.transform.parent != transform.parent || other.brain.CurrentMode != NpcMode.Calm || !other.isActiveAndEnabled ||
                    Vector3.Distance(other.transform.position, transform.position) > AnimalRules.GroupAlarmRange)
                    continue;
                if (action == AnimalStates.Attack && Temperaments.Fights(other.s.Temperament))
                {
                    other.AttackPlayer("with its group");
                }
                else if (!Temperaments.Fights(other.s.Temperament))
                {
                    other.Enter(AnimalStates.Flee);
                    other.feeding = false;
                    other.brain.AnimalFlee(threat);
                    other.lastMode = (int)NpcMode.Fleeing;
                }
            }
        }

        /// <summary>Prey and grazers of any kind within 20 m flee from what this one flees from (a stampede).</summary>
        void CrossAlarm(DaggerfallEntityBehaviour threat)
        {
            if (threat == null || Temperaments.Fights(s.Temperament))
                return;
            foreach (NpcAnimal other in All)
            {
                if (other == this || other.brain == null || other.brain.CurrentMode != NpcMode.Calm || !other.isActiveAndEnabled ||
                    (other.s.Temperament != Temperaments.Prey && other.s.Temperament != Temperaments.Grazer) ||
                    other.brain.EntityBehaviour == threat ||
                    Vector3.Distance(other.transform.position, transform.position) > AnimalRules.CrossAlarmRange)
                    continue;
                other.Enter(AnimalStates.Flee);
                other.feeding = false;
                other.brain.AnimalFlee(threat);
                other.lastMode = (int)NpcMode.Fleeing;
                AdvancedNpcsMod.Log(other.brain.Id + ": flees with " + brain.Id + ".");
            }
        }

        /// <summary>Prey and grazers flee from a predator they see hunting within their alert range.</summary>
        void FleeFromHunters()
        {
            if (s.Temperament != Temperaments.Prey && s.Temperament != Temperaments.Grazer)
                return;
            // Every few ticks for every prey animal: the live list as it is, no copy (nothing here changes it).
            foreach (NpcAnimal other in live)
            {
                if (other == null || other == this || other.brain == null || other.prey == null || other.brain.CurrentMode != NpcMode.Fighting ||
                    Vector3.Distance(other.transform.position, transform.position) > s.Alert || !brain.CanSee(other.brain.EntityBehaviour))
                    continue;
                Enter(AnimalStates.Flee);
                AdvancedNpcsMod.Log(brain.Id + ": flees from hunting " + other.brain.Id + ".");
                brain.AnimalFlee(other.brain.EntityBehaviour);
                lastMode = (int)NpcMode.Fleeing;
                return;
            }
        }

        void Calls(float step, float d)
        {
            if (s.CallMax <= 0f)
                return;
            callIn -= step;
            if (callIn > 0f)
                return;
            callIn = AnimalRules.CallSeconds(s, rng.NextDouble());
            if (d <= CallRange)
                Play(SoundPools.Call);
        }

        /// <summary>A hungry predator picks the nearest prey animal it sees within aggroRange; its group joins in.</summary>
        void Hunt(float step)
        {
            if (s.Hunts.Count == 0 || Now() < hungryAt)
                return;
            huntIn -= step;
            if (huntIn > 0f)
                return;
            huntIn = HuntRetrySeconds;
            NpcAnimal target = FindPrey();
            if (target == null)
                return;
            StartHunt(target);
            int group = brain.Instance != null ? brain.Instance.Group : -1;
            if (group < 0)
                return;
            foreach (NpcAnimal other in All)
            {
                if (other != this && other.brain != null && other.brain.Instance != null && other.brain.Instance.Group == group &&
                    other.brain.CurrentMode == NpcMode.Calm && other.s.Hunts.Count > 0 && other.prey == null &&
                    Vector3.Distance(other.transform.position, transform.position) <= AnimalRules.GroupAlarmRange)
                    other.StartHunt(target);
            }
        }

        /// <summary>Starts a hunt at once (the self-test, and a pack joining in).</summary>
        public void StartHunt(NpcAnimal target)
        {
            if (target == null || target.brain == null || target.brain.EntityBehaviour == null)
                return;
            prey = target;
            AdvancedNpcsMod.Log(brain.Id + ": hunts " + target.brain.Id + ".");
            brain.AnimalHunt(target.brain.EntityBehaviour);
            lastMode = (int)NpcMode.Fighting;
        }

        /// <summary>Makes it hungry now (the self-test).</summary>
        public void MakeHungry()
        {
            hungryAt = 0;
            huntIn = 0f;
        }

        NpcAnimal FindPrey()
        {
            NpcAnimal best = null;
            float bestDistance = brain.Definition.AggroRange;
            foreach (NpcAnimal other in All)
            {
                if (other == this || other.brain == null || other.brain.Definition == null || other.brain.CurrentMode == NpcMode.Dead ||
                    !other.isActiveAndEnabled || other.brain.Definition == brain.Definition ||
                    !AnimalRules.Hunts(s, other.brain.Definition.Id, other.s.Temperament))
                    continue;
                float to = Vector3.Distance(other.transform.position, transform.position);
                if (to <= bestDistance && brain.CanSee(other.brain.EntityBehaviour))
                {
                    best = other;
                    bestDistance = to;
                }
            }
            return best;
        }

        /// <summary>Back to calm after a hunt: a kill means a meal at the body and a full stomach; an escape, a try later.</summary>
        void EndHunt()
        {
            NpcAnimal was = prey;
            prey = null;
            bool killed = was == null || was.brain == null || was.brain.CurrentMode == NpcMode.Dead ||
                          was.brain.EntityBehaviour == null || was.brain.EntityBehaviour.Entity == null || was.brain.EntityBehaviour.Entity.CurrentHealth <= 0;
            if (killed && was != null)
            {
                feeding = true;
                atFood = false;
                feedLeft = FeedMin + (float)rng.NextDouble() * (FeedMax - FeedMin);
                Vector2 jitter = Random.insideUnitCircle * 0.6f;
                feedAt = was.transform.position + new Vector3(jitter.x, 0f, jitter.y);
                hungryAt = Now() + AnimalRules.HungerSeconds(s, rng.NextDouble());
                AdvancedNpcsMod.Log(brain.Id + ": feeds on " + was.brain.Id + ".");
            }
            else
            {
                hungryAt = Now() + EscapedHuntHours * 3600.0;
            }
        }

        /// <summary>Followers wander around their leader; when the leader is gone the next member leads.</summary>
        void Herd(float step)
        {
            herdIn -= step;
            if (herdIn > 0f)
                return;
            herdIn = HerdSeconds;
            if (Leader == null)
                return;
            if (Leader.CurrentMode == NpcMode.Dead || !Leader.isActiveAndEnabled)
            {
                Promote(Leader);
                if (Leader == null)
                    return;
            }
            if (s.Herd && Leader.transform.parent == transform.parent)
                brain.SetHome(Leader.transform.localPosition);
        }

        /// <summary>The first living follower of a fallen leader leads the rest.</summary>
        static void Promote(NpcBrain fallen)
        {
            NpcAnimal next = null;
            List<NpcAnimal> followers = new List<NpcAnimal>();
            foreach (NpcAnimal a in All)
            {
                if (a.Leader != fallen)
                    continue;
                followers.Add(a);
                if (next == null && a.brain != null && a.brain.CurrentMode != NpcMode.Dead && a.isActiveAndEnabled)
                    next = a;
            }
            foreach (NpcAnimal a in followers)
                a.Leader = a == next || next == null ? null : next.brain;
            if (next != null)
                AdvancedNpcsMod.Log(next.brain.Id + ": leads its group now.");
        }

        string PickAnimation()
        {
            if (resting)
                return Pick("sleep", "rest");
            if (feeding && atFood)
                return Pick("eat");
            if (state == AnimalStates.Alert)
                return Pick("alert", "look");
            if (state == AnimalStates.Warning)
                return Pick("warn", "alert", "look");
            return null;
        }

        /// <summary>The first family the sprite set has, else idle.</summary>
        string Pick(params string[] families)
        {
            LoadedSpriteSet set = sprite != null ? sprite.ChosenSet : null;
            if (set == null || set.Set == null)
                return "idle";
            foreach (string f in families)
                foreach (string name in set.Set.Animations.Keys)
                    if (PackageAnimations.Matches(f, name))
                        return f;
            return "idle";
        }

        void Play(string pool)
        {
            if (voice != null)
                voice.Play(pool);
        }

        /// <summary>One line for anpc_info.</summary>
        public string Describe()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append(s.Temperament).Append(", ").Append(s.Activity).Append(active ? " (active now)" : " (resting hours)");
            sb.Append(", ").Append(resting ? "resting" : AnimalStates.Name(state));
            sb.Append("; alert ").Append(s.Alert.ToString("0")).Append(" m (now ").Append(alertNow.ToString("0.0"))
              .Append("), react ").Append(s.React.ToString("0")).Append(" m (now ").Append(reactNow.ToString("0.0")).Append(")");
            if (s.Territory > 0 && brain != null)
                sb.Append(", territory ").Append(s.Territory.ToString("0")).Append(" m (").Append(FromHome().ToString("0")).Append(" m from home)");
            if (Leader != null)
                sb.Append(", follows ").Append(Leader.Id);
            if (s.Hunts.Count > 0)
            {
                double hours = (hungryAt - Now()) / 3600.0;
                sb.Append(prey != null && prey.brain != null ? ", hunting " + prey.brain.Id : feeding ? ", feeding" : hours > 0 ? ", hungry in " + hours.ToString("0.0") + " h" : ", hungry");
            }
            return sb.ToString();
        }

        static double Now()
        {
            return DaggerfallUnity.Instance.WorldTime.Now.ToSeconds();
        }
    }
}

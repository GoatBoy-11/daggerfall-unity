using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    // Top-level on purpose: DFU's runtime compiler cannot load a field whose type is a nested enum.
    public enum NpcMode { Calm, Fighting, Fleeing, Dead }

    /// <summary>
    /// Authoritative state machine for one Advanced NPC. Vanilla EnemyMotor/EnemyAttack only run while Fighting.
    /// Undoes GameManager.MakeEnemiesHostile() for NPCs that were not actually attacked.
    /// </summary>
    [RequireComponent(typeof(NpcMover))]
    public class NpcBrain : MonoBehaviour
    {

        const float SafeDistance = 30f;
        const float CalmCheckInterval = 2f;
        /// <summary>hostileUntil of an enemy: never calms by time (enemy rules decide).</summary>
        const ulong EnemyMarker = (ulong)long.MaxValue;     // the save serializer stores a long
        const float WanderPauseMin = 2f;
        const float WanderPauseMax = 6f;

        static readonly Dictionary<string, NpcBrain> live = new Dictionary<string, NpcBrain>();

        NpcDefinition def;
        NpcInstance instance;
        string key;
        NpcState state;
        NpcMode mode = NpcMode.Calm;
        DaggerfallEntityBehaviour threat;

        DaggerfallEntityBehaviour entityBehaviour;
        EnemyMotor motor;
        EnemySenses senses;
        EnemyAttack attack;
        NpcMover mover;

        int lastHealth;
        Vector3 homeLocal;
        Vector3 wanderTargetLocal;
        bool hasWanderTarget;
        bool enemy;             // an enemy of the player now (enemies spec)
        bool provoked;          // the player hit it while it was an enemy
        float wanderPause;
        float calmCheckTimer;
        FleeRoll fleeRoll;      // Normal's one flee roll while badly hurt
        float holdStill;        // seconds the calm NPC stands still (greeting)
        NpcGait gait;
        float patrolTimer;
        static Collider[] nearby = new Collider[64];
        const int MaxNearby = 512;
        System.Random rng;

        /// <summary>The spawned copy of an NPC, or null.</summary>
        public static NpcBrain Find(string id)
        {
            NpcBrain b;
            if (live.TryGetValue(id, out b) && b != null)
                return b;
            return null;
        }

        /// <summary>Forgets and destroys a spawned copy (used for stale copies under a town being torn down).</summary>
        public static void Discard(NpcBrain brain)
        {
            if (brain == null)
                return;
            NpcBrain current;
            if (brain.key != null && live.TryGetValue(brain.key, out current) && current == brain)
                live.Remove(brain.key);
            Destroy(brain.gameObject);
        }

        /// <summary>All spawned NPCs (for console commands).</summary>
        public static List<NpcBrain> All()
        {
            List<NpcBrain> result = new List<NpcBrain>();
            foreach (NpcBrain b in live.Values)
            {
                if (b != null)
                    result.Add(b);
            }
            return result;
        }

        /// <summary>Most metres this person wanders from its spot (indoors 3 m, spec outside-towns §7).</summary>
        public float WanderCap = float.MaxValue;

        public NpcMode CurrentMode
        {
            get { return mode; }
        }

        /// <summary>An enemy of the player now (hostile attitude in its hours, or switched on the go).</summary>
        public bool IsEnemy
        {
            get { return enemy; }
        }

        /// <summary>Re-reads the enemy rules now (normally every few seconds).</summary>
        public void RefreshEnemy()
        {
            if (mode == NpcMode.Dead || entityBehaviour == null)
                return;
            bool want = EnemyRules.IsEnemyNow(def.Hostile, def.HostileFrom, def.HostileTo,
                DaggerfallUnity.Instance.WorldTime.Now.Hour, state.enemyOn, state.enemyOff);
            if (want && !enemy)
                BecomeEnemy();
            else if (!want && enemy)
                StopBeingEnemy();
        }

        /// <summary>Switches this ANPC into an enemy or calms it (anpc_hostile, SetHostile). Saved in its state.</summary>
        public void SwitchHostile(bool hostile)
        {
            state.enemyOn = hostile;
            state.enemyOff = !hostile;
            RefreshEnemy();
            if (!hostile && state.hostile && mode != NpcMode.Dead)
                CalmNow();
        }

        void BecomeEnemy()
        {
            enemy = true;
            provoked = false;
            state.hostile = true;
            state.hostileUntil = EnemyMarker;
            threat = GameManager.Instance.PlayerEntityBehaviour;
            EnemyEntity entity = entityBehaviour.Entity as EnemyEntity;
            if (entity != null)
                entity.Team = entity.MobileEnemy.Team;       // its own side, like a vanilla enemy
            motor.IsHostile = true;
            AdvancedNpcsMod.Log(key + ": is an enemy now.");
            EnterCombat();
        }

        void StopBeingEnemy()
        {
            enemy = false;
            entityBehaviour.Entity.Team = MobileTeams.CityWatch;
            if (provoked && !state.enemyOff)
            {
                // The player fought it: it calms down like provoked townsfolk.
                state.hostileUntil = HostilityRules.NewCalmDeadline(Now(), def.CalmDownMinHours, def.CalmDownMaxHours, rng);
                AdvancedNpcsMod.Log(key + ": no longer an enemy, still angry.");
                return;
            }
            AdvancedNpcsMod.Log(key + ": no longer an enemy.");
            CalmNow();
        }

        void CalmNow()
        {
            state.hostile = false;
            motor.IsHostile = false;
            SetMode(NpcMode.Calm);
        }

        public NpcState State
        {
            get { return state; }
        }

        public DaggerfallEntityBehaviour EntityBehaviour
        {
            get { return entityBehaviour; }
        }

        public EnemyMotor Motor
        {
            get { return motor; }
        }

        /// <summary>Unique id or generic instance key.</summary>
        public string Id
        {
            get { return key != null ? key : "?"; }
        }

        public string DisplayName
        {
            get { return instance != null ? instance.Name : "?"; }
        }

        public NpcInstance Instance
        {
            get { return instance; }
        }

        public NpcDefinition Definition
        {
            get { return def; }
        }

        /// <summary>Personal opinion of the player now (spec opinion-lod §A), -100..100.</summary>
        public int Opinion
        {
            get { return state != null ? OpinionRules.Current(state.opinion, state.opinionAt, Now()) : 0; }
        }

        /// <summary>Changes the personal opinion of the player (drifted first, clamped); saved with the state.</summary>
        public void ChangeOpinion(int delta)
        {
            if (state == null || delta == 0)
                return;
            ulong now = Now();
            int next = OpinionRules.Change(state.opinion, state.opinionAt, now, delta);
            state.opinionAt = OpinionRules.ChangedAt(state.opinion, state.opinionAt, now);
            state.opinion = next;
        }

        /// <summary>Calm ANPCs (not enemies) within 15 m who can see the spot think less of the player.</summary>
        static void WitnessPlayerViolence(Vector3 victimAt, NpcBrain victim)
        {
            foreach (NpcBrain w in All())
            {
                // Not someone in a switched-off place (the town while the player is indoors): it cannot see.
                if (w == victim || !w.isActiveAndEnabled || w.mode != NpcMode.Calm || w.enemy || w.state == null || w.state.hostile)
                    continue;
                if (Vector3.Distance(w.transform.position, victimAt) > OpinionRules.WitnessRange || !w.CanSeePoint(victimAt))
                    continue;
                w.ChangeOpinion(OpinionRules.Witness);
                AdvancedNpcsMod.Log(w.key + ": saw the player attack someone (opinion " + w.Opinion + ").");
            }
        }

        /// <summary>A clear line from this NPC to a point (half a metre above both), ignoring what stands on the point.</summary>
        bool CanSeePoint(Vector3 point)
        {
            Vector3 from = transform.position + Vector3.up * 0.5f;
            Vector3 to = point + Vector3.up * 0.5f;
            RaycastHit hit;
            if (!Physics.Linecast(from, to, out hit))
                return true;
            return hit.collider.transform.IsChildOf(transform) || Vector3.Distance(hit.point, to) < 0.8f;
        }

        /// <summary>Whom it fights or flees from (null when calm).</summary>
        public DaggerfallEntityBehaviour Threat
        {
            get { return threat; }
        }

        /// <summary>Stops calm wandering for a while (greetings). Fighting and fleeing are not affected.</summary>
        public void HoldStill(float seconds)
        {
            holdStill = Mathf.Max(holdStill, seconds);
        }

        bool IsTurret
        {
            get { return gait != null && gait.IsTurret; }
        }

        /// <summary>Health as a fraction of max (the saved fraction before the ANPC has started).</summary>
        public float CurrentHealthFraction
        {
            get { return entityBehaviour != null && entityBehaviour.Entity != null ? HealthFraction() : state.healthFraction; }
        }

        /// <summary>One-line state for console output.</summary>
        public string Status
        {
            get
            {
                string text = mode.ToString();
                if (state != null && state.hostile)
                    text += enemy ? ", enemy" : ", hostile to player";
                if (entityBehaviour != null && entityBehaviour.Entity != null)
                    text += ", health " + entityBehaviour.Entity.CurrentHealth + "/" + entityBehaviour.Entity.MaxHealth;
                if (!gameObject.activeInHierarchy)
                    text += ", inactive";
                int opinion = Opinion;
                if (opinion != 0)
                    text += ", opinion " + opinion;
                if (def != null && def.Magic != null && entityBehaviour != null)
                    text += def.Magic.UsesMagicka ? ", magicka " + state.magicka + "/" + def.Magic.MaxMagicka : ", unlimited magicka";
                return text;
            }
        }

        /// <summary>Moves the NPC, and the centre it wanders around, to a world position. Not saved.</summary>
        public void Teleport(Vector3 worldPos)
        {
            CharacterController controller = GetComponent<CharacterController>();
            bool wasEnabled = controller.enabled;
            controller.enabled = false;
            transform.position = worldPos;
            controller.enabled = wasEnabled;
            homeLocal = transform.localPosition;
            hasWanderTarget = false;
            NpcGait g = GetComponent<NpcGait>();
            if (g != null)
                g.Repin();
        }

        public static void DespawnAll()
        {
            foreach (NpcBrain b in new List<NpcBrain>(live.Values))
            {
                if (b != null)
                    Destroy(b.gameObject);
            }
            live.Clear();
        }

        public void Init(NpcInstance npcInstance, NpcState npcState)
        {
            instance = npcInstance;
            def = npcInstance.Definition;
            key = npcInstance.Key;
            state = npcState;
            rng = new System.Random((int)npcInstance.Seed ^ System.Environment.TickCount);
            live[key] = this;
        }

        void Start()
        {
            entityBehaviour = GetComponent<DaggerfallEntityBehaviour>();
            motor = GetComponent<EnemyMotor>();
            senses = GetComponent<EnemySenses>();
            attack = GetComponent<EnemyAttack>();
            mover = GetComponent<NpcMover>();
            gait = GetComponent<NpcGait>();
            patrolTimer = Random.Range(0f, AggroRules.CheckInterval);
            homeLocal = transform.localPosition;

            // No language pacification roll: it would turn a player's attack back off before Update sees it.
            senses.HasEncounteredPlayer = true;

            int restored = HealthRules.Restore(entityBehaviour.Entity.MaxHealth, state.healthFraction);
            if (restored != entityBehaviour.Entity.CurrentHealth)
                entityBehaviour.Entity.CurrentHealth = restored;
            lastHealth = entityBehaviour.Entity.CurrentHealth;
            entityBehaviour.Entity.OnDeath += OnDeath;

            if (state.hostile && (HostilityRules.IsCalmDue(Now(), state.hostileUntil) || state.hostileUntil == EnemyMarker))
                state.hostile = false;      // an enemy's hostility is decided again by the enemy rules below

            if (state.hostile)
            {
                threat = GameManager.Instance.PlayerEntityBehaviour;
                EnterCombat();
            }
            else
            {
                SetMode(NpcMode.Calm);
            }
            RefreshEnemy();
        }

        void OnDestroy()
        {
            if (entityBehaviour != null && entityBehaviour.Entity != null)
            {
                entityBehaviour.Entity.OnDeath -= OnDeath;
                if (mode != NpcMode.Dead)
                    state.healthFraction = HealthRules.Fraction(entityBehaviour.Entity.CurrentHealth, entityBehaviour.Entity.MaxHealth);
            }
            NpcBrain current;
            if (key != null && live.TryGetValue(key, out current) && current == this)
                live.Remove(key);
        }

        void Update()
        {
            if (mode == NpcMode.Dead || entityBehaviour == null)
                return;

            DaggerfallEntityBehaviour player = GameManager.Instance.PlayerEntityBehaviour;
            int health = entityBehaviour.Entity.CurrentHealth;
            bool healthDropped = health < lastHealth;
            lastHealth = health;
            state.healthFraction = HealthRules.Fraction(health, entityBehaviour.Entity.MaxHealth);

            // Hostility guard: decide why IsHostile changed before anything else reads it.
            // GiveUpTimer is zeroed every calm frame; only MakeEnemyHostileToAttacker raises it while the motor is off.
            HostileFlip flip = HostilityRules.ClassifyHostileFlip(
                state.hostile, motor.IsHostile, healthDropped, senses.Target == player, motor.GiveUpTimer > 0);
            if (flip == HostileFlip.PlayerAttack)
            {
                OnAttackedByPlayer();
            }
            else if (flip == HostileFlip.EngineSweep)
            {
                motor.IsHostile = false;
                if (senses.Target == player)
                    senses.Target = null;
            }
            else if (healthDropped && state.hostile && threat == player && enemy)
            {
                provoked = true;    // fighting an enemy: no crime, no calm-down deadline
            }
            else if (healthDropped && state.hostile && threat == player)
            {
                // Player hit again while already hostile: anger lasts longer, no new assault.
                state.hostileUntil = HostilityRules.NewCalmDeadline(Now(), def.CalmDownMinHours, def.CalmDownMaxHours, rng);
            }
            else if (healthDropped && !state.hostile && senses.Target != null && senses.Target != player)
            {
                OnAttackedByCreature(senses.Target);
            }

            // Keep vanilla in line with our state (EnemyMotor.Start resets IsHostile from MobileReactions).
            motor.IsHostile = state.hostile;

            UpdateCalmDown();

            switch (mode)
            {
                case NpcMode.Calm:
                    if (HostilityRules.AttackedWhileCalm(true, motor.GiveUpTimer > 0, senses.Target != null && senses.Target != player,
                        senses.Target != null && senses.Target.Entity != null && senses.Target.Entity.CurrentHealth > 0))
                    {
                        // Swung at, hit or missed (DFU's MakeEnemyHostileToAttacker): fight back or flee.
                        OnAttackedByCreature(senses.Target);
                        break;
                    }
                    motor.GiveUpTimer = 0;
                    if (def.Bravery == Bravery.Brave && Patrol())
                        break;
                    UpdateCalm();
                    break;
                case NpcMode.Fighting:
                    UpdateFighting();
                    break;
                case NpcMode.Fleeing:
                    UpdateFleeing();
                    break;
            }
        }

        void OnAttackedByPlayer()
        {
            bool wasCalm = !state.hostile;
            state.hostile = true;
            state.hostileUntil = HostilityRules.NewCalmDeadline(Now(), def.CalmDownMinHours, def.CalmDownMaxHours, rng);
            threat = GameManager.Instance.PlayerEntityBehaviour;

            if (wasCalm)
            {
                ChangeOpinion(OpinionRules.Assault);
                WitnessPlayerViolence(transform.position, this);
            }
            if (wasCalm && def.CrimeOnAttack)
            {
                NpcCrime.Report(PlayerEntity.Crimes.Assault);
                AdvancedNpcsMod.Log(key + ": assaulted by player.");
            }
            EnterCombat();
        }

        /// <summary>A calm Brave ANPC attacks the nearest foe it sees within aggroRange (spec lazy-sprites §D).</summary>
        bool Patrol()
        {
            patrolTimer -= Time.deltaTime;
            if (patrolTimer > 0f)
                return false;
            GameObject playerObject = GameManager.Instance.PlayerObject;
            float playerDistance = playerObject != null ? Vector3.Distance(playerObject.transform.position, transform.position) : 0f;
            patrolTimer = AggroRules.CheckInterval * NpcLod.Interval(playerDistance, false);
            DaggerfallEntityBehaviour foe = FindFoe(def.AggroRange);
            if (foe == null)
                return false;
            AdvancedNpcsMod.Log(key + ": attacks " + foe.name + " on sight.");
            senses.Target = foe;        // DFU's senses chase it now instead of waiting to notice it
            OnAttackedByCreature(foe);
            return true;
        }

        DaggerfallEntityBehaviour FindFoe(float range)
        {
            DaggerfallEntityBehaviour best = null;
            float bestDistance = range;
            foreach (NpcBrain other in All())
            {
                if (other == this || !other.isActiveAndEnabled || other.entityBehaviour == null || other.entityBehaviour.Entity == null)
                    continue;
                bool alive = other.mode != NpcMode.Dead && other.entityBehaviour.Entity.CurrentHealth > 0;
                if (!AggroRules.IsFoe(true, other.IsEnemy, false, false, alive))
                    continue;
                float d = Vector3.Distance(transform.position, other.transform.position);
                if (d <= bestDistance && CanSee(other.entityBehaviour))
                {
                    best = other.entityBehaviour;
                    bestDistance = d;
                }
            }
            int count = Physics.OverlapSphereNonAlloc(transform.position, range, nearby, ~0, QueryTriggerInteraction.Ignore);
            while (count == nearby.Length && nearby.Length < MaxNearby)
            {
                nearby = new Collider[nearby.Length * 2];      // a crowded spot: look again with room for all
                count = Physics.OverlapSphereNonAlloc(transform.position, range, nearby, ~0, QueryTriggerInteraction.Ignore);
            }
            for (int i = 0; i < count; i++)
            {
                DaggerfallEntityBehaviour e = nearby[i].GetComponentInParent<DaggerfallEntityBehaviour>();
                if (e == null || e == entityBehaviour || e.Entity == null || e.GetComponent<NpcBrain>() != null)
                    continue;
                EnemyMotor m = e.GetComponent<EnemyMotor>();
                if (m == null)
                    continue;
                bool watchOrAlly = e.Entity.Team == MobileTeams.CityWatch || e.Entity.Team == MobileTeams.PlayerAlly;
                if (!AggroRules.IsFoe(false, false, m.IsHostile, watchOrAlly, e.Entity.CurrentHealth > 0))
                    continue;
                float d = Vector3.Distance(transform.position, e.transform.position);
                if (d <= bestDistance && CanSee(e))
                {
                    best = e;
                    bestDistance = d;
                }
            }
            return best;
        }

        void OnAttackedByCreature(DaggerfallEntityBehaviour attacker)
        {
            if (mode != NpcMode.Calm)
                return;
            threat = attacker;
            EnterCombat();
        }

        void EnterCombat()
        {
            CombatChoice choice = HostilityRules.Decide(def.Bravery, HealthFraction(), def.FleeHealthPercent, def.FleeChance, ref fleeRoll, rng.NextDouble());
            SetMode(choice == CombatChoice.Flee ? NpcMode.Fleeing : NpcMode.Fighting);
        }

        void UpdateCalmDown()
        {
            calmCheckTimer -= Time.deltaTime;
            if (calmCheckTimer > 0f)
                return;
            calmCheckTimer = CalmCheckInterval;
            RefreshEnemy();

            if (state.hostile && HostilityRules.IsCalmDue(Now(), state.hostileUntil))
            {
                state.hostile = false;
                motor.IsHostile = false;
                // Hours have passed; the NPC has recovered.
                entityBehaviour.Entity.CurrentHealth = entityBehaviour.Entity.MaxHealth;
                lastHealth = entityBehaviour.Entity.CurrentHealth;
                state.healthFraction = 1f;
                AdvancedNpcsMod.Log(key + ": calmed down.");
                if (threat == GameManager.Instance.PlayerEntityBehaviour)
                    SetMode(NpcMode.Calm);
            }
        }

        void UpdateCalm()
        {
            if (holdStill > 0f || IsTurret)
            {
                holdStill = Mathf.Max(0f, holdStill - Time.deltaTime);
                mover.Stop();
                return;
            }
            float wanderRadius = Mathf.Min(def.WanderRadius, WanderCap);
            if (wanderRadius <= 0f)
            {
                mover.Stop();
                return;
            }

            if (!hasWanderTarget)
            {
                wanderPause -= Time.deltaTime;
                mover.Stop();
                if (wanderPause > 0f)
                    return;
                Vector2 offset = Random.insideUnitCircle * wanderRadius;
                wanderTargetLocal = homeLocal + new Vector3(offset.x, 0f, offset.y);
                hasWanderTarget = true;
            }

            Vector3 targetWorld = transform.parent != null ? transform.parent.TransformPoint(wanderTargetLocal) : wanderTargetLocal;
            Vector3 flat = targetWorld - transform.position;
            flat.y = 0f;
            if (flat.magnitude < 0.5f)
            {
                hasWanderTarget = false;
                wanderPause = Random.Range(WanderPauseMin, WanderPauseMax);
                mover.Stop();
                return;
            }
            mover.MoveToward(targetWorld, NpcMover.WalkSpeed);
        }

        void UpdateFighting()
        {
            if (ThreatGone())
            {
                CountHelp();
                EndCreatureFight();
                return;
            }
            if (threat != GameManager.Instance.PlayerEntityBehaviour)
                senses.Target = threat;
            if (HostilityRules.Decide(def.Bravery, HealthFraction(), def.FleeHealthPercent, def.FleeChance, ref fleeRoll, rng.NextDouble()) == CombatChoice.Flee)
                SetMode(NpcMode.Fleeing);
        }

        /// <summary>The creature this ANPC fought or fled from died with the player close by: +10 opinion.</summary>
        void CountHelp()
        {
            DaggerfallEntityBehaviour player = GameManager.Instance.PlayerEntityBehaviour;
            if (threat != null && threat != player && threat.Entity != null && threat.Entity.CurrentHealth <= 0 && player != null &&
                Vector3.Distance(player.transform.position, transform.position) <= OpinionRules.HelpRange)
            {
                ChangeOpinion(OpinionRules.Helped);
                AdvancedNpcsMod.Log(key + ": the player helped in a fight (opinion " + Opinion + ").");
            }
        }

        void UpdateFleeing()
        {
            if (ThreatGone())
            {
                CountHelp();
                EndCreatureFight();
                return;
            }
            float distance = Vector3.Distance(transform.position, threat.transform.position);
            if (IsTurret || (distance >= SafeDistance && !CanSee(threat)))
                mover.Stop(); // cower (a turret never leaves its spot)
            else
                mover.MoveAway(threat.transform.position, gait != null ? gait.RunSpeed : NpcMover.RunSpeed);
        }

        bool ThreatGone()
        {
            if (threat == GameManager.Instance.PlayerEntityBehaviour)
                return false; // player hostility ends only by calm-down
            return threat == null || threat.Entity == null || threat.Entity.CurrentHealth <= 0 ||
                   Vector3.Distance(transform.position, threat.transform.position) > AggroRules.GiveUpDistance(def.AggroRange);
        }

        void EndCreatureFight()
        {
            if (state.hostile)
            {
                threat = GameManager.Instance.PlayerEntityBehaviour;
                EnterCombat();
            }
            else
            {
                SetMode(NpcMode.Calm);
            }
        }

        /// <summary>A clear line between this NPC and another entity (half a metre above their origins).</summary>
        public bool CanSee(DaggerfallEntityBehaviour other)
        {
            Vector3 from = transform.position + Vector3.up * 0.5f;
            Vector3 to = other.transform.position + Vector3.up * 0.5f;
            RaycastHit hit;
            if (!Physics.Linecast(from, to, out hit))
                return true;
            return hit.collider.GetComponentInParent<DaggerfallEntityBehaviour>() == other;
        }

        void SetMode(NpcMode newMode)
        {
            mode = newMode;
            bool fighting = newMode == NpcMode.Fighting;
            motor.enabled = fighting;
            attack.enabled = fighting;
            mover.enabled = !fighting && newMode != NpcMode.Dead;

            if (newMode == NpcMode.Calm)
            {
                threat = null;
                senses.Target = null;
                hasWanderTarget = false;
                wanderPause = Random.Range(WanderPauseMin, WanderPauseMax);
            }
        }

        void OnDeath(DaggerfallEntity entity)
        {
            // DFU raises OnDeath on every health change while health <= 0; only the first counts.
            if (mode == NpcMode.Dead)
                return;

            bool wasHostile = state.hostile;
            bool wasEnemy = enemy;
            bool fightingCreature = threat != null && threat != GameManager.Instance.PlayerEntityBehaviour;

            mode = NpcMode.Dead;
            state.dead = true;
            state.hostile = false;

            // Custom sprites: the death sheet plays and the body stays as the corpse (spec 1b §6.4).
            NpcSprite sprite = GetComponent<NpcSprite>();
            if (sprite != null && sprite.Set != null && NpcCorpseSprite.Spawn(sprite, entityBehaviour) != null)
                sprite.Hide();

            // DFU tells the victim who hit it only after OnDeath; decide the killer once that has happened.
            AdvancedNpcsMod.Instance.StartCoroutine(ResolveKiller(def, key, motor, wasHostile, fightingCreature, wasEnemy, transform.position));
        }

        static IEnumerator ResolveKiller(NpcDefinition def, string key, EnemyMotor motor, bool wasHostile, bool fightingCreature, bool wasEnemy,
            Vector3 diedAt)
        {
            yield return null;

            bool motorHostile = motor != null && motor.IsHostile;
            bool byPlayer = HostilityRules.KilledByPlayer(wasHostile, fightingCreature, motorHostile);
            if (byPlayer && def.CrimeOnAttack && !wasEnemy)
            {
                NpcCrime.Report(PlayerEntity.Crimes.Murder);
            }
            AdvancedNpcsMod.Log(key + ": died" + (byPlayer ? " (player)." : " (creature)."));
            if (byPlayer && !wasEnemy)
                WitnessPlayerViolence(diedAt, null);
        }

        float HealthFraction()
        {
            int max = entityBehaviour.Entity.MaxHealth;
            return max > 0 ? (float)entityBehaviour.Entity.CurrentHealth / max : 1f;
        }

        static ulong Now()
        {
            return DaggerfallUnity.Instance.WorldTime.Now.ToSeconds();
        }
    }
}

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
        const float CreatureGiveUpDistance = 40f;
        const float CalmCheckInterval = 2f;
        const float WanderPauseMin = 2f;
        const float WanderPauseMax = 6f;

        static readonly Dictionary<string, NpcBrain> live = new Dictionary<string, NpcBrain>();

        NpcDefinition def;
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
        float wanderPause;
        float calmCheckTimer;
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
            if (brain.def != null && live.TryGetValue(brain.def.Id, out current) && current == brain)
                live.Remove(brain.def.Id);
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

        public NpcMode CurrentMode
        {
            get { return mode; }
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

        public string Id
        {
            get { return def != null ? def.Id : "?"; }
        }

        /// <summary>One-line state for console output.</summary>
        public string Status
        {
            get
            {
                string text = mode.ToString();
                if (state != null && state.hostile)
                    text += ", hostile to player";
                if (entityBehaviour != null && entityBehaviour.Entity != null)
                    text += ", health " + entityBehaviour.Entity.CurrentHealth + "/" + entityBehaviour.Entity.MaxHealth;
                if (!gameObject.activeInHierarchy)
                    text += ", inactive";
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

        public void Init(NpcDefinition definition, NpcState npcState)
        {
            def = definition;
            state = npcState;
            rng = new System.Random(definition.Id.GetHashCode() ^ System.Environment.TickCount);
            live[def.Id] = this;
        }

        void Start()
        {
            entityBehaviour = GetComponent<DaggerfallEntityBehaviour>();
            motor = GetComponent<EnemyMotor>();
            senses = GetComponent<EnemySenses>();
            attack = GetComponent<EnemyAttack>();
            mover = GetComponent<NpcMover>();
            homeLocal = transform.localPosition;

            int restored = HealthRules.Restore(entityBehaviour.Entity.MaxHealth, state.healthFraction);
            if (restored != entityBehaviour.Entity.CurrentHealth)
                entityBehaviour.Entity.CurrentHealth = restored;
            lastHealth = entityBehaviour.Entity.CurrentHealth;
            entityBehaviour.Entity.OnDeath += OnDeath;

            if (state.hostile && HostilityRules.IsCalmDue(Now(), state.hostileUntil))
                state.hostile = false;

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

        void OnDestroy()
        {
            if (entityBehaviour != null && entityBehaviour.Entity != null)
            {
                entityBehaviour.Entity.OnDeath -= OnDeath;
                if (mode != NpcMode.Dead)
                    state.healthFraction = HealthRules.Fraction(entityBehaviour.Entity.CurrentHealth, entityBehaviour.Entity.MaxHealth);
            }
            NpcBrain current;
            if (def != null && live.TryGetValue(def.Id, out current) && current == this)
                live.Remove(def.Id);
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
                    motor.GiveUpTimer = 0;
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

            if (wasCalm && def.CrimeOnAttack)
            {
                NpcCrime.Report(PlayerEntity.Crimes.Assault);
                AdvancedNpcsMod.Log(def.Id + ": assaulted by player.");
            }
            EnterCombat();
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
            CombatChoice choice = HostilityRules.Decide(def.Bravery, HealthFraction(), def.FleeHealthPercent);
            SetMode(choice == CombatChoice.Flee ? NpcMode.Fleeing : NpcMode.Fighting);
        }

        void UpdateCalmDown()
        {
            calmCheckTimer -= Time.deltaTime;
            if (calmCheckTimer > 0f)
                return;
            calmCheckTimer = CalmCheckInterval;

            if (state.hostile && HostilityRules.IsCalmDue(Now(), state.hostileUntil))
            {
                state.hostile = false;
                motor.IsHostile = false;
                // Hours have passed; the NPC has recovered.
                entityBehaviour.Entity.CurrentHealth = entityBehaviour.Entity.MaxHealth;
                lastHealth = entityBehaviour.Entity.CurrentHealth;
                state.healthFraction = 1f;
                AdvancedNpcsMod.Log(def.Id + ": calmed down.");
                if (threat == GameManager.Instance.PlayerEntityBehaviour)
                    SetMode(NpcMode.Calm);
            }
        }

        void UpdateCalm()
        {
            if (def.WanderRadius <= 0f)
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
                Vector2 offset = Random.insideUnitCircle * def.WanderRadius;
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
                EndCreatureFight();
                return;
            }
            if (threat != GameManager.Instance.PlayerEntityBehaviour)
                senses.Target = threat;
            if (HostilityRules.Decide(def.Bravery, HealthFraction(), def.FleeHealthPercent) == CombatChoice.Flee)
                SetMode(NpcMode.Fleeing);
        }

        void UpdateFleeing()
        {
            if (ThreatGone())
            {
                EndCreatureFight();
                return;
            }
            float distance = Vector3.Distance(transform.position, threat.transform.position);
            if (distance >= SafeDistance && !CanSee(threat))
                mover.Stop(); // cower
            else
                mover.MoveAway(threat.transform.position, NpcMover.RunSpeed);
        }

        bool ThreatGone()
        {
            if (threat == GameManager.Instance.PlayerEntityBehaviour)
                return false; // player hostility ends only by calm-down
            return threat == null || threat.Entity == null || threat.Entity.CurrentHealth <= 0 ||
                   Vector3.Distance(transform.position, threat.transform.position) > CreatureGiveUpDistance;
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

        bool CanSee(DaggerfallEntityBehaviour other)
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
            bool fightingCreature = threat != null && threat != GameManager.Instance.PlayerEntityBehaviour;

            mode = NpcMode.Dead;
            state.dead = true;
            state.hostile = false;

            // DFU tells the victim who hit it only after OnDeath; decide the killer once that has happened.
            AdvancedNpcsMod.Instance.StartCoroutine(ResolveKiller(def, motor, wasHostile, fightingCreature));
        }

        static IEnumerator ResolveKiller(NpcDefinition def, EnemyMotor motor, bool wasHostile, bool fightingCreature)
        {
            yield return null;

            bool motorHostile = motor != null && motor.IsHostile;
            bool byPlayer = HostilityRules.KilledByPlayer(wasHostile, fightingCreature, motorHostile);
            if (byPlayer && def.CrimeOnAttack)
            {
                NpcCrime.Report(PlayerEntity.Crimes.Murder);
            }
            AdvancedNpcsMod.Log(def.Id + ": died" + (byPlayer ? " (player)." : " (creature)."));
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

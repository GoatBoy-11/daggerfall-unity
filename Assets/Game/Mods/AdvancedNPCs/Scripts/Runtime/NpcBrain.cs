using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>
    /// Authoritative state machine for one Advanced NPC. Vanilla EnemyMotor/EnemyAttack only run while Fighting.
    /// Undoes GameManager.MakeEnemiesHostile() for NPCs that were not actually attacked.
    /// </summary>
    [RequireComponent(typeof(NpcMover))]
    public class NpcBrain : MonoBehaviour
    {
        enum Mode { Calm, Fighting, Fleeing, Dead }

        const float SafeDistance = 30f;
        const float CreatureGiveUpDistance = 40f;
        const float CalmCheckInterval = 2f;
        const float WanderPauseMin = 2f;
        const float WanderPauseMax = 6f;

        static readonly Dictionary<string, NpcBrain> live = new Dictionary<string, NpcBrain>();

        NpcDefinition def;
        NpcState state;
        Mode mode = Mode.Calm;
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

        public static bool IsLive(string id)
        {
            NpcBrain b;
            return live.TryGetValue(id, out b) && b != null;
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

            if (state.health > 0)
                entityBehaviour.Entity.CurrentHealth = state.health;
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
                SetMode(Mode.Calm);
            }
        }

        void OnDestroy()
        {
            if (entityBehaviour != null && entityBehaviour.Entity != null)
            {
                entityBehaviour.Entity.OnDeath -= OnDeath;
                if (mode != Mode.Dead)
                    state.health = entityBehaviour.Entity.CurrentHealth;
            }
            NpcBrain current;
            if (def != null && live.TryGetValue(def.Id, out current) && current == this)
                live.Remove(def.Id);
        }

        void Update()
        {
            if (mode == Mode.Dead || entityBehaviour == null)
                return;

            DaggerfallEntityBehaviour player = GameManager.Instance.PlayerEntityBehaviour;
            int health = entityBehaviour.Entity.CurrentHealth;
            bool healthDropped = health < lastHealth;
            lastHealth = health;
            state.health = health;

            // Hostility guard: decide why IsHostile changed before anything else reads it.
            HostileFlip flip = HostilityRules.ClassifyHostileFlip(
                state.hostile, motor.IsHostile, healthDropped, senses.Target == player);
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
                case Mode.Calm:
                    UpdateCalm();
                    break;
                case Mode.Fighting:
                    UpdateFighting();
                    break;
                case Mode.Fleeing:
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
                PlayerEntity player = GameManager.Instance.PlayerEntity;
                player.CrimeCommitted = PlayerEntity.Crimes.Assault;
                player.SpawnCityGuards(true);
                AdvancedNpcsMod.Log(def.Id + ": assaulted by player.");
            }
            EnterCombat();
        }

        void OnAttackedByCreature(DaggerfallEntityBehaviour attacker)
        {
            if (mode != Mode.Calm)
                return;
            threat = attacker;
            EnterCombat();
        }

        void EnterCombat()
        {
            CombatChoice choice = HostilityRules.Decide(def.Bravery, HealthFraction(), def.FleeHealthPercent);
            SetMode(choice == CombatChoice.Flee ? Mode.Fleeing : Mode.Fighting);
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
                AdvancedNpcsMod.Log(def.Id + ": calmed down.");
                if (threat == GameManager.Instance.PlayerEntityBehaviour)
                    SetMode(Mode.Calm);
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
                SetMode(Mode.Fleeing);
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
                SetMode(Mode.Calm);
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

        void SetMode(Mode newMode)
        {
            mode = newMode;
            bool fighting = newMode == Mode.Fighting;
            motor.enabled = fighting;
            attack.enabled = fighting;
            mover.enabled = !fighting && newMode != Mode.Dead;

            if (newMode == Mode.Calm)
            {
                threat = null;
                senses.Target = null;
                hasWanderTarget = false;
                wanderPause = Random.Range(WanderPauseMin, WanderPauseMax);
            }
        }

        void OnDeath(DaggerfallEntity entity)
        {
            bool fightingCreature = threat != null && threat != GameManager.Instance.PlayerEntityBehaviour;
            bool byPlayer = HostilityRules.KilledByPlayer(state.hostile, fightingCreature);

            mode = Mode.Dead;
            state.dead = true;
            state.hostile = false;

            if (byPlayer && def.CrimeOnAttack)
            {
                PlayerEntity player = GameManager.Instance.PlayerEntity;
                player.CrimeCommitted = PlayerEntity.Crimes.Murder;
                player.SpawnCityGuards(true);
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

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Formulas;
using DaggerfallConnect;
using DaggerfallWorkshop.Utility;
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
    public class NpcBrain : MonoBehaviour, INpcVariableFacts
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
        internal Vector3 HomeLocal { get { return homeLocal; } }
        Vector3 wanderTargetLocal;
        bool hasWanderTarget;
        bool enemy;             // an enemy of the player now (enemies spec)
        bool provoked;          // the player hit it while it was an enemy
        bool pacifyRolled;      // the language pacification roll is made once per spawn, like vanilla
        float wanderPause;
        float calmCheckTimer;
        FleeRoll fleeRoll;      // Normal's one flee roll while badly hurt
        float holdStill;        // seconds the calm NPC stands still (greeting)
        NpcGait gait;
        float patrolTimer;
        NpcPackageDriver packages;
        NpcAnimal animal;       // animals only (spec animal-ai)
        bool animalStarted;     // an animal attacked the player on its own: killing it then is no crime
        NpcCompanionCombat companionCombat;
        NpcGuard guard;
        public NpcGuard Guard { get { return guard; } }
        public bool EnforcingLaw { get { return guard != null && guard.OwnsTarget; } }
        public NpcCompanionCombat CompanionCombat { get { return companionCombat; } }
        bool packageControlled;
        public bool PackageRunning { get { return mode == NpcMode.Calm && packages != null && packages.Running; } }
        public string PackageDescription { get { return packages != null ? packages.Description : "current AI (no schedule)"; } }
        internal void ReloadSchedule()
        {
            if (packages != null) packages.Dispose();
            packages = state.companion != null || (def.Schedule != null && def.Schedule.Count > 0) ? new NpcPackageDriver(this, mover) : null;
            packageControlled = false;
            hasWanderTarget = false;
        }
        bool INpcVariableFacts.TryGetVariable(string name, out NpcVariableValue value)
        { return NpcVariables.TryGet(def, state, name, out value); }
        public bool TryGetVariable(string name, out NpcVariableValue value)
        {
            if (!NpcVariables.TryGet(def, state, name, out value)) return false;
            value = value.Clone();
            return true;
        }
        public bool TrySetVariable(string name, NpcVariableValue value, out string error)
        { return NpcVariables.Set(def, state, name, value, out error); }
        public bool TryAddVariable(string name, int amount, out string error)
        { return NpcVariables.Add(def, state, name, amount, out error); }
        public bool SetVariable(string name, bool value) { return SetVariableValue(name, new NpcVariableValue(value)); }
        public bool SetVariable(string name, int value) { return SetVariableValue(name, new NpcVariableValue(value)); }
        bool SetVariableValue(string name, NpcVariableValue value)
        {
            string error;
            if (TrySetVariable(name, value, out error)) return true;
            AdvancedNpcsMod.LogError(key + ": " + error);
            return false;
        }
        public bool AddVariable(string name, int amount)
        {
            string error;
            if (TryAddVariable(name, amount, out error)) return true;
            AdvancedNpcsMod.LogError(key + ": " + error);
            return false;
        }
        public string DescribeVariables()
        {
            var lines = new List<string>();
            if (def != null)
                foreach (string name in def.Variables.Keys)
                {
                    NpcVariableValue value;
                    lines.Add(name + "=" + (TryGetVariable(name, out value) ? value.ToString() : "<incompatible saved type>"));
                }
            lines.Sort(System.StringComparer.Ordinal);
            return lines.Count == 0 ? "none" : string.Join(", ", lines.ToArray());
        }
        public Bravery EffectiveBravery { get { return def.Guard || (packages != null && packages.Guarding) ? Bravery.Brave : def.Bravery; } }
        public bool PackageWorkAtPost { get { return isActiveAndEnabled && mode == NpcMode.Calm && packages != null && packages.AtWorkPost; } }
        public bool PackageOnlyCombatInterrupts { get { return isActiveAndEnabled && mode == NpcMode.Calm && packages != null && packages.OnlyCombatInterrupts; } }
        public bool CivilianAnimation
        {
            get { return isActiveAndEnabled && packages != null && mode == NpcMode.Calm && holdStill <= 0 && !GameManager.IsGamePaused && packages.CivilianPaused; }
        }
        public PackageSettings MealAnimation
        {
            get { return isActiveAndEnabled && packages != null && mode == NpcMode.Calm && holdStill <= 0 && !GameManager.IsGamePaused ? packages.MealSettings : null; }
        }
        public PackageSettings GuardAnimation
        {
            get { return isActiveAndEnabled && packages != null && mode == NpcMode.Calm && holdStill <= 0 && !GameManager.IsGamePaused ? packages.GuardSettings : null; }
        }
        public string PackageAnimation
        {
            get
            {
                if (animal != null && isActiveAndEnabled && mode == NpcMode.Calm && !GameManager.IsGamePaused)
                {
                    string own = animal.Animation;     // alert, warning, resting, feeding
                    if (own != null)
                        return own;
                }
                return isActiveAndEnabled && packages != null && mode == NpcMode.Calm && (holdStill <= 0 || PackageOnlyCombatInterrupts) && (!GameManager.IsGamePaused || PackageOnlyCombatInterrupts) ? packages.Animation : null;
            }
        }
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
            brain.CaptureState();
            brain.gameObject.SetActive(false);
            NpcBrain current;
            if (brain.key != null && live.TryGetValue(brain.key, out current) && current == brain)
                live.Remove(brain.key);
            if (brain.packages != null) brain.packages.Dispose();
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

        /// <summary>A stand-in's "stayNear": metres it keeps to around its spot while calm (walks back after a fight); 0 = none.</summary>
        public float Leash;

        public NpcMode CurrentMode
        {
            get { return mode; }
        }

        /// <summary>An enemy of the player now (hostile attitude in its hours, or switched on the go).</summary>
        public bool IsEnemy
        {
            get { return enemy; }
        }

        /// <summary>
        /// An enemy of the player with no one to fight yet: DFU's AI has no target (none seen, or it gave up searching),
        /// so it stands where it is (spec conversations: hostile ANPCs talk among themselves until they spot someone).
        /// </summary>
        public bool IdleEnemy
        {
            get
            {
                return enemy && mode == NpcMode.Fighting && senses != null && motor != null &&
                       (senses.Target == null || motor.GiveUpTimer <= 0);
            }
        }

        /// <summary>Who it can greet and talk with now (GreetRules.Peaceful, IdleEnemy or Busy).</summary>
        public int ChatSide
        {
            get { return GreetRules.Side(mode == NpcMode.Calm, state != null && state.hostile, enemy, IdleEnemy); }
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
            state.pacified = false;
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

        /// <summary>
        /// Turns at once to face the player (the talk window pauses the game, so a slow turn would not show) and stays
        /// put while the window is open (calm movement would turn it back).
        /// </summary>
        public void FacePlayer()
        {
            GameObject player = GameManager.Instance.PlayerObject;
            if (player == null || mode != NpcMode.Calm)
                return;
            Vector3 to = player.transform.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(to);
            if (mover != null)
                mover.Stop();
            HoldStill(0.5f);    // counts down only while unpaused: covers the talk window, then a short beat
        }

        internal bool IsTurret
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
                if (guard != null) text += ", guard " + guard.Description;
                if (state != null && state.wantedCrime != 0) text += ", wanted for " + (PlayerEntity.Crimes)state.wantedCrime;
                return text;
            }
        }

        /// <summary>What an author wants to know about this ANPC (anpc_info, spec tools-quality §A1), one fact per line.</summary>
        public string Describe()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append(Id).Append(" (").Append(DisplayName).Append(")\n");
            if (def != null)
                sb.Append("  key ").Append(key).Append(", ").Append(def.Kind == NpcKind.Unique ? "unique" : "generic")
                  .Append(", folder ").Append(def.Folder).Append(", bravery ").Append(EffectiveBravery).Append('\n');
            sb.Append("  ").Append(Status).Append('\n');
            sb.Append("  variables: ").Append(DescribeVariables()).Append('\n');
            sb.Append("  package: ").Append(PackageDescription);
            if (packages != null && (mode != NpcMode.Calm || holdStill > 0 || (IsTurret && !packages.AllowsStationary) || GameManager.IsGamePaused))
                sb.Append(" (interrupted)");
            sb.Append('\n');
            sb.Append("  enemy: ").Append(enemy ? "yes" : "no");
            if (state != null && state.hostile)
            {
                if (state.hostileUntil == EnemyMarker)
                {
                    sb.Append(", hostile until the enemy rules end it");
                }
                else
                {
                    ulong now = Now();
                    double hours = state.hostileUntil > now ? (state.hostileUntil - now) / 3600.0 : 0.0;
                    sb.Append(", hostile for ").Append(hours.ToString("0.0")).Append(" more game hour(s)");
                }
            }
            sb.Append('\n');
            sb.Append("  opinion ").Append(Opinion).Append(", region's reaction with opinion: ")
              .Append(NpcGreeter.PlayerReactionBand(Opinion)).Append('\n');

            NpcSprite sprite = GetComponent<NpcSprite>();
            LoadedSpriteSet chosen = sprite != null ? sprite.ChosenSet : null;
            NpcCorpseSprite body = chosen == null && mode == NpcMode.Dead ? NpcCorpseSprite.Of(Id) : null;
            if (body != null && body.Set != null)
                sb.Append("  sprite: ").Append(body.Set.Label).Append(", shown by the corpse\n");
            else if (chosen == null)
                sb.Append("  sprite: vanilla (no sprite set)\n");
            else
                sb.Append("  sprite: ").Append(chosen.Label).Append(sprite.Pending ? ", loading" : ", loaded")
                  .Append(", ").Append(sprite.WorldHeight.ToString("0.00")).Append(" m tall")
                  .Append(sprite.CurrentAnimation != null ? ", playing " + sprite.CurrentAnimation : "").Append('\n');
            NpcVoice voice = GetComponent<NpcVoice>();
            LoadedSoundSet sounds = voice != null ? voice.Set : null;
            ModConfig config = AdvancedNpcsMod.Instance != null ? AdvancedNpcsMod.Instance.Config : null;
            if (sounds == null)
                sb.Append(config != null && config.VoiceMode == VoiceModes.Off ? "  sounds: none" : "  sounds: vanilla voice");
            else
                sb.Append("  sounds: ").Append(sounds.Label).Append(sounds.IsReady ? ", loaded: " : ", loading: ").Append(sounds.Summary());
            if (config != null && config.VoiceMode != VoiceModes.Anpc)
                sb.Append(" (ANPC sounds setting: ").Append(ModConfig.VoiceModeName(config.VoiceMode)).Append(')');
            if (voice != null)
                sb.Append("; steps: ").Append(voice.StepsSource);
            if (config != null && !config.Footsteps)
                sb.Append(" (footsteps off)");
            sb.Append('\n');
            NpcChat chat = GetComponent<NpcChat>();
            if (chat == null)
            {
                sb.Append("  conversation: off\n");
            }
            else
            {
                sb.Append("  conversation: chance ").Append(chat.Settings.Chance).Append("%, talk sounds ")
                  .Append(voice != null ? voice.TalkClips : 0).Append(", talk sheets ").Append(sprite != null ? sprite.TalkSheets : 0);
                string why = chat.Talking ? null : chat.WhyNot();
                if (chat.Talking && chat.Partner != null)
                    sb.Append(", talking with ").Append(chat.Partner.Brain.DisplayName).Append(", line ")
                      .Append(chat.Session.Line).Append(" of ").Append(chat.Session.Lines);
                else if (why != null)
                    sb.Append(", cannot talk now: ").Append(why);
                sb.Append('\n');
            }

            GameObject playerObject = GameManager.Instance.PlayerObject;
            float distance = playerObject != null ? Vector3.Distance(playerObject.transform.position, transform.position) : 0f;
            DaggerfallEntityBehaviour target = senses != null ? senses.Target : null;
            string targetName = target == null ? "none" : target == GameManager.Instance.PlayerEntityBehaviour ? "you" : target.name;
            sb.Append("  target: ").Append(targetName);
            if (target != null)
                sb.Append(senses.TargetInSight ? " (in sight)" : " (not in sight)");
            sb.Append(", ").Append(distance.ToString("0.0")).Append(" m from you\n");
            if (animal != null) sb.Append("  animal: ").Append(animal.Describe()).Append('\n');
            NpcDisease disease = GetComponent<NpcDisease>();
            if (disease != null) sb.Append("  disease: ").Append(def.Disease.Chance).Append("% per hit, ").Append(disease.Hits).Append(" hit(s), ")
                .Append(disease.Infections).Append(" passed on\n");
            if(companionCombat!=null && companionCombat.Active) sb.Append("  combat: ").Append(companionCombat.Description).Append('\n');
            if (state.companion != null) sb.Append("  companion: ").Append(state.companion.status).Append(", area ").Append(state.companion.area).Append('\n');
            sb.Append("  LOD: updates every ").Append(NpcLod.Interval(distance, false)).Append(" frame(s) when calm");
            return sb.ToString();
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
            if(companionCombat!=null) companionCombat.Reanchor();
            hasWanderTarget = false;
            if (packages != null || state.companion != null || (def.Schedule != null && def.Schedule.Count > 0))
            {
                if (packages != null) packages.Dispose();
                packages = new NpcPackageDriver(this, mover);
            }
            NpcGait g = GetComponent<NpcGait>();
            if (g != null)
                g.Repin();
        }

        public static void DespawnAll()
        {
            foreach (NpcBrain b in new List<NpcBrain>(live.Values))
            {
                if (b != null)
                    Discard(b);
            }
            live.Clear();
        }

        public void Init(NpcInstance npcInstance, NpcState npcState)
        {
            instance = npcInstance;
            def = npcInstance.Definition;
            key = npcInstance.Key;
            state = npcState;
            if (state.variables != null)
                foreach (var p in state.variables)
                {
                    NpcVariableValue initial;
                    if (def.Variables.TryGetValue(p.Key, out initial) && (p.Value == null || p.Value.isBoolean != initial.isBoolean))
                        AdvancedNpcsMod.LogError(key + ": saved variable " + p.Key + " has an incompatible type; conditions cannot match until corrected");
                }
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
            animal = GetComponent<NpcAnimal>();
            patrolTimer = Random.Range(0f, AggroRules.CheckInterval);
            if (state.companion == null)
                PlaceAmongPeople();
            Settled = true;
            homeLocal = transform.localPosition;
            if (state.companion != null || (def.Schedule != null && def.Schedule.Count > 0))
                packages = new NpcPackageDriver(this, mover);

            // No language pacification roll: it would turn a player's attack back off before Update sees it.
            senses.HasEncounteredPlayer = true;

            if (state.companion != null && state.companion.maxHealth > 0)
                entityBehaviour.Entity.MaxHealth = state.companion.maxHealth;
            int restored = HealthRules.Restore(entityBehaviour.Entity.MaxHealth, state.healthFraction);
            if (restored != entityBehaviour.Entity.CurrentHealth)
                entityBehaviour.Entity.CurrentHealth = restored;
            if (state.companion != null) NpcCompanionInventory.For(this);
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
            if(def.RecruitCost>=0 || state.companion!=null)
            { companionCombat=gameObject.AddComponent<NpcCompanionCombat>(); companionCombat.Init(this); }
            NpcMagic.EnsureCompanion(this);
            if (def.Guard) guard = new NpcGuard(this);
            FloatingOrigin.OnPositionUpdate += OnOriginShift;
        }

        /// <summary>
        /// DFU's floating origin moves each child of the streaming world on its own (FloatingOrigin.OffsetChildren), so the
        /// local position of a wilderness ANPC jumps by the offset: its home and wander target must jump with it, or it
        /// heads (or gives up a chase) 819 m away after the player crosses a map pixel.
        /// </summary>
        void OnOriginShift(Vector3 offset)
        {
            Transform parent = transform.parent;
            if (parent == null || GameManager.Instance.StreamingTarget == null || parent != GameManager.Instance.StreamingTarget.transform)
                return;
            Vector3 local = parent.InverseTransformVector(offset);
            homeLocal += local;
            wanderTargetLocal += local;
        }

        const float WallCheckSeconds = 1f;
        const float WallNear = 1.2f;
        const float WallTurnDegreesPerSecond = 240f;
        float wallCheck;
        Vector3 turnFromWall;
        NpcChat chat;

        /// <summary>
        /// A calm ANPC standing still with a wall right in front of it (it walked there, or was pushed) turns to the most
        /// open way. Work posts, package and guard poses, greetings and conversations keep their own facing.
        /// </summary>
        void TurnFromWall()
        {
            bool still = gait == null || gait.Current == Gait.Still;
            if (chat == null)
                chat = GetComponent<NpcChat>();
            if (!still || holdStill > 0f || (chat != null && chat.Talking) || PackageWorkAtPost || PackageAnimation != null ||
                GuardAnimation != null || MealAnimation != null || GameManager.IsGamePaused)
            {
                turnFromWall = Vector3.zero;
                return;
            }
            if (turnFromWall != Vector3.zero)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(turnFromWall),
                    WallTurnDegreesPerSecond * Time.deltaTime);
                if (Vector3.Angle(transform.forward, turnFromWall) < 1f)
                    turnFromWall = Vector3.zero;
                return;
            }
            wallCheck -= Time.deltaTime;
            if (wallCheck > 0f)
                return;
            wallCheck = WallCheckSeconds;
            if (SpotFinder.WallAhead(transform.position, transform.forward, WallNear))
                turnFromWall = SpotFinder.OpenDirection(transform.position);
        }

        /// <summary>Has taken its spawn spot (moved off anyone it was spawned inside); others then step around it.</summary>
        public bool Settled { get; private set; }

        /// <summary>
        /// Spawned inside another person (a vanilla flat NPC or an ANPC on the same marker): steps to a free spot close by,
        /// which becomes its home. Indoors it then faces the most open way, into the room rather than at a wall.
        /// </summary>
        void PlaceAmongPeople()
        {
            CharacterController cc = GetComponent<CharacterController>();
            float lift = cc != null ? cc.height * 0.52f : 1f;     // GameObjectHelper.AlignControllerToGround
            Vector3 body = transform.position;
            Vector3 feet;
            if (cc != null && SpotFinder.Crowded(body, this) && SpotFinder.Uncrowded(body, this, lift, out feet))
            {
                bool wasEnabled = cc.enabled;
                cc.enabled = false;
                transform.position = feet + Vector3.up * lift;
                cc.enabled = wasEnabled;
                if (gait != null)
                    gait.Repin();
                AdvancedNpcsMod.Log(key + ": spawned inside someone; stepped " + Vector3.Distance(body, transform.position).ToString("0.0") + " m aside.");
            }
            if (transform.parent == null || (transform.parent.GetComponent<DaggerfallInterior>() == null && transform.parent.GetComponent<DaggerfallDungeon>() == null))
                return;
            Vector3 open = SpotFinder.OpenDirection(transform.position);
            if (open != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(open);
        }

        // Companion policy owns permission and pursuit; the existing state machine owns component modes.
        public void CompanionMode(DaggerfallEntityBehaviour target, NpcMode wanted)
        { threat=target; if(mode!=wanted) SetMode(wanted); }
        public void CompanionBetrayed() { OnAttackedByPlayer(); }
        public bool CompanionWouldFlee()
        { return HostilityRules.Decide(EffectiveBravery, HealthFraction(), def.FleeHealthPercent, def.FleeChance, ref fleeRoll, rng.NextDouble())==CombatChoice.Flee; }

        public void CaptureState()
        {
            if (entityBehaviour == null || entityBehaviour.Entity == null) return;
            if (mode != NpcMode.Dead)
                state.healthFraction = HealthRules.Fraction(entityBehaviour.Entity.CurrentHealth, entityBehaviour.Entity.MaxHealth);
            if (state.companion != null) { state.companion.maxHealth = entityBehaviour.Entity.MaxHealth; if (!state.dead) NpcCompanionInventory.For(this).Capture(); }
            NpcMagic magic = GetComponent<NpcMagic>();
            if (magic != null) magic.Capture();
        }

        void OnDisable() { CaptureState(); if (guard != null) guard.Release(); if (packages != null) packages.Suspend(); }

        void OnDestroy()
        {
            FloatingOrigin.OnPositionUpdate -= OnOriginShift;
            if (guard != null) guard.Release();
            if (packages != null) packages.Dispose();
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

            if (packages != null)
            {
                Bravery before = EffectiveBravery;
                packages.Pause();
                packages.Select(AdvancedNpcsMod.Instance != null ? AdvancedNpcsMod.Instance.Packages : null, Now());
                if (before != EffectiveBravery) fleeRoll = new FleeRoll();
            }
            DaggerfallEntityBehaviour player = GameManager.Instance.PlayerEntityBehaviour;
            int health = entityBehaviour.Entity.CurrentHealth;
            bool healthDropped = health < lastHealth;
            lastHealth = health;
            state.healthFraction = HealthRules.Fraction(health, entityBehaviour.Entity.MaxHealth);

            if(companionCombat!=null && companionCombat.Active)
            {
                bool controlled=companionCombat.Tick();
                if(companionCombat.Active) { if(!controlled) UpdateCalm(); return; }
            }

            if (guard != null && guard.OwnsTarget && guard.Tick()) return;

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
                OnAttackedByCreature(senses.Target, true);
            }

            if (guard != null && guard.Tick()) return;

            // Our own language pacification roll (DFU's is off, see Start): hostile-attitude enemies only, first sighting.
            // Not during the self-test: a random calm-down would break its enemy checks (it tests Pacify directly).
            if (enemy && !pacifyRolled && !SelfTest.Running && !provoked && def.Hostile && !state.enemyOn && senses.Target == player && senses.DetectedTarget)
                TryPacify();

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
                        OnAttackedByCreature(senses.Target, true);
                        break;
                    }
                    motor.GiveUpTimer = 0;
                    if (EffectiveBravery == Bravery.Brave && Patrol())
                        break;
                    UpdateCalm();
                    TurnFromWall();
                    break;
                case NpcMode.Fighting:
                    UpdateFighting();
                    break;
                case NpcMode.Fleeing:
                    UpdateFleeing();
                    break;
            }
        }

        /// <summary>DFU's language pacification roll for an enemy that has just seen the player.</summary>
        void TryPacify()
        {
            pacifyRolled = true;
            EnemyEntity entity = entityBehaviour.Entity as EnemyEntity;
            DFCareer.Skills skill = entity != null ? entity.GetLanguageSkill() : DFCareer.Skills.None;
            if (skill == DFCareer.Skills.None)
                return;
            PlayerEntity player = GameManager.Instance.PlayerEntity;
            if (FormulaHelper.CalculateEnemyPacification(player, skill))
            {
                Pacify(skill);
                player.TallySkill(skill, 3);    // as vanilla (BCHG)
            }
            else if (skill != DFCareer.Skills.Etiquette && skill != DFCareer.Skills.Streetwise)
            {
                player.TallySkill(skill, 1);
            }
        }

        /// <summary>Calmed for good by the player's words (saved); attacking it later makes it an enemy again.</summary>
        internal void Pacify(DFCareer.Skills skill)
        {
            pacifyRolled = true;
            SwitchHostile(false);
            state.pacified = true;
            if (senses.Target == GameManager.Instance.PlayerEntityBehaviour)
                senses.Target = null;
            DaggerfallUI.AddHUDText(TextManager.Instance.GetLocalizedText("languagePacified").Replace("%e", DisplayName)
                .Replace("%s", DaggerfallUnity.Instance.TextProvider.GetSkillName(skill)), 5);
            AdvancedNpcsMod.Log(key + ": pacified with " + skill + ".");
        }

        void OnAttackedByPlayer()
        {
            if (state.pacified && state.companion == null)
            {
                // Betrayed after a pacification: an enemy again, as vanilla, so no crime and no second roll.
                state.pacified = false;
                SwitchHostile(true);
                provoked = true;
                AdvancedNpcsMod.Log(key + ": attacked after being pacified; an enemy again.");
                return;
            }
            bool wasCalm = !state.hostile;
            state.hostile = true;
            state.hostileUntil = HostilityRules.NewCalmDeadline(Now(), def.CalmDownMinHours, def.CalmDownMaxHours, rng);
            threat = GameManager.Instance.PlayerEntityBehaviour;

            if (wasCalm)
            {
                ChangeOpinion(OpinionRules.Assault);
                // Nobody minds a hunter shooting a wolf; a farmer minds someone beating his cow (livestock: crimeOnAttack).
                if (def.Animal == null || def.CrimeOnAttack)
                    WitnessPlayerViolence(transform.position, this);
                if (!enemy && state.companion == null)
                    RallyAllies(new AllyInfo(this), threat);
            }
            if (wasCalm && def.CrimeOnAttack && def.Animal == null)
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
            OnAttackedByCreature(foe, false);
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

        void OnAttackedByCreature(DaggerfallEntityBehaviour attacker, bool attacked)
        {
            if (mode != NpcMode.Calm)
                return;
            threat = attacker;
            EnterCombat();
            if (attacked && !enemy && state.companion == null)
                RallyAllies(new AllyInfo(this), attacker);
        }

        /// <summary>Who was attacked, kept by value: a victim killed outright is gone by the time its killer is known.</summary>
        struct AllyInfo
        {
            public string Key;
            public Vector3 At;
            public Transform Parent;
            public int Group;
            public int Faction;

            public AllyInfo(NpcBrain b)
            {
                Key = b.key;
                At = b.transform.position;
                Parent = b.transform.parent;
                Group = b.instance != null ? b.instance.Group : -1;
                Faction = b.StandInFaction;
            }
        }

        /// <summary>The vanilla faction of the flat NPC this ANPC stands in for, 0 if none.</summary>
        int StandInFaction
        {
            get
            {
                NpcTalk talk = GetComponent<NpcTalk>();
                return talk != null && talk.StandIn != null ? talk.StandIn.Data.factionID : 0;
            }
        }

        /// <summary>
        /// Calm allies who see or hear an ANPC attacked take its side: all but cowards fight the attacker (bravery and
        /// health decide as if attacked themselves), cowards run from it. Companions, guards and enemies stay out of it.
        /// </summary>
        static void RallyAllies(AllyInfo victim, DaggerfallEntityBehaviour attacker)
        {
            if (attacker == null)
                return;
            bool inBuilding = victim.Parent != null && victim.Parent.GetComponent<DaggerfallInterior>() != null;
            DaggerfallEntityBehaviour player = GameManager.Instance.PlayerEntityBehaviour;
            foreach (NpcBrain a in All())
            {
                if (a.key == victim.Key || !a.isActiveAndEnabled || a.mode != NpcMode.Calm || a.enemy || a.state == null || a.state.hostile ||
                    a.state.companion != null || a.guard != null || a.entityBehaviour == null || a.entityBehaviour == attacker)
                    continue;
                if (!HostilityRules.Allied(a.transform.parent == victim.Parent, inBuilding, victim.Group, a.instance != null ? a.instance.Group : -1,
                    victim.Faction, a.StandInFaction))
                    continue;
                float d = Vector3.Distance(a.transform.position, victim.At);
                if (d > HostilityRules.AllyRange || (d > HostilityRules.AllyHearRange && !a.CanSeePoint(victim.At) && !a.CanSee(attacker)))
                    continue;
                if (attacker == player)
                {
                    a.state.hostile = true;
                    a.state.hostileUntil = HostilityRules.NewCalmDeadline(Now(), a.def.CalmDownMinHours, a.def.CalmDownMaxHours, a.rng);
                }
                a.threat = attacker;
                a.EnterCombat();
                AdvancedNpcsMod.Log(a.key + ": takes the side of " + victim.Key + " against " + attacker.name +
                    (a.mode == NpcMode.Fleeing ? " (flees)." : "."));
            }
        }

        void EnterCombat()
        {
            CombatChoice choice = HostilityRules.Decide(EffectiveBravery, HealthFraction(), def.FleeHealthPercent, def.FleeChance, ref fleeRoll, rng.NextDouble());
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
                if (state.companion != null) NpcCompanionInventory.For(this);
            lastHealth = entityBehaviour.Entity.CurrentHealth;
                state.healthFraction = 1f;
                AdvancedNpcsMod.Log(key + ": calmed down.");
                if (threat == GameManager.Instance.PlayerEntityBehaviour)
                    SetMode(NpcMode.Calm);
            }
        }

        void UpdateCalm()
        {
            if (packages != null && GameManager.IsGamePaused)
            {
                // Freeze, not reset: catch-up clocks continue after a menu closes.
                mover.Stop();
                return;
            }
            if (animal != null && animal.ControlsCalm(mover))
                return;
            if (holdStill > 0f)
            {
                holdStill = Mathf.Max(0f, holdStill - Time.deltaTime);
                if (!PackageOnlyCombatInterrupts) { if (packages != null) packages.InterruptFollow(); mover.Stop(); return; }
            }
            if (packages != null && (!IsTurret || packages.AllowsStationary))
            {
                bool controlled = packages.Tick(homeLocal, Now(), Time.deltaTime);
                if (controlled != packageControlled)
                    hasWanderTarget = false;
                packageControlled = controlled;
                if (controlled) return;
            }
            if (IsTurret) { mover.Stop(); return; }
            float wanderRadius = Mathf.Min(def.WanderRadius, WanderCap);
            if (Leash > 0f)
            {
                wanderRadius = Mathf.Min(wanderRadius, Leash);
                Vector3 homeWorld = transform.parent != null ? transform.parent.TransformPoint(homeLocal) : homeLocal;
                Vector3 away = homeWorld - transform.position;
                away.y = 0f;
                if (away.magnitude > Leash + 0.5f)
                {
                    // Pushed or chased out of its area: walk straight back.
                    wanderTargetLocal = homeLocal;
                    hasWanderTarget = true;
                }
            }
            if (wanderRadius <= 0f && !hasWanderTarget)
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
            if (HostilityRules.Decide(EffectiveBravery, HealthFraction(), def.FleeHealthPercent, def.FleeChance, ref fleeRoll, rng.NextDouble()) == CombatChoice.Flee)
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
            if (EffectiveBravery == Bravery.Brave)
            {
                EnterCombat();
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
            if (packages != null && newMode != NpcMode.Calm) packages.Pause();
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
            // Clear lawful hostility before DFU supplies the killing blow's actual attacker.
            if (guard != null) guard.Release();
            if (packages != null) packages.Dispose();
            state.dead = true;
            // Native EnemyDeath transfers the live items to its corpse. The contract no longer owns them.
            if (state.companion != null && GetComponent<NpcCompanionInventory>() != null && GetComponent<NpcCompanionInventory>().Ready)
            {
                // Native corpses clear Head..Feet only; companion rings/amulets/gems can also be worn.
                CompanionEquipment.StopAll(entity);state.companion.belongings=null;
            }
            state.hostile = false;
            NpcVoice voice = GetComponent<NpcVoice>();
            if (voice != null)
                voice.OnDied();

            // Custom sprites: the death sheet plays and the body stays as the corpse (spec 1b §6.4).
            NpcSprite sprite = GetComponent<NpcSprite>();
            if (sprite != null && sprite.Set != null && NpcCorpseSprite.Spawn(sprite, entityBehaviour) != null)
                sprite.Hide();

            // DFU tells the victim who hit it only after OnDeath; decide the killer once that has happened.
            AdvancedNpcsMod.Instance.StartCoroutine(ResolveKiller(def, key, motor, wasHostile, fightingCreature, wasEnemy, transform.position,
                new AllyInfo(this), state.companion == null, animalStarted));
        }

        static IEnumerator ResolveKiller(NpcDefinition def, string key, EnemyMotor motor, bool wasHostile, bool fightingCreature, bool wasEnemy,
            Vector3 diedAt, AllyInfo victim, bool rally, bool animalAttackedFirst)
        {
            yield return null;

            bool motorHostile = motor != null && motor.IsHostile;
            bool byPlayer = HostilityRules.KilledByPlayer(wasHostile, fightingCreature, motorHostile);
            if (byPlayer && def.CrimeOnAttack && !wasEnemy && def.Animal == null)
            {
                NpcCrime.Report(PlayerEntity.Crimes.Murder);
            }
            else if (byPlayer && def.CrimeOnAttack && def.Animal != null && !animalAttackedFirst)
            {
                NpcCrime.Report(PlayerEntity.Crimes.Theft);     // someone's livestock (spec animal-ai: build notes)
            }
            AdvancedNpcsMod.Log(key + ": died" + (byPlayer ? " (player)." : " (creature)."));
            if (byPlayer && !wasEnemy && (def.Animal == null || def.CrimeOnAttack))
                WitnessPlayerViolence(diedAt, null);
            if (byPlayer && !wasEnemy && !wasHostile && rally)
                RallyAllies(victim, GameManager.Instance.PlayerEntityBehaviour);     // killed in one blow: never fought back
        }

        /// <summary>An animal runs from a threat (the player, a hunting predator) without being angry at it.</summary>
        internal void AnimalFlee(DaggerfallEntityBehaviour from)
        {
            if (mode == NpcMode.Dead || from == null)
                return;
            threat = from;
            SetMode(NpcMode.Fleeing);
        }

        /// <summary>An animal attacks the player on its own (spec animal-ai §2.1); its NpcAnimal ends the chase.</summary>
        internal void AnimalAttackPlayer()
        {
            if (mode == NpcMode.Dead)
                return;
            DaggerfallEntityBehaviour player = GameManager.Instance.PlayerEntityBehaviour;
            if (!state.hostile)
                animalStarted = true;
            state.hostile = true;
            state.hostileUntil = HostilityRules.NewCalmDeadline(Now(), def.CalmDownMinHours, def.CalmDownMaxHours, rng);
            threat = player;
            senses.Target = player;
            motor.IsHostile = true;
            SetMode(NpcMode.Fighting);
        }

        /// <summary>A hungry predator attacks a prey animal (spec animal-ai §3).</summary>
        internal void AnimalHunt(DaggerfallEntityBehaviour prey)
        {
            if (mode == NpcMode.Dead || prey == null)
                return;
            senses.Target = prey;
            threat = prey;
            SetMode(NpcMode.Fighting);
        }

        /// <summary>An animal calms down at once (a flight is over, a chase given up); health is not restored.</summary>
        internal void AnimalCalm()
        {
            if (mode == NpcMode.Dead)
                return;
            animalStarted = false;
            state.hostile = false;
            motor.IsHostile = false;
            SetMode(NpcMode.Calm);
        }

        /// <summary>Moves the centre it wanders around (herd followers keep near their leader). Not saved.</summary>
        internal void SetHome(Vector3 local)
        {
            homeLocal = local;
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

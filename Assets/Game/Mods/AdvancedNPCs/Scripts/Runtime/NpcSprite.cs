using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>
    /// Draws an ANPC's own sprite set instead of the vanilla class sprite (spec 1b §6). DFU's billboard keeps running
    /// with its renderer off, so combat timing, sounds and AI stay vanilla; this quad follows its state.
    /// </summary>
    public class NpcSprite : MonoBehaviour
    {
        const float WalkSpeed = 0.15f;      // units per second above which the NPC counts as moving
        const int NoBlow = 0;
        const int MeleeBlow = 1;
        const int ArrowBlow = 2;

        LoadedSpriteSet set;
        float worldPerPixel;
        float heightScale = 1f;
        MobileUnit mobile;
        MeshRenderer vanilla;
        CharacterController controller;
        DaggerfallEntityBehaviour entityBehaviour;
        GameObject quad;
        MeshRenderer quadRenderer;
        NpcGait gait;
        NpcBrain brain;
        NpcVoice voice;
        NpcRanged ranged;       // own ranged attack (creature throws, projectile images): fired here on the action frame
        LoadedSpriteSet waitingSet;     // chosen, still loading: nothing drawn yet
        float waitingHeight;
        float waitingScale = 1f;
        LoadedSpriteSet heldSet;        // chosen by Begin; acquired in Start, released when this sprite goes
        bool acquired;
        int lodPhase;           // spreads far ANPCs' updates over frames (spec opinion-lod §B)
        float lodDt;            // time since the last update that ran
        int updatesRun;
        Material material;

        string state;
        SpriteAnimation anim;
        float time;
        int frame = -1;
        int row;
        int frameChanges;
        Vector3 lastPosition;
        float speed;
        int lastHealth = -1;
        float oneShotLeft;      // seconds a hit (or attack) sheet keeps playing whatever DFU's own state says
        float talkLeft;         // seconds the speaker in a conversation keeps its talk sheet (NpcChat)
        int greetSide;          // its GreetRules side when the greeting began (a change ends the wave)
        int hitStarts;
        int greetStarts;
        MobileStates lastMobileState;
        int pendingBlow;        // MeleeBlow or ArrowBlow still to land on the attack sheet's action frame
        int sentBlow;           // the blow signalled last frame, for counting what DFU took
        int vanillaToHold;      // DFU blow signals still to hold back: one per swing whose blow is ours
        bool hitWaiting;        // hit during a swing: its sheet plays when the attack sheet ends
        bool paralysed;         // read in LateUpdate: DFU clears paralysis each frame and the effect sets it again
        int blowsLanded;
        int blowsDelivered;
        int vanillaBlowsHeld;

        /// <summary>npc.json "combat.rangedAnimation": the sheet a bow shot or throw plays; null or missing picks attack_range*, else attack*.</summary>
        public string RangedSheet;

        public LoadedSpriteSet Set
        {
            get { return set; }
        }

        /// <summary>How many frame updates actually ran (self-test: LOD skips some for far ANPCs).</summary>
        public int UpdatesRun
        {
            get { return updatesRun; }
        }

        /// <summary>Playing a one-shot sheet, running, or in an attack: updated every frame whatever the distance.</summary>
        bool Busy
        {
            get
            {
                return oneShotLeft > 0 || pendingBlow != NoBlow || (gait != null && gait.Current == Core.Gait.Run) ||
                       (mobile != null && IsAttack(mobile.EnemyState)) ||
                       (brain != null && (brain.CurrentMode == NpcMode.Fighting || brain.CurrentMode == NpcMode.Fleeing));
            }
        }

        float DistanceToPlayer()
        {
            GameObject player = GameManager.Instance != null ? GameManager.Instance.PlayerObject : null;
            return player != null ? Vector3.Distance(player.transform.position, transform.position) : 0f;
        }

        /// <summary>The set this person wears, loaded or still loading.</summary>
        public LoadedSpriteSet ChosenSet
        {
            get { return set != null ? set : waitingSet; }
        }

        /// <summary>Waiting for its set to load (invisible meanwhile).</summary>
        public bool Pending
        {
            get { return waitingSet != null; }
        }

        /// <summary>Spawner entry: shows the set now if it is loaded, else waits for it with the vanilla picture hidden.</summary>
        public void Begin(LoadedSpriteSet loaded, float worldHeight, float verticalScale = 1f)
        {
            heldSet = loaded;
            if (loaded.IsReady)
            {
                Init(loaded, worldHeight, verticalScale);
                return;
            }
            waitingSet = loaded;
            waitingHeight = worldHeight;
            waitingScale = verticalScale;
            DaggerfallEnemy enemy = GetComponent<DaggerfallEnemy>();
            MobileUnit unit = enemy != null ? enemy.MobileUnit : null;
            vanilla = unit != null ? unit.GetComponent<MeshRenderer>() : null;
            if (vanilla != null)
                vanilla.forceRenderingOff = true;
        }

        public float WorldPerPixel
        {
            get { return worldPerPixel; }
        }

        /// <summary>Standing height in metres (the spawner's height; set while the set still loads).</summary>
        public float WorldHeight
        {
            get { return set != null ? worldPerPixel * set.StandingHeightPx : waitingHeight; }
        }

        /// <summary>Vertical-only scale shared with corpse rendering.</summary>
        public float HeightScale
        {
            get { return heightScale; }
        }

        public string CurrentState
        {
            get { return state; }
        }

        public string CurrentAnimation
        {
            get { return anim != null ? anim.Name : null; }
        }

        public int CurrentRow
        {
            get { return row; }
        }

        public int CurrentFrame
        {
            get { return frame; }
        }

        /// <summary>How often the shown frame has changed (self-test).</summary>
        public int FrameChanges
        {
            get { return frameChanges; }
        }

        /// <summary>How often a hit sheet has started (self-test).</summary>
        public int HitStarts
        {
            get { return hitStarts; }
        }

        /// <summary>How often a greet sheet has started (self-test).</summary>
        public int GreetStarts
        {
            get { return greetStarts; }
        }

        /// <summary>Blows started on an attack sheet's action frame (self-test).</summary>
        public int BlowsLanded
        {
            get { return blowsLanded; }
        }

        /// <summary>An attack sheet's blow is still waiting for its action frame (self-test).</summary>
        public bool BlowPending
        {
            get { return pendingBlow != NoBlow; }
        }

        /// <summary>Those of the blows DFU's attack code has taken (self-test).</summary>
        public int BlowsDelivered
        {
            get { return blowsDelivered; }
        }

        /// <summary>DFU's own blow signals held back because the attack sheet has an action frame (self-test).</summary>
        public int VanillaBlowsHeld
        {
            get { return vanillaBlowsHeld; }
        }

        public bool Showing
        {
            get { return quadRenderer != null && quadRenderer.enabled; }
        }

        public bool VanillaHidden
        {
            get { return vanilla == null || vanilla.forceRenderingOff; }
        }

        /// <summary>The material the quad uses (copied for the corpse).</summary>
        public Material MaterialTemplate
        {
            get { return material; }
        }

        /// <summary>The ground point under the NPC.</summary>
        public Vector3 Feet
        {
            get
            {
                Vector3 feet = transform.position;
                if (controller != null)
                    feet.y = controller.bounds.min.y;
                return feet;
            }
        }

        /// <summary>
        /// Wears another set (anpc_reload_sprites), at the same world height: shown once loaded, the vanilla picture hidden
        /// meanwhile. Null shows the vanilla sprite.
        /// </summary>
        public void Swap(LoadedSpriteSet next)
        {
            float height = WorldHeight;
            float scale = set != null ? heightScale : waitingScale;
            bool started = acquired;
            if (acquired)
            {
                heldSet.Release();
                acquired = false;
            }
            if (quad != null)
                Destroy(quad);
            if (material != null)
                Destroy(material);
            quad = null;
            quadRenderer = null;
            material = null;
            set = null;
            waitingSet = null;
            anim = null;
            state = null;
            oneShotLeft = 0f;
            pendingBlow = NoBlow;
            vanillaToHold = 0;
            hitWaiting = false;
            heldSet = next;
            if (next == null)
            {
                if (vanilla != null)
                    vanilla.forceRenderingOff = false;
                return;
            }
            if (started)
            {
                next.Acquire();     // Start already ran: hold the new set now (else Start does)
                acquired = true;
            }
            Begin(next, height, scale);
        }

        /// <summary>Stops drawing and lets go of the set (the corpse sprite takes over and holds its own).</summary>
        public void Hide()
        {
            if (quadRenderer != null)
                quadRenderer.enabled = false;
            set = null;
            waitingSet = null;
            if (acquired)
            {
                heldSet.Release();
                acquired = false;
            }
            heldSet = null;
        }

        static Mesh billboardMesh;
        static readonly string[] ExtraMaps = { "_EmissionMap", "_BumpMap", "_MetallicGlossMap", "_ParallaxMap" };
        static readonly string[] ExtraMapKeywords = { "_EMISSION", "_NORMALMAP", "_METALLICGLOSSMAP", "_PARALLAXMAP" };

        /// <summary>
        /// A camera-facing quad (its -Z side) lit like DFU's own billboards: every normal points 45 degrees up toward the
        /// camera (DaggerfallMobileUnit uses up + forward), so sprites brighten and darken with the sun like vanilla ones.
        /// </summary>
        public static GameObject CreateQuad(Transform parent, string name)
        {
            if (billboardMesh == null)
            {
                billboardMesh = new Mesh();
                billboardMesh.name = "AnpcBillboard";
                billboardMesh.vertices = new Vector3[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(-0.5f, 0.5f, 0), new Vector3(0.5f, 0.5f, 0) };
                billboardMesh.uv = new Vector2[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
                billboardMesh.triangles = new int[] { 0, 3, 1, 3, 0, 2 };
                Vector3 n = new Vector3(0, 1, -1).normalized;
                billboardMesh.normals = new Vector3[] { n, n, n, n };
                billboardMesh.RecalculateBounds();
            }
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = billboardMesh;
            go.AddComponent<MeshRenderer>();
            return go;
        }

        /// <summary>The texture with DFU's global filter setting (Point, Bilinear or Trilinear), as vanilla billboards use.</summary>
        public static Texture2D Filtered(Texture2D texture)
        {
            if (texture != null && DaggerfallUnity.Instance != null && DaggerfallUnity.Instance.MaterialReader != null)
                texture.filterMode = DaggerfallUnity.Instance.MaterialReader.MainFilterMode;
            return texture;
        }

        /// <summary>The vanilla class sprite's height: the default world height of a custom sprite (spec 1b §5.2).</summary>
        public static float DefaultHeight(GameObject npc)
        {
            DaggerfallEnemy enemy = npc.GetComponent<DaggerfallEnemy>();
            MobileUnit unit = enemy != null ? enemy.MobileUnit : null;
            return unit != null ? unit.GetSize().y : 2f;
        }

        public void Init(LoadedSpriteSet loaded, float worldHeight, float verticalScale = 1f)
        {
            set = loaded;
            heightScale = verticalScale;
            worldPerPixel = worldHeight / Mathf.Max(1f, loaded.StandingHeightPx);
            DaggerfallEnemy enemy = GetComponent<DaggerfallEnemy>();
            mobile = enemy != null ? enemy.MobileUnit : null;
            vanilla = mobile != null ? mobile.GetComponent<MeshRenderer>() : null;
            controller = GetComponent<CharacterController>();
            entityBehaviour = GetComponent<DaggerfallEntityBehaviour>();
            if (gait == null)
                gait = GetComponent<NpcGait>();
            brain = GetComponent<NpcBrain>();
            voice = GetComponent<NpcVoice>();
            ranged = GetComponent<NpcRanged>();

            quad = CreateQuad(transform, "AnpcSprite");
            quadRenderer = quad.GetComponent<MeshRenderer>();
            // A copy of the vanilla billboard material keeps DFU's shader, lighting and fog.
            material = vanilla != null && vanilla.sharedMaterial != null ? new Material(vanilla.sharedMaterial) : new Material(Shader.Find("Sprites/Default"));
            // Texture-replacement packs add emission/normal maps of the vanilla picture to that material: not ours.
            foreach (string map in ExtraMaps)
            {
                if (material.HasProperty(map))
                    material.SetTexture(map, null);
            }
            foreach (string keyword in ExtraMapKeywords)
                material.DisableKeyword(keyword);
            quadRenderer.sharedMaterial = material;
            if (vanilla != null)
            {
                quadRenderer.shadowCastingMode = vanilla.shadowCastingMode;
                quadRenderer.receiveShadows = vanilla.receiveShadows;
                // Not "enabled = false": DFU's EntityConcealmentBehaviour sets enabled every frame for invisibility.
                vanilla.forceRenderingOff = true;
            }
            lastPosition = transform.localPosition;
            SetState(SpriteStates.Idle);
            Draw();
        }

        /// <summary>
        /// Holds the set only once the person is really in the world: Unity never calls Start or OnDestroy on an
        /// object that was never active (ANPCs spawned into a switched-off town), so acquiring in Begin would leak.
        /// </summary>
        void Start()
        {
            // The spawner adds the voice after the sprite: a set already loaded ran Init before it existed.
            if (voice == null)
                voice = GetComponent<NpcVoice>();
            if (ranged == null)
                ranged = GetComponent<NpcRanged>();
            if (heldSet == null || acquired)
                return;
            heldSet.Acquire();
            acquired = true;
            lodPhase = Mathf.Abs(GetInstanceID()) % NpcLod.Phases;
            if (set != null && !set.IsReady)
            {
                // Inactive long enough for its set to unload: wait for it again.
                waitingSet = set;
                set = null;
                if (quad != null)
                    Destroy(quad);
                if (material != null)
                    Destroy(material);
                quad = null;
                quadRenderer = null;
                material = null;
            }
        }

        void Update()
        {
            if (waitingSet != null)
            {
                if (waitingSet.IsReady)
                {
                    LoadedSpriteSet ready = waitingSet;
                    waitingSet = null;
                    Init(ready, waitingHeight, waitingScale);
                }
                else if (waitingSet.Failed)
                {
                    waitingSet = null;
                    if (vanilla != null)
                        vanilla.forceRenderingOff = false;
                }
                else if (vanilla != null)
                {
                    vanilla.forceRenderingOff = true;
                }
                return;
            }
            if (set == null)
                return;
            lodDt += Time.deltaTime;
            if (!NpcLod.Due(Time.frameCount, lodPhase, NpcLod.Interval(DistanceToPlayer(), Busy)))
                return;
            float dt = lodDt;
            lodDt = 0f;
            updatesRun++;
            // Relative to the town: DFU's floating-origin shifts move the whole town, which is not walking.
            Vector3 p = transform.localPosition;
            Vector3 moved = p - lastPosition;
            moved.y = 0;
            lastPosition = p;
            if (dt > 0)
                speed = Mathf.Lerp(speed, moved.magnitude / dt, 0.5f);
            // Paralysis stops the picture (DFU's own FreezeAnims is switched straight back off by EnemyMotor). A hit taken
            // meanwhile shows once it wears off.
            if (paralysed)
                return;

            // DFU only shows its hurt state on a hard knockback and never restarts it, so every health drop
            // plays a hit sheet from its first frame, to its end. A swing is not interrupted (no stun-lock, as in
            // vanilla): the hit sheet follows the attack sheet.
            int health = entityBehaviour != null && entityBehaviour.Entity != null ? entityBehaviour.Entity.CurrentHealth : -1;
            if (lastHealth >= 0 && health >= 0 && health < lastHealth && health > 0)
            {
                if ((state == SpriteStates.Attack || state == SpriteStates.Cast) && oneShotLeft > 0)
                    hitWaiting = true;
                else
                    StartOneShot(SpriteStates.Hit);
            }
            lastHealth = health;

            // Each attack DFU starts plays an attack sheet from its first frame, to its end.
            MobileStates s = mobile != null ? mobile.EnemyState : MobileStates.Idle;
            if (IsAttack(s) && s != lastMobileState)
                StartAttack(s);
            else if (!IsAttack(s))
                vanillaToHold = 0;      // DFU's swing is over: none of its signals can still come
            lastMobileState = s;

            if (oneShotLeft > 0)
                oneShotLeft -= dt;
            if (talkLeft > 0)
                talkLeft -= dt;
            if (state == SpriteStates.Greet && oneShotLeft > 0 && brain != null && brain.ChatSide != greetSide)
                oneShotLeft = 0;        // turned hostile, or an enemy spotted someone, mid-greeting: no wave in a fight
            if (oneShotLeft <= 0)
            {
                if (hitWaiting)
                {
                    hitWaiting = false;
                    StartOneShot(SpriteStates.Hit);
                }
                else
                {
                    SetState(WantedState());
                }
            }
            time += dt;
        }

        void LateUpdate()
        {
            if (set == null)
                return;
            if (vanilla != null)
                vanilla.forceRenderingOff = true;
            // Invisibility (Chameleon, Shadow, Invisibility spells) hides this sprite as it would the vanilla one.
            quadRenderer.enabled = entityBehaviour == null || entityBehaviour.Entity == null || !entityBehaviour.Entity.IsMagicallyConcealed;
            paralysed = entityBehaviour != null && entityBehaviour.Entity != null && entityBehaviour.Entity.IsParalyzed;
            Draw();
            TimeBlows();
        }

        static bool IsAttack(MobileStates s)
        {
            return s == MobileStates.PrimaryAttack || s == MobileStates.RangedAttack1 || s == MobileStates.RangedAttack2 || s == MobileStates.Spell;
        }

        /// <summary>Plays a cast sheet for a class without DFU spell frames, whose mobile state never shows Spell.</summary>
        public void PlayCast()
        {
            if (set != null)
                StartAttack(MobileStates.Spell);
        }

        /// <summary>The gait that chooses run, walk or idle (the spawner sets it before Init).</summary>
        public void UseGait(NpcGait source)
        {
            gait = source;
        }

        /// <summary>Plays a greet sheet once; false when the set has none or an attack, cast or hit sheet is playing.</summary>
        public bool PlayGreet()
        {
            if (set == null || SpriteStates.Variants(set.Set, SpriteStates.Greet).Count == 0 ||
                (oneShotLeft > 0 && state != SpriteStates.Greet))
                return false;
            StartOneShot(SpriteStates.Greet);
            greetSide = brain != null ? brain.ChatSide : GreetRules.Peaceful;
            greetStarts++;
            return true;
        }

        /// <summary>A custom sprite set is loaded (conversations need one).</summary>
        public bool HasSet
        {
            get { return set != null; }
        }

        /// <summary>talk* sheets of the set (0: talking shows idle).</summary>
        public int TalkSheets
        {
            get { return set != null ? SpriteStates.Variants(set.Set, SpriteStates.Talk).Count : 0; }
        }

        /// <summary>A greet sheet is playing.</summary>
        public bool Greeting
        {
            get { return state == SpriteStates.Greet && oneShotLeft > 0; }
        }

        /// <summary>
        /// Loops a random talk* sheet (idle without one) for this long while standing; each call picks again
        /// (spec conversations §2). A hit, attack or greeting sheet finishes first.
        /// </summary>
        public void Talk(float seconds)
        {
            if (set == null)
                return;
            talkLeft = seconds;
            if (oneShotLeft > 0)
                return;
            state = null;
            SetState(SpriteStates.Talk);
        }

        /// <summary>Back to idle (or whatever the ANPC does) at the next update.</summary>
        public void StopTalk()
        {
            talkLeft = 0f;
        }

        /// <summary>Seconds the greet sheet now playing lasts (0 when none plays).</summary>
        public float GreetSeconds
        {
            get { return set != null && state == SpriteStates.Greet && anim != null ? (float)anim.Frames / set.Set.Fps : 0f; }
        }

        /// <summary>A cast* or attack* sheet exists; without one a spell plays no animation.</summary>
        bool HasCastSheet
        {
            get { return SpriteStates.Variants(set.Set, SpriteStates.Cast).Count > 0; }
        }

        void StartAttack(MobileStates s)
        {
            if (s == MobileStates.Spell && !HasCastSheet)
                return;
            bool thrown = IsThrow(s);
            if (s == MobileStates.Spell)
                StartOneShot(SpriteStates.Cast);
            else if (IsRanged(s) || thrown)
                StartOneShot(SpriteStates.RangedAttack, RangedSheet);
            else
                StartOneShot(SpriteStates.Attack);
            hitWaiting = false;
            // Only attack sheets with an action frame take over the timing; spells keep DFU's own.
            bool ours = anim.ActionFrame >= 0 && SpriteStates.IsAttackSheet(anim.Name) && s != MobileStates.Spell;
            pendingBlow = ours ? (s == MobileStates.PrimaryAttack && !thrown ? MeleeBlow : ArrowBlow) : NoBlow;
            vanillaToHold = ours ? 1 : 0;
        }

        /// <summary>
        /// Attack sheets with an action frame decide when the blow lands. DFU raises DoMeleeDamage / ShootArrow from its
        /// own animation (a coroutine, before LateUpdate) and EnemyAttack.Update acts on it the next frame, so here,
        /// in LateUpdate, DFU's own signal for that swing (one per swing) is held back and ours is raised when the sheet
        /// shows its action frame. A swing this sprite did not see start keeps DFU's own signal.
        /// </summary>
        void TimeBlows()
        {
            if (mobile == null)
                return;
            if (sentBlow != NoBlow)
            {
                bool stillWaiting = sentBlow == MeleeBlow ? mobile.DoMeleeDamage : mobile.ShootArrow;
                if (!stillWaiting)
                    blowsDelivered++;
                sentBlow = NoBlow;
            }
            if (vanillaToHold > 0 && (mobile.DoMeleeDamage || mobile.ShootArrow))
            {
                mobile.DoMeleeDamage = false;
                mobile.ShootArrow = false;
                vanillaToHold--;
                vanillaBlowsHeld++;
            }
            else if (ranged != null)
            {
                ranged.TakeVanillaBlow();       // a sheet without an action frame: DFU's moment, our projectile
            }

            if (pendingBlow != NoBlow && (state == SpriteStates.Attack || state == SpriteStates.RangedAttack) && frame >= anim.ActionFrame)
            {
                if (pendingBlow == MeleeBlow)
                {
                    mobile.DoMeleeDamage = true;
                    sentBlow = pendingBlow;
                }
                else if (ranged != null)
                {
                    ranged.Fire();
                    blowsDelivered++;
                }
                else
                {
                    mobile.ShootArrow = true;
                    sentBlow = pendingBlow;
                }
                pendingBlow = NoBlow;
                blowsLanded++;
            }
        }

        void OnDestroy()
        {
            if (acquired)
                heldSet.Release();
            if (material != null)
                Destroy(material);
        }

        string WantedState()
        {
            MobileStates s = mobile != null ? mobile.EnemyState : MobileStates.Idle;
            if (s == MobileStates.Hurt)
                return SpriteStates.Hit;
            if (s == MobileStates.Spell && HasCastSheet)
                return SpriteStates.Cast;
            if (s == MobileStates.PrimaryAttack && !IsThrow(s))
                return SpriteStates.Attack;
            if (IsRanged(s) || IsThrow(s))
                return SpriteStates.RangedAttack;
            if (talkLeft > 0 && speed <= WalkSpeed)
                return SpriteStates.Talk;
            if (speed <= WalkSpeed)
                return SpriteStates.Idle;       // standing, or turning on the spot
            return gait != null && gait.Current == Gait.Run ? SpriteStates.Run : SpriteStates.Walk;
        }

        /// <summary>A creature's attack state that NpcRanged started as a throw.</summary>
        bool IsThrow(MobileStates s)
        {
            return s == MobileStates.PrimaryAttack && ranged != null && ranged.Throwing;
        }

        static bool IsRanged(MobileStates s)
        {
            return s == MobileStates.RangedAttack1 || s == MobileStates.RangedAttack2;
        }

        /// <summary>Plays a state's sheet (or the named one, if the set has it) from its first frame to its last.</summary>
        void StartOneShot(string wanted, string sheet = null)
        {
            state = null;
            SetState(wanted, sheet);
            oneShotLeft = (float)anim.Frames / set.Set.Fps;
            if (wanted == SpriteStates.Hit)
                hitStarts++;
        }

        void SetState(string wanted, string sheet = null)
        {
            if (wanted == state && anim != null)
                return;
            state = wanted;
            SpriteAnimation named;
            if (sheet != null && set.Set.Animations.TryGetValue(sheet, out named) && set.Sheets.ContainsKey(sheet))
            {
                anim = named;
            }
            else
            {
                List<SpriteAnimation> variants = SpriteStates.Variants(set.Set, wanted);
                if (variants.Count == 0)
                    variants = SpriteStates.Variants(set.Set, SpriteStates.Idle);
                anim = variants[Random.Range(0, variants.Count)];
            }
            time = 0;
            material.mainTexture = Filtered(set.Sheets[anim.Name]);
            material.mainTextureScale = new Vector2(1f / anim.Frames, 1f / SpriteSetParser.Directions.Length);
        }

        void Draw()
        {
            Camera cam = Camera.main;
            Vector3 feet = transform.position;
            if (controller != null)
                feet.y = controller.bounds.min.y;

            // Face the camera around the vertical axis (a Unity quad shows its -Z side).
            if (cam != null)
            {
                Vector3 look = cam.transform.forward;
                look.y = 0;
                if (look.sqrMagnitude > 0.0001f)
                    quad.transform.rotation = Quaternion.LookRotation(look);
                Vector3 toCamera = cam.transform.position - transform.position;
                Vector3 fwd = transform.forward;
                row = SpriteDirections.Row(toCamera.x, toCamera.z, fwd.x, fwd.z);
            }

            bool loop = state == SpriteStates.Idle || state == SpriteStates.Walk || state == SpriteStates.Run || state == SpriteStates.Talk;
            int f = (int)(time * set.Set.Fps);
            f = loop ? f % anim.Frames : Mathf.Min(f, anim.Frames - 1);
            if (f != frame)
            {
                frame = f;
                frameChanges++;
                if (voice != null && (state == SpriteStates.Walk || state == SpriteStates.Run))
                    voice.OnSheetFrame(anim, f);
            }

            float k = worldPerPixel;
            quad.transform.localScale = new Vector3(anim.CellWidth * k, set.Set.CellHeight * k * heightScale, 1f);
            quad.transform.position = feet + Vector3.up * ((set.Set.CellHeight * 0.5f - set.Set.GroundY) * k * heightScale);
            int rows = SpriteSetParser.Directions.Length;
            material.mainTextureOffset = new Vector2((float)frame / anim.Frames, (float)(rows - 1 - row) / rows);
        }
    }
}

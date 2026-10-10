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
        int hitStarts;
        MobileStates lastMobileState;
        int pendingBlow;        // MeleeBlow or ArrowBlow still to land on the attack sheet's action frame
        int sentBlow;           // the blow signalled last frame, for counting what DFU took
        int vanillaToHold;      // DFU blow signals still to hold back: one per swing whose blow is ours
        bool hitWaiting;        // hit during a swing: its sheet plays when the attack sheet ends
        bool paralysed;         // read in LateUpdate: DFU clears paralysis each frame and the effect sets it again
        int blowsLanded;
        int blowsDelivered;
        int vanillaBlowsHeld;

        public LoadedSpriteSet Set
        {
            get { return set; }
        }

        public float WorldPerPixel
        {
            get { return worldPerPixel; }
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

        /// <summary>Stops drawing (the corpse sprite takes over).</summary>
        public void Hide()
        {
            if (quadRenderer != null)
                quadRenderer.enabled = false;
            set = null;
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

        void Update()
        {
            if (set == null)
                return;
            float dt = Time.deltaTime;
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

        /// <summary>A cast* or attack* sheet exists; without one a spell plays no animation.</summary>
        bool HasCastSheet
        {
            get { return SpriteStates.Variants(set.Set, SpriteStates.Cast).Count > 0; }
        }

        void StartAttack(MobileStates s)
        {
            if (s == MobileStates.Spell && !HasCastSheet)
                return;
            StartOneShot(s == MobileStates.Spell ? SpriteStates.Cast : SpriteStates.Attack);
            hitWaiting = false;
            // Only attack sheets with an action frame take over the timing; spells keep DFU's own.
            bool ours = anim.ActionFrame >= 0 && SpriteStates.StateOf(anim.Name) == SpriteStates.Attack && s != MobileStates.Spell;
            pendingBlow = ours ? (s == MobileStates.PrimaryAttack ? MeleeBlow : ArrowBlow) : NoBlow;
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

            if (pendingBlow != NoBlow && state == SpriteStates.Attack && frame >= anim.ActionFrame)
            {
                if (pendingBlow == MeleeBlow)
                    mobile.DoMeleeDamage = true;
                else
                    mobile.ShootArrow = true;
                sentBlow = pendingBlow;
                pendingBlow = NoBlow;
                blowsLanded++;
            }
        }

        void OnDestroy()
        {
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
            if (s == MobileStates.PrimaryAttack || s == MobileStates.RangedAttack1 || s == MobileStates.RangedAttack2)
                return SpriteStates.Attack;
            return speed > WalkSpeed ? SpriteStates.Walk : SpriteStates.Idle;
        }

        /// <summary>Plays a state's sheet from its first frame and keeps it until its last frame has shown.</summary>
        void StartOneShot(string wanted)
        {
            state = null;
            SetState(wanted);
            oneShotLeft = (float)anim.Frames / set.Set.Fps;
            if (wanted == SpriteStates.Hit)
                hitStarts++;
        }

        void SetState(string wanted)
        {
            if (wanted == state && anim != null)
                return;
            List<SpriteAnimation> variants = SpriteStates.Variants(set.Set, wanted);
            if (variants.Count == 0)
                variants = SpriteStates.Variants(set.Set, SpriteStates.Idle);
            state = wanted;
            anim = variants[Random.Range(0, variants.Count)];
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

            bool loop = state == SpriteStates.Idle || state == SpriteStates.Walk;
            int f = (int)(time * set.Set.Fps);
            f = loop ? f % anim.Frames : Mathf.Min(f, anim.Frames - 1);
            if (f != frame)
            {
                frame = f;
                frameChanges++;
            }

            float k = worldPerPixel;
            quad.transform.localScale = new Vector3(anim.CellWidth * k, set.Set.CellHeight * k * heightScale, 1f);
            quad.transform.position = feet + Vector3.up * ((set.Set.CellHeight * 0.5f - set.Set.GroundY) * k * heightScale);
            int rows = SpriteSetParser.Directions.Length;
            material.mainTextureOffset = new Vector2((float)frame / anim.Frames, (float)(rows - 1 - row) / rows);
        }
    }
}

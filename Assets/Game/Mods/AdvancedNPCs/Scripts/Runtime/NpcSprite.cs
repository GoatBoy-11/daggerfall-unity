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

        LoadedSpriteSet set;
        float worldPerPixel;
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

        public LoadedSpriteSet Set
        {
            get { return set; }
        }

        public float WorldPerPixel
        {
            get { return worldPerPixel; }
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

        /// <summary>The vanilla class sprite's height: the default world height of a custom sprite (spec 1b §5.2).</summary>
        public static float DefaultHeight(GameObject npc)
        {
            DaggerfallEnemy enemy = npc.GetComponent<DaggerfallEnemy>();
            MobileUnit unit = enemy != null ? enemy.MobileUnit : null;
            return unit != null ? unit.GetSize().y : 2f;
        }

        public void Init(LoadedSpriteSet loaded, float worldHeight)
        {
            set = loaded;
            worldPerPixel = worldHeight / Mathf.Max(1f, loaded.StandingHeightPx);
            DaggerfallEnemy enemy = GetComponent<DaggerfallEnemy>();
            mobile = enemy != null ? enemy.MobileUnit : null;
            vanilla = mobile != null ? mobile.GetComponent<MeshRenderer>() : null;
            controller = GetComponent<CharacterController>();
            entityBehaviour = GetComponent<DaggerfallEntityBehaviour>();

            quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "AnpcSprite";
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(transform, false);
            quadRenderer = quad.GetComponent<MeshRenderer>();
            // A copy of the vanilla billboard material keeps DFU's shader, lighting and fog.
            material = vanilla != null && vanilla.sharedMaterial != null ? new Material(vanilla.sharedMaterial) : new Material(Shader.Find("Sprites/Default"));
            quadRenderer.sharedMaterial = material;
            if (vanilla != null)
            {
                quadRenderer.shadowCastingMode = vanilla.shadowCastingMode;
                quadRenderer.receiveShadows = vanilla.receiveShadows;
                // Not "enabled = false": DFU's EntityConcealmentBehaviour sets enabled every frame for invisibility.
                vanilla.forceRenderingOff = true;
            }
            lastPosition = transform.position;
            SetState(SpriteStates.Idle);
            Draw();
        }

        void Update()
        {
            if (set == null)
                return;
            float dt = Time.deltaTime;
            Vector3 p = transform.position;
            Vector3 moved = p - lastPosition;
            moved.y = 0;
            lastPosition = p;
            if (dt > 0)
                speed = Mathf.Lerp(speed, moved.magnitude / dt, 0.5f);

            SetState(WantedState());
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
            Draw();
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
            if (s == MobileStates.PrimaryAttack || s == MobileStates.RangedAttack1 || s == MobileStates.RangedAttack2 || s == MobileStates.Spell)
                return SpriteStates.Attack;
            return speed > WalkSpeed ? SpriteStates.Walk : SpriteStates.Idle;
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
            material.mainTexture = set.Sheets[anim.Name];
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
            quad.transform.localScale = new Vector3(anim.CellWidth * k, set.Set.CellHeight * k, 1f);
            quad.transform.position = feet + Vector3.up * ((set.Set.CellHeight * 0.5f - set.Set.GroundY) * k);
            int rows = SpriteSetParser.Directions.Length;
            material.mainTextureOffset = new Vector2((float)frame / anim.Frames, (float)(rows - 1 - row) / rows);
        }
    }
}

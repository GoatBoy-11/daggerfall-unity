using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>
    /// A dead ANPC's body (spec 1b §6.4): plays a death sheet once, then stays as the corpse (death_static.png if the set
    /// has one, otherwise the last death frame). DFU's loot pile stays clickable, with its own picture hidden.
    /// </summary>
    public class NpcCorpseSprite : MonoBehaviour
    {
        const float LootSearchSeconds = 10f;   // quest foes get no loot pile: stop looking after this

        /// <summary>Every corpse sprite currently in the world (self-test).</summary>
        public static readonly List<NpcCorpseSprite> All = new List<NpcCorpseSprite>();

        string key;
        LoadedSpriteSet set;
        float worldPerPixel;
        float heightScale = 1f;
        SpriteAnimation anim;
        Vector3 facing;
        float time;
        bool finished;
        bool showingStatic;
        GameObject quad;
        Material material;
        DaggerfallEntityBehaviour behaviour;
        DaggerfallLoot loot;
        bool searching = true;
        bool hidLoot;
        float searchTime;
        float lootGroundY;      // where DFU meant the loot pile to stand

        public bool Finished
        {
            get { return finished; }
        }

        public bool ShowingStatic
        {
            get { return showingStatic; }
        }

        public bool HidLoot
        {
            get { return hidLoot; }
        }

        /// <summary>DFU's loot pile for this body, once found (self-test).</summary>
        public DaggerfallLoot Loot
        {
            get { return loot; }
        }

        /// <summary>Starts a body where the NPC stands; null if its sprite set has no death sheet.</summary>
        public static NpcCorpseSprite Spawn(NpcSprite from, DaggerfallEntityBehaviour behaviour)
        {
            List<SpriteAnimation> deaths = SpriteStates.Variants(from.Set.Set, SpriteStates.Death);
            if (deaths.Count == 0)
                return null;
            GameObject go = new GameObject("AnpcCorpse");
            go.transform.SetParent(from.transform.parent, true);     // the town: unloads with it
            // On the ground, also when killed in mid-air. DFU drops the loot pile at EnemyMotor.FindGroundPosition, whose
            // ray can hit the dying NPC itself; the pile is moved down to the same ground when it turns up.
            Vector3 feet = GroundBelow(from.Feet, from.transform);
            EnemyMotor motor = from.GetComponent<EnemyMotor>();
            go.transform.position = feet;
            NpcCorpseSprite corpse = go.AddComponent<NpcCorpseSprite>();
            corpse.set = from.Set;
            corpse.worldPerPixel = from.WorldPerPixel;
            corpse.heightScale = from.HeightScale;
            corpse.anim = deaths[Random.Range(0, deaths.Count)];
            corpse.facing = from.transform.forward;
            corpse.behaviour = behaviour;
            corpse.lootGroundY = motor != null ? motor.FindGroundPosition().y : feet.y;
            NpcBrain brain = from.GetComponent<NpcBrain>();
            corpse.key = brain != null ? brain.Id : null;

            corpse.quad = NpcSprite.CreateQuad(go.transform, "AnpcCorpseSprite");
            corpse.material = new Material(from.MaterialTemplate);
            corpse.quad.GetComponent<MeshRenderer>().sharedMaterial = corpse.material;
            corpse.ShowDeathSheet();
            corpse.Draw();
            return corpse;
        }

        void OnEnable()
        {
            All.Add(this);
        }

        void OnDisable()
        {
            All.Remove(this);
        }

        void OnDestroy()
        {
            if (material != null)
                Destroy(material);
            // The body went first (e.g. anpc_remove): never leave a loot pile without any picture.
            if (hidLoot && loot != null)
            {
                foreach (MeshRenderer r in loot.GetComponentsInChildren<MeshRenderer>(true))
                    r.forceRenderingOff = false;
            }
        }

        /// <summary>The first ground below a point that is not part of the NPC itself; the point if there is none.</summary>
        static Vector3 GroundBelow(Vector3 point, Transform npc)
        {
            RaycastHit[] hits = Physics.RaycastAll(point + Vector3.up * 0.1f, Vector3.down, 50f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            Vector3 ground = point;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(npc) || hit.distance >= best)
                    continue;
                best = hit.distance;
                ground = hit.point;
            }
            return ground;
        }

        /// <summary>Removes the bodies and loot piles of a person (anpc_remove).</summary>
        public static void RemoveFor(string key)
        {
            foreach (NpcCorpseSprite corpse in new List<NpcCorpseSprite>(All))
            {
                if (corpse.key != key)
                    continue;
                DaggerfallLoot pile = corpse.loot != null ? corpse.loot : (corpse.behaviour != null ? corpse.behaviour.CorpseLootContainer : null);
                if (pile != null)
                    Destroy(pile.gameObject);
                corpse.hidLoot = false;
                Destroy(corpse.gameObject);
            }
        }

        void Update()
        {
            time += Time.deltaTime;
            if (!finished && time >= (float)anim.Frames / set.Set.Fps)
            {
                finished = true;
                if (set.DeathStatic != null)
                {
                    showingStatic = true;
                    material.mainTexture = NpcSprite.Filtered(set.DeathStatic);
                    material.mainTextureScale = Vector2.one;
                    material.mainTextureOffset = Vector2.zero;
                }
            }
            WatchLoot();
        }

        void LateUpdate()
        {
            Draw();
        }

        void ShowDeathSheet()
        {
            material.mainTexture = NpcSprite.Filtered(set.Sheets[anim.Name]);
            material.mainTextureScale = new Vector2(1f / anim.Frames, 1f / SpriteSetParser.Directions.Length);
        }

        /// <summary>Hide the picture of the loot pile DFU leaves here; remove the body when the pile is gone.</summary>
        void WatchLoot()
        {
            if (searching)
            {
                loot = behaviour != null ? behaviour.CorpseLootContainer : null;
                if (loot != null)
                {
                    loot.transform.position += Vector3.up * (transform.position.y - lootGroundY);
                    foreach (MeshRenderer r in loot.GetComponentsInChildren<MeshRenderer>(true))
                        r.forceRenderingOff = true;
                    hidLoot = true;
                    searching = false;
                }
                else
                {
                    searchTime += Time.deltaTime;
                    if (searchTime > LootSearchSeconds)
                        searching = false;
                }
            }
            else if (hidLoot && loot == null)
            {
                Destroy(gameObject);
            }
        }

        void Draw()
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 look = cam.transform.forward;
                look.y = 0;
                if (look.sqrMagnitude > 0.0001f)
                    quad.transform.rotation = Quaternion.LookRotation(look);
            }
            float k = worldPerPixel;
            Vector3 feet = transform.position;
            if (showingStatic)
            {
                Texture2D t = set.DeathStatic;
                quad.transform.localScale = new Vector3(t.width * k, t.height * k * heightScale, 1f);
                quad.transform.position = feet + Vector3.up * ((t.height * 0.5f - set.Set.DeathStaticGroundY) * k * heightScale);
                return;
            }

            int row = 0;
            if (cam != null)
            {
                Vector3 toCamera = cam.transform.position - feet;
                row = SpriteDirections.Row(toCamera.x, toCamera.z, facing.x, facing.z);
            }
            int frame = Mathf.Min((int)(time * set.Set.Fps), anim.Frames - 1);
            int rows = SpriteSetParser.Directions.Length;
            quad.transform.localScale = new Vector3(anim.CellWidth * k, set.Set.CellHeight * k * heightScale, 1f);
            quad.transform.position = feet + Vector3.up * ((set.Set.CellHeight * 0.5f - set.Set.GroundY) * k * heightScale);
            material.mainTextureOffset = new Vector2((float)frame / anim.Frames, (float)(rows - 1 - row) / rows);
        }
    }
}

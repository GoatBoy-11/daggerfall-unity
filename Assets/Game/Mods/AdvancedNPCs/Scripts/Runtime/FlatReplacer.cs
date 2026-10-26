using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using DaggerfallConnect;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>
    /// Generic ANPCs with "replaces" stand in for vanilla flat NPCs (StaticNPC): the flat stays in the scene but is hidden
    /// and cannot be clicked, an ANPC spawns on its spot, and clicking the ANPC runs vanilla's own click on the flat
    /// (PlayerActivate.StaticNPCClick), so talk, quests, shops, inns and guild services are the vanilla NPC's.
    /// A quest hiding or showing the vanilla NPC hides or shows its stand-in. A stand-in can die; it stays dead.
    /// </summary>
    public class FlatReplacer : MonoBehaviour
    {
        const float PollInterval = 0.5f;

        static readonly MethodInfo StaticNpcClick =
            typeof(PlayerActivate).GetMethod("StaticNPCClick", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo NameLabelField =
            typeof(DaggerfallTalkWindow).GetField("labelNameNPC", BindingFlags.Instance | BindingFlags.NonPublic);

        class Link
        {
            public StaticNPC Flat;
            public NpcBrain Brain;
            public NpcTalk Talk;
            public ReplaceSettings Settings;
        }

        /// <summary>A place ReplaceIn looked at, so anpc_flat can name the flats there.</summary>
        class Scanned
        {
            public Transform Root;
            public int MapId;
            public string Where;
        }

        const float FlatLookupRange = 10f;

        AdvancedNpcsMod owner;
        NpcSpawner spawner;
        readonly List<Link> links = new List<Link>();
        readonly List<Scanned> scanned = new List<Scanned>();
        float pollTimer;
        bool warnedNoClick;
        bool listening;

        /// <summary>Flats replaced by the last ReplaceIn call (read by the self-test).</summary>
        public int LastReplaced { get; private set; }

        public void Init(AdvancedNpcsMod mod, NpcSpawner npcSpawner)
        {
            owner = mod;
            spawner = npcSpawner;
        }

        /// <summary>True when any loaded template replaces vanilla NPCs (nothing to scan otherwise).</summary>
        public bool Active
        {
            get
            {
                foreach (NpcDefinition t in owner.Catalog.Generics)
                {
                    if (t.Replace != null)
                        return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Replaces every matching flat under root (a town, building interior or dungeon; inactive flats too, since quests
        /// and closed buildings switch them off). where tells places in one map apart ("town", "dungeon", a building key).
        /// </summary>
        public List<NpcBrain> ReplaceIn(Transform root, int mapId, string where, string defaultRace, float wanderCap)
        {
            List<NpcBrain> spawned = new List<NpcBrain>();
            LastReplaced = 0;
            if (root != null)
            {
                scanned.RemoveAll(delegate (Scanned s) { return s.Root == null || s.Root == root; });
                scanned.Add(new Scanned { Root = root, MapId = mapId, Where = where });
            }
            if (root == null || !Active)
                return spawned;
            ListenForTalk();
            foreach (StaticNPC flat in root.GetComponentsInChildren<StaticNPC>(true))
            {
                if (IsLinked(flat) || HasOwnAction(flat))
                    continue;
                StaticNPC.NPCData d = flat.Data;
                List<NpcDefinition> candidates = ReplaceRules.Candidates(owner.Catalog.Generics, d.billboardArchiveIndex, d.billboardRecordIndex,
                    d.factionID, ReplaceRules.FlatId(mapId, where, d.hash));
                if (candidates.Count == 0)
                    continue;
                string key = ReplaceRules.KeyFor(mapId, where, d.hash);
                NpcDefinition def = ReplaceRules.Pick(candidates, key);
                NpcState state = owner.States.GetOrCreate(key);
                Hide(flat);
                LastReplaced++;
                if (state.dead)
                    continue;       // the stand-in died: the vanilla NPC stays gone too

                Vector3 feet;
                Vector3 spot = flat.transform.position;
                if (!SpotFinder.Near(spot, spot, out feet))
                    feet = spot;
                Vector3 local = root.InverseTransformPoint(feet);
                NpcInstance instance = PopulationPlanner.ForReplacement(def, key, d.gender == Genders.Female ? "Female" : "Male", defaultRace,
                    owner.Names, local.x, local.y, local.z);
                NpcBrain brain = spawner.SpawnChecked(instance, state, root);
                if (brain == null)
                    continue;
                brain.WanderCap = wanderCap;
                brain.Leash = def.Replace.StayNear;
                NpcTalk talk = brain.GetComponent<NpcTalk>();
                if (talk != null)
                    talk.StandIn = flat;
                links.Add(new Link { Flat = flat, Brain = brain, Talk = talk, Settings = def.Replace });
                brain.gameObject.SetActive(flat.gameObject.activeSelf);
                spawned.Add(brain);
                AdvancedNpcsMod.Log(key + ": " + def.Id + " stands in for " + flat.DisplayName + " (" + d.billboardArchiveIndex + "." +
                    d.billboardRecordIndex + ", faction " + d.factionID + ").");
            }
            return spawned;
        }

        /// <summary>The vanilla NPC an ANPC stands in for, or null.</summary>
        public StaticNPC FlatOf(NpcBrain brain)
        {
            foreach (Link l in links)
            {
                if (l.Brain == brain)
                    return l.Flat;
            }
            return null;
        }

        /// <summary>anpc_flat: the vanilla flat NPC nearest to a point (within 10 m) with its "replacesFlat" id.</summary>
        public string DescribeNearest(Vector3 from)
        {
            StaticNPC best = null;
            Scanned bestPlace = null;
            float bestDistance = FlatLookupRange;
            scanned.RemoveAll(delegate (Scanned s) { return s.Root == null; });
            foreach (Scanned place in scanned)
            {
                if (!place.Root.gameObject.activeInHierarchy)
                    continue;
                foreach (StaticNPC flat in place.Root.GetComponentsInChildren<StaticNPC>(true))
                {
                    float distance = Vector3.Distance(from, flat.transform.position);
                    if (distance < bestDistance)
                    {
                        best = flat;
                        bestPlace = place;
                        bestDistance = distance;
                    }
                }
            }
            if (best == null)
                return "No vanilla flat NPC within " + FlatLookupRange + " m (stand next to one in a town, building or dungeon).";
            StaticNPC.NPCData d = best.Data;
            string id = ReplaceRules.FlatId(bestPlace.MapId, bestPlace.Where, d.hash);
            Link link = links.Find(delegate (Link l) { return l.Flat == best && l.Brain != null; });
            string now = HasOwnAction(best) ? "It has its own scripted action and is never replaced."
                : link != null ? "Now replaced by " + link.Brain.Instance.Definition.Id + "."
                : "Not replaced now.";
            return best.DisplayName + " (sprite " + d.billboardArchiveIndex + "." + d.billboardRecordIndex + ", faction " + d.factionID + ", " +
                bestDistance.ToString("0.0") + " m away). " + now + "\n\"replacesFlat\": \"" + id + "\"";
        }

        bool IsLinked(StaticNPC flat)
        {
            return links.Exists(delegate (Link l) { return l.Flat == flat && l.Brain != null; });
        }

        /// <summary>Flats with a ShowText action (castle gate guard, Sheogorath) do their own thing; vanilla never talks to them either.</summary>
        static bool HasOwnAction(StaticNPC flat)
        {
            DaggerfallAction action = flat.GetComponent<DaggerfallAction>();
            return action != null && (action.ActionFlag == DFBlock.RdbActionFlags.ShowTextWithInput || action.ActionFlag == DFBlock.RdbActionFlags.ShowText);
        }

        static void Hide(StaticNPC flat)
        {
            foreach (Renderer r in flat.GetComponentsInChildren<Renderer>(true))
                r.enabled = false;
            foreach (Collider c in flat.GetComponentsInChildren<Collider>(true))
                c.enabled = false;
        }

        void Update()
        {
            pollTimer -= Time.deltaTime;
            if (pollTimer > 0f || links.Count == 0)
                return;
            pollTimer = PollInterval;
            links.RemoveAll(delegate (Link l) { return l.Flat == null || l.Brain == null; });
            foreach (Link l in links)
            {
                // A quest "hide npc" / "restore npc" switches the flat itself off and on.
                bool shown = l.Flat.gameObject.activeSelf;
                if (l.Brain.gameObject.activeSelf != shown && l.Brain.CurrentMode != NpcMode.Dead)
                    l.Brain.gameObject.SetActive(shown);
                Renderer r = l.Flat.GetComponent<Renderer>();
                if (r != null && r.enabled)
                    Hide(l.Flat);
            }
        }

        /// <summary>
        /// Runs vanilla's click on the flat this ANPC stands in for. Returns false if DFU no longer has that method
        /// (the ANPC then talks as itself).
        /// </summary>
        public bool ClickFlat(StaticNPC flat)
        {
            if (StaticNpcClick == null || GameManager.Instance.PlayerActivate == null)
            {
                if (!warnedNoClick)
                {
                    warnedNoClick = true;
                    AdvancedNpcsMod.Log("PlayerActivate.StaticNPCClick not found in this DFU version; stand-ins talk as themselves.");
                }
                return false;
            }
            StaticNpcClick.Invoke(GameManager.Instance.PlayerActivate, new object[] { flat });
            return true;
        }

        void ListenForTalk()
        {
            if (listening || DaggerfallUI.UIManager == null)
                return;
            listening = true;
            DaggerfallUI.UIManager.OnWindowChange += OnWindowChange;
        }

        void OnDestroy()
        {
            if (listening && DaggerfallUI.UIManager != null)
                DaggerfallUI.UIManager.OnWindowChange -= OnWindowChange;
        }

        // The talk window can open straight from the click or later (a shop or guild menu's Talk button).
        void OnWindowChange(object sender, System.EventArgs e)
        {
            DaggerfallTalkWindow window = DaggerfallUI.UIManager.TopWindow as DaggerfallTalkWindow;
            if (window == null || TalkManager.Instance == null)
                return;
            StaticNPC target = TalkManager.Instance.StaticNPC;
            Link link = target == null ? null : links.Find(delegate (Link l) { return l.Flat == target && l.Brain != null; });
            if (link == null || link.Talk == null)
                return;
            if (link.Settings.Portrait && link.Talk.Portrait != null)
                NpcTalk.ApplyPortrait(window, link.Talk.Portrait);
            if (link.Settings.Name)
                StartCoroutine(SetName(window, link.Talk.DisplayName));
        }

        // The window sets its name label from TalkManager on Setup (first frame shown): set ours after that.
        IEnumerator SetName(DaggerfallTalkWindow window, string name)
        {
            for (int i = 0; i < 3; i++)
            {
                TextLabel label = NameLabelField == null ? null : NameLabelField.GetValue(window) as TextLabel;
                if (label != null)
                    label.Text = name;
                yield return null;
            }
        }
    }
}

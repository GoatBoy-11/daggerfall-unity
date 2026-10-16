using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Utility;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>Transfers hired people between area-owned spawns; NpcState remains the sole saved authority.</summary>
    public class CompanionRoster
    {
        readonly AdvancedNpcsMod owner;
        readonly NpcSpawner spawner;
        readonly HashSet<string> warned = new HashSet<string>();
        readonly List<KeyValuePair<string,NpcState>> members=new List<KeyValuePair<string,NpcState>>();
        float nextTick;
        public CompanionRoster(AdvancedNpcsMod owner, NpcSpawner spawner)
        {
            this.owner = owner; this.spawner = spawner;
            StreamingWorld.OnClearStreamingWorld += WorldCleared;
        }
        public void Dispose() { StreamingWorld.OnClearStreamingWorld -= WorldCleared; }
        void WorldCleared()
        {
            // Teleport/fast travel can rebuild outdoors into outdoors: do not reuse the old tile's position.
            // A subsequent save restoration supplies its own saved area/position again.
            foreach (var pair in owner.States.Companions())
                if (pair.Value.companion.status == "following") pair.Value.companion.area = null;
        }

        bool Exempt(CompanionRecord record)
        {
            NpcDefinition definition;
            if(owner.Catalog.ById.TryGetValue(record.template ?? "",out definition))return definition.NoCompanionSlot;
            definition=owner.Catalog.Generics.Find(delegate(NpcDefinition d){return d.Id==record.template;});
            return definition!=null?definition.NoCompanionSlot:record.noCompanionSlot;
        }
        public int UsedSlots
        {
            get
            {
                int count=0;
                foreach(var pair in owner.States.Companions())
                    if(CompanionLimits.ConsumesSlot(pair.Value.dead,pair.Value.companion.status,Exempt(pair.Value.companion)))count++;
                return count;
            }
        }
        public bool HasRecruitmentSlot(NpcBrain brain)
        {return brain!=null && CompanionLimits.CanRecruit(owner.Config.MaxFollowers,UsedSlots,brain.Definition.NoCompanionSlot);}
        public bool CanCommand(NpcBrain brain, string command)
        {
            if (brain == null || !brain.isActiveAndEnabled || brain.EntityBehaviour == null ||
                brain.CurrentMode == NpcMode.Dead || brain.EntityBehaviour.Entity.CurrentHealth<=0 || brain.IsEnemy || brain.State.hostile || brain.Definition.RecruitCost < 0 || brain.IsTurret) return false;
            CompanionRecord c = brain.State.companion;
            bool active=c!=null && CompanionCombatRules.Contract(c.status);
            if(brain.CurrentMode!=NpcMode.Calm && !(active && (CompanionOrders.Tactical(command) || command=="wait" || command=="follow"))) return false;
            if(active && (NpcBrain.Find(brain.Id)!=brain || !owner.States.Has(brain.Id) || owner.States.GetOrCreate(brain.Id)!=brain.State)) return false;
            return CompanionRecord.Allows(c != null ? c.status : null, command == "offer" ? "recruit" : command) &&
                (command != "recruit" || GameManager.Instance.PlayerEntity.GoldPieces >= brain.Definition.RecruitCost);
        }

        public bool Command(NpcBrain brain, string command)
        {
            // Recheck at execution time; never charge for a second hire, a dead NPC or a stale dialogue choice.
            if (command == "offer" || !CanCommand(brain, command)) return false;
            string area; Transform parent;
            if (!CurrentArea(out area, out parent)) return false;
            if(command=="push")
                return brain.CompanionCombat!=null && brain.CompanionCombat.Active && brain.State.companion.area==area && brain.CompanionCombat.PushAway();
            if (CompanionOrders.Tactical(command))
            {
                if(brain.CompanionCombat==null || !brain.CompanionCombat.Active || brain.State.companion.area!=area) return false;
                if(command=="orders") { var talk=brain.GetComponent<NpcTalk>(); return talk!=null && talk.RequestOrders(); }
                if(command=="regroup") brain.CompanionCombat.Regroup();
                else brain.CompanionCombat.SetStance(command);
                brain.CaptureState();
                NpcVoice orderVoice=brain.GetComponent<NpcVoice>();if(orderVoice!=null)orderVoice.OnCompanionCommand(command);
                return true;
            }
            if (command == "recruit")
            {
                // Keep the hire topic visible; capacity is checked at execution before gold or state changes.
                if(!HasRecruitmentSlot(brain)) {DaggerfallUI.MessageBox("Too many followers!");return false;}
                NpcMagic magic=brain.GetComponent<NpcMagic>();
                if(magic!=null) magic.BeginContract();
                if (brain.State.companion == null) brain.State.companion = CompanionRecord.From(brain.Instance);
                owner.States.Keep(brain.Id, brain.State); // promote even random-each-visit and wilderness people
                brain.State.companion.noCompanionSlot=brain.Definition.NoCompanionSlot;
                brain.Instance.Persistent = true;
                GameManager.Instance.PlayerEntity.GoldPieces -= brain.Definition.RecruitCost;
            }
            if(brain.CompanionCombat!=null && brain.CompanionCombat.Active) brain.CompanionCombat.CancelOrders();
            brain.State.companion.status = command == "wait" ? "waiting" : command == "dismiss" ? "dismissed" : "following";
            brain.State.companion.area = area;
            NpcMagic.EnsureCompanion(brain, command == "recruit");
            if(brain.CompanionCombat!=null) brain.CompanionCombat.Reanchor();
            CaptureLocation(brain, area);
            brain.CaptureState();
            brain.Teleport(brain.transform.position); // reset routine origin and cached area/package selection
            NpcVoice voice=brain.GetComponent<NpcVoice>();if(voice!=null)voice.OnCompanionCommand(command);
            AdvancedNpcsMod.Log(brain.Id + ": companion " + brain.State.companion.status + ".");
            return true;
        }

        public static bool CurrentArea(out string area, out Transform parent)
        {
            area = null; parent = null;
            GameManager gm = GameManager.Instance;
            if (gm == null || gm.PlayerEnterExit == null || gm.PlayerObject == null) return false;
            PlayerEnterExit pee = gm.PlayerEnterExit;
            if (pee.IsPlayerInsideDungeon)
            {
                if (pee.Dungeon == null) return false;
                area = pee.Dungeon.Summary.ID + ":dungeon"; parent = pee.Dungeon.transform;
            }
            else if (pee.IsPlayerInsideBuilding)
            {
                if (pee.Interior == null) return false;
                area = gm.PlayerGPS.CurrentMapID + ":b" + pee.Interior.EntryDoor.buildingKey; parent = pee.Interior.transform;
            }
            else { area = "outdoors"; parent = GameObjectHelper.GetBestParent(); }
            return parent != null && parent.gameObject.activeInHierarchy;
        }

        // Exterior waiting positions use a terrain tile's own coordinates, surviving floating origin shifts.
        public void CaptureLocation(NpcBrain brain, string area)
        {
            CompanionRecord c = brain.State.companion;
            if (c == null) return;
            Transform root = brain.transform.parent;
            if (area == "outdoors")
            {
                var pixel = GameManager.Instance.PlayerGPS.CurrentMapPixel;
                GameObject terrain = GameManager.Instance.StreamingWorld.GetTerrainFromPixel(pixel);
                if (terrain == null) return;
                c.mapX = pixel.X; c.mapY = pixel.Y; root = terrain.transform;
            }
            if (root == null) return;
            Vector3 position = c.status=="waiting" && brain.CompanionCombat!=null && brain.CompanionCombat.Active ? brain.CompanionCombat.WaitingPosition : brain.transform.position;
            Vector3 local = root.InverseTransformPoint(position);
            c.area = area; c.x = local.x; c.y = local.y; c.z = local.z;
        }

        // Rebuilding exterior ownership must preserve the original waiting point even mid-fight.
        internal void Reparent(NpcBrain brain, Transform parent)
        {
            NpcCompanionCombat combat=brain.CompanionCombat;
            bool waiting=brain.State.companion!=null && brain.State.companion.status=="waiting" && combat!=null && combat.Active;
            Vector3 anchor=waiting?combat.WaitingPosition:brain.transform.position;
            brain.transform.SetParent(parent,true);brain.Teleport(brain.transform.position);
            if(waiting) combat.PreserveWaitingPoint(anchor);
        }

        bool SavedPosition(CompanionRecord c, string area, Transform parent, out Vector3 position)
        {
            position = Vector3.zero;
            if (c.area != area) return false;
            Transform root = parent;
            if (area == "outdoors")
            {
                GameObject terrain = GameManager.Instance.StreamingWorld.GetTerrainFromPixel(c.mapX, c.mapY);
                if (terrain == null || !terrain.activeInHierarchy) return false;
                root = terrain.transform;
            }
            position = root.TransformPoint(new Vector3(c.x, c.y, c.z));
            return true;
        }

        public void Tick()
        {
            GameManager gm = GameManager.Instance;
            if (Time.unscaledTime < nextTick || gm == null || !gm.IsPlayingGame() || SaveLoadManager.Instance.LoadInProgress) return;
            nextTick = Time.unscaledTime + 0.25f;
            string area; Transform parent;
            if (!CurrentArea(out area, out parent)) return;
            owner.States.CopyCompanionsTo(members);
            foreach (var pair in members)
            {
                NpcState state = pair.Value;
                CompanionRecord c = state.companion;
                NpcBrain brain = NpcBrain.Find(pair.Key);
                if (state.dead) continue;
                if (brain != null && brain.State != state) { NpcBrain.Discard(brain); brain = null; }
                if (brain != null && c.area == area && brain.gameObject.activeInHierarchy)
                {
                    // Walk across town/wilderness ownership boundaries without moving the person.
                    if (brain.transform.parent != parent)
                    { Reparent(brain,parent); }
                    CaptureLocation(brain, area);
                    continue;
                }
                if (brain != null) { brain.CaptureState(); NpcBrain.Discard(brain); }
                Vector3 position;
                bool saved = SavedPosition(c, area, parent, out position);
                if (c.status != "following" && !saved) continue;
                NpcDefinition definition;
                if (!owner.Catalog.ById.TryGetValue(c.template ?? "", out definition))
                    definition = owner.Catalog.Generics.Find(delegate(NpcDefinition d) { return d.Id == c.template; });
                if (definition == null)
                {
                    if (warned.Add(pair.Key)) AdvancedNpcsMod.LogError(pair.Key + ": companion definition missing; saved identity retained.");
                    continue;
                }
                c.noCompanionSlot=definition.NoCompanionSlot;
                Vector3 feet;
                if (saved)
                {
                    // Saved feet must still have a floor and clearance. Retry rather than move a waiting person.
                    if (!SpotFinder.CompanionNear(position, position, out feet)) continue;
                }
                else
                {
                    Vector3 anchor = gm.PlayerObject.transform.position - Vector3.up * 0.8f;
                    if (!SpotFinder.CompanionNear(anchor, anchor - gm.PlayerObject.transform.forward * 2f, out feet))
                    {
                        if (warned.Add("arrival:" + pair.Key)) AdvancedNpcsMod.Log(pair.Key + ": no clear companion arrival spot in " + area + "; retrying.");
                        continue;
                    }
                }
                warned.Remove("arrival:" + pair.Key);
                NpcInstance person = c.Person(pair.Key, definition);
                Vector3 local = parent.InverseTransformPoint(feet);
                person.X = local.x; person.Y = local.y; person.Z = local.z;
                brain = spawner.SpawnChecked(person, state, parent, true);
                if (brain != null) { c.area = area; CaptureLocation(brain, area); }
            }
        }
    }
}

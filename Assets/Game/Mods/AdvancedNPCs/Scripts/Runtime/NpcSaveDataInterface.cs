using System;
using System.Collections.Generic;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using FullSerializer;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    [fsObject("v1")]
    public class NpcSaveData
    {
        public Dictionary<string, NpcState> States = new Dictionary<string, NpcState>();
        /// <summary>People placed with anpc_spawn (missing in older saves).</summary>
        public List<PlacedNpc> Placed = new List<PlacedNpc>();
        /// <summary>Dialogue flags set by topics (missing in older saves).</summary>
        public List<string> Flags = new List<string>();
        /// <summary>SaveFormat of the data; 0 in saves from before it existed.</summary>
        public int Version;
        public string MagicArea = "";
        public long MagicVisit;
    }

    /// <summary>Connects the NPC state table to DFU's per-mod save data.</summary>
    public class NpcSaveDataInterface : IHasModSaveData
    {
        readonly NpcStateTable table;
        readonly PlacedNpcList placed;
        readonly FlagSet flags;
        readonly Action onRestored;

        public NpcSaveDataInterface(NpcStateTable table, PlacedNpcList placed, FlagSet flags, Action onRestored)
        {
            this.table = table;
            this.placed = placed;
            this.flags = flags;
            this.onRestored = onRestored;
        }

        public Type SaveDataType
        {
            get { return typeof(NpcSaveData); }
        }

        public object NewSaveData()
        {
            return new NpcSaveData();
        }

        public object GetSaveData()
        {
            NpcSaveData data = new NpcSaveData();
            data.Version = SaveFormat.Current;
            foreach (NpcBrain brain in NpcBrain.All())
            {
                brain.CaptureState();
                string area; UnityEngine.Transform parent;
                if (brain.State.companion != null && CompanionRoster.CurrentArea(out area, out parent) &&
                    brain.State.companion.area == area && brain.gameObject.activeInHierarchy)
                    AdvancedNpcsMod.Instance.Companions.CaptureLocation(brain, area);
            }
            data.MagicArea = AdvancedNpcsMod.Instance.MagicWorld.Area;
            data.MagicVisit = AdvancedNpcsMod.Instance.MagicWorld.Visit;
            data.States = table.Snapshot();
            data.Placed = placed.Snapshot();
            data.Flags = flags.Names();
            return data;
        }

        public void RestoreSaveData(object saveData)
        {
            NpcSaveData data = saveData as NpcSaveData;
            string formatProblem = data != null ? SaveFormat.Check(data.Version) : null;
            if (formatProblem != null)
                AdvancedNpcsMod.LogError(formatProblem);
            AdvancedNpcsMod.Instance.MagicWorld.Area = data != null ? data.MagicArea : "";
            AdvancedNpcsMod.Instance.MagicWorld.Visit = data != null ? data.MagicVisit : 0;
            table.Restore(data != null ? data.States : null);
            placed.Restore(data != null ? data.Placed : null);
            flags.Restore(data != null ? data.Flags : null);
            onRestored();
        }
    }
}

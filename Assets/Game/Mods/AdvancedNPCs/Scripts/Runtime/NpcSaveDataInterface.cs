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
    }

    /// <summary>Connects the NPC state table to DFU's per-mod save data.</summary>
    public class NpcSaveDataInterface : IHasModSaveData
    {
        readonly NpcStateTable table;
        readonly Action onRestored;

        public NpcSaveDataInterface(NpcStateTable table, Action onRestored)
        {
            this.table = table;
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
            data.States = table.Snapshot();
            return data;
        }

        public void RestoreSaveData(object saveData)
        {
            NpcSaveData data = saveData as NpcSaveData;
            table.Restore(data != null ? data.States : null);
            onRestored();
        }
    }
}

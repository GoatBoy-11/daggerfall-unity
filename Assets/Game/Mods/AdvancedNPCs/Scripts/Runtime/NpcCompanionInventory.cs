using System;
using System.Collections.Generic;
using UnityEngine;
using DaggerfallConnect.Save;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.Serialization;
namespace AdvancedNPCs
{
    [Serializable]
    public class CompanionInventoryData
    {
        public int version=1;
        public bool managedEquipment;
        public ulong bow,melee,shield;
        public ItemData_v1[] items;
        public ulong[] equipment;
        public int[] stats;
        public sbyte[] armor;
    }
    /// <summary>Live entity owns belongings; the contract holds a native-item snapshot between spawns.</summary>
    public class NpcCompanionInventory : MonoBehaviour
    {
        NpcBrain brain;
        public bool ManagedEquipment { get; set; }
        public ulong Bow, Melee, Shield;
        bool dirty=true;
        public int Captures {get;private set;}
        public void Invalidate() {dirty=true;}
        public bool Ready { get; private set; }
        public string Problem { get; private set; }
        public static NpcCompanionInventory For(NpcBrain person)
        {
            var inventory=person.GetComponent<NpcCompanionInventory>();
            if(inventory==null) { inventory=person.gameObject.AddComponent<NpcCompanionInventory>();inventory.Initialize(person); }
            return inventory;
        }
        void Initialize(NpcBrain person)
        {
            brain=person;
            try
            {
                var entity=brain.EntityBehaviour.Entity;
                string saved=brain.State.companion.belongings;
                if(!string.IsNullOrEmpty(saved))
                {
                    var data=SaveLoadManager.Deserialize(typeof(CompanionInventoryData),saved) as CompanionInventoryData;
                    if(data==null || data.version!=1 || data.items==null || data.stats==null || data.stats.Length!=8 || data.armor==null ||
                        data.armor.Length!=entity.ArmorValues.Length || data.equipment==null || data.equipment.Length!=entity.ItemEquipTable.SerializeEquipTable().Length)
                        throw new InvalidOperationException("Unsupported or damaged companion inventory.");
                    var restored=RestoreItems(data.items);
                    var worn=new HashSet<ulong>();
                    foreach(ulong id in data.equipment)
                        if(id!=0 && (!restored.Contains(id) || !worn.Add(id))) throw new InvalidOperationException("Invalid saved equipment reference.");
                    // Validate everything before replacing the newly generated kit.
                    CompanionEquipment.StopAll(entity);entity.ItemEquipTable.Clear();entity.Items.Clear();
                    for(int i=0;i<restored.Count;i++) entity.Items.AddItem(restored.GetItem(i),noStack:true);
                    entity.ItemEquipTable.DeserializeEquipTable(data.equipment,entity.Items);
                    entity.ArmorValues=(sbyte[])data.armor.Clone();
                    for(int i=0;i<8;i++) entity.Stats.SetPermanentStatValue(i,data.stats[i]);
                    ManagedEquipment=data.managedEquipment;Bow=data.bow;Melee=data.melee;Shield=data.shield;
                    if(ManagedEquipment)CompanionEquipment.RecalculateArmor(brain);
                    CompanionEquipment.ResumeEffects(entity);
                }
                Ready=true;Capture();
            }
            catch(Exception exception)
            {
                // Keep the original snapshot intact: a missing custom item mod must never erase its data.
                Ready=false;
                Problem="Belongings unavailable. Restore the required item mod or check the ANPC log.";
                AdvancedNpcsMod.LogError(brain.Id+": companion inventory preserved: "+exception.Message);
            }
        }
        public static ItemCollection RestoreItems(ItemData_v1[] data)
        {
            var ids=new HashSet<ulong>();
            foreach(var item in data)
                if(item.uid==0 || item.stackCount<=0 || !ids.Add(item.uid)) throw new InvalidOperationException("Invalid or duplicate saved item.");
            var items=new ItemCollection();items.DeserializeItems(data);
            if(items.Count!=data.Length) throw new InvalidOperationException("Incomplete item restoration.");
            for(int i=0;i<data.Length;i++)
                if(items.GetItem(data[i].uid).GetSaveData().className!=data[i].className)
                    throw new InvalidOperationException("Missing custom item class: "+data[i].className);
            return items;
        }
        public void Capture(bool force=true)
        {
            if(!Ready || brain.State.dead || brain.CurrentMode==NpcMode.Dead || brain.State.companion==null) return;
            // Real saves/teardown force a fresh native record, including changes made by other mods or native wear.
            // In-memory callers may reuse the snapshot until a known mutation invalidates it.
            if(!force && !dirty && !string.IsNullOrEmpty(brain.State.companion.belongings))return;
            if(ManagedEquipment)CompanionEquipment.RecalculateArmor(brain);
            var entity=brain.EntityBehaviour.Entity;
            var data=new CompanionInventoryData {items=entity.Items.SerializeItems(),equipment=entity.ItemEquipTable.SerializeEquipTable(),
                managedEquipment=ManagedEquipment,bow=Bow,melee=Melee,shield=Shield,stats=new int[8],armor=(sbyte[])entity.ArmorValues.Clone()};
            for(int i=0;i<8;i++) data.stats[i]=entity.Stats.GetPermanentStatValue(i);
            brain.State.companion.belongings=SaveLoadManager.Serialize(typeof(CompanionInventoryData),data,false);
            dirty=false;Captures++;
        }
    }
}

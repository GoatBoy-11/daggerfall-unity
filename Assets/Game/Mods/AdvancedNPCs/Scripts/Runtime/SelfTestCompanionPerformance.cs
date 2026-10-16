using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.Serialization;
using AdvancedNPCs.Core;
namespace AdvancedNPCs
{
    public partial class SelfTest
    {
        public const string CompanionPerformanceSuite="performance";
        static long AllocatedNow()
        {
            var method=typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Public);
            return method!=null?(long)method.Invoke(null,null):-1;
        }
        static string Metric(double value){return value.ToString("F4",System.Globalization.CultureInfo.InvariantCulture);}
        IEnumerator CompanionPerformanceSteps(DaggerfallLocation location,Transform player)
        {
            var gm=GameManager.Instance;int oldLimit=mod.Config.MaxFollowers;bool oldAI=gm.DisableAI,look=gm.PlayerMouseLook.enabled;
            Vector3 oldPosition=player.position;Quaternion oldRotation=player.rotation,oldView=gm.MainCamera.transform.rotation;
            var actors=new List<NpcBrain>();var floor=new GameObject("ANPC performance verification");floor.transform.SetParent(location.transform,false);
            Vector3 origin=oldPosition+Vector3.up*24;NavigationBox(floor.transform,"performance floor",origin-Vector3.up*.15f,new Vector3(100,.3f,70));
            try
            {
                if(mod.Companions.UsedSlots!=0){Fail("performance fixture requires no existing hired companions","use dedicated AtDaggerfall fixture");yield break;}
                gm.DisableAI=false;gm.PlayerMouseLook.enabled=false;mod.Config.MaxFollowers=0;
                var definition=mod.Catalog.Generics.Find(delegate(NpcDefinition d){return d.Id=="ironclad_rogue_1";});
                foreach(int count in new[]{1,3,5,10})
                {
                    NavigationPlayerAt(player,origin,Vector3.forward);
                    for(int i=0;i<count;i++)actors.Add(CombatPerson(location,definition,"selftest_perf_"+count+"_"+i,origin+new Vector3((i%5-2)*1.8f,1,-3-(i/5)*2),true));
                    yield return new WaitForSecondsRealtime(1.5f);
                    var frames=new List<float>();int gc=GC.CollectionCount(0);long allocated=AllocatedNow();
                    var watch=Stopwatch.StartNew();
                    for(int frame=0;frame<180;frame++)
                    {
                        if(frame%30==0)NavigationPlayerAt(player,origin+Vector3.forward*(frame/30),Vector3.forward);
                        yield return null;frames.Add(Time.unscaledDeltaTime*1000);
                    }
                    watch.Stop();long after=AllocatedNow();frames.Sort();
                    AdvancedNpcsMod.Log("COMPANION PERF party followers="+count+" frames="+frames.Count+" median_ms="+Metric(frames[frames.Count/2])+" p95_ms="+Metric(frames[(int)(frames.Count*.95)])+" elapsed_ms="+Metric(watch.Elapsed.TotalMilliseconds)+" allocated_bytes="+(allocated>=0?after-allocated:-1)+" gc0="+(GC.CollectionCount(0)-gc));
                    Check("profile party "+count+" retains one actor per contract",mod.Companions.UsedSlots==count && actors.TrueForAll(delegate(NpcBrain a){return a!=null && NpcBrain.Find(a.Id)==a;}),"slots="+mod.Companions.UsedSlots);
                    foreach(var actor in actors){mod.States.Remove(actor.Id);NpcBrain.Discard(actor);}actors.Clear();yield return new WaitForSecondsRealtime(.2f);
                }
                gm.DisableAI=true;NavigationPlayerAt(player,origin,Vector3.forward);
                var person=CombatPerson(location,definition,"selftest_perf_inventory",origin+new Vector3(0,1,-2),true);actors.Add(person);yield return new WaitForSecondsRealtime(.4f);
                var entity=person.EntityBehaviour.Entity;CompanionEquipment.StopAll(entity);entity.Items.Clear();var inventory=NpcCompanionInventory.For(person);
                foreach(int count in new[]{0,25,100,250})
                {
                    entity.Items.Clear();for(int i=0;i<count;i++){var item=ItemBuilder.CreateWeapon(Weapons.Dagger,WeaponMaterialTypes.Iron);item.shortName="Perf item "+i;entity.Items.AddItem(item,noStack:true);}
                    inventory.Capture();string snapshot=person.State.companion.belongings;int bytes=Encoding.UTF8.GetByteCount(snapshot);
                    long allocated=AllocatedNow();var watch=Stopwatch.StartNew();for(int i=0;i<100;i++)inventory.Capture(false);watch.Stop();long after=AllocatedNow();
                    var restore=Stopwatch.StartNew();for(int i=0;i<5;i++){var data=SaveLoadManager.Deserialize(typeof(CompanionInventoryData),snapshot) as CompanionInventoryData;NpcCompanionInventory.RestoreItems(data.items);}restore.Stop();
                    var save=new NpcSaveDataInterface(mod.States,mod.Placed,mod.Flags,mod.RaiseStateRestored);
                    string full=SaveLoadManager.Serialize(typeof(NpcSaveData),save.GetSaveData(),false);
                    AdvancedNpcsMod.Log("COMPANION PERF inventory items="+count+" inventory_bytes="+bytes+" mod_save_bytes="+Encoding.UTF8.GetByteCount(full)+" repeat100_ms="+Metric(watch.Elapsed.TotalMilliseconds)+" repeat100_allocated_bytes="+(allocated>=0?after-allocated:-1)+" restore_mean_ms="+Metric(restore.Elapsed.TotalMilliseconds/5));
                    Check("inventory "+count+" captures replace snapshot without history",person.State.companion.belongings==snapshot && entity.Items.Count==count,"bytes="+bytes);
                }
                inventory.Capture();int captures=inventory.Captures;string cached=person.State.companion.belongings;
                for(int i=0;i<100;i++)inventory.Capture(false);
                Check("unchanged inventory reuses cached snapshot without native serialization",inventory.Captures==captures && object.ReferenceEquals(cached,person.State.companion.belongings),"captures="+inventory.Captures);
                var external=entity.Items.GetItem(0);external.currentCondition--;inventory.Capture();
                var forced=SaveLoadManager.Deserialize(typeof(CompanionInventoryData),person.State.companion.belongings) as CompanionInventoryData;
                Check("forced capture includes native or external item changes without dirty notification",inventory.Captures==captures+1 && Array.Exists(forced.items,delegate(ItemData_v1 item){return item.uid==external.UID && item.hits1==external.currentCondition;}),"captures="+inventory.Captures);
                captures=inventory.Captures;external.stackCount++;inventory.Invalidate();inventory.Capture(false);
                Check("known inventory change invalidates and rebuilds cache once",inventory.Captures==captures+1,"captures="+inventory.Captures);
                var dummy=CombatPerson(location,definition,"selftest_perf_sight",origin+new Vector3(12,1,-2),false);actors.Add(dummy);yield return new WaitForSecondsRealtime(.3f);
                var combat=person.CompanionCombat;var clear=typeof(NpcCompanionCombat).GetMethod("Clear",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                var perceive=typeof(NpcCompanionCombat).GetMethod("Perceive",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                perceive.Invoke(combat,new object[]{false});int writes=combat.PerceptionWrites;
                for(int i=0;i<10;i++)perceive.Invoke(combat,new object[]{false});
                Check("unchanged native perception avoids repeated boxed reflection writes",combat.PerceptionWrites==writes,"writes="+(combat.PerceptionWrites-writes));
                typeof(DaggerfallWorkshop.Game.EnemySenses).GetField("distanceToTarget",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(person.GetComponent<DaggerfallWorkshop.Game.EnemySenses>(),7f);
                perceive.Invoke(combat,new object[]{false});
                Check("native perception changes are corrected despite write optimization",person.GetComponent<DaggerfallWorkshop.Game.EnemySenses>().DistanceToTarget==0 && combat.PerceptionWrites==writes+1,"writes="+(combat.PerceptionWrites-writes));
                int rays=combat.SightChecks;for(int i=0;i<10;i++)clear.Invoke(combat,new object[]{dummy.EntityBehaviour,false});
                Check("same-frame perception reuses one line-of-sight query",combat.SightChecks==rays+1,"rays="+(combat.SightChecks-rays));
                rays=combat.SightChecks;bool open=combat.ClearShot(dummy.EntityBehaviour);combat.ClearShot(dummy.EntityBehaviour);
                Check("actual launch checks always use fresh physics",open && combat.SightChecks==rays+2,"rays="+(combat.SightChecks-rays));
                Vector3 midpoint=(person.transform.position+dummy.transform.position)*.5f;
                NavigationBox(floor.transform,"fresh shot obstruction",midpoint,new Vector3(1,5,4));Physics.SyncTransforms();
                Check("new same-frame obstruction blocks a launch despite cached sight",!combat.ClearShot(dummy.EntityBehaviour),"fresh shot clear="+combat.ClearShot(dummy.EntityBehaviour));
            }
            finally
            {
                foreach(var actor in actors)if(actor!=null){mod.States.Remove(actor.Id);NpcBrain.Discard(actor);}UnityEngine.Object.Destroy(floor);
                mod.Config.MaxFollowers=oldLimit;gm.DisableAI=oldAI;gm.PlayerMouseLook.enabled=look;player.position=oldPosition;player.rotation=oldRotation;gm.MainCamera.transform.rotation=oldView;
            }
        }
    }
}

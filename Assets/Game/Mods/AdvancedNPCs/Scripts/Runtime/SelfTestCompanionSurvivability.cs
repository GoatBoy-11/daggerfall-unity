using System;
using System.Collections;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Utility;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    public partial class SelfTest
    {
        public const string CompanionSurvivabilitySuite="survivability";
        IEnumerator CompanionSurvivabilitySteps(DaggerfallLocation location,Transform player)
        {
            var gm=GameManager.Instance;
            Vector3 position=player.position;bool ai=gm.DisableAI,look=gm.PlayerMouseLook.enabled;
            bool protection=mod.Config.InvulnerableCompanions,potions=mod.Config.AutoHealingPotions;
            int gold=gm.PlayerEntity.GoldPieces;
            NpcBrain actor=null;GameObject native=null;
            var geometry=new GameObject("ANPC survivability verification");geometry.transform.SetParent(location.transform,false);
            Vector3 origin=position+Vector3.up*24;
            NavigationBox(geometry.transform,"survivability floor",origin-Vector3.up*.15f,new Vector3(50,.3f,30));
            try
            {
                gm.DisableAI=true;gm.PlayerMouseLook.enabled=false;gm.PlayerEntity.GoldPieces=10000;
                mod.Config.AutoHealingPotions=false;mod.Config.InvulnerableCompanions=false;
                NavigationPlayerAt(player,origin,Vector3.right);
                Check("invulnerability defaults off and loads through native settings",!new ModConfig().InvulnerableCompanions &&
                    mod.Config.FromSettings && !mod.Config.MissingKeys.Contains("InvulnerableCompanions"),"settings");
                native=GameObjectHelper.CreateEnemy("native preservation test",MobileTypes.Rogue,origin+Vector3.right*12);
                var behaviour=native.GetComponent<DaggerfallEntityBehaviour>();var original=(EnemyEntity)behaviour.Entity;
                var clone=new NpcEnemyEntity(behaviour,original);
                Check("health guard preserves native stats loot spells armor and equipment",clone.MaxHealth==original.MaxHealth &&
                    clone.CurrentHealth==original.CurrentHealth && clone.Level==original.Level && clone.CareerIndex==original.CareerIndex &&
                    clone.Stats==original.Stats && clone.Skills==original.Skills && clone.Items==original.Items &&
                    clone.ArmorValues==original.ArmorValues && clone.GetSpells().Length==original.GetSpells().Length &&
                    string.Join(",",Array.ConvertAll(clone.ItemEquipTable.SerializeEquipTable(),delegate(ulong id){return id.ToString();}))==
                    string.Join(",",Array.ConvertAll(original.ItemEquipTable.SerializeEquipTable(),delegate(ulong id){return id.ToString();})),"native preservation");
                UnityEngine.Object.Destroy(native);native=null;
                var definition=mod.Catalog.Generics.Find(delegate(NpcDefinition d){return d.Id=="ironclad_rogue_1";});
                actor=CombatPerson(location,definition,"selftest_survivability",origin+new Vector3(-2,1,0),false);
                yield return new WaitForSecondsRealtime(.5f);
                var entity=actor.EntityBehaviour.Entity;int baseline=entity.MaxHealth;
                entity.SetHealth(Math.Max(1,baseline/2),true);float fraction=entity.CurrentHealthPercent;
                Check("fixture recruits",mod.Companions.Command(actor,"recruit"),actor.Describe());
                int boosted=CombatRules.ScaleHealth(baseline,1.5f);
                Check("recruitment boosts max health by 1.5 and preserves wounds",entity.MaxHealth==boosted &&
                    entity.CurrentHealth==HealthRules.Restore(boosted,fraction),actor.Describe());
                int before=entity.CurrentHealth;entity.DecreaseHealth(1);
                Check("default mortal companion takes damage",entity.CurrentHealth==before-1,actor.Describe());
                mod.Config.InvulnerableCompanions=true;before=entity.CurrentHealth;
                entity.DecreaseHealth(100000);entity.CurrentHealth=0;
                Check("toggle immediately blocks ordinary and one hit lethal damage without healing",entity.CurrentHealth==before && !actor.State.dead,actor.Describe());
                PlayerHit(actor,gm.PlayerEntityBehaviour,100000);
                yield return new WaitForSecondsRealtime(.3f);
                Check("native player damage cannot kill protected companion",entity.CurrentHealth==before && !actor.State.dead,actor.Describe());
                mod.Companions.Command(actor,"wait");entity.DecreaseHealth(100000);
                Check("waiting companions retain boost and protection",entity.MaxHealth==boosted && entity.CurrentHealth==before && actor.State.companion.status=="waiting",actor.Describe());
                mod.Companions.Command(actor,"follow");
                Check("follow and wait never stack health",entity.MaxHealth==boosted && entity.CurrentHealth==before,actor.Describe());
                actor.CaptureState();
                string saved=SaveLoadManager.Serialize(typeof(NpcState),actor.State);
                string key=actor.Id;NpcBrain.Discard(actor);mod.States.Remove(key);actor=null;
                yield return new WaitForSecondsRealtime(.3f);
                var state=(NpcState)SaveLoadManager.Deserialize(typeof(NpcState),saved);
                mod.States.Keep(key,state);
                var person=state.companion.Person(key,definition);Vector3 local=location.transform.InverseTransformPoint(origin+Vector3.left*2);
                person.X=local.x;person.Y=local.y;person.Z=local.z;
                actor=spawner.SpawnChecked(person,state,location.transform,true);
                yield return new WaitForSecondsRealtime(.5f);entity=actor.EntityBehaviour.Entity;
                Check("save round trip preserves boosted health and wounds without stacking",entity.MaxHealth==boosted && entity.CurrentHealth==before &&
                    actor.State.companion.baseMaxHealth==baseline,actor.Describe());
                entity.DecreaseHealth(100000);
                Check("rebuilt companion remains protected",entity.CurrentHealth==before && !actor.State.dead,actor.Describe());
                mod.Config.InvulnerableCompanions=false;entity.DecreaseHealth(1);
                Check("turning protection off immediately restores damage",entity.CurrentHealth==before-1,actor.Describe());
                fraction=entity.CurrentHealthPercent;
                mod.Config.InvulnerableCompanions=true;mod.Companions.Command(actor,"dismiss");
                Check("dismissal removes health bonus and preserves wound percentage",entity.MaxHealth==baseline &&
                    entity.CurrentHealth==HealthRules.Restore(baseline,fraction),actor.Describe());
                before=entity.CurrentHealth;entity.DecreaseHealth(1);
                Check("dismissed person takes damage with toggle still enabled",entity.CurrentHealth==before-1,actor.Describe());
                mod.Companions.Command(actor,"recruit");
                Check("rehiring grants exactly one health bonus",entity.MaxHealth==boosted,actor.Describe());
                mod.Config.InvulnerableCompanions=false;entity.SetHealth(Math.Max(1,boosted/2),true);actor.CaptureState();
                actor.State.companion.maxHealth=baseline;actor.State.companion.baseMaxHealth=0;
                saved=SaveLoadManager.Serialize(typeof(NpcState),actor.State);
                NpcBrain.Discard(actor);mod.States.Remove(key);actor=null;yield return new WaitForSecondsRealtime(.3f);
                state=(NpcState)SaveLoadManager.Deserialize(typeof(NpcState),saved);mod.States.Keep(key,state);
                actor=spawner.SpawnChecked(person,state,location.transform,true);
                yield return new WaitForSecondsRealtime(.5f);entity=actor.EntityBehaviour.Entity;
                Check("legacy contract gains bonus from saved roll on actual spawn",entity.MaxHealth==boosted && state.companion.baseMaxHealth==baseline &&
                    entity.CurrentHealth==HealthRules.Restore(boosted,state.healthFraction),actor.Describe());
                mod.Config.InvulnerableCompanions=true;state.companion.status="dismissed";NpcCombat.ApplyCompanionHealth(actor);
                entity.DecreaseHealth(100000);yield return new WaitForSecondsRealtime(.3f);
                Check("dismissal restores native death even with protection enabled",state.dead && entity.CurrentHealth==0,actor.Describe());
                Check("protection does not revive a dead companion",!mod.Companions.Command(actor,"recruit"),actor.Describe());
            }
            finally
            {
                NpcBrain.Discard(actor);UnityEngine.Object.Destroy(native);UnityEngine.Object.Destroy(geometry);
                mod.Config.InvulnerableCompanions=protection;mod.Config.AutoHealingPotions=potions;
                gm.PlayerEntity.GoldPieces=gold;gm.DisableAI=ai;gm.PlayerMouseLook.enabled=look;player.position=position;
            }
        }
    }
}

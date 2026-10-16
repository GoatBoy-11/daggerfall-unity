using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DaggerfallConnect;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Utility;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    public partial class SelfTest
    {
        public const string CompanionSuite = "companions";
        IEnumerator CompanionSteps(DaggerfallLocation location, Transform player)
        {
            GameManager gm = GameManager.Instance;
            PlayerEnterExit pee = gm.PlayerEnterExit;
            int originalGold = gm.PlayerEntity.GoldPieces;
            Vector3 originalPosition = player.position;
            var originalPixel = gm.PlayerGPS.CurrentMapPixel;
            NpcDefinition definition = mod.Catalog.Generics.Find(delegate(NpcDefinition d) { return d.Id == "ironclad_rogue_1"; });
            if (definition == null) { Fail("mercenary setup", "ironclad_rogue_1 is not installed"); yield break; }
            const string key = "selftest_companion";
            ids.Add(key);
            NpcBrain actor = null;
            GameObject transferEnemy=null;
            try
            {
                Vector3 local = location.transform.InverseTransformPoint(player.position + player.right * 3f);
                var person = new NpcInstance { Key = key, Definition = definition, Name = "Ironclad Mercenary Test", Race = "Breton",
                    Gender = "Female", Seed = 12345, FaceOutfit = 1, FaceVariant = 2, Persistent = false,
                    HasFixedPosition = true, X = local.x, Y = local.y, Z = local.z };
                actor = spawner.SpawnChecked(person, new NpcState(), location.transform);
                yield return new WaitForSecondsRealtime(1f);
                Check("mercenary costs 100 and is 6 percent smaller than vanilla Rogue", definition.RecruitCost == 100 && definition.SpriteScale == 0.94f,
                    "price=" + definition.RecruitCost + "; scale=" + definition.SpriteScale + "; vanilla=" + NpcSprite.DefaultHeight(actor.gameObject));
                var suppliedVoice=actor.GetComponent<NpcVoice>();
                float voiceDeadline=Time.realtimeSinceStartup+15f;
                while(Time.realtimeSinceStartup<voiceDeadline && suppliedVoice.Set!=null && !suppliedVoice.Set.IsReady)yield return null;
                Check("Ironclad supplied companion voice samples decode in game",suppliedVoice.Set!=null && suppliedVoice.Set.IsReady &&
                    suppliedVoice.Set.Clips.ContainsKey(CompanionBarkRules.Bark) && suppliedVoice.Set.Clips[CompanionBarkRules.Bark].Count==3 &&
                    suppliedVoice.Set.Clips.ContainsKey(CompanionBarkRules.Dungeon) && suppliedVoice.Set.Clips[CompanionBarkRules.Dungeon].Count==2 &&
                    suppliedVoice.Set.Clips.ContainsKey(CompanionBarkRules.Inn) && suppliedVoice.Set.Clips[CompanionBarkRules.Inn].Count==3 &&
                    suppliedVoice.Set.Clips.ContainsKey(CompanionBarkRules.Hurt) && suppliedVoice.Set.Clips[CompanionBarkRules.Hurt].Count==2,
                    suppliedVoice.Set!=null?suppliedVoice.Set.Summary()+"; ready="+suppliedVoice.Set.IsReady+", users="+suppliedVoice.Set.Users+", clips="+string.Join(",",new List<string>(suppliedVoice.Set.Clips.Keys).ToArray()):"missing Sounds folder");
                bool eventClips=suppliedVoice.Set!=null && suppliedVoice.Set.IsReady;
                foreach(string pool in new[]{CompanionBarkRules.Recruit,CompanionBarkRules.Wait,CompanionBarkRules.Follow,CompanionBarkRules.Dismiss,CompanionBarkRules.Regroup,CompanionBarkRules.Combat,CompanionBarkRules.Victory})
                    eventClips &= suppliedVoice.Set!=null && suppliedVoice.Set.Clips.ContainsKey(pool) && suppliedVoice.Set.Clips[pool].Count>0;
                Check("Ironclad command and battle samples also decode",eventClips,suppliedVoice.Set!=null?suppliedVoice.Set.Summary():"missing sounds");
                gm.PlayerEntity.GoldPieces = 99;
                Check("insufficient gold rejects recruitment without charging", !mod.Companions.Command(actor,"recruit") &&
                    gm.PlayerEntity.GoldPieces == 99 && actor.State.companion == null, "gold=" + gm.PlayerEntity.GoldPieces);
                gm.PlayerEntity.GoldPieces = 100;
                yield return OpenTalk(actor);
                yield return UseTopic("Hire you (100 gold)");
                DaggerfallUI.Instance.PopToHUD();
                yield return new WaitForSecondsRealtime(0.6f);
                Check("real dialogue hires exact person and charges 100 once", actor.State.companion != null && gm.PlayerEntity.GoldPieces == 0 &&
                    actor.Instance.Persistent && mod.States.IsCompanion(key), actor.Describe());
                if (actor.State.companion == null) yield break;
                gm.PlayerEntity.GoldPieces = 200;
                actor.EntityBehaviour.Entity.Stats.SetPermanentStatValue(0,67);
                var keepsake=ItemBuilder.CreateItem(ItemGroups.Weapons,(int)Weapons.Dagger);keepsake.currentCondition=17;keepsake.weightInKg=.1f;
                gm.PlayerEntity.Items.AddItem(keepsake,noStack:true);string tradeMessage;
                Check("supplied keepsake enters companion inventory",CompanionTrade.Transfer(actor,true,keepsake.UID,1,out tradeMessage),tradeMessage);
                ulong keepsakeUid=keepsake.UID;
                Check("supplied weapon can be equipped before travelling",CompanionEquipment.SetEquipped(actor,keepsakeUid,true,out tradeMessage),tradeMessage);
                Check("repeated recruit never charges again", !mod.Companions.Command(actor,"recruit") && gm.PlayerEntity.GoldPieces == 200, actor.Describe());
                Check("original population spawn cannot duplicate a hired person", spawner.SpawnChecked(person,new NpcState(),location.transform) == null, key);
                actor.State.companion.barksMuted=true;actor.GetComponent<NpcVoice>().BarksChanged();
                actor.State.opinion = 23;
                actor.EntityBehaviour.Entity.CurrentHealth = Math.Max(1,actor.EntityBehaviour.Entity.MaxHealth / 2);
                actor.CaptureState();
                int maxHealth = actor.EntityBehaviour.Entity.MaxHealth;
                float health = actor.State.healthFraction;
                string name = actor.DisplayName;
                yield return OpenTalk(actor);
                yield return UseTopic("Wait here"); DaggerfallUI.Instance.PopToHUD();
                yield return new WaitForSecondsRealtime(0.5f);
                Check("wait command retains paid contract", actor.State.companion.status == "waiting" && gm.PlayerEntity.GoldPieces == 200, actor.Describe());
                DaggerfallStaticDoors doorOwner; StaticDoor door;
                if (!FindBuildingDoor(location,"tavern",out doorOwner,out door)) { Fail("companion tavern setup","no tavern"); yield break; }
                Vector3 waited = actor.transform.position;
                pee.TransitionInterior(doorOwner.transform,door,false);
                yield return new WaitForSecondsRealtime(1.5f);
                Check("waiting companion does not transfer with player", NpcBrain.Find(key) == null, key);
                pee.TransitionExterior(false);
                yield return new WaitForSecondsRealtime(1.5f);
                actor = NpcBrain.Find(key);
                Check("returning restores waiting person near saved position", actor != null && Vector3.Distance(actor.transform.position,waited)<2f &&
                    actor.State.companion.status == "waiting", actor != null ? actor.Describe() : "missing; record=" + SaveLoadManager.Serialize(typeof(CompanionRecord),mod.States.GetOrCreate(key).companion) + "; player=" + player.position);
                Check("belongings and Strength survive waiting area teardown",actor!=null && actor.EntityBehaviour.Entity.Items.GetItem(keepsakeUid)!=null && actor.EntityBehaviour.Entity.Items.GetItem(keepsakeUid).currentCondition==17 && actor.EntityBehaviour.Entity.Items.GetItem(keepsakeUid).IsEquipped && actor.EntityBehaviour.Entity.Stats.GetPermanentStatValue(0)==67,"keepsake="+keepsakeUid);
                if (actor == null) yield break;
                yield return OpenTalk(actor); yield return UseTopic("Follow me"); DaggerfallUI.Instance.PopToHUD();
                yield return new WaitForSecondsRealtime(0.5f);
                Check("resume is free and selects follow", actor.State.companion.status == "following" && gm.PlayerEntity.GoldPieces == 200 &&
                    actor.PackageDescription.Contains("companion following"), actor.Describe());
                mod.Companions.Command(actor,"passive");
                // Exercise area teardown with an actual combat mode and a transient native enemy reference.
                transferEnemy=GameObjectHelper.CreateEnemy("ANPC transfer combat fixture",MobileTypes.Orc,
                    location.transform.InverseTransformPoint(actor.transform.position+Vector3.right*3),MobileGender.Unspecified,location.transform,MobileReactions.Hostile);
                transferEnemy.GetComponent<EnemyMotor>().enabled=false;transferEnemy.GetComponent<EnemyAttack>().enabled=false;transferEnemy.GetComponent<EnemySenses>().enabled=false;
                actor.CompanionMode(transferEnemy.GetComponent<DaggerfallWorkshop.Game.Entity.DaggerfallEntityBehaviour>(),NpcMode.Fighting);
                bool transferringFight=actor.CurrentMode==NpcMode.Fighting;
                pee.TransitionInterior(doorOwner.transform,door,false);
                yield return new WaitForSecondsRealtime(1.5f);
                float arrivalDeadline = Time.realtimeSinceStartup + 8f;
                while (NpcBrain.Find(key) == null && Time.realtimeSinceStartup < arrivalDeadline) yield return null;
                actor = NpcBrain.Find(key);
                Check("companion enters building once with saved identity and health", actor != null && actor.transform.parent == pee.Interior.transform &&
                    actor.DisplayName == name && actor.Instance.Seed == 12345 && actor.State.opinion == 23 && actor.State.companion.stance=="passive" &&
                    actor.EntityBehaviour.Entity.MaxHealth == maxHealth && Math.Abs(actor.State.healthFraction-health)<0.02f && CountObjects(key)==1,
                    actor != null ? actor.Describe() : "missing");
                if (actor == null) yield break;
                Check("bark preference survives real tavern entry",actor.State.companion.barksMuted && NpcVoice.BarkContext()==CompanionBarkRules.Inn,"muted="+actor.State.companion.barksMuted);
                Check("area entry safely interrupts companion combat",transferringFight && actor.CurrentMode==NpcMode.Calm && actor.Threat==null &&
                    actor.State.companion.status=="following" && Math.Abs(actor.State.healthFraction-health)<.02f,actor.Describe());
                Check("belongings and Strength survive building entry",actor!=null && actor.EntityBehaviour.Entity.Items.GetItem(keepsakeUid)!=null && actor.EntityBehaviour.Entity.Items.GetItem(keepsakeUid).currentCondition==17 && actor.EntityBehaviour.Entity.Items.GetItem(keepsakeUid).IsEquipped && actor.EntityBehaviour.Entity.Stats.GetPermanentStatValue(0)==67,"keepsake="+keepsakeUid);
                UnityEngine.Object.Destroy(transferEnemy);transferEnemy=null;
                var save = new NpcSaveDataInterface(mod.States,mod.Placed,mod.Flags,mod.RaiseStateRestored);
                NpcSaveData before = (NpcSaveData)save.GetSaveData();
                string text = SaveLoadManager.Serialize(typeof(NpcSaveData),before);
                save.RestoreSaveData(SaveLoadManager.Deserialize(typeof(NpcSaveData),text));
                yield return new WaitForSecondsRealtime(1.5f);
                actor = NpcBrain.Find(key);
                Check("mod save round trip restores recruited person once inside building", actor != null && actor.State.companion.status=="following" &&
                    actor.DisplayName==name && actor.State.companion.stance=="passive" && CountObjects(key)==1 && gm.PlayerEntity.GoldPieces==200, actor != null ? actor.Describe() : "missing");
                Check("bark preference survives body reconstruction on load",actor!=null && actor.State.companion.barksMuted,"companion="+(actor!=null));
                Check("belongings and Strength survive mod save restoration",actor!=null && actor.EntityBehaviour.Entity.Items.GetItem(keepsakeUid)!=null && actor.EntityBehaviour.Entity.Items.GetItem(keepsakeUid).currentCondition==17 && actor.EntityBehaviour.Entity.Items.GetItem(keepsakeUid).IsEquipped && actor.EntityBehaviour.Entity.Stats.GetPermanentStatValue(0)==67,"keepsake="+keepsakeUid);
                pee.TransitionExterior(false);
                yield return new WaitForSecondsRealtime(1.5f);
                actor = NpcBrain.Find(key);
                Check("companion exits building once", actor != null && actor.isActiveAndEnabled && CountObjects(key)==1, key);
                DFLocation dungeon;
                if (FindDungeon(out dungeon))
                {
                    pee.StartDungeonInterior(dungeon);
                    yield return new WaitForSecondsRealtime(2f);
                    actor=NpcBrain.Find(key);
                    Check("companion enters dungeon once", actor!=null && actor.transform.parent==pee.Dungeon.transform && CountObjects(key)==1,
                        actor!=null ? actor.Describe() : "missing");
                Check("belongings and Strength survive dungeon entry",actor!=null && actor.EntityBehaviour.Entity.Items.GetItem(keepsakeUid)!=null && actor.EntityBehaviour.Entity.Items.GetItem(keepsakeUid).currentCondition==17 && actor.EntityBehaviour.Entity.Items.GetItem(keepsakeUid).IsEquipped && actor.EntityBehaviour.Entity.Stats.GetPermanentStatValue(0)==67,"keepsake="+keepsakeUid);
                    SaveLoadManager.Instance.Save(gm.PlayerEntity.Name,"ANPC follower verification 20261015",true);
                    float deadline=Time.realtimeSinceStartup+60;
                    while(Time.realtimeSinceStartup<deadline && !SaveLoadManager.Instance.LoadInProgress) yield return null;
                    while(Time.realtimeSinceStartup<deadline && SaveLoadManager.Instance.LoadInProgress) yield return null;
                    yield return new WaitForSecondsRealtime(2f);
                    DaggerfallUI.Instance.PopToHUD(); gm.PlayerEntity.GodMode=true;
                    actor=NpcBrain.Find(key);
                    Check("real dungeon save and load preserves contract identity health and one copy", actor!=null && pee.IsPlayerInsideDungeon &&
                        actor.DisplayName==name && actor.State.companion.stance=="passive" && actor.State.companion.status=="following" && actor.EntityBehaviour.Entity.MaxHealth==maxHealth && CountObjects(key)==1,
                        actor!=null ? actor.Describe() : "missing");
                Check("belongings and Strength survive real disk save and load",actor!=null && actor.EntityBehaviour.Entity.Items.GetItem(keepsakeUid)!=null && actor.EntityBehaviour.Entity.Items.GetItem(keepsakeUid).currentCondition==17 && actor.EntityBehaviour.Entity.Items.GetItem(keepsakeUid).IsEquipped && actor.EntityBehaviour.Entity.Stats.GetPermanentStatValue(0)==67,"keepsake="+keepsakeUid);
                    pee.TransitionDungeonExterior(false);
                    yield return new WaitForSecondsRealtime(2f);
                }
                else Fail("companion dungeon setup","no dungeon");
                gm.StreamingWorld.TeleportToCoordinates(originalPixel.X+1,originalPixel.Y);
                yield return new WaitForSecondsRealtime(5f);
                actor=NpcBrain.Find(key);
                Check("world rebuild transfers follower into wilderness once", actor!=null && actor.isActiveAndEnabled &&
                    !pee.IsPlayerInside && actor.DisplayName==name && CountObjects(key)==1 && Vector3.Distance(actor.transform.position,player.position)<8f, actor!=null ? actor.Describe() : "missing");
                Check("belongings and Strength survive wilderness world rebuild",actor!=null && actor.EntityBehaviour.Entity.Items.GetItem(keepsakeUid)!=null && actor.EntityBehaviour.Entity.Items.GetItem(keepsakeUid).currentCondition==17 && actor.EntityBehaviour.Entity.Items.GetItem(keepsakeUid).IsEquipped && actor.EntityBehaviour.Entity.Stats.GetPermanentStatValue(0)==67,"keepsake="+keepsakeUid);
                gm.StreamingWorld.TeleportToCoordinates(originalPixel.X,originalPixel.Y);
                yield return new WaitForSecondsRealtime(5f);
                actor=NpcBrain.Find(key);
                Check("return world rebuild preserves recruited follower", actor!=null && CountObjects(key)==1, key);
                if(actor==null) yield break;
                Check("dismissal ends contract without a refund",mod.Companions.Command(actor,"dismiss") && actor.State.companion.status=="dismissed" &&
                    gm.PlayerEntity.GoldPieces==200,key);
                Check("new contract charges a fresh fee",mod.Companions.Command(actor,"recruit") && gm.PlayerEntity.GoldPieces==100,key);
                Check("belongings and Strength survive dismissal and rehire",actor!=null && actor.EntityBehaviour.Entity.Items.GetItem(keepsakeUid)!=null && actor.EntityBehaviour.Entity.Items.GetItem(keepsakeUid).currentCondition==17 && actor.EntityBehaviour.Entity.Items.GetItem(keepsakeUid).IsEquipped && actor.EntityBehaviour.Entity.Stats.GetPermanentStatValue(0)==67,"keepsake="+keepsakeUid);
                actor.EntityBehaviour.Entity.CurrentHealth=0;
                yield return new WaitForSecondsRealtime(0.6f);
                NpcBrain.Discard(actor);
                yield return new WaitForSecondsRealtime(0.6f);
                Check("dead recruited companion is never respawned",mod.States.GetOrCreate(key).dead && NpcBrain.Find(key)==null,key);
            }
            finally
            {
                UnityEngine.Object.Destroy(transferEnemy);
                DaggerfallUI.Instance.PopToHUD();
                NpcBrain.Discard(NpcBrain.Find(key)); mod.States.Remove(key);
                gm.PlayerEntity.GoldPieces=originalGold;
                if(pee.IsPlayerInsideBuilding) pee.TransitionExterior(false);
                if(pee.IsPlayerInsideDungeon) pee.TransitionDungeonExterior(false);
            }
        }
    }
}

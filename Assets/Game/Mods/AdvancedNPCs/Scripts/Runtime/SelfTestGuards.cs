using System.Collections;
using System.Reflection;
using UnityEngine;
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    public partial class SelfTest
    {
        public const string GuardSuite = "guards";
        IEnumerator GuardSteps(DaggerfallLocation location, Transform player)
        {
            GameManager gm = GameManager.Instance;
            PlayerEntity entity = gm.PlayerEntity;
            Vector3 before = player.position;
            bool oldAI = gm.DisableAI, look = gm.PlayerMouseLook.enabled;
            PlayerEntity.Crimes oldCrime = entity.CrimeCommitted;
            bool shown = entity.HaveShownSurrenderToGuardsDialogue;
            int region = gm.PlayerGPS.CurrentRegionIndex;
            short legal = entity.RegionData[region].LegalRep;
            int oldHealth = entity.CurrentHealth, oldMaxHealth = entity.MaxHealth;
            bool god = entity.GodMode;
            FactionFile.FactionData people;
            bool hasPeople = entity.FactionData.FindFactionByTypeAndRegion(15, region, out people);
            var geometry = new GameObject("ANPC guard verification");
            geometry.transform.SetParent(location.transform, false);
            Vector3 origin = before + Vector3.up * 24;
            NavigationBox(geometry.transform, "guard floor", origin - Vector3.up * 0.15f, new Vector3(60, 0.3f, 40));
            NpcBrain guard = null, second = null, suspect = null;
            try
            {
                gm.DisableAI = false; gm.PlayerMouseLook.enabled = false;
                player.position = origin + Vector3.up;
                entity.CrimeCommitted = PlayerEntity.Crimes.None;
                GuardArrest.Reset();
                NpcDefinition duty = TestDefinition("selftest_law_guard", Bravery.Normal, location, player, 0);
                duty.Guard = true; duty.BaseClass = "Knight"; duty.AggroRange = 15;
                guard = CombatPerson(location, duty, duty.Id, origin + Vector3.right * 3, false);
                NpcDefinition civilian = TestDefinition("selftest_law_suspect", Bravery.Normal, location, player, 0);
                civilian.BaseClass = "Warrior";
                suspect = CombatPerson(location, civilian, civilian.Id, origin + Vector3.right * 8, false);
                yield return new WaitForSeconds(0.7f);
                Check("guard role retains its normal class", guard.Guard != null &&
                    ((EnemyEntity)guard.EntityBehaviour.Entity).MobileEnemy.ID == (int)MobileTypes.Knight, guard.Describe());
                Check("guard ignores innocent player and civilian", !guard.EnforcingLaw, guard.Describe());
                Check("explicit NPC crime is accepted", NpcCrime.ReportNpc(suspect.Id, PlayerEntity.Crimes.Assault), suspect.Describe());
                NpcInstance person = suspect.Instance; NpcState state = suspect.State;
                float deadline = Time.time + 6;
                while (NpcBrain.Find(person.Key) != null && Time.time < deadline) yield return null;
                Check("guard pursues and arrests surrendering NPC", state.arrestedUntil > DaggerfallUnity.Instance.WorldTime.Now.ToSeconds() && !state.dead,
                    guard.Describe());
                Check("arrest clears guilt and prevents spawning", state.wantedCrime == 0 && spawner.SpawnChecked(person, state, location.transform) == null, person.Key);
                NpcSaveData saved = (NpcSaveData)new NpcSaveDataInterface(mod.States, mod.Placed, mod.Flags, delegate { }).GetSaveData();
                string json = SaveLoadManager.Serialize(typeof(NpcSaveData), saved);
                NpcSaveData restored = SaveLoadManager.Deserialize(typeof(NpcSaveData), json) as NpcSaveData;
                Check("custody survives actual DFU serialization", restored != null && restored.States.ContainsKey(person.Key) &&
                    restored.States[person.Key].arrestedUntil == state.arrestedUntil, json);
                state.arrestedUntil = 0;
                suspect = spawner.SpawnChecked(person, state, location.transform);
                yield return Settle;
                Check("released NPC respawns alive and innocent", suspect != null && !suspect.State.dead && suspect.State.wantedCrime == 0, person.Key);
                civilian.Bravery = Bravery.Brave;
                suspect.EntityBehaviour.Entity.MaxHealth = 500;
                suspect.EntityBehaviour.Entity.CurrentHealth = 500;
                suspect.enabled = false;
                suspect.Motor.enabled = false;
                suspect.GetComponent<EnemyAttack>().enabled = false;
                suspect.GetComponent<EnemySenses>().enabled = false;
                guard.Teleport(origin + Vector3.right * 6);
                suspect.Teleport(origin + Vector3.right * 7.2f);
                NpcCrime.ReportNpc(suspect.Id, PlayerEntity.Crimes.Assault);
                yield return new WaitForSeconds(0.7f);
                Check("healthy brave suspect resists arrest", guard.EnforcingLaw && state.arrestedUntil == 0, guard.Describe());
                int healthy = suspect.EntityBehaviour.Entity.CurrentHealth;
                for (int blow = 0; blow < 12 && suspect.EntityBehaviour.Entity.CurrentHealth == healthy; blow++)
                {
                    guard.GetComponentInChildren<MobileUnit>().DoMeleeDamage = true;
                    yield return new WaitForSeconds(0.1f);
                }
                Check("resisting suspect takes real native melee damage", suspect.EntityBehaviour.Entity.CurrentHealth < healthy, suspect.Status);
                suspect.EntityBehaviour.Entity.CurrentHealth = 100;
                yield return new WaitForSeconds(0.5f);
                Check("wounded brave suspect is arrested alive", state.arrestedUntil > DaggerfallUnity.Instance.WorldTime.Now.ToSeconds() && !state.dead, guard.Describe());
                suspect = null;

                guard.Teleport(player.position + Vector3.right * 1.2f);
                duty = TestDefinition("selftest_law_guard_two", Bravery.Brave, location, player, 0);
                duty.Guard = true; duty.BaseClass = "Knight";
                second = CombatPerson(location, duty, duty.Id, player.position + Vector3.left * 1.2f, false);
                yield return Settle;
                entity.RegionData[region].LegalRep = 0;
                entity.HaveShownSurrenderToGuardsDialogue = false;
                entity.CrimeCommitted = PlayerEntity.Crimes.Theft;
                FieldInfo boxField = typeof(GuardArrest).GetField("prompt", BindingFlags.Static | BindingFlags.NonPublic);
                float realtime = Time.realtimeSinceStartup + 5;
                while (boxField.GetValue(null) == null && Time.realtimeSinceStartup < realtime) yield return null;
                var box = boxField.GetValue(null) as DaggerfallMessageBox;
                Check("wanted player receives surrender offer", box != null && (guard.EnforcingLaw || second.EnforcingLaw), guard.Describe());
                short charged = entity.RegionData[region].LegalRep;
                if (box != null)
                {
                    NpcBrain presenter = guard.EnforcingLaw ? guard : second;
                    NpcBrain other = presenter == guard ? second : guard;
                    GuardArrest.Demand(other);
                    Check("two guards share one prompt and reputation charge", boxField.GetValue(null) == box &&
                        entity.RegionData[region].LegalRep == charged && charged < 0, "legal=" + charged);
                    typeof(GuardArrest).GetMethod("Answer", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
                        new object[] { box, DaggerfallMessageBox.MessageBoxButtons.No });
                    yield return new WaitForSecondsRealtime(0.15f);
                    Check("refusal permits combat without another charge", GuardArrest.Demand(other) &&
                        entity.RegionData[region].LegalRep == charged, "legal=" + entity.RegionData[region].LegalRep);
                    entity.MaxHealth = 1000; entity.CurrentHealth = 1000; entity.GodMode = false;
                    for (int blow = 0; blow < 12 && entity.CurrentHealth == 1000; blow++)
                    {
                        presenter.GetComponentInChildren<MobileUnit>().DoMeleeDamage = true;
                        yield return new WaitForSeconds(0.1f);
                    }
                    Check("refusing player takes real guard melee damage", entity.CurrentHealth < 1000 && entity.CurrentHealth > 0,
                        "health=" + entity.CurrentHealth);
                    entity.GodMode = true;
                }
                entity.CrimeCommitted = PlayerEntity.Crimes.None;
                yield return new WaitForSeconds(0.7f);
                Check("cleared player crime ends both pursuits", !guard.EnforcingLaw && !second.EnforcingLaw &&
                    !guard.State.hostile && !guard.Motor.IsHostile, guard.Describe());
                Check("persistent guard survives crime clearing", guard.isActiveAndEnabled && guard.CurrentMode == NpcMode.Calm, guard.Describe());
                entity.CrimeCommitted = PlayerEntity.Crimes.Theft;
                entity.HaveShownSurrenderToGuardsDialogue = true;
                yield return new WaitForSecondsRealtime(0.7f);
                var lastBox = boxField.GetValue(null) as DaggerfallMessageBox;
                if (lastBox != null)
                {
                    typeof(GuardArrest).GetMethod("Answer", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
                        new object[] { lastBox, DaggerfallMessageBox.MessageBoxButtons.No });
                    yield return new WaitForSecondsRealtime(0.15f);
                }
                NpcBrain pursued = guard.EnforcingLaw ? guard : second;
                NpcBrain attacker = pursued == guard ? second : guard;
                Check("death attribution setup has a pursuing guard", pursued.EnforcingLaw, pursued.Describe());
                int reported = crimes.Count;
                CreatureHit(pursued, attacker.EntityBehaviour, pursued.EntityBehaviour.Entity.CurrentHealth + 1);
                yield return new WaitForSeconds(0.5f);
                Check("creature killing pursuing guard does not blame player", crimes.Count == reported, CrimeList());
                PlayerHit(attacker, gm.PlayerEntityBehaviour, attacker.EntityBehaviour.Entity.CurrentHealth + 1);
                yield return new WaitForSeconds(0.5f);
                Check("player killing guard still reports murder", crimes.Count == reported + 1 && crimes[reported] == PlayerEntity.Crimes.Murder, CrimeList());
            }
            finally
            {
                GuardArrest.Reset();
                NpcBrain.Discard(guard); NpcBrain.Discard(second); NpcBrain.Discard(suspect);
                Object.Destroy(geometry);
                entity.CrimeCommitted = oldCrime;
                entity.HaveShownSurrenderToGuardsDialogue = shown;
                entity.RegionData[region].LegalRep = legal;
                entity.MaxHealth = oldMaxHealth; entity.CurrentHealth = oldHealth; entity.GodMode = god;
                if (hasPeople) entity.FactionData.FactionDict[people.id] = people;
                player.position = before; gm.DisableAI = oldAI; gm.PlayerMouseLook.enabled = look;
            }
        }
    }
}

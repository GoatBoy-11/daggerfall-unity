using System.Collections;
using System.Collections.Generic;
using System.IO;
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
            int voiceMode = mod.Config.VoiceMode;
            AudioClip haltClip = null;
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
                string soundDir = Path.Combine(Application.temporaryCachePath, "anpc_guard_halt");
                Directory.CreateDirectory(soundDir);
                string haltFile = Path.Combine(soundDir, "halt.wav");
                File.WriteAllBytes(haltFile, ToneWav(0.1f));
                LoadedSoundSet haltSet = SoundLibrary.IndexSet("selftest guard/Sounds", soundDir);
                NpcVoice voice = guard.GetComponent<NpcVoice>();
                mod.Config.VoiceMode = VoiceModes.Anpc;
                duty.Sounds.GreetChance = duty.Sounds.CombatChance = 0;
                voice.Swap(haltSet);
                // No library owns this fixture: finish loading explicitly after the pursuit starts.
                suspect.EntityBehaviour.Entity.MaxHealth = 500;
                suspect.EntityBehaviour.Entity.CurrentHealth = 500;
                suspect.enabled = false;
                suspect.Motor.enabled = false;
                suspect.GetComponent<EnemyAttack>().enabled = false;
                suspect.GetComponent<EnemySenses>().enabled = false;
                Check("explicit NPC crime is accepted", NpcCrime.ReportNpc(suspect.Id, PlayerEntity.Crimes.Assault), suspect.Describe());
                NpcInstance person = suspect.Instance; NpcState state = suspect.State;
                NpcSaveData saved = (NpcSaveData)new NpcSaveDataInterface(mod.States, mod.Placed, mod.Flags, delegate { }).GetSaveData();
                string json = SaveLoadManager.Serialize(typeof(NpcSaveData), saved);
                string legacy = json.Replace("\"wantedCrime\":", "\"arrestedUntil\":999999999,\"wantedCrime\":");
                Check("legacy custody fixture contains the removed field", legacy != json, json);
                NpcSaveData restored = SaveLoadManager.Deserialize(typeof(NpcSaveData), legacy) as NpcSaveData;
                Check("wanted offence survives actual DFU serialization", restored != null && restored.States.ContainsKey(person.Key) &&
                    restored.States[person.Key].wantedCrime == state.wantedCrime, json);
                yield return new WaitForSeconds(0.7f);
                Check("guard pursues non-brave offender without arresting", guard.EnforcingLaw && !state.dead && state.wantedCrime != 0, guard.Describe());
                Check("halt waits for custom audio instead of using default", voice.Played(SoundPools.Halt) == 0 && voice.Played("vanilla_halt") == 0, voice.Set.Summary());
                string problem;
                haltClip = SoundLibrary.LoadClip(haltFile, out problem);
                Check("halt WAV decodes", haltClip != null, problem ?? "decoded");
                if (haltClip == null) yield break;
                haltSet.Clips[SoundPools.Halt] = new List<AudioClip> { haltClip };
                haltSet.IsReady = true;
                yield return new WaitForSeconds(0.15f);
                Check("custom halt plays once despite zero voice chances", voice.Played(SoundPools.Halt) == 1 && voice.Played("vanilla_halt") == 0, "halt=" + voice.Played(SoundPools.Halt));
                FieldInfo haltTimer = typeof(NpcGuard).GetField("haltWait", BindingFlags.Instance | BindingFlags.NonPublic);
                float wait = (float)haltTimer.GetValue(guard.Guard);
                Check("pursuit halt delay is less frequent than vanilla", wait > 14 && wait <= 30, "delay=" + wait);
                haltTimer.SetValue(guard.Guard, 0.05f);
                gm.DisableAI = true;
                yield return new WaitForSeconds(0.15f);
                Check("disabled AI freezes pursuit halt timer", (float)haltTimer.GetValue(guard.Guard) == 0.05f &&
                    voice.Played(SoundPools.Halt) == 1, "halt=" + voice.Played(SoundPools.Halt));
                gm.DisableAI = false;
                yield return new WaitForSeconds(0.15f);
                wait = (float)haltTimer.GetValue(guard.Guard);
                Check("pursuit repeats custom halt and reschedules", voice.Played(SoundPools.Halt) == 2 &&
                    wait > 14 && wait <= 30, "halt=" + voice.Played(SoundPools.Halt) + "; delay=" + wait);
                voice.Swap(null);
                haltTimer.SetValue(guard.Guard, 0f);
                yield return new WaitForSeconds(0.15f);
                Check("pursuit repeats vanilla halt without custom clip", voice.Played("vanilla_halt") == 1,
                    "vanilla=" + voice.Played("vanilla_halt"));
                voice.Swap(haltSet);
                haltTimer.SetValue(guard.Guard, 0f);
                state.wantedCrime = 0;
                yield return new WaitForSeconds(0.15f);
                Check("cleared NPC offence cancels overdue halt", !guard.EnforcingLaw &&
                    voice.Played(SoundPools.Halt) == 2 && voice.Played("vanilla_halt") == 1, guard.Describe());
                state.wantedCrime = (int)PlayerEntity.Crimes.Assault;
                yield return new WaitForSeconds(0.7f);
                wait = (float)haltTimer.GetValue(guard.Guard);
                Check("new pursuit shouts immediately and resets halt delay", guard.EnforcingLaw &&
                    voice.Played(SoundPools.Halt) == 3 && wait > 14 && wait <= 30, guard.Describe());
                int healthy = suspect.EntityBehaviour.Entity.CurrentHealth;
                for (int blow = 0; blow < 12 && suspect.EntityBehaviour.Entity.CurrentHealth == healthy; blow++)
                {
                    guard.GetComponentInChildren<MobileUnit>().DoMeleeDamage = true;
                    yield return new WaitForSeconds(0.1f);
                }
                Check("non-brave offender takes real native melee damage", suspect.EntityBehaviour.Entity.CurrentHealth < healthy, suspect.Status);
                suspect.EntityBehaviour.Entity.CurrentHealth = 1;
                for (int blow = 0; blow < 24 && !state.dead; blow++)
                {
                    guard.GetComponentInChildren<MobileUnit>().DoMeleeDamage = true;
                    yield return new WaitForSeconds(0.1f);
                }
                Check("guard kills wounded offender instead of taking custody", state.dead, guard.Describe());
                Check("ordinary death prevents offender respawning", spawner.SpawnChecked(person, state, location.transform) == null, person.Key);
                NpcBrain.Discard(suspect); suspect = null;
                NpcState legacyState = restored.States[person.Key];
                legacyState.wantedCrime = 0;
                suspect = spawner.SpawnChecked(person, legacyState, location.transform);
                yield return Settle;
                Check("old custody record no longer prevents spawning", suspect != null && !suspect.State.dead, person.Key);
                NpcBrain.Discard(suspect); suspect = null;
                int custom = voice.Played(SoundPools.Halt), native = voice.Played("vanilla_halt");
                mod.Config.VoiceMode = VoiceModes.Off;
                voice.PlayHalt();
                Check("voices off mutes halt including fallback", voice.Played(SoundPools.Halt) == custom && voice.Played("vanilla_halt") == native, "off");
                mod.Config.VoiceMode = VoiceModes.Vanilla;
                voice.PlayHalt();
                Check("vanilla voice setting uses default halt", voice.Played(SoundPools.Halt) == custom && voice.Played("vanilla_halt") == native + 1, "vanilla");
                mod.Config.VoiceMode = VoiceModes.Anpc;
                voice.Swap(null);
                voice.PlayHalt();
                Check("missing custom halt uses default", voice.Played("vanilla_halt") == native + 2, "no Sounds folder");
                voice.Swap(new LoadedSoundSet { IsReady = true });
                voice.PlayHalt();
                Check("missing halt pool uses default", voice.Played("vanilla_halt") == native + 3, "empty Sounds folder");
                haltSet.Clips.Clear();
                voice.Swap(haltSet);
                voice.PlayHalt();
                Check("failed custom halt uses default", voice.Played("vanilla_halt") == native + 4, "no decoded halt clips");
                voice.Swap(null);

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
                if (haltClip != null) Object.Destroy(haltClip);
                mod.Config.VoiceMode = voiceMode;
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

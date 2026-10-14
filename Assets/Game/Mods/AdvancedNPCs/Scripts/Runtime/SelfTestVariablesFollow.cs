using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    public partial class SelfTest
    {
        IEnumerator VariableFollowSteps(DaggerfallLocation location, Transform player)
        {
            DiscardTestPeople();
            string error;
            AiPackage package=PackageParser.Parse("selftest_follow","{\"type\":\"follow\"}",out error);
            AiPackage stand=PackageParser.Parse("selftest_follow_stand","{\"type\":\"stand\"}",out error);
            mod.Packages.AddForTest(package); mod.Packages.AddForTest(stand);
            NpcBrain actor=null, other=null;
            try
            {
                NpcDefinition d=TestDefinition("selftest_follower",Bravery.Prudent,location,player,OutOfGreeting);
                d.BaseClass="Warrior";
                d.Variables.Add("following",new NpcVariableValue(false));
                d.Variables.Add("times_recruited",new NpcVariableValue(0));
                var tests=Json.Parse("{\"following\":true}") as Dictionary<string,object>;
                NpcVariableCondition when=NpcVariableCondition.Read(tests,out error);
                d.Schedule.Add(new AiScheduleEntry {Package=package.Id,When=when});
                d.Schedule.Add(new AiScheduleEntry {Package=stand.Id});
                d.OwnDialogue=DialogueParser.Parse("selftest_follower/dialogue.json",
                    "{\"topics\":[{\"caption\":\"Follow me\",\"when\":{\"variables\":{\"following\":false}},\"answers\":[{\"text\":\"Lead the way.\",\"setVariables\":{\"following\":true},\"addVariables\":{\"times_recruited\":1}}]},"+
                    "{\"caption\":\"Wait here\",\"when\":{\"variables\":{\"following\":true}},\"answers\":[{\"text\":\"I will wait.\",\"setVariables\":{\"following\":false}}]}]}").File;
                actor=SpawnDefinition(d,location);
                NpcDefinition otherDef=TestDefinition("selftest_follower_other",Bravery.Prudent,location,player,OutOfGreeting+3);
                otherDef.BaseClass="Warrior";otherDef.Variables=d.Variables;otherDef.Schedule=d.Schedule;
                other=SpawnDefinition(otherDef,location);
                yield return Settle;
                Check("conditional follow starts on normal fallback", actor.PackageDescription.Contains(stand.Id) && other.PackageDescription.Contains(stand.Id), actor.Describe());
                yield return OpenTalk(actor);
                yield return UseTopic("Follow me");
                NpcVariableValue following,count;
                bool recruited=actor.TryGetVariable("following",out following) && following.booleanValue;
                actor.TryGetVariable("times_recruited",out count);
                Check("actual unique dialogue recruits and increments NPC integer", recruited && count.integerValue==1, actor.Describe());
                DaggerfallUI.Instance.PopToHUD();
                yield return null;
                float until=Time.time+4f;
                bool ran=false;
                while (Time.time<until)
                {
                    ran|=actor.PackageRunning;
                    yield return null;
                }
                Check("dialogue condition selects follow and approaches player", actor.PackageDescription.Contains(package.Id) && Vector3.Distance(actor.transform.position,player.position)<3.2f,
                    "distance="+Vector3.Distance(actor.transform.position,player.position)+"; "+actor.PackageDescription);
                Check("follow uses running when distant",ran,actor.Describe());
                other.TryGetVariable("following",out following);
                Check("recruitment changes only addressed entity sharing defaults", !following.booleanValue && other.PackageDescription.Contains(stand.Id) && !d.Variables["following"].booleanValue,other.Describe());

                yield return OpenTalk(actor);
                yield return UseTopic("Wait here");
                DaggerfallUI.Instance.PopToHUD();
                yield return new WaitForSeconds(0.6f);
                Vector3 waiting=actor.transform.localPosition;
                yield return new WaitForSeconds(0.6f);
                actor.TryGetVariable("following",out following);
                Check("dismissal dialogue restores fallback and stops movement", !following.booleanValue && actor.PackageDescription.Contains(stand.Id) && Vector3.Distance(waiting,actor.transform.localPosition)<0.2f,actor.Describe());
                Check("console sets typed variables and exposes current values", AdvancedNpcsMod.VariableCommand(actor.Id,"following","true").Contains("following=true") &&
                    AdvancedNpcsMod.AddVariableCommand(actor.Id,"times_recruited","2").Contains("times_recruited=3"),actor.DescribeVariables());
                Check("console rejects type changes",AdvancedNpcsMod.VariableCommand(actor.Id,"following","1").Contains("expected boolean"),actor.DescribeVariables());
                actor.SetVariable("following",false);actor.SetVariable("times_recruited",0);
                actor.TryGetVariable("following",out following);following.booleanValue=true;
                actor.TryGetVariable("following",out following);
                Check("public variable reads cannot mutate saved state",!following.booleanValue,actor.DescribeVariables());
                var snapshot=mod.States.Snapshot();
                FullSerializer.fsSerializer serializer=new FullSerializer.fsSerializer();
                FullSerializer.fsData encoded;
                var saved=new NpcSaveData {Version=SaveFormat.Current,States=snapshot};
                bool encodedOk=serializer.TrySerialize(saved,out encoded).Succeeded;
                NpcSaveData decoded=null;
                bool decodedOk=serializer.TryDeserialize(encoded,ref decoded).Succeeded;
                Check("false and zero survive DFU save serialization",encodedOk && decodedOk && decoded!=null && decoded.Version==2 &&
                    decoded.States[d.Id].variables["following"].isBoolean && !decoded.States[d.Id].variables["following"].booleanValue &&
                    !decoded.States[d.Id].variables["times_recruited"].isBoolean && decoded.States[d.Id].variables["times_recruited"].integerValue==0,"serialization result");
                NpcBrain.Discard(actor);actor=null;
                mod.States.Remove(d.Id);
                mod.States.GetOrCreate(d.Id).variables=NpcVariables.Clone(decoded.States[d.Id].variables);
                actor=SpawnDefinition(d,location);
                yield return Settle;
                actor.TryGetVariable("following",out following);
                Check("respawn reads saved variables and conditional fallback",!following.booleanValue && actor.PackageDescription.Contains(stand.Id),actor.Describe());
                var legacy=FullSerializer.fsJsonParser.Parse("{\"States\":{\"legacy\":{\"opinion\":7}},\"Version\":1}");
                NpcSaveData legacySave=null;
                bool legacyOk=serializer.TryDeserialize(legacy,ref legacySave).Succeeded;
                NpcVariableValue legacyValue=null;
                Check("old saves load definition variable defaults",legacyOk && legacySave!=null && legacySave.States["legacy"].variables==null &&
                    NpcVariables.TryGet(d,legacySave.States["legacy"],"following",out legacyValue) && !legacyValue.booleanValue,"legacy save result");

                actor.SetVariable("following",true);
                d.CanRun=false;
                yield return new WaitForSeconds(0.4f);
                Check("follow respects combat.canRun false", !actor.PackageRunning && actor.PackageDescription.Contains(package.Id),actor.Describe());
                d.CanRun=true;
                actor.HoldStill(0.8f);
                waiting=actor.transform.localPosition;
                yield return new WaitForSeconds(0.5f);
                Check("follow respects greeting or casting holds",Vector3.Distance(waiting,actor.transform.localPosition)<0.2f,actor.Describe());
                actor.SwitchHostile(true);
                yield return Settle;
                Check("combat interrupts follow movement",actor.CurrentMode!=NpcMode.Calm && !actor.PackageRunning,actor.Status);
                actor.SwitchHostile(false);
                yield return new WaitForSeconds(0.5f);
                Check("calm resumes eligible follow package",actor.CurrentMode==NpcMode.Calm && actor.PackageDescription.Contains(package.Id),actor.Describe());

                d.Schedule[0]=new AiScheduleEntry {Package=package.Id,When=when,Parameters=new Dictionary<string,object>{{"target",other.Id}}};
                yield return new WaitForSeconds(5f);
                Check("follow can target another exact spawned ANPC key",Vector3.Distance(actor.transform.position,other.transform.position)<3.2f &&
                    actor.PackageDescription.Contains("following "+other.Id),actor.Describe());
                NpcBrain.Discard(other);other=null;
                yield return new WaitForSeconds(0.4f);
                waiting=actor.transform.localPosition;
                yield return new WaitForSeconds(0.6f);
                Check("lost follow target holds safely instead of wandering",actor.PackageDescription.Contains("waiting for target") && Vector3.Distance(waiting,actor.transform.localPosition)<0.2f,actor.Describe());
                other=SpawnDefinition(otherDef,location);
                yield return new WaitForSeconds(0.5f);
                Check("follow reacquires returned target",actor.PackageDescription.Contains("following "+other.Id),actor.Describe());
            }
            finally
            {
                DaggerfallUI.Instance.PopToHUD();
                NpcBrain.Discard(actor);NpcBrain.Discard(other);
                mod.Packages.RemoveForTest(package.Id);mod.Packages.RemoveForTest(stand.Id);
            }
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    public partial class SelfTest
    {
        static Dictionary<string,object> RoutineParameters(string json) { return Json.Parse(json) as Dictionary<string,object>; }
        IEnumerator RoutineSteps(DaggerfallLocation location,Transform player)
        {
            DiscardTestPeople();
            string error;
            var work=PackageParser.Parse("selftest_work","{\"type\":\"work_at\",\"parameters\":{\"animation\":\"job\"}}",out error);
            var play=PackageParser.Parse("selftest_protected","{\"type\":\"play_animation\",\"parameters\":{\"animation\":\"job\",\"only_interrupt_at_combat\":true}}",out error);
            var stand=PackageParser.Parse("selftest_routine_stand","{\"type\":\"stand\"}",out error);
            var travel=PackageParser.Parse("selftest_routine_travel","{\"type\":\"travel_to\",\"parameters\":{\"target\":[0,0,2]}}",out error);
            var sequence=PackageParser.Parse("selftest_routine_sequence","{\"type\":\"sequence\",\"parameters\":{\"steps\":[{\"package\":\"selftest_routine_travel\"},{\"package\":\"selftest_work\",\"duration\":1},{\"package\":\"selftest_routine_stand\",\"duration\":0.4}]}}",out error);
            var loop=PackageParser.Parse("selftest_routine_loop","{\"type\":\"sequence\",\"parameters\":{\"loop\":true,\"steps\":[{\"package\":\"selftest_routine_stand\",\"duration\":0.25},{\"package\":\"selftest_protected\",\"duration\":0.4}]}}",out error);
            AiPackage[] packages={work,play,stand,travel,sequence,loop};
            foreach(var p in packages) mod.Packages.AddForTest(p);
            mod.Sprites.Add("selftest_routine_art",TestSpriteSet(false,"idle","walk","run","job_1","job_2","greet","talk","cast","hit","attack","death"));
            bool shiftWasSet=mod.Flags.Has("selftest_shift");
            NpcBrain actor=null,a=null,c=null,second=null,foe=null;
            SpotLibrary temporarySpots=mod.Spots;
            string aKey=null,bKey=null;
            try
            {
                NpcDefinition d=TestDefinition("selftest_worker",Bravery.Prudent,location,player,OutOfGreeting);
                d.BaseClass="Warrior";d.Folder="selftest_routine_art";d.WanderRadius=6;
                Vector3 post=new Vector3(d.X+2,d.Y,d.Z);
                d.Schedule.Add(new AiScheduleEntry {Package=work.Id,Parameters=RoutineParameters("{\"position\":["+post.x.ToString(System.Globalization.CultureInfo.InvariantCulture)+","+post.y.ToString(System.Globalization.CultureInfo.InvariantCulture)+","+post.z.ToString(System.Globalization.CultureInfo.InvariantCulture)+"],\"facing\":90}")});
                actor=SpawnDefinition(d,location);yield return new WaitForSeconds(3f);
                var sprite=actor.GetComponent<NpcSprite>();
                Check("work_at reaches configured post and plays animation",Vector3.Distance(post,actor.transform.localPosition)<0.8f && sprite.CurrentState==NpcSprite.PackageState && sprite.CurrentAnimation.StartsWith("job_"),actor.Describe());
                Check("work_at applies local facing",Quaternion.Angle(actor.transform.localRotation,Quaternion.Euler(0,90,0))<5,actor.transform.localEulerAngles.ToString());
                int cycles=sprite.PackageCycles;yield return new WaitForSeconds(0.6f);
                Check("work_at loops animation variants and preserves defaults",sprite.PackageCycles>cycles && d.WanderRadius==6,"cycles="+sprite.PackageCycles);
                actor.SwitchHostile(true);yield return Settle;
                actor.transform.localPosition=post+Vector3.right*2;actor.SwitchHostile(false);yield return new WaitForSeconds(3f);
                Check("work_at returns to post after combat",Vector3.Distance(post,actor.transform.localPosition)<0.8f && sprite.CurrentState==NpcSprite.PackageState,actor.Describe());
                NpcBrain.Discard(actor);actor=null;

                NpcDefinition factsDef=TestDefinition("selftest_routine_facts",Bravery.Prudent,location,player,OutOfGreeting);
                factsDef.BaseClass="Warrior";factsDef.Folder="selftest_routine_art";
                var messages=new List<string>();
                Condition conditions=ConditionParser.Parse(RoutineParameters("{\"minOpinion\":10,\"maxHealth\":75,\"flags\":\"selftest_shift\"}"),"selftest",messages,false);
                factsDef.Schedule.Add(new AiScheduleEntry {Package=work.Id,Rules=conditions});factsDef.Schedule.Add(new AiScheduleEntry {Package=stand.Id});
                actor=SpawnDefinition(factsDef,location);yield return Settle;
                Check("live schedule conditions initially use fallback",actor.PackageDescription.Contains(stand.Id),actor.Describe());
                mod.Flags.Set("selftest_shift");actor.ChangeOpinion(20);actor.EntityBehaviour.Entity.CurrentHealth=actor.EntityBehaviour.Entity.MaxHealth/2;
                yield return new WaitForSeconds(0.6f);
                Check("live health opinion and flag conditions activate work",actor.PackageDescription.Contains(work.Id),actor.Describe());
                mod.Flags.Clear("selftest_shift");yield return Settle;
                Check("live condition change immediately restores fallback",actor.PackageDescription.Contains(stand.Id),actor.Describe());
                NpcBrain.Discard(actor);actor=null;

                NpcDefinition ad=TestDefinition("selftest_spot_a",Bravery.Prudent,location,player,OutOfGreeting+8);
                ad.BaseClass="Warrior";ad.Folder="selftest_routine_art";
                Vector3 firstPost=new Vector3(ad.X,ad.Y,ad.Z),secondPost=firstPost+Vector3.right*3;
                temporarySpots.AddForTest(new NamedSpot {Id="selftest_desk_a",RelativeTo="area",Region=location.Summary.RegionName,Place=location.Summary.LocationName,Position=new PackagePoint(firstPost.x,firstPost.y,firstPost.z),Facing=45});
                temporarySpots.AddForTest(new NamedSpot {Id="selftest_desk_b",RelativeTo="area",Region=location.Summary.RegionName,Place=location.Summary.LocationName,Position=new PackagePoint(secondPost.x,secondPost.y,secondPost.z),Facing=135});
                foreach(var spot in temporarySpots.All) if(spot!=null && spot.Id.StartsWith("selftest_desk")) spot.Tags.Add("selftest_desks");
                ad.Schedule.Add(new AiScheduleEntry {Package=work.Id,Parameters=RoutineParameters("{\"spot\":\"selftest_desks\"}")});
                a=SpawnDefinition(ad,location);yield return Settle;
                NpcDefinition bd=TestDefinition("selftest_spot_b",Bravery.Prudent,location,player,OutOfGreeting+9);
                bd.BaseClass="Warrior";bd.Folder="selftest_routine_art";bd.Schedule=ad.Schedule;
                second=SpawnDefinition(bd,location);yield return new WaitForSeconds(3f);
                var choices=temporarySpots.Candidates(new[]{"selftest_desks"},location.Summary.RegionName,location.Summary.LocationName,"",location.transform.GetInstanceID(),Vector3.zero,firstPost);
                foreach(var spot in choices) { if(spot.Spot.Id=="selftest_desk_a") aKey=spot.Key;else bKey=spot.Key; }
                Check("named spot occupancy chooses another free matching post",a.PackageDescription.Contains("using spot selftest_desk_a") && second.PackageDescription.Contains("using spot selftest_desk_b") &&
                    temporarySpots.Occupancy.Count(aKey)==1 && temporarySpots.Occupancy.Count(bKey)==1,a.Describe()+second.Describe());
                NpcDefinition cd=TestDefinition("selftest_spot_c",Bravery.Prudent,location,player,OutOfGreeting+14);
                cd.BaseClass="Warrior";cd.Folder="selftest_routine_art";cd.Schedule=ad.Schedule;cd.WanderRadius=1;
                c=SpawnDefinition(cd,location);yield return new WaitForSeconds(1f);
                Check("all occupied spots release NPC to normal AI",c.PackageDescription.Contains("current AI") && c.PackageAnimation==null,c.Describe());
                NpcBrain.Discard(second);second=null;yield return new WaitForSeconds(6f);
                Check("waiting NPC takes a newly freed spot",c.PackageDescription.Contains("using spot selftest_desk_b") && temporarySpots.Occupancy.Count(bKey)==1,c.Describe());
                a.SwitchHostile(true);
                Check("combat releases named spot reservation immediately",temporarySpots.Occupancy.Count(aKey)==0,a.Describe());
                a.SwitchHostile(false);yield return new WaitForSeconds(0.5f);
                Check("calm NPC reacquires its available work spot",temporarySpots.Occupancy.Count(aKey)==1,a.Describe());
                a.enabled=false;yield return null;
                Check("disabled brain releases its reservation",temporarySpots.Occupancy.Count(aKey)==0,a.Status);
                a.enabled=true;yield return new WaitForSeconds(0.5f);
                Check("re-enabled brain reacquires its work post",temporarySpots.Occupancy.Count(aKey)==1,a.Describe());
                ad.Schedule.Clear();yield return Settle;
                Check("schedule end releases named spot",temporarySpots.Occupancy.Count(aKey)==0,a.Describe());
                c.EntityBehaviour.Entity.CurrentHealth=0;yield return null;
                Check("death releases named spot",temporarySpots.Occupancy.Count(bKey)==0,c.Status);
                NpcBrain.Discard(a);a=null;NpcBrain.Discard(c);c=null;
                AdvancedNpcsMod.ReloadSpotsCommand();yield return Settle;
                Check("spot reload replaces transient reservation table",mod.Spots!=temporarySpots,"spot libraries replaced");
                string spotJson=AdvancedNpcsMod.SpotPositionCommand();
                Check("anpc_spot_pos generates valid area-scoped JSON",SpotParser.Parse("selftest_printed",spotJson,out error)!=null,error ?? spotJson);

                NpcDefinition sd=TestDefinition("selftest_sequence",Bravery.Prudent,location,player,OutOfGreeting);
                sd.BaseClass="Warrior";sd.Folder="selftest_routine_art";sd.Schedule.Add(new AiScheduleEntry {Package=sequence.Id});
                actor=SpawnDefinition(sd,location);
                float until=Time.time+5;
                while(Time.time<until && !actor.PackageDescription.Contains("step 2/")) yield return null;
                Check("sequence automatically completes travel and starts work",actor.PackageDescription.Contains("step 2/"),actor.Describe());
                actor.HoldStill(1.5f);yield return new WaitForSeconds(1.2f);
                Check("sequence duration excludes greeting/casting holds",actor.PackageDescription.Contains("step 2/"),actor.Describe());
                yield return new WaitForSeconds(0.4f);
                actor.SwitchHostile(true);yield return new WaitForSeconds(1.2f);
                Check("sequence does not advance during combat",actor.PackageDescription.Contains("step 2/"),actor.Describe());
                actor.SwitchHostile(false);yield return null;
                Check("sequence resumes interrupted step",actor.PackageDescription.Contains("step 2/"),actor.Describe());
                until=Time.time+9;
                while(Time.time<until && !actor.PackageDescription.Contains("completed")) yield return null;
                Check("sequence finishes and holds until schedule ends",actor.PackageDescription.Contains("completed") && actor.PackageAnimation==null,actor.Describe());
                sd.Schedule[0]=new AiScheduleEntry {Package=loop.Id};
                bool first=false,secondStep=false;until=Time.time+1.6f;
                while(Time.time<until) { first|=actor.PackageDescription.Contains("step 1/");secondStep|=actor.PackageDescription.Contains("step 2/");yield return null; }
                Check("looping sequence repeats its configured steps",first && secondStep && !actor.PackageDescription.Contains("completed"),actor.Describe());
                NpcBrain.Discard(actor);actor=null;

                NpcDefinition pd=TestDefinition("selftest_protected_animation",Bravery.Prudent,location,player,OutOfGreeting);
                pd.BaseClass="Warrior";pd.Folder="selftest_routine_art";pd.Schedule.Add(new AiScheduleEntry {Package=play.Id});
                pd.OwnDialogue=DialogueParser.Parse("test","{\"topics\":[{\"caption\":\"Routine test\",\"answers\":[\"Still working.\"]}]}").File;
                actor=SpawnDefinition(pd,location);yield return Settle;sprite=actor.GetComponent<NpcSprite>();
                cycles=sprite.PackageCycles;actor.HoldStill(1f);sprite.Talk(1f);bool greeted=sprite.PlayGreet();yield return new WaitForSeconds(0.5f);
                Check("combat-only animation ignores holds, talk and greetings",!greeted && actor.PackageOnlyCombatInterrupts && sprite.CurrentState==NpcSprite.PackageState && sprite.PackageCycles>cycles,actor.Describe());
                sprite.PlayCast(true);yield return new WaitForSeconds(0.2f);
                Check("combat-only animation retains pose through calm cast requests",sprite.CurrentState==NpcSprite.PackageState,sprite.CurrentState);
                yield return OpenTalk(actor);yield return UseTopic("Routine test");
                Check("player dialogue retains protected package pose",actor.PackageAnimation=="job" && sprite.CurrentState==NpcSprite.PackageState,sprite.CurrentState);
                DaggerfallUI.Instance.PopToHUD();yield return Settle;
                PlayerHit(actor,GameManager.Instance.PlayerEntityBehaviour,1);yield return Settle;
                Check("being attacked interrupts protected animation",actor.CurrentMode!=NpcMode.Calm && actor.PackageAnimation==null,actor.Status);
                actor.SwitchHostile(false);yield return Settle;
                Check("protected animation resumes when combat ends",actor.PackageOnlyCombatInterrupts && sprite.CurrentState==NpcSprite.PackageState,actor.Describe());
                pd.Bravery=Bravery.Brave;pd.AggroRange=15;
                NpcDefinition fd=TestDefinition("selftest_routine_foe",Bravery.Brave,location,player,OutOfGreeting+2);fd.Hostile=true;fd.BaseClass="Warrior";
                foe=SpawnDefinition(fd,location);yield return new WaitForSeconds(1.2f);
                Check("Brave enemy detection interrupts protected animation",actor.CurrentMode==NpcMode.Fighting && actor.PackageAnimation==null,actor.Describe());
                NpcBrain.Discard(foe);foe=null;NpcBrain.Discard(actor);actor=null;
            }
            finally
            {
                DaggerfallUI.Instance.PopToHUD();
                NpcBrain.Discard(actor);NpcBrain.Discard(a);NpcBrain.Discard(second);NpcBrain.Discard(c);NpcBrain.Discard(foe);
                if(shiftWasSet) mod.Flags.Set("selftest_shift");else mod.Flags.Clear("selftest_shift");
                foreach(var p in packages) mod.Packages.RemoveForTest(p.Id);
                temporarySpots.RemoveForTest("selftest_desk_a");temporarySpots.RemoveForTest("selftest_desk_b");
                mod.Sprites.Remove("selftest_routine_art");
            }
        }
    }
}

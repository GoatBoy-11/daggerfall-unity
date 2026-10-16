using System;
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
        public const string NavigationSuite = "navigation";
        float navigationJump, navigationSide;
        bool navigationDucked, navigationTrail;
        Vector3 navigationPrevious, navigationOrigin;
        void ObserveNavigation(NpcBrain actor)
        {
            if (actor == null) return;
            navigationJump = Mathf.Max(navigationJump, Vector3.Distance(navigationPrevious, actor.transform.position));
            navigationPrevious = actor.transform.position;
            navigationSide = Mathf.Max(navigationSide, Mathf.Abs(actor.transform.position.z - navigationOrigin.z));
            navigationDucked |= actor.GetComponent<NpcMover>().DuckingForDoor;
            navigationTrail |= actor.PackageDescription.Contains("recent route");
        }
        void ResetNavigationMetrics(NpcBrain actor)
        { navigationJump = navigationSide = 0; navigationDucked = navigationTrail = false; navigationPrevious = actor.transform.position; }
        IEnumerator WatchNavigation(NpcBrain actor, float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until) { ObserveNavigation(actor); yield return null; }
        }
        void NavigationActorAt(NpcBrain actor, Vector3 feet)
        {
            var c = actor.GetComponent<CharacterController>();
            actor.Teleport(feet + Vector3.up * (actor.transform.position.y - c.bounds.min.y));
            ResetNavigationMetrics(actor);
        }
        void NavigationPlayerAt(Transform player, Vector3 feet, Vector3 facing)
        {
            var c = player.GetComponent<CharacterController>();
            float offset = player.position.y - c.bounds.min.y;
            c.enabled = false; player.position = feet + Vector3.up * offset; c.enabled = true;
            GameManager.Instance.MainCamera.transform.rotation = Quaternion.LookRotation(facing);
        }
        GameObject NavigationBox(Transform root, string name, Vector3 position, Vector3 size)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube); box.name = name;
            box.transform.SetParent(root, false); box.transform.position = position; box.transform.localScale = size;
            return box;
        }
        IEnumerator NavigationSteps(DaggerfallLocation location, Transform player)
        {
            GameManager gm = GameManager.Instance;
            Vector3 oldPosition = player.position; Quaternion oldRotation = player.rotation, oldView = gm.MainCamera.transform.rotation;
            bool oldLook = gm.PlayerMouseLook.enabled, oldAI = gm.DisableAI;
            gm.PlayerMouseLook.enabled = false;
            const string key = "selftest_navigation_rogue"; ids.Add(key);
            var geometry = new GameObject("ANPC navigation verification"); geometry.transform.SetParent(location.transform,false);
            navigationOrigin = oldPosition + Vector3.up * 24;
            NpcBrain actor = null;
            try
            {
                var definition = mod.Catalog.Generics.Find(delegate(NpcDefinition d) { return d.Id == "ironclad_rogue_1"; });
                if (definition == null) { Fail("navigation rogue setup", "ironclad_rogue_1 not installed"); yield break; }
                NavigationBox(geometry.transform,"test floor",navigationOrigin-Vector3.up*0.15f,new Vector3(160,0.3f,30));
                NavigationPlayerAt(player,navigationOrigin+Vector3.right*7,Vector3.left);
                Vector3 local = location.transform.InverseTransformPoint(navigationOrigin-Vector3.right*7+Vector3.up*2);
                var person = new NpcInstance { Key=key, Definition=definition, Name="Navigation Rogue", Race="Breton", Gender="Female", Seed=12345,
                    Persistent=true, HasFixedPosition=true, X=local.x,Y=local.y,Z=local.z };
                var state = new NpcState { companion=CompanionRecord.From(person) }; state.companion.area="outdoors";
                mod.States.Keep(key,state); actor=spawner.SpawnChecked(person,state,location.transform,true);
                yield return new WaitForSeconds(1f);
                var crate=NavigationBox(geometry.transform,"obstacle",navigationOrigin+Vector3.up*1.25f,new Vector3(2.4f,2.5f,2.4f));
                NavigationActorAt(actor,navigationOrigin-Vector3.right*7);
                yield return WatchNavigation(actor,18);
                Check("rogue walks around a solid obstacle without teleporting",Vector3.Distance(actor.transform.position,player.position)<3.3f &&
                    navigationJump<5 && navigationSide>1.2f,"gap="+Vector3.Distance(actor.transform.position,player.position)+" jump="+navigationJump+" side="+navigationSide+"; "+actor.Describe());
                // Record a route the player actually walks around a wider corner.
                crate.transform.localScale=new Vector3(4,2.5f,4);
                NavigationPlayerAt(player,navigationOrigin-Vector3.right*4,Vector3.left);
                NavigationActorAt(actor,navigationOrigin-Vector3.right*8);
                Vector3[] route={navigationOrigin+new Vector3(-4,0,4),navigationOrigin+new Vector3(5,0,4),navigationOrigin+new Vector3(5,0,0)};
                foreach(Vector3 end in route)
                {
                    Vector3 start=player.position; start.y=navigationOrigin.y;
                    float duration=Vector3.Distance(start,end)/8f, elapsed=0;
                    while(elapsed<duration)
                    {
                        elapsed+=Time.deltaTime; NavigationPlayerAt(player,Vector3.Lerp(start,end,elapsed/duration),Vector3.left);
                        ObserveNavigation(actor); yield return null;
                    }
                }
                yield return WatchNavigation(actor,10);
                Check("follower rounds a corner on the player route without teleporting",Vector3.Distance(actor.transform.position,player.position)<3.3f &&
                    navigationJump<5 && navigationSide>2.2f,"gap="+Vector3.Distance(actor.transform.position,player.position)+" trail="+navigationTrail+" jump="+navigationJump);
                UnityEngine.Object.Destroy(crate); yield return null;
                // A narrow ordinary door with a low lintel: tall vanilla controllers must duck with feet anchored.
                var left=NavigationBox(geometry.transform,"wall left",navigationOrigin+new Vector3(0,1.5f,-8.5f),new Vector3(0.25f,3,15.5f));
                var right=NavigationBox(geometry.transform,"wall right",navigationOrigin+new Vector3(0,1.5f,8.5f),new Vector3(0.25f,3,15.5f));
                var lintel=NavigationBox(geometry.transform,"lintel",navigationOrigin+new Vector3(0,2.4f,0),new Vector3(0.25f,1.3f,1.5f));
                var doorObject=NavigationBox(geometry.transform,"ordinary hinged door",navigationOrigin+new Vector3(0,0.825f,0),new Vector3(0.2f,1.65f,1.5f));
                var door=doorObject.AddComponent<DaggerfallActionDoor>(); door.PlaySounds=false; door.OpenDuration=0.2f;
                yield return new WaitForSeconds(0.3f);
                NavigationPlayerAt(player,navigationOrigin+Vector3.right*7,Vector3.right);
                NavigationActorAt(actor,navigationOrigin-Vector3.right*6);
                yield return WatchNavigation(actor,14);
                Check("follower opens and walks through an ordinary unlocked door",door.IsOpen && Vector3.Distance(actor.transform.position,player.position)<3.3f && navigationJump<5,
                    "open="+door.IsOpen+" gap="+Vector3.Distance(actor.transform.position,player.position)+" jump="+navigationJump+"; "+actor.Describe());
                Check("low doorway posture restores after passage without sinking",navigationDucked && !actor.GetComponent<NpcMover>().DuckingForDoor &&
                    actor.GetComponent<CharacterController>().bounds.min.y>=navigationOrigin.y-0.15f,"ducked="+navigationDucked+" height="+actor.GetComponent<CharacterController>().height);
                door.SetOpen(false,true); door.StartingLockValue=5;
                NavigationActorAt(actor,navigationOrigin-Vector3.right*6);
                yield return WatchNavigation(actor,12);
                Check("locked door is neither opened bashed nor bypassed by catch-up",door.IsLocked && !door.IsOpen && navigationJump<5 &&
                    actor.transform.position.x<navigationOrigin.x,"lock="+door.CurrentLockValue+" jump="+navigationJump+"; "+actor.Describe());
                door.CurrentLockValue=0;
                yield return WatchNavigation(actor,12);
                Check("unlocking a blocked door lets follower resume walking",door.IsOpen && actor.transform.position.x>navigationOrigin.x && navigationJump<5,
                    "open="+door.IsOpen+" jump="+navigationJump+" offset="+(actor.transform.position-navigationOrigin)+"; "+actor.Describe());
                UnityEngine.Object.Destroy(left);UnityEngine.Object.Destroy(right);UnityEngine.Object.Destroy(lintel);UnityEngine.Object.Destroy(doorObject);
                yield return new WaitForSeconds(0.3f);
                // In view: no pop. Out of view: bounded catch-up on safe floor with the same health/state.
                NavigationPlayerAt(player,navigationOrigin+Vector3.right*22,Vector3.left);
                NavigationActorAt(actor,navigationOrigin-Vector3.right*55);
                yield return WatchNavigation(actor,7);
                Check("visible distant follower never teleports",navigationJump<5,"largest jump="+navigationJump+" gap="+Vector3.Distance(actor.transform.position,player.position));
                actor.EntityBehaviour.Entity.CurrentHealth=Math.Max(1,actor.EntityBehaviour.Entity.MaxHealth/2);
                actor.CaptureState(); int health=actor.EntityBehaviour.Entity.CurrentHealth;
                NavigationPlayerAt(player,navigationOrigin+Vector3.right*22,Vector3.right);
                NavigationActorAt(actor,navigationOrigin-Vector3.right*55);
                yield return WatchNavigation(actor,9);
                Check("hidden distant follower catches up onto safe floor",navigationJump>10 && Vector3.Distance(actor.transform.position,player.position)<4 &&
                    actor.GetComponent<CharacterController>().bounds.min.y>=navigationOrigin.y-0.15f,"jump="+navigationJump+" gap="+Vector3.Distance(actor.transform.position,player.position)+"; "+actor.Describe());
                Check("catch-up preserves person contract and exact current health",actor.Id==key && actor.State.companion.status=="following" &&
                    actor.EntityBehaviour.Entity.CurrentHealth==health,"health="+actor.EntityBehaviour.Entity.CurrentHealth+" expected="+health);
                mod.Companions.Command(actor,"wait");
                NavigationPlayerAt(player,navigationOrigin+Vector3.right*22,Vector3.right);
                NavigationActorAt(actor,navigationOrigin-Vector3.right*55);
                yield return WatchNavigation(actor,8);
                Check("waiting companion never catches up",navigationJump<1 && actor.transform.position.x<navigationOrigin.x && actor.State.companion.status=="waiting",actor.Describe());
                mod.Companions.Command(actor,"follow");
                NavigationActorAt(actor,navigationOrigin-Vector3.right*55);
                actor.SwitchHostile(true);
                yield return WatchNavigation(actor,7);
                Check("combat never invokes follower catch-up",navigationJump<5 && actor.CurrentMode!=NpcMode.Calm,actor.Describe());
                actor.SwitchHostile(false);
                NavigationActorAt(actor,navigationOrigin-Vector3.right*55);
                gm.DisableAI=true;
                yield return WatchNavigation(actor,7);
                Check("disabled AI freezes follow and recovery",navigationJump<1 && actor.transform.position.x<navigationOrigin.x,actor.Describe());
                gm.DisableAI=false;
                actor.HoldStill(10); NavigationActorAt(actor,navigationOrigin-Vector3.right*55);
                yield return WatchNavigation(actor,7);
                Check("greeting or casting holds prevent recovery movement",navigationJump<1 && actor.transform.position.x<navigationOrigin.x,actor.Describe());
                yield return new WaitForSeconds(3.5f); // allow the remaining deliberate hold to expire
                // Floor ends at z=15: avoid a drop even if the leader is beyond its edge, and never catch up without floor.
                NavigationPlayerAt(player,navigationOrigin+Vector3.forward*18,Vector3.forward);
                NavigationActorAt(actor,navigationOrigin+Vector3.forward*13);
                yield return WatchNavigation(actor,3);
                Check("follow steering does not walk off a ledge",actor.GetComponent<CharacterController>().bounds.min.y>=navigationOrigin.y-0.2f &&
                    actor.transform.position.z<navigationOrigin.z+15,"position="+actor.transform.position);
            }
            finally
            {
                NpcBrain.Discard(actor);mod.States.Remove(key);UnityEngine.Object.Destroy(geometry);
                gm.DisableAI=oldAI; player.position=oldPosition; player.rotation=oldRotation; gm.MainCamera.transform.rotation=oldView; gm.PlayerMouseLook.enabled=oldLook;
            }
        }
    }
}

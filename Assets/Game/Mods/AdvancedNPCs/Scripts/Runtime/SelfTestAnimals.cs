using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DaggerfallConnect.Utility;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.MagicAndEffects;
using DaggerfallWorkshop.Utility;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>Self-test steps for animals and wildlife spawning (spec animal-ai, Verification). Run alone: suite "animals".</summary>
    public partial class SelfTest
    {
        public const string AnimalSuite = "animals";

        IEnumerator AnimalSteps(DaggerfallLocation location, Transform playerTransform)
        {
            GameManager gm = GameManager.Instance;
            DaggerfallEntityBehaviour player = gm.PlayerEntityBehaviour;
            if (gm.PlayerGPS.IsPlayerInLocationRect || gm.PlayerEnterExit.IsPlayerInside)
            {
                if (gm.PlayerEnterExit.IsPlayerInside)
                    Fail("animals: setup", "stand outdoors");
                DFPosition target;
                if (!FindWilderness(gm.PlayerGPS.CurrentMapPixel, out target))
                {
                    Fail("animals: setup", "no wilderness map pixel near " + gm.PlayerGPS.CurrentMapPixel.X + "," + gm.PlayerGPS.CurrentMapPixel.Y);
                    yield break;
                }
                gm.StreamingWorld.TeleportToCoordinates(target.X, target.Y, StreamingWorld.RepositionMethods.Origin);
                float until = Time.realtimeSinceStartup + 20f;
                yield return new WaitForSecondsRealtime(2f);
                while (Time.realtimeSinceStartup < until && (!gm.StreamingWorld.IsReady || gm.StreamingWorld.IsInit))
                    yield return null;
                yield return new WaitForSecondsRealtime(2f);
            }
            // The checks spawn their own animals and must not meet rolled ones; crime checks count from here.
            mod.Wilderness.DespawnAll();
            crimes.Clear();
            Action<DaggerfallEntity, DaggerfallEntity, Diseases[]> inflict = NpcDisease.Inflict;
            List<Diseases> inflicted = new List<Diseases>();
            NpcDisease.Inflict = delegate (DaggerfallEntity a, DaggerfallEntity t, Diseases[] list) { inflicted.AddRange(list); };

            GameFacts facts = new GameFacts(null, mod.Flags, "", null);
            Check("animals: the climate where the player stands is known", facts.Climate.Length > 0,
                "climate index " + gm.PlayerGPS.CurrentClimateIndex);
            List<string> here = mod.Wilderness.Explain();
            Check("animals: anpc_here names the climate and ground", here.Count > 0 && here[0].StartsWith("climate ", StringComparison.Ordinal) && here[0].Contains("ground"),
                here.Count > 0 ? here[0] : "no lines");

            NpcDefinition prey = AnimalTemplate("selftest_an_prey",
                "{ \"baseClass\": \"Bard\", \"wanderRadius\": 3, \"crimeOnAttack\": false, \"animal\": { \"temperament\": \"prey\", \"calls\": false }, " +
                "\"spawn\": { \"wilderness\": { \"chance\": 100, \"max\": 8, \"group\": [4, 4] } } }");
            NpcDefinition bear = AnimalTemplate("selftest_an_bear",
                "{ \"baseClass\": \"GrizzlyBear\", \"crimeOnAttack\": false, \"combat\": { \"health\": 3 }, " +
                "\"animal\": { \"temperament\": \"territorial\", \"warnSeconds\": 1, \"calls\": false }, \"spawn\": { \"wilderness\": { \"chance\": 100 } } }");
            NpcDefinition tiger = AnimalTemplate("selftest_an_tiger",
                "{ \"baseClass\": \"SabertoothTiger\", \"aggroRange\": 30, \"crimeOnAttack\": false, " +
                "\"animal\": { \"temperament\": \"predator\", \"senses\": { \"alert\": 4, \"react\": 2 }, \"boldChance\": 0, \"hunts\": [\"selftest_an_prey\"], \"calls\": false }, " +
                "\"spawn\": { \"wilderness\": { \"chance\": 100 } } }");
            NpcDefinition cow = AnimalTemplate("selftest_an_cow",
                "{ \"baseClass\": \"Bard\", \"crimeOnAttack\": true, \"animal\": { \"temperament\": \"grazer\", \"calls\": false }, " +
                "\"disease\": { \"chance\": 100, \"diseases\": [\"Plague\"] }, \"spawn\": { \"wilderness\": { \"chance\": 100 } } }");
            if (prey == null || bear == null || tiger == null || cow == null)
            {
                NpcDisease.Inflict = inflict;
                yield break;
            }

            // --- a group spawn: one roll, one spot, one group, a leader
            string why;
            List<NpcBrain> herd = mod.Wilderness.SpawnGroup(prey, 4, out why);
            Track(herd);
            yield return new WaitForSecondsRealtime(1f);
            int group = herd.Count > 0 ? herd[0].Instance.Group : -1;
            bool together = herd.Count >= 3 && group >= 0;
            foreach (NpcBrain b in herd)
                together &= b.Instance.Group == group && Vector3.Distance(Flat(b.transform.position), Flat(herd[0].transform.position)) <= AnimalRules.GroupSpread + 2f;
            bool led = herd.Count >= 2;
            for (int i = 1; i < herd.Count; i++)
                led &= herd[i].GetComponent<NpcAnimal>() != null && herd[i].GetComponent<NpcAnimal>().Leader == herd[0];
            Check("animals: a wilderness group appears together and shares one group", together,
                (why ?? "") + " count " + herd.Count + ", group " + group + "; " + Spread(herd));
            Check("animals: the group's first member leads the others", led, Leaders(herd));
            Check("animals: wild animals count toward the wildlife cap, not the wilderness people cap",
                mod.Wilderness.CountAlive(true) == herd.Count && mod.Wilderness.CountAlive(false) == 0,
                "animals " + mod.Wilderness.CountAlive(true) + ", people " + mod.Wilderness.CountAlive(false));
            if (herd.Count < 2)
            {
                Cleanup(inflict);
                yield break;
            }

            // --- the floating origin moves wilderness bodies one by one: their homes must move along
            FloatingOrigin origin = UnityEngine.Object.FindObjectOfType<FloatingOrigin>();
            if (origin != null)
            {
                NpcBrain probe = herd[herd.Count - 1];
                Vector3 homeBefore = probe.HomeLocal;
                Vector3 shift = new Vector3(819.2f, 0f, 0f);
                origin.RaiseOnPositionUpdateEvent(shift);
                Vector3 homeShifted = probe.HomeLocal;
                origin.RaiseOnPositionUpdateEvent(-shift);
                Check("animals: a wilderness ANPC's home follows a floating-origin shift",
                    Vector3.Distance(homeShifted, homeBefore + shift) < 0.01f && Vector3.Distance(probe.HomeLocal, homeBefore) < 0.01f,
                    "before " + homeBefore + ", shifted " + homeShifted + ", back " + probe.HomeLocal);
            }

            // --- prey flees from a player inside react range; the group takes up the alarm; then calms down far away
            NpcBrain doe = herd[1];
            NpcAnimal doeAnimal = doe.GetComponent<NpcAnimal>();
            // The live react range (after the test character's Stealth), not the template's.
            PutPlayerNear(doe.transform.position, Mathf.Max(2f, doeAnimal.ReactNow * 0.6f));
            yield return new WaitForSecondsRealtime(1.5f);
            Check("animals: prey flees from a player inside its react range", doe.CurrentMode == NpcMode.Fleeing && !doe.State.hostile,
                doe.Describe());
            int fleeing = herd.FindAll(delegate (NpcBrain b) { return b.CurrentMode == NpcMode.Fleeing; }).Count;
            Check("animals: its group flees with it", fleeing == herd.Count, fleeing + " of " + herd.Count + " fleeing");
            Check("animals: an animal is not angry at a player it only fled from", Count(PlayerEntity.Crimes.Assault) == 0 && !doe.State.hostile, CrimeList());
            PutPlayerNear(doe.transform.position, 70f);
            yield return new WaitForSecondsRealtime(2f);
            Check("animals: a flight ends once the player is far", doe.CurrentMode == NpcMode.Calm && doeAnimal.State == AnimalStates.Unaware, doe.Describe());

            // --- a click opens no talk window
            NpcTalk talk = doe.GetComponent<NpcTalk>();
            talk.HandleActivate(PlayerActivateModes.Talk, 1f);
            Check("animals: clicking an animal opens no talk window", !TalkWindowOpen() && talk.LastMessage != null && talk.LastMessage.EndsWith("ignores you.", StringComparison.Ordinal),
                "message=" + talk.LastMessage + ", top window=" + DaggerfallUI.UIManager.TopWindow);
            if (TalkWindowOpen())
                CloseTalkWindow();

            // --- a hit wild animal flees, with no crime
            NpcBrain hind = herd[herd.Count - 1];
            PutPlayerNear(hind.transform.position, 2.5f);
            yield return new WaitForSecondsRealtime(0.3f);
            PlayerHit(hind, player, 1);
            yield return new WaitForSecondsRealtime(0.5f);
            Check("animals: a hit prey animal flees and hitting a wild animal is no crime",
                hind.CurrentMode == NpcMode.Fleeing && crimes.Count == 0, hind.Describe() + "; " + CrimeList());
            PutPlayerNear(hind.transform.position, 70f);
            yield return new WaitForSecondsRealtime(1.5f);

            // --- a calm creature-class animal: warns, attacks, gives up far from home
            List<NpcBrain> bears = mod.Wilderness.SpawnGroup(bear, 1, out why);
            Track(bears);
            yield return new WaitForSecondsRealtime(1f);
            NpcBrain grizzly = bears.Count > 0 ? bears[0] : null;
            Check("animals: a calm GrizzlyBear animal spawns calm", grizzly != null && grizzly.CurrentMode == NpcMode.Calm && grizzly.Definition.IsCreature,
                grizzly != null ? grizzly.Describe() : why);
            if (grizzly != null)
            {
                NpcAnimal ga = grizzly.GetComponent<NpcAnimal>();
                PutPlayerNear(grizzly.transform.position, Mathf.Max(2f, ga.ReactNow * 0.8f));
                yield return new WaitForSecondsRealtime(0.8f);
                Check("animals: a territorial animal warns a player inside its react range", ga.State == AnimalStates.Warning && grizzly.CurrentMode == NpcMode.Calm,
                    grizzly.Describe());
                yield return new WaitForSecondsRealtime(1.5f);
                Check("animals: it attacks when the player stays after the warning", grizzly.CurrentMode == NpcMode.Fighting && grizzly.Threat == player,
                    grizzly.Describe());
                Check("animals: an animal attacking the player counts as a foe for companions and guards", grizzly.AttacksPeople, grizzly.Status);
                PutPlayerNear(grizzly.transform.position, 70f);
                yield return new WaitForSecondsRealtime(1.5f);
                Check("animals: it gives up the chase once the player is far", grizzly.CurrentMode == NpcMode.Calm && !grizzly.State.hostile,
                    grizzly.Describe());
            }

            // --- a dog with "attacks": false barks but does not bite
            NpcDefinition dog = AnimalTemplate("selftest_an_dog",
                "{ \"baseClass\": \"SabertoothTiger\", \"crimeOnAttack\": true, " +
                "\"animal\": { \"temperament\": \"territorial\", \"warnSeconds\": 1, \"attacks\": false, \"calls\": false }, \"spawn\": { \"wilderness\": { \"chance\": 100 } } }");
            List<NpcBrain> dogs = dog != null ? mod.Wilderness.SpawnGroup(dog, 1, out why) : new List<NpcBrain>();
            Track(dogs);
            yield return new WaitForSecondsRealtime(1f);
            if (dogs.Count > 0)
            {
                NpcAnimal da = dogs[0].GetComponent<NpcAnimal>();
                PutPlayerNear(dogs[0].transform.position, Mathf.Max(1.5f, da.ReactNow * 0.4f));
                yield return new WaitForSecondsRealtime(2.5f);
                Check("animals: a dog that does not attack keeps barking at a close player", da.State == AnimalStates.Warning && dogs[0].CurrentMode == NpcMode.Calm,
                    dogs[0].Describe());
                PutPlayerNear(dogs[0].transform.position, 70f);
            }

            // --- a hungry predator hunts a prey animal and feeds on the kill
            NpcBrain victim = herd[0];
            List<NpcBrain> tigers = mod.Wilderness.SpawnGroup(tiger, 1, out why);
            Track(tigers);
            yield return new WaitForSecondsRealtime(0.5f);
            NpcBrain hunter = tigers.Count > 0 ? tigers[0] : null;
            if (hunter == null)
            {
                Fail("animals: a hungry predator hunts a prey animal", why);
            }
            else
            {
                Vector3 side = Vector3.Cross(Vector3.up, Flat(victim.transform.position - gm.PlayerObject.transform.position).normalized);
                hunter.Teleport(victim.transform.position + side * 5f + Vector3.up * 0.5f);
                PutPlayerNear(victim.transform.position, 40f);
                NpcAnimal ta = hunter.GetComponent<NpcAnimal>();
                ta.MakeHungry();
                yield return new WaitForSecondsRealtime(1.5f);
                Check("animals: a hungry predator hunts a prey animal it sees", ta.Prey != null && hunter.CurrentMode == NpcMode.Fighting,
                    hunter.Describe());
                Check("animals: a predator hunting an animal is no foe for companions and guards", !hunter.AttacksPeople, hunter.Status);
                if (ta.Prey != null)
                    victim = ta.Prey.Brain;     // the nearest one it saw, not necessarily the one it was put next to
                // Prey often outruns it (and goes when 150 m from the player): finish it early so the feeding is tested.
                float until = Time.realtimeSinceStartup + 3f;
                while (Time.realtimeSinceStartup < until && victim != null && victim.CurrentMode != NpcMode.Dead)
                    yield return new WaitForSecondsRealtime(0.25f);
                if (victim != null && victim.CurrentMode != NpcMode.Dead && victim.EntityBehaviour != null)
                {
                    AdvancedNpcsMod.Log(Prefix + "NOTE finishing the prey for the feeding check (" + victim.Id + ", " + victim.Status + ")");
                    CreatureHit(victim, hunter.EntityBehaviour, victim.EntityBehaviour.Entity.CurrentHealth + 5);
                }
                yield return new WaitForSecondsRealtime(1.5f);
                Check("animals: the predator feeds on its kill", ta.Feeding && hunter.CurrentMode == NpcMode.Calm, hunter.Describe());
            }

            // --- livestock: no assault for a hit, theft for a kill; disease from its hits
            List<NpcBrain> cows = mod.Wilderness.SpawnGroup(cow, 1, out why);
            Track(cows);
            yield return new WaitForSecondsRealtime(0.8f);
            NpcBrain bessie = cows.Count > 0 ? cows[0] : null;
            if (bessie == null)
            {
                Fail("animals: livestock", why);
            }
            else
            {
                NpcDisease disease = bessie.GetComponent<NpcDisease>();
                bool passed = disease != null && disease.Hit(0) && inflicted.Contains(Diseases.Plague);
                Check("animals: a disease block passes its disease on a hit", passed, "component " + (disease != null) + ", inflicted " + inflicted.Count);
                PutPlayerNear(bessie.transform.position, 2.5f);
                yield return new WaitForSecondsRealtime(0.3f);
                int assaults = Count(PlayerEntity.Crimes.Assault);
                PlayerHit(bessie, player, 1);
                yield return new WaitForSecondsRealtime(0.4f);
                Check("animals: hitting livestock is no assault", Count(PlayerEntity.Crimes.Assault) == assaults, CrimeList());
                PlayerHit(bessie, player, bessie.EntityBehaviour.Entity.CurrentHealth + 5);
                yield return new WaitForSecondsRealtime(0.5f);
                Check("animals: killing livestock is theft, not murder", Count(PlayerEntity.Crimes.Theft) == 1 && Count(PlayerEntity.Crimes.Murder) == 0,
                    CrimeList());
            }

            // --- cost: frame time with a full wildlife cap of animals around vs none (CPU mostly: the test window is minimized)
            mod.Wilderness.DespawnAll();
            yield return new WaitForSecondsRealtime(1f);
            float before = 0f;
            yield return MeasureFrames(delegate (float ms) { before = ms; });
            List<NpcBrain> crowd = new List<NpcBrain>();
            for (int i = 0; i < 3 && crowd.Count < 10; i++)
                crowd.AddRange(mod.Wilderness.SpawnGroup(prey, Mathf.Min(4, 10 - crowd.Count), out why));
            Track(crowd);
            foreach (NpcBrain b in crowd)
                PutNear(b, gm.PlayerObject.transform.position, 20f);
            yield return new WaitForSecondsRealtime(1.5f);
            float with = 0f;
            yield return MeasureFrames(delegate (float ms) { with = ms; });
            AdvancedNpcsMod.Log(Prefix + "NOTE frame time " + before.ToString("0.00") + " ms without animals, " + with.ToString("0.00") + " ms with " +
                crowd.Count + " within 20 m");
            Check("animals: " + crowd.Count + " animals around cost under 4 ms a frame", crowd.Count >= 8 && with - before < 4f,
                before.ToString("0.00") + " -> " + with.ToString("0.00") + " ms, " + crowd.Count + " animals");

            Cleanup(inflict);
        }

        /// <summary>Average frame time in milliseconds over 3 real seconds.</summary>
        static IEnumerator MeasureFrames(Action<float> done)
        {
            float start = Time.realtimeSinceStartup;
            int frames = 0;
            while (Time.realtimeSinceStartup - start < 3f)
            {
                frames++;
                yield return null;
            }
            done((Time.realtimeSinceStartup - start) * 1000f / Mathf.Max(1, frames));
        }

        /// <summary>Puts an animal on the ground at a random spot within radius of a point (its home moves along).</summary>
        static void PutNear(NpcBrain b, Vector3 point, float radius)
        {
            Vector2 r = UnityEngine.Random.insideUnitCircle * radius;
            Vector3 at = point + new Vector3(r.x, 0f, r.y);
            RaycastHit hit;
            if (Physics.Raycast(at + Vector3.up * 200f, Vector3.down, out hit, 400f, SpotFinder.Mask, QueryTriggerInteraction.Ignore))
                at = hit.point;
            b.Teleport(at + Vector3.up * 1f);
        }

        void Cleanup(Action<DaggerfallEntity, DaggerfallEntity, Diseases[]> inflict)
        {
            NpcDisease.Inflict = inflict;
            mod.Wilderness.DespawnAll();
            crimes.Clear();
        }

        NpcDefinition AnimalTemplate(string id, string json)
        {
            ParseResult r = DefinitionParser.ParseFolder(id, json);
            Check("animals: test template " + id + " parses", r.Ok && r.Warnings.Count == 0, r.Error + " " + string.Join("; ", r.Warnings.ToArray()));
            return r.Ok ? r.Definition : null;
        }

        void Track(List<NpcBrain> brains)
        {
            foreach (NpcBrain b in brains)
                if (b != null && !ids.Contains(b.Id))
                    ids.Add(b.Id);
        }

        /// <summary>Stands the player on the ground this far from a point (on the far side from where it stood).</summary>
        static void PutPlayerNear(Vector3 point, float distance)
        {
            GameManager gm = GameManager.Instance;
            Transform player = gm.PlayerObject.transform;
            Vector3 away = Flat(player.position - point);
            away = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward;
            Vector3 stand = point + away * distance;
            RaycastHit hit;
            if (Physics.Raycast(stand + Vector3.up * 200f, Vector3.down, out hit, 400f, SpotFinder.Mask, QueryTriggerInteraction.Ignore))
                stand = hit.point;
            CharacterController controller = gm.PlayerObject.GetComponent<CharacterController>();
            if (controller != null)
                controller.enabled = false;
            player.position = stand + Vector3.up * 1.1f;
            if (controller != null)
                controller.enabled = true;
            gm.PlayerMouseLook.SetFacing(-away);
        }

        static string Spread(List<NpcBrain> brains)
        {
            List<string> parts = new List<string>();
            foreach (NpcBrain b in brains)
                parts.Add(b.Id + " g" + b.Instance.Group + " at " + Vector3.Distance(Flat(b.transform.position), Flat(brains[0].transform.position)).ToString("0.0"));
            return string.Join(", ", parts.ToArray());
        }

        static string Leaders(List<NpcBrain> brains)
        {
            List<string> parts = new List<string>();
            foreach (NpcBrain b in brains)
            {
                NpcAnimal a = b.GetComponent<NpcAnimal>();
                parts.Add(b.Id + " -> " + (a == null ? "no animal" : a.Leader != null ? a.Leader.Id : "none"));
            }
            return string.Join(", ", parts.ToArray());
        }
    }
}

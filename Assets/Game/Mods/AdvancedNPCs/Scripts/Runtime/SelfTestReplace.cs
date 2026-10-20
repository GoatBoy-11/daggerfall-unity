using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>
    /// Self-test: ANPCs standing in for vanilla flat NPCs ("replaces"). Also runs alone as the "replace" suite
    /// (anpc_selftest replace, Tools~/selftest-replace.sh) for a short run. A test flat with the bald mage's sprite
    /// (177.2) is made next to the player; no building or quest is needed.
    /// </summary>
    public partial class SelfTest
    {
        public const string ReplaceSuite = "replace";
        const string BaldMage = "mages_guild_bald_man_1";
        const string CowledOldMan = "cowled_old_man_1";
        static readonly FieldInfo FlatDataField = typeof(StaticNPC).GetField("npcData", BindingFlags.Instance | BindingFlags.NonPublic);

        IEnumerator ReplaceSteps(DaggerfallLocation location, Transform player)
        {
            DiscardTestPeople();

            // The installed examples: each replaces its vanilla flat, with its fireball and ranged sheet.
            NpcDefinition mage = CheckStandInExample(BaldMage, 2, "bald mage");
            CheckStandInExample(CowledOldMan, 0, "cowled old man");
            NpcDefinition added = null;
            if (mage == null)
            {
                // Without the example: a plain stand-in, so the remaining checks still run.
                added = TestDefinition("selftest_standin", Bravery.Normal, location, player, 0f);
                added.Kind = NpcKind.Generic;
                added.Replace = new ReplaceSettings { StayNear = 2f };
                added.Replace.Targets.Add(ReplaceRules.ParseTarget("177.2"));
                mod.Catalog.Generics.Add(added);
            }

            // A vanilla flat with that sprite, under its own parent so only it is scanned.
            GameObject parent = new GameObject("selftest_flats");
            parent.transform.SetParent(location.transform, false);
            GameObject flatObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            flatObject.name = "selftest_flat";
            flatObject.transform.SetParent(parent.transform, false);
            flatObject.transform.position = player.position + player.forward * 4f + Vector3.up;
            StaticNPC flat = flatObject.AddComponent<StaticNPC>();
            StaticNPC.NPCData data = new StaticNPC.NPCData();
            StaticNPC.SetLayoutData(ref data, 4242, 0, 4242, 0, 0, 177, 2, 4242, -7, 0, 0);
            FlatDataField.SetValue(flat, data);

            List<NpcBrain> spawned = mod.Flats.ReplaceIn(parent.transform, -7, "selftest", "Breton", IndoorSpawner.IndoorWander);
            NpcBrain brain = spawned.Count == 1 ? spawned[0] : null;
            if (brain != null)
                ids.Add(brain.Id);
            NpcTalk talk = brain != null ? brain.GetComponent<NpcTalk>() : null;
            Collider flatCollider = flatObject.GetComponent<Collider>();
            Check("a stand-in spawns and the flat is hidden", brain != null && talk != null && talk.StandIn == flat &&
                !flatObject.GetComponent<Renderer>().enabled && !flatCollider.enabled,
                "spawned " + spawned.Count + ", renderer " + flatObject.GetComponent<Renderer>().enabled + ", collider " + flatCollider.enabled);
            if (brain == null)
            {
                Object.Destroy(parent);
                if (added != null)
                    mod.Catalog.Generics.Remove(added);
                yield break;
            }
            yield return Settle;

            // Clicking the stand-in runs vanilla's own click on the flat: its talk window, with the ANPC's portrait.
            brain.transform.rotation = Quaternion.LookRotation(Flatten(brain.transform.position - player.position).normalized);    // its back to the player
            talk.HandleActivate(PlayerActivateModes.Talk, 1f);
            Vector3 toPlayer = Flatten(player.position - brain.transform.position).normalized;
            Check("talking turns the ANPC to face the player", Vector3.Dot(Flatten(brain.transform.forward).normalized, toPlayer) > 0.95f,
                "facing dot " + Vector3.Dot(Flatten(brain.transform.forward).normalized, toPlayer).ToString("0.00"));
            yield return null;
            yield return null;
            DaggerfallTalkWindow window = DaggerfallUI.UIManager.TopWindow as DaggerfallTalkWindow;
            bool wantFace = brain.Instance.Definition.Replace.Portrait && talk.Portrait != null;
            Check("clicking the stand-in opens the vanilla NPC's talk", window != null && TalkManager.Instance.StaticNPC == flat &&
                (!wantFace || NpcTalk.CurrentTalkPortrait() == talk.Portrait),
                "top window " + (DaggerfallUI.UIManager.TopWindow == null ? "none" : DaggerfallUI.UIManager.TopWindow.GetType().Name) +
                ", target is the flat " + (TalkManager.Instance.StaticNPC == flat) + ", own portrait wanted " + wantFace);
            while (DaggerfallUI.UIManager.TopWindow is DaggerfallTalkWindow)
                DaggerfallUI.UIManager.PopWindow();
            yield return Settle;

            // A quest hiding the vanilla NPC hides its stand-in (and showing it brings it back).
            flatObject.SetActive(false);
            yield return new WaitForSecondsRealtime(0.8f);
            bool hidden = !brain.gameObject.activeSelf;
            flatObject.SetActive(true);
            yield return new WaitForSecondsRealtime(0.8f);
            Check("quest hide and show reach the stand-in", hidden && brain.gameObject.activeSelf, "hidden " + hidden + ", back " + brain.gameObject.activeSelf);

            // stayNear: pushed 5 m away, a calm stand-in walks back.
            if (brain.Leash > 0f)
            {
                Vector3 home = brain.transform.position;
                CharacterController cc = brain.GetComponent<CharacterController>();
                cc.enabled = false;
                brain.transform.position = home + player.right * 5f;
                cc.enabled = true;
                yield return new WaitForSeconds(4f);
                float back = Vector3.Distance(Flatten(brain.transform.position), Flatten(home));
                Check("stayNear walks a stand-in back to its spot", back < 4f, back.ToString("0.0") + " m from its spot after 4 s");
            }

            // A dead stand-in stays dead: the flat stays hidden and nobody new appears for it.
            string key = brain.Id;
            NpcBrain.Discard(brain);
            mod.States.GetOrCreate(key).dead = true;
            yield return null;
            List<NpcBrain> again = mod.Flats.ReplaceIn(parent.transform, -7, "selftest", "Breton", IndoorSpawner.IndoorWander);
            Check("a dead stand-in stays dead", again.Count == 0 && mod.Flats.LastReplaced == 1 && !flatObject.GetComponent<Renderer>().enabled,
                "respawned " + again.Count + ", replaced " + mod.Flats.LastReplaced);

            Object.Destroy(parent);
            yield return AllySteps(player);
            if (added != null)
                mod.Catalog.Generics.Remove(added);
        }

        /// <summary>Two stand-ins of one faction on the same spot: one steps aside; attacking one turns the other against the player.</summary>
        IEnumerator AllySteps(Transform player)
        {
            GameObject parent = new GameObject("selftest_allies");
            parent.transform.SetParent(player.parent, true);
            Vector3 spot = player.position + player.forward * 4f + Vector3.up;
            for (int i = 0; i < 2; i++)
            {
                GameObject flatObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
                flatObject.name = "selftest_ally_flat_" + i;
                Object.Destroy(flatObject.GetComponent<Collider>());
                flatObject.transform.SetParent(parent.transform, false);
                flatObject.transform.position = spot;
                StaticNPC flat = flatObject.AddComponent<StaticNPC>();
                StaticNPC.NPCData data = new StaticNPC.NPCData();
                StaticNPC.SetLayoutData(ref data, 5000 + i, 0, 5000, 0, 40, 177, 2, 5000 + i, -8, 0, 0);
                FlatDataField.SetValue(flat, data);
            }
            List<NpcBrain> pair = mod.Flats.ReplaceIn(parent.transform, -8, "selftest", "Breton", IndoorSpawner.IndoorWander);
            foreach (NpcBrain b in pair)
            {
                ids.Add(b.Id);
                mod.States.GetOrCreate(b.Id).dead = false;
            }
            yield return Settle;
            float apart = pair.Count == 2 ? Vector3.Distance(Flatten(pair[0].transform.position), Flatten(pair[1].transform.position)) : 0f;
            Check("an ANPC spawned inside another steps aside", pair.Count == 2 && apart >= SpotFinder.PersonSpacing,
                "spawned " + pair.Count + ", " + apart.ToString("0.00") + " m apart");
            if (pair.Count == 2)
            {
                // A calm ANPC left staring at a wall turns to the open room.
                NpcBrain starer = pair[1];
                starer.WanderCap = 0f;
                Vector3 ahead = Flatten(player.position - starer.transform.position).normalized;
                GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = "selftest_wall";
                wall.transform.position = starer.transform.position + ahead * 0.8f;
                wall.transform.rotation = Quaternion.LookRotation(ahead);
                wall.transform.localScale = new Vector3(3f, 3f, 0.2f);
                starer.transform.rotation = Quaternion.LookRotation(ahead);
                yield return new WaitForSeconds(2.5f);
                bool turned = !SpotFinder.WallAhead(starer.transform.position, starer.transform.forward, 1.2f);
                Object.Destroy(wall);
                Check("a calm ANPC facing a wall turns to the open room", turned,
                    "angle to the wall " + Vector3.Angle(Flatten(starer.transform.forward), ahead).ToString("0"));

                pair[0].CompanionBetrayed();    // the player's attack on a calm ANPC
                yield return null;
                Check("an attacked ANPC's ally takes its side", pair[1].State.hostile && pair[1].CurrentMode != NpcMode.Calm,
                    "ally hostile " + pair[1].State.hostile + ", mode " + pair[1].CurrentMode);
            }
            foreach (NpcBrain b in pair)
            {
                b.SwitchHostile(false);
                NpcBrain.Discard(b);
            }
            Object.Destroy(parent);
        }

        NpcDefinition CheckStandInExample(string id, int record, string label)
        {
            NpcDefinition d = mod.Catalog.Generics.Find(delegate (NpcDefinition t) { return t.Id == id; });
            List<LoadedSpriteSet> sets = mod.Sprites.For(id);
            Check(label + " example replaces 177." + record + " and has its fireball", d != null && d.Replace != null && d.Replace.Matches(177, record, 0) &&
                sets.Count > 0 && sets[0].Set.Animations.ContainsKey("attack_range_1") && sets[0].Set.Projectiles.Count > 0,
                d == null ? id + " not installed" : "replace " + (d.Replace != null) + ", sprite sets " + sets.Count);
            return d;
        }

        static Vector3 Flatten(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}

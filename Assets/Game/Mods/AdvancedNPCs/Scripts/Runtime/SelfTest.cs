using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>
    /// In-game checks of Advanced NPC behaviour through DFU's own attack code (anpc_selftest).
    /// Uses temporary NPCs next to the player, records crimes instead of punishing, turns god mode on
    /// for the run and never touches the game clock. Results go to Player.log as "SELFTEST ..." lines.
    /// </summary>
    public class SelfTest
    {
        public const string Prefix = "SELFTEST ";
        static readonly WaitForSeconds Settle = new WaitForSeconds(0.3f);

        public static bool Running { get; private set; }

        readonly AdvancedNpcsMod mod;
        readonly NpcSpawner spawner;
        readonly List<PlayerEntity.Crimes> crimes = new List<PlayerEntity.Crimes>();
        readonly List<string> ids = new List<string>();
        int passed;
        int failed;

        public SelfTest(AdvancedNpcsMod mod, NpcSpawner spawner)
        {
            this.mod = mod;
            this.spawner = spawner;
        }

        public IEnumerator Run(Action<int, int> onDone)
        {
            Running = true;
            PlayerEntity player = GameManager.Instance.PlayerEntity;
            bool godMode = player.GodMode;
            Action<PlayerEntity.Crimes> report = NpcCrime.Report;
            player.GodMode = true;
            NpcCrime.Report = delegate (PlayerEntity.Crimes c) { crimes.Add(c); };
            AdvancedNpcsMod.Log(Prefix + "START");

            // Step through manually so an exception in one check is reported and cleanup still runs.
            IEnumerator steps = Steps();
            while (true)
            {
                object current;
                try
                {
                    if (!steps.MoveNext())
                        break;
                    current = steps.Current;
                }
                catch (Exception e)
                {
                    Fail("unexpected exception", e.ToString());
                    break;
                }
                yield return current;
            }

            foreach (string id in ids)
            {
                NpcBrain.Discard(NpcBrain.Find(id));
                mod.States.Remove(id);
            }
            NpcCrime.Report = report;
            player.GodMode = godMode;
            Running = false;

            string summary = Prefix + "DONE " + passed + "/" + (passed + failed) + " passed";
            AdvancedNpcsMod.Log(summary);
            DaggerfallUI.AddHUDText("Advanced NPCs self-test: " + passed + "/" + (passed + failed) + " passed (details in Player.log)");
            if (onDone != null)
                onDone(passed, failed);
        }

        IEnumerator Steps()
        {
            GameManager gm = GameManager.Instance;
            DaggerfallLocation location = gm.StreamingWorld.CurrentPlayerLocationObject;
            if (location == null || gm.PlayerEnterExit.IsPlayerInside)
            {
                Fail("setup", "stand outdoors in a town before running the self-test");
                yield break;
            }
            DaggerfallEntityBehaviour playerBehaviour = gm.PlayerEntityBehaviour;
            Transform playerTransform = gm.PlayerObject.transform;

            NpcBrain normal = Make("selftest_normal", Bravery.Normal, location, playerTransform, -3f);
            NpcBrain bystander = Make("selftest_bystander", Bravery.Normal, location, playerTransform, -1f);
            NpcBrain coward = Make("selftest_coward", Bravery.Coward, location, playerTransform, 1f);
            NpcBrain brave = Make("selftest_brave", Bravery.Brave, location, playerTransform, 3f);
            yield return Settle;

            NpcBrain[] all = new NpcBrain[] { normal, bystander, coward, brave };
            bool allCalm = true;
            foreach (NpcBrain b in all)
                allCalm &= b.CurrentMode == NpcMode.Calm && !b.State.hostile && !b.Motor.IsHostile;
            Check("spawned NPCs are calm", allCalm, Describe(all));
            Check("calm NPCs do not block resting", !gm.AreEnemiesNearby(true), "GameManager.AreEnemiesNearby(resting) returned true");
            // Talking (spec §9.2): name, PNG portrait, vanilla face fallback, Info text.
            NpcTalk talk = normal.GetComponent<NpcTalk>();
            Texture2D testPortrait = TestPortrait();
            talk.Portrait = testPortrait;
            bool opened = talk.TryTalk();
            for (int wait = 0; wait < 10 && !TalkWindowOpen(); wait++)
                yield return new WaitForSeconds(0.1f);
            Check("calm ANPC opens DFU's talk window", opened && TalkWindowOpen(),
                "opened=" + opened + ", top window=" + DaggerfallUI.UIManager.TopWindow);
            Check("talk window shows the ANPC's name", TalkManager.Instance.NameNPC == "Selftest Normal", "name=" + TalkManager.Instance.NameNPC);
            Check("talk window shows the ANPC's PNG portrait", NpcTalk.CurrentTalkPortrait() == testPortrait,
                "portrait=" + NpcTalk.CurrentTalkPortrait());
            CloseTalkWindow();
            yield return Settle;

            NpcTalk plain = bystander.GetComponent<NpcTalk>();
            plain.Portrait = null;
            opened = plain.TryTalk();
            for (int wait = 0; wait < 10 && !TalkWindowOpen(); wait++)
                yield return new WaitForSeconds(0.1f);
            Check("ANPC without portrait talks with a vanilla face",
                opened && TalkWindowOpen() && NpcTalk.CurrentTalkPortrait() != testPortrait,
                "opened=" + opened + ", portrait=" + NpcTalk.CurrentTalkPortrait() + ", face record=" + plain.Proxy.PersonFaceRecordId);
            CloseTalkWindow();
            yield return Settle;

            plain.HandleActivate(PlayerActivateModes.Info, 1f);
            Check("Info-mode click names the ANPC", plain.LastMessage == "You see Selftest Normal.", "message=" + plain.LastMessage);
            DaggerfallUI.Instance.PopupMessage(TextManager.Instance.GetLocalizedText("youSeeA").Replace("%s", "Spellsword"));
            plain.HandleActivate(PlayerActivateModes.Info, 1f);
            Check("Info-mode click replaces vanilla's class text", !NpcTalk.PopupLines().Exists(delegate (string l) { return l.Contains("Spellsword"); }),
                "popup=" + string.Join(" | ", NpcTalk.PopupLines().ToArray()));

            gm.MakeEnemiesHostile();
            yield return Settle;
            bool noneHostile = true;
            foreach (NpcBrain b in all)
                noneHostile &= !b.State.hostile && !b.Motor.IsHostile;
            Check("engine 'anger everyone' sweep is undone", noneHostile, Describe(all));

            int assaults = Count(PlayerEntity.Crimes.Assault);
            PlayerHit(normal, playerBehaviour, 1);
            yield return Settle;
            Check("player hit turns NPC hostile and fighting", normal.State.hostile && normal.CurrentMode == NpcMode.Fighting, Describe(normal));
            Check("player hit on calm NPC reports one assault", Count(PlayerEntity.Crimes.Assault) == assaults + 1, CrimeList());
            Check("bystander stays calm when another NPC is hit",
                !bystander.State.hostile && !bystander.Motor.IsHostile && bystander.CurrentMode == NpcMode.Calm, Describe(bystander));

            PlayerHit(normal, playerBehaviour, 1);
            yield return Settle;
            Check("hitting an already hostile NPC reports no new assault", Count(PlayerEntity.Crimes.Assault) == assaults + 1, CrimeList());
            Check("hostile ANPC refuses to talk",
                !talk.TryTalk() && !TalkWindowOpen() && talk.LastMessage == "Selftest Normal will not talk to you now.",
                "message=" + talk.LastMessage + ", top window=" + DaggerfallUI.UIManager.TopWindow);

            SetHealthFraction(normal, 0.2f);
            yield return Settle;
            Check("normal NPC flees below 25% health", normal.CurrentMode == NpcMode.Fleeing, Describe(normal));

            PlayerHit(coward, playerBehaviour, 1);
            yield return Settle;
            Check("coward flees when hit", coward.CurrentMode == NpcMode.Fleeing, Describe(coward));

            PlayerHit(brave, playerBehaviour, 1);
            SetHealthFraction(brave, 0.1f); // after the hit, so the hit itself cannot kill a low-health NPC
            yield return Settle;
            Check("brave NPC fights at 10% health", brave.CurrentMode == NpcMode.Fighting, Describe(brave));

            normal.State.hostileUntil = 0; // deadline passed, without touching the game clock
            yield return new WaitForSeconds(2.5f);
            Check("hostile NPC calms down when its deadline passes", !normal.State.hostile && normal.CurrentMode == NpcMode.Calm, Describe(normal));
            Check("calmed NPC is healed", normal.EntityBehaviour.Entity.CurrentHealth == normal.EntityBehaviour.Entity.MaxHealth, Describe(normal));

            assaults = Count(PlayerEntity.Crimes.Assault);
            CreatureHit(bystander, brave.EntityBehaviour, 1);
            yield return Settle;
            Check("NPC fights back against a creature", bystander.CurrentMode == NpcMode.Fighting, Describe(bystander));
            Check("creature attack is not hostility to the player and no crime",
                !bystander.State.hostile && !bystander.Motor.IsHostile && Count(PlayerEntity.Crimes.Assault) == assaults, Describe(bystander) + "; " + CrimeList());

            NpcBrain.Discard(brave);
            yield return new WaitForSeconds(1f);
            Check("NPC calms down when the creature is gone", bystander.CurrentMode == NpcMode.Calm, Describe(bystander));

            int murders = Count(PlayerEntity.Crimes.Murder);
            PlayerHit(bystander, playerBehaviour, 100000);
            yield return Settle;
            Check("one-hit kill of a calm NPC by the player is murder",
                bystander.State.dead && Count(PlayerEntity.Crimes.Murder) == murders + 1, "dead=" + bystander.State.dead + "; " + CrimeList());

            NpcBrain victim = Make("selftest_victim", Bravery.Normal, location, playerTransform, 0f);
            yield return Settle;
            murders = Count(PlayerEntity.Crimes.Murder);
            CreatureHit(victim, coward.EntityBehaviour, 100000);
            yield return Settle;
            Check("one-hit kill of a calm NPC by a creature is not murder",
                victim.State.dead && Count(PlayerEntity.Crimes.Murder) == murders, "dead=" + victim.State.dead + "; " + CrimeList());

            NpcSaveData data = (NpcSaveData)new NpcSaveDataInterface(mod.States, delegate { }).GetSaveData();
            string text = SaveLoadManager.Serialize(typeof(NpcSaveData), data);
            NpcSaveData back = SaveLoadManager.Deserialize(typeof(NpcSaveData), text) as NpcSaveData;
            Check("save data survives a serialize/deserialize round trip",
                back != null && SameState(data, back, "selftest_brave") && SameState(data, back, "selftest_victim"), text);
            Check("mod settings are read", mod.Config.FromSettings, "no modsettings.json in the mod, or DFU could not read it");
            List<string> partial = new List<string>();
            foreach (DaggerfallLocation town in spawner.LoadedTowns())
            {
                int planned;
                if (town != null && spawner.PlannedCellCounts.TryGetValue(town.Summary.MapID, out planned))
                {
                    int now = NpcSpawner.WalkableCount(town);
                    if (now != planned)
                        partial.Add(town.Summary.LocationName + " planned on " + planned + " of " + now + " cells");
                }
            }
            Check("generic ANPCs are planned on complete street grids", spawner.PlannedCellCounts.Count > 0 && partial.Count == 0,
                "towns planned=" + spawner.PlannedCellCounts.Count + "; " + string.Join("; ", partial.ToArray()));
            // Generic townsfolk (spec §7.4, §8). The test template only matches this town.
            List<NpcDefinition> templates = new List<NpcDefinition>();
            templates.Add(GenericTemplate(location, 3));

            List<NpcBrain> first = SpawnGenerics(location, templates, GenericMode.SamePeople, 12);
            yield return Settle;
            string firstLayout = Layout(first);
            Check("generic template spawns its count", first.Count == 3, firstLayout);
            DiscardAll(first);
            yield return Settle;

            List<NpcBrain> second = SpawnGenerics(location, templates, GenericMode.SamePeople, 12);
            yield return Settle;
            Check("same-people mode brings back the same people", Layout(second) == firstLayout, firstLayout + " vs " + Layout(second));

            string hurtKey = second.Count > 0 ? second[0].Id : "?";
            if (second.Count > 0)
                SetHealthFraction(second[0], 0.5f);
            yield return Settle;
            DiscardAll(second);
            yield return Settle;
            NpcSaveData saved = (NpcSaveData)new NpcSaveDataInterface(mod.States, delegate { }).GetSaveData();
            NpcState savedState;
            Check("a generic ANPC's damage is saved under its key",
                saved.States.TryGetValue(hurtKey, out savedState) && Mathf.Abs(savedState.healthFraction - 0.5f) < 0.1f, hurtKey);

            List<NpcBrain> third = SpawnGenerics(location, templates, GenericMode.SamePeople, 12);
            yield return Settle;
            NpcBrain hurt = NpcBrain.Find(hurtKey);
            Check("a damaged generic ANPC comes back damaged",
                // Max health is re-rolled on every spawn and health is whole points: allow one point of rounding.
                hurt != null && savedState != null && hurt.EntityBehaviour != null &&
                Mathf.Abs(hurt.CurrentHealthFraction - savedState.healthFraction) <= 1f / Mathf.Max(1, hurt.EntityBehaviour.Entity.MaxHealth) + 0.001f,
                hurt == null ? "missing" : hurt.Status + ", saved fraction " + (savedState != null ? savedState.healthFraction : -1f));
            DiscardAll(third);
            yield return Settle;

            foreach (string key in ids)
            {
                if (key.StartsWith("selftest_generic@", StringComparison.Ordinal))
                    mod.States.Remove(key);
            }
            List<NpcBrain> random1 = SpawnGenerics(location, templates, GenericMode.RandomEachVisit, 12);
            yield return Settle;
            string randomLayout = Layout(random1);
            bool randomInTable = false;
            foreach (NpcBrain b in random1)
                randomInTable |= mod.States.Has(b.Id);
            DiscardAll(random1);
            yield return Settle;
            List<NpcBrain> random2 = SpawnGenerics(location, templates, GenericMode.RandomEachVisit, 12);
            yield return Settle;
            Check("random mode re-rolls the people", random2.Count == 3 && Layout(random2) != randomLayout, randomLayout + " vs " + Layout(random2));
            Check("random mode saves nothing about them", !randomInTable, randomLayout);
            DiscardAll(random2);
            yield return Settle;

            List<NpcBrain> none = SpawnGenerics(location, templates, GenericMode.SamePeople, 0);
            Check("MaxGenericPerTown 0 spawns no generic ANPCs", none.Count == 0, Layout(none));

            // Name lists (spec v2.1 §5): one generic person from a simple list, one from a vanilla-format list.
            mod.Names.Add(NameListParser.Parse("selftest_simple", "{ \"male\": [\"Testmale\"], \"female\": [\"Testfemale\"] }").List);
            mod.Names.Add(NameListParser.Parse("selftest_vanilla", "{ \"style\": \"breton\", \"sets\": [ {\"parts\":[\"A\"]}, " +
                "{\"parts\":[\"b\"]}, {\"parts\":[\"C\"]}, {\"parts\":[\"d\"]}, {\"parts\":[\"E\"]}, {\"parts\":[\"f\"]} ] }").List);
            NpcDefinition simpleNames = GenericTemplate(location, 1);
            simpleNames.Id = "selftest_names_simple";
            simpleNames.NameList = "selftest_simple";
            NpcDefinition vanillaNames = GenericTemplate(location, 1);
            vanillaNames.Id = "selftest_names_vanilla";
            vanillaNames.NameList = "selftest_vanilla";
            List<NpcBrain> named = SpawnGenerics(location, new List<NpcDefinition> { simpleNames, vanillaNames }, GenericMode.SamePeople, 12);
            yield return Settle;
            NpcBrain simpleOne = NpcBrain.Find(PopulationPlanner.KeyFor("selftest_names_simple", location.Summary.MapID, 0));
            NpcBrain vanillaOne = NpcBrain.Find(PopulationPlanner.KeyFor("selftest_names_vanilla", location.Summary.MapID, 0));
            Check("generic name from a simple name list",
                simpleOne != null && (simpleOne.DisplayName == "Testmale" || simpleOne.DisplayName == "Testfemale"),
                simpleOne == null ? "missing" : simpleOne.DisplayName);
            Check("generic name from a vanilla-format name list",
                vanillaOne != null && (vanillaOne.DisplayName == "Ab Ef" || vanillaOne.DisplayName == "Cd Ef"),
                vanillaOne == null ? "missing" : vanillaOne.DisplayName);
            DiscardAll(named);
            yield return Settle;

            // Portrait pools (spec v2.1 §6): the face shown at the first talk is kept, even when the pool grows.
            mod.Portraits.Add("selftest_face_1", TestPortrait());
            mod.Portraits.Add("selftest_face_2", TestPortrait());
            NpcDefinition faced = GenericTemplate(location, 1);
            faced.Id = "selftest_faces";
            faced.Portraits.Add("selftest_face");
            List<NpcBrain> facedFirst = SpawnGenerics(location, new List<NpcDefinition> { faced }, GenericMode.SamePeople, 12);
            yield return Settle;
            string facedKey = facedFirst.Count > 0 ? facedFirst[0].Id : "?";
            NpcTalk facedTalk = facedFirst.Count > 0 ? facedFirst[0].GetComponent<NpcTalk>() : null;
            string shown = facedTalk != null ? facedTalk.PortraitFile : null;
            bool talked = facedTalk != null && facedTalk.TryTalk();
            for (int wait = 0; wait < 10 && !TalkWindowOpen(); wait++)
                yield return new WaitForSeconds(0.1f);
            CloseTalkWindow();
            yield return Settle;
            string locked = mod.States.GetOrCreate(facedKey).portrait;
            mod.Portraits.Add("selftest_face_3", TestPortrait());
            DiscardAll(facedFirst);
            yield return Settle;
            List<NpcBrain> facedBack = SpawnGenerics(location, new List<NpcDefinition> { faced }, GenericMode.SamePeople, 12);
            yield return Settle;
            NpcBrain facedPerson = NpcBrain.Find(facedKey);
            string shownBack = facedPerson != null ? facedPerson.GetComponent<NpcTalk>().PortraitFile : null;
            Check("talked-to person keeps its portrait", talked && shown != null && locked == shown && shownBack == shown,
                "shown=" + shown + ", locked=" + locked + ", after pool grew=" + shownBack);
            DiscardAll(facedBack);

            // A single-picture pool can never change, so talking saves nothing for it.
            mod.Portraits.Add("selftest_solo", TestPortrait());
            NpcDefinition solo = GenericTemplate(location, 1);
            solo.Id = "selftest_solo";
            solo.Portraits.Add("selftest_solo");
            List<NpcBrain> soloPeople = SpawnGenerics(location, new List<NpcDefinition> { solo }, GenericMode.SamePeople, 12);
            yield return Settle;
            NpcTalk soloTalk = soloPeople.Count > 0 ? soloPeople[0].GetComponent<NpcTalk>() : null;
            bool soloTalked = soloTalk != null && soloTalk.TryTalk();
            for (int wait = 0; wait < 10 && !TalkWindowOpen(); wait++)
                yield return new WaitForSeconds(0.1f);
            CloseTalkWindow();
            yield return Settle;
            string soloKey = soloPeople.Count > 0 ? soloPeople[0].Id : "?";
            Check("one-picture portrait is not locked in save data",
                soloTalked && soloTalk.PortraitFile == "selftest_solo" && string.IsNullOrEmpty(mod.States.GetOrCreate(soloKey).portrait),
                "talked=" + soloTalked + ", locked=" + mod.States.GetOrCreate(soloKey).portrait);
            DiscardAll(soloPeople);
            mod.Portraits.Remove("selftest_solo");

            // Custom sprites (spec 1b): a generated 3-frame set on a calm, standing NPC.
            mod.Sprites.Add("selftest_sprite", TestSpriteSet(false));
            NpcBrain sprited = Make("selftest_sprite", Bravery.Normal, location, playerTransform, 2f);
            yield return Settle;
            NpcSprite sprite = sprited.GetComponent<NpcSprite>();
            Check("sprite NPC hides the vanilla billboard and shows its own",
                sprite != null && sprite.VanillaHidden && sprite.Showing, sprite == null ? "no NpcSprite" : "vanilla hidden=" + sprite.VanillaHidden + ", showing=" + sprite.Showing);

            Vector3 toCamera = Camera.main.transform.position - sprited.transform.position;
            toCamera.y = 0;
            toCamera.Normalize();
            sprited.transform.rotation = Quaternion.LookRotation(toCamera);
            yield return null;
            yield return null;
            int facingRow = sprite != null ? sprite.CurrentRow : -1;
            sprited.transform.rotation = Quaternion.LookRotation(new Vector3(-toCamera.z, 0, toCamera.x));   // camera on its right
            yield return null;
            yield return null;
            int rightRow = sprite != null ? sprite.CurrentRow : -1;
            Check("sprite direction follows the camera", facingRow == 0 && rightRow == 2, "facing camera row=" + facingRow + ", camera on right row=" + rightRow);

            int changes = sprite != null ? sprite.FrameChanges : 0;
            yield return new WaitForSeconds(0.6f);
            Check("sprite frames advance", sprite != null && sprite.FrameChanges > changes, "frame changes " + changes + " -> " + (sprite != null ? sprite.FrameChanges : 0));

            string walking = null;
            for (int step = 0; step < 20; step++)
            {
                sprited.transform.position += sprited.transform.forward * 0.08f;
                yield return null;
                if (sprite != null && sprite.CurrentState == SpriteStates.Walk)
                    walking = sprite.CurrentAnimation;
            }
            Check("sprite walks while moving", walking == "walk", "animation while moving=" + walking);

            // Lit and filtered like vanilla billboards (normals 45 degrees up toward the camera, DFU's filter setting).
            Transform quadChild = sprited.transform.Find("AnpcSprite");
            Vector3 quadNormal = quadChild != null ? quadChild.GetComponent<MeshFilter>().sharedMesh.normals[0] : Vector3.zero;
            Check("sprite quad is lit like vanilla billboards", Vector3.Distance(quadNormal, new Vector3(0, 1, -1).normalized) < 0.01f, "normal=" + quadNormal);
            Texture spriteTexture = sprite != null && sprite.MaterialTemplate != null ? sprite.MaterialTemplate.mainTexture : null;
            Check("sprite uses DFU's texture filter setting", spriteTexture != null && spriteTexture.filterMode == DaggerfallUnity.Instance.MaterialReader.MainFilterMode,
                "sprite=" + (spriteTexture != null ? spriteTexture.filterMode.ToString() : "none") + ", DFU=" + DaggerfallUnity.Instance.MaterialReader.MainFilterMode);

            // A floating-origin shift moves the whole town; a standing NPC must not start walking.
            yield return new WaitForSeconds(1f);
            Transform spriteTown = sprited.transform.parent;
            spriteTown.position += new Vector3(500f, 0f, 0f);
            yield return null;
            yield return null;
            string afterShift = sprite != null ? sprite.CurrentState : null;
            spriteTown.position -= new Vector3(500f, 0f, 0f);
            yield return new WaitForSeconds(1f);
            Check("world shift does not make a standing sprite walk", afterShift == SpriteStates.Idle, "state after shift=" + afterShift);

            // Every hit plays the hit sheet from its first frame, even a second hit while the first still plays.
            int hitsBefore = sprite != null ? sprite.HitStarts : 0;
            PlayerHit(sprited, playerBehaviour, 1);
            yield return null;
            yield return null;
            string firstHit = sprite != null ? sprite.CurrentState : null;
            yield return new WaitForSeconds(0.2f);
            int frameBeforeSecond = sprite != null ? sprite.CurrentFrame : -1;
            PlayerHit(sprited, playerBehaviour, 1);
            yield return null;
            yield return null;      // coroutines resume before LateUpdate draws the frame
            string secondHit = sprite != null ? sprite.CurrentState : null;
            int frameAfterSecond = sprite != null ? sprite.CurrentFrame : -1;
            int hitStarts = sprite != null ? sprite.HitStarts - hitsBefore : 0;
            Check("every hit plays the hit sheet from the start",
                firstHit == SpriteStates.Hit && secondHit == SpriteStates.Hit && hitStarts == 2 && frameAfterSecond < frameBeforeSecond,
                "first hit=" + firstHit + ", second hit=" + secondHit + ", hit starts=" + hitStarts + ", frame " + frameBeforeSecond + " -> " + frameAfterSecond);

            // Death (spec 1b §6.4): the death sheet plays, the body stays, the loot pile's own picture is hidden.
            int corpsesBefore = NpcCorpseSprite.All.Count;
            PlayerHit(sprited, playerBehaviour, 100000);
            yield return new WaitForSeconds(1.5f);
            NpcCorpseSprite body = NpcCorpseSprite.All.Count > corpsesBefore ? NpcCorpseSprite.All[NpcCorpseSprite.All.Count - 1] : null;
            Check("death leaves a sprite corpse and hides the loot picture",
                body != null && body.Finished && body.HidLoot && !body.ShowingStatic,
                body == null ? "no corpse sprite" : "finished=" + body.Finished + ", hid loot=" + body.HidLoot + ", static=" + body.ShowingStatic);

            mod.Sprites.Add("selftest_sprite_static", TestSpriteSet(true));
            NpcBrain staticDeath = Make("selftest_sprite_static", Bravery.Normal, location, playerTransform, -2f);
            yield return Settle;
            corpsesBefore = NpcCorpseSprite.All.Count;
            PlayerHit(staticDeath, playerBehaviour, 100000);
            yield return new WaitForSeconds(1.5f);
            NpcCorpseSprite staticBody = NpcCorpseSprite.All.Count > corpsesBefore ? NpcCorpseSprite.All[NpcCorpseSprite.All.Count - 1] : null;
            Check("death_static is used as the corpse", staticBody != null && staticBody.ShowingStatic,
                staticBody == null ? "no corpse sprite" : "static=" + staticBody.ShowingStatic);
            mod.Sprites.Remove("selftest_sprite");
            mod.Sprites.Remove("selftest_sprite_static");

            // Developer look (not a check): if a real sprite set is installed, stand one in front of the camera and
            // save two screenshots (front and side) next to Player.log.
            LoadedSpriteSet real = null;
            foreach (NpcDefinition g in mod.Catalog.Generics)
            {
                if (mod.Sprites.For(g.Folder).Count > 0)
                {
                    real = mod.Sprites.For(g.Folder)[0];
                    break;
                }
            }
            if (real != null)
            {
                mod.Sprites.Add("selftest_look", real);
                NpcBrain look = Make("selftest_look", Bravery.Normal, location, playerTransform, 0f);
                yield return new WaitForSeconds(1f);
                Vector3 toCam = Camera.main.transform.position - look.transform.position;
                toCam.y = 0;
                look.transform.rotation = Quaternion.LookRotation(toCam.normalized);
                yield return new WaitForSeconds(0.5f);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(Application.persistentDataPath, "selftest-sprite-front.png"));
                yield return new WaitForSeconds(0.5f);
                look.transform.rotation = Quaternion.LookRotation(new Vector3(-toCam.z, 0, toCam.x));
                yield return new WaitForSeconds(0.5f);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(Application.persistentDataPath, "selftest-sprite-side.png"));
                yield return new WaitForSeconds(0.5f);
                AdvancedNpcsMod.Log(Prefix + "LOOK screenshots saved (" + real.Label + ")");
                mod.Sprites.Remove("selftest_look");
            }
            mod.Portraits.Remove("selftest_face_1");
            mod.Portraits.Remove("selftest_face_2");
            mod.Portraits.Remove("selftest_face_3");
            yield return Settle;
        }

        NpcBrain Make(string id, Bravery bravery, DaggerfallLocation location, Transform player, float side)
        {
            mod.States.Remove(id);
            ids.Add(id);
            Vector3 world = player.position + player.forward * 4f + player.right * side;
            Vector3 local = location.transform.InverseTransformPoint(world);

            NpcDefinition d = new NpcDefinition();
            d.Id = id;
            d.Name = "Selftest " + bravery;
            d.Region = location.Summary.RegionName;
            d.Place = location.Summary.LocationName;
            d.X = local.x;
            d.Y = local.y;
            d.Z = local.z;
            d.BaseClass = "Spellsword";
            d.Gender = "";
            d.Bravery = bravery;
            d.FleeHealthPercent = 25;
            d.CalmDownMinHours = 6f;
            d.CalmDownMaxHours = 48f;
            d.CrimeOnAttack = true;
            d.WanderRadius = 0f;
            d.Folder = id;
            d.SourceFile = "(selftest)";
            return spawner.SpawnTest(NpcInstance.ForUnique(d, "Breton"), location.transform);
        }

        /// <summary>Spawns generic test ANPCs; their keys go into ids so the end of the run removes them and their state.</summary>
        List<NpcBrain> SpawnGenerics(DaggerfallLocation location, List<NpcDefinition> templates, GenericMode mode, int cap)
        {
            List<NpcBrain> brains = spawner.SpawnGenerics(location, templates, mode, cap);
            foreach (NpcBrain b in brains)
            {
                if (!ids.Contains(b.Id))
                    ids.Add(b.Id);
            }
            return brains;
        }

        static void DiscardAll(List<NpcBrain> brains)
        {
            foreach (NpcBrain b in brains)
                NpcBrain.Discard(b);
        }

        /// <summary>Keys, names and rounded positions, sorted: equal strings mean the same people in the same places.</summary>
        static string Layout(List<NpcBrain> brains)
        {
            List<string> parts = new List<string>();
            foreach (NpcBrain b in brains)
            {
                if (b == null)
                    continue;
                Vector3 p = b.transform.localPosition;
                parts.Add(b.Id + "=" + b.DisplayName + "@" + Mathf.Round(p.x) + "," + Mathf.Round(p.z));
            }
            parts.Sort(StringComparer.Ordinal);
            return string.Join("; ", parts.ToArray());
        }

        /// <summary>A tiny generated sprite set: idle and hit (3 frames), walk and death (2 frames), 16x32 cells, feet 4 px up.</summary>
        static LoadedSpriteSet TestSpriteSet(bool withDeathStatic)
        {
            SpriteSet set = new SpriteSet();
            set.PixelsPerUnit = 10f;
            set.CellHeight = 32;
            set.GroundY = 4;
            set.Fps = 8;
            Dictionary<string, Texture2D> sheets = new Dictionary<string, Texture2D>();
            string[] names = { "idle", "walk", "death", "hit" };
            int[] frames = { 3, 2, 2, 3 };
            for (int i = 0; i < names.Length; i++)
            {
                SpriteAnimation a = new SpriteAnimation();
                a.Name = names[i];
                a.CellWidth = 16;
                a.Frames = frames[i];
                set.Animations[a.Name] = a;
                sheets[a.Name] = SolidTexture(16 * frames[i], 32 * 8, new Color32((byte)(80 * i), 120, 200, 255));
            }
            Texture2D deathStatic = withDeathStatic ? SolidTexture(24, 12, new Color32(160, 0, 0, 255)) : null;
            return SpriteLibrary.FromTextures("selftest", set, sheets, deathStatic);
        }

        static Texture2D SolidTexture(int width, int height, Color32 colour)
        {
            Texture2D t = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color32[] px = new Color32[width * height];
            for (int i = 0; i < px.Length; i++)
                px[i] = colour;
            t.SetPixels32(px);
            t.Apply();
            return t;
        }

        static NpcDefinition GenericTemplate(DaggerfallLocation location, int count)
        {
            NpcDefinition d = new NpcDefinition();
            d.Id = "selftest_generic";
            d.Folder = d.Id;
            d.Kind = NpcKind.Generic;
            d.Name = "";
            d.Gender = "";
            d.BaseClass = "Spellsword";
            d.Bravery = Bravery.Normal;
            d.FleeHealthPercent = 25;
            d.CalmDownMinHours = 6f;
            d.CalmDownMaxHours = 48f;
            d.CrimeOnAttack = true;
            d.WanderRadius = 0f;
            d.SourceFile = "(selftest)";
            d.Spawn = new GenericSpawn();
            d.Spawn.CountMin = count;
            d.Spawn.CountMax = count;
            SpawnPlace here = new SpawnPlace();
            here.Region = location.Summary.RegionName;
            here.Place = location.Summary.LocationName;
            d.Spawn.Places.Add(here);
            return d;
        }

        // Same order as a real weapon hit: damage first, then DFU's attack handling.
        static void PlayerHit(NpcBrain npc, DaggerfallEntityBehaviour player, int damage)
        {
            npc.EntityBehaviour.Entity.DecreaseHealth(damage);
            npc.EntityBehaviour.HandleAttackFromSource(player);
        }

        // Same order as EnemyAttack: damage first, then the victim is told who attacked it.
        static void CreatureHit(NpcBrain npc, DaggerfallEntityBehaviour attacker, int damage)
        {
            npc.EntityBehaviour.Entity.DecreaseHealth(damage);
            npc.Motor.MakeEnemyHostileToAttacker(attacker);
        }

        static void SetHealthFraction(NpcBrain npc, float fraction)
        {
            DaggerfallEntity e = npc.EntityBehaviour.Entity;
            e.CurrentHealth = Math.Max(1, (int)(e.MaxHealth * fraction));
        }

        static bool SameState(NpcSaveData a, NpcSaveData b, string id)
        {
            NpcState x;
            NpcState y;
            if (!a.States.TryGetValue(id, out x) || !b.States.TryGetValue(id, out y))
                return false;
            return x.dead == y.dead && x.hostile == y.hostile && x.hostileUntil == y.hostileUntil &&
                   Math.Abs(x.healthFraction - y.healthFraction) < 0.0001f;
        }

        int Count(PlayerEntity.Crimes crime)
        {
            int n = 0;
            foreach (PlayerEntity.Crimes c in crimes)
            {
                if (c == crime)
                    n++;
            }
            return n;
        }

        string CrimeList()
        {
            return "crimes=[" + string.Join(", ", crimes.ConvertAll(c => c.ToString()).ToArray()) + "]";
        }

        static string Describe(params NpcBrain[] brains)
        {
            List<string> parts = new List<string>();
            foreach (NpcBrain b in brains)
                parts.Add(b == null ? "(gone)" : b.Id + ": " + b.Status + (b.Motor != null && b.Motor.IsHostile ? ", motor hostile" : ""));
            return string.Join("; ", parts.ToArray());
        }

        static bool TalkWindowOpen()
        {
            return DaggerfallUI.UIManager.TopWindow is DaggerfallTalkWindow;
        }

        static void CloseTalkWindow()
        {
            if (TalkWindowOpen())
                DaggerfallUI.UIManager.PopWindow();
        }

        static Texture2D TestPortrait()
        {
            Texture2D texture = new Texture2D(64, 64, TextureFormat.ARGB32, false);
            Color32[] pixels = new Color32[64 * 64];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32((byte)(i % 64 * 4), (byte)(i / 64 * 4), 160, 255);
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        void Check(string name, bool ok, string detail)
        {
            if (ok)
            {
                passed++;
                AdvancedNpcsMod.Log(Prefix + "PASS " + name);
            }
            else
            {
                Fail(name, detail);
            }
        }

        void Fail(string name, string detail)
        {
            failed++;
            AdvancedNpcsMod.LogError(Prefix + "FAIL " + name + " -- " + detail);
        }
    }
}

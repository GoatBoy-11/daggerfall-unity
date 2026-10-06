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

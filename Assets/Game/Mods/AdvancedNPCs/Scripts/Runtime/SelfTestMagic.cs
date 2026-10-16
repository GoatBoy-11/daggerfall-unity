using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.MagicAndEffects;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    public partial class SelfTest
    {
        IEnumerator MagicSteps(DaggerfallLocation location, Transform player)
        {
            NpcDefinition d = TestDefinition("selftest_magic", Bravery.Brave, location, player, 4f);
            d.BaseClass = "Mage";
            d.Magic = new NpcMagicDefinition();
            d.Magic.Spells.AddRange(new[] { "Fireball", "Heal", "Shield", "Recall", "not_a_spell" });
            NpcBrain b = spawner.SpawnTest(NpcInstance.ForUnique(d, "Breton"), location.transform);
            yield return Settle;
            NpcMagic magic = b.GetComponent<NpcMagic>();
            string supported = NpcSpellLibrary.ListSupported("Resist Fire");
            Check("magic lists supported elemental resistance spell", supported.Contains("Resist Fire"), supported);
            Check("magic resolves combat spells, rejects utility/unknown", magic != null && magic.SpellCount == 3, "count=" + (magic != null ? magic.SpellCount : -1));
            Check("magic overrides inherited book and maximum", b.EntityBehaviour.Entity.SpellbookCount() == 0 && b.EntityBehaviour.Entity.MaxMagicka == 150,
                b.Status);
            List<EffectBundleSettings> spells = NpcSpellLibrary.Resolve(d);
            EffectBundleSettings heal = spells.Find(delegate(EffectBundleSettings s) { return s.Effects[0].Key == "Heal-Health"; });
            if (heal.Effects == null) { Fail("magic heal setup", "no Heal spell"); yield break; }
            bool oldAI = GameManager.Instance.DisableAI;
            GameManager.Instance.DisableAI = true;
            try
            {
                b.SwitchHostile(true);
                b.State.magicCooldown = 0;
                b.EntityBehaviour.Entity.CurrentHealth = Mathf.Max(1, b.EntityBehaviour.Entity.MaxHealth / 3);
                int beforeHealth = b.EntityBehaviour.Entity.CurrentHealth;
                int cost = magic.Cost(heal);
                b.EntityBehaviour.Entity.CurrentMagicka = cost - 1;
                Check("magic rejects insufficient funds", !magic.TryCast(heal) && b.EntityBehaviour.Entity.CurrentMagicka == cost - 1, b.Status);
                b.EntityBehaviour.Entity.CurrentMagicka = cost;
                Check("magic spends exact cost", magic.TryCast(heal) && b.EntityBehaviour.Entity.CurrentMagicka == 0, "cost=" + cost + " " + b.Status);
                Check("magic self healing affects caster", b.EntityBehaviour.Entity.CurrentHealth > beforeHealth, "before=" + beforeHealth + " after=" + b.EntityBehaviour.Entity.CurrentHealth);
                Check("magic cooldown blocks repeated casts", !magic.TryCast(heal) && b.State.magicCooldown >= 4 && b.State.magicCooldown <= 7, "cooldown=" + b.State.magicCooldown);
                d.Magic.UsesMagicka = false;
                b.State.magicCooldown = 0;
                Check("magic unlimited works with zero magicka", magic.TryCast(heal) && b.EntityBehaviour.Entity.CurrentMagicka == 0, b.Status);
                b.State.magicCooldown = 0;
                b.EntityBehaviour.Entity.IsSilenced = true;
                Check("magic unlimited still obeys silence", !magic.TryCast(heal), b.Status);
                b.EntityBehaviour.Entity.IsSilenced = false;
                b.EntityBehaviour.Entity.IsParalyzed = true;
                Check("magic obeys paralysis", !magic.TryCast(heal), b.Status);
                b.EntityBehaviour.Entity.IsParalyzed = false;
                b.SwitchHostile(false);
                Check("magic calm NPC does not cast", !magic.TryCast(heal), b.Status);
                magic.Capture();
                Dictionary<string, NpcState> snapshot = mod.States.Snapshot();
                Check("magic depletion survives snapshot", snapshot.ContainsKey(d.Id) && snapshot[d.Id].magicInitialized && snapshot[d.Id].magicka == 0, b.Status);
                FullSerializer.fsSerializer serializer = new FullSerializer.fsSerializer();
                FullSerializer.fsData encoded;
                NpcSaveData saved = new NpcSaveData { States = snapshot, MagicArea = mod.MagicWorld.Area, MagicVisit = mod.MagicWorld.Visit };
                bool encodedOk = serializer.TrySerialize(saved, out encoded).Succeeded;
                NpcSaveData decoded = null;
                bool decodedOk = serializer.TryDeserialize(encoded, ref decoded).Succeeded;
                Check("magic save data round trips through DFU serializer", encodedOk && decodedOk && decoded != null &&
                    decoded.MagicArea == saved.MagicArea && decoded.MagicVisit == saved.MagicVisit &&
                    decoded.States[d.Id].magicInitialized && decoded.States[d.Id].magicka == 0 &&
                    decoded.States[d.Id].magicVisit == b.State.magicVisit, "serialization failed");
            }
            finally
            {
                GameManager.Instance.DisableAI = oldAI;
                NpcBrain.Discard(b);
            }
            yield return Settle;

            // Exercise autonomous selection and a real offensive missile, not just the direct cast API.
            NpcDefinition attacker = TestDefinition("selftest_magic_ai", Bravery.Brave, location, player, 2f);
            attacker.Magic = new NpcMagicDefinition { UsesMagicka = false, CooldownMin = 4, CooldownMax = 4 };
            attacker.Magic.Spells.Add("Fireball");
            DiscardTestPeople();     // a leftover test ANPC would be its target instead of the player
            // A turret (no walk sheet) stays at its spot: a walking caster can reach melee range before its first cast
            // and then only swings (a swing blocks casting).
            mod.Sprites.Add("selftest_magic_ai", TestSpriteSet(false, "idle", "attack", "death"));
            NpcBrain ai = spawner.SpawnTest(NpcInstance.ForUnique(attacker, "Breton"), location.transform);
            yield return Settle;
            Vector3 aiLine = OpenDirection(player, 12f);
            ai.Teleport(player.position + aiLine * 8f);
            FaceToward(ai, GameManager.Instance.PlayerObject.transform.position);
            ai.SwitchHostile(true);
            NpcMagic caster = ai.GetComponent<NpcMagic>();
            float until = Time.time + 10;
            while (caster.CastCount == 0 && Time.time < until) yield return null;
            Check("magic AI casts offensive spell", caster.CastCount > 0, ai.Status + SensesNote(ai));
            int count = caster.CastCount;
            yield return new WaitForSeconds(2);
            Check("magic AI respects shared cooldown", caster.CastCount == count, "casts=" + caster.CastCount);
            ai.SwitchHostile(false);
            NpcBrain.Discard(ai);
            mod.Sprites.Remove("selftest_magic_ai");
            yield return Settle;

            // A class without DFU spell frames (Bard) casts without entering the Spell state, which would throw.
            NpcDefinition bardDef = TestDefinition("selftest_magic_bard", Bravery.Brave, location, player, -2f);
            bardDef.BaseClass = "Bard";
            bardDef.Magic = new NpcMagicDefinition { UsesMagicka = false };
            bardDef.Magic.Spells.Add("Heal");
            NpcBrain bard = spawner.SpawnTest(NpcInstance.ForUnique(bardDef, "Breton"), location.transform);
            yield return Settle;
            NpcMagic bardMagic = bard.GetComponent<NpcMagic>();
            List<EffectBundleSettings> bardSpells = NpcSpellLibrary.Resolve(bardDef);
            bool bardCast = false;
            string bardError = "";
            bool bardOldAI = GameManager.Instance.DisableAI;
            GameManager.Instance.DisableAI = true;
            try
            {
                bard.SwitchHostile(true);
                bard.State.magicCooldown = 0;
                bardCast = bardMagic != null && bardSpells.Count == 1 && bardMagic.TryCast(bardSpells[0]);
            }
            catch (System.Exception e) { bardError = e.GetType().Name + ": " + e.Message; }
            finally
            {
                GameManager.Instance.DisableAI = bardOldAI;
                MobileStates bardState = bard.GetComponentInChildren<MobileUnit>().EnemyState;
                Check("magic non-caster class casts without DFU spell frames", bardCast && bardError == "" && bardState != MobileStates.Spell,
                    bardError + " state=" + bardState + " " + bard.Status);
                NpcBrain.Discard(bard);
            }
            yield return Settle;

            IEnumerator dunmer = DunmerSpellbladeSteps(location, player);
            while (dunmer.MoveNext()) yield return dunmer.Current;

            IEnumerator mercenary = DunmerMercenarySteps(location, player);
            while (mercenary.MoveNext()) yield return mercenary.Current;

            IEnumerator healing = HealingSteps(location, player);
            while (healing.MoveNext()) yield return healing.Current;

            IEnumerator edge = MagicTownEdgeSteps(location);
            while (edge.MoveNext()) yield return edge.Current;
        }

        public const string HealingSuite = "healing";

        IEnumerator HealingSteps(DaggerfallLocation location, Transform player)
        {
            GameManager gm = GameManager.Instance;
            bool oldAI = gm.DisableAI;
            string oldReaction = NpcGreeter.ReactionOverride;
            int playerHealth = gm.PlayerEntity.CurrentHealth;
            bool playerGodMode = gm.PlayerEntity.GodMode;
            GameObject wall = null;
            NpcBrain caster = null, ally = null;
            string id = "selftest_healing";
            string soundsPath = System.IO.Path.Combine(Application.temporaryCachePath, "anpc_selftest_healing", "Sounds");
            System.IO.Directory.CreateDirectory(soundsPath);
            foreach (string name in new[] { "heal_1", "heal_2", "cast_1" })
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(soundsPath, name + ".wav"), ToneWav(0.1f));
            LoadedSoundSet soundSet = SoundLibrary.IndexSet(id + "/Sounds", soundsPath);
            mod.Sounds.Add(id, soundSet);
            mod.Sprites.Add(id, TestSpriteSet(false, "idle", "attack", "cast", "heal_1", "heal_2"));
            DiscardTestPeople();
            Vector3 open = OpenDirection(player, 14f);
            try
            {
                gm.DisableAI = true;
                NpcGreeter.ReactionOverride = "likes";
                NpcDefinition d = TestDefinition(id, Bravery.Brave, location, player, OutOfGreeting);
                d.BaseClass = "Mage";
                d.Sounds = new SoundSettings { CombatChance = 100, GreetChance = 0 };
                d.Magic = new NpcMagicDefinition { UsesMagicka = true, Healing = new NpcHealingDefinition() };
                d.Magic.Spells.Add("Heal");
                caster = SpawnDefinition(d, location);
                ally = Make("selftest_healing_ally", Bravery.Brave, location, player, -OutOfGreeting);
                yield return Settle;
                NpcMagic magic = caster.GetComponent<NpcMagic>();
                magic.enabled = false; // final cast APIs stay testable while autonomous decisions are isolated
                EffectBundleSettings heal = NpcSpellLibrary.Resolve(d)[0];
                NpcSprite sprite = caster.GetComponent<NpcSprite>();
                NpcVoice voice = caster.GetComponent<NpcVoice>();
                float until = Time.time + 5f;
                while ((!soundSet.IsReady || !sprite.HasSet) && Time.time < until) yield return null;
                caster.Teleport(player.position + open * 6f);
                ally.Teleport(player.position + open * 7.5f);
                Physics.SyncTransforms();
                ally.EntityBehaviour.Entity.CurrentHealth = Mathf.Max(1, ally.EntityBehaviour.Entity.MaxHealth / 3);
                int before = ally.EntityBehaviour.Entity.CurrentHealth;
                int cost = magic.Cost(magic.SupportSpell(heal));
                caster.EntityBehaviour.Entity.CurrentMagicka = cost - 1;
                Check("healing refuses insufficient touch-cost magicka", !magic.TryHeal(heal, ally.EntityBehaviour) &&
                    caster.EntityBehaviour.Entity.CurrentMagicka == cost - 1 && ally.EntityBehaviour.Entity.CurrentHealth == before, "cost=" + cost);
                caster.EntityBehaviour.Entity.CurrentMagicka = cost;
                Check("healing applies to calm ally and spends exact touch cost", magic.TryHeal(heal, ally.EntityBehaviour) &&
                    caster.EntityBehaviour.Entity.CurrentMagicka == 0 && ally.EntityBehaviour.Entity.CurrentHealth > before, caster.Status);
                Check("healing retains self spell and does not provoke ally", heal.TargetType == TargetTypes.CasterOnly &&
                    ally.CurrentMode == NpcMode.Calm && ally.Threat == null && !ally.State.hostile, ally.Status);
                Check("healing chooses dedicated numbered sheet and heal sound", sprite.CurrentState == SpriteStates.Heal &&
                    (sprite.CurrentAnimation == "heal_1" || sprite.CurrentAnimation == "heal_2") && voice.Played(SoundPools.Heal) == 1 &&
                    voice.Played(SoundPools.Cast) == 0 && !sprite.BlowPending, sprite.CurrentAnimation);
                yield return null;
                Check("healing sheet survives vanilla caster Spell state", sprite.CurrentState == SpriteStates.Heal, sprite.CurrentState);
                Check("healing shares cooldown", !magic.TryHeal(heal, ally.EntityBehaviour), "cooldown=" + caster.State.magicCooldown);
                caster.State.magicCooldown = 0;
                d.Magic.UsesMagicka = false;
                ally.EntityBehaviour.Entity.CurrentHealth = Mathf.Max(1, ally.EntityBehaviour.Entity.MaxHealth / 3);
                caster.EntityBehaviour.Entity.IsSilenced = true;
                Check("healing unlimited still obeys silence", !magic.TryHeal(heal, ally.EntityBehaviour), caster.Status);
                caster.EntityBehaviour.Entity.IsSilenced = false;
                caster.EntityBehaviour.Entity.IsParalyzed = true;
                Check("healing obeys paralysis", !magic.TryHeal(heal, ally.EntityBehaviour), caster.Status);
                caster.EntityBehaviour.Entity.IsParalyzed = false;
                d.Magic.Healing.OutsideCombat = false;
                Check("healing can be restricted to fighting", !magic.TryHeal(heal, ally.EntityBehaviour), caster.Status);
                d.Magic.Healing.OutsideCombat = true;
                d.Magic.Healing.Allies = false;
                Check("healing respects allies opt-out", !magic.TryHeal(heal, ally.EntityBehaviour), ally.Status);
                d.Magic.Healing.Allies = true;
                ally.Teleport(player.position + open * 10f);
                Physics.SyncTransforms();
                Check("healing rejects recipients beyond touch range", !magic.TryHeal(heal, ally.EntityBehaviour), ally.Status);
                ally.Teleport(player.position + open * 7.5f);
                Physics.SyncTransforms();
                wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.position = (caster.transform.position + ally.transform.position) * 0.5f + Vector3.up * 0.5f;
                wall.transform.localScale = new Vector3(0.6f, 3f, 0.6f);
                Physics.SyncTransforms();
                Check("healing rejects blocked sight", !magic.TryHeal(heal, ally.EntityBehaviour), caster.Status);
                wall.SetActive(false);
                Object.Destroy(wall);
                wall = null;
                ally.EntityBehaviour.Entity.CurrentHealth = ally.EntityBehaviour.Entity.MaxHealth;
                Check("healing rejects full health", !magic.TryHeal(heal, ally.EntityBehaviour), ally.Status);
                ally.EntityBehaviour.Entity.CurrentHealth = 0;
                Check("healing does not revive dead recipients", !magic.TryHeal(heal, ally.EntityBehaviour), ally.Status);
                NpcBrain.Discard(ally);
                ally = Make("selftest_healing_ally2", Bravery.Brave, location, player, -OutOfGreeting);
                yield return Settle;
                ally.Teleport(player.position + open * 7.5f);
                ally.EntityBehaviour.Entity.CurrentHealth = Mathf.Max(1, ally.EntityBehaviour.Entity.MaxHealth / 3);
                Physics.SyncTransforms();
                EffectBundleSettings mixed = heal;
                mixed.Effects = new[] { heal.Effects[0], new EffectEntry("Damage-Health", heal.Effects[0].Settings) };
                Check("healing rejects mixed harmful effects", !magic.TryHeal(mixed, ally.EntityBehaviour), caster.Status);
                caster.SwitchHostile(true);
                caster.State.magicCooldown = 0;
                caster.GetComponent<EnemySenses>().Target = gm.PlayerEntityBehaviour;
                Check("healing rejects opposite allegiance", !magic.TryHeal(heal, ally.EntityBehaviour), ally.Status);
                ally.SwitchHostile(true);
                ally.GetComponent<EnemySenses>().Target = gm.PlayerEntityBehaviour;
                Check("healing enemy can heal allied ANPC without losing foe", magic.TryHeal(heal, ally.EntityBehaviour) &&
                    caster.GetComponent<EnemySenses>().Target == gm.PlayerEntityBehaviour, caster.Status);
                caster.State.magicCooldown = 0;
                ally.EntityBehaviour.Entity.CurrentHealth = Mathf.Max(1, ally.EntityBehaviour.Entity.MaxHealth / 3);
                ally.GetComponent<EnemySenses>().Target = caster.EntityBehaviour;
                Check("healing rejects an ally attacking the healer", !magic.TryHeal(heal, ally.EntityBehaviour), ally.Status);
                caster.SwitchHostile(false);
                ally.SwitchHostile(false);
                caster.State.magicCooldown = 0;
                caster.Teleport(player.position + open * 1.5f);
                ally.Teleport(player.position + open * 8f);
                Physics.SyncTransforms();
                // The surrounding self-test enables god mode, whose SetHealth always restores full health.
                gm.PlayerEntity.GodMode = false;
                gm.PlayerEntity.CurrentHealth = Mathf.Max(1, gm.PlayerEntity.MaxHealth / 3);
                NpcGreeter.ReactionOverride = "neutral";
                Check("healing neutral player is ineligible", !magic.TryHeal(heal, gm.PlayerEntityBehaviour), caster.Status);
                NpcGreeter.ReactionOverride = "likes";
                d.Magic.Healing.Player = "none";
                Check("healing respects player opt-out", !magic.TryHeal(heal, gm.PlayerEntityBehaviour), caster.Status);
                d.Magic.Healing.Player = "friendly";
                caster.State.hostile = true;
                Check("healing angry caster refuses player", !magic.TryHeal(heal, gm.PlayerEntityBehaviour), caster.Status);
                caster.State.hostile = false;
                before = gm.PlayerEntity.CurrentHealth;
                Check("healing friendly player with zero magicka", magic.TryHeal(heal, gm.PlayerEntityBehaviour) &&
                    gm.PlayerEntity.CurrentHealth > before && caster.EntityBehaviour.Entity.CurrentMagicka == 0, caster.Status);
                d.Sounds.CombatChance = 0;
                Check("healing sound obeys zero combatChance", !voice.Play(SoundPools.Heal), "played=" + voice.Played(SoundPools.Heal));
                d.Sounds.CombatChance = 100;
                Check("healing sound variants use their own pool", soundSet.Clips[SoundPools.Heal].Count == 2 && voice.Play(SoundPools.Heal), soundSet.Summary());
                sprite.Swap(TestSpriteSet(false, "idle", "attack", "cast"));
                sprite.PlayCast(true);
                Check("healing falls back to cast sheet", sprite.CurrentAnimation == "cast" && !sprite.BlowPending, sprite.CurrentAnimation);
                sprite.Swap(TestSpriteSet(false, "idle", "attack"));
                sprite.PlayCast(true);
                Check("healing falls back to attack without physical blow", sprite.CurrentAnimation == "attack" && !sprite.BlowPending, sprite.CurrentAnimation);
                sprite.Swap(TestSpriteSet(false, "idle"));
                sprite.PlayCast(true);
                Check("healing without any compatible sheet has no animation", sprite.CurrentState == SpriteStates.Idle && !sprite.PlayingOneShot, sprite.CurrentState);
                sprite.Swap(TestSpriteSet(false, "idle", "attack", "cast", "heal_1", "heal_2"));
                sprite.PlayCast();
                Check("healing does not change subsequent offensive cast animation", sprite.CurrentState == SpriteStates.Cast && sprite.CurrentAnimation == "cast", sprite.CurrentState);
                // Autonomous calm casting and selection of the most wounded eligible recipient.
                gm.PlayerEntity.CurrentHealth = gm.PlayerEntity.MaxHealth * 4 / 10;
                ally.Teleport(player.position + open * 2.8f);
                ally.EntityBehaviour.Entity.CurrentHealth = Mathf.Max(1, ally.EntityBehaviour.Entity.MaxHealth / 5);
                Physics.SyncTransforms();
                before = ally.EntityBehaviour.Entity.CurrentHealth;
                int playerBefore = gm.PlayerEntity.CurrentHealth;
                caster.State.magicCooldown = 0;
                gm.DisableAI = false;
                magic.enabled = true;
                until = Time.time + 5f;
                while (ally.EntityBehaviour.Entity.CurrentHealth == before && Time.time < until) yield return null;
                magic.enabled = false;
                gm.DisableAI = true;
                Check("healing AI picks most wounded nearby ally while calm", ally.EntityBehaviour.Entity.CurrentHealth > before &&
                    gm.PlayerEntity.CurrentHealth == playerBefore, ally.Status);
                caster.SwitchHostile(true);
                caster.EntityBehaviour.Entity.CurrentHealth = Mathf.Max(1, caster.EntityBehaviour.Entity.MaxHealth / 3);
                before = caster.EntityBehaviour.Entity.CurrentHealth;
                caster.State.magicCooldown = 0;
                Check("healing self spell still works with dedicated heal presentation", magic.TryCast(heal) &&
                    caster.EntityBehaviour.Entity.CurrentHealth > before && sprite.CurrentState == SpriteStates.Heal, caster.Status);
            }
            finally
            {
                gm.DisableAI = oldAI;
                gm.PlayerEntity.CurrentHealth = playerHealth;
                gm.PlayerEntity.GodMode = playerGodMode;
                NpcGreeter.ReactionOverride = oldReaction;
                if (wall != null) Object.Destroy(wall);
                if (caster != null) NpcBrain.Discard(caster);
                if (ally != null) NpcBrain.Discard(ally);
                mod.Sprites.Remove(id);
                mod.Sounds.Remove(id);
            }
            yield return Settle;
        }

        /// <summary>The outdoors around a town is one magic area: past the town's edge, town casters keep casting and do not refill.</summary>
        IEnumerator MagicTownEdgeSteps(DaggerfallLocation location)
        {
            GameManager gm = GameManager.Instance;
            NpcMagicWorld world = mod.MagicWorld;
            world.Refresh();
            Check("magic town outdoors is keyed by the town", world.Area == "out:" + location.Summary.MapID, world.Area);
            long visit = world.Visit;

            GameObject playerObject = gm.PlayerObject;
            CharacterController controller = playerObject.GetComponent<CharacterController>();
            Vector3 home = location.transform.InverseTransformPoint(playerObject.transform.position);
            // The town object's origin is its south-west corner; stand a little outside it.
            Vector3 outside = location.transform.position + new Vector3(-30f, 0f, -30f);
            RaycastHit ground;
            if (Physics.Raycast(outside + Vector3.up * 500f, Vector3.down, out ground, 1000f, ~0, QueryTriggerInteraction.Ignore))
                outside = ground.point + Vector3.up * 1.2f;
            MovePlayer(controller, playerObject.transform, outside);
            for (int i = 0; i < 120 && gm.PlayerGPS.IsPlayerInLocationRect; i++)
                yield return null;
            if (gm.PlayerGPS.IsPlayerInLocationRect)
            {
                Fail("magic town edge: player left the town rect", "still inside at " + playerObject.transform.position);
                MovePlayer(controller, playerObject.transform, location.transform.TransformPoint(home));
                yield break;
            }
            world.Refresh();
            Check("magic walking past the town edge is the same visit", world.Area == "out:" + location.Summary.MapID && world.Visit == visit,
                world.Area + " visit " + world.Visit + " (was " + visit + ")");

            NpcDefinition d = TestDefinition("selftest_magic_edge", Bravery.Brave, location, playerObject.transform, 0f);
            d.Magic = new NpcMagicDefinition { UsesMagicka = true, MaxMagicka = 150, CooldownMin = 4, CooldownMax = 4 };
            d.Magic.Spells.Add("Fireball");
            DiscardTestPeople();
            mod.Sprites.Add("selftest_magic_edge", TestSpriteSet(false, "idle", "attack", "death"));     // a turret: stays at 8 m
            NpcBrain b = spawner.SpawnTest(NpcInstance.ForUnique(d, "Breton"), location.transform);
            yield return Settle;
            Vector3 edgeLine = OpenDirection(playerObject.transform, 12f);
            b.Teleport(playerObject.transform.position + edgeLine * 8f);
            NpcMagic magic = b.GetComponent<NpcMagic>();
            try
            {
                Check("magic town caster past the edge is still in the area", magic != null && world.Contains(magic), b.Status);
                b.EntityBehaviour.Entity.CurrentMagicka = 100;
                FaceToward(b, GameManager.Instance.PlayerObject.transform.position);
                b.SwitchHostile(true);
            }
            catch (System.Exception e) { Fail("magic town edge setup", e.Message); }
            float until = Time.time + 10;
            while (magic != null && magic.CastCount == 0 && Time.time < until) yield return null;
            Check("magic town caster keeps casting past the town edge", magic != null && magic.CastCount > 0, b.Status + SensesNote(b));
            Check("magic town caster past the edge spends rather than refills", magic != null && b.EntityBehaviour.Entity.CurrentMagicka < 100,
                b.Status);
            b.SwitchHostile(false);
            NpcBrain.Discard(b);
            mod.Sprites.Remove("selftest_magic_edge");

            MovePlayer(controller, playerObject.transform, location.transform.TransformPoint(home));
            for (int i = 0; i < 120 && !gm.PlayerGPS.IsPlayerInLocationRect; i++)
                yield return null;
            world.Refresh();
            Check("magic walking back into town is still the same visit", world.Visit == visit, "visit " + world.Visit + " (was " + visit + ")");
            yield return Settle;
        }

        /// <summary>What the ANPC's DFU senses see (for failure details).</summary>
        static string SensesNote(NpcBrain b)
        {
            EnemySenses s = b != null ? b.GetComponent<EnemySenses>() : null;
            if (s == null)
                return "";
            return ", target=" + (s.Target == null ? "none" : s.Target == GameManager.Instance.PlayerEntityBehaviour ? "player" : s.Target.name) +
                   ", sight=" + s.TargetInSight + ", distance=" + s.DistanceToTarget.ToString("0.0");
        }

        static void MovePlayer(CharacterController controller, Transform player, Vector3 position)
        {
            if (controller != null) controller.enabled = false;
            player.position = position;
            if (controller != null) controller.enabled = true;
        }

        // Optional sample asset checks: also works when users install the mod without its examples.
        IEnumerator DunmerSpellbladeSteps(DaggerfallLocation location, Transform player)
        {
            NpcDefinition sample = mod.Catalog.Generics.Find(delegate(NpcDefinition g) { return g.Id == "dunmer_spellblade"; });
            if (sample == null) yield break;
            string namePath = System.IO.Path.Combine(AnpcFiles.Root, "_Namelists/dunmer_morrowind.json");
            NameListResult nameList = NameListParser.Parse("dunmer_morrowind", System.IO.File.ReadAllText(namePath));
            bool namesOk = nameList.List != null && sample.NameList == "dunmer_morrowind" && string.IsNullOrEmpty(sample.Name);
            if (namesOk)
            {
                foreach (string gender in new[] { "Male", "Female" })
                {
                    string generated = mod.Names.Generate(sample.NameList, sample.Race, gender, 123u);
                    string expected = NameGenerator.Generate(nameList.List, gender, new SeededRandom(123u), "sen");
                    namesOk = namesOk && generated == expected && generated.Split(' ').Length == 2;
                }
            }
            Check("Dunmer Morrowind namelist generates seeded male and female names", namesOk, nameList.Error);
            NpcDefinition d = TestDefinition("selftest_dunmer_spellblade", Bravery.Brave, location, player, 0f);
            d.Folder = sample.Folder;
            d.Race = sample.Race;
            d.Gender = sample.Gender;
            d.Magic = sample.Magic;
            d.Portraits.AddRange(sample.Portraits);
            NpcBrain b = spawner.SpawnTest(NpcInstance.ForUnique(d, "Breton"), location.transform);
            yield return Settle;
            NpcSprite sprite = b.GetComponent<NpcSprite>();
            float readyUntil = Time.time + 10f;
            while (sprite != null && sprite.Pending && Time.time < readyUntil)
                yield return null;     // sprite sets load on demand
            Check("Dunmer sample has its race, portrait and full rendered set", b.Instance.Race == "DarkElf" &&
                mod.Portraits.Get("dunmer_spellblade") != null && sprite != null && sprite.ChosenSet.Set.Animations.Count == 12,      // talk_1 included: it converses
                sprite == null ? "no sprite set" : sprite.ChosenSet.Label);
            if (sprite == null) { NpcBrain.Discard(b); yield break; }
            Check("Dunmer melee impact markers loaded", sprite.ChosenSet.Set.Animations["attack_1"].ActionFrame == 2 &&
                sprite.ChosenSet.Set.Animations["attack_2"].ActionFrame == 1, "expected frames 3 and 2, counted from 1");
            bool oldAI = GameManager.Instance.DisableAI;
            GameManager.Instance.DisableAI = true;
            try
            {
                Vector3 toCam = Camera.main.transform.position - b.transform.position;
                toCam.y = 0;
                b.transform.rotation = Quaternion.LookRotation(toCam.normalized);
                yield return new WaitForSeconds(0.5f);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(Application.persistentDataPath, "selftest-dunmer-front.png"));
                yield return new WaitForSeconds(0.5f);
                b.transform.rotation = Quaternion.LookRotation(new Vector3(-toCam.z, 0, toCam.x));
                yield return new WaitForSeconds(0.5f);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(Application.persistentDataPath, "selftest-dunmer-side.png"));
                yield return new WaitForSeconds(0.5f);
                b.SwitchHostile(true);
                b.State.magicCooldown = 0;
                b.EntityBehaviour.Entity.CurrentMagicka = 150;
                NpcMagic caster = b.GetComponent<NpcMagic>();
                EffectBundleSettings heal = NpcSpellLibrary.Resolve(d).Find(delegate(EffectBundleSettings spell) { return spell.Effects[0].Key == "Heal-Health"; });
                Check("Dunmer configured spell casts", caster != null && caster.TryCast(heal), b.Status);
                yield return null;
                yield return null;
                Check("Dunmer healing uses dedicated sheet without melee blow", sprite.CurrentState == SpriteStates.Heal &&
                    sprite.CurrentAnimation == "cast_1" && !sprite.BlowPending,
                    "state=" + sprite.CurrentState + " animation=" + sprite.CurrentAnimation);
                int hits = sprite.HitStarts;
                b.EntityBehaviour.Entity.DecreaseHealth(1);
                yield return null;
                Check("Dunmer hit waits while healing sheet finishes", sprite.CurrentState == SpriteStates.Heal && sprite.HitStarts == hits,
                    "state=" + sprite.CurrentState);
                yield return new WaitForSeconds(0.8f);
                Check("Dunmer deferred hit plays after casting", sprite.HitStarts == hits + 1, "hits=" + sprite.HitStarts);
            }
            finally
            {
                GameManager.Instance.DisableAI = oldAI;
                NpcBrain.Discard(b);
            }
            yield return Settle;
        }
        IEnumerator DunmerMercenarySteps(DaggerfallLocation location, Transform player)
        {
            NpcDefinition sample = mod.Catalog.Generics.Find(delegate(NpcDefinition g) { return g.Id == "dunmer_mercenary"; });
            if (sample == null) yield break;
            List<EffectBundleSettings> spells = NpcSpellLibrary.Resolve(sample);
            EffectBundleSettings resistance = spells.Find(delegate(EffectBundleSettings spell) { return spell.Effects[0].Key == "ElementalResistance-Fire"; });
            int fireMissiles = spells.FindAll(delegate(EffectBundleSettings spell) { return spell.ElementType == ElementTypes.Fire &&
                (spell.TargetType == TargetTypes.SingleTargetAtRange || spell.TargetType == TargetTypes.AreaAtRange); }).Count;
            Check("mercenary fire spellbook resolves completely", spells.Count == 3 && fireMissiles == 2 && resistance.Effects != null,
                "resolved=" + spells.Count + " fire missiles=" + fireMissiles);
            NpcDefinition d = TestDefinition("selftest_dunmer_mercenary", Bravery.Brave, location, player, 0f);
            d.Folder = sample.Folder;
            d.Race = sample.Race;
            d.Gender = sample.Gender;
            d.Magic = sample.Magic;
            d.Portraits.AddRange(sample.Portraits);
            NpcBrain b = spawner.SpawnTest(NpcInstance.ForUnique(d, "Breton"), location.transform);
            yield return Settle;
            NpcSprite sprite = b.GetComponent<NpcSprite>();
            float readyUntil = Time.time + 10f;
            while (sprite != null && sprite.Pending && Time.time < readyUntil)
                yield return null;     // sprite sets load on demand
            NpcMagic caster = b.GetComponent<NpcMagic>();
            Check("mercenary custom sheets and portrait load", sprite != null && sprite.ChosenSet.Set.Animations.Count == 12 &&
                mod.Portraits.Get("dunmer_mercenary") != null && b.Instance.Race == "DarkElf",
                sprite == null ? "no sprite set" : sprite.ChosenSet.Label);
            Check("mercenary keeps original melee impact frames", sprite != null &&
                sprite.ChosenSet.Set.Animations["attack_1"].ActionFrame == 2 && sprite.ChosenSet.Set.Animations["attack_2"].ActionFrame == 1,
                "expected impact frames 3 and 2");
            Check("mercenary starts with smaller magicka pool", caster != null && b.EntityBehaviour.Entity.MaxMagicka == 80 &&
                b.EntityBehaviour.Entity.CurrentMagicka == 80 && sample.NameList == "dunmer_morrowind", b.Status);
            bool oldAI = GameManager.Instance.DisableAI;
            GameManager.Instance.DisableAI = true;
            try
            {
                bool affordable = caster != null && spells.Count == 3;
                foreach (EffectBundleSettings spell in spells) affordable = affordable && caster.Cost(spell) <= 80;
                Check("mercenary can afford each configured spell when full", affordable, b.Status);
                if (caster != null && resistance.Effects != null)
                {
                    b.SwitchHostile(true);
                    b.State.magicCooldown = 0;
                    int cost = caster.Cost(resistance);
                    Check("mercenary casts fire resistance and spends magicka", caster.TryCast(resistance) &&
                        b.EntityBehaviour.Entity.CurrentMagicka == 80 - cost, "cost=" + cost + " " + b.Status);
                    yield return null;
                    yield return null;
                    Check("mercenary uses its casting sheet", sprite != null && sprite.CurrentState == SpriteStates.Cast &&
                        sprite.CurrentAnimation == "cast_1" && !sprite.BlowPending, b.Status);
                    b.State.magicCooldown = 0;
                    b.EntityBehaviour.Entity.CurrentMagicka = Mathf.Max(0, cost - 1);
                    Check("mercenary rejects casts when magicka runs low", !caster.TryCast(resistance), b.Status);
                }
            }
            finally
            {
                GameManager.Instance.DisableAI = oldAI;
                NpcBrain.Discard(b);
            }
            yield return Settle;
        }
    }
}

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
            NpcBrain ai = spawner.SpawnTest(NpcInstance.ForUnique(attacker, "Breton"), location.transform);
            yield return Settle;
            ai.SwitchHostile(true);
            NpcMagic caster = ai.GetComponent<NpcMagic>();
            float until = Time.time + 10;
            while (caster.CastCount == 0 && Time.time < until) yield return null;
            Check("magic AI casts offensive spell", caster.CastCount > 0, ai.Status);
            int count = caster.CastCount;
            yield return new WaitForSeconds(2);
            Check("magic AI respects shared cooldown", caster.CastCount == count, "casts=" + caster.CastCount);
            ai.SwitchHostile(false);
            NpcBrain.Discard(ai);
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

            IEnumerator edge = MagicTownEdgeSteps(location);
            while (edge.MoveNext()) yield return edge.Current;
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
            NpcBrain b = spawner.SpawnTest(NpcInstance.ForUnique(d, "Breton"), location.transform);
            yield return Settle;
            NpcMagic magic = b.GetComponent<NpcMagic>();
            try
            {
                Check("magic town caster past the edge is still in the area", magic != null && world.Contains(magic), b.Status);
                b.EntityBehaviour.Entity.CurrentMagicka = 100;
                b.SwitchHostile(true);
            }
            catch (System.Exception e) { Fail("magic town edge setup", e.Message); }
            float until = Time.time + 10;
            while (magic != null && magic.CastCount == 0 && Time.time < until) yield return null;
            Check("magic town caster keeps casting past the town edge", magic != null && magic.CastCount > 0, b.Status);
            Check("magic town caster past the edge spends rather than refills", magic != null && b.EntityBehaviour.Entity.CurrentMagicka < 100,
                b.Status);
            b.SwitchHostile(false);
            NpcBrain.Discard(b);

            MovePlayer(controller, playerObject.transform, location.transform.TransformPoint(home));
            for (int i = 0; i < 120 && !gm.PlayerGPS.IsPlayerInLocationRect; i++)
                yield return null;
            world.Refresh();
            Check("magic walking back into town is still the same visit", world.Visit == visit, "visit " + world.Visit + " (was " + visit + ")");
            yield return Settle;
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
            Check("Dunmer sample has its race, portrait and full rendered set", b.Instance.Race == "DarkElf" &&
                mod.Portraits.Get("dunmer_spellblade") != null && sprite != null && sprite.Set.Set.Animations.Count == 10,
                sprite == null ? "no sprite set" : sprite.Set.Label);
            if (sprite == null) { NpcBrain.Discard(b); yield break; }
            Check("Dunmer melee impact markers loaded", sprite.Set.Set.Animations["attack_1"].ActionFrame == 2 &&
                sprite.Set.Set.Animations["attack_2"].ActionFrame == 1, "expected frames 3 and 2, counted from 1");
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
                Check("Dunmer casting uses dedicated sheet without melee blow", sprite.CurrentState == SpriteStates.Cast &&
                    sprite.CurrentAnimation == "cast_1" && !sprite.BlowPending,
                    "state=" + sprite.CurrentState + " animation=" + sprite.CurrentAnimation);
                int hits = sprite.HitStarts;
                b.EntityBehaviour.Entity.DecreaseHealth(1);
                yield return null;
                Check("Dunmer hit waits while cast sheet finishes", sprite.CurrentState == SpriteStates.Cast && sprite.HitStarts == hits,
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
            NpcMagic caster = b.GetComponent<NpcMagic>();
            Check("mercenary custom sheets and portrait load", sprite != null && sprite.Set.Set.Animations.Count == 10 &&
                mod.Portraits.Get("dunmer_mercenary") != null && b.Instance.Race == "DarkElf",
                sprite == null ? "no sprite set" : sprite.Set.Label);
            Check("mercenary keeps original melee impact frames", sprite != null &&
                sprite.Set.Set.Animations["attack_1"].ActionFrame == 2 && sprite.Set.Set.Animations["attack_2"].ActionFrame == 1,
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

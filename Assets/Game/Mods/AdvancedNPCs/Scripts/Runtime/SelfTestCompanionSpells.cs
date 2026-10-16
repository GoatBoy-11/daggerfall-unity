using System;
using System.Collections;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.MagicAndEffects;
using DaggerfallWorkshop.Game.Serialization;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    public partial class SelfTest
    {
        public const string CompanionSpellsSuite = "companionspells";
        IEnumerator CompanionSpellsSteps(DaggerfallLocation location, Transform player)
        {
            var gm=GameManager.Instance;
            Vector3 oldPosition=player.position;Quaternion oldRotation=player.rotation,oldView=gm.MainCamera.transform.rotation;
            bool oldAI=gm.DisableAI,look=gm.PlayerMouseLook.enabled,god=gm.PlayerEntity.GodMode;
            string reaction=NpcGreeter.ReactionOverride;int playerHealth=gm.PlayerEntity.CurrentHealth;
            NpcBrain actor=null,ally=null,foe=null,neutral=null;
            var floor=new GameObject("ANPC companion casting verification");floor.transform.SetParent(location.transform,false);
            Vector3 origin=oldPosition+Vector3.up*24;
            NavigationBox(floor.transform,"spell floor",origin-Vector3.up*.15f,new Vector3(100,.3f,50));
            const string key="selftest_companion_mage";
            var definition=TestDefinition(key,Bravery.Brave,location,player,0);definition.BaseClass="Mage";definition.RecruitCost=0;
            definition.Magic=new NpcMagicDefinition{MaxMagicka=1000,CooldownMin=2,CooldownMax=2,RestHours=1};
            definition.Magic.Spells.AddRange(new[]{"Heal","Shield","Fireball"});
            mod.Catalog.ById.Add(key,definition);
            mod.Sprites.Add(key,TestSpriteSet(false,"idle","walk","attack","cast","heal"));
            try
            {
                gm.DisableAI=true;gm.PlayerMouseLook.enabled=false;NavigationPlayerAt(player,origin+new Vector3(0,1,5),Vector3.right);
                actor=CombatPerson(location,definition,key,origin+new Vector3(0,1,0),false);
                var allyDef=TestDefinition("selftest_spell_ally",Bravery.Brave,location,player,0);allyDef.RecruitCost=0;mod.Catalog.ById.Add(allyDef.Id,allyDef);
                ally=CombatPerson(location,allyDef,"selftest_spell_ally",origin+new Vector3(0,1,1.8f),true);
                var foeDef=TestDefinition("selftest_spell_foe",Bravery.Brave,location,player,0);foeDef.BaseClass="Warrior";foeDef.FleeChance=0;foeDef.CrimeOnAttack=false;
                foe=CombatPerson(location,foeDef,"selftest_spell_foe",origin+new Vector3(12,1,0),false);
                neutral=CombatPerson(location,TestDefinition("selftest_spell_neutral",Bravery.Brave,location,player,0),"selftest_spell_neutral",origin+new Vector3(7,1,8),false);
                yield return new WaitForSecondsRealtime(.6f);
                var magic=actor.GetComponent<NpcMagic>();var entity=actor.EntityBehaviour.Entity;foe.EntityBehaviour.Entity.MaxHealth=10000;foe.EntityBehaviour.Entity.CurrentHealth=10000;neutral.enabled=false;neutral.Motor.enabled=false;neutral.GetComponent<EnemySenses>().enabled=false;
                var book=NpcSpellLibrary.Resolve(definition);
                var heal=book.Find(delegate(EffectBundleSettings s){return NpcSpellLibrary.IsHealthHeal(s);});
                var shield=book.Find(delegate(EffectBundleSettings s){return NpcSpellLibrary.IsProtection(s);});
                var fire=book.Find(delegate(EffectBundleSettings s){return s.TargetType!=TargetTypes.CasterOnly;});
                Check("companion caster resolves configured book only",magic.SpellCount==3 && entity.SpellbookCount()==0,"count="+magic.SpellCount);
                entity.CurrentMagicka=75;actor.State.magicCooldown=7;
                Check("recruitment preserves depleted mana",mod.Companions.Command(actor,"recruit") && entity.CurrentMagicka==75,actor.Status);
                magic.enabled=false;
                ulong now=DaggerfallUnity.Instance.WorldTime.Now.ToSeconds();
                actor.State.magicka=75;actor.State.magicUpdatedAt=now-3600;actor.State.magicRestAfter=now-3600;
                NpcMagicRules.Recover(definition.Magic,actor.State,now,false);
                Check("hired onAreaEntry caster gains timed mana without JSON mutation",actor.State.magicka==1000 && definition.Magic.Recovery=="onAreaEntry","mana="+actor.State.magicka);
                entity.CurrentMagicka=75;actor.State.magicka=75;actor.State.magicCooldown=7;
                NpcMagicRules.Enter(definition.Magic,actor.State,mod.MagicWorld.Visit+1,now);
                Check("companion area entry preserves mana and cooldown",actor.State.magicka==75 && actor.State.magicCooldown==7,"mana="+actor.State.magicka);
                actor.State.magicCooldown=0;actor.State.magicVisit=mod.MagicWorld.Visit;
                NpcGreeter.ReactionOverride="neutral";gm.DisableAI=false;
                ally.EntityBehaviour.Entity.CurrentHealth=Mathf.Max(1,ally.EntityBehaviour.Entity.MaxHealth/3);
                int before=ally.EntityBehaviour.Entity.CurrentHealth,cost=magic.Cost(magic.SupportSpell(heal));entity.CurrentMagicka=cost;
                Check("party healing ignores town reaction and uses exact shared cost",magic.TryHeal(heal,ally.EntityBehaviour) && entity.CurrentMagicka==0 && ally.EntityBehaviour.Entity.CurrentHealth>before,actor.Status);
                Check("party healing shares offensive cooldown",!magic.TryCast(fire),"cooldown="+actor.State.magicCooldown);
                actor.State.magicCooldown=0;entity.CurrentMagicka=1000;
                neutral.Teleport(actor.transform.position+Vector3.right*1.8f);neutral.EntityBehaviour.Entity.CurrentHealth=1;Physics.SyncTransforms();
                Check("companion support does not heal unrelated friendly townsfolk",!magic.TryHeal(heal,neutral.EntityBehaviour),neutral.Status);
                neutral.Teleport(origin+new Vector3(7,1,8));
                NavigationPlayerAt(player,actor.transform.position+Vector3.forward*1.5f,Vector3.right);gm.PlayerEntity.GodMode=false;gm.PlayerEntity.CurrentHealth=Mathf.Max(1,gm.PlayerEntity.MaxHealth/3);
                before=gm.PlayerEntity.CurrentHealth;
                Check("hired healer can help neutral-reaction player",magic.TryHeal(heal,gm.PlayerEntityBehaviour) && gm.PlayerEntity.CurrentHealth>before,actor.Status);
                actor.State.magicCooldown=0;definition.Magic.Healing=new NpcHealingDefinition{Player="none",Allies=false};gm.PlayerEntity.CurrentHealth=1;
                Check("explicit healing player opt-out is retained",!magic.TryHeal(heal,gm.PlayerEntityBehaviour),actor.Status);
                ally.EntityBehaviour.Entity.CurrentHealth=1;
                Check("explicit healing allies opt-out is retained",!magic.TryHeal(heal,ally.EntityBehaviour),actor.Status);
                definition.Magic.Healing=null;gm.PlayerEntity.GodMode=true;NavigationPlayerAt(player,origin+new Vector3(0,1,5),Vector3.right);
                ally.EntityBehaviour.Entity.CurrentHealth=ally.EntityBehaviour.Entity.MaxHealth;
                foe.SwitchHostile(true);AimCombat(foe,gm.PlayerEntityBehaviour);foe.enabled=false;
                actor.CompanionCombat.SetStance("passive");yield return new WaitForSecondsRealtime(.4f);
                actor.State.magicCooldown=0;entity.CurrentHealth=Mathf.Max(1,entity.MaxHealth/5);before=entity.CurrentHealth;
                Check("passive retreat permits emergency self healing",magic.TryHeal(heal,actor.EntityBehaviour) && entity.CurrentHealth>before,actor.Describe());
                actor.State.magicCooldown=0;entity.CurrentHealth=entity.MaxHealth;
                Check("passive support cannot release offensive spells",!magic.TryCast(fire),actor.Describe());
                actor.State.magicCooldown=0;entity.IsSilenced=true;
                Check("companion support obeys silence",!magic.TryBuff(shield,actor.EntityBehaviour),actor.Describe());entity.IsSilenced=false;
                entity.IsParalyzed=true;Check("companion support obeys paralysis",!magic.TryBuff(shield,actor.EntityBehaviour),actor.Describe());entity.IsParalyzed=false;
                Check("passive companion casts protective spell",magic.TryBuff(shield,actor.EntityBehaviour),actor.Describe());
                actor.State.magicCooldown=0;int mana=entity.CurrentMagicka;
                Check("active protection is not recast or charged twice",!magic.TryBuff(shield,actor.EntityBehaviour) && entity.CurrentMagicka==mana,actor.Status);
                ally.Teleport(actor.transform.position+Vector3.forward*1.5f);Physics.SyncTransforms();
                Check("nearby hired ally receives native protection",magic.TryBuff(shield,ally.EntityBehaviour),ally.Status);
                actor.State.magicCooldown=0;
                Check("unrelated town NPC cannot receive companion buff",!magic.TryBuff(shield,neutral.EntityBehaviour),neutral.Status);
                ally.CompanionCombat.SetStance("passive");ally.Teleport(origin+new Vector3(0,1,9));mod.Companions.Command(ally,"wait");
                actor.CompanionCombat.SetStance("defensive");actor.Teleport(origin+new Vector3(0,1,0));foe.Teleport(origin+new Vector3(12,1,0));AimCombat(foe,gm.PlayerEntityBehaviour);
                yield return new WaitForSecondsRealtime(.5f);actor.State.magicCooldown=0;entity.CurrentMagicka=1000;
                before=foe.EntityBehaviour.Entity.CurrentHealth;int impacts=CompanionSpellMissile.Impacts;
                Check("defensive companion launches native offensive missile",magic.TryCast(fire),actor.Describe());
                float until=Time.time+5;while(Time.time<until && CompanionSpellMissile.Impacts==impacts)yield return null;
                Check("native companion spell reaches selected hostile",CompanionSpellMissile.Impacts>impacts,"impacts="+CompanionSpellMissile.Impacts+" health="+foe.EntityBehaviour.Entity.CurrentHealth+" before="+before);
                for(int attempt=0;attempt<6 && foe.EntityBehaviour.Entity.CurrentHealth==before;attempt++)
                {
                    actor.State.magicCooldown=0;magic.TryCast(fire);yield return new WaitForSecondsRealtime(.8f);
                }
                Check("companion spell deals native health damage",foe.EntityBehaviour.Entity.CurrentHealth<before,"before="+before+" now="+foe.EntityBehaviour.Entity.CurrentHealth);
                actor.State.magicCooldown=0;foe.Teleport(origin+new Vector3(12,1,0));AimCombat(foe,gm.PlayerEntityBehaviour);yield return new WaitForSecondsRealtime(.3f);
                neutral.Teleport(origin+new Vector3(6,1,0));Physics.SyncTransforms();mana=entity.CurrentMagicka;actor.State.magicCooldown=0;
                Check("blocking bystander prevents launch and mana loss",!magic.TryCast(fire) && entity.CurrentMagicka==mana,actor.Describe());
                neutral.Teleport(origin+new Vector3(6,1,8));Physics.SyncTransforms();actor.State.magicCooldown=0;
                bool launched=magic.TryCast(fire);before=neutral.EntityBehaviour.Entity.CurrentHealth;
                Check("clear second shot launches before bystander crosses",launched,actor.Describe()+SensesNote(actor));
                yield return null;neutral.Teleport(origin+new Vector3(4,1,0));Physics.SyncTransforms();
                yield return new WaitForSecondsRealtime(1);
                Check("bystander entering missile flight is protected at impact",launched && neutral.EntityBehaviour.Entity.CurrentHealth==before,neutral.Status);
                // Check the native area spell against consenting party members and an unrelated civilian at impact.
                neutral.Teleport(foe.transform.position+Vector3.forward*1.5f);neutral.enabled=false;
                ally.Teleport(foe.transform.position-Vector3.forward*1.5f);ally.enabled=false;
                NavigationPlayerAt(player,foe.transform.position+Vector3.forward*2.5f,Vector3.left);
                AimCombat(foe,gm.PlayerEntityBehaviour);yield return new WaitForSecondsRealtime(.3f);actor.State.magicCooldown=0;
                var areaFire=fire;areaFire.TargetType=TargetTypes.AreaAtRange;int neutralHealth=neutral.EntityBehaviour.Entity.CurrentHealth,allyHealth=ally.EntityBehaviour.Entity.CurrentHealth;
                gm.PlayerEntity.GodMode=false;gm.PlayerEntity.CurrentHealth=gm.PlayerEntity.MaxHealth;int protectedPlayer=gm.PlayerEntity.CurrentHealth;
                impacts=CompanionSpellMissile.Impacts;launched=magic.TryCast(areaFire);until=Time.time+4;
                while(Time.time<until && CompanionSpellMissile.Impacts==impacts)yield return null;
                Check("native area impact protects player party and civilian",launched && CompanionSpellMissile.Impacts>impacts && gm.PlayerEntity.CurrentHealth==protectedPlayer && ally.EntityBehaviour.Entity.CurrentHealth==allyHealth && neutral.EntityBehaviour.Entity.CurrentHealth==neutralHealth,"launched="+launched+" player="+gm.PlayerEntity.CurrentHealth+" ally="+ally.EntityBehaviour.Entity.CurrentHealth+" civilian="+neutral.EntityBehaviour.Entity.CurrentHealth);
                gm.PlayerEntity.GodMode=true;NavigationPlayerAt(player,origin+new Vector3(0,1,5),Vector3.right);ally.Teleport(origin+new Vector3(0,1,9));
                Check("spell impact permissions exclude party and civilians",!actor.CompanionCombat.AllowsSpellHit(gm.PlayerEntityBehaviour,foe.EntityBehaviour) && !actor.CompanionCombat.AllowsSpellHit(ally.EntityBehaviour,foe.EntityBehaviour) && !actor.CompanionCombat.AllowsSpellHit(neutral.EntityBehaviour,foe.EntityBehaviour),actor.Status);
                neutral.Teleport(origin+new Vector3(6,1,8));actor.State.magicCooldown=0;foe.Teleport(origin+new Vector3(12,1,0));AimCombat(foe,gm.PlayerEntityBehaviour);yield return new WaitForSecondsRealtime(.3f);actor.State.magicCooldown=0;
                launched=magic.TryCast(fire);actor.CompanionCombat.Regroup();impacts=CompanionSpellMissile.Impacts;yield return new WaitForSecondsRealtime(1);
                Check("regroup cancels released offensive spell",launched && CompanionSpellMissile.Impacts==impacts,actor.Describe());
                actor.CompanionCombat.CancelOrders();actor.CompanionMode(null,NpcMode.Calm);foe.SwitchHostile(false);gm.DisableAI=true;
                entity.CurrentMagicka=37;actor.State.magicCooldown=6;actor.CaptureState();
                var save=new NpcSaveDataInterface(mod.States,mod.Placed,mod.Flags,mod.RaiseStateRestored);
                ulong recoveryFrom=Math.Max(actor.State.magicUpdatedAt,actor.State.magicRestAfter);
                double recoveryFraction=actor.State.magicRemainder;
                string encoded=SaveLoadManager.Serialize(typeof(NpcSaveData),save.GetSaveData());save.RestoreSaveData(SaveLoadManager.Deserialize(typeof(NpcSaveData),encoded));
                yield return new WaitForSecondsRealtime(1);
                actor=NpcBrain.Find(key);magic=actor.GetComponent<NpcMagic>();entity=actor.EntityBehaviour.Entity;
                ulong afterLoad=DaggerfallUnity.Instance.WorldTime.Now.ToSeconds();
                int expectedMana=Math.Min(1000,37+(int)Math.Floor(recoveryFraction+(afterLoad>recoveryFrom?afterLoad-recoveryFrom:0)*1000.0/3600));
                Check("real mod save hook restores magicka plus legitimate timed recovery and cooldown",Math.Abs(entity.CurrentMagicka-expectedMana)<=1 && actor.State.magicCooldown>3 && actor.State.magicCooldown<=6,actor.Status+" expected="+expectedMana+" cooldown="+actor.State.magicCooldown);
                Check("restored caster retains one configured spell controller",actor.GetComponents<NpcMagic>().Length==1 && magic.SpellCount==3,actor.Status);
                magic.enabled=false;actor.State.magicCooldown=0;entity.CurrentMagicka=1000;entity.CurrentHealth=entity.MaxHealth;
                // Isolate the autonomous healing priority from the existing fighter and friendly support paths.
                ally=NpcBrain.Find("selftest_spell_ally");foe=NpcBrain.Find("selftest_spell_foe");neutral=NpcBrain.Find("selftest_spell_neutral");
                actor.Teleport(origin+new Vector3(0,1,0));ally.Teleport(origin+new Vector3(0,1,1.8f));ally.EntityBehaviour.Entity.CurrentHealth=1;
                before=ally.EntityBehaviour.Entity.CurrentHealth;gm.DisableAI=false;magic.enabled=true;until=Time.time+5;
                while(Time.time<until && ally.EntityBehaviour.Entity.CurrentHealth==before)yield return null;
                magic.enabled=false;Check("shared companion AI autonomously heals injured party member",ally.EntityBehaviour.Entity.CurrentHealth>before,actor.Status);
                ally.EntityBehaviour.Entity.CurrentHealth=ally.EntityBehaviour.Entity.MaxHealth;ally.Teleport(origin+new Vector3(0,1,9));mod.Companions.Command(ally,"wait");
                gm.DisableAI=true;foe=CombatPerson(location,foeDef,"selftest_spell_autofoe",origin+new Vector3(12,1,0),false);yield return new WaitForSecondsRealtime(.3f);foe.enabled=false;foe.SwitchHostile(true);AimCombat(foe,gm.PlayerEntityBehaviour);
                foe.EntityBehaviour.Entity.MaxHealth=10000;foe.EntityBehaviour.Entity.CurrentHealth=10000;
                actor.State.magicCooldown=0;magic.enabled=true;gm.DisableAI=false;impacts=CompanionSpellMissile.Impacts;until=Time.time+15;
                while(Time.time<until && CompanionSpellMissile.Impacts==impacts)yield return null;
                magic.enabled=false;
                Check("companion AI autonomously buffs then casts at legal foe",CompanionSpellMissile.Impacts>impacts,actor.Describe());
                entity.CurrentMagicka=0;Check("depleted caster falls back from holding spell range",!magic.HoldsRange(12),actor.Status);
                gm.DisableAI=true;actor.CompanionCombat.CancelOrders();actor.CompanionMode(null,NpcMode.Calm);
                var nativeDef=TestDefinition("selftest_native_spellbook",Bravery.Brave,location,player,0);nativeDef.BaseClass="Mage";nativeDef.RecruitCost=0;mod.Catalog.ById.Add(nativeDef.Id,nativeDef);
                var native=CombatPerson(location,nativeDef,"selftest_native_spellbook",origin+new Vector3(-8,1,0),false);
                yield return new WaitForSecondsRealtime(.3f);
                bool nativeCosts=native.GetComponent<EntityEffectManager>().UsePlayerCharacterSkillsForEnemyMagicCost;
                int inherited=native.EntityBehaviour.Entity.SpellbookCount();int nativeMana=native.EntityBehaviour.Entity.CurrentMagicka;
                Check("native class has inherited spells before hire",inherited>0 && native.GetComponent<NpcMagic>()==null,"spells="+inherited);
                Check("hiring class caster installs one safe controller",mod.Companions.Command(native,"recruit") && native.GetComponents<NpcMagic>().Length==1 && native.EntityBehaviour.Entity.SpellbookCount()==0,native.Status);
                yield return null;
                Check("native class casting preserves original mana pool",native.EntityBehaviour.Entity.CurrentMagicka==nativeMana && nativeDef.Magic==null,native.Status);
                native.EntityBehaviour.Entity.CurrentMagicka=17;native.State.magicCooldown=4;native.CaptureState();
                encoded=SaveLoadManager.Serialize(typeof(NpcSaveData),save.GetSaveData());save.RestoreSaveData(SaveLoadManager.Deserialize(typeof(NpcSaveData),encoded));
                yield return new WaitForSecondsRealtime(1);
                native=NpcBrain.Find("selftest_native_spellbook");actor=NpcBrain.Find(key);ally=NpcBrain.Find("selftest_spell_ally");foe=null;neutral=null;
                Check("native class caster restores controlled spellbook and depleted pool",native!=null && native.GetComponents<NpcMagic>().Length==1 && native.EntityBehaviour.Entity.SpellbookCount()==0 && native.EntityBehaviour.Entity.CurrentMagicka>=17 && native.EntityBehaviour.Entity.CurrentMagicka<25,native!=null?native.Status:"missing native caster");
                mod.Companions.Command(native,"dismiss");yield return null;yield return null;
                Check("dismissed class caster regains original native spellbook",native.GetComponent<NpcMagic>()==null && native.EntityBehaviour.Entity.SpellbookCount()==inherited && native.GetComponent<EntityEffectManager>().UsePlayerCharacterSkillsForEnemyMagicCost==nativeCosts,native.Status);
                native.EntityBehaviour.Entity.CurrentMagicka=3;
                bool rehired=mod.Companions.Command(native,"recruit");yield return null;
                Check("rehiring native caster keeps current depleted mana",rehired && native.EntityBehaviour.Entity.CurrentMagicka==3,native.Status);
                NpcBrain.Discard(native);
            }
            finally
            {
                DaggerfallUI.Instance.PopToHUD();NpcBrain.Discard(actor);NpcBrain.Discard(ally);NpcBrain.Discard(foe);NpcBrain.Discard(neutral);
                mod.Catalog.ById.Remove("selftest_native_spellbook");mod.Catalog.ById.Remove("selftest_spell_ally");mod.Catalog.ById.Remove(key);mod.Sprites.Remove(key);UnityEngine.Object.Destroy(floor);
                NpcGreeter.ReactionOverride=reaction;gm.PlayerEntity.GodMode=god;gm.PlayerEntity.CurrentHealth=playerHealth;
                gm.DisableAI=oldAI;gm.PlayerMouseLook.enabled=look;player.position=oldPosition;player.rotation=oldRotation;gm.MainCamera.transform.rotation=oldView;
            }
        }
    }
}

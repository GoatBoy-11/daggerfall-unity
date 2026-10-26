using System;
using System.Collections;
using System.Reflection;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Formulas;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.Utility.ModSupport;

namespace AdvancedNPCs
{
    /// <summary>
    /// Tells an ANPC's sprite when a melee or arrow attack on it did no damage, so a block* sheet can play. DFU raises no
    /// event for a miss, so this wraps FormulaHelper's "CalculateAttackDamage" formula. The damage is still decided by the
    /// override another mod registered (Physical Combat and Armor Overhaul's shield blocks, say), or by DFU's own formula.
    /// </summary>
    static class AttackOutcomeHook
    {
        const string FormulaName = "CalculateAttackDamage";
        static readonly Func<DaggerfallEntity, DaggerfallEntity, bool, int, DaggerfallUnityItem, int> Wrapper = Damage;

        static IDictionary overrides;       // FormulaHelper's private table: formula name -> FormulaOverride (a struct)
        static FieldInfo formulaField;
        static object installed;            // our boxed FormulaOverride
        static Func<DaggerfallEntity, DaggerfallEntity, bool, int, DaggerfallUnityItem, int> previous;
        static bool broken;

        /// <summary>The formula it calls: another mod's override, or null for DFU's own (self-test).</summary>
        public static Delegate Previous { get { return previous; } }

        /// <summary>Its wrapper is the registered formula right now (self-test).</summary>
        public static bool Active { get { return !broken && overrides != null && Current() == (Delegate)Wrapper; } }

        /// <summary>
        /// Called every frame: wraps the registered formula again when another mod registered one since. Our entry has the
        /// lowest load priority, so any mod's RegisterOverride still replaces it.
        /// </summary>
        public static void Keep()
        {
            if (broken)
                return;
            try
            {
                if (overrides == null)
                    Setup();
                Delegate current = Current();
                if (current == (Delegate)Wrapper)
                    return;
                var next = current as Func<DaggerfallEntity, DaggerfallEntity, bool, int, DaggerfallUnityItem, int>;
                if (current != null && next == null)
                    return;     // wrong signature: DFU drops it on its first use, then this wraps DFU's own formula
                previous = next;
                overrides[FormulaName] = installed;
            }
            catch (Exception e)
            {
                broken = true;
                AdvancedNpcsMod.Log("block animations are off: could not wrap DFU's attack damage formula (" + e.Message + ")");
            }
        }

        /// <summary>A mod object for FormulaHelper.RegisterOverride with the given load priority (ours, and self-test fakes).</summary>
        public static Mod Provider(string title, int loadPriority)
        {
            Mod provider = new Mod();
            provider.ModInfo.ModTitle = title;
            typeof(Mod).GetProperty("LoadPriority").GetSetMethod(true).Invoke(provider, new object[] { loadPriority });
            return provider;
        }

        /// <summary>Removes the registered formula, ours or a self-test fake; the next Keep wraps DFU's own (self-test).</summary>
        public static void ClearForTest()
        {
            if (overrides != null)
                overrides.Remove(FormulaName);
        }

        static void Setup()
        {
            overrides = (IDictionary)typeof(FormulaHelper).GetField("overrides", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            Type entry = typeof(FormulaHelper).GetNestedType("FormulaOverride", BindingFlags.NonPublic);
            formulaField = entry.GetField("Formula", BindingFlags.NonPublic | BindingFlags.Instance);
            installed = Activator.CreateInstance(entry, BindingFlags.NonPublic | BindingFlags.Instance, null,
                new object[] { Wrapper, Provider("Advanced NPCs (block animations)", int.MinValue) }, null);
        }

        static Delegate Current()
        {
            return overrides.Contains(FormulaName) ? (Delegate)formulaField.GetValue(overrides[FormulaName]) : null;
        }

        static int Damage(DaggerfallEntity attacker, DaggerfallEntity target, bool backFacing, int animTime, DaggerfallUnityItem weapon)
        {
            int damage;
            if (previous != null)
            {
                damage = previous(attacker, target, backFacing, animTime, weapon);
            }
            else
            {
                // DFU's own formula only runs with no override registered.
                overrides.Remove(FormulaName);
                try { damage = FormulaHelper.CalculateAttackDamage(attacker, target, backFacing, animTime, weapon); }
                finally { overrides[FormulaName] = installed; }
            }
            if (damage <= 0 && target != null && target.EntityBehaviour != null)
            {
                NpcSprite sprite = target.EntityBehaviour.GetComponent<NpcSprite>();
                if (sprite != null)
                    sprite.Blocked();
            }
            return damage;
        }
    }
}

using System;
using System.Collections.Generic;
using DaggerfallConnect.Save;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.MagicAndEffects;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    public static class NpcSpellLibrary
    {
        static readonly HashSet<string> warned = new HashSet<string>();
        static readonly HashSet<string> Offensive = new HashSet<string> {
            "Damage-Health", "Damage-Fatigue", "Damage-SpellPoints", "ContinuousDamage-Health",
            "ContinuousDamage-Fatigue", "ContinuousDamage-SpellPoints", "Paralyze", "Silence",
            "Drain-Strength", "Drain-Intelligence", "Drain-Willpower", "Drain-Agility", "Drain-Endurance",
            "Drain-Personality", "Drain-Speed", "Drain-Luck"
        };
        static readonly HashSet<string> Defensive = new HashSet<string> {
            "Heal-Health", "Regenerate", "Shield", "SpellResistance", "SpellReflection", "SpellAbsorption",
            "ElementalResistance-Fire", "ElementalResistance-Frost", "ElementalResistance-Poison",
            "ElementalResistance-Shock", "ElementalResistance-Magicka",
            "Invisibility-Normal", "Invisibility-True", "Shadow-Normal", "Shadow-True"
        };

        public static string ListSupported(params string[] args)
        {
            string filter = args != null ? string.Join(" ", args) : "";
            List<string> names = new List<string>();
            EntityEffectBroker broker = GameManager.Instance.EntityEffectBroker;
            foreach (SpellRecord.SpellRecordData record in broker.StandardSpells)
            {
                EffectBundleSettings spell;
                if (record.spellName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 &&
                    broker.ClassicSpellRecordDataToEffectBundleSettings(record, BundleTypes.Spell, out spell) && Supported(spell))
                    names.Add(record.spellName.Trim());
            }
            names.Sort(StringComparer.OrdinalIgnoreCase);
            string result = "ANPC combat spells (use these names in magic.spells):\n" + string.Join(", ", names.ToArray());
            AdvancedNpcsMod.Log(result);
            return result;
        }

        public static List<EffectBundleSettings> Resolve(NpcDefinition definition)
        {
            List<EffectBundleSettings> result = new List<EffectBundleSettings>();
            EntityEffectBroker broker = GameManager.Instance.EntityEffectBroker;
            foreach (string name in definition.Magic.Spells)
            {
                bool found = false;
                foreach (SpellRecord.SpellRecordData record in broker.StandardSpells)
                {
                    if (!string.Equals(name, record.spellName.Trim(), StringComparison.OrdinalIgnoreCase)) continue;
                    found = true;
                    EffectBundleSettings settings;
                    if (!broker.ClassicSpellRecordDataToEffectBundleSettings(record, BundleTypes.Spell, out settings) || !Supported(settings))
                        Warn(definition, name, "unsupported NPC combat spell (player utility effects and unsupported targets are skipped)");
                    else
                        result.Add(settings);
                    break;
                }
                if (!found) Warn(definition, name, "unknown standard spell name");
            }
            return result;
        }

        public static bool Supported(EffectBundleSettings spell)
        {
            bool self = spell.TargetType == TargetTypes.CasterOnly;
            if (!self && spell.TargetType != TargetTypes.ByTouch && spell.TargetType != TargetTypes.SingleTargetAtRange &&
                spell.TargetType != TargetTypes.AreaAtRange && spell.TargetType != TargetTypes.AreaAroundCaster) return false;
            if (spell.Effects == null || spell.Effects.Length == 0) return false;
            foreach (EffectEntry effect in spell.Effects)
            {
                if (!(self ? Defensive : Offensive).Contains(effect.Key) ||
                    GameManager.Instance.EntityEffectBroker.GetEffectTemplate(effect.Key) == null) return false;
            }
            return true;
        }

        static void Warn(NpcDefinition d, string name, string reason)
        {
            string text = d.SourceFile + ": magic.spells: " + name + ": " + reason;
            if (warned.Add(text)) AdvancedNpcsMod.Log(text);
        }
    }
}

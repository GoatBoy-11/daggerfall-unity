using System;
using System.Collections.Generic;

namespace AdvancedNPCs.Core
{
    /// <summary>Portable identity and last location of a hired person, owned by their saved NpcState.</summary>
    [Serializable]
    public class CompanionRecord
    {
        public string template, name, race, gender, portrait;
        public uint seed;
        // Opaque native item records, equipment links and permanent stats; absent in pre-trading saves.
        public string belongings;
        public int faceOutfit, faceVariant, maxHealth;
        public int baseMaxHealth; // Missing in older saves: their saved maxHealth is the unboosted value.
        public bool barksMuted; // Optional saved preference; older contracts default to audible.
        public bool noCompanionSlot; // Fallback when a saved contract has no installed definition.
        // All three states reserve the original spawn key, preventing duplicate people.
        public string status = "following";
        public string stance = "defensive";
        public string area;
        public int mapX, mapY;
        public float x, y, z;
        public int HealthForContract(int fallback)
        {
            if(baseMaxHealth<=0) baseMaxHealth=Math.Max(1,maxHealth>0?maxHealth:fallback);
            return maxHealth=CompanionCombatRules.Contract(status)?CombatRules.ScaleHealth(baseMaxHealth,1.5f):baseMaxHealth;
        }
        public CompanionRecord Clone() { var copy=(CompanionRecord)MemberwiseClone(); copy.stance=CompanionOrders.Stance(stance); return copy; }
        public static CompanionRecord From(NpcInstance person)
        {
            return new CompanionRecord { template = person.Definition.Id, name = person.Name, race = person.Race,
                gender = person.Gender, portrait = person.PortraitName, seed = person.Seed,
                faceOutfit = person.FaceOutfit, faceVariant = person.FaceVariant, noCompanionSlot=person.Definition.NoCompanionSlot };
        }
        public NpcInstance Person(string key, NpcDefinition definition)
        {
            return new NpcInstance { Key = key, Definition = definition, Name = name, Race = race, Gender = gender,
                PortraitName = portrait, Seed = seed, FaceOutfit = faceOutfit, FaceVariant = faceVariant,
                Persistent = true, HasFixedPosition = true, X = x, Y = y, Z = z };
        }
        public static bool Allows(string status, string command)
        {
            if (CompanionOrders.Tactical(command)) return CompanionCombatRules.Contract(status);
            if (command == "recruit") return status == null || status == "dismissed";
            if (command == "follow") return status == "waiting";
            if (command == "wait") return status == "following";
            if (command == "dismiss" || command == "push") return status == "following" || status == "waiting";
            return false;
        }
    }
    public interface ICompanionFacts { bool CanCommandCompanion(string command); }
    public interface ICompanionActions { bool CommandCompanion(string command); }
    /// <summary>Opt-in mercenary commands. Charging and state promotion are one runtime transaction.</summary>
    public static class CompanionDialogue
    {
        public static void Append(ComposedDialogue dialogue, int cost)
        {
            if (cost < 0) return;
            Add(dialogue, "orders", "Companion orders", "What are your orders?");
            Add(dialogue, "dismiss", "You're dismissed", "Our contract is finished. Until next time.");
            Add(dialogue, "wait", "Wait here", "I'll stay here until you return.");
            Add(dialogue, "follow", "Follow me", "Ready when you are.");
            Add(dialogue, "recruit", "Hire you (" + cost + " gold)", "My blade is yours. Lead the way.");
            var offer = new DialogueTopic { Id = "anpc_companion_price", Caption = "Your hiring fee", When = new Condition() };
            offer.When.Add("cannot afford recruitment", delegate(IDialogueFacts f) {
                var companion = f as ICompanionFacts;
                return companion != null && companion.CanCommandCompanion("offer") && !companion.CanCommandCompanion("recruit");
            });
            offer.Answers.Add(new DialogueAnswer { Text = "My fee is " + cost + " gold. Come back when you have it, and we can make a contract." });
            dialogue.Topics.Insert(0, offer);
        }
        static void Add(ComposedDialogue dialogue, string command, string caption, string answer)
        {
            DialogueTopic topic = new DialogueTopic { Id = "anpc_companion_" + command, Caption = caption, When = new Condition() };
            topic.Question.All = caption + ".";
            topic.When.Add("companion " + command, delegate(IDialogueFacts f) {
                ICompanionFacts companion = f as ICompanionFacts;
                return companion != null && companion.CanCommandCompanion(command);
            });
            DialogueAnswer line = new DialogueAnswer { Text = answer };
            line.Actions.Companion = command;
            line.Actions.EndConversation = true;
            topic.Answers.Add(line);
            dialogue.Topics.Insert(0, topic);
        }
    }
}

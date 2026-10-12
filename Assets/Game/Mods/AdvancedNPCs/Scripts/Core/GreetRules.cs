using System.Collections.Generic;

namespace AdvancedNPCs.Core
{
    /// <summary>Who an ANPC greets, and how often (spec run/greet/turret §5). No Unity or DFU types.</summary>
    public static class GreetRules
    {
        public const float Distance = 4f;
        public const double Chance = 0.25;
        public const double CooldownHours = 2.0;
        /// <summary>The player's key in GreetMemory (ANPCs use their own keys).</summary>
        public const string PlayerKey = "player";

        /// <summary>The player: this ANPC is calm toward them and DFU's reaction to them is likes or loves.</summary>
        public static bool LikesPlayer(bool selfCalm, string reactionBand)
        {
            return selfCalm && (reactionBand == "likes" || reactionBand == "loves");
        }

        /// <summary>Another ANPC: both calm, neither an enemy of the player.</summary>
        public static bool SameSide(bool selfCalm, bool selfEnemy, bool otherCalm, bool otherEnemy)
        {
            return selfCalm && otherCalm && !selfEnemy && !otherEnemy;
        }
    }

    /// <summary>
    /// One ANPC's greeting memory: who is near now (a target that stays near is one encounter, rolled once) and when
    /// it last greeted each target (cooldown). Kept in memory only.
    /// </summary>
    public class GreetMemory
    {
        readonly HashSet<string> near = new HashSet<string>();
        readonly Dictionary<string, double> lastGreet = new Dictionary<string, double>();

        /// <summary>Greetings given (self-test).</summary>
        public int Count { get; private set; }

        /// <summary>Nobody is near any more (the greeter stopped checking): the next sighting is a new encounter.</summary>
        public void LeaveAll()
        {
            near.Clear();
        }

        /// <summary>True when the ANPC should greet this target now. Call for every candidate at each check.</summary>
        public bool Consider(string target, bool inRangeAndSeen, bool likes, double nowHours, double roll, double chance)
        {
            if (!inRangeAndSeen)
            {
                near.Remove(target);
                return false;
            }
            if (!near.Add(target))
                return false;               // the same encounter: rolled already
            double last;
            if (!likes || (lastGreet.TryGetValue(target, out last) && nowHours - last < GreetRules.CooldownHours))
                return false;
            if (chance < 1.0 && roll >= chance)     // a roll can be 1 (Unity's Random.value): a certain chance stays certain
                return false;
            lastGreet[target] = nowHours;
            Count++;
            return true;
        }
    }
}

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

        /// <summary>
        /// An ANPC gets a greeter when it has greet files (sheet or sound), or when it converses (spec conversations §1);
        /// the second kind greets silently, only other ANPCs.
        /// </summary>
        public static bool NeedsGreeter(bool greetFiles, bool converses)
        {
            return greetFiles || converses;
        }

        /// <summary>
        /// Who an ANPC can greet and talk with now: Peaceful (calm, not angry at the player), IdleEnemy (an enemy of the
        /// player that has no one to fight yet, like bandits waiting in a dungeon) or Busy (fighting, fleeing, angry).
        /// Ints, not an enum (DFU's runtime compiler, see VoiceModes).
        /// </summary>
        public const int Busy = 0, Peaceful = 1, IdleEnemy = 2;

        /// <summary>
        /// calmMode: in its calm mode; hostile: angry at the player; enemy: an enemy of the player (attitude or
        /// schedule); enemyIdle: DFU's AI has no target for it (none seen yet, or it gave up searching).
        /// </summary>
        public static int Side(bool calmMode, bool hostile, bool enemy, bool enemyIdle)
        {
            if (enemy)
                return enemyIdle ? IdleEnemy : Busy;
            return calmMode && !hostile ? Peaceful : Busy;
        }

        /// <summary>Another ANPC: both peaceful, or both enemies of the player with no one to fight yet.</summary>
        public static bool SameSide(int self, int other)
        {
            return self != Busy && self == other;
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

        /// <summary>
        /// The target is near and already met (a conversation just ended): no greeting until it leaves and comes back.
        /// </summary>
        public void Met(string target)
        {
            near.Add(target);
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

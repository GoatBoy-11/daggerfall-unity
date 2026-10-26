namespace AdvancedNPCs.Core
{
    /// <summary>Defensive contract rules. Targets and all timers are transient, never saved.</summary>
    public static class CompanionCombatRules
    {
        public const float FollowLeash = 30, WaitLeash = 8, RetrySeconds = 8;
        public static bool Contract(string status) { return status == "following" || status == "waiting"; }
        public static bool Legal(bool alive, bool player, bool ally, bool guard, bool questProtected, bool hostile)
        { return alive && !player && !ally && !guard && !questProtected && hostile; }
        // Only DFU's city watch knight is a guard in dungeons: other mods' CityWatch-team enemies
        // (Daggerfall Enemy Expansion's prison guards) are ordinary foes there.
        public static bool Guard(bool cityWatchTeam, bool cityWatchKnight, bool inDungeon)
        { return cityWatchKnight || (cityWatchTeam && !inDungeon); }
        // DFU marks every quest foe AI-protected by default, including attacking night ghosts.
        // Permit party defence without changing that shared flag or initiating quest fights.
        public static bool QuestProtected(bool attackableByAI,bool hostile,bool foe,bool restricted,bool attackingParty)
        { return restricted || (!attackableByAI && !(hostile && foe && attackingParty)); }
        public static int Priority(bool waiting, bool attackingSelf, bool attackingPlayer, bool attackingParty)
        { return attackingSelf ? 0 : waiting ? -1 : attackingPlayer ? 1 : attackingParty ? 2 : -1; }
        public static bool WithinLeash(bool waiting, float selfDistance, float targetDistance)
        { float limit = waiting ? WaitLeash : FollowLeash; return selfDistance <= limit && targetDistance <= limit; }
        public static bool Retreat(float healthFraction, bool alreadyRetreating)
        { return healthFraction < (alreadyRetreating ? 0.4f : 0.25f); }
    }
    public class CompanionPursuit
    {
        double window, startingGap, previousGap, unseen, stalled;
        bool observed;
        public void Reset() { window = startingGap = previousGap = unseen = stalled = 0; observed = false; }
        public bool Tick(double gap, bool visible, double dt)
        {
            if (dt <= 0 || double.IsNaN(dt) || double.IsInfinity(dt)) return false;
            unseen = visible ? 0 : unseen + dt;
            if (visible && gap <= 1.75) { window = stalled = 0; return false; }
            if (window == 0) startingGap = observed ? previousGap : gap;
            previousGap = gap; observed = true;
            window += dt;
            if (window >= 1) { stalled = gap < startingGap - 0.25 ? 0 : stalled + window; window = 0; }
            return unseen >= 6 || stalled >= 10;
        }
    }
    public class CompanionHits
    {
        double remaining;
        public int Count { get; private set; }
        public void Reset() { remaining=0; Count=0; }
        public void Tick(double dt) { remaining -= dt; if (remaining <= 0) Count = 0; }
        public bool Hit() { if (remaining <= 0) Count = 0; remaining = 10; return ++Count >= 3; }
    }
}

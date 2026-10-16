namespace AdvancedNPCs.Core
{
    public static class CompanionOrders
    {
        public static string Stance(string value)
        { return value=="aggressive" || value=="passive" ? value : "defensive"; }
        public static bool Tactical(string command)
        { return command=="orders" || command=="regroup" || command=="defensive" || command=="aggressive" || command=="passive"; }
        public static int Priority(string stance, bool waiting, bool self, bool player, bool party)
        {
            int rank=CompanionCombatRules.Priority(waiting,self,player,party);
            return rank<0 && !waiting && Stance(stance)=="aggressive" ? 3 : rank;
        }
        public static string Condition(float fraction)
        { return fraction<.25f ? "Critical" : fraction<.6f ? "Wounded" : fraction<1 ? "Hurt" : "Healthy"; }
    }
    /// <summary>Continuous pressure only, with a cooldown shared by manual and automatic pushes.</summary>
    public class CompanionPushContact
    {
        double elapsed,cooldown;
        public void Reset() {elapsed=cooldown=0;}
        public void Interrupt() {elapsed=0;}
        public void Pushed() {elapsed=0;cooldown=6;}
        public bool Tick(double dt,bool pressing,bool eligible)
        {
            if(dt<=0 || double.IsNaN(dt) || double.IsInfinity(dt)) return false;
            if(cooldown>0) {cooldown=System.Math.Max(0,cooldown-dt);elapsed=0;return false;}
            if(!eligible || !pressing || cooldown>0) {elapsed=0;return false;}
            elapsed+=dt;
            if(elapsed<.75) return false;
            Pushed();return true;
        }
    }
    /// <summary>A short walk followed by time to pass. Never saved; uses active game time.</summary>
    public class CompanionPush
    {
        double elapsed, distance, pause;
        public bool Active { get; private set; }
        public bool Moving { get; private set; }
        public void Begin() { elapsed=distance=pause=0; Active=Moving=true; }
        public void Reset() { Active=Moving=false; }
        public void StopMoving() { Moving=false; }
        public void Tick(double dt,double travelled)
        {
            if(!Active || dt<=0 || double.IsNaN(dt) || double.IsInfinity(dt)) return;
            if(Moving)
            {
                elapsed+=dt;
                if(travelled>0 && !double.IsNaN(travelled) && !double.IsInfinity(travelled)) distance+=travelled;
                if(distance>=2.5 || elapsed>=4) Moving=false;
            }
            else { pause+=dt; if(pause>=3) Reset(); }
        }
    }
    /// <summary>Active game time only. A blocked return expires rather than suppressing combat forever.</summary>
    public class CompanionRegroup
    {
        double elapsed;
        public bool Active { get; private set; }
        public void Begin() { elapsed=0;Active=true; }
        public void Reset() { elapsed=0;Active=false; }
        public void Tick(double dt,bool arrived)
        {
            if(!Active || dt<=0 || double.IsNaN(dt) || double.IsInfinity(dt)) return;
            elapsed+=dt;
            if((arrived && elapsed>=8) || elapsed>=20) Reset();
        }
    }
}

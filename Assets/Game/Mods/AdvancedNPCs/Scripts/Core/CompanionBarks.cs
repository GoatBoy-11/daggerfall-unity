using System;
namespace AdvancedNPCs.Core
{
    /// <summary>Companion-only speech selection and active-time scheduling, independent of playback.</summary>
    public static class CompanionBarkRules
    {
        public const string Bark="cm_bark", Dungeon="cm_dungeon", Inn="cm_inn", Hurt="cm_hurt", NoMagicka="cm_no_magicka";
        public const string Recruit="cm_recruit", Wait="cm_wait", Follow="cm_follow", Dismiss="cm_dismiss", Regroup="cm_regroup", Push="cm_push", Combat="cm_combat", Victory="cm_victory";
        public static string Command(string command)
        {
            switch(command)
            {
                case "recruit":return Recruit;case "wait":return Wait;case "follow":return Follow;
                case "push":return Push;case "dismiss":return Dismiss;case "regroup":return Regroup;default:return null;
            }
        }
        public static bool PushVoice(bool automatic,double roll)
        { return SoundRules.Rolls(automatic?15:50,roll); }
        public static bool IsBark(string pool)
        { return pool==Bark || pool==Dungeon || pool==Inn || pool==Hurt || pool==NoMagicka || pool==Recruit || pool==Wait || pool==Follow || pool==Dismiss || pool==Regroup || pool==Push || pool==Combat || pool==Victory; }
        public static string Pool(bool hurt,string context,double roll)
        {
            if(hurt)return Hurt;
            if((context==Dungeon || context==Inn) && roll<2.0/3.0)return context;
            return Bark;
        }
        public static double Interval(double min,double max,double roll)
        { return min+(max-min)*Math.Max(0,Math.Min(1,roll)); }
    }
    /// <summary>One battle cry per episode; victory requires a dead foe, never an escape or regroup.</summary>
    public class CompanionBattleSpeech
    {
        bool engaged;
        public void Reset() {engaged=false;}
        public string Observe(bool fighting,bool calm,bool foeDead,bool regrouping)
        {
            if(regrouping) {Reset();return null;}
            if(fighting && !engaged) {engaged=true;return CompanionBarkRules.Combat;}
            if(calm && engaged) {engaged=false;return foeDead?CompanionBarkRules.Victory:null;}
            return null;
        }
    }
    public class CompanionBarkClock
    {
        double remaining=-1;
        int lastMagicka=-1;
        public void Reset(int magicka) {remaining=-1;lastMagicka=magicka;}
        /// <summary>Observe even while muted, so unmuting cannot replay an old depletion.</summary>
        public bool ObserveMagicka(int magicka)
        {bool depleted=lastMagicka>0 && magicka==0;lastMagicka=magicka;return depleted;}
        public void Schedule(double min,double max,double roll)
        {remaining=CompanionBarkRules.Interval(min,max,roll);}
        public bool NeedsSchedule { get {return remaining<0;} }
        public bool Advance(double dt)
        {
            if(remaining<0 || dt<=0 || double.IsNaN(dt) || double.IsInfinity(dt))return false;
            remaining-=dt;return remaining<=0;
        }
        public bool Tick(double dt,double min,double max,double roll)
        {
            if(dt<=0 || double.IsNaN(dt) || double.IsInfinity(dt))return false;
            if(NeedsSchedule) {Schedule(min,max,roll);return false;}
            if(!Advance(dt))return false;
            Schedule(min,max,roll);return true;
        }
    }
}

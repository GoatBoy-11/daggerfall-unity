using System;
using System.Collections.Generic;

namespace AdvancedNPCs.Core
{
    /// <summary>Named visual routines, independent of combat animation states.</summary>
    public static class PackageAnimations
    {
        public static readonly string[] CivilianFamilies = { "idle", "pray", "play", "emote", "dance" };
        /// <summary>Chore package defaults: any of these families a set has.</summary>
        public static readonly string[] ChoreFamilies = { "sweep", "chop", "scrub", "work" };
        /// <summary>Hawk package: sheets tried after its own animation when the set has none of it.</summary>
        public static readonly string[] HawkFallbacks = { "greet", "talk" };

        /// <summary>Chore stop: the sheets matching any requested family or sheet; idle when the set has none of them.</summary>
        public static List<SpriteAnimation> ChoreVariants(IEnumerable<SpriteAnimation> available, ICollection<string> requested)
        {
            var chores = new List<SpriteAnimation>();
            var idle = new List<SpriteAnimation>();
            foreach (var animation in available)
            {
                if (Referenced(requested, animation.Name)) chores.Add(animation);
                else if (Matches("idle", animation.Name)) idle.Add(animation);
            }
            return chores.Count > 0 ? chores : idle;
        }

        public static List<SpriteAnimation> CivilianVariants(IEnumerable<SpriteAnimation> available, double roll)
        {
            var idle = new List<SpriteAnimation>();
            var activities = new List<SpriteAnimation>();
            foreach (var animation in available)
            {
                if (Matches("idle", animation.Name)) idle.Add(animation);
                else for (int i = 1; i < CivilianFamilies.Length; i++)
                    if (Matches(CivilianFamilies[i], animation.Name)) { activities.Add(animation); break; }
            }
            return roll < 0.5 || activities.Count == 0 ? idle : activities;
        }

        public static bool ValidName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length > 64 || !Letter(name[0])) return false;
            for (int i = 1; i < name.Length; i++)
                if (!Letter(name[i]) && !Digit(name[i]) && name[i] != '_') return false;
            return true;
        }
        static bool Letter(char c) { return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'); }
        static bool Digit(char c) { return c >= '0' && c <= '9'; }

        /// <summary>Numbered requests are exact; a family includes its bare name and numbered variants only.</summary>
        public static bool Matches(string requested, string sheet)
        {
            if (!ValidName(requested) || string.IsNullOrEmpty(sheet)) return false;
            if (string.Equals(requested, sheet, StringComparison.OrdinalIgnoreCase)) return true;
            int underscore = requested.LastIndexOf('_');
            bool exact = underscore >= 0 && underscore < requested.Length - 1;
            if (exact)
                for (int i = underscore + 1; i < requested.Length; i++) exact &= Digit(requested[i]);
            if (exact || !sheet.StartsWith(requested + "_", StringComparison.OrdinalIgnoreCase)) return false;
            int suffix = requested.Length + 1;
            if (suffix >= sheet.Length) return false;
            for (int i = suffix; i < sheet.Length; i++) if (!Digit(sheet[i])) return false;
            return true;
        }

        public static bool Referenced(ICollection<string> requested, string sheet)
        {
            if (requested == null) return false;
            foreach (string name in requested) if (Matches(name, sheet)) return true;
            return false;
        }

        public static List<SpriteAnimation> Variants(SpriteSet set, string requested)
        {
            List<SpriteAnimation> found = new List<SpriteAnimation>();
            if (set == null) return found;
            foreach (SpriteAnimation animation in set.Animations.Values)
                if (Matches(requested, animation.Name)) found.Add(animation);
            found.Sort(delegate(SpriteAnimation a, SpriteAnimation b) { return string.CompareOrdinal(a.Name, b.Name); });
            return found;
        }

        public static int Pick(int count, int previous, double roll)
        {
            if (count <= 1) return 0;
            bool exclude = previous >= 0 && previous < count;
            int choices = exclude ? count - 1 : count;
            int index = Math.Min(choices - 1, Math.Max(0, (int)(roll * choices)));
            return exclude && index >= previous ? index + 1 : index;
        }
    }

    /// <summary>
    /// Eat package: a bout of mealCycles eat / drink cycles (eat or drink picked at random each cycle when the set has
    /// both), then a break of breakCycles idle cycles, repeated. Without eat and drink sheets only idle plays; without
    /// idle sheets the bouts follow each other. Started counts cycles begun, so the caller can sound each one once.
    /// </summary>
    public class MealAnimationLoop
    {
        public const string EatFamily = "eat", DrinkFamily = "drink", IdleFamily = "idle";
        readonly List<SpriteAnimation> eat = new List<SpriteAnimation>(), drink = new List<SpriteAnimation>(), idle = new List<SpriteAnimation>();
        readonly double fps;
        readonly int mealMin, mealMax, breakMin, breakMax;
        readonly Func<double> random;
        List<SpriteAnimation> pool;
        int index = -1, left;
        bool meal;
        public SpriteAnimation Current { get { return pool[index]; } }
        public string Family { get; private set; }
        public double Elapsed { get; private set; }
        public int Started { get; private set; }

        public static bool CanPlay(IEnumerable<SpriteAnimation> available)
        {
            foreach (SpriteAnimation a in available)
                if (a.Frames > 0 && (PackageAnimations.Matches(EatFamily, a.Name) || PackageAnimations.Matches(DrinkFamily, a.Name) ||
                    PackageAnimations.Matches(IdleFamily, a.Name))) return true;
            return false;
        }

        public MealAnimationLoop(IEnumerable<SpriteAnimation> available, double fps, int mealMin, int mealMax, int breakMin, int breakMax, Func<double> random)
        {
            foreach (SpriteAnimation a in available)
            {
                if (a.Frames < 1) continue;
                if (PackageAnimations.Matches(EatFamily, a.Name)) eat.Add(a);
                else if (PackageAnimations.Matches(DrinkFamily, a.Name)) drink.Add(a);
                else if (PackageAnimations.Matches(IdleFamily, a.Name)) idle.Add(a);
            }
            if (eat.Count + drink.Count + idle.Count == 0 || !(fps > 0) || double.IsInfinity(fps) || mealMin < 1 || mealMax < mealMin ||
                breakMin < 0 || breakMax < breakMin)
                throw new ArgumentException("Eat package needs eat, drink or idle sheets, a positive fps and ordered cycle counts");
            this.fps = fps;
            this.mealMin = mealMin; this.mealMax = mealMax; this.breakMin = breakMin; this.breakMax = breakMax;
            this.random = random;
            NextPhase(true);
            StartCycle();
        }

        bool HasMeal { get { return eat.Count + drink.Count > 0; } }

        void NextPhase(bool wantMeal)
        {
            meal = (wantMeal && HasMeal) || idle.Count == 0;
            // A zero-cycle break goes straight into the next bout; idle-only sets always play at least one cycle.
            left = meal ? Roll(mealMin, mealMax) : Math.Max(HasMeal ? 0 : 1, Roll(breakMin, breakMax));
            if (left == 0) NextPhase(true);
        }

        int Roll(int min, int max)
        {
            return Math.Min(max, min + (int)(Math.Max(0, random()) * (max - min + 1)));
        }

        void StartCycle()
        {
            List<SpriteAnimation> next = !meal ? idle : eat.Count == 0 ? drink : drink.Count == 0 ? eat : random() < 0.5 ? eat : drink;
            index = PackageAnimations.Pick(next.Count, next == pool ? index : -1, random());
            pool = next;
            Family = pool == eat ? EatFamily : pool == drink ? DrinkFamily : IdleFamily;
            Elapsed = 0;
            Started++;
        }

        public void Advance(double seconds)
        {
            if (seconds < 0 || double.IsNaN(seconds) || double.IsInfinity(seconds)) return;
            Elapsed += seconds;
            while (Elapsed >= Current.Frames / fps)
            {
                double rest = Elapsed - Current.Frames / fps;
                if (--left <= 0) NextPhase(!meal);
                StartCycle();
                Elapsed = rest;
            }
        }
    }

    /// <summary>Hold frame zero, then play one randomly chosen idle cycle between active-time intervals.</summary>
    public class GuardAnimationLoop
    {
        readonly IList<SpriteAnimation> variants;
        readonly Func<double> random;
        readonly double fps, min, max;
        readonly int frozen;
        int index;
        double remaining;
        public bool Playing { get; private set; }
        public double Elapsed { get; private set; }
        public int Cycles { get; private set; }
        public SpriteAnimation Current { get { return variants[index]; } }

        public GuardAnimationLoop(IList<SpriteAnimation> variants, double fps, double min, double max, Func<double> random)
        {
            if (variants == null || variants.Count == 0 || !(fps > 0) || double.IsInfinity(fps) ||
                !(min > 0) || max < min || double.IsNaN(max) || double.IsInfinity(max))
                throw new ArgumentException("Guard animation needs sheets, positive fps and an ordered finite interval");
            foreach (SpriteAnimation a in variants)
                if (a.Frames < 1) throw new ArgumentException("Idle sheets need at least one frame");
            this.variants = variants;
            this.fps = fps;
            this.min = min;
            this.max = max;
            this.random = random;
            frozen = FrozenIndex(variants);
            Rest();
        }

        public static int FrozenIndex(IList<SpriteAnimation> variants)
        {
            for (int i = 0; i < variants.Count; i++)
                if (string.Equals(variants[i].Name, "idle_1", StringComparison.OrdinalIgnoreCase)) return i;
            for (int i = 0; i < variants.Count; i++)
                if (string.Equals(variants[i].Name, "idle", StringComparison.OrdinalIgnoreCase)) return i;
            int best = 0;
            for (int i = 1; i < variants.Count; i++)
            {
                string a = variants[i].Name.Substring(5).TrimStart('0');
                string b = variants[best].Name.Substring(5).TrimStart('0');
                if (a.Length < b.Length || (a.Length == b.Length && string.CompareOrdinal(a, b) < 0)) best = i;
            }
            return best;
        }

        void Rest()
        {
            Playing = false;
            index = frozen;
            Elapsed = 0;
            remaining = min + (max - min) * Math.Max(0, Math.Min(1, random()));
        }

        public void Advance(double seconds)
        {
            if (seconds < 0 || double.IsNaN(seconds) || double.IsInfinity(seconds)) return;
            while (seconds >= remaining)
            {
                seconds -= remaining;
                if (Playing) { Cycles++; Rest(); }
                else
                {
                    Playing = true;
                    index = PackageAnimations.Pick(variants.Count, -1, random());
                    Elapsed = 0;
                    remaining = Current.Frames / fps;
                }
            }
            remaining -= seconds;
            if (Playing) Elapsed += seconds;
        }
    }

    /// <summary>Independent per-NPC cycle timing. Paused routines do not advance; a new instance restarts at frame zero.</summary>
    public class PackageAnimationLoop
    {
        readonly IList<SpriteAnimation> variants;
        readonly double fps;
        readonly Func<double> random;
        public int Index { get; private set; }
        public double Elapsed { get; private set; }
        public int Cycles { get; private set; }
        public SpriteAnimation Current { get { return variants[Index]; } }
        readonly int cycleLimit;
        public bool Finished { get { return Cycles >= cycleLimit; } }
        public PackageAnimationLoop(IList<SpriteAnimation> variants, double fps, Func<double> random, int cycleLimit = int.MaxValue)
        {
            if (variants == null || variants.Count == 0 || !(fps > 0) || double.IsInfinity(fps) || cycleLimit < 1)
                throw new ArgumentException("Animation loop needs usable sheets and a finite positive fps");
            foreach (SpriteAnimation a in variants)
                if (a.Frames < 1) throw new ArgumentException("Animation sheets need at least one frame");
            this.variants = variants;
            this.fps = fps;
            this.random = random;
            this.cycleLimit = cycleLimit;
            Index = PackageAnimations.Pick(variants.Count, -1, random());
        }
        public void Advance(double seconds)
        {
            if (seconds < 0 || double.IsNaN(seconds) || double.IsInfinity(seconds)) return;
            if (Finished) return;
            Elapsed += seconds;
            while (Elapsed >= Current.Frames / fps)
            {
                Elapsed -= Current.Frames / fps;
                Cycles++;
                if (Finished) { Elapsed = 0; return; }
                Index = PackageAnimations.Pick(variants.Count, Index, random());
            }
        }
    }
}

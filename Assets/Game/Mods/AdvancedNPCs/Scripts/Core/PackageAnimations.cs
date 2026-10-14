using System;
using System.Collections.Generic;

namespace AdvancedNPCs.Core
{
    /// <summary>Named visual routines, independent of combat animation states.</summary>
    public static class PackageAnimations
    {
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
        public PackageAnimationLoop(IList<SpriteAnimation> variants, double fps, Func<double> random)
        {
            if (variants == null || variants.Count == 0 || !(fps > 0) || double.IsInfinity(fps))
                throw new ArgumentException("Animation loop needs usable sheets and a finite positive fps");
            foreach (SpriteAnimation a in variants)
                if (a.Frames < 1) throw new ArgumentException("Animation sheets need at least one frame");
            this.variants = variants;
            this.fps = fps;
            this.random = random;
            Index = PackageAnimations.Pick(variants.Count, -1, random());
        }
        public void Advance(double seconds)
        {
            if (seconds < 0 || double.IsNaN(seconds) || double.IsInfinity(seconds)) return;
            Elapsed += seconds;
            while (Elapsed >= Current.Frames / fps)
            {
                Elapsed -= Current.Frames / fps;
                Index = PackageAnimations.Pick(variants.Count, Index, random());
                Cycles++;
            }
        }
    }
}

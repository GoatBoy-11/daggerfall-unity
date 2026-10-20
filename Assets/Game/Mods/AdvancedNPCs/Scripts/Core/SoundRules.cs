using System;
using System.Collections.Generic;

namespace AdvancedNPCs.Core
{
    /// <summary>
    /// The sound pools of an ANPC's Sounds folder (spec sounds §A): a file's pool is its name without the extension
    /// and without a trailing _&lt;digits&gt;, so "attack_3.wav" joins "attack". No Unity or DFU types.
    /// </summary>
    public static class SoundPools
    {
        public const string Greet = "greet";
        public const string Halt = "halt";
        public const string Attack = "attack";
        public const string AttackRange = "attack_range";
        public const string Cast = "cast";
        public const string Heal = "heal";
        public const string Hit = "hit";
        public const string Death = "death";
        public const string Step = "step";
        /// <summary>Lines in a conversation with another ANPC (NpcChat); always play there, nowhere else.</summary>
        public const string Talk = "talk";
        /// <summary>Talk lines for one gender only (an ANPC whose gender is picked at random); before plain talk.</summary>
        public const string TalkMale = "talk_male", TalkFemale = "talk_female";
        /// <summary>Eat package: one per eat / drink sheet cycle; the shared _Sounds ones where the folder has none.</summary>
        public const string Eat = "eat", Drink = "drink";
        /// <summary>Hawk package: a seller calling out to the passing player; no chance roll (its interval paces it).</summary>
        public const string Hawk = "hawk";
        public static readonly string[] All = { Greet, Halt, Attack, AttackRange, Cast, Heal, Hit, Death, Step, Talk, TalkMale, TalkFemale, Eat, Drink, Hawk, CompanionBarkRules.Bark, CompanionBarkRules.Dungeon, CompanionBarkRules.Inn, CompanionBarkRules.Hurt, CompanionBarkRules.NoMagicka, CompanionBarkRules.Recruit, CompanionBarkRules.Wait, CompanionBarkRules.Follow, CompanionBarkRules.Dismiss, CompanionBarkRules.Regroup, CompanionBarkRules.Push, CompanionBarkRules.Combat, CompanionBarkRules.Victory };

        /// <summary>The pool a file name (without extension) belongs to; null for any other name.</summary>
        public static string PoolOf(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            string lower = name.ToLowerInvariant();
            int end = lower.Length;
            while (end > 0 && char.IsDigit(lower[end - 1]))
                end--;
            if (end < lower.Length)
            {
                if (end == 0 || lower[end - 1] != '_')
                    return null;        // "step2": digits without the underscore are part of the name
                lower = lower.Substring(0, end - 1);
            }
            if (lower == "push") lower = CompanionBarkRules.Push;
            return Array.IndexOf(All, lower) >= 0 ? lower : null;
        }

        /// <summary>
        /// The talk pool of an ANPC of this gender ("Male"/"Female", any case): talk_male or talk_female when the folder
        /// has it, else talk; null when it has neither (never the other gender's lines).
        /// </summary>
        public static string TalkPool(string gender, ICollection<string> available)
        {
            string own = string.Equals(gender, "Female", StringComparison.OrdinalIgnoreCase) ? TalkFemale :
                string.Equals(gender, "Male", StringComparison.OrdinalIgnoreCase) ? TalkMale : null;
            if (own != null && available.Contains(own))
                return own;
            return available.Contains(Talk) ? Talk : null;
        }

        /// <summary>attack, attack_range, cast, heal and hit roll combatChance (heal even outside combat).</summary>
        public static bool IsCombat(string pool)
        {
            return pool == Attack || pool == AttackRange || pool == Cast || pool == Heal || pool == Hit;
        }

        /// <summary>The pool that plays for an event: its own, or attack for attack_range; null when there is none.</summary>
        public static string Resolve(string pool, ICollection<string> available)
        {
            if (available.Contains(pool))
                return pool;
            if (pool == AttackRange && available.Contains(Attack))
                return Attack;
            return null;
        }
    }

    /// <summary>
    /// The "ANPC sounds" setting (spec corpse-sound settings): whose voice an ANPC uses. Ints, not an enum: DFU's runtime
    /// compiler cannot load a class whose enum field type is declared in a file it compiles later.
    /// </summary>
    public static class VoiceModes
    {
        /// <summary>DFU's own voice for every ANPC; Sounds folders are not used for voices.</summary>
        public const int Vanilla = 0;
        /// <summary>The ANPC's Sounds folder, DFU's own voice where it has none (the default).</summary>
        public const int Anpc = 1;
        /// <summary>No voice at all, not even DFU's.</summary>
        public const int Off = 2;

        /// <summary>The mode for the setting's choice index; anything unknown is the default.</summary>
        public static int FromSetting(int index)
        {
            return index == Vanilla || index == Off ? index : Anpc;
        }

        /// <summary>The Sounds folder's greet, attack, cast, hit and death sounds play.</summary>
        public static bool CustomVoice(int mode)
        {
            return mode == Anpc;
        }

        /// <summary>DFU voices the ANPC itself (EnemySounds keeps its race): with Vanilla, or with ANPC and no Sounds folder.</summary>
        public static bool DfuVoice(int mode, bool hasSoundsFolder)
        {
            return mode == Vanilla || (mode == Anpc && !hasSoundsFolder);
        }

        /// <summary>A creature's own attack growl is silenced: its folder's attack sounds take over, or voices are off.</summary>
        public static bool SilenceCreatureAttack(int mode, bool hasAttackSounds)
        {
            return mode == Off || (mode == Anpc && hasAttackSounds);
        }
    }

    /// <summary>npc.json "sounds" (spec sounds §C).</summary>
    public class SoundSettings
    {
        public int CombatChance = 50;
        public int GreetChance = 50;
        public float BarkMinSeconds = 45f, BarkMaxSeconds = 90f;
        public float StepDistance = 15f;
        public float SoundPitch = 1f;
        public float SoundPitchVariation = 0f;
        public float StepPitch = 1f;
        public float StepPitchVariation = 0f;
        /// <summary>Without step files of its own, the shared _Sounds steps play (spec default steps).</summary>
        public bool DefaultSteps = true;
        /// <summary>"voice": shared voice packs (ANPCs/_Sounds/va_*); one is picked per person by seed. Empty: none.</summary>
        public List<string> Voices = new List<string>();
    }

    /// <summary>Settings, chances, pitch and footfall frames of custom sounds (spec sounds §C-E). No Unity or DFU types.</summary>
    public static class SoundRules
    {
        public const float MinPitch = 0.1f;
        public const double MinBasePitch = 0.25, MaxBasePitch = 3;
        /// <summary>Seconds between footsteps of an ANPC without a walk/run sheet.</summary>
        public const float WalkStepSeconds = 0.5f, RunStepSeconds = 0.33f;
        /// <summary>Metres beyond which nothing of an ANPC is heard.</summary>
        public const float MaxAudibleDistance = 30f;
        static readonly string[] Keys =
            { "combatChance", "greetChance", "stepDistance", "soundPitch", "soundPitchVariation", "stepPitch", "stepPitchVariation", "defaultSteps", "barkMinSeconds", "barkMaxSeconds", "voice" };
        /// <summary>Voice pack folders in ANPCs/_Sounds start with this.</summary>
        public const string VoicePrefix = "va_";

        /// <summary>Reads "sounds" from an npc.json root: null, or a problem with its key ("sounds.greetChance"). Unknown keys only warn.</summary>
        public static string Read(Dictionary<string, object> root, out string key, out SoundSettings settings, List<string> warnings)
        {
            settings = new SoundSettings();
            key = "sounds";
            object raw;
            if (!root.TryGetValue("sounds", out raw) || raw == null)
                return null;
            Dictionary<string, object> o = raw as Dictionary<string, object>;
            if (o == null)
                return "must be an object like { \"combatChance\": 50, \"greetChance\": 50 }";
            foreach (string k in FieldReader.UnknownKeys(o, Keys))
                warnings.Add("sounds." + k + ": unknown setting (use " + string.Join(", ", Keys) + "), ignored");

            double v;
            string problem;
            if ((problem = Ranged(o, "combatChance", 50, 0, 100, true, out v, ref key)) != null) return problem;
            settings.CombatChance = (int)v;
            if ((problem = Ranged(o, "greetChance", 50, 0, 100, true, out v, ref key)) != null) return problem;
            settings.GreetChance = (int)v;
            if ((problem = Ranged(o, "stepDistance", 15, 0, double.MaxValue, false, out v, ref key)) != null) return problem;
            if (v <= 0)
            {
                key = "sounds.stepDistance";
                return "must be a number of metres above 0";
            }
            settings.StepDistance = (float)v;
            if ((problem = Ranged(o, "soundPitch", 1, MinBasePitch, MaxBasePitch, false, out v, ref key)) != null) return problem;
            settings.SoundPitch = (float)v;
            if ((problem = Ranged(o, "soundPitchVariation", 0, 0, 1, false, out v, ref key)) != null) return problem;
            settings.SoundPitchVariation = (float)v;
            if ((problem = Ranged(o, "stepPitch", 1, MinBasePitch, MaxBasePitch, false, out v, ref key)) != null) return problem;
            settings.StepPitch = (float)v;
            if ((problem = Ranged(o, "stepPitchVariation", 0, 0, 1, false, out v, ref key)) != null) return problem;
            settings.StepPitchVariation = (float)v;
            if ((problem = Ranged(o, "barkMinSeconds", 45, 1, 86400, false, out v, ref key)) != null) return problem;
            settings.BarkMinSeconds = (float)v;
            if ((problem = Ranged(o, "barkMaxSeconds", 90, settings.BarkMinSeconds, 86400, false, out v, ref key)) != null) return problem;
            settings.BarkMaxSeconds = (float)v;
            if (settings.BarkMaxSeconds < settings.BarkMinSeconds) { key="sounds.barkMaxSeconds";return "must be at least barkMinSeconds"; }
            bool defaultSteps;
            if (FieldReader.Bool(o, "defaultSteps", true, out defaultSteps) != null)
            {
                key = "sounds.defaultSteps";
                return "must be true or false";
            }
            settings.DefaultSteps = defaultSteps;
            key = "sounds.voice";
            object voice;
            if (o.TryGetValue("voice", out voice) && voice != null)
            {
                List<string> names;
                if (voice is string)
                    names = new List<string> { (string)voice };
                else if (FieldReader.Texts(o, "voice", out names) != null)
                    names = null;
                if (names == null || names.Count == 0)
                    return "must be a voice pack name like \"va_mercenary_male\" or a list of them";
                foreach (string n in names)
                    if (!IsVoicePackName(n))
                        return "\"" + n + "\" is not a voice pack name: a folder name in ANPCs/_Sounds starting with " + VoicePrefix;
                settings.Voices = names;
            }
            key = "sounds";
            return null;
        }

        /// <summary>"va_" plus a plain folder name (no path parts).</summary>
        public static bool IsVoicePackName(string name)
        {
            return name != null && name.Length > VoicePrefix.Length && name.StartsWith(VoicePrefix, StringComparison.OrdinalIgnoreCase) &&
                name.IndexOfAny(new[] { '/', '\\', ':' }) < 0 && name.IndexOf("..", StringComparison.Ordinal) < 0;
        }

        /// <summary>
        /// The voice pack this person uses: same seed, same pack; null without any. Packs named for the other gender
        /// ("_male" / "_female" at the end) are left out unless nothing else is left.
        /// </summary>
        public static string PickVoice(List<string> voices, uint seed, string gender)
        {
            if (voices == null || voices.Count == 0)
                return null;
            string other = string.Equals(gender, "Female", StringComparison.OrdinalIgnoreCase) ? "_male" :
                string.Equals(gender, "Male", StringComparison.OrdinalIgnoreCase) ? "_female" : null;
            List<string> fit = voices.FindAll(v => other == null || !v.EndsWith(other, StringComparison.OrdinalIgnoreCase));
            if (fit.Count == 0)
                fit = voices;
            return fit.Count == 1 ? fit[0] : fit[new SeededRandom(seed ^ 0x9E3779B9u).Next(fit.Count)];
        }

        static string Ranged(Dictionary<string, object> o, string name, double fallback, double min, double max, bool whole,
            out double value, ref string key)
        {
            string problem = FieldReader.Number(o, name, fallback, out value);
            bool bad = problem != null || (o.ContainsKey(name) && o[name] == null) || double.IsNaN(value) || value < min || value > max ||
                       (whole && value != Math.Floor(value));
            if (!bad)
                return null;
            key = "sounds." + name;
            if (whole)
                return "must be a whole number from " + FieldReader.Num(min) + " to " + FieldReader.Num(max);
            return max == double.MaxValue ? "must be a number" : "must be a number from " + FieldReader.Num(min) + " to " + FieldReader.Num(max);
        }

        /// <summary>Whether a sound with this percent chance plays, for a roll in [0, 1] (Unity's Random.value includes 1).</summary>
        public static bool Rolls(int chance, double roll01)
        {
            return chance >= 100 || roll01 * 100.0 < chance;
        }

        /// <summary>The pitch of one play: pitch ± variation (roll 0 → lowest, 1 → highest), never below MinPitch.</summary>
        public static float Pitch(float pitch, float variation, double roll01)
        {
            float p = pitch + variation * (float)(roll01 * 2.0 - 1.0);
            return Math.Max(MinPitch, p);
        }

        /// <summary>Footfall frames (from 0) of a walk/run sheet: its stepFrames, else the first and the middle frame.</summary>
        public static int[] StepFrames(SpriteAnimation a)
        {
            if (a.StepFrames != null)
                return a.StepFrames;
            int middle = a.Frames / 2;
            return middle == 0 ? new[] { 0 } : new[] { 0, middle };
        }

        /// <summary>
        /// DFU's own voice grunt plays for an event only when the ANPC has no custom pool for it (spec sounds §E), and only
        /// where DFU itself voices one: a melee attack, or a hit by a weapon (not a bow shot, a spell or damage over time).
        /// </summary>
        public static bool VanillaVoice(string pool, bool hasCustomPool, bool magicDamage)
        {
            if (hasCustomPool)
                return false;
            return pool == SoundPools.Attack || (pool == SoundPools.Hit && !magicDamage);
        }

        /// <summary>
        /// Real seconds a replaced sound set (anpc_reload_assets) keeps its clips once unused: long enough for a sound
        /// still playing, such as a death cry, at the lowest pitch.
        /// </summary>
        public static float RetireDelay(float longestClipSeconds)
        {
            return Math.Max(0f, longestClipSeconds) / MinPitch + 0.5f;
        }

        /// <summary>
        /// The shared default footsteps (ANPCs/_Sounds) play for an ANPC whose own Sounds folder has no step files,
        /// unless its npc.json turns them off with "defaultSteps": false (spec default steps).
        /// </summary>
        public static bool UseDefaultSteps(bool ownStepFiles, bool defaultStepsSetting)
        {
            return !ownStepFiles && defaultStepsSetting;
        }

        /// <summary>The shared default footsteps play this much quieter than an ANPC's own (user tuning, 0.21.0).</summary>
        public const float DefaultStepVolume = 0.85f;

        /// <summary>
        /// Volume scale of one footstep (times DFU's sound volume): the "Steps volume" setting (percent, 0-100), and the
        /// shared default steps a little quieter.
        /// </summary>
        public static float StepVolume(bool defaultStep, int stepsVolumePercent)
        {
            float scale = Math.Max(0, Math.Min(100, stepsVolumePercent)) / 100f;
            return defaultStep ? scale * DefaultStepVolume : scale;
        }

        /// <summary>One-shot voices of an ANPC take turns over this many audio sources, so each keeps its own pitch.</summary>
        public const int VoiceSources = 3, StepSources = 2;

        /// <summary>The source after this one, round robin.</summary>
        public static int NextVoice(int current, int count)
        {
            return count <= 1 ? 0 : (current + 1) % count;
        }
    }
}

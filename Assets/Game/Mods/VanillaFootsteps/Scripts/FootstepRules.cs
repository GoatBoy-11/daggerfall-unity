using System;

namespace VanillaFootsteps
{
    /// <summary>How one kind of walker sounds: volume and pitch of its steps.</summary>
    public class StepProfile
    {
        public readonly float Volume;
        public readonly float Pitch;

        public StepProfile(float volume, float pitch)
        {
            Volume = volume;
            Pitch = pitch;
        }
    }

    /// <summary>
    /// When a footfall happens and how it sounds. No Unity or DFU types (Tools~/test-rules.sh tests it with plain mcs).
    /// Enemy ids are DFU's MobileTypes: monsters 0-42, classes 128-146.
    /// </summary>
    public static class FootstepRules
    {
        /// <summary>Seconds between steps of a walker whose sprite frames cannot be read (a mod's 3D model, say).</summary>
        public const float WalkStepSeconds = 0.5f;
        /// <summary>A walker counts as moving while it moved within this many seconds (DFU moves enemies in bursts).</summary>
        public const float MovingGraceSeconds = 0.2f;
        /// <summary>Metres per second below which a change of position is a nudge, not walking.</summary>
        public const float MinWalkSpeed = 0.25f;
        /// <summary>Metres beyond which a step is not heard (the audio fades out linearly up to here).</summary>
        public const float MaxAudibleDistance = 30f;
        /// <summary>Each step changes the profile's pitch by up to ± this.</summary>
        public const float PitchVariation = 0.08f;
        public const float MinPitch = 0.1f;

        /// <summary>The same loudness as ANPC's shared default steps (ANPC plays them at 85%).</summary>
        public static readonly StepProfile Person = new StepProfile(0.85f, 1f);
        public static readonly StepProfile Armoured = new StepProfile(0.95f, 0.9f);
        public static readonly StepProfile Creature = new StepProfile(0.85f, 1f);

        public const int FirstClassId = 128, LastClassId = 146;
        const int Knight = 145, CityWatch = 146;

        /// <summary>A class enemy (mage ... city watch), as opposed to a monster.</summary>
        public static bool IsClass(int mobileId)
        {
            return mobileId >= FirstClassId && mobileId <= LastClassId;
        }

        /// <summary>Volume and pitch of an enemy's steps. Flyers, swimmers and ghosts are left out by behaviour, not here.</summary>
        public static StepProfile ForEnemy(int mobileId)
        {
            if (IsClass(mobileId))
                return mobileId == Knight || mobileId == CityWatch ? Armoured : Person;
            switch (mobileId)
            {
                case 0: return new StepProfile(0.35f, 1.6f);     // rat
                case 6: return new StepProfile(0.5f, 1.3f);      // spider
                case 20: return new StepProfile(0.55f, 1.2f);    // giant scorpion
                case 10: return new StepProfile(0.7f, 1.1f);     // nymph
                case 2: return new StepProfile(0.75f, 0.95f);    // spriggan
                case 5: return new StepProfile(0.8f, 0.9f);      // sabertooth tiger
                case 4:                                          // grizzly bear
                case 8:                                          // centaur
                case 14: return new StepProfile(1f, 0.85f);      // wereboar
                case 25:                                         // frost daedra
                case 26: return new StepProfile(0.95f, 0.8f);    // fire daedra
                case 27:                                         // daedroth
                case 31:                                         // daedra lord
                case 38: return new StepProfile(1f, 0.75f);      // ice atronach
                case 22:                                         // gargoyle
                case 37: return new StepProfile(1f, 0.7f);       // flesh atronach
                case 16: return new StepProfile(1f, 0.65f);      // giant
                case 36: return new StepProfile(1f, 0.6f);       // iron atronach
                default: return Creature;
            }
        }

        /// <summary>A footfall shows on the first and the middle frame of a walk cycle of this many frames.</summary>
        public static bool IsFootfall(int frame, int frames)
        {
            if (frames <= 1)
                return frame == 0;
            return frame == 0 || frame == frames / 2;
        }

        /// <summary>Volume scale of one step (times DFU's sound volume): the profile's, times the setting in percent.</summary>
        public static float Volume(StepProfile profile, int volumePercent)
        {
            return profile.Volume * Math.Max(0, Math.Min(100, volumePercent)) / 100f;
        }

        /// <summary>The pitch of one step: the profile's ± PitchVariation (roll 0 → lowest, 1 → highest).</summary>
        public static float Pitch(StepProfile profile, double roll01)
        {
            return Math.Max(MinPitch, profile.Pitch + PitchVariation * (float)(roll01 * 2.0 - 1.0));
        }

        /// <summary>Metres in one frame beyond which a walker was placed somewhere else (a recycled townsperson), not walking.</summary>
        public const float MaxFrameMove = 2f;

        /// <summary>Whether a horizontal move of this many metres in this many seconds (one frame) is walking.</summary>
        public static bool IsWalking(float metres, float seconds)
        {
            return seconds > 0f && metres <= MaxFrameMove && metres / seconds >= MinWalkSpeed;
        }

        /// <summary>A file name ("step_2.ogg", "Step.wav") that holds a footstep sound.</summary>
        public static bool IsStepFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return false;
            string lower = fileName.ToLowerInvariant();
            if (!lower.EndsWith(".ogg") && !lower.EndsWith(".wav") && !lower.EndsWith(".mp3"))
                return false;
            string stem = lower.Substring(0, lower.Length - 4);
            if (stem == "step")
                return true;
            if (!stem.StartsWith("step_") || stem.Length == 5)
                return false;
            for (int i = 5; i < stem.Length; i++)
                if (stem[i] < '0' || stem[i] > '9')
                    return false;
            return true;
        }
    }
}

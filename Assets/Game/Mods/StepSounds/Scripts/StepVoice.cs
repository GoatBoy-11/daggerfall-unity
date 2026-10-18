using UnityEngine;
using DaggerfallWorkshop;

namespace StepSounds
{
    /// <summary>
    /// The audio sources one walker's steps (or a flyer's wing beats) play from, made on its first sound. Two take
    /// turns, so a sound's pitch does not change the one still playing.
    /// </summary>
    public class StepVoice
    {
        const int SourceCount = 2;
        readonly Transform owner;
        AudioSource[] sources;
        int next;

        public StepVoice(Transform owner)
        {
            this.owner = owner;
        }

        /// <summary>Plays a random step sound with this profile, if the player is in earshot; true when one played.</summary>
        public bool Step(StepProfile profile)
        {
            StepSoundsMod mod = StepSoundsMod.Instance;
            return mod != null && Play(mod.Steps, profile, mod);
        }

        /// <summary>Plays a random wing-flap sound with this profile, if the player is in earshot; true when one played.</summary>
        public bool Wing(StepProfile profile)
        {
            StepSoundsMod mod = StepSoundsMod.Instance;
            return mod != null && Play(mod.Wings, profile, mod);
        }

        bool Play(SoundPool pool, StepProfile profile, StepSoundsMod mod)
        {
            if (mod.VolumePercent <= 0 || !mod.InEarshot(owner.position))
                return false;
            AudioClip clip = pool.Pick();
            if (clip == null)
                return false;
            if (sources == null)
                MakeSources();
            AudioSource source = sources[next];
            next = (next + 1) % sources.Length;
            source.pitch = FootstepRules.Pitch(profile, Random.value);
            source.PlayOneShot(clip, DaggerfallUnity.Settings.SoundVolume * FootstepRules.Volume(profile, mod.VolumePercent));
            pool.Played++;
            return true;
        }

        void MakeSources()
        {
            sources = new AudioSource[SourceCount];
            for (int i = 0; i < SourceCount; i++)
            {
                GameObject go = new GameObject("StepSounds");
                go.transform.SetParent(owner, false);
                AudioSource a = go.AddComponent<AudioSource>();
                a.playOnAwake = false;
                a.spatialBlend = 1f;
                a.rolloffMode = AudioRolloffMode.Linear;
                a.minDistance = 1f;
                a.maxDistance = FootstepRules.MaxAudibleDistance;
                a.dopplerLevel = 0f;
                sources[i] = a;
            }
        }
    }
}

using System;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace AdvancedNPCs
{
    /// <summary>
    /// Sound sets of every NPC folder (spec sounds §A, §F): ANPCs/&lt;npc&gt;/Sounds. Indexed at start (file names only),
    /// loaded one clip per frame while used, released a while after the last user.
    /// </summary>
    public class SoundLibrary
    {
        /// <summary>A clip from a .wav, .ogg or .mp3 file (blocking, as DFU's own loose-file sounds); null and a reason on failure.</summary>
        public static AudioClip LoadClip(string path, out string problem)
        {
            problem = null;
            AudioType type = TypeOf(Path.GetExtension(path));
            if (type == AudioType.UNKNOWN)
            {
                problem = "not a .wav, .ogg or .mp3 file";
                return null;
            }
            try
            {
                using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip("file://" + path, type))
                {
                    DownloadHandlerAudioClip handler = request.downloadHandler as DownloadHandlerAudioClip;
                    if (handler == null)
                    {
                        problem = "no audio handler";
                        return null;
                    }
                    handler.streamAudio = false;
                    request.SendWebRequest();
                    while (!request.isDone) { }
                    if (request.isNetworkError || request.isHttpError)
                    {
                        problem = request.error;
                        return null;
                    }
                    AudioClip clip = handler.audioClip;
                    if (clip == null || clip.length <= 0f)
                    {
                        problem = "could not be decoded";
                        return null;
                    }
                    clip.name = Path.GetFileName(path);
                    return clip;
                }
            }
            catch (Exception e)
            {
                problem = e.Message;
                return null;
            }
        }

        static AudioType TypeOf(string extension)
        {
            switch ((extension ?? "").ToLowerInvariant())
            {
                case ".wav": return AudioType.WAV;
                case ".ogg": return AudioType.OGGVORBIS;
                case ".mp3": return AudioType.MPEG;
                default: return AudioType.UNKNOWN;
            }
        }
    }
}

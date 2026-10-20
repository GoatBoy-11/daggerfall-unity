using System;
using System.IO;
using System.Threading;
using UnityEngine;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>
    /// One sprite sheet read, decoded and DXT5-compressed on a worker thread; SpriteLibrary uploads it on the main thread
    /// once Done. A PNG the worker cannot decode keeps its bytes for Unity's own decoder (slower, on the main thread).
    /// </summary>
    class SheetJob
    {
        public string Name;
        /// <summary>Null for death_static.png.</summary>
        public SpriteAnimation Anim;
        public bool Projectile;
        /// <summary>The worker measures the idle height (sets without standingHeight).</summary>
        public bool Idle;

        volatile bool done;
        public bool Done { get { return done; } }
        public bool Missing;
        public string Error;
        public int Width, Height;
        public byte[] Pixels;
        public bool Dxt;
        /// <summary>The file's bytes when only Unity's decoder can read it.</summary>
        public byte[] Png;
        public float IdleHeight;

        string path;
        SpriteSet set;

        public static SheetJob Start(string name, string path, SpriteSet set, SpriteAnimation anim, bool projectile, bool idle)
        {
            SheetJob job = new SheetJob();
            job.Name = name;
            job.path = path;
            job.set = set;
            job.Anim = anim;
            job.Projectile = projectile;
            job.Idle = idle;
            ThreadPool.QueueUserWorkItem(delegate { job.Run(); });
            return job;
        }

        void Run()
        {
            try
            {
                if (!File.Exists(path))
                {
                    Missing = true;
                    return;
                }
                byte[] bytes = File.ReadAllBytes(path);
                int w, h;
                if (!PngHeader.TryReadSize(bytes, out w, out h))
                {
                    Error = "not a readable PNG";
                    return;
                }
                byte[] rgba = SheetCodec.DecodePng(bytes, out w, out h);
                if (rgba == null)
                {
                    Png = bytes;
                    return;
                }
                Width = w;
                Height = h;
                if (Idle)
                    IdleHeight = SheetCodec.IdleHeight(set, Anim, rgba, w);
                Dxt = w % 4 == 0 && h % 4 == 0;
                Pixels = Dxt ? SheetCodec.EncodeDxt5(rgba, w, h) : rgba;
            }
            catch (Exception e)
            {
                Error = "could not read (" + e.Message + ")";
            }
            finally
            {
                done = true;
            }
        }

        /// <summary>The uploaded, non-readable texture (main thread only); null if Unity cannot read the file either.</summary>
        public Texture2D ToTexture(string label)
        {
            Texture2D t;
            if (Pixels != null)
            {
                t = new Texture2D(Width, Height, Dxt ? TextureFormat.DXT5 : TextureFormat.RGBA32, false);
                t.LoadRawTextureData(Pixels);
                Pixels = null;
            }
            else
            {
                t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (Png == null || !t.LoadImage(Png))
                {
                    UnityEngine.Object.Destroy(t);
                    return null;
                }
                if (Idle)
                    IdleHeight = SpriteLibrary.IdleHeight(set, Anim, t);
                if (t.width % 4 == 0 && t.height % 4 == 0)
                    t.Compress(true);
                Png = null;
            }
            t.name = label;
            NpcSprite.Filtered(t);
            t.wrapMode = TextureWrapMode.Clamp;
            t.Apply(false, true);
            return t;
        }
    }
}

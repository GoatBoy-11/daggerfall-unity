using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>
    /// One sprite sheet read, decoded and DXT5-compressed on a worker thread; SpriteLibrary uploads it on the main thread
    /// once Done. A PNG the worker cannot decode keeps its bytes for Unity's own decoder (slower, on the main thread).
    /// Few, low-priority workers and reused buffers: a big sheet used to make ~150 MB of garbage, on every thread-pool
    /// thread at once, and the garbage collector froze the game while animals loaded.
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
        /// <summary>DXT5 blocks (a pooled buffer, possibly longer than PixelBytes) or RGBA32 pixels.</summary>
        public byte[] Pixels;
        public int PixelBytes;
        public bool Dxt;
        /// <summary>The file's bytes when only Unity's decoder can read it.</summary>
        public byte[] Png;
        public float IdleHeight;

        string path;
        SpriteSet set;

        static readonly Queue<SheetJob> queue = new Queue<SheetJob>();
        static int workers;
        static readonly int MaxWorkers = Math.Max(1, Math.Min(4, Environment.ProcessorCount / 4));

        /// <summary>Per worker, kept while the queue has work: the file's bytes and its joined image data.</summary>
        [ThreadStatic] static byte[] fileBuffer;
        [ThreadStatic] static MemoryStream idat;

        /// <summary>DXT buffers uploaded and free again (main thread returns them, workers take them).</summary>
        static readonly List<byte[]> spare = new List<byte[]>();
        const int MaxSpare = 4;

        public static SheetJob Start(string name, string path, SpriteSet set, SpriteAnimation anim, bool projectile, bool idle)
        {
            SheetJob job = new SheetJob();
            job.Name = name;
            job.path = path;
            job.set = set;
            job.Anim = anim;
            job.Projectile = projectile;
            job.Idle = idle;
            lock (queue)
            {
                queue.Enqueue(job);
                if (workers < MaxWorkers)
                {
                    workers++;
                    Thread t = new Thread(Work);
                    t.IsBackground = true;
                    t.Priority = System.Threading.ThreadPriority.BelowNormal;   // the main and render threads come first
                    t.Name = "ANPC sheet decoder";
                    t.Start();
                }
                else
                    Monitor.Pulse(queue);
            }
            return job;
        }

        static void Work()
        {
            while (true)
            {
                SheetJob job;
                lock (queue)
                {
                    while (queue.Count == 0)
                    {
                        fileBuffer = null;      // idle: let the buffers go
                        idat = null;
                        Monitor.Wait(queue);
                    }
                    job = queue.Dequeue();
                }
                job.Run();
            }
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
                int length = ReadFile(path);
                byte[] bytes = fileBuffer;
                int w, h;
                if (!PngHeader.TryReadSize(bytes, out w, out h))
                {
                    Error = "not a readable PNG";
                    return;
                }
                if (idat == null)
                    idat = new MemoryStream();
                using (PngRows rows = PngRows.Open(bytes, length, idat))
                {
                    if (rows == null)
                    {
                        Png = new byte[length];
                        Buffer.BlockCopy(bytes, 0, Png, 0, length);
                        return;
                    }
                    w = rows.Width;
                    h = rows.Height;
                    Dxt = w % 4 == 0 && h % 4 == 0;
                    IdleProbe probe = Idle ? new IdleProbe(set, Anim) : null;
                    if (Dxt)
                    {
                        PixelBytes = w * h;
                        Pixels = Rent(PixelBytes);
                        SheetCodec.EncodeDxt5(rows, Pixels, probe);
                        if (probe != null)
                            IdleHeight = probe.Best;
                    }
                    else
                    {
                        PixelBytes = w * h * 4;
                        Pixels = new byte[PixelBytes];
                        for (int y = 0; y < h; y++)
                            rows.Next(Pixels, (h - 1 - y) * w * 4);
                        if (probe != null)
                            IdleHeight = SheetCodec.IdleHeight(set, Anim, Pixels, w);
                    }
                    Width = w;
                    Height = h;
                }
            }
            catch (Exception e)
            {
                Error = "could not read (" + e.Message + ")";
                if (Dxt && Pixels != null)
                    Return(Pixels);
                Pixels = null;
            }
            finally
            {
                done = true;
            }
        }

        /// <summary>Reads the file into this worker's fileBuffer (grown as needed); returns its length.</summary>
        static int ReadFile(string path)
        {
            using (FileStream f = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096))
            {
                long size = f.Length;
                if (size > int.MaxValue / 2)
                    throw new IOException("file too large");
                if (fileBuffer == null || fileBuffer.Length < size)
                    fileBuffer = new byte[size];
                int read = 0;
                while (read < size)
                {
                    int n = f.Read(fileBuffer, read, (int)size - read);
                    if (n <= 0)
                        throw new IOException("file ends early");
                    read += n;
                }
                return read;
            }
        }

        static byte[] Rent(int size)
        {
            lock (spare)
            {
                int best = -1;
                for (int i = 0; i < spare.Count; i++)
                    if (spare[i].Length >= size && (best < 0 || spare[i].Length < spare[best].Length))
                        best = i;
                if (best >= 0)
                {
                    byte[] b = spare[best];
                    spare.RemoveAt(best);
                    return b;
                }
            }
            return new byte[size];
        }

        static void Return(byte[] buffer)
        {
            lock (spare)
            {
                spare.Add(buffer);
                if (spare.Count > MaxSpare)
                {
                    int smallest = 0;
                    for (int i = 1; i < spare.Count; i++)
                        if (spare[i].Length < spare[smallest].Length)
                            smallest = i;
                    spare.RemoveAt(smallest);
                }
            }
        }

        /// <summary>Drops the spare DXT buffers (SpriteLibrary, once nothing is loading).</summary>
        public static void TrimSpare()
        {
            lock (spare)
                spare.Clear();
        }

        /// <summary>The uploaded, non-readable texture (main thread only); null if Unity cannot read the file either.</summary>
        public Texture2D ToTexture(string label)
        {
            Texture2D t;
            if (Pixels != null)
            {
                t = new Texture2D(Width, Height, Dxt ? TextureFormat.DXT5 : TextureFormat.RGBA32, false);
                GCHandle pin = GCHandle.Alloc(Pixels, GCHandleType.Pinned);
                try
                {
                    t.LoadRawTextureData(pin.AddrOfPinnedObject(), PixelBytes);
                }
                finally
                {
                    pin.Free();
                }
                if (Dxt)
                    Return(Pixels);
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

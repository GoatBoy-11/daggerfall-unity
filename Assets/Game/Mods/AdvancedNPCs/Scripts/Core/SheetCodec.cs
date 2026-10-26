using System;
using System.IO;
using System.IO.Compression;

namespace AdvancedNPCs.Core
{
    /// <summary>
    /// Sprite sheet decoding that needs no Unity API, so it can run on a worker thread: PNG to RGBA32 and RGBA32 to DXT5
    /// (the main thread then only uploads the result; Texture2D.LoadImage + Compress stalled a frame per sheet).
    /// Pixel rows are bottom-up, as Unity stores texture data.
    /// </summary>
    public static class SheetCodec
    {
        /// <summary>
        /// RGBA32 pixels, rows bottom-up, of an 8-bit non-interlaced PNG (grey, RGB, palette, grey+alpha, RGBA);
        /// null for any other PNG (the caller falls back to Unity's decoder). Throws on a damaged file.
        /// </summary>
        public static byte[] DecodePng(byte[] png, out int width, out int height)
        {
            width = height = 0;
            using (PngRows rows = PngRows.Open(png, png == null ? 0 : png.Length, new MemoryStream()))
            {
                if (rows == null)
                    return null;
                width = rows.Width;
                height = rows.Height;
                byte[] rgba = new byte[width * height * 4];
                for (int y = 0; y < height; y++)
                    rows.Next(rgba, (height - 1 - y) * width * 4);
                return rgba;
            }
        }

        /// <summary>
        /// DXT5 blocks (rows bottom-up) of a PNG whose width and height are multiples of 4, written into output (at least
        /// width * height bytes). Decodes and encodes four rows at a time, so no whole-image RGBA copy is ever made.
        /// idle, if given, sees every row.
        /// </summary>
        public static void EncodeDxt5(PngRows rows, byte[] output, IdleProbe idle)
        {
            int width = rows.Width, height = rows.Height;
            if (width % 4 != 0 || height % 4 != 0)
                throw new ArgumentException("DXT5 needs a width and height divisible by 4");
            byte[] band = new byte[width * 16];
            DxtScratch scratch = new DxtScratch();
            int blockRows = height / 4;
            for (int k = 0; k < blockRows; k++)
            {
                for (int i = 0; i < 4; i++)
                {
                    int offset = (3 - i) * width * 4;   // file rows run top-down, texture rows bottom-up
                    rows.Next(band, offset);
                    if (idle != null)
                        idle.Row(k * 4 + i, band, offset, width);
                }
                EncodeBand(band, 0, width, output, (blockRows - 1 - k) * width * 4, scratch);
            }
        }

        internal static void Unfilter(int filter, byte[] raw, int start, byte[] line, byte[] prev, int bpp)
        {
            int n = line.Length;
            for (int i = 0; i < n; i++)
            {
                int a = i >= bpp ? line[i - bpp] : 0;
                int b = prev[i];
                int c = i >= bpp ? prev[i - bpp] : 0;
                int v = raw[start + i];
                switch (filter)
                {
                    case 0: break;
                    case 1: v += a; break;
                    case 2: v += b; break;
                    case 3: v += (a + b) >> 1; break;
                    case 4:
                        int pa = Math.Abs(b - c), pb = Math.Abs(a - c), pc = Math.Abs(a + b - 2 * c);
                        v += pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
                        break;
                    default: throw new InvalidDataException("unknown PNG row filter " + filter);
                }
                line[i] = (byte)v;
            }
        }

        /// <summary>DXT5 blocks of RGBA32 pixels (width and height multiples of 4), in the same row order as the input.</summary>
        public static byte[] EncodeDxt5(byte[] rgba, int width, int height)
        {
            if (width % 4 != 0 || height % 4 != 0)
                throw new ArgumentException("DXT5 needs a width and height divisible by 4");
            byte[] output = new byte[width * height];   // 16 bytes per 16 pixels
            DxtScratch scratch = new DxtScratch();
            for (int by = 0; by < height; by += 4)
                EncodeBand(rgba, by * width * 4, width, output, by * width, scratch);
            return output;
        }

        sealed class DxtScratch
        {
            public readonly byte[] Block = new byte[64];
            public readonly int[] R = new int[4], G = new int[4], B = new int[4];
        }

        /// <summary>One row of 4x4 blocks: four RGBA rows from src[start] to output[o] (16 bytes per block).</summary>
        static void EncodeBand(byte[] src, int start, int width, byte[] output, int o, DxtScratch scratch)
        {
            byte[] block = scratch.Block;
            for (int bx = 0; bx < width; bx += 4, o += 16)
            {
                bool empty = true;
                for (int r = 0; r < 4; r++)
                {
                    int p = start + (r * width + bx) * 4;
                    Buffer.BlockCopy(src, p, block, r * 16, 16);
                    if (src[p + 3] != 0 || src[p + 7] != 0 || src[p + 11] != 0 || src[p + 15] != 0)
                        empty = false;
                }
                Array.Clear(output, o, 16);     // output may be a reused buffer
                if (!empty)     // most of a sprite sheet is empty: all-zero blocks decode as transparent black
                    EncodeBlock(block, output, o, scratch.R, scratch.G, scratch.B);
            }
        }

        static void EncodeBlock(byte[] px, byte[] output, int o, int[] pr, int[] pg, int[] pb)
        {
            // Alpha: 8-level mode between the block's highest and lowest alpha.
            int aMax = 0, aMin = 255;
            for (int i = 0; i < 16; i++)
            {
                int a = px[i * 4 + 3];
                if (a > aMax) aMax = a;
                if (a < aMin) aMin = a;
            }
            output[o] = (byte)aMax;
            output[o + 1] = (byte)aMin;
            ulong alphaBits = 0;
            if (aMax > aMin)
            {
                for (int i = 0; i < 16; i++)
                {
                    int step = ((aMax - px[i * 4 + 3]) * 7 + (aMax - aMin) / 2) / (aMax - aMin);   // 0 = aMax .. 7 = aMin
                    int code = step == 0 ? 0 : step == 7 ? 1 : step + 1;
                    alphaBits |= (ulong)code << (3 * i);
                }
            }
            for (int i = 0; i < 6; i++)
                output[o + 2 + i] = (byte)(alphaBits >> (8 * i));

            // Colour: the bounding box of the visible pixels, its diagonal flipped to follow how the channels vary together.
            int minR = 255, minG = 255, minB = 255, maxR = 0, maxG = 0, maxB = 0, visible = 0;
            for (int i = 0; i < 16; i++)
            {
                if (px[i * 4 + 3] < 8)
                    continue;
                visible++;
                int r = px[i * 4], g = px[i * 4 + 1], b = px[i * 4 + 2];
                if (r < minR) minR = r;
                if (r > maxR) maxR = r;
                if (g < minG) minG = g;
                if (g > maxG) maxG = g;
                if (b < minB) minB = b;
                if (b > maxB) maxB = b;
            }
            if (visible == 0)
                return;     // colour 0, indices 0: invisible anyway
            int mr = (minR + maxR) / 2, mg = (minG + maxG) / 2, mb = (minB + maxB) / 2;
            int covRG = 0, covRB = 0, covGB = 0;
            for (int i = 0; i < 16; i++)
            {
                if (px[i * 4 + 3] < 8)
                    continue;
                int r = px[i * 4] - mr, g = px[i * 4 + 1] - mg, b = px[i * 4 + 2] - mb;
                covRG += r * g; covRB += r * b; covGB += g * b;
            }
            // The widest channel keeps its direction; the others flip when they fall as it rises.
            int rr = maxR - minR, gg = maxG - minG, bb = maxB - minB;
            if (rr >= gg && rr >= bb)
            {
                if (covRG < 0) Swap(ref minG, ref maxG);
                if (covRB < 0) Swap(ref minB, ref maxB);
            }
            else if (gg >= bb)
            {
                if (covRG < 0) Swap(ref minR, ref maxR);
                if (covGB < 0) Swap(ref minB, ref maxB);
            }
            else
            {
                if (covRB < 0) Swap(ref minR, ref maxR);
                if (covGB < 0) Swap(ref minG, ref maxG);
            }
            int c0 = To565(maxR, maxG, maxB), c1 = To565(minR, minG, minB);
            From565(c0, out pr[0], out pg[0], out pb[0]);
            From565(c1, out pr[1], out pg[1], out pb[1]);
            pr[2] = (2 * pr[0] + pr[1]) / 3; pg[2] = (2 * pg[0] + pg[1]) / 3; pb[2] = (2 * pb[0] + pb[1]) / 3;
            pr[3] = (pr[0] + 2 * pr[1]) / 3; pg[3] = (pg[0] + 2 * pg[1]) / 3; pb[3] = (pb[0] + 2 * pb[1]) / 3;
            uint colorBits = 0;
            for (int i = 0; i < 16; i++)
            {
                int best = 0, bestError = int.MaxValue;
                for (int k = 0; k < 4; k++)
                {
                    int dr = px[i * 4] - pr[k], dg = px[i * 4 + 1] - pg[k], db = px[i * 4 + 2] - pb[k];
                    int e = dr * dr + dg * dg + db * db;
                    if (e < bestError) { bestError = e; best = k; }
                }
                colorBits |= (uint)best << (2 * i);
            }
            output[o + 8] = (byte)c0; output[o + 9] = (byte)(c0 >> 8);
            output[o + 10] = (byte)c1; output[o + 11] = (byte)(c1 >> 8);
            for (int i = 0; i < 4; i++)
                output[o + 12 + i] = (byte)(colorBits >> (8 * i));
        }

        static void Swap(ref int a, ref int b) { int t = a; a = b; b = t; }

        static int To565(int r, int g, int b)
        {
            return (((r * 31 + 127) / 255) << 11) | (((g * 63 + 127) / 255) << 5) | ((b * 31 + 127) / 255);
        }

        static void From565(int c, out int r, out int g, out int b)
        {
            r = ((c >> 11) & 31) * 255 / 31;
            g = ((c >> 5) & 63) * 255 / 63;
            b = (c & 31) * 255 / 31;
        }

        /// <summary>
        /// Tallest opaque pixel above the feet row over every frame and direction of one idle sheet (0 if none);
        /// rgba rows bottom-up, as DecodePng returns them.
        /// </summary>
        public static float IdleHeight(SpriteSet set, SpriteAnimation a, byte[] rgba, int width)
        {
            IdleProbe probe = new IdleProbe(set, a);
            int height = rgba.Length / 4 / width;
            for (int y = 0; y < height; y++)
                probe.Row(y, rgba, (height - 1 - y) * width * 4, width);
            return probe.Best;
        }
    }

    /// <summary>
    /// Measures an idle sheet's standing height row by row: the tallest opaque pixel (alpha above 16) above the feet row
    /// in any frame and direction, in pixels above groundY.
    /// </summary>
    public sealed class IdleProbe
    {
        readonly SpriteSet set;
        readonly SpriteAnimation anim;
        public int Best;

        public IdleProbe(SpriteSet set, SpriteAnimation anim)
        {
            this.set = set;
            this.anim = anim;
        }

        /// <summary>File row fileRow (top-down) as RGBA at rgba[offset].</summary>
        public void Row(int fileRow, byte[] rgba, int offset, int width)
        {
            int cell = set.CellHeight;
            if (fileRow >= SpriteSetParser.Directions.Length * cell)
                return;
            int y = cell - 1 - fileRow % cell;      // height in the cell, 0 = its bottom row
            int candidate = y + 1 - set.GroundY;
            if (y <= set.GroundY || candidate <= Best)
                return;
            int end = Math.Min(width, anim.Frames * anim.CellWidth);
            for (int x = 0; x < end; x++)
            {
                if (rgba[offset + x * 4 + 3] > 16)
                {
                    Best = candidate;
                    return;
                }
            }
        }
    }

    /// <summary>
    /// An 8-bit non-interlaced PNG read one row at a time (top-down) as RGBA32: only a few rows are ever held.
    /// </summary>
    public sealed class PngRows : IDisposable
    {
        static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };

        public int Width, Height;
        int colorType, channels;
        byte[] palette, paletteAlpha;
        byte[] raw, line, prev;
        DeflateStream z;

        /// <summary>
        /// The rows of png[0..length), with idat (emptied first) holding its image data; null for a PNG this reader does
        /// not handle (16-bit, interlaced, ...). Throws on a damaged file.
        /// </summary>
        public static PngRows Open(byte[] png, int length, MemoryStream idat)
        {
            if (png == null || length < 33)
                return null;
            for (int i = 0; i < Signature.Length; i++)
                if (png[i] != Signature[i])
                    return null;
            PngRows r = new PngRows();
            int bitDepth = 0, interlace = 0;
            r.colorType = -1;
            idat.SetLength(0);
            int pos = 8;
            while (pos + 12 <= length)
            {
                int size = (png[pos] << 24) | (png[pos + 1] << 16) | (png[pos + 2] << 8) | png[pos + 3];
                int data = pos + 8;
                if (size < 0 || data + size > length)
                    throw new InvalidDataException("truncated PNG chunk");
                if (Is(png, pos + 4, "IHDR"))
                {
                    r.Width = (png[data] << 24) | (png[data + 1] << 16) | (png[data + 2] << 8) | png[data + 3];
                    r.Height = (png[data + 4] << 24) | (png[data + 5] << 16) | (png[data + 6] << 8) | png[data + 7];
                    bitDepth = png[data + 8];
                    r.colorType = png[data + 9];
                    interlace = png[data + 12];
                }
                else if (Is(png, pos + 4, "PLTE"))
                {
                    r.palette = new byte[size];
                    Buffer.BlockCopy(png, data, r.palette, 0, size);
                }
                else if (Is(png, pos + 4, "tRNS"))
                {
                    r.paletteAlpha = new byte[size];
                    Buffer.BlockCopy(png, data, r.paletteAlpha, 0, size);
                }
                else if (Is(png, pos + 4, "IDAT"))
                    idat.Write(png, data, size);
                else if (Is(png, pos + 4, "IEND"))
                    break;
                pos = data + size + 4;
            }
            int ct = r.colorType;
            r.channels = ct == 0 ? 1 : ct == 2 ? 3 : ct == 3 ? 1 : ct == 4 ? 2 : ct == 6 ? 4 : 0;
            if (bitDepth != 8 || interlace != 0 || r.channels == 0 || r.Width <= 0 || r.Height <= 0 || (ct == 3 && r.palette == null))
                return null;
            int stride = r.Width * r.channels;
            r.raw = new byte[stride + 1];
            r.line = new byte[stride];
            r.prev = new byte[stride];
            idat.Position = 2;      // zlib header; DeflateStream reads the raw deflate data after it
            r.z = new DeflateStream(idat, CompressionMode.Decompress, true);
            return r;
        }

        static bool Is(byte[] png, int at, string type)
        {
            return png[at] == type[0] && png[at + 1] == type[1] && png[at + 2] == type[2] && png[at + 3] == type[3];
        }

        /// <summary>The next row (top-down) as RGBA32 into rgba[offset .. offset + Width * 4).</summary>
        public void Next(byte[] rgba, int offset)
        {
            int read = 0;
            while (read < raw.Length)
            {
                int n = z.Read(raw, read, raw.Length - read);
                if (n <= 0)
                    throw new InvalidDataException("PNG image data ends early");
                read += n;
            }
            SheetCodec.Unfilter(raw[0], raw, 1, line, prev, channels);
            int o = offset;
            for (int x = 0; x < Width; x++, o += 4)
            {
                switch (colorType)
                {
                    case 6:
                        rgba[o] = line[x * 4]; rgba[o + 1] = line[x * 4 + 1]; rgba[o + 2] = line[x * 4 + 2]; rgba[o + 3] = line[x * 4 + 3];
                        break;
                    case 2:
                        rgba[o] = line[x * 3]; rgba[o + 1] = line[x * 3 + 1]; rgba[o + 2] = line[x * 3 + 2]; rgba[o + 3] = 255;
                        break;
                    case 0:
                        rgba[o] = rgba[o + 1] = rgba[o + 2] = line[x]; rgba[o + 3] = 255;
                        break;
                    case 4:
                        rgba[o] = rgba[o + 1] = rgba[o + 2] = line[x * 2]; rgba[o + 3] = line[x * 2 + 1];
                        break;
                    default:
                        int p = line[x];
                        if (p * 3 + 2 < palette.Length)
                        {
                            rgba[o] = palette[p * 3]; rgba[o + 1] = palette[p * 3 + 1]; rgba[o + 2] = palette[p * 3 + 2];
                        }
                        else
                            rgba[o] = rgba[o + 1] = rgba[o + 2] = 0;    // the row buffer may be reused
                        rgba[o + 3] = paletteAlpha != null && p < paletteAlpha.Length ? paletteAlpha[p] : (byte)255;
                        break;
                }
            }
            byte[] swap = prev; prev = line; line = swap;
        }

        public void Dispose()
        {
            if (z != null)
                z.Dispose();
        }
    }
}

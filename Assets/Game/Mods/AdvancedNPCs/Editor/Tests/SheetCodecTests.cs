using System;
using System.IO;
using System.IO.Compression;
using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    public class SheetCodecTests
    {
        /// <summary>A PNG with every row filter (row y uses filter y % 5), top-down rows as in the file.</summary>
        static byte[] Png(byte[] topDown, int w, int h, int colorType, int channels)
        {
            int stride = w * channels;
            byte[] filtered = new byte[(stride + 1) * h];
            for (int y = 0; y < h; y++)
            {
                int f = y % 5;
                filtered[y * (stride + 1)] = (byte)f;
                for (int i = 0; i < stride; i++)
                {
                    int x = topDown[y * stride + i];
                    int a = i >= channels ? topDown[y * stride + i - channels] : 0;
                    int b = y > 0 ? topDown[(y - 1) * stride + i] : 0;
                    int c = y > 0 && i >= channels ? topDown[(y - 1) * stride + i - channels] : 0;
                    int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
                    int predictor = f == 0 ? 0 : f == 1 ? a : f == 2 ? b : f == 3 ? (a + b) / 2 : pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
                    filtered[y * (stride + 1) + 1 + i] = (byte)(x - predictor);
                }
            }
            MemoryStream z = new MemoryStream();
            z.WriteByte(0x78); z.WriteByte(0x01);
            using (DeflateStream d = new DeflateStream(z, CompressionMode.Compress, true))
                d.Write(filtered, 0, filtered.Length);
            z.Write(new byte[4], 0, 4);     // adler32: not checked by the decoder
            MemoryStream png = new MemoryStream();
            png.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, 0, 8);
            Chunk(png, "IHDR", new byte[] { 0, 0, 0, (byte)w, 0, 0, 0, (byte)h, 8, (byte)colorType, 0, 0, 0 });
            Chunk(png, "IDAT", z.ToArray());
            Chunk(png, "IEND", new byte[0]);
            return png.ToArray();
        }

        static void Chunk(MemoryStream s, string type, byte[] data)
        {
            s.Write(new byte[] { (byte)(data.Length >> 24), (byte)(data.Length >> 16), (byte)(data.Length >> 8), (byte)data.Length }, 0, 4);
            foreach (char ch in type) s.WriteByte((byte)ch);
            s.Write(data, 0, data.Length);
            s.Write(new byte[4], 0, 4);     // crc: not checked
        }

        [Test]
        public void DecodesRgbaWithEveryFilter_RowsBottomUp()
        {
            int w = 7, h = 6;
            byte[] top = new byte[w * h * 4];
            new Random(5).NextBytes(top);
            int width, height;
            byte[] rgba = SheetCodec.DecodePng(Png(top, w, h, 6, 4), out width, out height);
            Assert.AreEqual(w, width);
            Assert.AreEqual(h, height);
            for (int y = 0; y < h; y++)
                for (int i = 0; i < w * 4; i++)
                    Assert.AreEqual(top[y * w * 4 + i], rgba[(h - 1 - y) * w * 4 + i], "row " + y + " byte " + i);
        }

        [Test]
        public void DecodesRgbAsOpaque()
        {
            byte[] top = { 10, 20, 30, 40, 50, 60 };
            int width, height;
            byte[] rgba = SheetCodec.DecodePng(Png(top, 2, 1, 2, 3), out width, out height);
            CollectionAssert.AreEqual(new byte[] { 10, 20, 30, 255, 40, 50, 60, 255 }, rgba);
        }

        [Test]
        public void SixteenBitPng_IsLeftToUnity()
        {
            byte[] png = Png(new byte[8], 1, 1, 6, 4);
            png[24] = 16;   // IHDR bit depth
            int width, height;
            Assert.IsNull(SheetCodec.DecodePng(png, out width, out height));
            Assert.IsNull(SheetCodec.DecodePng(new byte[40], out width, out height));
        }

        /// <summary>Colour 0 or 1 plus the 2-bit index's blend, alpha from the 8-level table: the reference DXT5 decode.</summary>
        static byte[] Decode(byte[] block, int pixel)
        {
            int a0 = block[0], a1 = block[1];
            ulong bits = 0;
            for (int i = 0; i < 6; i++) bits |= (ulong)block[2 + i] << (8 * i);
            int ai = (int)((bits >> (3 * pixel)) & 7);
            int alpha = ai == 0 ? a0 : ai == 1 ? a1 : a0 > a1 ? ((8 - ai) * a0 + (ai - 1) * a1) / 7 : ai == 6 ? 0 : ai == 7 ? 255 : ((6 - ai) * a0 + (ai - 1) * a1) / 5;
            int c0 = block[8] | (block[9] << 8), c1 = block[10] | (block[11] << 8);
            int ci = (int)(((uint)(block[12] | (block[13] << 8) | (block[14] << 16) | (block[15] << 24)) >> (2 * pixel)) & 3);
            int[] e0 = { ((c0 >> 11) & 31) * 255 / 31, ((c0 >> 5) & 63) * 255 / 63, (c0 & 31) * 255 / 31 };
            int[] e1 = { ((c1 >> 11) & 31) * 255 / 31, ((c1 >> 5) & 63) * 255 / 63, (c1 & 31) * 255 / 31 };
            byte[] rgb = new byte[4];
            for (int k = 0; k < 3; k++)
                rgb[k] = (byte)(ci == 0 ? e0[k] : ci == 1 ? e1[k] : ci == 2 ? (2 * e0[k] + e1[k]) / 3 : (e0[k] + 2 * e1[k]) / 3);
            rgb[3] = (byte)alpha;
            return rgb;
        }

        [Test]
        public void Dxt5_StreamedRowsMatchWholeImage_InADirtyReusedBuffer()
        {
            int w = 12, h = 8;
            byte[] top = new byte[w * h * 4];
            new Random(9).NextBytes(top);
            for (int i = 0; i < w * 4 * 4; i += 4)
                top[i + 3] = 0;     // an empty top block row
            byte[] png = Png(top, w, h, 6, 4);
            int width, height;
            byte[] expected = SheetCodec.EncodeDxt5(SheetCodec.DecodePng(png, out width, out height), w, h);
            byte[] output = new byte[w * h + 7];
            for (int i = 0; i < output.Length; i++) output[i] = 0xAB;
            using (PngRows rows = PngRows.Open(png, png.Length, new MemoryStream()))
                SheetCodec.EncodeDxt5(rows, output, null);
            for (int i = 0; i < expected.Length; i++)
                Assert.AreEqual(expected[i], output[i], "byte " + i);
        }

        [Test]
        public void IdleProbe_TallestOpaquePixelAboveGround_StreamedOrWhole()
        {
            SpriteSet set = new SpriteSet { CellHeight = 4, GroundY = 1 };
            SpriteAnimation idle = new SpriteAnimation { Name = "idle", CellWidth = 4, Frames = 2 };
            int w = 8, h = 4 * 8;
            byte[] top = new byte[w * h * 4];
            top[((3 * 4 + 0) * w + 5) * 4 + 3] = 255;   // direction 3, frame 1, top row of the cell (y 3)
            top[((5 * 4 + 2) * w + 1) * 4 + 3] = 255;   // direction 5, on the feet row (y 1): not counted
            byte[] png = Png(top, w, h, 6, 4);
            IdleProbe probe = new IdleProbe(set, idle);
            using (PngRows rows = PngRows.Open(png, png.Length, new MemoryStream()))
                SheetCodec.EncodeDxt5(rows, new byte[w * h], probe);
            Assert.AreEqual(3, probe.Best);
            int width, height;
            Assert.AreEqual(3f, SheetCodec.IdleHeight(set, idle, SheetCodec.DecodePng(png, out width, out height), w));
        }

        [Test]
        public void Dxt5_GradientBlockStaysClose()
        {
            byte[] rgba = new byte[4 * 4 * 4];
            for (int i = 0; i < 16; i++)
            {
                rgba[i * 4] = (byte)(40 + i * 12);          // red rises
                rgba[i * 4 + 1] = (byte)(220 - i * 12);     // green falls: the box diagonal must flip
                rgba[i * 4 + 2] = 90;
                rgba[i * 4 + 3] = (byte)(i * 17);
            }
            byte[] dxt = SheetCodec.EncodeDxt5(rgba, 4, 4);
            Assert.AreEqual(16, dxt.Length);
            for (int i = 0; i < 16; i++)
            {
                byte[] px = Decode(dxt, i);
                Assert.AreEqual(rgba[i * 4 + 3], px[3], 20, "alpha " + i);
                if (rgba[i * 4 + 3] < 8)
                    continue;       // colour of invisible pixels does not matter
                for (int k = 0; k < 3; k++)
                    Assert.AreEqual(rgba[i * 4 + k], px[k], 36, "pixel " + i + " channel " + k);
            }
        }

        [Test]
        public void Dxt5_EmptyBlocksAreZero_AndBlocksFollowRowOrder()
        {
            byte[] rgba = new byte[8 * 4 * 4];
            for (int y = 0; y < 4; y++)
                for (int x = 4; x < 8; x++)
                {
                    int o = (y * 8 + x) * 4;
                    rgba[o] = 255; rgba[o + 3] = 255;       // right block solid red
                }
            byte[] dxt = SheetCodec.EncodeDxt5(rgba, 8, 4);
            Assert.AreEqual(32, dxt.Length);
            for (int i = 0; i < 16; i++)
                Assert.AreEqual(0, dxt[i]);
            byte[] right = new byte[16];
            Array.Copy(dxt, 16, right, 0, 16);
            byte[] px = Decode(right, 5);
            Assert.AreEqual(255, px[0]);
            Assert.AreEqual(0, px[1]);
            Assert.AreEqual(255, px[3]);
        }

        [Test]
        public void Allies_SameBuildingGroupOrFaction()
        {
            Assert.IsTrue(HostilityRules.Allied(true, true, -1, -1, 0, 0), "same guild hall");
            Assert.IsFalse(HostilityRules.Allied(false, true, -1, -1, 0, 0), "another building");
            Assert.IsTrue(HostilityRules.Allied(true, false, 2, 2, 0, 0), "same group outdoors");
            Assert.IsFalse(HostilityRules.Allied(true, false, 2, 3, 0, 0), "other group outdoors");
            Assert.IsFalse(HostilityRules.Allied(true, false, -1, -1, 0, 0), "ungrouped strangers outdoors");
            Assert.IsTrue(HostilityRules.Allied(false, false, -1, -1, 40, 40), "stand-ins of one faction");
        }
    }
}

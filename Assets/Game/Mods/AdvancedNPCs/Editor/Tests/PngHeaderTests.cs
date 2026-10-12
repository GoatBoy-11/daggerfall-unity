using NUnit.Framework;
using AdvancedNPCs.Core;

namespace AdvancedNPCs.Tests
{
    /// <summary>Sheet sizes read from the PNG header at startup (spec lazy-sprites §A).</summary>
    public class PngHeaderTests
    {
        static byte[] Head(int w, int h)
        {
            byte[] b = { 137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R',
                0, 0, 0, 0, 0, 0, 0, 0 };
            b[16] = (byte)(w >> 24); b[17] = (byte)(w >> 16); b[18] = (byte)(w >> 8); b[19] = (byte)w;
            b[20] = (byte)(h >> 24); b[21] = (byte)(h >> 16); b[22] = (byte)(h >> 8); b[23] = (byte)h;
            return b;
        }

        [Test]
        public void ReadsWidthAndHeight()
        {
            int w, h;
            Assert.IsTrue(PngHeader.TryReadSize(Head(1320, 2048), out w, out h));
            Assert.AreEqual(1320, w);
            Assert.AreEqual(2048, h);
        }

        [Test]
        public void RejectsShortOrForeignData()
        {
            int w, h;
            Assert.IsFalse(PngHeader.TryReadSize(new byte[10], out w, out h));
            Assert.IsFalse(PngHeader.TryReadSize(null, out w, out h));
            byte[] notPng = Head(16, 16);
            notPng[1] = (byte)'X';
            Assert.IsFalse(PngHeader.TryReadSize(notPng, out w, out h));
            byte[] noIhdr = Head(16, 16);
            noIhdr[12] = (byte)'X';
            Assert.IsFalse(PngHeader.TryReadSize(noIhdr, out w, out h));
            Assert.IsFalse(PngHeader.TryReadSize(Head(0, 16), out w, out h));
        }
    }
}

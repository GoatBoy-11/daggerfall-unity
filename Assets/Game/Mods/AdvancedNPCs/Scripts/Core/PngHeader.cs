namespace AdvancedNPCs.Core
{
    /// <summary>Width and height from a PNG's first 24 bytes (signature + IHDR), without decoding the image.</summary>
    public static class PngHeader
    {
        public const int Length = 24;
        static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };

        public static bool TryReadSize(byte[] head, out int width, out int height)
        {
            width = height = 0;
            if (head == null || head.Length < Length)
                return false;
            for (int i = 0; i < Signature.Length; i++)
            {
                if (head[i] != Signature[i])
                    return false;
            }
            if (head[12] != 'I' || head[13] != 'H' || head[14] != 'D' || head[15] != 'R')
                return false;
            width = (head[16] << 24) | (head[17] << 16) | (head[18] << 8) | head[19];
            height = (head[20] << 24) | (head[21] << 16) | (head[22] << 8) | head[23];
            return width > 0 && height > 0;
        }
    }
}

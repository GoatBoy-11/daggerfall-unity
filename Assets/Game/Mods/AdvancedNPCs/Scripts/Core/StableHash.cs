using System.Text;

namespace AdvancedNPCs.Core
{
    /// <summary>
    /// FNV-1a 32-bit hash of the UTF-8 bytes. Unlike string.GetHashCode it never changes between runs, runtimes
    /// or versions, so it can seed generic people that must look the same on every visit (spec §4, §7.4).
    /// </summary>
    public static class StableHash
    {
        public static uint Of(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text == null ? "" : text);
            uint hash = 2166136261;
            unchecked
            {
                for (int i = 0; i < bytes.Length; i++)
                {
                    hash ^= bytes[i];
                    hash *= 16777619;
                }
            }
            return hash;
        }
    }
}

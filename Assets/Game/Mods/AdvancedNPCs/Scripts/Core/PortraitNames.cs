using System;

namespace AdvancedNPCs.Core
{
    /// <summary>Portrait names as used in npc.json and as keys of _Portraits/*.png: trimmed, lowercase, no ".png".</summary>
    public static class PortraitNames
    {
        public static string Normalize(string name)
        {
            if (name == null)
                return "";
            string n = name.Trim().ToLowerInvariant();
            if (n.EndsWith(".png", StringComparison.Ordinal))
                n = n.Substring(0, n.Length - 4);
            return n;
        }
    }
}

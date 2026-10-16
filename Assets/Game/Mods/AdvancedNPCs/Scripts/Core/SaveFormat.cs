namespace AdvancedNPCs.Core
{
    /// <summary>
    /// The version of the mod's save data (spec tools-quality §B3). Saves from before the version existed read 0 and
    /// load as today; a later format can migrate from the number. No Unity or DFU types.
    /// </summary>
    public static class SaveFormat
    {
        public const int Current = 6;

        /// <summary>Null for a format this version reads, else a warning for the log.</summary>
        public static string Check(int version)
        {
            if (version >= 0 && version <= Current)
                return null;
            return "save written by a newer Advanced NPCs (format " + version + "); unknown data is ignored";
        }
    }
}

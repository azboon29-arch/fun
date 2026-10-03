namespace Game.Core.Save
{
    /// <summary>
    /// Layout of the save file root. Bump <see cref="CurrentVersion"/> whenever the stored shape changes,
    /// and register an <see cref="ISaveMigration"/> from the previous version.
    /// </summary>
    public static class SaveFormat
    {
        /// <summary>The save version this build writes.</summary>
        public const int CurrentVersion = 1;

        /// <summary>Root key holding the format version.</summary>
        public const string VersionKey = "version";

        /// <summary>Root key holding when the save was first created (Unix seconds, UTC).</summary>
        public const string CreatedAtKey = "createdAt";

        /// <summary>Root key holding when the save was last written (Unix seconds, UTC).</summary>
        public const string SavedAtKey = "savedAt";

        /// <summary>Root key holding one object per <see cref="ISaveSection"/>.</summary>
        public const string SectionsKey = "sections";
    }
}

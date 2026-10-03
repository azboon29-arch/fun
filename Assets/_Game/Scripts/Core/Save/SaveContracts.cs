using Game.Core.Json;

namespace Game.Core.Save
{
    /// <summary>
    /// A system that owns one section of the save file. The save service calls <see cref="Capture"/> when
    /// writing and <see cref="Restore"/> after loading.
    /// </summary>
    public interface ISaveSection
    {
        /// <summary>Unique, stable key of this section in the save file (e.g. "board"). Never rename without a migration.</summary>
        string Key { get; }

        /// <summary>Returns the current state as JSON.</summary>
        JsonObject Capture();

        /// <summary>
        /// Restores state from <paramref name="data"/>. Called with null for a new game (or when the section is
        /// missing from the file), in which case the system resets to its starting state.
        /// </summary>
        void Restore(JsonObject data);
    }

    /// <summary>Upgrades raw save JSON from one version to the next.</summary>
    public interface ISaveMigration
    {
        /// <summary>The version this migration reads. It produces <c>FromVersion + 1</c>.</summary>
        int FromVersion { get; }

        /// <summary>Reshapes <paramref name="root"/> in place. The migrator updates the version number afterwards.</summary>
        void Migrate(JsonObject root);
    }

    /// <summary>Key/value text storage for save files (disk on device, memory in tests).</summary>
    public interface ISaveStorage
    {
        /// <summary>Returns the stored text for <paramref name="key"/>, or null if none exists.</summary>
        string Read(string key);

        /// <summary>Stores <paramref name="content"/> under <paramref name="key"/>, replacing any previous value atomically.</summary>
        void Write(string key, string content);

        /// <summary>Deletes the value for <paramref name="key"/>. Returns false if there was none.</summary>
        bool Delete(string key);
    }

    /// <summary>Outcome of <see cref="SaveService.Load"/>.</summary>
    public enum LoadStatus
    {
        /// <summary>No save existed; every system starts fresh.</summary>
        NewGame,

        /// <summary>The save was read at the current version.</summary>
        Loaded,

        /// <summary>The save was read from an older version and migrated.</summary>
        Migrated,

        /// <summary>The main file was unreadable, so the backup was used.</summary>
        RestoredFromBackup,

        /// <summary>Both the main file and backup were unreadable. A copy was kept and a new game started.</summary>
        CorruptStartedNew,

        /// <summary>The save comes from a newer build. Nothing was changed and saving is disabled to protect it.</summary>
        TooNew,
    }

    /// <summary>Tunable save settings, supplied from the game's config asset.</summary>
    public sealed class SaveSettings
    {
        /// <summary>Creates settings.</summary>
        /// <param name="fileKey">Storage key of the main save (e.g. "save"). Backup and corrupt copies derive from it.</param>
        /// <param name="autosaveDelaySeconds">Seconds after the first unsaved change before autosave writes.</param>
        public SaveSettings(string fileKey, double autosaveDelaySeconds)
        {
            FileKey = fileKey;
            AutosaveDelaySeconds = autosaveDelaySeconds;
        }

        /// <summary>Storage key of the main save.</summary>
        public string FileKey { get; }

        /// <summary>Storage key of the previous good save.</summary>
        public string BackupKey => FileKey + ".bak";

        /// <summary>Storage key where an unreadable save is preserved for debugging.</summary>
        public string CorruptKey => FileKey + ".corrupt";

        /// <summary>Seconds after the first unsaved change before autosave writes.</summary>
        public double AutosaveDelaySeconds { get; }
    }
}

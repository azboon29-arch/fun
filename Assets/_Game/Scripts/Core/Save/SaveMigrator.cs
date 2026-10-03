using System;
using System.Collections.Generic;
using Game.Core.Json;

namespace Game.Core.Save
{
    /// <summary>Thrown when a save was written by a newer build than this one.</summary>
    public sealed class SaveTooNewException : Exception
    {
        /// <summary>Creates the exception.</summary>
        public SaveTooNewException(int fileVersion, int currentVersion)
            : base($"Save version {fileVersion} is newer than this build supports ({currentVersion}).")
        {
            FileVersion = fileVersion;
        }

        /// <summary>The version found in the file.</summary>
        public int FileVersion { get; }
    }

    /// <summary>Applies registered <see cref="ISaveMigration"/> steps one version at a time.</summary>
    public sealed class SaveMigrator
    {
        private readonly Dictionary<int, ISaveMigration> _steps = new Dictionary<int, ISaveMigration>();

        /// <summary>Creates a migrator targeting <paramref name="currentVersion"/>.</summary>
        public SaveMigrator(int currentVersion)
        {
            if (currentVersion < 1) throw new ArgumentOutOfRangeException(nameof(currentVersion));
            CurrentVersion = currentVersion;
        }

        /// <summary>The version every save is migrated to.</summary>
        public int CurrentVersion { get; }

        /// <summary>Adds a migration step. Only one step per source version is allowed.</summary>
        public SaveMigrator Register(ISaveMigration migration)
        {
            if (migration == null) throw new ArgumentNullException(nameof(migration));
            if (migration.FromVersion < 1 || migration.FromVersion >= CurrentVersion)
            {
                throw new ArgumentOutOfRangeException(nameof(migration),
                    $"Migration from version {migration.FromVersion} is outside 1..{CurrentVersion - 1}.");
            }
            if (_steps.ContainsKey(migration.FromVersion))
            {
                throw new InvalidOperationException($"A migration from version {migration.FromVersion} is already registered.");
            }
            _steps[migration.FromVersion] = migration;
            return this;
        }

        /// <summary>
        /// Upgrades <paramref name="root"/> in place to <see cref="CurrentVersion"/>.
        /// Returns the number of steps applied.
        /// </summary>
        /// <exception cref="SaveTooNewException">The file is newer than this build.</exception>
        /// <exception cref="FormatException">The version is missing/invalid or a step is not registered.</exception>
        public int Migrate(JsonObject root)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (!(root[SaveFormat.VersionKey] is JsonNumber versionNode))
            {
                throw new FormatException("Save has no version number.");
            }

            var version = checked((int)versionNode.AsLong());
            if (version < 1) throw new FormatException($"Save version {version} is invalid.");
            if (version > CurrentVersion) throw new SaveTooNewException(version, CurrentVersion);

            var applied = 0;
            while (version < CurrentVersion)
            {
                if (!_steps.TryGetValue(version, out var step))
                {
                    throw new FormatException($"No save migration registered from version {version}.");
                }
                step.Migrate(root);
                version++;
                root.Set(SaveFormat.VersionKey, version);
                applied++;
            }
            return applied;
        }
    }
}

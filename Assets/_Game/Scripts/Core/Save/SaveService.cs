using System;
using System.Collections.Generic;
using Game.Core.Events;
using Game.Core.Json;
using Game.Core.Services;

namespace Game.Core.Save
{
    /// <summary>
    /// Loads and writes the single local JSON save. Systems contribute data through <see cref="ISaveSection"/>.
    /// The service autosaves after any <see cref="GameStateChanged"/> event (debounced, see <see cref="Tick"/>)
    /// and the app layer calls <see cref="Flush"/> on pause/quit.
    /// </summary>
    public sealed class SaveService : IDisposable
    {
        private readonly ISaveStorage _storage;
        private readonly SaveMigrator _migrator;
        private readonly EventBus _bus;
        private readonly IClock _clock;
        private readonly IGameLog _log;
        private readonly SaveSettings _settings;
        private readonly Dictionary<string, ISaveSection> _sections = new Dictionary<string, ISaveSection>();
        private readonly IDisposable _changeSubscription;

        // Sections found in the file that no registered system claimed. Written back unchanged so data is never lost.
        private JsonObject _unclaimedSections = new JsonObject();
        private long _createdAtUnix;
        private DateTime? _dirtySinceUtc;

        /// <summary>Creates the service and subscribes to <see cref="GameStateChanged"/> on <paramref name="bus"/>.</summary>
        public SaveService(ISaveStorage storage, SaveMigrator migrator, EventBus bus, IClock clock, IGameLog log, SaveSettings settings)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _migrator = migrator ?? throw new ArgumentNullException(nameof(migrator));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _log = log ?? NullGameLog.Instance;
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _changeSubscription = _bus.Subscribe<GameStateChanged>(_ => MarkDirty());
        }

        /// <summary>True when there are changes not yet written.</summary>
        public bool IsDirty => _dirtySinceUtc.HasValue;

        /// <summary>True when saving is disabled because the file on disk came from a newer build.</summary>
        public bool IsReadOnly { get; private set; }

        /// <summary>When the loaded save was last written, or null for a new game. Used for offline earnings.</summary>
        public DateTime? LastSavedUtc { get; private set; }

        /// <summary>Result of the most recent <see cref="Load"/>.</summary>
        public LoadStatus LastLoadStatus { get; private set; }

        /// <summary>Adds a section. Register every section before calling <see cref="Load"/>.</summary>
        public void Register(ISaveSection section)
        {
            if (section == null) throw new ArgumentNullException(nameof(section));
            if (string.IsNullOrEmpty(section.Key)) throw new ArgumentException("Section key is required.", nameof(section));
            if (_sections.ContainsKey(section.Key))
            {
                throw new InvalidOperationException($"A save section with key '{section.Key}' is already registered.");
            }
            _sections.Add(section.Key, section);
        }

        /// <summary>
        /// Reads the save (falling back to the backup if the main file is unreadable), migrates it to the
        /// current version and restores every registered section. Publishes <see cref="GameLoaded"/>.
        /// </summary>
        public LoadStatus Load()
        {
            IsReadOnly = false;
            _dirtySinceUtc = null;
            var status = LoadInternal();
            LastLoadStatus = status;
            _bus.Publish(new GameLoaded(status));
            return status;
        }

        /// <summary>Writes the save now if anything changed. Call on app pause and quit.</summary>
        public void Flush()
        {
            if (IsDirty) Save();
        }

        /// <summary>Writes the save if changes have waited at least the autosave delay. Call every frame.</summary>
        public void Tick()
        {
            if (!_dirtySinceUtc.HasValue) return;
            var waited = (_clock.UtcNow - _dirtySinceUtc.Value).TotalSeconds;
            if (waited >= _settings.AutosaveDelaySeconds) Save();
        }

        /// <summary>Flags that state changed. Autosave will write after the configured delay.</summary>
        public void MarkDirty()
        {
            if (!_dirtySinceUtc.HasValue) _dirtySinceUtc = _clock.UtcNow;
        }

        /// <summary>
        /// Writes the save immediately. The previous file is kept as a backup first.
        /// Returns false if saving is disabled (see <see cref="IsReadOnly"/>) or the write failed.
        /// </summary>
        public bool Save()
        {
            if (IsReadOnly)
            {
                _log.Warn("Save skipped: the save on this device is from a newer version of the game.");
                return false;
            }

            var now = _clock.UtcNow;
            var nowUnix = ToUnix(now);
            if (_createdAtUnix == 0) _createdAtUnix = nowUnix;

            var root = new JsonObject()
                .Set(SaveFormat.VersionKey, _migrator.CurrentVersion)
                .Set(SaveFormat.CreatedAtKey, _createdAtUnix)
                .Set(SaveFormat.SavedAtKey, nowUnix);

            var sections = new JsonObject();
            foreach (var key in _unclaimedSections.Keys) sections.Set(key, _unclaimedSections[key]);
            foreach (var pair in _sections) sections.Set(pair.Key, pair.Value.Capture() ?? new JsonObject());
            root.Set(SaveFormat.SectionsKey, sections);

            try
            {
                var previous = _storage.Read(_settings.FileKey);
                if (previous != null && IsReadable(previous)) _storage.Write(_settings.BackupKey, previous);
                _storage.Write(_settings.FileKey, root.ToJson(pretty: true));
            }
            catch (Exception e)
            {
                // Keep the dirty flag so the next tick retries.
                _log.Error($"Save failed: {e.Message}");
                return false;
            }

            _dirtySinceUtc = null;
            LastSavedUtc = FromUnix(nowUnix);
            _bus.Publish(new GameSaved(nowUnix));
            return true;
        }

        /// <summary>Deletes all save files and restores every section to a new game. Intended for debug tools.</summary>
        public void DeleteAll()
        {
            _storage.Delete(_settings.FileKey);
            _storage.Delete(_settings.BackupKey);
            _storage.Delete(_settings.CorruptKey);
            Load();
        }

        /// <summary>Unsubscribes from the event bus.</summary>
        public void Dispose() { _changeSubscription.Dispose(); }

        private LoadStatus LoadInternal()
        {
            var mainText = SafeRead(_settings.FileKey);
            if (mainText == null)
            {
                StartNewGame();
                return LoadStatus.NewGame;
            }

            if (TryApply(mainText, out var migrated, out var tooNew)) return migrated ? LoadStatus.Migrated : LoadStatus.Loaded;
            if (tooNew) return LoadStatus.TooNew;

            _log.Warn("Main save is unreadable, trying the backup.");
            var backupText = SafeRead(_settings.BackupKey);
            if (backupText != null && TryApply(backupText, out _, out tooNew))
            {
                // Keep the unreadable file for debugging, and rewrite a good main file on the next save.
                _storage.Write(_settings.CorruptKey, mainText);
                MarkDirty();
                return LoadStatus.RestoredFromBackup;
            }
            if (tooNew) return LoadStatus.TooNew;

            _log.Error("Save and backup are both unreadable. Starting a new game; the bad file was kept.");
            _storage.Write(_settings.CorruptKey, mainText);
            StartNewGame();
            return LoadStatus.CorruptStartedNew;
        }

        private bool TryApply(string text, out bool migrated, out bool tooNew)
        {
            migrated = false;
            tooNew = false;
            JsonObject root;
            try
            {
                root = JsonNode.Parse(text) as JsonObject ?? throw new FormatException("Save root is not an object.");
                migrated = _migrator.Migrate(root) > 0;
            }
            catch (SaveTooNewException e)
            {
                _log.Error(e.Message);
                tooNew = true;
                IsReadOnly = true;
                RestoreSections(null);
                return false;
            }
            catch (Exception e) when (e is FormatException || e is OverflowException || e is InvalidCastException)
            {
                _log.Warn($"Save could not be read: {e.Message}");
                return false;
            }

            _createdAtUnix = root.GetLong(SaveFormat.CreatedAtKey);
            var savedAt = root.GetLong(SaveFormat.SavedAtKey);
            LastSavedUtc = savedAt > 0 ? FromUnix(savedAt) : (DateTime?)null;

            var sections = root.GetObject(SaveFormat.SectionsKey) ?? new JsonObject();
            RestoreSections(sections);
            if (migrated) MarkDirty();
            return true;
        }

        private void RestoreSections(JsonObject sections)
        {
            _unclaimedSections = new JsonObject();
            if (sections != null)
            {
                foreach (var key in sections.Keys)
                {
                    if (!_sections.ContainsKey(key)) _unclaimedSections.Set(key, sections[key]);
                }
            }
            foreach (var pair in _sections)
            {
                pair.Value.Restore(sections?.GetObject(pair.Key));
            }
        }

        private void StartNewGame()
        {
            _createdAtUnix = 0;
            LastSavedUtc = null;
            RestoreSections(null);
        }

        private string SafeRead(string key)
        {
            try
            {
                return _storage.Read(key);
            }
            catch (Exception e)
            {
                _log.Warn($"Could not read '{key}': {e.Message}");
                return null;
            }
        }

        private static bool IsReadable(string text)
        {
            try
            {
                return JsonNode.Parse(text) is JsonObject;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static long ToUnix(DateTime utc) => new DateTimeOffset(utc.ToUniversalTime()).ToUnixTimeSeconds();

        private static DateTime FromUnix(long seconds) => DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime;
    }
}

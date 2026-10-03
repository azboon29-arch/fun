namespace Game.Core.Events
{
    /// <summary>
    /// Published by any system whose persistent state changed. The save service marks itself dirty
    /// and autosaves after a short debounce.
    /// </summary>
    public readonly struct GameStateChanged
    {
        /// <summary>Short label of what changed, for logs only.</summary>
        public readonly string Source;

        /// <summary>Creates the event.</summary>
        public GameStateChanged(string source) { Source = source; }
    }

    /// <summary>Published after a save file has been written.</summary>
    public readonly struct GameSaved
    {
        /// <summary>UTC time of the save, in Unix seconds.</summary>
        public readonly long SavedAtUnixSeconds;

        /// <summary>Creates the event.</summary>
        public GameSaved(long savedAtUnixSeconds) { SavedAtUnixSeconds = savedAtUnixSeconds; }
    }

    /// <summary>Published after a save has been loaded (or a new game was created) and restored into all systems.</summary>
    public readonly struct GameLoaded
    {
        /// <summary>How the load went.</summary>
        public readonly Save.LoadStatus Status;

        /// <summary>Creates the event.</summary>
        public GameLoaded(Save.LoadStatus status) { Status = status; }
    }
}

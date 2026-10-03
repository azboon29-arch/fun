using System;

namespace Game.Core.Services
{
    /// <summary>Source of wall-clock time. Swap in a fake clock in tests (e.g. for offline earnings).</summary>
    public interface IClock
    {
        /// <summary>Current time in UTC.</summary>
        DateTime UtcNow { get; }
    }

    /// <summary>Real system clock.</summary>
    public sealed class SystemClock : IClock
    {
        /// <inheritdoc />
        public DateTime UtcNow => DateTime.UtcNow;
    }

    /// <summary>Clock whose time is set by hand. Used by tests and debug tools.</summary>
    public sealed class ManualClock : IClock
    {
        /// <summary>Creates a clock showing <paramref name="start"/> (converted to UTC).</summary>
        public ManualClock(DateTime start) { UtcNow = start.ToUniversalTime(); }

        /// <inheritdoc />
        public DateTime UtcNow { get; set; }

        /// <summary>Moves the clock forward by <paramref name="delta"/>.</summary>
        public void Advance(TimeSpan delta) { UtcNow = UtcNow + delta; }
    }
}

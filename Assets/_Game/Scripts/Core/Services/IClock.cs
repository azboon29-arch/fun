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

    /// <summary>
    /// Wraps another clock and adds an adjustable offset. Debug tools use it to simulate time away
    /// (offline earnings) without touching the device clock.
    /// </summary>
    public sealed class OffsetClock : IClock
    {
        private readonly IClock _inner;

        /// <summary>Creates a clock that follows <paramref name="inner"/> plus <see cref="Offset"/>.</summary>
        public OffsetClock(IClock inner) { _inner = inner ?? throw new ArgumentNullException(nameof(inner)); }

        /// <summary>Time added to the inner clock.</summary>
        public TimeSpan Offset { get; set; }

        /// <inheritdoc />
        public DateTime UtcNow => _inner.UtcNow + Offset;
    }
}

namespace Game.Core.Services
{
    /// <summary>Logging abstraction so plain C# systems can log without referencing UnityEngine.</summary>
    public interface IGameLog
    {
        /// <summary>Logs an informational message.</summary>
        void Info(string message);

        /// <summary>Logs a warning.</summary>
        void Warn(string message);

        /// <summary>Logs an error.</summary>
        void Error(string message);
    }

    /// <summary>A log that discards everything. Default for tests.</summary>
    public sealed class NullGameLog : IGameLog
    {
        /// <summary>Shared instance.</summary>
        public static readonly NullGameLog Instance = new NullGameLog();

        /// <inheritdoc />
        public void Info(string message) { }

        /// <inheritdoc />
        public void Warn(string message) { }

        /// <inheritdoc />
        public void Error(string message) { }
    }
}

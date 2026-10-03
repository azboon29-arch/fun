using System.Collections.Generic;

namespace Game.Core.Save
{
    /// <summary>In-memory <see cref="ISaveStorage"/> for tests and tools.</summary>
    public sealed class MemorySaveStorage : ISaveStorage
    {
        private readonly Dictionary<string, string> _files = new Dictionary<string, string>();

        /// <summary>How many times <see cref="Write"/> was called. Handy for autosave tests.</summary>
        public int WriteCount { get; private set; }

        /// <inheritdoc />
        public string Read(string key) => _files.TryGetValue(key, out var content) ? content : null;

        /// <inheritdoc />
        public void Write(string key, string content)
        {
            _files[key] = content;
            WriteCount++;
        }

        /// <inheritdoc />
        public bool Delete(string key) => _files.Remove(key);
    }
}

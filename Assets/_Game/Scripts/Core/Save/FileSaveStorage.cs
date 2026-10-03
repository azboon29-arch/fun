using System;
using System.IO;
using System.Text;

namespace Game.Core.Save
{
    /// <summary>
    /// Stores each key as <c>&lt;key&gt;.json</c> in a directory. Writes go to a temporary file first and are
    /// then moved into place, so a crash mid-write never leaves a half-written save.
    /// </summary>
    public sealed class FileSaveStorage : ISaveStorage
    {
        private const string Extension = ".json";
        private const string TempSuffix = ".tmp";

        private readonly string _directory;

        /// <summary>Creates storage rooted at <paramref name="directory"/> (created if missing).</summary>
        public FileSaveStorage(string directory)
        {
            if (string.IsNullOrEmpty(directory)) throw new ArgumentException("Directory is required.", nameof(directory));
            _directory = directory;
        }

        /// <summary>Full path of the file used for <paramref name="key"/>.</summary>
        public string PathFor(string key) => Path.Combine(_directory, key + Extension);

        /// <inheritdoc />
        public string Read(string key)
        {
            var path = PathFor(key);
            if (File.Exists(path)) return File.ReadAllText(path, Encoding.UTF8);

            // A crash between deleting the old file and moving the new one in leaves only the complete temp file.
            var temp = path + TempSuffix;
            return File.Exists(temp) ? File.ReadAllText(temp, Encoding.UTF8) : null;
        }

        /// <inheritdoc />
        public void Write(string key, string content)
        {
            Directory.CreateDirectory(_directory);
            var path = PathFor(key);
            var temp = path + TempSuffix;
            File.WriteAllText(temp, content, new UTF8Encoding(false));
            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);
        }

        /// <inheritdoc />
        public bool Delete(string key)
        {
            var path = PathFor(key);
            if (!File.Exists(path)) return false;
            File.Delete(path);
            return true;
        }
    }
}

using Game.Core.Services;
using UnityEngine;

namespace Game.App
{
    /// <summary>Routes plain C# logging to the Unity console.</summary>
    public sealed class UnityGameLog : IGameLog
    {
        private const string Prefix = "[Game] ";

        /// <inheritdoc />
        public void Info(string message) => Debug.Log(Prefix + message);

        /// <inheritdoc />
        public void Warn(string message) => Debug.LogWarning(Prefix + message);

        /// <inheritdoc />
        public void Error(string message) => Debug.LogError(Prefix + message);
    }
}

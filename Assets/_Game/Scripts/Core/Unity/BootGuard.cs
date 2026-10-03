using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.App
{
    /// <summary>
    /// Makes pressing Play in any scene work: if services are not booted yet, the Boot scene runs first and then
    /// returns to the scene you started in.
    /// </summary>
    public static class BootGuard
    {
        private static string _pendingScene;

        /// <summary>Returns and clears the scene to open after boot, if Play was pressed in another scene.</summary>
        public static string ConsumePendingScene()
        {
            var scene = _pendingScene;
            _pendingScene = null;
            return scene;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureBoot()
        {
            _pendingScene = null;
            var active = SceneManager.GetActiveScene().name;
            if (active == SceneNames.Boot || GameBootstrap.IsBooted) return;
            if (!Application.CanStreamedLevelBeLoaded(SceneNames.Boot)) return;

            _pendingScene = active;
            SceneManager.LoadScene(SceneNames.Boot);
        }
    }
}

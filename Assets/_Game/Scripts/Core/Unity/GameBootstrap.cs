using System;
using Game.Core.Audio;
using Game.Core.Events;
using Game.Core.Random;
using Game.Core.Save;
using Game.Core.Services;
using Game.Data;
using Game.Logic.Merge;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.App
{
    /// <summary>
    /// Lives in the Boot scene. Builds the <see cref="GameServices"/> root, loads the save, then opens the Base scene.
    /// Persists across scenes and drives autosave (every frame, on pause and on quit).
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private GameConfig config;

        [Tooltip("Scene opened once boot finishes.")]
        [SerializeField] private string firstScene = SceneNames.Base;

        private SaveService _save;

        /// <summary>True once services exist. Scenes opened directly in the Editor use this to boot first.</summary>
        public static bool IsBooted => GameServices.Current != null;

        private void Awake()
        {
            if (IsBooted)
            {
                // A second Boot scene was loaded; the original keeps running.
                Destroy(gameObject);
                return;
            }

            DontDestroyOnLoad(gameObject);
            if (config == null)
            {
                Debug.LogWarning("[Game] GameBootstrap has no GameConfig assigned; using defaults. Run Tools > Merge Command > Build Scenes.");
                config = ScriptableObject.CreateInstance<GameConfig>();
            }
            Application.targetFrameRate = config.TargetFrameRate;

            var services = BuildServices();
            GameServices.SetCurrent(services);

            var status = _save.Load();
            services.Get<IGameLog>().Info($"Save loaded: {status}");

            var pending = BootGuard.ConsumePendingScene();
            SceneManager.LoadScene(string.IsNullOrEmpty(pending) ? firstScene : pending);
        }

        private GameServices BuildServices()
        {
            var services = new GameServices();
            var log = new UnityGameLog();
            var bus = new EventBus();
            var clock = new SystemClock();

            services.Register(config);
            services.Register<IGameLog>(log);
            services.Register(bus);
            services.Register<IClock>(clock);
            services.Register<IAudioService>(new NullAudioService());

            var migrator = new SaveMigrator(SaveFormat.CurrentVersion);
            var storage = new FileSaveStorage(Application.persistentDataPath);
            _save = new SaveService(storage, migrator, bus, clock, log, config.CreateSaveSettings());
            services.Register(_save);

            // Every system with saved state registers its ISaveSection here, before Load().
            if (config.MergeConfig == null)
            {
                throw new InvalidOperationException("GameConfig has no MergeConfig. Run Tools > Merge Command > Build Scenes.");
            }
            var mergeConfig = config.MergeConfig.ToModel();
            foreach (var error in mergeConfig.Validate()) log.Error("MergeConfig: " + error);
            // Board placement only needs to be unpredictable, not replayable, so a time-based seed is fine.
            var board = new MergeBoardService(mergeConfig, bus, new DeterministicRandom(clock.UtcNow.Ticks), new UnlimitedWallet());
            services.Register(board);
            _save.Register(board);

            if (config.ShowDebugOverlay && (Application.isEditor || Debug.isDebugBuild))
            {
                gameObject.AddComponent<DebugOverlay>();
            }
            return services;
        }

        private void Update()
        {
            _save?.Tick();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) _save?.Flush();
        }

        private void OnApplicationFocus(bool focused)
        {
            // Some Android devices skip OnApplicationPause when the app is swiped away.
            if (!focused) _save?.Flush();
        }

        private void OnApplicationQuit()
        {
            _save?.Flush();
        }

        private void OnDestroy()
        {
            if (_save == null) return;
            _save.Flush();
            _save.Dispose();
            GameServices.SetCurrent(null);
        }
    }
}

using System;
using Game.Core.Audio;
using Game.Core.Events;
using Game.Core.Random;
using Game.Core.Save;
using Game.Core.Services;
using Game.Data;
using Game.Logic.Base;
using Game.Logic.Campaign;
using Game.Logic.Economy;
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
        private EconomyService _economy;
        private IClock _clock;

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
            // The offset lets debug tools simulate time away; it stays zero in normal play.
            var clock = new OffsetClock(new SystemClock());
            _clock = clock;

            services.Register(config);
            services.Register<IGameLog>(log);
            services.Register(bus);
            services.Register<IClock>(clock);
            services.Register(clock);
            services.Register<IAudioService>(new NullAudioService());

            var migrator = new SaveMigrator(SaveFormat.CurrentVersion);
            var storage = new FileSaveStorage(Application.persistentDataPath);
            _save = new SaveService(storage, migrator, bus, clock, log, config.CreateSaveSettings());
            services.Register(_save);

            // Every system with saved state registers its ISaveSection here, before Load().
            if (config.MergeConfig == null || config.EconomyConfig == null || config.BattleConfig == null || config.CampaignConfig == null)
            {
                throw new InvalidOperationException(
                    "GameConfig is missing a content asset (Merge, Economy, Battle or Campaign). Run Tools > Merge Command > Build Scenes.");
            }
            var mergeConfig = config.MergeConfig.ToModel();
            var economyConfig = config.EconomyConfig.ToEconomy();
            var hqConfig = config.EconomyConfig.ToHq();
            foreach (var error in mergeConfig.Validate()) log.Error("MergeConfig: " + error);
            foreach (var error in economyConfig.Validate()) log.Error("EconomyConfig: " + error);
            foreach (var error in hqConfig.Validate()) log.Error("EconomyConfig HQ: " + error);
            var battleConfig = config.BattleConfig.ToModel();
            var campaignConfig = config.CampaignConfig.ToModel();
            foreach (var error in battleConfig.Validate()) log.Error("BattleConfig: " + error);
            foreach (var error in campaignConfig.Validate(battleConfig)) log.Error("CampaignConfig: " + error);

            var wallet = new CurrencyWallet(bus, economyConfig.StartingGold, economyConfig.StartingGems);
            // Board placement only needs to be unpredictable, not replayable, so a time-based seed is fine.
            var board = new MergeBoardService(mergeConfig, bus, new DeterministicRandom(clock.UtcNow.Ticks), wallet);
            _economy = new EconomyService(economyConfig, wallet, board, bus);
            var hq = new HqService(hqConfig, wallet, board, bus);
            var progress = new CampaignProgress(campaignConfig, bus);
            var battles = new BattleService(battleConfig, campaignConfig, progress, board, wallet, bus);

            services.Register(wallet);
            services.Register(board);
            services.Register(_economy);
            services.Register(hq);
            services.Register(progress);
            services.Register(battles);
            _save.Register(wallet);
            _save.Register(board);
            _save.Register(_economy);
            _save.Register(hq);
            _save.Register(progress);

            if (config.ShowDebugOverlay && (Application.isEditor || Debug.isDebugBuild))
            {
                gameObject.AddComponent<DebugOverlay>();
            }
            return services;
        }

        private void Update()
        {
            // Pays mine income (or offline earnings after a long gap), then autosaves if anything changed.
            _economy?.Tick(_clock.UtcNow);
            _save?.Tick();
        }

        private void OnApplicationPause(bool paused)
        {
            // Ticking first records the exact moment the player left; resuming pays the time away.
            _economy?.Tick(_clock.UtcNow);
            if (paused) FlushNow();
        }

        private void OnApplicationFocus(bool focused)
        {
            // Some Android devices skip OnApplicationPause when the app is swiped away.
            if (!focused) FlushNow();
        }

        private void OnApplicationQuit() => FlushNow();

        private void FlushNow()
        {
            if (_save == null) return;
            _economy?.Tick(_clock.UtcNow);
            _save.MarkDirty(); // always store the latest "last active" time
            _save.Flush();
        }

        private void OnDestroy()
        {
            if (_save == null) return;
            FlushNow();
            _save.Dispose();
            GameServices.SetCurrent(null);
        }
    }
}

using System;
using Game.Core.Events;
using Game.Core.Json;
using Game.Core.Save;
using Game.Logic.Economy;
using Game.Logic.Merge;

namespace Game.Logic.Base
{
    /// <summary>The HQ reached a new level.</summary>
    public readonly struct HqUpgraded
    {
        /// <summary>Creates the event.</summary>
        public HqUpgraded(int level) { Level = level; }

        /// <summary>New HQ level.</summary>
        public int Level { get; }
    }

    /// <summary>Why an HQ upgrade failed.</summary>
    public enum HqUpgradeFailure
    {
        /// <summary>The HQ is already at its highest level.</summary>
        MaxLevel,

        /// <summary>Not enough gold.</summary>
        NotEnoughGold,
    }

    /// <summary>
    /// The HQ: an upgradable building outside the board whose level sets the board size and the highest buy level.
    /// Saves under the "hq" section and re-applies its effects after every load.
    /// </summary>
    public sealed class HqService : ISaveSection, IDisposable
    {
        /// <summary>Save section key.</summary>
        public const string SaveKey = "hq";

        private const string LevelKey = "level";

        private readonly HqConfig _config;
        private readonly CurrencyWallet _wallet;
        private readonly MergeBoardService _board;
        private readonly EventBus _bus;
        private readonly IDisposable _loadedSubscription;

        /// <summary>Creates the HQ at level 1 and applies its effects.</summary>
        public HqService(HqConfig config, CurrencyWallet wallet, MergeBoardService board, EventBus bus)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _board = board ?? throw new ArgumentNullException(nameof(board));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            Level = 1;
            // Sections restore in registration order; applying after the whole load avoids depending on that order.
            _loadedSubscription = _bus.Subscribe<GameLoaded>(_ => ApplyEffects());
            ApplyEffects();
        }

        /// <inheritdoc />
        public string Key => SaveKey;

        /// <summary>Current HQ level (1-based).</summary>
        public int Level { get; private set; }

        /// <summary>True when no further upgrade exists.</summary>
        public bool IsMaxLevel => Level >= _config.MaxLevel;

        /// <summary>Data for the current level.</summary>
        public HqLevel Current => _config.Get(Level);

        /// <summary>Data for the next level, or null at max.</summary>
        public HqLevel Next => IsMaxLevel ? null : _config.Get(Level + 1);

        /// <summary>Upgrades one level if affordable. Returns null on success, otherwise why it failed.</summary>
        public HqUpgradeFailure? TryUpgrade()
        {
            if (IsMaxLevel) return HqUpgradeFailure.MaxLevel;
            if (!_wallet.TrySpend(Currency.Gold, Next.UpgradeCost)) return HqUpgradeFailure.NotEnoughGold;
            Level++;
            ApplyEffects();
            _bus.Publish(new HqUpgraded(Level));
            _bus.Publish(new GameStateChanged("hq"));
            return null;
        }

        /// <inheritdoc />
        public JsonObject Capture() => new JsonObject().Set(LevelKey, Level);

        /// <inheritdoc />
        public void Restore(JsonObject data)
        {
            var level = data?.GetInt(LevelKey, 1) ?? 1;
            Level = Math.Max(1, Math.Min(_config.MaxLevel, level));
        }

        /// <summary>Unsubscribes from the event bus.</summary>
        public void Dispose() => _loadedSubscription.Dispose();

        private void ApplyEffects()
        {
            var current = Current;
            _board.BuyLevelCap = current.MaxBuyLevel;
            _board.Expand(current.BoardColumns, current.BoardRows);
            _bus.Publish(new BoardChanged()); // the buy cap changes prices shown on the buy buttons
        }
    }
}

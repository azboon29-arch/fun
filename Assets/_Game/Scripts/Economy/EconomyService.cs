using System;
using Game.Core.Events;
using Game.Core.Json;
using Game.Core.Save;
using Game.Logic.Merge;

namespace Game.Logic.Economy
{
    /// <summary>Gold earned while the player was away. Shown in the "Welcome back" popup.</summary>
    public readonly struct OfflineEarnings
    {
        /// <summary>Creates the event.</summary>
        public OfflineEarnings(long gold, double secondsAway, double secondsPaid)
        {
            Gold = gold;
            SecondsAway = secondsAway;
            SecondsPaid = secondsPaid;
        }

        /// <summary>Gold added to the wallet.</summary>
        public long Gold { get; }

        /// <summary>How long the player was away.</summary>
        public double SecondsAway { get; }

        /// <summary>How much of that was paid (capped by the Warehouse limit).</summary>
        public double SecondsPaid { get; }

        /// <summary>True when the away time was longer than the cap.</summary>
        public bool WasCapped => SecondsAway > SecondsPaid;
    }

    /// <summary>
    /// Runs the base economy: Gold Mine income while playing and while away (capped by Warehouses), and the Barracks
    /// discount on unit purchases. Driven by <see cref="Tick"/> with the current time, so a fake clock tests it fully.
    /// </summary>
    public sealed class EconomyService : IBuyCostModifier, ISaveSection
    {
        /// <summary>Save section key.</summary>
        public const string SaveKey = "economy";

        private const string LastActiveKey = "lastActive";
        private const string CarryKey = "carry";
        private const double SecondsPerHour = 3600d;

        private readonly EconomyConfig _config;
        private readonly CurrencyWallet _wallet;
        private readonly MergeBoardService _board;
        private readonly EventBus _bus;

        private DateTime? _lastActiveUtc;
        private double _carry; // fractional gold not yet paid, so slow incomes are never rounded away

        /// <summary>Creates the service and installs the Barracks discount on the board.</summary>
        public EconomyService(EconomyConfig config, CurrencyWallet wallet, MergeBoardService board, EventBus bus)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _board = board ?? throw new ArgumentNullException(nameof(board));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _board.CostModifier = this;
        }

        /// <inheritdoc />
        public string Key => SaveKey;

        /// <summary>The most recent offline report not yet shown by the UI, or null.</summary>
        public OfflineEarnings? PendingOfflineReport { get; private set; }

        /// <summary>Returns and clears <see cref="PendingOfflineReport"/>.</summary>
        public OfflineEarnings? TakeOfflineReport()
        {
            var report = PendingOfflineReport;
            PendingOfflineReport = null;
            return report;
        }

        /// <summary>Total gold per second from every Gold Mine on the board.</summary>
        public double IncomePerSecond
        {
            get
            {
                var total = 0d;
                foreach (var mine in _board.ItemsOfType(_config.GoldMineId)) total += _config.MineIncome(mine.Level);
                return total;
            }
        }

        /// <summary>Current unit discount from Barracks, as a fraction (0.1 = 10% off).</summary>
        public double UnitDiscount
        {
            get
            {
                var levels = 0;
                foreach (var barracks in _board.ItemsOfType(_config.BarracksId)) levels += barracks.Level;
                return Math.Min(_config.BarracksMaxDiscount, levels * _config.BarracksDiscountPerLevel);
            }
        }

        /// <summary>Current offline earnings cap in hours, including Warehouses.</summary>
        public double OfflineCapHours
        {
            get
            {
                var levels = 0;
                foreach (var warehouse in _board.ItemsOfType(_config.WarehouseId)) levels += warehouse.Level;
                return Math.Min(_config.OfflineMaxCapHours, _config.OfflineBaseCapHours + levels * _config.WarehouseHoursPerLevel);
            }
        }

        /// <inheritdoc />
        public double CostMultiplier(ItemDefinition item) => item.Kind == ItemKind.Unit ? 1d - UnitDiscount : 1d;

        /// <summary>
        /// Pays income for the time since the previous tick. A gap longer than the offline threshold (app closed or
        /// backgrounded) is paid at the offline rate, capped, and reported through <see cref="OfflineEarnings"/>.
        /// A clock that moved backwards pays nothing.
        /// </summary>
        public void Tick(DateTime utcNow)
        {
            if (!_lastActiveUtc.HasValue)
            {
                _lastActiveUtc = utcNow;
                return;
            }

            var elapsed = (utcNow - _lastActiveUtc.Value).TotalSeconds;
            _lastActiveUtc = utcNow;
            if (elapsed <= 0) return;

            if (elapsed > _config.OfflineThresholdSeconds) PayOffline(elapsed);
            else Pay(IncomePerSecond * elapsed);
        }

        /// <inheritdoc />
        public JsonObject Capture()
        {
            var data = new JsonObject().Set(CarryKey, _carry);
            if (_lastActiveUtc.HasValue) data.Set(LastActiveKey, _lastActiveUtc.Value.Ticks);
            return data;
        }

        /// <inheritdoc />
        public void Restore(JsonObject data)
        {
            PendingOfflineReport = null;
            var carry = data?.GetDouble(CarryKey) ?? 0;
            _carry = carry >= 0 && carry < 1 ? carry : 0;
            var ticks = data?.GetLong(LastActiveKey) ?? 0;
            _lastActiveUtc = ticks > 0 && ticks <= DateTime.MaxValue.Ticks ? new DateTime(ticks, DateTimeKind.Utc) : (DateTime?)null;
        }

        private void PayOffline(double secondsAway)
        {
            var paidSeconds = Math.Min(secondsAway, OfflineCapHours * SecondsPerHour);
            var gold = Pay(IncomePerSecond * paidSeconds * _config.OfflineIncomeMultiplier);
            if (gold <= 0) return;
            var report = new OfflineEarnings(gold, secondsAway, paidSeconds);
            PendingOfflineReport = report;
            _bus.Publish(report);
        }

        private long Pay(double amount)
        {
            if (amount <= 0 || double.IsNaN(amount)) return 0;
            var total = amount + _carry;
            if (total >= long.MaxValue)
            {
                _carry = 0;
                _wallet.Add(Currency.Gold, long.MaxValue);
                return long.MaxValue;
            }
            var whole = (long)Math.Floor(total);
            _carry = total - whole;
            _wallet.Add(Currency.Gold, whole);
            return whole;
        }
    }
}

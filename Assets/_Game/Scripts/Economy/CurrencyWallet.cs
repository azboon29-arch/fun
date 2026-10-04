using System;
using Game.Core.Events;
using Game.Core.Json;
using Game.Core.Save;
using Game.Logic.Merge;

namespace Game.Logic.Economy
{
    /// <summary>The player's currencies.</summary>
    public enum Currency
    {
        /// <summary>Main currency: earned from Gold Mines and battles, spent on items and upgrades.</summary>
        Gold,

        /// <summary>Premium currency, earned slowly through stars and achievements.</summary>
        Gems,
    }

    /// <summary>A currency balance changed.</summary>
    public readonly struct CurrencyChanged
    {
        /// <summary>Creates the event.</summary>
        public CurrencyChanged(Currency currency, long balance, long delta)
        {
            Currency = currency;
            Balance = balance;
            Delta = delta;
        }

        /// <summary>Which currency.</summary>
        public Currency Currency { get; }

        /// <summary>New balance.</summary>
        public long Balance { get; }

        /// <summary>Change (negative when spent).</summary>
        public long Delta { get; }
    }

    /// <summary>
    /// Holds Gold and Gems. Balances never go negative and saturate at <see cref="long.MaxValue"/>.
    /// Pays for board purchases (<see cref="IGoldWallet"/>) and saves under the "wallet" section.
    /// </summary>
    public sealed class CurrencyWallet : IGoldWallet, ISaveSection
    {
        /// <summary>Save section key.</summary>
        public const string SaveKey = "wallet";

        private const string GoldKey = "gold";
        private const string GemsKey = "gems";

        private readonly EventBus _bus;
        private readonly long _startingGold;
        private readonly long _startingGems;
        private long _gold;
        private long _gems;

        /// <summary>Creates a wallet holding the starting balances.</summary>
        public CurrencyWallet(EventBus bus, long startingGold, long startingGems)
        {
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _startingGold = Math.Max(0, startingGold);
            _startingGems = Math.Max(0, startingGems);
            _gold = _startingGold;
            _gems = _startingGems;
        }

        /// <inheritdoc />
        public string Key => SaveKey;

        /// <inheritdoc />
        public long Gold => _gold;

        /// <summary>Gems balance.</summary>
        public long Gems => _gems;

        /// <summary>Balance of <paramref name="currency"/>.</summary>
        public long Balance(Currency currency) => currency == Currency.Gold ? _gold : _gems;

        /// <summary>Adds <paramref name="amount"/> (must not be negative). Saturates instead of overflowing.</summary>
        public void Add(Currency currency, long amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), "Use TrySpend to remove currency.");
            if (amount == 0) return;
            var before = Balance(currency);
            var after = before > long.MaxValue - amount ? long.MaxValue : before + amount;
            Set(currency, after);
        }

        /// <inheritdoc />
        public bool TrySpend(long amount) => TrySpend(Currency.Gold, amount);

        /// <summary>Spends <paramref name="amount"/> if the balance covers it. Returns false and changes nothing otherwise.</summary>
        public bool TrySpend(Currency currency, long amount)
        {
            if (amount < 0) return false;
            var before = Balance(currency);
            if (amount > before) return false;
            if (amount > 0) Set(currency, before - amount);
            return true;
        }

        /// <inheritdoc />
        public JsonObject Capture() => new JsonObject().Set(GoldKey, _gold).Set(GemsKey, _gems);

        /// <inheritdoc />
        public void Restore(JsonObject data)
        {
            _gold = Math.Max(0, data?.GetLong(GoldKey, _startingGold) ?? _startingGold);
            _gems = Math.Max(0, data?.GetLong(GemsKey, _startingGems) ?? _startingGems);
            _bus.Publish(new CurrencyChanged(Currency.Gold, _gold, 0));
            _bus.Publish(new CurrencyChanged(Currency.Gems, _gems, 0));
        }

        private void Set(Currency currency, long value)
        {
            var before = Balance(currency);
            if (currency == Currency.Gold) _gold = value;
            else _gems = value;
            _bus.Publish(new CurrencyChanged(currency, value, value - before));
            _bus.Publish(new GameStateChanged("wallet"));
        }
    }
}

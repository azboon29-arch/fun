using System;
using System.Collections.Generic;
using Game.Core.Events;
using Game.Core.Json;
using Game.Logic.Economy;
using NUnit.Framework;

namespace Game.Tests
{
    public class CurrencyWalletTests
    {
        [Test]
        public void StartsWithStartingBalances()
        {
            var wallet = new CurrencyWallet(new EventBus(), 250, 3);
            Assert.AreEqual(250, wallet.Gold);
            Assert.AreEqual(3, wallet.Gems);
        }

        [Test]
        public void AddAndSpend_UpdateBalanceAndPublish()
        {
            var bus = new EventBus();
            var events = new List<CurrencyChanged>();
            var changes = 0;
            bus.Subscribe<CurrencyChanged>(events.Add);
            bus.Subscribe<GameStateChanged>(_ => changes++);
            var wallet = new CurrencyWallet(bus, 0, 0);

            wallet.Add(Currency.Gold, 100);
            Assert.IsTrue(wallet.TrySpend(30));
            Assert.IsTrue(wallet.TrySpend(Currency.Gold, 0));

            Assert.AreEqual(70, wallet.Gold);
            Assert.AreEqual(2, events.Count);
            Assert.AreEqual(-30, events[1].Delta);
            Assert.AreEqual(70, events[1].Balance);
            Assert.AreEqual(2, changes);
        }

        [Test]
        public void Spend_MoreThanBalanceOrNegative_FailsAndChangesNothing()
        {
            var wallet = new CurrencyWallet(new EventBus(), 50, 1);

            Assert.IsFalse(wallet.TrySpend(51));
            Assert.IsFalse(wallet.TrySpend(-1));
            Assert.IsFalse(wallet.TrySpend(Currency.Gems, 2));
            Assert.AreEqual(50, wallet.Gold);
            Assert.AreEqual(1, wallet.Gems);
        }

        [Test]
        public void Add_SaturatesInsteadOfOverflowing_AndRejectsNegative()
        {
            var wallet = new CurrencyWallet(new EventBus(), long.MaxValue - 5, 0);

            wallet.Add(Currency.Gold, 100);

            Assert.AreEqual(long.MaxValue, wallet.Gold);
            Assert.Throws<ArgumentOutOfRangeException>(() => wallet.Add(Currency.Gold, -1));
        }

        [Test]
        public void CaptureRestore_RoundTrips_NullGivesStartingBalances()
        {
            var wallet = new CurrencyWallet(new EventBus(), 10, 2);
            wallet.Add(Currency.Gold, 1234567890123);
            wallet.Add(Currency.Gems, 7);

            var copy = new CurrencyWallet(new EventBus(), 10, 2);
            copy.Restore((JsonObject)JsonNode.Parse(wallet.Capture().ToJson()));
            Assert.AreEqual(wallet.Gold, copy.Gold);
            Assert.AreEqual(9, copy.Gems);

            copy.Restore(null);
            Assert.AreEqual(10, copy.Gold);
            Assert.AreEqual(2, copy.Gems);

            copy.Restore((JsonObject)JsonNode.Parse("{\"gold\":-5}"));
            Assert.AreEqual(0, copy.Gold, "a damaged negative balance is clamped");
        }
    }
}

using System;
using Game.Core.Json;
using Game.Logic.Economy;
using Game.Logic.Merge;
using NUnit.Framework;

namespace Game.Tests
{
    public class EconomyServiceTests
    {
        [Test]
        public void Income_SumsAllMinesByLevel()
        {
            var rig = new EconomyRig();
            Assert.AreEqual(0, rig.Economy.IncomePerSecond);

            rig.Put(0, 0, "goldmine", 1); // 1/s
            rig.Put(1, 0, "goldmine", 3); // 4/s
            rig.Put(2, 0, "infantry", 5); // not a mine

            Assert.AreEqual(5, rig.Economy.IncomePerSecond);
        }

        [Test]
        public void Online_GoldAccumulatesExactlyOverManySmallTicks()
        {
            var rig = new EconomyRig();
            rig.Put(0, 0, "goldmine", 1); // 1 gold per second
            rig.Economy.Tick(rig.Now);

            rig.Play(seconds: 10, step: 0.016); // about 60 fps

            // Fractional gold carries between ticks, so frame-sized steps add up instead of rounding to zero.
            Assert.That(rig.Wallet.Gold, Is.InRange(9, 10));
            Assert.IsEmpty(rig.OfflineEvents, "normal play is not reported as time away");
        }

        [Test]
        public void Online_IncomeFollowsMergedMines()
        {
            var rig = new EconomyRig();
            rig.Put(0, 0, "goldmine", 1);
            rig.Put(1, 0, "goldmine", 1);
            rig.Economy.Tick(rig.Now);
            rig.Play(10, 1);
            Assert.AreEqual(20, rig.Wallet.Gold);

            rig.Board.Drag(new Cell(0, 0), new Cell(1, 0)); // two L1 (1/s each) -> one L2 (2/s)
            rig.Play(10, 1);
            Assert.AreEqual(40, rig.Wallet.Gold);
        }

        [Test]
        public void FirstTick_PaysNothing()
        {
            var rig = new EconomyRig();
            rig.Put(0, 0, "goldmine", 5);
            rig.Economy.Tick(rig.Now);
            Assert.AreEqual(0, rig.Wallet.Gold);
        }

        [Test]
        public void Offline_PaysForTimeAwayAndReportsIt()
        {
            var rig = new EconomyRig();
            rig.Put(0, 0, "goldmine", 2); // 2/s
            rig.Economy.Tick(rig.Now);

            rig.Advance(TimeSpan.FromHours(1).TotalSeconds);

            Assert.AreEqual(7200, rig.Wallet.Gold);
            Assert.AreEqual(1, rig.OfflineEvents.Count);
            Assert.AreEqual(7200, rig.OfflineEvents[0].Gold);
            Assert.IsFalse(rig.OfflineEvents[0].WasCapped);
            Assert.AreEqual(7200, rig.Economy.TakeOfflineReport().Value.Gold);
            Assert.IsNull(rig.Economy.TakeOfflineReport(), "the report is shown once");
        }

        [Test]
        public void Offline_IsCappedAtEightHoursByDefault()
        {
            var rig = new EconomyRig();
            rig.Put(0, 0, "goldmine", 1);
            rig.Economy.Tick(rig.Now);

            rig.Advance(TimeSpan.FromDays(3).TotalSeconds);

            Assert.AreEqual(8 * 3600, rig.Wallet.Gold);
            Assert.IsTrue(rig.OfflineEvents[0].WasCapped);
        }

        [Test]
        public void Warehouses_RaiseTheOfflineCapUpToTheMax()
        {
            var rig = new EconomyRig();
            rig.Put(0, 0, "goldmine", 1);
            rig.Put(1, 0, "warehouse", 2);
            rig.Put(2, 0, "warehouse", 1);
            Assert.AreEqual(11, rig.Economy.OfflineCapHours);

            rig.Economy.Tick(rig.Now);
            rig.Advance(TimeSpan.FromDays(1).TotalSeconds);
            Assert.AreEqual(11 * 3600, rig.Wallet.Gold);

            rig.Put(3, 0, "warehouse", 10);
            Assert.AreEqual(12, rig.Economy.OfflineCapHours, "capped at the configured max");
        }

        [Test]
        public void OfflineMultiplier_ScalesEarningsAway()
        {
            var rig = new EconomyRig();
            rig.Config.OfflineIncomeMultiplier = 0.5;
            rig.Put(0, 0, "goldmine", 1);
            rig.Economy.Tick(rig.Now);

            rig.Advance(1000);

            Assert.AreEqual(500, rig.Wallet.Gold);
        }

        [Test]
        public void ClockGoingBackwards_PaysNothing()
        {
            var rig = new EconomyRig();
            rig.Put(0, 0, "goldmine", 1);
            rig.Economy.Tick(rig.Now);

            rig.Advance(-TimeSpan.FromHours(5).TotalSeconds);
            rig.Advance(30);

            Assert.AreEqual(30, rig.Wallet.Gold);
            Assert.IsEmpty(rig.OfflineEvents);
        }

        [Test]
        public void NoMines_NoOfflineReport()
        {
            var rig = new EconomyRig();
            rig.Economy.Tick(rig.Now);
            rig.Advance(TimeSpan.FromHours(2).TotalSeconds);
            Assert.IsEmpty(rig.OfflineEvents);
            Assert.IsNull(rig.Economy.PendingOfflineReport);
        }

        [Test]
        public void Barracks_DiscountUnitsOnly_Capped()
        {
            var rig = new EconomyRig();
            Assert.AreEqual(100, rig.Board.BuyCost("infantry"));

            rig.Put(0, 0, "barracks", 2); // 2 levels x 10% = 20% off units
            Assert.AreEqual(0.2, rig.Economy.UnitDiscount, 1e-9);
            Assert.AreEqual(80, rig.Board.BuyCost("infantry"));
            Assert.AreEqual(100, rig.Board.BuyCost("goldmine"), "buildings are not discounted");

            rig.Put(1, 0, "barracks", 9);
            Assert.AreEqual(50, rig.Board.BuyCost("infantry"), "capped at 50%");
        }

        [Test]
        public void Buy_ChargesTheDiscountedPrice()
        {
            var rig = new EconomyRig(startingGold: 1000);
            rig.Put(0, 0, "barracks", 2);

            rig.Board.Buy("infantry");

            Assert.AreEqual(920, rig.Wallet.Gold);
        }

        [Test]
        public void SaveRoundTrip_PaysOfflineTimeAfterReload()
        {
            var rig = new EconomyRig();
            var save = new SaveRig(bus: rig.Bus);
            save.Service.Register(rig.Wallet);
            save.Service.Register(rig.Board);
            save.Service.Register(rig.Economy);
            save.Service.Register(rig.Hq);
            save.Service.Load();
            rig.Put(0, 0, "goldmine", 1);
            rig.Economy.Tick(rig.Now);
            rig.Advance(30);
            save.Service.Save();

            // Reopen the game two hours later.
            var later = new EconomyRig();
            var reload = new SaveRig(save.Storage, bus: later.Bus);
            reload.Service.Register(later.Wallet);
            reload.Service.Register(later.Board);
            reload.Service.Register(later.Economy);
            reload.Service.Register(later.Hq);
            reload.Service.Load();
            later.Now = rig.Now.AddHours(2);
            later.Economy.Tick(later.Now);

            Assert.AreEqual(30 + 7200, later.Wallet.Gold);
            Assert.AreEqual(7200, later.Economy.PendingOfflineReport.Value.Gold);
        }

        [Test]
        public void Restore_IgnoresDamagedValues()
        {
            var rig = new EconomyRig();
            rig.Economy.Restore((JsonObject)JsonNode.Parse("{\"lastActive\":-4,\"carry\":7}"));
            rig.Put(0, 0, "goldmine", 1);

            rig.Economy.Tick(rig.Now); // no valid last-active time: starts fresh, pays nothing
            rig.Advance(1);

            Assert.AreEqual(1, rig.Wallet.Gold, "carry is clamped below one gold");
        }

        [Test]
        public void Config_Validates()
        {
            CollectionAssert.IsEmpty(EconomyRig.CreateConfig().Validate());
            var bad = new EconomyConfig { MineIncomeGrowthPerLevel = 0.5, BarracksMaxDiscount = 1, OfflineBaseCapHours = 8, OfflineMaxCapHours = 4 };
            var errors = bad.Validate();
            Assert.That(errors, Has.Some.Contains("Gold Mine item id"));
            Assert.That(errors, Has.Some.Contains("income growth"));
            Assert.That(errors, Has.Some.Contains("max discount"));
            Assert.That(errors, Has.Some.Contains("max cap"));
            Assert.That(errors, Has.Some.Contains("threshold"));
        }
    }
}

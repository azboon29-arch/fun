using Game.Core.Events;
using Game.Core.Json;
using Game.Core.Save;
using Game.Logic.Base;
using Game.Logic.Merge;
using NUnit.Framework;

namespace Game.Tests
{
    public class HqServiceTests
    {
        [Test]
        public void StartsAtLevelOne_WithItsBoardAndBuyCap()
        {
            var rig = new EconomyRig();
            Assert.AreEqual(1, rig.Hq.Level);
            Assert.AreEqual(4, rig.Board.Board.Columns);
            Assert.AreEqual(5, rig.Board.Board.Rows);
            Assert.AreEqual(3, rig.Board.BuyLevelCap);
        }

        [Test]
        public void Upgrade_SpendsGoldAndAppliesNextLevel()
        {
            var rig = new EconomyRig(startingGold: 1500);
            var upgraded = 0;
            rig.Bus.Subscribe<HqUpgraded>(e => upgraded = e.Level);

            Assert.IsNull(rig.Hq.TryUpgrade());

            Assert.AreEqual(2, rig.Hq.Level);
            Assert.AreEqual(500, rig.Wallet.Gold);
            Assert.AreEqual(5, rig.Board.Board.Columns);
            Assert.AreEqual(5, rig.Board.BuyLevelCap);
            Assert.AreEqual(2, upgraded);
        }

        [Test]
        public void Upgrade_WithoutEnoughGold_FailsAndChangesNothing()
        {
            var rig = new EconomyRig(startingGold: 999);

            Assert.AreEqual(HqUpgradeFailure.NotEnoughGold, rig.Hq.TryUpgrade());
            Assert.AreEqual(1, rig.Hq.Level);
            Assert.AreEqual(999, rig.Wallet.Gold);
            Assert.AreEqual(4, rig.Board.Board.Columns);
        }

        [Test]
        public void Upgrade_AtMaxLevel_Fails()
        {
            var rig = new EconomyRig(startingGold: 100000);
            rig.Hq.TryUpgrade();
            rig.Hq.TryUpgrade();

            Assert.IsTrue(rig.Hq.IsMaxLevel);
            Assert.IsNull(rig.Hq.Next);
            Assert.AreEqual(HqUpgradeFailure.MaxLevel, rig.Hq.TryUpgrade());
            Assert.AreEqual(6, rig.Board.Board.Columns);
            Assert.AreEqual(7, rig.Board.Board.Rows);
            CollectionAssert.AreEqual(new[] { "shield" }, rig.Hq.Current.UnlockedTraits);
        }

        [Test]
        public void BuyCap_LimitsPurchaseLevel()
        {
            var rig = new EconomyRig();
            rig.Put(0, 0, "infantry", 19);
            rig.Put(1, 0, "infantry", 19);
            rig.Board.Drag(new Cell(0, 0), new Cell(1, 0)); // highest 20, offset 2 -> would buy L18

            Assert.AreEqual(3, rig.Board.BuyLevel("infantry"), "HQ level 1 caps purchases at L3");
        }

        [Test]
        public void Load_ReappliesHqBoardSizeAfterBoardRestore()
        {
            var rig = new EconomyRig(startingGold: 1000);
            var save = new SaveRig(bus: rig.Bus);
            save.Service.Register(rig.Hq); // registered before the board on purpose
            save.Service.Register(rig.Board);
            save.Service.Register(rig.Wallet);
            save.Service.Load();
            rig.Hq.TryUpgrade();
            save.Service.Save();

            var later = new EconomyRig();
            var reload = new SaveRig(save.Storage, bus: later.Bus);
            reload.Service.Register(later.Hq);
            reload.Service.Register(later.Board);
            reload.Service.Register(later.Wallet);
            reload.Service.Load();

            Assert.AreEqual(2, later.Hq.Level);
            Assert.AreEqual(5, later.Board.Board.Columns);
            Assert.AreEqual(5, later.Board.BuyLevelCap);
        }

        [Test]
        public void Restore_ClampsDamagedLevel()
        {
            var rig = new EconomyRig();
            rig.Hq.Restore((JsonObject)JsonNode.Parse("{\"level\":99}"));
            Assert.AreEqual(3, rig.Hq.Level);
            rig.Hq.Restore((JsonObject)JsonNode.Parse("{\"level\":-2}"));
            Assert.AreEqual(1, rig.Hq.Level);
        }

        [Test]
        public void Config_Validates()
        {
            CollectionAssert.IsEmpty(EconomyRig.CreateHqConfig().Validate());
            var bad = new HqConfig(new[] { new HqLevel(0, 5, 5, 5), new HqLevel(-1, 4, 5, 2) });
            var errors = bad.Validate();
            Assert.That(errors, Has.Some.Contains("shrink"));
            Assert.That(errors, Has.Some.Contains("must not drop"));
            Assert.That(errors, Has.Some.Contains("negative"));
        }
    }
}

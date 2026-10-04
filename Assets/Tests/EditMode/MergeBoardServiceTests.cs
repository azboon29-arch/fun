using System.Linq;
using Game.Core.Json;
using Game.Core.Random;
using Game.Logic.Merge;
using NUnit.Framework;

namespace Game.Tests
{
    public class MergeBoardServiceTests
    {
        [Test]
        public void NewBoard_UsesStartSize()
        {
            var rig = new MergeRig();
            Assert.AreEqual(4, rig.Board.Columns);
            Assert.AreEqual(5, rig.Board.Rows);
        }

        [Test]
        public void Buy_PlacesLevelOneItemAndCharges()
        {
            var wallet = new TestWallet(1000);
            var rig = new MergeRig(wallet);

            var cell = rig.Service.Buy("infantry");

            Assert.IsTrue(cell.HasValue);
            Assert.AreEqual(new BoardItem("infantry", 1), rig.Board.Get(cell.Value));
            Assert.AreEqual(900, wallet.Gold);
            Assert.AreEqual(1, rig.Service.PurchaseCount("infantry"));
            Assert.IsInstanceOf<ItemBought>(rig.Events.Last());
            Assert.AreEqual(1, rig.StateChanges);
        }

        [Test]
        public void Buy_CostRisesWithEachPurchasePerType()
        {
            var rig = new MergeRig();

            Assert.AreEqual(100, rig.Service.BuyCost("infantry"));
            rig.Service.Buy("infantry");
            Assert.AreEqual(150, rig.Service.BuyCost("infantry"));
            rig.Service.Buy("infantry");
            Assert.AreEqual(225, rig.Service.BuyCost("infantry"));
            Assert.AreEqual(200, rig.Service.BuyCost("armor"), "other types keep their own count");
        }

        [Test]
        public void Buy_OnFullBoard_FailsWithoutChargingOrChangingBoard()
        {
            var wallet = new TestWallet(1000);
            var rig = new MergeRig(wallet);
            rig.Fill();
            var before = rig.Board.Items();

            Assert.IsNull(rig.Service.Buy("infantry"));

            Assert.AreEqual(1000, wallet.Gold);
            Assert.AreEqual(0, rig.Service.PurchaseCount("infantry"));
            CollectionAssert.AreEqual(before, rig.Board.Items());
            var failed = (BuyFailed)rig.Events.Last();
            Assert.AreEqual(BuyFailure.BoardFull, failed.Reason);
            Assert.AreEqual(0, rig.StateChanges);
        }

        [Test]
        public void Buy_WithoutEnoughGold_Fails()
        {
            var wallet = new TestWallet(99);
            var rig = new MergeRig(wallet);

            Assert.IsNull(rig.Service.Buy("infantry"));
            Assert.AreEqual(99, wallet.Gold);
            Assert.AreEqual(0, rig.Board.ItemCount);
            Assert.AreEqual(BuyFailure.NotEnoughGold, ((BuyFailed)rig.Events.Last()).Reason);
        }

        [Test]
        public void Buy_UnknownType_Fails()
        {
            var rig = new MergeRig();
            Assert.IsNull(rig.Service.Buy("dragon"));
            Assert.AreEqual(BuyFailure.UnknownType, ((BuyFailed)rig.Events.Last()).Reason);
        }

        [Test]
        public void Buy_FillsEveryEmptySlotThenReportsFull()
        {
            var rig = new MergeRig(columns: 2, rows: 2);
            for (var i = 0; i < 4; i++) Assert.IsTrue(rig.Service.Buy("armor").HasValue);
            Assert.IsTrue(rig.Board.IsFull);
            Assert.IsNull(rig.Service.Buy("armor"));
        }

        [Test]
        public void Buy_IsDeterministicForTheSameSeed()
        {
            var a = new MergeRig(seed: 7);
            var b = new MergeRig(seed: 7);
            for (var i = 0; i < 10; i++)
            {
                Assert.AreEqual(a.Service.Buy("infantry"), b.Service.Buy("infantry"));
            }
        }

        [Test]
        public void BuyLevel_FollowsHighestLevelMinusOffset()
        {
            var rig = new MergeRig(buyOffset: 2);
            Assert.AreEqual(1, rig.Service.BuyLevel("infantry"));

            rig.Put(0, 0, "infantry", 3);
            rig.Put(1, 0, "infantry", 3);
            rig.Service.Drag(new Cell(0, 0), new Cell(1, 0));

            Assert.AreEqual(4, rig.Service.HighestLevel("infantry"));
            Assert.AreEqual(2, rig.Service.BuyLevel("infantry"));
            Assert.AreEqual(1, rig.Service.BuyLevel("armor"));
            Assert.AreEqual(200, rig.Service.BuyCost("infantry"), "level 2 costs one level multiplier more");

            var cell = rig.Service.Buy("infantry");
            Assert.AreEqual(2, rig.Board.Get(cell.Value).Value.Level);
        }

        [Test]
        public void Drag_Merge_PublishesEventAndMarksStateChanged()
        {
            var rig = new MergeRig();
            rig.Put(0, 0, "infantry", 1);
            rig.Put(1, 0, "infantry", 1);

            Assert.AreEqual(DragOutcome.Merged, rig.Service.Drag(new Cell(0, 0), new Cell(1, 0)));

            var merged = (ItemMerged)rig.Events.Last();
            Assert.AreEqual(new Cell(1, 0), merged.To);
            Assert.AreEqual(new BoardItem("infantry", 2), merged.Result);
            Assert.AreEqual(1, rig.StateChanges);
        }

        [Test]
        public void Drag_WithNoChange_DoesNotMarkStateChanged()
        {
            var rig = new MergeRig();
            rig.Put(0, 0, "infantry", MergeRig.MaxLevel);
            rig.Put(1, 0, "infantry", MergeRig.MaxLevel);

            Assert.AreEqual(DragOutcome.MaxLevel, rig.Service.Drag(new Cell(0, 0), new Cell(1, 0)));
            Assert.AreEqual(DragOutcome.Invalid, rig.Service.Drag(new Cell(3, 3), new Cell(1, 0)));
            Assert.AreEqual(0, rig.StateChanges);
            Assert.AreEqual(DragOutcome.MaxLevel, ((ItemDragged)rig.Events[0]).Outcome);
        }

        [Test]
        public void Expand_ClampsToMaxAndNeverShrinks()
        {
            var rig = new MergeRig();
            Assert.IsTrue(rig.Service.Expand(5, 6));
            Assert.IsTrue(rig.Service.Expand(99, 99));
            Assert.AreEqual(6, rig.Board.Columns);
            Assert.AreEqual(7, rig.Board.Rows);
            Assert.IsFalse(rig.Service.Expand(1, 1));
            Assert.AreEqual(6, rig.Board.Columns);
        }

        [Test]
        public void CaptureRestore_RoundTripsBoardAndProgress()
        {
            var rig = new MergeRig();
            rig.Service.Expand(5, 6);
            rig.Put(4, 5, "armor", 4);
            rig.Put(0, 0, "mine", 2);
            rig.Service.Buy("infantry");
            rig.Service.Buy("infantry");
            rig.Put(2, 2, "infantry", 3);
            rig.Put(3, 3, "infantry", 3);
            rig.Service.Drag(new Cell(2, 2), new Cell(3, 3));

            var json = rig.Service.Capture().ToJson();
            var copy = new MergeRig();
            copy.Service.Restore((JsonObject)JsonNode.Parse(json));

            Assert.AreEqual(5, copy.Board.Columns);
            Assert.AreEqual(6, copy.Board.Rows);
            CollectionAssert.AreEqual(rig.Board.Items(), copy.Board.Items());
            Assert.AreEqual(2, copy.Service.PurchaseCount("infantry"));
            Assert.AreEqual(4, copy.Service.HighestLevel("infantry"));
            Assert.AreEqual(rig.Service.BuyCost("infantry"), copy.Service.BuyCost("infantry"));
        }

        [Test]
        public void Restore_Null_ResetsToEmptyStartBoard()
        {
            var rig = new MergeRig();
            rig.Service.Expand(6, 7);
            rig.Service.Buy("armor");

            rig.Service.Restore(null);

            Assert.AreEqual(4, rig.Board.Columns);
            Assert.AreEqual(0, rig.Board.ItemCount);
            Assert.AreEqual(0, rig.Service.PurchaseCount("armor"));
        }

        [Test]
        public void Restore_DamagedData_KeepsEveryValidItem()
        {
            var rig = new MergeRig(columns: 2, rows: 1);
            var data = (JsonObject)JsonNode.Parse(
                "{\"columns\":2,\"rows\":1,\"items\":[" +
                "{\"c\":0,\"r\":0,\"type\":\"armor\",\"level\":2}," +
                "{\"c\":0,\"r\":0,\"type\":\"infantry\",\"level\":1}," +   // same cell twice
                "{\"c\":1,\"r\":0,\"type\":\"dragon\",\"level\":1}," +     // unknown type
                "{\"c\":5,\"r\":3,\"type\":\"mine\",\"level\":99}," +      // outside the stored size, above max level
                "\"junk\"]}");

            rig.Service.Restore(data);

            var items = rig.Board.Items().Select(p => p.Value).ToList();
            CollectionAssert.AreEquivalent(new[]
            {
                new BoardItem("armor", 2), new BoardItem("infantry", 1), new BoardItem("mine", 3),
            }, items);
            Assert.AreEqual(new BoardItem("mine", 3), rig.At(5, 3));
        }

        [Test]
        public void SaveService_RoundTripsBoardThroughSaveFile()
        {
            var save = new SaveRig();
            var board = new MergeBoardService(MergeRig.CreateConfig(), save.Bus, new DeterministicRandom(3), new UnlimitedWallet());
            save.Service.Register(board);
            save.Service.Load();
            board.Buy("infantry");
            board.Buy("mine");
            Assert.IsTrue(save.Service.IsDirty, "buying should schedule an autosave");
            save.Service.Flush();

            var reloadSave = new SaveRig(save.Storage);
            var reloaded = new MergeBoardService(MergeRig.CreateConfig(), reloadSave.Bus, new DeterministicRandom(3), new UnlimitedWallet());
            reloadSave.Service.Register(reloaded);
            reloadSave.Service.Load();

            CollectionAssert.AreEqual(board.Board.Items(), reloaded.Board.Items());
        }
    }
}

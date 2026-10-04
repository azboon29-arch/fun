using System;
using Game.Core.Random;
using Game.Logic.Merge;
using NUnit.Framework;

namespace Game.Tests
{
    public class BoardModelTests
    {
        private static int Max(string _) => MergeRig.MaxLevel;

        private static BoardModel Board(int columns = 4, int rows = 5) => new BoardModel(columns, rows);

        private static readonly Cell A = new Cell(0, 0);
        private static readonly Cell B = new Cell(1, 0);

        [Test]
        public void NewBoard_IsEmptyWithRequestedSize()
        {
            var board = Board();
            Assert.AreEqual(4, board.Columns);
            Assert.AreEqual(5, board.Rows);
            Assert.AreEqual(20, board.SlotCount);
            Assert.AreEqual(0, board.ItemCount);
            Assert.AreEqual(20, board.EmptyCells().Count);
        }

        [Test]
        public void Drag_ToEmptySlot_Moves()
        {
            var board = Board();
            board.Set(A, new BoardItem("infantry", 2));

            Assert.AreEqual(DragOutcome.Moved, board.Drag(A, B, Max));
            Assert.IsNull(board.Get(A));
            Assert.AreEqual(new BoardItem("infantry", 2), board.Get(B));
        }

        [Test]
        public void Drag_OntoIdenticalItem_MergesAtTarget()
        {
            var board = Board();
            board.Set(A, new BoardItem("infantry", 2));
            board.Set(B, new BoardItem("infantry", 2));

            Assert.AreEqual(DragOutcome.Merged, board.Drag(A, B, Max));
            Assert.IsNull(board.Get(A));
            Assert.AreEqual(new BoardItem("infantry", 3), board.Get(B));
            Assert.AreEqual(1, board.ItemCount);
        }

        [Test]
        public void Drag_OntoSameTypeDifferentLevel_Swaps()
        {
            var board = Board();
            board.Set(A, new BoardItem("infantry", 1));
            board.Set(B, new BoardItem("infantry", 2));

            Assert.AreEqual(DragOutcome.Swapped, board.Drag(A, B, Max));
            Assert.AreEqual(new BoardItem("infantry", 2), board.Get(A));
            Assert.AreEqual(new BoardItem("infantry", 1), board.Get(B));
        }

        [Test]
        public void Drag_OntoDifferentTypeSameLevel_Swaps()
        {
            var board = Board();
            board.Set(A, new BoardItem("infantry", 3));
            board.Set(B, new BoardItem("armor", 3));

            Assert.AreEqual(DragOutcome.Swapped, board.Drag(A, B, Max));
            Assert.AreEqual(new BoardItem("armor", 3), board.Get(A));
            Assert.AreEqual(new BoardItem("infantry", 3), board.Get(B));
        }

        [Test]
        public void Drag_IdenticalItemsAtMaxLevel_ChangesNothing()
        {
            var board = Board();
            board.Set(A, new BoardItem("infantry", MergeRig.MaxLevel));
            board.Set(B, new BoardItem("infantry", MergeRig.MaxLevel));

            Assert.AreEqual(DragOutcome.MaxLevel, board.Drag(A, B, Max));
            Assert.AreEqual(new BoardItem("infantry", MergeRig.MaxLevel), board.Get(A));
            Assert.AreEqual(new BoardItem("infantry", MergeRig.MaxLevel), board.Get(B));
        }

        [Test]
        public void Drag_UpToMaxLevel_IsAllowed()
        {
            var board = Board();
            board.Set(A, new BoardItem("infantry", MergeRig.MaxLevel - 1));
            board.Set(B, new BoardItem("infantry", MergeRig.MaxLevel - 1));

            Assert.AreEqual(DragOutcome.Merged, board.Drag(A, B, Max));
            Assert.AreEqual(MergeRig.MaxLevel, board.Get(B).Value.Level);
        }

        [Test]
        public void Drag_UsesPerTypeMaxLevel()
        {
            var board = Board();
            board.Set(A, new BoardItem("mine", 3));
            board.Set(B, new BoardItem("mine", 3));

            Assert.AreEqual(DragOutcome.MaxLevel, board.Drag(A, B, id => id == "mine" ? 3 : 30));
        }

        [Test]
        public void Drag_FromEmptyOrOffBoard_IsInvalid()
        {
            var board = Board();
            board.Set(B, new BoardItem("infantry", 1));

            Assert.AreEqual(DragOutcome.Invalid, board.Drag(A, B, Max));
            Assert.AreEqual(DragOutcome.Invalid, board.Drag(B, new Cell(4, 0), Max));
            Assert.AreEqual(DragOutcome.Invalid, board.Drag(B, new Cell(-1, 0), Max));
            Assert.AreEqual(DragOutcome.Invalid, board.Drag(new Cell(0, 5), B, Max));
            Assert.AreEqual(new BoardItem("infantry", 1), board.Get(B));
        }

        [Test]
        public void Drag_OntoItself_DoesNothing()
        {
            var board = Board();
            board.Set(A, new BoardItem("infantry", 1));

            Assert.AreEqual(DragOutcome.None, board.Drag(A, A, Max));
            Assert.AreEqual(new BoardItem("infantry", 1), board.Get(A));
        }

        [Test]
        public void Drag_NeverChangesItemCountExceptMerge()
        {
            var board = Board(2, 1);
            board.Set(A, new BoardItem("infantry", 1));
            board.Set(B, new BoardItem("armor", 1));

            for (var i = 0; i < 5; i++) board.Drag(A, B, Max);

            Assert.AreEqual(2, board.ItemCount);
        }

        [Test]
        public void RandomEmptyCell_ReturnsOnlyEmptyCells_AndNullWhenFull()
        {
            var board = Board(2, 2);
            var random = new DeterministicRandom(1);
            board.Set(A, new BoardItem("infantry", 1));

            for (var i = 0; i < 50; i++)
            {
                var cell = board.RandomEmptyCell(random);
                Assert.IsTrue(cell.HasValue);
                Assert.IsTrue(board.IsEmpty(cell.Value));
            }

            foreach (var cell in board.EmptyCells()) board.Set(cell, new BoardItem("armor", 1));
            Assert.IsTrue(board.IsFull);
            Assert.IsNull(board.RandomEmptyCell(random));
        }

        [Test]
        public void Resize_GrowsAndKeepsItems_RefusesShrink()
        {
            var board = Board();
            var corner = new Cell(3, 4);
            board.Set(corner, new BoardItem("armor", 4));

            board.Resize(6, 7);

            Assert.AreEqual(6, board.Columns);
            Assert.AreEqual(7, board.Rows);
            Assert.AreEqual(new BoardItem("armor", 4), board.Get(corner));
            Assert.Throws<ArgumentOutOfRangeException>(() => board.Resize(5, 7));
        }

        [Test]
        public void Set_OffBoard_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Board().Set(new Cell(9, 9), new BoardItem("armor", 1)));
        }

        [Test]
        public void BoardItem_RejectsInvalidValues()
        {
            Assert.Throws<ArgumentException>(() => new BoardItem("", 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BoardItem("armor", 0));
        }
    }
}

using Game.Logic.Battle;
using Game.Logic.Merge;
using NUnit.Framework;

namespace Game.Tests
{
    public class FormationTests
    {
        [Test]
        public void Place_RespectsDeploymentLimit_ButAllowsReplacingASlot()
        {
            var f = new Formation();
            Assert.That(f.Place(new FormationEntry(FormationRow.Front, 0, "infantry", 1), 2), Is.True);
            Assert.That(f.Place(new FormationEntry(FormationRow.Front, 1, "infantry", 1), 2), Is.True);
            Assert.That(f.Place(new FormationEntry(FormationRow.Back, 1, "infantry", 1), 2), Is.False);
            Assert.That(f.Place(new FormationEntry(FormationRow.Front, 1, "armor", 3), 2), Is.True, "replacing does not add a unit");
            Assert.That(f.Count, Is.EqualTo(2));
            Assert.That(f.Get(FormationRow.Front, 1)?.UnitId, Is.EqualTo("armor"));
        }

        [Test]
        public void Place_RejectsSlotsOutsideTheGrid()
        {
            var f = new Formation();
            Assert.That(f.Place(new FormationEntry(FormationRow.Front, Formation.ColumnCount, "infantry", 1), 10), Is.False);
            Assert.That(f.Place(new FormationEntry((FormationRow)Formation.RowCount, 0, "infantry", 1), 10), Is.False);
            Assert.That(f.Place(new FormationEntry(FormationRow.Front, -1, "infantry", 1), 10), Is.False);
        }

        [Test]
        public void Place_SameBoardItemTwice_MovesIt()
        {
            var f = new Formation();
            var cell = new Cell(1, 1);
            f.Place(new FormationEntry(FormationRow.Front, 0, "infantry", 2, cell), 1);
            Assert.That(f.Place(new FormationEntry(FormationRow.Back, 4, "infantry", 2, cell), 1), Is.True, "a move is allowed at the limit");
            Assert.That(f.Count, Is.EqualTo(1));
            Assert.That(f.Get(FormationRow.Front, 0), Is.Null);
            Assert.That(f.Uses(cell), Is.True);
        }

        [Test]
        public void Entries_AreOrderedRowThenColumn()
        {
            var f = new Formation(new[]
            {
                new FormationEntry(FormationRow.Back, 0, "a", 1),
                new FormationEntry(FormationRow.Front, 3, "b", 1),
                new FormationEntry(FormationRow.Front, 1, "c", 1),
            });
            Assert.That(f.Entries, Has.Count.EqualTo(3));
            Assert.That(f.Entries[0].UnitId, Is.EqualTo("c"));
            Assert.That(f.Entries[1].UnitId, Is.EqualTo("b"));
            Assert.That(f.Entries[2].UnitId, Is.EqualTo("a"));
        }

        [Test]
        public void RemoveMissing_DropsUnitsThatLeftTheBoard()
        {
            var board = new BoardModel(3, 3);
            board.Set(new Cell(0, 0), new BoardItem("infantry", 2));
            board.Set(new Cell(1, 0), new BoardItem("infantry", 3));
            var f = new Formation();
            f.Place(new FormationEntry(FormationRow.Front, 0, "infantry", 2, new Cell(0, 0)), 5);
            f.Place(new FormationEntry(FormationRow.Front, 1, "infantry", 2, new Cell(1, 0)), 5); // level changed
            f.Place(new FormationEntry(FormationRow.Front, 2, "infantry", 2, new Cell(2, 0)), 5); // empty cell
            f.Place(new FormationEntry(FormationRow.Front, 3, "infantry", 2), 5); // not from the board
            f.RemoveMissing(board);
            Assert.That(f.Count, Is.EqualTo(2));
            Assert.That(f.Get(FormationRow.Front, 0).HasValue, Is.True);
            Assert.That(f.Get(FormationRow.Front, 3).HasValue, Is.True);
        }

        [Test]
        public void Power_GrowsWithLevel()
        {
            var config = BattleTestHelpers.CreateConfig();
            var low = new Formation(new[] { new FormationEntry(FormationRow.Front, 0, "infantry", 1) });
            var high = new Formation(new[] { new FormationEntry(FormationRow.Front, 0, "infantry", 2) });
            Assert.That(high.Power(config), Is.GreaterThan(low.Power(config)));
        }
    }
}

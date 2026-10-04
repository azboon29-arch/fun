using System;
using System.Collections.Generic;
using Game.Core.Random;

namespace Game.Logic.Merge
{
    /// <summary>
    /// The merge grid: which item sits in which slot, and the drag rules (move, swap, merge).
    /// Knows nothing about prices or views; <see cref="MergeBoardService"/> wraps it.
    /// </summary>
    public sealed class BoardModel
    {
        private BoardItem?[,] _slots;

        /// <summary>Creates an empty board.</summary>
        public BoardModel(int columns, int rows)
        {
            if (columns < 1 || rows < 1) throw new ArgumentOutOfRangeException(nameof(columns), "Board must be at least 1x1.");
            _slots = new BoardItem?[columns, rows];
        }

        /// <summary>Number of columns.</summary>
        public int Columns => _slots.GetLength(0);

        /// <summary>Number of rows.</summary>
        public int Rows => _slots.GetLength(1);

        /// <summary>Total slots.</summary>
        public int SlotCount => Columns * Rows;

        /// <summary>True if <paramref name="cell"/> is on the board.</summary>
        public bool Contains(Cell cell) =>
            cell.Column >= 0 && cell.Row >= 0 && cell.Column < Columns && cell.Row < Rows;

        /// <summary>The item at <paramref name="cell"/>, or null if empty or off the board.</summary>
        public BoardItem? Get(Cell cell) => Contains(cell) ? _slots[cell.Column, cell.Row] : null;

        /// <summary>True if <paramref name="cell"/> is on the board and empty.</summary>
        public bool IsEmpty(Cell cell) => Contains(cell) && !_slots[cell.Column, cell.Row].HasValue;

        /// <summary>Puts <paramref name="item"/> at <paramref name="cell"/>, replacing whatever was there. Used by loading and tests.</summary>
        public void Set(Cell cell, BoardItem? item)
        {
            if (!Contains(cell)) throw new ArgumentOutOfRangeException(nameof(cell), $"Cell {cell} is off the board.");
            _slots[cell.Column, cell.Row] = item;
        }

        /// <summary>Removes every item.</summary>
        public void Clear() => _slots = new BoardItem?[Columns, Rows];

        /// <summary>Number of occupied slots.</summary>
        public int ItemCount
        {
            get
            {
                var count = 0;
                foreach (var slot in _slots) if (slot.HasValue) count++;
                return count;
            }
        }

        /// <summary>True when every slot holds an item.</summary>
        public bool IsFull => ItemCount == SlotCount;

        /// <summary>Empty cells, row by row from the top-left.</summary>
        public List<Cell> EmptyCells()
        {
            var cells = new List<Cell>();
            for (var row = 0; row < Rows; row++)
            {
                for (var column = 0; column < Columns; column++)
                {
                    if (!_slots[column, row].HasValue) cells.Add(new Cell(column, row));
                }
            }
            return cells;
        }

        /// <summary>Occupied cells with their items, row by row from the top-left.</summary>
        public List<KeyValuePair<Cell, BoardItem>> Items()
        {
            var items = new List<KeyValuePair<Cell, BoardItem>>();
            for (var row = 0; row < Rows; row++)
            {
                for (var column = 0; column < Columns; column++)
                {
                    var item = _slots[column, row];
                    if (item.HasValue) items.Add(new KeyValuePair<Cell, BoardItem>(new Cell(column, row), item.Value));
                }
            }
            return items;
        }

        /// <summary>
        /// Applies the drag rules for dropping the item at <paramref name="from"/> onto <paramref name="to"/>:
        /// empty target moves, identical target merges (unless at <paramref name="maxLevelOf"/>), different target swaps.
        /// </summary>
        /// <param name="from">Source cell.</param>
        /// <param name="to">Target cell.</param>
        /// <param name="maxLevelOf">Returns the max level for an item type id.</param>
        public DragOutcome Drag(Cell from, Cell to, Func<string, int> maxLevelOf)
        {
            if (maxLevelOf == null) throw new ArgumentNullException(nameof(maxLevelOf));
            if (!Contains(from) || !Contains(to)) return DragOutcome.Invalid;

            var source = _slots[from.Column, from.Row];
            if (!source.HasValue) return DragOutcome.Invalid;
            if (from == to) return DragOutcome.None;

            var target = _slots[to.Column, to.Row];
            if (!target.HasValue)
            {
                _slots[to.Column, to.Row] = source;
                _slots[from.Column, from.Row] = null;
                return DragOutcome.Moved;
            }

            if (source.Value.CanMergeWith(target.Value))
            {
                if (source.Value.Level >= maxLevelOf(source.Value.TypeId)) return DragOutcome.MaxLevel;
                _slots[to.Column, to.Row] = source.Value.NextLevel();
                _slots[from.Column, from.Row] = null;
                return DragOutcome.Merged;
            }

            _slots[to.Column, to.Row] = source;
            _slots[from.Column, from.Row] = target;
            return DragOutcome.Swapped;
        }

        /// <summary>Picks a random empty cell, or returns null when the board is full.</summary>
        public Cell? RandomEmptyCell(IRandom random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            var empty = EmptyCells();
            if (empty.Count == 0) return null;
            return empty[random.Next(empty.Count)];
        }

        /// <summary>
        /// Grows the board to <paramref name="columns"/> × <paramref name="rows"/>. Items keep their cells.
        /// Shrinking is refused so items can never be lost.
        /// </summary>
        public void Resize(int columns, int rows)
        {
            if (columns < Columns || rows < Rows)
            {
                throw new ArgumentOutOfRangeException(nameof(columns), "The board can only grow; shrinking could drop items.");
            }
            if (columns == Columns && rows == Rows) return;

            var grown = new BoardItem?[columns, rows];
            for (var column = 0; column < Columns; column++)
            {
                for (var row = 0; row < Rows; row++) grown[column, row] = _slots[column, row];
            }
            _slots = grown;
        }
    }
}

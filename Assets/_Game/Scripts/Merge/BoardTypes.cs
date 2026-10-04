using System;

namespace Game.Logic.Merge
{
    /// <summary>A board position. Column 0 is the left edge, row 0 the top row.</summary>
    public readonly struct Cell : IEquatable<Cell>
    {
        /// <summary>Creates a cell.</summary>
        public Cell(int column, int row)
        {
            Column = column;
            Row = row;
        }

        /// <summary>Column index, from the left.</summary>
        public int Column { get; }

        /// <summary>Row index, from the top.</summary>
        public int Row { get; }

        /// <inheritdoc />
        public bool Equals(Cell other) => Column == other.Column && Row == other.Row;

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is Cell other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => unchecked(Column * 397) ^ Row;

        /// <summary>Equality.</summary>
        public static bool operator ==(Cell a, Cell b) => a.Equals(b);

        /// <summary>Inequality.</summary>
        public static bool operator !=(Cell a, Cell b) => !a.Equals(b);

        /// <inheritdoc />
        public override string ToString() => $"({Column},{Row})";
    }

    /// <summary>An item on the board: a type and a level.</summary>
    public readonly struct BoardItem : IEquatable<BoardItem>
    {
        /// <summary>Creates an item.</summary>
        public BoardItem(string typeId, int level)
        {
            if (string.IsNullOrEmpty(typeId)) throw new ArgumentException("Type id is required.", nameof(typeId));
            if (level < 1) throw new ArgumentOutOfRangeException(nameof(level));
            TypeId = typeId;
            Level = level;
        }

        /// <summary>Item type id (see <see cref="ItemDefinition.Id"/>).</summary>
        public string TypeId { get; }

        /// <summary>Level, starting at 1.</summary>
        public int Level { get; }

        /// <summary>True when <paramref name="other"/> has the same type and level, so the two can merge.</summary>
        public bool CanMergeWith(BoardItem other) => Equals(other);

        /// <summary>The item one level higher.</summary>
        public BoardItem NextLevel() => new BoardItem(TypeId, Level + 1);

        /// <inheritdoc />
        public bool Equals(BoardItem other) => TypeId == other.TypeId && Level == other.Level;

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is BoardItem other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => unchecked(((TypeId?.GetHashCode() ?? 0) * 397) ^ Level);

        /// <inheritdoc />
        public override string ToString() => $"{TypeId} L{Level}";
    }

    /// <summary>What a drag did.</summary>
    public enum DragOutcome
    {
        /// <summary>Nothing happened: the source was empty or a cell was off the board.</summary>
        Invalid,

        /// <summary>Dropped back on its own slot.</summary>
        None,

        /// <summary>Moved to an empty slot.</summary>
        Moved,

        /// <summary>Swapped with a different item.</summary>
        Swapped,

        /// <summary>Merged with an identical item into one item a level higher at the target.</summary>
        Merged,

        /// <summary>Dropped on an identical item that is already at max level. Nothing changed.</summary>
        MaxLevel,
    }

    /// <summary>Why a purchase failed.</summary>
    public enum BuyFailure
    {
        /// <summary>The item type does not exist in the config.</summary>
        UnknownType,

        /// <summary>No empty slot. Nothing was charged.</summary>
        BoardFull,

        /// <summary>The wallet could not pay.</summary>
        NotEnoughGold,
    }
}

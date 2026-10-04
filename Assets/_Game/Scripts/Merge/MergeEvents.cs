namespace Game.Logic.Merge
{
    /// <summary>Two identical items merged. Views animate and play the merge sound on this.</summary>
    public readonly struct ItemMerged
    {
        /// <summary>Creates the event.</summary>
        public ItemMerged(Cell from, Cell to, BoardItem result)
        {
            From = from;
            To = to;
            Result = result;
        }

        /// <summary>Cell the dragged item came from (now empty).</summary>
        public Cell From { get; }

        /// <summary>Cell holding the merged item.</summary>
        public Cell To { get; }

        /// <summary>The new, higher-level item.</summary>
        public BoardItem Result { get; }
    }

    /// <summary>An item was bought and placed.</summary>
    public readonly struct ItemBought
    {
        /// <summary>Creates the event.</summary>
        public ItemBought(Cell cell, BoardItem item, long cost)
        {
            Cell = cell;
            Item = item;
            Cost = cost;
        }

        /// <summary>Where it was placed.</summary>
        public Cell Cell { get; }

        /// <summary>The new item.</summary>
        public BoardItem Item { get; }

        /// <summary>Gold paid.</summary>
        public long Cost { get; }
    }

    /// <summary>A purchase was refused. The view shows a message; nothing was charged or lost.</summary>
    public readonly struct BuyFailed
    {
        /// <summary>Creates the event.</summary>
        public BuyFailed(string typeId, BuyFailure reason)
        {
            TypeId = typeId;
            Reason = reason;
        }

        /// <summary>Item type that was requested.</summary>
        public string TypeId { get; }

        /// <summary>Why it failed.</summary>
        public BuyFailure Reason { get; }
    }

    /// <summary>A drag finished (any outcome other than <see cref="DragOutcome.Merged"/>, which raises <see cref="ItemMerged"/>).</summary>
    public readonly struct ItemDragged
    {
        /// <summary>Creates the event.</summary>
        public ItemDragged(Cell from, Cell to, DragOutcome outcome)
        {
            From = from;
            To = to;
            Outcome = outcome;
        }

        /// <summary>Source cell.</summary>
        public Cell From { get; }

        /// <summary>Target cell.</summary>
        public Cell To { get; }

        /// <summary>What happened.</summary>
        public DragOutcome Outcome { get; }
    }

    /// <summary>The board's contents or size changed. Views redraw on this.</summary>
    public readonly struct BoardChanged
    {
    }
}

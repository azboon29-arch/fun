using System;
using System.Collections.Generic;
using System.Linq;
using Game.Logic.Merge;

namespace Game.Logic.Battle
{
    /// <summary>Formation rows. The front row is hit first; the back row is protected until the rows ahead fall.</summary>
    public enum FormationRow
    {
        /// <summary>Closest to the enemy.</summary>
        Front = 0,

        /// <summary>Middle row.</summary>
        Middle = 1,

        /// <summary>Furthest from the enemy.</summary>
        Back = 2,
    }

    /// <summary>One unit placed in a formation.</summary>
    public readonly struct FormationEntry
    {
        /// <summary>Creates an entry.</summary>
        public FormationEntry(FormationRow row, int column, string unitId, int level, Cell? sourceCell = null)
        {
            Row = row;
            Column = column;
            UnitId = unitId;
            Level = level;
            SourceCell = sourceCell;
        }

        /// <summary>Row.</summary>
        public FormationRow Row { get; }

        /// <summary>Column (lane), 0 = left as the player sees it.</summary>
        public int Column { get; }

        /// <summary>Unit type id.</summary>
        public string UnitId { get; }

        /// <summary>Unit level.</summary>
        public int Level { get; }

        /// <summary>Board cell the unit was taken from (player side only), so one board item is deployed once.</summary>
        public Cell? SourceCell { get; }
    }

    /// <summary>
    /// A 3-row × 5-column formation grid for one side. The player fills theirs from board units within the level's
    /// deployment limit; enemy formations come from level data.
    /// </summary>
    public sealed class Formation
    {
        /// <summary>Rows per formation.</summary>
        public const int RowCount = 3;

        /// <summary>Columns (lanes) per formation.</summary>
        public const int ColumnCount = 5;

        private readonly Dictionary<(FormationRow, int), FormationEntry> _slots = new Dictionary<(FormationRow, int), FormationEntry>();

        /// <summary>Creates an empty formation.</summary>
        public Formation() { }

        /// <summary>Creates a formation holding <paramref name="entries"/> (later entries replace earlier ones in the same slot).</summary>
        public Formation(IEnumerable<FormationEntry> entries)
        {
            foreach (var entry in entries) _slots[(entry.Row, entry.Column)] = entry;
        }

        /// <summary>Units in row-then-column order (stable, so the simulation creates units in the same order every time).</summary>
        public IReadOnlyList<FormationEntry> Entries =>
            _slots.Values.OrderBy(e => (int)e.Row).ThenBy(e => e.Column).ToList();

        /// <summary>Number of units placed.</summary>
        public int Count => _slots.Count;

        /// <summary>True if the slot exists on the grid.</summary>
        public static bool IsValidSlot(FormationRow row, int column) =>
            (int)row >= 0 && (int)row < RowCount && column >= 0 && column < ColumnCount;

        /// <summary>The entry in a slot, or null.</summary>
        public FormationEntry? Get(FormationRow row, int column) =>
            _slots.TryGetValue((row, column), out var entry) ? entry : (FormationEntry?)null;

        /// <summary>True when the board item at <paramref name="cell"/> is already placed.</summary>
        public bool Uses(Cell cell) => _slots.Values.Any(e => e.SourceCell == cell);

        /// <summary>
        /// Places <paramref name="entry"/>, replacing what was in that slot. If the same board item was placed elsewhere,
        /// it moves. Returns false (and changes nothing) for an invalid slot or when the deployment limit is reached.
        /// </summary>
        public bool Place(FormationEntry entry, int deploymentLimit)
        {
            if (!IsValidSlot(entry.Row, entry.Column)) return false;
            if (entry.SourceCell.HasValue)
            {
                var previous = _slots.FirstOrDefault(p => p.Value.SourceCell == entry.SourceCell);
                if (previous.Value.UnitId != null) _slots.Remove(previous.Key);
            }
            var key = (entry.Row, entry.Column);
            if (!_slots.ContainsKey(key) && _slots.Count >= deploymentLimit) return false;
            _slots[key] = entry;
            return true;
        }

        /// <summary>Removes the unit in a slot. Returns false if it was empty.</summary>
        public bool Remove(FormationRow row, int column) => _slots.Remove((row, column));

        /// <summary>Removes every unit.</summary>
        public void Clear() => _slots.Clear();

        /// <summary>
        /// Drops entries that are no longer valid for the player's board (the item moved, merged or was replaced),
        /// so a remembered formation never deploys units the player does not own.
        /// </summary>
        public void RemoveMissing(BoardModel board)
        {
            foreach (var key in _slots.Keys.ToList())
            {
                var entry = _slots[key];
                if (!entry.SourceCell.HasValue) continue;
                var item = board.Get(entry.SourceCell.Value);
                if (!item.HasValue || item.Value.TypeId != entry.UnitId || item.Value.Level != entry.Level) _slots.Remove(key);
            }
        }

        /// <summary>Sum of rough power of every placed unit.</summary>
        public long Power(BattleConfig config)
        {
            long total = 0;
            foreach (var entry in _slots.Values)
            {
                if (config.TryGetUnit(entry.UnitId, out var def)) total += def.StatsAt(entry.Level).Power;
            }
            return total;
        }
    }
}

using System;
using System.Collections.Generic;
using Game.Core.Events;
using Game.Core.Json;
using Game.Core.Random;
using Game.Core.Save;

namespace Game.Logic.Merge
{
    /// <summary>
    /// Game-facing merge board: drag rules, buying with rising prices, buy-level unlocks, board growth and saving.
    /// Publishes <see cref="ItemMerged"/>, <see cref="ItemBought"/>, <see cref="BuyFailed"/>, <see cref="ItemDragged"/>,
    /// <see cref="BoardChanged"/> and <see cref="GameStateChanged"/> on the event bus.
    /// </summary>
    public sealed class MergeBoardService : ISaveSection
    {
        /// <summary>Save section key.</summary>
        public const string SaveKey = "board";

        private const string ColumnsKey = "columns";
        private const string RowsKey = "rows";
        private const string ItemsKey = "items";
        private const string PurchasesKey = "purchases";
        private const string HighestKey = "highest";
        private const string ColumnKey = "c";
        private const string RowKey = "r";
        private const string TypeKey = "type";
        private const string LevelKey = "level";

        private readonly MergeConfig _config;
        private readonly EventBus _bus;
        private readonly IRandom _random;
        private readonly Dictionary<string, int> _purchases = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _highestLevel = new Dictionary<string, int>();

        /// <summary>Creates the service with an empty starting board.</summary>
        public MergeBoardService(MergeConfig config, EventBus bus, IRandom random, IGoldWallet wallet)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            Wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            Board = new BoardModel(config.StartColumns, config.StartRows);
        }

        /// <summary>The board contents. Read it freely; change it only through this service so events fire.</summary>
        public BoardModel Board { get; private set; }

        /// <summary>The merge config in use.</summary>
        public MergeConfig Config => _config;

        /// <summary>Wallet purchases are paid from. Phase 3 swaps in the real economy wallet.</summary>
        public IGoldWallet Wallet { get; set; }

        /// <inheritdoc />
        public string Key => SaveKey;

        /// <summary>How many times <paramref name="typeId"/> has been bought.</summary>
        public int PurchaseCount(string typeId) => _purchases.TryGetValue(typeId, out var n) ? n : 0;

        /// <summary>Highest level of <paramref name="typeId"/> ever owned (0 if never).</summary>
        public int HighestLevel(string typeId) => _highestLevel.TryGetValue(typeId, out var n) ? n : 0;

        /// <summary>Level the next purchase of <paramref name="typeId"/> arrives at: highest reached minus the offset, at least 1.</summary>
        public int BuyLevel(string typeId)
        {
            var max = _config.TryGetItem(typeId, out var def) ? def.MaxLevel : 1;
            return Math.Min(max, Math.Max(1, HighestLevel(typeId) - _config.BuyLevelOffset));
        }

        /// <summary>Gold cost of the next purchase of <paramref name="typeId"/>. Throws for unknown types.</summary>
        public long BuyCost(string typeId)
        {
            if (!_config.TryGetItem(typeId, out var def)) throw new ArgumentException($"Unknown item type '{typeId}'.", nameof(typeId));
            return def.CostOf(BuyLevel(typeId), PurchaseCount(typeId));
        }

        /// <summary>Drops the item at <paramref name="from"/> onto <paramref name="to"/> (move, swap or merge).</summary>
        public DragOutcome Drag(Cell from, Cell to)
        {
            var outcome = Board.Drag(from, to, MaxLevelOf);
            switch (outcome)
            {
                case DragOutcome.Merged:
                    var result = Board.Get(to).Value;
                    RecordLevel(result);
                    _bus.Publish(new ItemMerged(from, to, result));
                    Changed("merge");
                    break;
                case DragOutcome.Moved:
                case DragOutcome.Swapped:
                    _bus.Publish(new ItemDragged(from, to, outcome));
                    Changed("drag");
                    break;
                default:
                    _bus.Publish(new ItemDragged(from, to, outcome));
                    break;
            }
            return outcome;
        }

        /// <summary>
        /// Buys one <paramref name="typeId"/> at <see cref="BuyLevel"/> into a random empty slot.
        /// Checks for space before charging, so a full board never costs gold or loses an item.
        /// </summary>
        /// <returns>The cell used, or null on failure (a <see cref="BuyFailed"/> event says why).</returns>
        public Cell? Buy(string typeId)
        {
            if (!_config.TryGetItem(typeId, out _)) return Fail(typeId, BuyFailure.UnknownType);

            var cell = Board.RandomEmptyCell(_random);
            if (!cell.HasValue) return Fail(typeId, BuyFailure.BoardFull);

            var cost = BuyCost(typeId);
            if (!Wallet.TrySpend(cost)) return Fail(typeId, BuyFailure.NotEnoughGold);

            var item = new BoardItem(typeId, BuyLevel(typeId));
            Board.Set(cell.Value, item);
            _purchases[typeId] = PurchaseCount(typeId) + 1;
            RecordLevel(item);
            _bus.Publish(new ItemBought(cell.Value, item, cost));
            Changed("buy");
            return cell;
        }

        /// <summary>Grows the board, clamped to the config's max size. Never shrinks. Returns true if the size changed.</summary>
        public bool Expand(int columns, int rows)
        {
            columns = Math.Min(_config.MaxColumns, Math.Max(Board.Columns, columns));
            rows = Math.Min(_config.MaxRows, Math.Max(Board.Rows, rows));
            if (columns == Board.Columns && rows == Board.Rows) return false;
            Board.Resize(columns, rows);
            Changed("expand");
            return true;
        }

        /// <inheritdoc />
        public JsonObject Capture()
        {
            var items = new JsonArray();
            foreach (var pair in Board.Items())
            {
                items.Add(new JsonObject()
                    .Set(ColumnKey, pair.Key.Column)
                    .Set(RowKey, pair.Key.Row)
                    .Set(TypeKey, pair.Value.TypeId)
                    .Set(LevelKey, pair.Value.Level));
            }
            return new JsonObject()
                .Set(ColumnsKey, Board.Columns)
                .Set(RowsKey, Board.Rows)
                .Set(ItemsKey, items)
                .Set(PurchasesKey, ToJson(_purchases))
                .Set(HighestKey, ToJson(_highestLevel));
        }

        /// <inheritdoc />
        public void Restore(JsonObject data)
        {
            _purchases.Clear();
            _highestLevel.Clear();
            if (data == null)
            {
                Board = new BoardModel(_config.StartColumns, _config.StartRows);
                Notify();
                return;
            }

            // Never smaller than the start size, never larger than the max (a config change must not drop items).
            var columns = Math.Max(_config.StartColumns, data.GetInt(ColumnsKey, _config.StartColumns));
            var rows = Math.Max(_config.StartRows, data.GetInt(RowsKey, _config.StartRows));
            var stored = data.GetArray(ItemsKey)?.Items ?? (IReadOnlyList<JsonNode>)Array.Empty<JsonNode>();
            foreach (var node in stored)
            {
                if (node is JsonObject o)
                {
                    columns = Math.Max(columns, o.GetInt(ColumnKey) + 1);
                    rows = Math.Max(rows, o.GetInt(RowKey) + 1);
                }
            }
            Board = new BoardModel(columns, rows);

            var overflow = new List<BoardItem>();
            foreach (var node in stored)
            {
                if (!(node is JsonObject o)) continue;
                var typeId = o.GetString(TypeKey);
                var level = o.GetInt(LevelKey);
                // Unknown types (removed from config) are dropped; there is nothing valid to show for them.
                if (!_config.TryGetItem(typeId, out var def) || level < 1) continue;
                var item = new BoardItem(typeId, Math.Min(level, def.MaxLevel));
                var cell = new Cell(o.GetInt(ColumnKey), o.GetInt(RowKey));
                if (Board.Contains(cell) && Board.IsEmpty(cell)) Board.Set(cell, item);
                else overflow.Add(item);
                RecordLevel(item);
            }
            // Two items stored in one cell (a hand-edited or damaged save): place the extras in free cells rather than lose them.
            foreach (var item in overflow)
            {
                var free = Board.EmptyCells();
                if (free.Count == 0)
                {
                    Board.Resize(Board.Columns, Board.Rows + 1);
                    free = Board.EmptyCells();
                }
                Board.Set(free[0], item);
            }

            ReadCounts(data.GetObject(PurchasesKey), _purchases);
            var highest = new Dictionary<string, int>();
            ReadCounts(data.GetObject(HighestKey), highest);
            foreach (var pair in highest)
            {
                if (pair.Value > HighestLevel(pair.Key)) _highestLevel[pair.Key] = pair.Value;
            }
            Notify();
        }

        private int MaxLevelOf(string typeId) => _config.TryGetItem(typeId, out var def) ? def.MaxLevel : 1;

        private void RecordLevel(BoardItem item)
        {
            if (item.Level > HighestLevel(item.TypeId)) _highestLevel[item.TypeId] = item.Level;
        }

        private Cell? Fail(string typeId, BuyFailure reason)
        {
            _bus.Publish(new BuyFailed(typeId, reason));
            return null;
        }

        private void Changed(string source)
        {
            Notify();
            _bus.Publish(new GameStateChanged(source));
        }

        private void Notify() => _bus.Publish(new BoardChanged());

        private static JsonObject ToJson(Dictionary<string, int> counts)
        {
            var obj = new JsonObject();
            var keys = new List<string>(counts.Keys);
            keys.Sort(StringComparer.Ordinal);
            foreach (var key in keys) obj.Set(key, counts[key]);
            return obj;
        }

        private void ReadCounts(JsonObject source, Dictionary<string, int> target)
        {
            if (source == null) return;
            foreach (var key in source.Keys)
            {
                if (!_config.TryGetItem(key, out _)) continue;
                var value = source.GetInt(key);
                if (value > 0) target[key] = value;
            }
        }
    }
}

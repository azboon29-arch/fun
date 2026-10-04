using System.Collections.Generic;
using System.Linq;

namespace Game.Logic.Merge
{
    /// <summary>Plain C# merge board settings, built from the MergeConfig asset.</summary>
    public sealed class MergeConfig
    {
        private readonly Dictionary<string, ItemDefinition> _items;

        /// <summary>Creates the config.</summary>
        /// <param name="startColumns">Board columns in a new game.</param>
        /// <param name="startRows">Board rows in a new game.</param>
        /// <param name="maxColumns">Columns after full expansion.</param>
        /// <param name="maxRows">Rows after full expansion.</param>
        /// <param name="buyLevelOffset">Purchases come at (highest level reached − offset), minimum 1.</param>
        /// <param name="items">Item types. Ids must be unique.</param>
        public MergeConfig(int startColumns, int startRows, int maxColumns, int maxRows, int buyLevelOffset, IEnumerable<ItemDefinition> items)
        {
            StartColumns = startColumns;
            StartRows = startRows;
            MaxColumns = maxColumns;
            MaxRows = maxRows;
            BuyLevelOffset = buyLevelOffset;
            Items = items.ToList();
            _items = new Dictionary<string, ItemDefinition>();
            foreach (var item in Items)
            {
                if (item?.Id != null && !_items.ContainsKey(item.Id)) _items.Add(item.Id, item);
            }
        }

        /// <summary>Board columns in a new game.</summary>
        public int StartColumns { get; }

        /// <summary>Board rows in a new game.</summary>
        public int StartRows { get; }

        /// <summary>Columns after full expansion.</summary>
        public int MaxColumns { get; }

        /// <summary>Rows after full expansion.</summary>
        public int MaxRows { get; }

        /// <summary>Purchases come at (highest level reached − offset), minimum 1.</summary>
        public int BuyLevelOffset { get; }

        /// <summary>All item types in display order.</summary>
        public IReadOnlyList<ItemDefinition> Items { get; }

        /// <summary>Looks up an item type by id.</summary>
        public bool TryGetItem(string id, out ItemDefinition item)
        {
            if (id == null)
            {
                item = null;
                return false;
            }
            return _items.TryGetValue(id, out item);
        }

        /// <summary>Returns human-readable problems with this config; empty when valid.</summary>
        public List<string> Validate()
        {
            var errors = new List<string>();
            if (StartColumns < 1 || StartRows < 1) errors.Add("Start board size must be at least 1x1.");
            if (MaxColumns < StartColumns || MaxRows < StartRows) errors.Add("Max board size must be at least the start size.");
            if (BuyLevelOffset < 0) errors.Add("Buy level offset must not be negative.");
            if (Items.Count == 0) errors.Add("At least one item type is required.");

            var seen = new HashSet<string>();
            foreach (var item in Items)
            {
                if (item == null)
                {
                    errors.Add("Item list contains an empty entry.");
                    continue;
                }
                var name = string.IsNullOrEmpty(item.Id) ? "(no id)" : item.Id;
                if (string.IsNullOrEmpty(item.Id)) errors.Add("An item has no id.");
                else if (!seen.Add(item.Id)) errors.Add($"Item id '{item.Id}' is used more than once.");
                if (item.MaxLevel < 1) errors.Add($"Item '{name}' max level must be at least 1.");
                if (item.BaseCost < 0) errors.Add($"Item '{name}' base cost must not be negative.");
                if (item.CostGrowthPerPurchase < 1) errors.Add($"Item '{name}' cost growth must be at least 1.");
                if (item.LevelCostMultiplier < 1) errors.Add($"Item '{name}' level cost multiplier must be at least 1.");
            }
            return errors;
        }
    }
}

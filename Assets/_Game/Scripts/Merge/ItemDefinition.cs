using System;

namespace Game.Logic.Merge
{
    /// <summary>Whether a board item is a combat unit or a base building.</summary>
    public enum ItemKind
    {
        /// <summary>Combat unit (Infantry, Artillery, Armor).</summary>
        Unit,

        /// <summary>Base building (Gold Mine, Barracks, Warehouse).</summary>
        Building,
    }

    /// <summary>Static data for one item type, loaded from the merge config asset.</summary>
    public sealed class ItemDefinition
    {
        /// <summary>Creates a definition.</summary>
        /// <param name="id">Stable id stored in saves (e.g. "infantry"). Never rename without a save migration.</param>
        /// <param name="kind">Unit or building.</param>
        /// <param name="maxLevel">Highest level reachable by merging.</param>
        /// <param name="baseCost">Gold cost of the first level-1 purchase.</param>
        /// <param name="costGrowthPerPurchase">Cost multiplier applied per previous purchase of this type (e.g. 1.07).</param>
        /// <param name="levelCostMultiplier">Cost multiplier per level above 1 when buying higher levels (e.g. 2 = one merge's worth).</param>
        public ItemDefinition(string id, ItemKind kind, int maxLevel, long baseCost, double costGrowthPerPurchase, double levelCostMultiplier)
        {
            Id = id;
            Kind = kind;
            MaxLevel = maxLevel;
            BaseCost = baseCost;
            CostGrowthPerPurchase = costGrowthPerPurchase;
            LevelCostMultiplier = levelCostMultiplier;
        }

        /// <summary>Stable id stored in saves.</summary>
        public string Id { get; }

        /// <summary>Unit or building.</summary>
        public ItemKind Kind { get; }

        /// <summary>Highest level reachable by merging.</summary>
        public int MaxLevel { get; }

        /// <summary>Gold cost of the first level-1 purchase.</summary>
        public long BaseCost { get; }

        /// <summary>Cost multiplier per previous purchase of this type.</summary>
        public double CostGrowthPerPurchase { get; }

        /// <summary>Cost multiplier per level above 1.</summary>
        public double LevelCostMultiplier { get; }

        /// <summary>
        /// Gold cost of buying this item at <paramref name="level"/> after <paramref name="previousPurchases"/> purchases:
        /// <c>baseCost × levelCostMultiplier^(level−1) × costGrowthPerPurchase^previousPurchases</c>, rounded,
        /// capped at <see cref="long.MaxValue"/>.
        /// </summary>
        public long CostOf(int level, int previousPurchases)
        {
            if (level < 1) throw new ArgumentOutOfRangeException(nameof(level));
            if (previousPurchases < 0) throw new ArgumentOutOfRangeException(nameof(previousPurchases));

            var cost = BaseCost * Math.Pow(LevelCostMultiplier, level - 1) * Math.Pow(CostGrowthPerPurchase, previousPurchases);
            if (double.IsNaN(cost) || cost >= long.MaxValue) return long.MaxValue;
            return Math.Max(0L, (long)Math.Round(cost, MidpointRounding.AwayFromZero));
        }
    }
}

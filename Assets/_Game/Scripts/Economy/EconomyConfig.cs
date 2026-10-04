using System;
using System.Collections.Generic;

namespace Game.Logic.Economy
{
    /// <summary>
    /// Every economy number in one place: building outputs, offline earnings and starting balances.
    /// Built from the EconomyConfig asset. Formulas are documented on each property.
    /// </summary>
    public sealed class EconomyConfig
    {
        /// <summary>Item id of the Gold Mine.</summary>
        public string GoldMineId { get; set; } = "";

        /// <summary>Item id of the Barracks.</summary>
        public string BarracksId { get; set; } = "";

        /// <summary>Item id of the Warehouse.</summary>
        public string WarehouseId { get; set; } = "";

        /// <summary>Gold per second of a level-1 Gold Mine.</summary>
        public double MineBaseIncomePerSecond { get; set; }

        /// <summary>Gold Mine income multiplier per level: income(L) = base × growth^(L−1).</summary>
        public double MineIncomeGrowthPerLevel { get; set; }

        /// <summary>Unit price discount per Barracks level, as a fraction (0.02 = 2% per level), summed over all Barracks.</summary>
        public double BarracksDiscountPerLevel { get; set; }

        /// <summary>Largest total unit discount, as a fraction (0.5 = 50%).</summary>
        public double BarracksMaxDiscount { get; set; }

        /// <summary>Offline earnings cap with no Warehouse, in hours.</summary>
        public double OfflineBaseCapHours { get; set; }

        /// <summary>Extra offline cap per Warehouse level, in hours, summed over all Warehouses.</summary>
        public double WarehouseHoursPerLevel { get; set; }

        /// <summary>Largest offline cap, in hours.</summary>
        public double OfflineMaxCapHours { get; set; }

        /// <summary>Share of normal income earned while away (1 = full rate).</summary>
        public double OfflineIncomeMultiplier { get; set; }

        /// <summary>
        /// A gap between ticks longer than this (seconds) counts as time away: it is paid at the offline rate, capped,
        /// and reported in the "Welcome back" popup. Shorter gaps are normal play.
        /// </summary>
        public double OfflineThresholdSeconds { get; set; }

        /// <summary>Gold in a new game.</summary>
        public long StartingGold { get; set; }

        /// <summary>Gems in a new game.</summary>
        public long StartingGems { get; set; }

        /// <summary>Returns human-readable problems; empty when valid.</summary>
        public List<string> Validate()
        {
            var errors = new List<string>();
            if (string.IsNullOrEmpty(GoldMineId)) errors.Add("Gold Mine item id is required.");
            if (string.IsNullOrEmpty(BarracksId)) errors.Add("Barracks item id is required.");
            if (string.IsNullOrEmpty(WarehouseId)) errors.Add("Warehouse item id is required.");
            if (MineBaseIncomePerSecond < 0) errors.Add("Mine income must not be negative.");
            if (MineIncomeGrowthPerLevel < 1) errors.Add("Mine income growth must be at least 1.");
            if (BarracksDiscountPerLevel < 0) errors.Add("Barracks discount must not be negative.");
            if (BarracksMaxDiscount < 0 || BarracksMaxDiscount >= 1) errors.Add("Barracks max discount must be in [0, 1).");
            if (OfflineBaseCapHours < 0) errors.Add("Offline cap must not be negative.");
            if (WarehouseHoursPerLevel < 0) errors.Add("Warehouse hours must not be negative.");
            if (OfflineMaxCapHours < OfflineBaseCapHours) errors.Add("Offline max cap must be at least the base cap.");
            if (OfflineIncomeMultiplier < 0) errors.Add("Offline income multiplier must not be negative.");
            if (OfflineThresholdSeconds <= 0) errors.Add("Offline threshold must be positive.");
            if (StartingGold < 0 || StartingGems < 0) errors.Add("Starting balances must not be negative.");
            return errors;
        }

        /// <summary>Gold per second of one Gold Mine at <paramref name="level"/>.</summary>
        public double MineIncome(int level)
        {
            if (level < 1) throw new ArgumentOutOfRangeException(nameof(level));
            return MineBaseIncomePerSecond * Math.Pow(MineIncomeGrowthPerLevel, level - 1);
        }
    }
}

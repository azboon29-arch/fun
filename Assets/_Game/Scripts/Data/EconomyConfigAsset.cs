using System;
using System.Collections.Generic;
using System.Linq;
using Game.Logic.Base;
using Game.Logic.Economy;
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// Designer-editable economy: building outputs, offline earnings, starting balances and HQ levels.
    /// Converted to the plain C# <see cref="EconomyConfig"/> and <see cref="HqConfig"/> at boot.
    /// </summary>
    [CreateAssetMenu(menuName = "Merge Command/Economy Config", fileName = "EconomyConfig")]
    public sealed class EconomyConfigAsset : ScriptableObject
    {
        [Header("Building item ids (from MergeConfig)")]
        [SerializeField] private string goldMineId = "goldmine";
        [SerializeField] private string barracksId = "barracks";
        [SerializeField] private string warehouseId = "warehouse";

        [Header("Gold Mine: income(L) = base x growth^(L-1)")]
        [Min(0f)] [SerializeField] private float mineBaseIncomePerSecond = 1f;
        [Min(1f)] [SerializeField] private float mineIncomeGrowthPerLevel = 2.1f;

        [Header("Barracks: unit discount = min(max, sum of levels x perLevel)")]
        [Range(0f, 1f)] [SerializeField] private float barracksDiscountPerLevel = 0.02f;
        [Range(0f, 0.95f)] [SerializeField] private float barracksMaxDiscount = 0.5f;

        [Header("Offline earnings: cap = min(max, base + sum of Warehouse levels x perLevel) hours")]
        [Min(0f)] [SerializeField] private float offlineBaseCapHours = 8f;
        [Min(0f)] [SerializeField] private float warehouseHoursPerLevel = 0.5f;
        [Min(0f)] [SerializeField] private float offlineMaxCapHours = 24f;
        [Tooltip("Share of normal income earned while away (1 = full rate).")]
        [Min(0f)] [SerializeField] private float offlineIncomeMultiplier = 1f;
        [Tooltip("Gaps longer than this (seconds) count as time away and show the Welcome back popup.")]
        [Min(1f)] [SerializeField] private float offlineThresholdSeconds = 60f;

        [Header("New game")]
        [Min(0)] [SerializeField] private long startingGold = 500;
        [Min(0)] [SerializeField] private long startingGems;

        [Header("HQ levels (first entry is level 1)")]
        [SerializeField] private List<HqLevelEntry> hqLevels = new List<HqLevelEntry>();

        /// <summary>Builds the plain C# economy config.</summary>
        public EconomyConfig ToEconomy() => new EconomyConfig
        {
            GoldMineId = goldMineId,
            BarracksId = barracksId,
            WarehouseId = warehouseId,
            MineBaseIncomePerSecond = mineBaseIncomePerSecond,
            MineIncomeGrowthPerLevel = mineIncomeGrowthPerLevel,
            BarracksDiscountPerLevel = barracksDiscountPerLevel,
            BarracksMaxDiscount = barracksMaxDiscount,
            OfflineBaseCapHours = offlineBaseCapHours,
            WarehouseHoursPerLevel = warehouseHoursPerLevel,
            OfflineMaxCapHours = offlineMaxCapHours,
            OfflineIncomeMultiplier = offlineIncomeMultiplier,
            OfflineThresholdSeconds = offlineThresholdSeconds,
            StartingGold = startingGold,
            StartingGems = startingGems,
        };

        /// <summary>Builds the plain C# HQ config.</summary>
        public HqConfig ToHq() => new HqConfig(hqLevels.Select(l => l.ToModel()).ToList());

        /// <summary>Replaces the HQ levels. Used by the editor's default content generator.</summary>
        public void SetHqLevels(IEnumerable<HqLevelEntry> levels) => hqLevels = levels.ToList();

        private void OnValidate()
        {
            foreach (var error in ToEconomy().Validate().Concat(ToHq().Validate())) Debug.LogWarning($"[Game] {name}: {error}", this);
        }

        /// <summary>One HQ level.</summary>
        [Serializable]
        public sealed class HqLevelEntry
        {
            [Tooltip("Gold to upgrade to this level (ignored for level 1).")]
            [Min(0)] [SerializeField] private long upgradeCost;
            [Min(1)] [SerializeField] private int boardColumns = 4;
            [Min(1)] [SerializeField] private int boardRows = 5;
            [Min(1)] [SerializeField] private int maxBuyLevel = 5;
            [Tooltip("Unit trait ids unlocked at this level (used from Phase 5).")]
            [SerializeField] private List<string> unlockedTraits = new List<string>();

            /// <summary>Creates an empty entry (serializer and inspector).</summary>
            public HqLevelEntry() { }

            /// <summary>Creates an entry.</summary>
            public HqLevelEntry(long upgradeCost, int boardColumns, int boardRows, int maxBuyLevel)
            {
                this.upgradeCost = upgradeCost;
                this.boardColumns = boardColumns;
                this.boardRows = boardRows;
                this.maxBuyLevel = maxBuyLevel;
            }

            /// <summary>Plain C# level.</summary>
            public HqLevel ToModel() => new HqLevel(upgradeCost, boardColumns, boardRows, maxBuyLevel, unlockedTraits.ToArray());
        }
    }
}

using System.Collections.Generic;

namespace Game.Logic.Base
{
    /// <summary>What one HQ level gives and costs.</summary>
    public sealed class HqLevel
    {
        /// <summary>Creates a level.</summary>
        /// <param name="upgradeCost">Gold to upgrade to this level (ignored for level 1).</param>
        /// <param name="boardColumns">Board columns unlocked at this level.</param>
        /// <param name="boardRows">Board rows unlocked at this level.</param>
        /// <param name="maxBuyLevel">Highest level the buy buttons may sell at.</param>
        /// <param name="unlockedTraits">Unit trait ids unlocked at this level (used from Phase 5).</param>
        public HqLevel(long upgradeCost, int boardColumns, int boardRows, int maxBuyLevel, IReadOnlyList<string> unlockedTraits = null)
        {
            UpgradeCost = upgradeCost;
            BoardColumns = boardColumns;
            BoardRows = boardRows;
            MaxBuyLevel = maxBuyLevel;
            UnlockedTraits = unlockedTraits ?? new string[0];
        }

        /// <summary>Gold to upgrade to this level.</summary>
        public long UpgradeCost { get; }

        /// <summary>Board columns unlocked.</summary>
        public int BoardColumns { get; }

        /// <summary>Board rows unlocked.</summary>
        public int BoardRows { get; }

        /// <summary>Highest purchase level.</summary>
        public int MaxBuyLevel { get; }

        /// <summary>Unit trait ids unlocked at this level.</summary>
        public IReadOnlyList<string> UnlockedTraits { get; }
    }

    /// <summary>HQ levels in order; index 0 is level 1.</summary>
    public sealed class HqConfig
    {
        /// <summary>Creates the config.</summary>
        public HqConfig(IReadOnlyList<HqLevel> levels) { Levels = levels; }

        /// <summary>Levels in order; index 0 is level 1.</summary>
        public IReadOnlyList<HqLevel> Levels { get; }

        /// <summary>Highest HQ level.</summary>
        public int MaxLevel => Levels.Count;

        /// <summary>Data for HQ <paramref name="level"/> (1-based).</summary>
        public HqLevel Get(int level) => Levels[level - 1];

        /// <summary>Returns human-readable problems; empty when valid.</summary>
        public List<string> Validate()
        {
            var errors = new List<string>();
            if (Levels == null || Levels.Count == 0)
            {
                errors.Add("At least one HQ level is required.");
                return errors;
            }
            for (var i = 0; i < Levels.Count; i++)
            {
                var level = Levels[i];
                if (level.BoardColumns < 1 || level.BoardRows < 1) errors.Add($"HQ level {i + 1}: board size must be at least 1x1.");
                if (level.MaxBuyLevel < 1) errors.Add($"HQ level {i + 1}: max buy level must be at least 1.");
                if (i > 0 && level.UpgradeCost < 0) errors.Add($"HQ level {i + 1}: upgrade cost must not be negative.");
                if (i == 0) continue;
                var previous = Levels[i - 1];
                if (level.BoardColumns < previous.BoardColumns || level.BoardRows < previous.BoardRows)
                {
                    errors.Add($"HQ level {i + 1}: board size must not shrink.");
                }
                if (level.MaxBuyLevel < previous.MaxBuyLevel) errors.Add($"HQ level {i + 1}: max buy level must not drop.");
            }
            return errors;
        }
    }
}

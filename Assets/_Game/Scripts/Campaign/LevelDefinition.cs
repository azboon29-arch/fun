using System.Collections.Generic;
using Game.Logic.Battle;

namespace Game.Logic.Campaign
{
    /// <summary>One campaign battle: the enemy formation, limits and rewards.</summary>
    public sealed class LevelDefinition
    {
        /// <summary>Stable id used in saves (e.g. "1-1").</summary>
        public string Id { get; set; } = "";

        /// <summary>Display name key or text.</summary>
        public string Name { get; set; } = "";

        /// <summary>Enemy units.</summary>
        public List<FormationEntry> Enemies { get; set; } = new List<FormationEntry>();

        /// <summary>Most units the player may deploy.</summary>
        public int DeploymentLimit { get; set; }

        /// <summary>The battle is lost when this many seconds pass.</summary>
        public int TimeLimitSeconds { get; set; }

        /// <summary>Third star: win within this many seconds.</summary>
        public int FastWinSeconds { get; set; }

        /// <summary>Gold paid for every victory.</summary>
        public long RewardGold { get; set; }

        /// <summary>
        /// Battle seed. Fixed per level, so the same formation always plays out the same way: results depend on the
        /// player's choices, not on retrying for luck.
        /// </summary>
        public long Seed { get; set; }

        /// <summary>The enemy formation.</summary>
        public Formation CreateEnemyFormation() => new Formation(Enemies);
    }

    /// <summary>The ordered list of campaign levels plus reward rules.</summary>
    public sealed class CampaignConfig
    {
        /// <summary>Levels in play order. Each unlocks when the previous one is won.</summary>
        public List<LevelDefinition> Levels { get; set; } = new List<LevelDefinition>();

        /// <summary>Gems paid for each star earned for the first time.</summary>
        public long GemsPerNewStar { get; set; }

        /// <summary>The level with <paramref name="id"/>, or null.</summary>
        public LevelDefinition Find(string id) => Levels.Find(l => l.Id == id);

        /// <summary>Position of <paramref name="id"/> in the list, or -1.</summary>
        public int IndexOf(string id) => Levels.FindIndex(l => l.Id == id);

        /// <summary>Returns human-readable problems; empty when valid.</summary>
        public List<string> Validate(BattleConfig battle)
        {
            var errors = new List<string>();
            if (Levels.Count == 0) errors.Add("At least one campaign level is required.");
            if (GemsPerNewStar < 0) errors.Add("Gems per star must not be negative.");
            var ids = new HashSet<string>();
            foreach (var level in Levels)
            {
                if (string.IsNullOrEmpty(level.Id)) errors.Add("Every level needs an id.");
                else if (!ids.Add(level.Id)) errors.Add($"Duplicate level id '{level.Id}'.");
                if (level.Enemies.Count == 0) errors.Add($"Level '{level.Id}' has no enemies.");
                if (level.DeploymentLimit < 1) errors.Add($"Level '{level.Id}' deployment limit must be at least 1.");
                if (level.TimeLimitSeconds < 1) errors.Add($"Level '{level.Id}' needs a time limit.");
                if (level.FastWinSeconds < 0 || level.FastWinSeconds > level.TimeLimitSeconds)
                    errors.Add($"Level '{level.Id}' fast-win time must be between 0 and the time limit.");
                if (level.RewardGold < 0) errors.Add($"Level '{level.Id}' reward must not be negative.");
                var slots = new HashSet<(FormationRow, int)>();
                foreach (var enemy in level.Enemies)
                {
                    if (!Formation.IsValidSlot(enemy.Row, enemy.Column)) errors.Add($"Level '{level.Id}' has an enemy outside the grid.");
                    else if (!slots.Add((enemy.Row, enemy.Column))) errors.Add($"Level '{level.Id}' has two enemies in one slot.");
                    if (battle != null && !battle.TryGetUnit(enemy.UnitId, out _)) errors.Add($"Level '{level.Id}' uses unknown unit '{enemy.UnitId}'.");
                    if (enemy.Level < 1) errors.Add($"Level '{level.Id}' has an enemy below level 1.");
                }
            }
            return errors;
        }
    }
}

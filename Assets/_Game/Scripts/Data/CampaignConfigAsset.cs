using System;
using System.Collections.Generic;
using System.Linq;
using Game.Logic.Battle;
using Game.Logic.Campaign;
using UnityEngine;

namespace Game.Data
{
    /// <summary>Designer-editable campaign: levels in order, each with its enemy formation, limits and rewards.</summary>
    [CreateAssetMenu(menuName = "Merge Command/Campaign Config", fileName = "CampaignConfig")]
    public sealed class CampaignConfigAsset : ScriptableObject
    {
        [Tooltip("Gems paid for each star earned for the first time.")]
        [Min(0)] [SerializeField] private long gemsPerNewStar = 1;

        [Tooltip("Levels in play order. Each unlocks when the previous one is won.")]
        [SerializeField] private List<LevelEntry> levels = new List<LevelEntry>();

        /// <summary>Builds the plain C# config.</summary>
        public CampaignConfig ToModel() => new CampaignConfig
        {
            GemsPerNewStar = gemsPerNewStar,
            Levels = levels.Select(l => l.ToModel()).ToList(),
        };

        /// <summary>Replaces the level list. Used by the editor's default content generator.</summary>
        public void SetLevels(IEnumerable<LevelEntry> entries) => levels = entries.ToList();

        /// <summary>One campaign level.</summary>
        [Serializable]
        public sealed class LevelEntry
        {
            [Tooltip("Stable id stored in saves. Never rename without a save migration.")]
            [SerializeField] private string id = "";
            [Tooltip("Display name (text for now; a localization key in Phase 7).")]
            [SerializeField] private string displayName = "";
            [Min(1)] [SerializeField] private int deploymentLimit = 5;
            [Min(1)] [SerializeField] private int timeLimitSeconds = 90;
            [Tooltip("Third star: win within this many seconds (0 = no time star).")]
            [Min(0)] [SerializeField] private int fastWinSeconds = 30;
            [Min(0)] [SerializeField] private long rewardGold = 100;
            [Tooltip("Battle seed. Same formation + same seed = same battle.")]
            [SerializeField] private long seed = 1;
            [SerializeField] private List<EnemyEntry> enemies = new List<EnemyEntry>();

            /// <summary>Creates an empty entry (serializer and inspector).</summary>
            public LevelEntry() { }

            /// <summary>Creates an entry (default content generator).</summary>
            public LevelEntry(string id, string displayName, int deploymentLimit, int timeLimitSeconds, int fastWinSeconds, long rewardGold,
                long seed, IEnumerable<EnemyEntry> enemies)
            {
                this.id = id;
                this.displayName = displayName;
                this.deploymentLimit = deploymentLimit;
                this.timeLimitSeconds = timeLimitSeconds;
                this.fastWinSeconds = fastWinSeconds;
                this.rewardGold = rewardGold;
                this.seed = seed;
                this.enemies = enemies.ToList();
            }

            /// <summary>Plain C# level.</summary>
            public LevelDefinition ToModel() => new LevelDefinition
            {
                Id = id,
                Name = displayName,
                DeploymentLimit = deploymentLimit,
                TimeLimitSeconds = timeLimitSeconds,
                FastWinSeconds = fastWinSeconds,
                RewardGold = rewardGold,
                Seed = seed,
                Enemies = enemies.Select(e => e.ToModel()).ToList(),
            };
        }

        /// <summary>One enemy unit in a level's formation.</summary>
        [Serializable]
        public sealed class EnemyEntry
        {
            [SerializeField] private FormationRow row;
            [Range(0, Formation.ColumnCount - 1)] [SerializeField] private int column;
            [SerializeField] private string unitId = "";
            [Min(1)] [SerializeField] private int level = 1;

            /// <summary>Creates an empty entry (serializer and inspector).</summary>
            public EnemyEntry() { }

            /// <summary>Creates an entry (default content generator).</summary>
            public EnemyEntry(FormationRow row, int column, string unitId, int level)
            {
                this.row = row;
                this.column = column;
                this.unitId = unitId;
                this.level = level;
            }

            /// <summary>Plain C# formation entry.</summary>
            public FormationEntry ToModel() => new FormationEntry(row, column, unitId, level);
        }
    }
}

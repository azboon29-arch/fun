using System;
using System.Collections.Generic;
using Game.Logic.Battle;
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// Designer-editable battle rules and unit stats. Values are entered in friendly units (cells, seconds,
    /// multipliers) and converted once at boot to the integer values the deterministic simulation uses.
    /// </summary>
    [CreateAssetMenu(menuName = "Merge Command/Battle Config", fileName = "BattleConfig")]
    public sealed class BattleConfigAsset : ScriptableObject
    {
        private const int MilliPerCell = 1000;

        [Header("Simulation")]
        [Tooltip("Fixed simulation steps per second. Changing it changes every battle's exact outcome.")]
        [Min(1)] [SerializeField] private int tickRate = 20;

        [Header("Damage")]
        [Tooltip("Damage multiplier when the attacker's class counters the target's (1.5 = +50%).")]
        [Min(1f)] [SerializeField] private float counterMultiplier = 1.5f;
        [Tooltip("Random damage spread either way (0.05 = ±5%). Seeded, so replays match.")]
        [Range(0f, 0.5f)] [SerializeField] private float damageVariance = 0.05f;

        [Header("Field layout (cells)")]
        [Min(0.1f)] [SerializeField] private float laneSpacing = 1f;
        [Min(0.1f)] [SerializeField] private float rowSpacing = 1f;
        [Tooltip("Distance from the middle of the field to each army's front row.")]
        [Min(0.1f)] [SerializeField] private float frontOffset = 2.5f;

        [Header("Units (ids match MergeConfig items)")]
        [SerializeField] private List<UnitEntry> units = new List<UnitEntry>();

        /// <summary>Builds the plain C# config.</summary>
        public BattleConfig ToModel()
        {
            var config = new BattleConfig
            {
                TickRate = tickRate,
                CounterBonusPerMille = ToPermille(counterMultiplier),
                DamageVariancePerMille = ToPermille(damageVariance),
                LaneSpacing = ToMilli(laneSpacing),
                RowSpacing = ToMilli(rowSpacing),
                FrontOffset = ToMilli(frontOffset),
            };
            foreach (var unit in units) config.AddUnit(unit.ToModel(tickRate));
            return config;
        }

        /// <summary>Replaces the unit list. Used by the editor's default content generator.</summary>
        public void SetUnits(IEnumerable<UnitEntry> entries) => units = new List<UnitEntry>(entries);

        private void OnValidate()
        {
            foreach (var error in ToModel().Validate()) Debug.LogWarning($"[Game] {name}: {error}", this);
        }

        private static int ToMilli(float cells) => Mathf.RoundToInt(cells * MilliPerCell);

        private static int ToPermille(float multiplier) => Mathf.RoundToInt(multiplier * Permille.Scale);

        /// <summary>One unit type's level-1 stats.</summary>
        [Serializable]
        public sealed class UnitEntry
        {
            [Tooltip("Item id on the merge board.")]
            [SerializeField] private string id = "";
            [SerializeField] private UnitClass unitClass;
            [Min(1)] [SerializeField] private long hp = 100;
            [Min(1)] [SerializeField] private long attack = 10;
            [Tooltip("Attack reach in cells.")]
            [Min(0.1f)] [SerializeField] private float range = 1.2f;
            [Tooltip("Seconds between attacks.")]
            [Min(0.05f)] [SerializeField] private float attackInterval = 1f;
            [Tooltip("Cells per second.")]
            [Min(0f)] [SerializeField] private float moveSpeed = 1f;
            [Tooltip("HP and attack multiplier per merge level (1.9 = x1.9).")]
            [Min(1f)] [SerializeField] private float growthPerLevel = 1.9f;

            /// <summary>Creates an empty entry (serializer and inspector).</summary>
            public UnitEntry() { }

            /// <summary>Creates an entry (default content generator).</summary>
            public UnitEntry(string id, UnitClass unitClass, long hp, long attack, float range, float attackInterval, float moveSpeed, float growthPerLevel)
            {
                this.id = id;
                this.unitClass = unitClass;
                this.hp = hp;
                this.attack = attack;
                this.range = range;
                this.attackInterval = attackInterval;
                this.moveSpeed = moveSpeed;
                this.growthPerLevel = growthPerLevel;
            }

            /// <summary>Plain C# definition at <paramref name="tickRate"/> steps per second.</summary>
            public UnitDefinition ToModel(int tickRate) => new UnitDefinition
            {
                Id = id,
                Class = unitClass,
                BaseHp = hp,
                BaseAttack = attack,
                Range = ToMilli(range),
                AttackIntervalTicks = Mathf.Max(1, Mathf.RoundToInt(attackInterval * tickRate)),
                MoveSpeed = ToMilli(moveSpeed),
                GrowthPerMille = ToPermille(growthPerLevel),
            };
        }
    }
}

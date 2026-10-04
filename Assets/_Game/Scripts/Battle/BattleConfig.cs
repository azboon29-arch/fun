using System.Collections.Generic;

namespace Game.Logic.Battle
{
    /// <summary>Battle rules and unit stats, built from the BattleConfig asset.</summary>
    public sealed class BattleConfig
    {
        private readonly Dictionary<string, UnitDefinition> _units = new Dictionary<string, UnitDefinition>();

        /// <summary>Simulation steps per second.</summary>
        public int TickRate { get; set; }

        /// <summary>Damage multiplier when the attacker's class beats the defender's, per-mille (1500 = +50%).</summary>
        public int CounterBonusPerMille { get; set; }

        /// <summary>Random damage spread, per-mille either way (50 = ±5%). Drawn from the seeded RNG.</summary>
        public int DamageVariancePerMille { get; set; }

        /// <summary>Distance between formation lanes (columns), milli-units.</summary>
        public int LaneSpacing { get; set; }

        /// <summary>Distance between formation rows, milli-units.</summary>
        public int RowSpacing { get; set; }

        /// <summary>Distance from the middle of the field to each side's front row, milli-units.</summary>
        public int FrontOffset { get; set; }

        /// <summary>Unit types by id.</summary>
        public IReadOnlyDictionary<string, UnitDefinition> Units => _units;

        /// <summary>Adds or replaces a unit type.</summary>
        public BattleConfig AddUnit(UnitDefinition unit)
        {
            _units[unit.Id] = unit;
            return this;
        }

        /// <summary>Looks up a unit type.</summary>
        public bool TryGetUnit(string id, out UnitDefinition unit)
        {
            if (id == null)
            {
                unit = null;
                return false;
            }
            return _units.TryGetValue(id, out unit);
        }

        /// <summary>Returns human-readable problems; empty when valid.</summary>
        public List<string> Validate()
        {
            var errors = new List<string>();
            if (TickRate < 1) errors.Add("Tick rate must be at least 1.");
            if (CounterBonusPerMille < Permille.Scale) errors.Add("Counter bonus must be at least 1000 (no penalty).");
            if (DamageVariancePerMille < 0 || DamageVariancePerMille >= Permille.Scale) errors.Add("Damage variance must be in [0, 1000).");
            if (LaneSpacing <= 0 || RowSpacing <= 0 || FrontOffset <= 0) errors.Add("Field spacing values must be positive.");
            if (_units.Count == 0) errors.Add("At least one unit type is required.");
            foreach (var unit in _units.Values)
            {
                if (unit.BaseHp <= 0 || unit.BaseAttack <= 0) errors.Add($"Unit '{unit.Id}' needs positive HP and attack.");
                if (unit.Range <= 0) errors.Add($"Unit '{unit.Id}' range must be positive.");
                if (unit.AttackIntervalTicks <= 0) errors.Add($"Unit '{unit.Id}' attack interval must be positive.");
                if (unit.MoveSpeed < 0) errors.Add($"Unit '{unit.Id}' move speed must not be negative.");
                if (unit.GrowthPerMille < Permille.Scale) errors.Add($"Unit '{unit.Id}' growth must be at least 1000.");
            }
            return errors;
        }
    }
}

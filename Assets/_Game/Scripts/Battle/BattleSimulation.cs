using System;
using System.Collections.Generic;
using Game.Core.Random;

namespace Game.Logic.Battle
{
    /// <summary>
    /// Deterministic auto-battle: fixed timestep, integer math and one seeded RNG, so the same setup always produces
    /// the same battle on every device.
    ///
    /// Each tick, in unit-id order, every living unit:
    /// 1. Picks a target (see <see cref="ChooseTarget"/>): only enemies in the frontmost row that still has a living
    ///    unit can be targeted; among those the nearest, then the lowest HP, then the lowest id.
    /// 2. If the target is out of range, moves straight toward it (stopping at its range).
    /// 3. If in range and its cooldown is over, attacks: damage = attack, ×1.5 if its class counters the target's,
    ///    ± a small seeded random spread.
    /// Units destroyed this tick are removed at the end of the tick. The battle ends when one side has no units left,
    /// or in defeat when the time limit is reached.
    /// </summary>
    public sealed class BattleSimulation
    {
        private readonly BattleConfig _config;
        private readonly BattleSetup _setup;
        private readonly DeterministicRandom _random;
        private readonly List<BattleUnit> _units = new List<BattleUnit>();
        private readonly List<BattleEvent> _tickEvents = new List<BattleEvent>();
        private readonly List<BattleEvent> _log = new List<BattleEvent>();
        private readonly int _timeLimitTicks;
        private ulong _checksum = 1469598103934665603UL; // FNV-1a offset basis

        /// <summary>Creates a battle from <paramref name="setup"/>. Unknown unit types are skipped.</summary>
        public BattleSimulation(BattleConfig config, BattleSetup setup)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _setup = setup ?? throw new ArgumentNullException(nameof(setup));
            _random = new DeterministicRandom(setup.Seed);
            _timeLimitTicks = Math.Max(1, setup.TimeLimitSeconds) * config.TickRate;
            Spawn(setup.PlayerFormation, Side.Player);
            Spawn(setup.EnemyFormation, Side.Enemy);
            Outcome = BattleOutcome.InProgress;
            CheckEnd();
        }

        /// <summary>Every unit in creation order, including destroyed ones.</summary>
        public IReadOnlyList<BattleUnit> Units => _units;

        /// <summary>Ticks simulated so far.</summary>
        public int Tick { get; private set; }

        /// <summary>Simulation steps per second.</summary>
        public int TickRate => _config.TickRate;

        /// <summary>Seconds simulated so far.</summary>
        public double ElapsedSeconds => (double)Tick / _config.TickRate;

        /// <summary>Seconds left before the time limit.</summary>
        public double RemainingSeconds => Math.Max(0, (double)(_timeLimitTicks - Tick) / _config.TickRate);

        /// <summary>Current outcome; <see cref="BattleOutcome.InProgress"/> until the battle ends.</summary>
        public BattleOutcome Outcome { get; private set; }

        /// <summary>True once the battle has ended.</summary>
        public bool IsOver => Outcome != BattleOutcome.InProgress;

        /// <summary>Events from the most recent tick (for animations).</summary>
        public IReadOnlyList<BattleEvent> LastTickEvents => _tickEvents;

        /// <summary>Every event so far.</summary>
        public IReadOnlyList<BattleEvent> Log => _log;

        /// <summary>The unit with <paramref name="id"/>, or null.</summary>
        public BattleUnit Get(int id) => id >= 0 && id < _units.Count ? _units[id] : null;

        /// <summary>Advances one fixed step. Does nothing once the battle is over.</summary>
        public void Step()
        {
            _tickEvents.Clear();
            if (IsOver) return;
            Tick++;

            foreach (var unit in _units)
            {
                if (!unit.Alive) continue;
                if (unit.Cooldown > 0) unit.Cooldown--;

                var target = ChooseTarget(unit);
                unit.TargetId = target?.Id ?? -1;
                if (target == null) continue;

                var distance = Distance(unit, target);
                if (distance > unit.Stats.Range)
                {
                    MoveToward(unit, target, distance);
                }
                else if (unit.Cooldown == 0)
                {
                    Attack(unit, target);
                    unit.Cooldown = unit.Stats.AttackIntervalTicks;
                }
            }

            foreach (var unit in _units) Hash(unit.Hp, unit.X, unit.Y);
            CheckEnd();
        }

        /// <summary>Runs until the battle ends and returns the result.</summary>
        public BattleResult RunToEnd()
        {
            while (!IsOver) Step();
            return BuildResult();
        }

        /// <summary>The result. Only meaningful once <see cref="IsOver"/>.</summary>
        public BattleResult BuildResult()
        {
            var result = new BattleResult
            {
                Outcome = Outcome,
                Ticks = Tick,
                Seconds = Tick / _config.TickRate,
                TimedOut = Outcome == BattleOutcome.Defeat && Tick >= _timeLimitTicks && AnyAlive(Side.Player),
                Checksum = _checksum,
            };
            foreach (var unit in _units)
            {
                if (unit.Side != Side.Player) continue;
                result.PlayerUnits++;
                if (!unit.Alive) result.PlayerLosses++;
            }
            foreach (var e in _log)
            {
                if (e.Type != BattleEventType.Death) continue;
                var victim = _units[e.TargetId];
                var killer = Get(e.SourceId);
                if (victim.Side != Side.Player || killer == null) continue;
                var key = (killer.Class, victim.Class);
                result.PlayerDeathsBy[key] = result.PlayerDeathsBy.TryGetValue(key, out var n) ? n + 1 : 1;
            }

            var won = Outcome == BattleOutcome.Victory;
            result.Objectives[0] = won;
            result.Objectives[1] = won && result.PlayerLosses == 0;
            result.Objectives[2] = won && _setup.FastWinSeconds > 0 && Tick <= _setup.FastWinSeconds * _config.TickRate;
            foreach (var met in result.Objectives) if (met) result.Stars++;
            return result;
        }

        /// <summary>
        /// Targeting rule (documented for players): enemies in the frontmost row that still has a living unit can be
        /// targeted; pick the nearest, then the lowest HP, then the lowest id.
        /// </summary>
        public BattleUnit ChooseTarget(BattleUnit unit)
        {
            var frontRow = int.MaxValue;
            foreach (var other in _units)
            {
                if (other.Alive && other.Side != unit.Side && (int)other.Row < frontRow) frontRow = (int)other.Row;
            }
            if (frontRow == int.MaxValue) return null;

            BattleUnit best = null;
            long bestDistance = 0;
            foreach (var other in _units)
            {
                if (!other.Alive || other.Side == unit.Side || (int)other.Row != frontRow) continue;
                var distance = DistanceSquared(unit, other);
                if (best == null || distance < bestDistance || (distance == bestDistance && other.Hp < best.Hp))
                {
                    best = other;
                    bestDistance = distance;
                }
            }
            return best;
        }

        private void Spawn(Formation formation, Side side)
        {
            if (formation == null) return;
            var direction = side == Side.Player ? -1 : 1;
            foreach (var entry in formation.Entries)
            {
                if (!_config.TryGetUnit(entry.UnitId, out var def)) continue;
                // Lanes are mirrored for the enemy so column 0 is on the same screen side for both armies.
                var x = (entry.Column - (Formation.ColumnCount - 1) / 2) * _config.LaneSpacing;
                var y = direction * (_config.FrontOffset + (int)entry.Row * _config.RowSpacing);
                var unit = new BattleUnit(_units.Count, side, entry.UnitId, entry.Level, entry.Row, entry.Column, def.StatsAt(entry.Level), x, y);
                _units.Add(unit);
                Hash(unit.Id, unit.Stats.Hp, unit.Stats.Attack);
            }
        }

        private void Attack(BattleUnit attacker, BattleUnit target)
        {
            var damage = attacker.Stats.Attack;
            var countered = Counters.Beats(attacker.Class, target.Class);
            if (countered) damage = Permille.Apply(damage, _config.CounterBonusPerMille);

            var variance = _config.DamageVariancePerMille;
            if (variance > 0)
            {
                var roll = _random.Next(variance * 2 + 1) - variance; // -variance..+variance
                damage = Permille.Apply(damage, Permille.Scale + roll);
            }
            damage = Math.Max(1, damage);

            target.Hp = Math.Max(0, target.Hp - damage);
            Record(new BattleEvent(Tick, BattleEventType.Attack, attacker.Id, target.Id, damage, countered));
            if (!target.Alive) Record(new BattleEvent(Tick, BattleEventType.Death, attacker.Id, target.Id, 0, false));
        }

        private void MoveToward(BattleUnit unit, BattleUnit target, long distance)
        {
            var step = unit.Stats.MoveSpeed / _config.TickRate;
            if (step <= 0 || distance <= 0) return;
            var travel = Math.Min(step, distance - unit.Stats.Range);
            long dx = target.X - unit.X;
            long dy = target.Y - unit.Y;
            // Round each component away from zero so the unit always covers at least `travel` and can never stall
            // just outside its range; it never overshoots the target because travel <= distance.
            unit.X += (int)DivideAwayFromZero(dx * travel, distance);
            unit.Y += (int)DivideAwayFromZero(dy * travel, distance);
        }

        private static long DivideAwayFromZero(long numerator, long denominator)
        {
            var quotient = numerator / denominator;
            return numerator % denominator == 0 ? quotient : quotient + Math.Sign(numerator);
        }

        private void CheckEnd()
        {
            var player = AnyAlive(Side.Player);
            var enemy = AnyAlive(Side.Enemy);
            if (!enemy && player) Outcome = BattleOutcome.Victory;
            else if (!player) Outcome = BattleOutcome.Defeat;
            else if (Tick >= _timeLimitTicks) Outcome = BattleOutcome.Defeat;
        }

        private bool AnyAlive(Side side)
        {
            foreach (var unit in _units) if (unit.Alive && unit.Side == side) return true;
            return false;
        }

        private void Record(BattleEvent e)
        {
            _tickEvents.Add(e);
            _log.Add(e);
            Hash(e.Tick, e.SourceId, e.TargetId, e.Amount, (long)e.Type);
        }

        private void Hash(params long[] values)
        {
            const ulong prime = 1099511628211UL;
            foreach (var value in values)
            {
                unchecked
                {
                    _checksum ^= (ulong)value;
                    _checksum *= prime;
                }
            }
        }

        private static long DistanceSquared(BattleUnit a, BattleUnit b)
        {
            long dx = a.X - b.X;
            long dy = a.Y - b.Y;
            return dx * dx + dy * dy;
        }

        private static long Distance(BattleUnit a, BattleUnit b) => IntegerSqrt(DistanceSquared(a, b));

        /// <summary>Floor of the square root, using integers only (no floating point in the simulation).</summary>
        public static long IntegerSqrt(long value)
        {
            if (value <= 0) return 0;
            var x = (long)Math.Sqrt(value); // starting guess only; corrected below to the exact integer root
            // Compare by division so the checks cannot overflow near long.MaxValue.
            while (x > value / x) x--;
            while (x + 1 <= value / (x + 1)) x++;
            return x;
        }
    }
}

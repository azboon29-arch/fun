using System;

namespace Game.Logic.Battle
{
    /// <summary>The three unit classes. Each beats one other (see <see cref="Counters"/>).</summary>
    public enum UnitClass
    {
        /// <summary>Short range, fast. Beats Artillery.</summary>
        Infantry,

        /// <summary>Long range, fragile. Beats Armor.</summary>
        Artillery,

        /// <summary>Tough, short range. Beats Infantry.</summary>
        Armor,
    }

    /// <summary>The counter triangle: Infantry beats Artillery, Artillery beats Armor, Armor beats Infantry.</summary>
    public static class Counters
    {
        /// <summary>True when <paramref name="attacker"/> deals bonus damage to <paramref name="defender"/>.</summary>
        public static bool Beats(UnitClass attacker, UnitClass defender)
        {
            switch (attacker)
            {
                case UnitClass.Infantry: return defender == UnitClass.Artillery;
                case UnitClass.Artillery: return defender == UnitClass.Armor;
                case UnitClass.Armor: return defender == UnitClass.Infantry;
                default: return false;
            }
        }

        /// <summary>The class that <paramref name="unitClass"/> is weak against.</summary>
        public static UnitClass CounteredBy(UnitClass unitClass)
        {
            switch (unitClass)
            {
                case UnitClass.Infantry: return UnitClass.Armor;
                case UnitClass.Artillery: return UnitClass.Infantry;
                default: return UnitClass.Artillery;
            }
        }
    }

    /// <summary>
    /// Level-1 battle stats of a unit type. All values are integers so the simulation is identical on every device.
    /// Distances are in milli-units (1000 = one formation cell), times in simulation ticks or seconds.
    /// </summary>
    public sealed class UnitDefinition
    {
        /// <summary>Item id on the merge board (e.g. "infantry").</summary>
        public string Id { get; set; } = "";

        /// <summary>Counter class.</summary>
        public UnitClass Class { get; set; }

        /// <summary>Hit points at level 1.</summary>
        public long BaseHp { get; set; }

        /// <summary>Damage per attack at level 1.</summary>
        public long BaseAttack { get; set; }

        /// <summary>Attack reach in milli-units.</summary>
        public int Range { get; set; }

        /// <summary>Simulation ticks between attacks.</summary>
        public int AttackIntervalTicks { get; set; }

        /// <summary>Movement in milli-units per second.</summary>
        public int MoveSpeed { get; set; }

        /// <summary>HP and attack multiplier per level, in per-mille (1900 = ×1.9 per merge).</summary>
        public int GrowthPerMille { get; set; }

        /// <summary>Stats at <paramref name="level"/>, using integer growth so every platform gets the same numbers.</summary>
        public UnitStats StatsAt(int level)
        {
            if (level < 1) throw new ArgumentOutOfRangeException(nameof(level));
            var hp = BaseHp;
            var attack = BaseAttack;
            for (var i = 1; i < level; i++)
            {
                hp = Grow(hp);
                attack = Grow(attack);
            }
            return new UnitStats(Class, hp, attack, Range, AttackIntervalTicks, MoveSpeed);
        }

        private long Grow(long value)
        {
            // Saturate instead of overflowing for absurd levels.
            if (value > long.MaxValue / Math.Max(1, GrowthPerMille)) return long.MaxValue / Permille.Scale;
            return Math.Max(value + 1, value * GrowthPerMille / Permille.Scale);
        }
    }

    /// <summary>Per-mille helpers (1000 = 100%).</summary>
    public static class Permille
    {
        /// <summary>The value meaning 100%.</summary>
        public const int Scale = 1000;

        /// <summary><paramref name="value"/> × <paramref name="permille"/> / 1000, saturating.</summary>
        public static long Apply(long value, int permille)
        {
            if (permille != 0 && Math.Abs(value) > long.MaxValue / Math.Abs(permille)) return long.MaxValue / Scale;
            return value * permille / Scale;
        }
    }

    /// <summary>Battle stats of one unit at a given level.</summary>
    public readonly struct UnitStats
    {
        /// <summary>Creates stats.</summary>
        public UnitStats(UnitClass unitClass, long hp, long attack, int range, int attackIntervalTicks, int moveSpeed)
        {
            Class = unitClass;
            Hp = hp;
            Attack = attack;
            Range = range;
            AttackIntervalTicks = Math.Max(1, attackIntervalTicks);
            MoveSpeed = moveSpeed;
        }

        /// <summary>Counter class.</summary>
        public UnitClass Class { get; }

        /// <summary>Hit points.</summary>
        public long Hp { get; }

        /// <summary>Damage per attack.</summary>
        public long Attack { get; }

        /// <summary>Reach in milli-units.</summary>
        public int Range { get; }

        /// <summary>Ticks between attacks.</summary>
        public int AttackIntervalTicks { get; }

        /// <summary>Milli-units per second.</summary>
        public int MoveSpeed { get; }

        /// <summary>
        /// Rough strength for the planning screen's power comparison only: HP × damage per tick, square-rooted
        /// to keep numbers readable. It ignores counters, range and formation, which is why it is labelled a rough guide.
        /// </summary>
        public long Power
        {
            get
            {
                var dps = Attack * Permille.Scale / AttackIntervalTicks;
                var product = (double)Hp * dps;
                return (long)Math.Sqrt(product);
            }
        }
    }
}

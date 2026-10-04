namespace Game.Logic.Battle
{
    /// <summary>A unit during a battle. Positions are integer milli-units; the player's side is at negative Y.</summary>
    public sealed class BattleUnit
    {
        internal BattleUnit(int id, Side side, string typeId, int level, FormationRow row, int column, UnitStats stats, int x, int y)
        {
            Id = id;
            Side = side;
            TypeId = typeId;
            Level = level;
            Row = row;
            Column = column;
            Stats = stats;
            X = x;
            Y = y;
            Hp = stats.Hp;
            TargetId = -1;
        }

        /// <summary>Unique id within the battle (creation order).</summary>
        public int Id { get; }

        /// <summary>Owning army.</summary>
        public Side Side { get; }

        /// <summary>Unit type id.</summary>
        public string TypeId { get; }

        /// <summary>Level.</summary>
        public int Level { get; }

        /// <summary>Formation row it started in. Decides who is protected.</summary>
        public FormationRow Row { get; }

        /// <summary>Formation column it started in.</summary>
        public int Column { get; }

        /// <summary>Stats for its level.</summary>
        public UnitStats Stats { get; }

        /// <summary>Counter class.</summary>
        public UnitClass Class => Stats.Class;

        /// <summary>Position across the field, milli-units.</summary>
        public int X { get; internal set; }

        /// <summary>Position along the field (player negative, enemy positive), milli-units.</summary>
        public int Y { get; internal set; }

        /// <summary>Current hit points.</summary>
        public long Hp { get; internal set; }

        /// <summary>True while HP is above zero.</summary>
        public bool Alive => Hp > 0;

        /// <summary>Ticks until it may attack again.</summary>
        public int Cooldown { get; internal set; }

        /// <summary>Id of its current target, or -1.</summary>
        public int TargetId { get; internal set; }
    }
}

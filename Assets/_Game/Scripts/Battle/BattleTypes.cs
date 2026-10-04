using System.Collections.Generic;

namespace Game.Logic.Battle
{
    /// <summary>Which army a unit belongs to.</summary>
    public enum Side
    {
        /// <summary>The player's army, at the bottom of the screen.</summary>
        Player,

        /// <summary>The enemy army, at the top.</summary>
        Enemy,
    }

    /// <summary>How a battle ended.</summary>
    public enum BattleOutcome
    {
        /// <summary>Still running.</summary>
        InProgress,

        /// <summary>Every enemy unit was destroyed.</summary>
        Victory,

        /// <summary>Every player unit was destroyed, or time ran out.</summary>
        Defeat,
    }

    /// <summary>Kinds of things that happen in a tick, for views and loss analysis.</summary>
    public enum BattleEventType
    {
        /// <summary>A unit hit another.</summary>
        Attack,

        /// <summary>A unit was destroyed.</summary>
        Death,
    }

    /// <summary>Something that happened during a battle tick.</summary>
    public readonly struct BattleEvent
    {
        /// <summary>Creates an event.</summary>
        public BattleEvent(int tick, BattleEventType type, int sourceId, int targetId, long amount, bool countered)
        {
            Tick = tick;
            Type = type;
            SourceId = sourceId;
            TargetId = targetId;
            Amount = amount;
            Countered = countered;
        }

        /// <summary>Tick it happened on.</summary>
        public int Tick { get; }

        /// <summary>What happened.</summary>
        public BattleEventType Type { get; }

        /// <summary>Acting unit (the attacker, or the killer for deaths).</summary>
        public int SourceId { get; }

        /// <summary>Affected unit.</summary>
        public int TargetId { get; }

        /// <summary>Damage dealt (attacks).</summary>
        public long Amount { get; }

        /// <summary>True when the counter bonus applied.</summary>
        public bool Countered { get; }
    }

    /// <summary>Everything needed to run one battle. Same setup = same result.</summary>
    public sealed class BattleSetup
    {
        /// <summary>The player's formation.</summary>
        public Formation PlayerFormation { get; set; } = new Formation();

        /// <summary>The enemy's formation.</summary>
        public Formation EnemyFormation { get; set; } = new Formation();

        /// <summary>Seed for every random roll in the battle.</summary>
        public long Seed { get; set; }

        /// <summary>The battle is lost when this many seconds pass without a victory.</summary>
        public int TimeLimitSeconds { get; set; }

        /// <summary>Star objective: win within this many seconds (0 = no time star).</summary>
        public int FastWinSeconds { get; set; }
    }

    /// <summary>The final result of a battle.</summary>
    public sealed class BattleResult
    {
        /// <summary>Most stars a battle can earn.</summary>
        public const int MaxStars = 3;

        /// <summary>Victory or defeat.</summary>
        public BattleOutcome Outcome { get; set; }

        /// <summary>Ticks simulated.</summary>
        public int Ticks { get; set; }

        /// <summary>Battle length in seconds (rounded down).</summary>
        public int Seconds { get; set; }

        /// <summary>True when the battle ended because time ran out.</summary>
        public bool TimedOut { get; set; }

        /// <summary>Player units deployed.</summary>
        public int PlayerUnits { get; set; }

        /// <summary>Player units destroyed.</summary>
        public int PlayerLosses { get; set; }

        /// <summary>Stars earned (0 on defeat; 1 for a win, +1 with no losses, +1 for a fast win).</summary>
        public int Stars { get; set; }

        /// <summary>Which star objectives were met, in order: win, no losses, fast win.</summary>
        public bool[] Objectives { get; set; } = new bool[MaxStars];

        /// <summary>
        /// Player units destroyed, counted by (killer class, victim class). Used for the loss hint
        /// ("Your Infantry was destroyed by Armor...").
        /// </summary>
        public Dictionary<(UnitClass Killer, UnitClass Victim), int> PlayerDeathsBy { get; } =
            new Dictionary<(UnitClass, UnitClass), int>();

        /// <summary>A short fingerprint of the whole battle, for determinism checks.</summary>
        public ulong Checksum { get; set; }
    }
}

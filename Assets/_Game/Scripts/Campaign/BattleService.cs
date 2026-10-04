using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core.Events;
using Game.Logic.Battle;
using Game.Logic.Economy;
using Game.Logic.Merge;

namespace Game.Logic.Campaign
{
    /// <summary>A unit on the merge board that can be deployed.</summary>
    public readonly struct RosterUnit
    {
        /// <summary>Creates a roster entry.</summary>
        public RosterUnit(Cell cell, BoardItem item, UnitStats stats)
        {
            Cell = cell;
            Item = item;
            Stats = stats;
        }

        /// <summary>Where it sits on the board.</summary>
        public Cell Cell { get; }

        /// <summary>The board item.</summary>
        public BoardItem Item { get; }

        /// <summary>Its battle stats.</summary>
        public UnitStats Stats { get; }
    }

    /// <summary>Why a battle could not start.</summary>
    public enum BattleStartFailure
    {
        /// <summary>No such level.</summary>
        UnknownLevel,

        /// <summary>The previous level has not been won yet.</summary>
        Locked,

        /// <summary>No units placed in the formation.</summary>
        EmptyFormation,
    }

    /// <summary>What a finished battle paid out.</summary>
    public readonly struct BattleReward
    {
        /// <summary>Creates a reward summary.</summary>
        public BattleReward(long gold, long gems, int newStars)
        {
            Gold = gold;
            Gems = gems;
            NewStars = newStars;
        }

        /// <summary>Gold paid.</summary>
        public long Gold { get; }

        /// <summary>Gems paid for first-time stars.</summary>
        public long Gems { get; }

        /// <summary>Stars above the previous best.</summary>
        public int NewStars { get; }
    }

    /// <summary>A battle finished and its rewards were paid.</summary>
    public readonly struct BattleFinished
    {
        /// <summary>Creates the event.</summary>
        public BattleFinished(string levelId, BattleResult result, BattleReward reward)
        {
            LevelId = levelId;
            Result = result;
            Reward = reward;
        }

        /// <summary>Level fought.</summary>
        public string LevelId { get; }

        /// <summary>Outcome and stars.</summary>
        public BattleResult Result { get; }

        /// <summary>What was paid.</summary>
        public BattleReward Reward { get; }
    }

    /// <summary>
    /// Connects the merge board, formations, the simulation, campaign progress and the wallet. Units are never lost
    /// in battle: deploying a board unit only references it.
    /// </summary>
    public sealed class BattleService
    {
        private readonly BattleConfig _battle;
        private readonly CampaignConfig _campaign;
        private readonly CampaignProgress _progress;
        private readonly MergeBoardService _board;
        private readonly CurrencyWallet _wallet;
        private readonly EventBus _bus;
        private readonly Dictionary<string, Formation> _formations = new Dictionary<string, Formation>();

        /// <summary>Creates the service.</summary>
        public BattleService(BattleConfig battle, CampaignConfig campaign, CampaignProgress progress, MergeBoardService board,
            CurrencyWallet wallet, EventBus bus)
        {
            _battle = battle ?? throw new ArgumentNullException(nameof(battle));
            _campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
            _progress = progress ?? throw new ArgumentNullException(nameof(progress));
            _board = board ?? throw new ArgumentNullException(nameof(board));
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        }

        /// <summary>Battle rules.</summary>
        public BattleConfig Config => _battle;

        /// <summary>Campaign levels.</summary>
        public CampaignConfig Campaign => _campaign;

        /// <summary>Stars and unlocks.</summary>
        public CampaignProgress Progress => _progress;

        /// <summary>Deployable units on the board, strongest first (ties in board order).</summary>
        public List<RosterUnit> Roster()
        {
            var roster = new List<RosterUnit>();
            foreach (var pair in _board.Board.Items())
            {
                if (_battle.TryGetUnit(pair.Value.TypeId, out var def))
                    roster.Add(new RosterUnit(pair.Key, pair.Value, def.StatsAt(pair.Value.Level)));
            }
            return roster.OrderByDescending(u => u.Stats.Power).ToList();
        }

        /// <summary>
        /// The player's formation for <paramref name="levelId"/>. It is remembered for the session, so Retry and
        /// coming back keep it; entries whose board unit is gone are dropped.
        /// </summary>
        public Formation FormationFor(string levelId)
        {
            if (!_formations.TryGetValue(levelId, out var formation))
            {
                formation = new Formation();
                _formations[levelId] = formation;
            }
            formation.RemoveMissing(_board.Board);
            return formation;
        }

        /// <summary>
        /// Fills the formation with the strongest units up to the limit: Armor and Infantry from the front row back,
        /// Artillery from the back row forward, centre lanes first.
        /// </summary>
        public void AutoFill(string levelId)
        {
            var level = _campaign.Find(levelId);
            if (level == null) return;
            FormationFor(levelId).Clear();
            foreach (var unit in Roster().Take(level.DeploymentLimit)) PlaceInFreeSlot(levelId, unit);
        }

        /// <summary>
        /// Places <paramref name="unit"/> in the first free slot that suits its class (Artillery at the back, others at
        /// the front, centre lanes first). Returns false when the formation is full or at the deployment limit.
        /// </summary>
        public bool PlaceInFreeSlot(string levelId, RosterUnit unit)
        {
            var level = _campaign.Find(levelId);
            if (level == null) return false;
            var formation = FormationFor(levelId);
            if (formation.Uses(unit.Cell) || formation.Count >= level.DeploymentLimit) return false;
            var backFirst = unit.Stats.Class == UnitClass.Artillery;
            for (var r = 0; r < Formation.RowCount; r++)
            {
                var row = (FormationRow)(backFirst ? Formation.RowCount - 1 - r : r);
                foreach (var column in CentreOutLanes)
                {
                    if (formation.Get(row, column).HasValue) continue;
                    return formation.Place(new FormationEntry(row, column, unit.Item.TypeId, unit.Item.Level, unit.Cell), level.DeploymentLimit);
                }
            }
            return false;
        }

        /// <summary>
        /// Moves the unit in slot <paramref name="from"/> to slot <paramref name="to"/>, swapping with whatever is there.
        /// </summary>
        public bool MoveOrSwap(string levelId, (FormationRow Row, int Column) from, (FormationRow Row, int Column) to)
        {
            var formation = FormationFor(levelId);
            var moving = formation.Get(from.Row, from.Column);
            if (!moving.HasValue || !Formation.IsValidSlot(to.Row, to.Column)) return false;
            if (from == to) return true;
            var other = formation.Get(to.Row, to.Column);
            formation.Remove(from.Row, from.Column);
            formation.Remove(to.Row, to.Column);
            var m = moving.Value;
            formation.Place(new FormationEntry(to.Row, to.Column, m.UnitId, m.Level, m.SourceCell), int.MaxValue);
            if (other.HasValue)
            {
                var o = other.Value;
                formation.Place(new FormationEntry(from.Row, from.Column, o.UnitId, o.Level, o.SourceCell), int.MaxValue);
            }
            return true;
        }

        /// <summary>Checks whether a battle on <paramref name="levelId"/> can start now.</summary>
        public BattleStartFailure? CanStart(string levelId)
        {
            if (_campaign.Find(levelId) == null) return BattleStartFailure.UnknownLevel;
            if (!_progress.IsUnlocked(levelId)) return BattleStartFailure.Locked;
            if (FormationFor(levelId).Count == 0) return BattleStartFailure.EmptyFormation;
            return null;
        }

        /// <summary>Builds the simulation input for a level with the current formation.</summary>
        public BattleSetup CreateSetup(string levelId)
        {
            var level = _campaign.Find(levelId) ?? throw new ArgumentException($"Unknown level '{levelId}'.", nameof(levelId));
            return new BattleSetup
            {
                PlayerFormation = new Formation(FormationFor(levelId).Entries),
                EnemyFormation = level.CreateEnemyFormation(),
                Seed = level.Seed,
                TimeLimitSeconds = level.TimeLimitSeconds,
                FastWinSeconds = level.FastWinSeconds,
            };
        }

        /// <summary>Starts a simulation, or returns null when <see cref="CanStart"/> fails.</summary>
        public BattleSimulation Start(string levelId) =>
            CanStart(levelId) == null ? new BattleSimulation(_battle, CreateSetup(levelId)) : null;

        /// <summary>Records stars and pays rewards for a finished battle. Defeats pay nothing.</summary>
        public BattleReward Complete(string levelId, BattleResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            var level = _campaign.Find(levelId) ?? throw new ArgumentException($"Unknown level '{levelId}'.", nameof(levelId));
            var reward = new BattleReward(0, 0, 0);
            if (result.Outcome == BattleOutcome.Victory)
            {
                var newStars = _progress.Record(levelId, result.Stars);
                var gems = newStars * _campaign.GemsPerNewStar;
                _wallet.Add(Currency.Gold, level.RewardGold);
                _wallet.Add(Currency.Gems, gems);
                reward = new BattleReward(level.RewardGold, gems, newStars);
            }
            _bus.Publish(new BattleFinished(levelId, result, reward));
            return reward;
        }

        /// <summary>Lane order used when filling a row: centre first, then alternating outwards.</summary>
        private static readonly List<int> CentreOutLanes = BuildCentreOutLanes();

        private static List<int> BuildCentreOutLanes()
        {
            var centre = (Formation.ColumnCount - 1) / 2;
            var lanes = new List<int> { centre };
            for (var offset = 1; lanes.Count < Formation.ColumnCount; offset++)
            {
                if (centre - offset >= 0) lanes.Add(centre - offset);
                if (centre + offset < Formation.ColumnCount) lanes.Add(centre + offset);
            }
            return lanes;
        }
    }
}

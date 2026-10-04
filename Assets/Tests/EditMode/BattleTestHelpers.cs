using System.Collections.Generic;
using Game.Core.Events;
using Game.Core.Random;
using Game.Logic.Battle;
using Game.Logic.Campaign;
using Game.Logic.Economy;
using Game.Logic.Merge;

namespace Game.Tests
{
    /// <summary>Battle configs and rigs shared by the battle tests.</summary>
    internal static class BattleTestHelpers
    {
        public const int TickRate = 20;

        /// <summary>The shipped default numbers, so tests also cover realistic battles.</summary>
        public static BattleConfig CreateConfig(int variance = 50) => new BattleConfig
        {
            TickRate = TickRate,
            CounterBonusPerMille = 1500,
            DamageVariancePerMille = variance,
            LaneSpacing = 1000,
            RowSpacing = 1000,
            FrontOffset = 2500,
        }
            .AddUnit(new UnitDefinition { Id = "infantry", Class = UnitClass.Infantry, BaseHp = 100, BaseAttack = 12, Range = 1200, AttackIntervalTicks = 20, MoveSpeed = 1500, GrowthPerMille = 1900 })
            .AddUnit(new UnitDefinition { Id = "artillery", Class = UnitClass.Artillery, BaseHp = 60, BaseAttack = 18, Range = 5000, AttackIntervalTicks = 30, MoveSpeed = 800, GrowthPerMille = 1900 })
            .AddUnit(new UnitDefinition { Id = "armor", Class = UnitClass.Armor, BaseHp = 200, BaseAttack = 10, Range = 1500, AttackIntervalTicks = 25, MoveSpeed = 1000, GrowthPerMille = 1900 });

        public static FormationEntry Unit(FormationRow row, int column, string id, int level = 1) =>
            new FormationEntry(row, column, id, level);

        public static Formation Formation(params FormationEntry[] entries) => new Formation(entries);

        public static BattleSetup Setup(Formation player, Formation enemy, long seed = 7, int timeLimit = 120, int fastWin = 30) => new BattleSetup
        {
            PlayerFormation = player,
            EnemyFormation = enemy,
            Seed = seed,
            TimeLimitSeconds = timeLimit,
            FastWinSeconds = fastWin,
        };

        public static CampaignConfig CreateCampaign() => new CampaignConfig
        {
            GemsPerNewStar = 2,
            Levels = new List<LevelDefinition>
            {
                new LevelDefinition
                {
                    Id = "1-1", Name = "First", DeploymentLimit = 3, TimeLimitSeconds = 90, FastWinSeconds = 60, RewardGold = 100, Seed = 11,
                    Enemies = new List<FormationEntry> { Unit(FormationRow.Front, 2, "infantry") },
                },
                new LevelDefinition
                {
                    Id = "1-2", Name = "Second", DeploymentLimit = 3, TimeLimitSeconds = 90, FastWinSeconds = 30, RewardGold = 200, Seed = 12,
                    Enemies = new List<FormationEntry> { Unit(FormationRow.Front, 2, "armor", 6) },
                },
            },
        };
    }

    /// <summary>Board, wallet, progress and battle service wired together.</summary>
    internal sealed class BattleRig
    {
        public readonly EventBus Bus = new EventBus();
        public readonly CurrencyWallet Wallet;
        public readonly MergeBoardService Board;
        public readonly CampaignConfig Campaign = BattleTestHelpers.CreateCampaign();
        public readonly CampaignProgress Progress;
        public readonly BattleService Service;
        public readonly List<BattleFinished> Finished = new List<BattleFinished>();

        public BattleRig()
        {
            Wallet = new CurrencyWallet(Bus, 0, 0);
            Board = new MergeBoardService(new MergeConfig(4, 5, 6, 7, 2, new[]
            {
                new ItemDefinition("infantry", ItemKind.Unit, 30, 100, 1, 2),
                new ItemDefinition("artillery", ItemKind.Unit, 30, 100, 1, 2),
                new ItemDefinition("armor", ItemKind.Unit, 30, 100, 1, 2),
                new ItemDefinition("goldmine", ItemKind.Building, 30, 100, 1, 2),
            }), Bus, new DeterministicRandom(1), Wallet);
            Progress = new CampaignProgress(Campaign, Bus);
            Service = new BattleService(BattleTestHelpers.CreateConfig(), Campaign, Progress, Board, Wallet, Bus);
            Bus.Subscribe<BattleFinished>(e => Finished.Add(e));
        }

        public Cell Put(int column, int row, string type, int level)
        {
            var cell = new Cell(column, row);
            Board.Board.Set(cell, new BoardItem(type, level));
            return cell;
        }
    }
}

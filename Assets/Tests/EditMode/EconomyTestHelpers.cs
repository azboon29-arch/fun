using System;
using System.Collections.Generic;
using Game.Core.Events;
using Game.Core.Random;
using Game.Logic.Base;
using Game.Logic.Economy;
using Game.Logic.Merge;

namespace Game.Tests
{
    /// <summary>Wallet, board, economy and HQ wired together with simple round numbers.</summary>
    internal sealed class EconomyRig
    {
        public static readonly DateTime Start = new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

        public readonly EventBus Bus = new EventBus();
        public readonly EconomyConfig Config = CreateConfig();
        public readonly HqConfig HqConfig = CreateHqConfig();
        public readonly CurrencyWallet Wallet;
        public readonly MergeBoardService Board;
        public readonly EconomyService Economy;
        public readonly HqService Hq;
        public readonly List<OfflineEarnings> OfflineEvents = new List<OfflineEarnings>();
        public DateTime Now = Start;

        public EconomyRig(long startingGold = 0)
        {
            Wallet = new CurrencyWallet(Bus, startingGold, 5);
            Board = new MergeBoardService(CreateMergeConfig(), Bus, new DeterministicRandom(1), Wallet);
            Economy = new EconomyService(Config, Wallet, Board, Bus);
            Hq = new HqService(HqConfig, Wallet, Board, Bus);
            Bus.Subscribe<OfflineEarnings>(e => OfflineEvents.Add(e));
        }

        public static EconomyConfig CreateConfig() => new EconomyConfig
        {
            GoldMineId = "goldmine",
            BarracksId = "barracks",
            WarehouseId = "warehouse",
            MineBaseIncomePerSecond = 1,
            MineIncomeGrowthPerLevel = 2,
            BarracksDiscountPerLevel = 0.1,
            BarracksMaxDiscount = 0.5,
            OfflineBaseCapHours = 8,
            WarehouseHoursPerLevel = 1,
            OfflineMaxCapHours = 12,
            OfflineIncomeMultiplier = 1,
            OfflineThresholdSeconds = 60,
            StartingGold = 0,
            StartingGems = 5,
        };

        public static HqConfig CreateHqConfig() => new HqConfig(new[]
        {
            new HqLevel(0, 4, 5, 3),
            new HqLevel(1000, 5, 5, 5),
            new HqLevel(5000, 6, 7, 8, new[] { "shield" }),
        });

        public static MergeConfig CreateMergeConfig() => new MergeConfig(4, 5, 6, 7, 2, new[]
        {
            new ItemDefinition("infantry", ItemKind.Unit, 30, 100, 1, 2),
            new ItemDefinition("goldmine", ItemKind.Building, 30, 100, 1, 2),
            new ItemDefinition("barracks", ItemKind.Building, 30, 100, 1, 2),
            new ItemDefinition("warehouse", ItemKind.Building, 30, 100, 1, 2),
        });

        public void Put(int column, int row, string type, int level) =>
            Board.Board.Set(new Cell(column, row), new BoardItem(type, level));

        /// <summary>Advances the fake clock and ticks the economy.</summary>
        public void Advance(double seconds)
        {
            Now = Now.AddSeconds(seconds);
            Economy.Tick(Now);
        }

        /// <summary>Ticks in many small steps, like frames during play.</summary>
        public void Play(double seconds, double step)
        {
            for (var t = 0d; t < seconds - 1e-9; t += step) Advance(step);
        }
    }
}

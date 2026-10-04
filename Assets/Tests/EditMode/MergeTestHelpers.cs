using System.Collections.Generic;
using Game.Core.Events;
using Game.Core.Random;
using Game.Logic.Merge;

namespace Game.Tests
{
    /// <summary>Wallet with a fixed balance, for purchase tests.</summary>
    internal sealed class TestWallet : IGoldWallet
    {
        public TestWallet(long gold) { Gold = gold; }

        public long Gold { get; private set; }

        public bool TrySpend(long amount)
        {
            if (amount < 0 || amount > Gold) return false;
            Gold -= amount;
            return true;
        }
    }

    /// <summary>A merge board service with a small test config, a seeded RNG and recorded events.</summary>
    internal sealed class MergeRig
    {
        public const int MaxLevel = 5;

        public readonly EventBus Bus = new EventBus();
        public readonly MergeConfig Config;
        public readonly MergeBoardService Service;
        public readonly List<object> Events = new List<object>();
        public int StateChanges;

        public MergeRig(IGoldWallet wallet = null, int columns = 4, int rows = 5, int maxColumns = 6, int maxRows = 7, int buyOffset = 2, long seed = 42)
        {
            Config = CreateConfig(columns, rows, maxColumns, maxRows, buyOffset);
            Service = new MergeBoardService(Config, Bus, new DeterministicRandom(seed), wallet ?? new UnlimitedWallet());
            Bus.Subscribe<ItemMerged>(e => Events.Add(e));
            Bus.Subscribe<ItemBought>(e => Events.Add(e));
            Bus.Subscribe<BuyFailed>(e => Events.Add(e));
            Bus.Subscribe<ItemDragged>(e => Events.Add(e));
            Bus.Subscribe<GameStateChanged>(_ => StateChanges++);
        }

        public BoardModel Board => Service.Board;

        public static MergeConfig CreateConfig(int columns = 4, int rows = 5, int maxColumns = 6, int maxRows = 7, int buyOffset = 2)
        {
            return new MergeConfig(columns, rows, maxColumns, maxRows, buyOffset, new[]
            {
                new ItemDefinition("infantry", ItemKind.Unit, MaxLevel, 100, 1.5, 2),
                new ItemDefinition("armor", ItemKind.Unit, MaxLevel, 200, 1.5, 2),
                new ItemDefinition("mine", ItemKind.Building, 3, 50, 2, 2),
            });
        }

        public void Put(int column, int row, string type, int level) =>
            Service.Board.Set(new Cell(column, row), new BoardItem(type, level));

        public BoardItem? At(int column, int row) => Service.Board.Get(new Cell(column, row));

        public void Fill(string type = "armor", int level = 1)
        {
            foreach (var cell in Service.Board.EmptyCells()) Service.Board.Set(cell, new BoardItem(type, level));
        }
    }
}

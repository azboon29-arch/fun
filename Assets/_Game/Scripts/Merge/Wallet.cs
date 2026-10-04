namespace Game.Logic.Merge
{
    /// <summary>Adjusts purchase prices, e.g. the Barracks discount on units.</summary>
    public interface IBuyCostModifier
    {
        /// <summary>Multiplier applied to the base price of <paramref name="item"/> (1 = unchanged, 0.8 = 20% off).</summary>
        double CostMultiplier(ItemDefinition item);
    }

    /// <summary>Pays for purchases.</summary>
    public interface IGoldWallet
    {
        /// <summary>Gold available.</summary>
        long Gold { get; }

        /// <summary>Spends <paramref name="amount"/> if affordable. Returns false (and spends nothing) otherwise.</summary>
        bool TrySpend(long amount);
    }

    /// <summary>A wallet that can always pay. Used by tests and debug tools.</summary>
    public sealed class UnlimitedWallet : IGoldWallet
    {
        /// <inheritdoc />
        public long Gold => long.MaxValue;

        /// <inheritdoc />
        public bool TrySpend(long amount) => amount >= 0;
    }
}

namespace Game.Logic.Merge
{
    /// <summary>Pays for purchases. Phase 3 replaces the stub with the real gold wallet.</summary>
    public interface IGoldWallet
    {
        /// <summary>Gold available.</summary>
        long Gold { get; }

        /// <summary>Spends <paramref name="amount"/> if affordable. Returns false (and spends nothing) otherwise.</summary>
        bool TrySpend(long amount);
    }

    /// <summary>A wallet that can always pay. Used until the economy exists (Phase 3).</summary>
    public sealed class UnlimitedWallet : IGoldWallet
    {
        /// <inheritdoc />
        public long Gold => long.MaxValue;

        /// <inheritdoc />
        public bool TrySpend(long amount) => amount >= 0;
    }
}

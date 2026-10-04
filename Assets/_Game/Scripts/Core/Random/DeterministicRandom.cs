using System;

namespace Game.Core.Random
{
    /// <summary>Source of random numbers. Game logic only uses this, never <see cref="System.Random"/>, so results are reproducible.</summary>
    public interface IRandom
    {
        /// <summary>Returns an integer in [0, <paramref name="maxExclusive"/>).</summary>
        int Next(int maxExclusive);

        /// <summary>Returns a double in [0, 1).</summary>
        double NextDouble();
    }

    /// <summary>
    /// Seeded xorshift64* generator. Same seed gives the same sequence on every platform and runtime,
    /// which the deterministic battle simulation and replays rely on.
    /// </summary>
    public sealed class DeterministicRandom : IRandom
    {
        private const ulong Multiplier = 2685821657736338717UL;
        private const ulong SeedMixer = 0x9E3779B97F4A7C15UL;
        private const int DoubleBits = 53;

        private ulong _state;

        /// <summary>Creates a generator from <paramref name="seed"/>. Any seed is valid, including 0.</summary>
        public DeterministicRandom(long seed)
        {
            // Mix the seed so nearby seeds give unrelated sequences and the state is never zero.
            var z = unchecked((ulong)seed + SeedMixer);
            z = unchecked((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL);
            z = unchecked((z ^ (z >> 27)) * 0x94D049BB133111EBUL);
            z ^= z >> 31;
            _state = z == 0 ? SeedMixer : z;
        }

        /// <summary>The internal state. Store it to continue the same sequence later (see <see cref="FromState"/>).</summary>
        public ulong State => _state;

        /// <summary>Recreates a generator from a stored <see cref="State"/>.</summary>
        public static DeterministicRandom FromState(ulong state)
        {
            if (state == 0) throw new ArgumentOutOfRangeException(nameof(state), "State must not be zero.");
            var random = new DeterministicRandom(0);
            random._state = state;
            return random;
        }

        /// <inheritdoc />
        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            return (int)(NextULong() % (ulong)maxExclusive);
        }

        /// <inheritdoc />
        public double NextDouble() => (NextULong() >> (64 - DoubleBits)) * (1.0 / (1UL << DoubleBits));

        private ulong NextULong()
        {
            _state ^= _state >> 12;
            _state ^= _state << 25;
            _state ^= _state >> 27;
            return unchecked(_state * Multiplier);
        }
    }
}

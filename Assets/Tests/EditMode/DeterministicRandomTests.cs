using System;
using Game.Core.Random;
using NUnit.Framework;

namespace Game.Tests
{
    public class DeterministicRandomTests
    {
        [Test]
        public void SameSeed_SameSequence()
        {
            var a = new DeterministicRandom(123);
            var b = new DeterministicRandom(123);
            for (var i = 0; i < 1000; i++) Assert.AreEqual(a.Next(1000), b.Next(1000));
        }

        [Test]
        public void KnownSequence_IsStableAcrossRuntimes()
        {
            // Pinned values: if these change, saved replays and balance results would change too.
            var random = new DeterministicRandom(2026);
            var values = new int[5];
            for (var i = 0; i < values.Length; i++) values[i] = random.Next(1_000_000);
            CollectionAssert.AreEqual(Expected, values);
        }

        // Cross-checked against an independent Python implementation of the same xorshift64* generator.
        private static readonly int[] Expected = { 613288, 451849, 885377, 43193, 775004 };

        [Test]
        public void FromState_ContinuesSequence()
        {
            var a = new DeterministicRandom(9);
            a.Next(10);
            var b = DeterministicRandom.FromState(a.State);
            for (var i = 0; i < 100; i++) Assert.AreEqual(a.NextDouble(), b.NextDouble());
        }

        [Test]
        public void Outputs_StayInRangeAndCoverIt()
        {
            var random = new DeterministicRandom(0);
            var seen = new bool[6];
            for (var i = 0; i < 1000; i++)
            {
                var n = random.Next(6);
                Assert.That(n, Is.InRange(0, 5));
                seen[n] = true;
                var d = random.NextDouble();
                Assert.That(d, Is.GreaterThanOrEqualTo(0.0).And.LessThan(1.0));
            }
            CollectionAssert.DoesNotContain(seen, false);
        }

        [Test]
        public void Next_RejectsNonPositiveBound()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DeterministicRandom(1).Next(0));
        }
    }
}

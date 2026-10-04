using Game.Logic.Merge;
using NUnit.Framework;

namespace Game.Tests
{
    public class MergeConfigTests
    {
        [Test]
        public void TestConfig_IsValid()
        {
            CollectionAssert.IsEmpty(MergeRig.CreateConfig().Validate());
        }

        [Test]
        public void Validate_ReportsProblems()
        {
            var config = new MergeConfig(4, 5, 3, 5, -1, new[]
            {
                new ItemDefinition("a", ItemKind.Unit, 0, -1, 0.5, 0.5),
                new ItemDefinition("a", ItemKind.Unit, 5, 1, 1, 1),
                new ItemDefinition("", ItemKind.Building, 5, 1, 1, 1),
            });

            var errors = config.Validate();

            Assert.That(errors, Has.Some.Contains("Max board size"));
            Assert.That(errors, Has.Some.Contains("offset"));
            Assert.That(errors, Has.Some.Contains("max level"));
            Assert.That(errors, Has.Some.Contains("base cost"));
            Assert.That(errors, Has.Some.Contains("cost growth"));
            Assert.That(errors, Has.Some.Contains("more than once"));
            Assert.That(errors, Has.Some.Contains("no id"));
        }

        [Test]
        public void CostOf_FollowsFormulaAndCapsOverflow()
        {
            var item = new ItemDefinition("x", ItemKind.Unit, 30, 100, 1.1, 2);

            Assert.AreEqual(100, item.CostOf(1, 0));
            Assert.AreEqual(110, item.CostOf(1, 1));
            Assert.AreEqual(400, item.CostOf(3, 0));
            Assert.AreEqual(long.MaxValue, item.CostOf(30, 100000));
        }
    }
}

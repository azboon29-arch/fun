using System.Collections.Generic;
using System.Linq;
using Game.Logic.Battle;
using NUnit.Framework;
using static Game.Tests.BattleTestHelpers;

namespace Game.Tests
{
    public class BattleSimulationTests
    {
        private static BattleSetup LargeMixedBattle(long seed) => Setup(
            Formation(
                Unit(FormationRow.Front, 0, "armor", 4), Unit(FormationRow.Front, 1, "infantry", 5), Unit(FormationRow.Front, 2, "armor", 5),
                Unit(FormationRow.Front, 3, "infantry", 4), Unit(FormationRow.Front, 4, "armor", 3),
                Unit(FormationRow.Middle, 1, "infantry", 3), Unit(FormationRow.Middle, 3, "infantry", 3),
                Unit(FormationRow.Back, 0, "artillery", 5), Unit(FormationRow.Back, 2, "artillery", 6), Unit(FormationRow.Back, 4, "artillery", 4)),
            Formation(
                Unit(FormationRow.Front, 0, "infantry", 5), Unit(FormationRow.Front, 1, "armor", 5), Unit(FormationRow.Front, 2, "infantry", 6),
                Unit(FormationRow.Front, 3, "armor", 4), Unit(FormationRow.Front, 4, "infantry", 4),
                Unit(FormationRow.Middle, 2, "armor", 4),
                Unit(FormationRow.Back, 1, "artillery", 5), Unit(FormationRow.Back, 2, "artillery", 5), Unit(FormationRow.Back, 3, "artillery", 5)),
            seed);

        private static string Fingerprint(BattleSimulation sim, BattleResult result) =>
            $"{result.Outcome}|{result.Ticks}|{result.Checksum}|{result.PlayerLosses}|{result.Stars}|{sim.Log.Count}|" +
            string.Join(",", sim.Units.Select(u => $"{u.Id}:{u.Hp}:{u.X}:{u.Y}"));

        [Test]
        public void SameBattle_Run100Times_GivesIdenticalResults()
        {
            var config = CreateConfig();
            string first = null;
            for (var run = 0; run < 100; run++)
            {
                var sim = new BattleSimulation(config, LargeMixedBattle(2026));
                var result = sim.RunToEnd();
                var print = Fingerprint(sim, result);
                if (first == null)
                {
                    first = print;
                    Assert.That(result.Ticks, Is.GreaterThan(TickRate), "the reference battle should last more than a second");
                    Assert.That(sim.Log.Count(e => e.Type == BattleEventType.Death), Is.GreaterThan(3));
                }
                else
                {
                    Assert.That(print, Is.EqualTo(first), $"run {run} differed");
                }
            }
        }

        [Test]
        public void SameSetup_FromFreshConfigs_GivesIdenticalResults()
        {
            var a = new BattleSimulation(CreateConfig(), LargeMixedBattle(5)).RunToEnd();
            var b = new BattleSimulation(CreateConfig(), LargeMixedBattle(5)).RunToEnd();
            Assert.That(b.Checksum, Is.EqualTo(a.Checksum));
        }

        [Test]
        public void DifferentSeeds_ChangeTheDamageRolls()
        {
            var checksums = new HashSet<ulong>();
            for (var seed = 1; seed <= 5; seed++) checksums.Add(new BattleSimulation(CreateConfig(), LargeMixedBattle(seed)).RunToEnd().Checksum);
            Assert.That(checksums.Count, Is.GreaterThan(1));
        }

        [Test]
        public void Targeting_OnlyFrontmostLivingRow_EvenWhenBackRowIsNearer()
        {
            var config = CreateConfig();
            config.LaneSpacing = 3000;
            var sim = new BattleSimulation(config, Setup(
                Formation(Unit(FormationRow.Front, 0, "infantry")),
                Formation(Unit(FormationRow.Front, 4, "infantry"), Unit(FormationRow.Back, 0, "infantry"))));
            var player = sim.Units[0];
            var enemyFront = sim.Units[1];
            var enemyBack = sim.Units[2];
            Assert.That(enemyBack.Row, Is.EqualTo(FormationRow.Back));
            Assert.That(Distance(player, enemyBack), Is.LessThan(Distance(player, enemyFront)), "test setup: back row must be nearer");

            Assert.That(sim.ChooseTarget(player), Is.SameAs(enemyFront));
            enemyFront.Hp = 0;
            Assert.That(sim.ChooseTarget(player), Is.SameAs(enemyBack));
        }

        [Test]
        public void Targeting_PicksNearestFirst()
        {
            var sim = new BattleSimulation(CreateConfig(), Setup(
                Formation(Unit(FormationRow.Front, 2, "infantry")),
                Formation(Unit(FormationRow.Front, 0, "infantry"), Unit(FormationRow.Front, 3, "infantry"))));
            Assert.That(sim.ChooseTarget(sim.Units[0]).Column, Is.EqualTo(3));
        }

        [Test]
        public void Targeting_EqualDistance_PicksLowestHpThenLowestId()
        {
            var sim = new BattleSimulation(CreateConfig(), Setup(
                Formation(Unit(FormationRow.Front, 2, "infantry")),
                Formation(Unit(FormationRow.Front, 1, "infantry"), Unit(FormationRow.Front, 3, "infantry"))));
            Assert.That(sim.ChooseTarget(sim.Units[0]).Id, Is.EqualTo(1), "tie on distance and HP goes to the lowest id");
            sim.Units[2].Hp -= 10;
            Assert.That(sim.ChooseTarget(sim.Units[0]).Id, Is.EqualTo(2));
        }

        [TestCase("infantry", "artillery", true)]
        [TestCase("artillery", "armor", true)]
        [TestCase("armor", "infantry", true)]
        [TestCase("artillery", "infantry", false)]
        [TestCase("infantry", "armor", false)]
        [TestCase("armor", "artillery", false)]
        [TestCase("infantry", "infantry", false)]
        public void Counters_AddFiftyPercentDamage(string attacker, string defender, bool countered)
        {
            var config = CreateConfig(variance: 0);
            var sim = new BattleSimulation(config, Setup(
                Formation(Unit(FormationRow.Front, 2, attacker, 10)),
                Formation(Unit(FormationRow.Front, 2, defender, 10))));
            var hit = RunUntil(sim, () => sim.Log.Any(e => e.Type == BattleEventType.Attack && e.SourceId == 0))
                .Log.First(e => e.Type == BattleEventType.Attack && e.SourceId == 0);

            config.TryGetUnit(attacker, out var def);
            var attack = def.StatsAt(10).Attack;
            var expected = countered ? attack * 3 / 2 : attack;
            Assert.That(hit.Amount, Is.EqualTo(expected));
            Assert.That(hit.Countered, Is.EqualTo(countered));
            Assert.That(Counters.Beats(def.Class, sim.Units[1].Class), Is.EqualTo(countered));
        }

        [Test]
        public void CounterBonus_IsConfigurable()
        {
            var config = CreateConfig(variance: 0);
            config.CounterBonusPerMille = 2000;
            var sim = new BattleSimulation(config, Setup(
                Formation(Unit(FormationRow.Front, 2, "armor")),
                Formation(Unit(FormationRow.Front, 2, "infantry", 10))));
            RunUntil(sim, () => sim.Log.Any(e => e.SourceId == 0));
            Assert.That(sim.Log.First(e => e.SourceId == 0).Amount, Is.EqualTo(20));
        }

        [Test]
        public void Variance_StaysWithinConfiguredSpread()
        {
            var config = CreateConfig(variance: 100);
            var sim = new BattleSimulation(config, LargeMixedBattle(3));
            sim.RunToEnd();
            foreach (var hit in sim.Log.Where(e => e.Type == BattleEventType.Attack))
            {
                var attacker = sim.Units[hit.SourceId];
                var target = sim.Units[hit.TargetId];
                var baseDamage = hit.Countered ? Permille.Apply(attacker.Stats.Attack, config.CounterBonusPerMille) : attacker.Stats.Attack;
                Assert.That(hit.Amount, Is.InRange(baseDamage * 9 / 10, baseDamage * 11 / 10 + 1), $"{attacker.TypeId} -> {target.TypeId}");
            }
        }

        [Test]
        public void LongRangeUnit_AttacksWithoutMoving_WhenTargetInRange()
        {
            var sim = new BattleSimulation(CreateConfig(), Setup(
                Formation(Unit(FormationRow.Front, 2, "artillery")),
                Formation(Unit(FormationRow.Front, 2, "armor"))));
            var artillery = sim.Units[0];
            Assert.That(Distance(artillery, sim.Units[1]), Is.LessThanOrEqualTo(artillery.Stats.Range));
            var startY = artillery.Y;
            sim.Step();
            Assert.That(artillery.Y, Is.EqualTo(startY));
            Assert.That(sim.LastTickEvents.Any(e => e.SourceId == 0 && e.Type == BattleEventType.Attack), Is.True);
        }

        [Test]
        public void MeleeUnits_CloseIn_AndOnlyAttackWithinRange()
        {
            var sim = new BattleSimulation(CreateConfig(), Setup(
                Formation(Unit(FormationRow.Front, 0, "infantry", 3)),
                Formation(Unit(FormationRow.Front, 4, "infantry", 3))));
            var a = sim.Units[0];
            var b = sim.Units[1];
            var startDistance = Distance(a, b);
            while (!sim.IsOver)
            {
                sim.Step();
                foreach (var e in sim.LastTickEvents.Where(x => x.Type == BattleEventType.Attack))
                {
                    var attacker = sim.Units[e.SourceId];
                    Assert.That(Distance(attacker, sim.Units[e.TargetId]), Is.LessThanOrEqualTo(attacker.Stats.Range));
                }
                Assert.That(a.Y, Is.LessThan(b.Y), "units never pass through each other");
            }
            Assert.That(sim.Log.Count(e => e.Type == BattleEventType.Attack), Is.GreaterThan(0));
            Assert.That(startDistance, Is.GreaterThan(a.Stats.Range));
        }

        [Test]
        public void Movement_NeverStallsJustOutsideRange()
        {
            // Odd spacing makes the straight-line steps land on awkward integer positions.
            var config = CreateConfig();
            config.LaneSpacing = 997;
            config.FrontOffset = 2503;
            foreach (var column in Enumerable.Range(0, Logic.Battle.Formation.ColumnCount))
            {
                var sim = new BattleSimulation(config, Setup(
                    Formation(Unit(FormationRow.Back, column, "armor", 20)),
                    Formation(Unit(FormationRow.Back, 4 - column, "armor", 1)), timeLimit: 60));
                var result = sim.RunToEnd();
                Assert.That(result.Outcome, Is.EqualTo(BattleOutcome.Victory), $"column {column}");
            }
        }

        [Test]
        public void TimeLimit_EndsInDefeat()
        {
            var sim = new BattleSimulation(CreateConfig(), Setup(
                Formation(Unit(FormationRow.Front, 2, "infantry")),
                Formation(Unit(FormationRow.Front, 2, "armor", 10)), timeLimit: 1));
            var result = sim.RunToEnd();
            Assert.That(result.Outcome, Is.EqualTo(BattleOutcome.Defeat));
            Assert.That(result.TimedOut, Is.True);
            Assert.That(result.Ticks, Is.EqualTo(TickRate));
            Assert.That(result.PlayerLosses, Is.Zero);
            Assert.That(result.Stars, Is.Zero);
        }

        [Test]
        public void Wipeout_IsDefeat_WithLossBreakdown()
        {
            var sim = new BattleSimulation(CreateConfig(), Setup(
                Formation(Unit(FormationRow.Front, 1, "infantry"), Unit(FormationRow.Front, 3, "infantry")),
                Formation(Unit(FormationRow.Front, 2, "armor", 8))));
            var result = sim.RunToEnd();
            Assert.That(result.Outcome, Is.EqualTo(BattleOutcome.Defeat));
            Assert.That(result.TimedOut, Is.False);
            Assert.That(result.PlayerLosses, Is.EqualTo(2));
            Assert.That(result.PlayerDeathsBy[(UnitClass.Armor, UnitClass.Infantry)], Is.EqualTo(2));
        }

        [Test]
        public void Stars_FlawlessFastWin_GetsThree()
        {
            var result = new BattleSimulation(CreateConfig(), Setup(
                Formation(Unit(FormationRow.Front, 2, "armor", 8)),
                Formation(Unit(FormationRow.Front, 2, "infantry")), fastWin: 30)).RunToEnd();
            Assert.That(result.Outcome, Is.EqualTo(BattleOutcome.Victory));
            Assert.That(result.Objectives, Is.EqualTo(new[] { true, true, true }));
            Assert.That(result.Stars, Is.EqualTo(3));
        }

        [Test]
        public void Stars_SlowWin_MissesTimeStar()
        {
            var result = new BattleSimulation(CreateConfig(), Setup(
                Formation(Unit(FormationRow.Front, 2, "armor", 8)),
                Formation(Unit(FormationRow.Front, 2, "infantry")), fastWin: 0)).RunToEnd();
            Assert.That(result.Objectives, Is.EqualTo(new[] { true, true, false }));
            Assert.That(result.Stars, Is.EqualTo(2));
        }

        [Test]
        public void Stars_WinWithLosses_MissesFlawlessStar()
        {
            var result = new BattleSimulation(CreateConfig(), Setup(
                Formation(Unit(FormationRow.Front, 2, "armor", 8), Unit(FormationRow.Front, 0, "infantry")),
                Formation(Unit(FormationRow.Back, 0, "artillery", 5)), fastWin: 60)).RunToEnd();
            Assert.That(result.Outcome, Is.EqualTo(BattleOutcome.Victory));
            Assert.That(result.PlayerLosses, Is.EqualTo(1));
            Assert.That(result.Objectives[1], Is.False);
            Assert.That(result.Stars, Is.EqualTo(2));
        }

        [Test]
        public void EmptyEnemy_IsInstantVictory_AndStepIsANoOp()
        {
            var sim = new BattleSimulation(CreateConfig(), Setup(Formation(Unit(FormationRow.Front, 2, "infantry")), Formation()));
            Assert.That(sim.IsOver, Is.True);
            Assert.That(sim.Outcome, Is.EqualTo(BattleOutcome.Victory));
            sim.Step();
            Assert.That(sim.Tick, Is.Zero);
        }

        [Test]
        public void UnknownUnitTypes_AreSkipped()
        {
            var sim = new BattleSimulation(CreateConfig(), Setup(
                Formation(Unit(FormationRow.Front, 2, "infantry"), Unit(FormationRow.Front, 3, "goldmine")),
                Formation(Unit(FormationRow.Front, 2, "infantry"))));
            Assert.That(sim.Units.Count, Is.EqualTo(2));
        }

        [Test]
        public void Stats_GrowAbout1Point9PerLevel()
        {
            var config = CreateConfig();
            config.TryGetUnit("infantry", out var def);
            Assert.That(def.StatsAt(2).Hp, Is.EqualTo(190));
            Assert.That(def.StatsAt(2).Attack, Is.EqualTo(22));
            for (var level = 2; level <= 30; level++)
            {
                var ratio = (double)def.StatsAt(level).Power / def.StatsAt(level - 1).Power;
                Assert.That(ratio, Is.InRange(1.7, 2.1), $"level {level}");
            }
            Assert.That(def.StatsAt(30).Hp, Is.GreaterThan(0), "no overflow at max level");
        }

        [TestCase(0, 0)]
        [TestCase(1, 1)]
        [TestCase(15, 3)]
        [TestCase(16, 4)]
        [TestCase(17, 4)]
        [TestCase(999999999999L, 999999)]
        [TestCase(long.MaxValue, 3037000499L)]
        public void IntegerSqrt_IsExactFloor(long value, long expected)
        {
            Assert.That(BattleSimulation.IntegerSqrt(value), Is.EqualTo(expected));
        }

        [Test]
        public void DefaultConfig_IsValid()
        {
            Assert.That(CreateConfig().Validate(), Is.Empty);
        }

        [Test]
        public void Validate_ReportsBadValues()
        {
            var config = CreateConfig();
            config.TickRate = 0;
            config.CounterBonusPerMille = 900;
            config.AddUnit(new UnitDefinition { Id = "bad", BaseHp = 0, BaseAttack = 1, Range = 0, AttackIntervalTicks = 1, GrowthPerMille = 1000 });
            Assert.That(config.Validate().Count, Is.EqualTo(4));
        }

        private static long Distance(BattleUnit a, BattleUnit b)
        {
            long dx = a.X - b.X;
            long dy = a.Y - b.Y;
            return BattleSimulation.IntegerSqrt(dx * dx + dy * dy);
        }

        private static BattleSimulation RunUntil(BattleSimulation sim, System.Func<bool> condition)
        {
            while (!condition() && !sim.IsOver) sim.Step();
            Assert.That(condition(), Is.True, "condition never met");
            return sim;
        }
    }
}

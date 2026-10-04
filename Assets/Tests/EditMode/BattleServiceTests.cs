using System.Linq;
using Game.Logic.Battle;
using Game.Logic.Campaign;
using NUnit.Framework;

namespace Game.Tests
{
    public class BattleServiceTests
    {
        [Test]
        public void Roster_ListsOnlyBattleUnits_StrongestFirst()
        {
            var rig = new BattleRig();
            rig.Put(0, 0, "infantry", 1);
            rig.Put(1, 0, "armor", 4);
            rig.Put(2, 0, "goldmine", 9);
            var roster = rig.Service.Roster();
            Assert.That(roster.Select(r => r.Item.TypeId), Is.EqualTo(new[] { "armor", "infantry" }));
        }

        [Test]
        public void AutoFill_TakesStrongestUpToLimit_ArtilleryAtTheBack()
        {
            var rig = new BattleRig();
            rig.Put(0, 0, "infantry", 1);
            rig.Put(1, 0, "armor", 5);
            rig.Put(2, 0, "artillery", 5);
            rig.Put(3, 0, "infantry", 4);
            rig.Service.AutoFill("1-1"); // limit 3
            var f = rig.Service.FormationFor("1-1");
            Assert.That(f.Count, Is.EqualTo(3));
            Assert.That(f.Entries.Any(e => e.Level == 1), Is.False, "weakest unit left out");
            Assert.That(f.Entries.Single(e => e.UnitId == "artillery").Row, Is.EqualTo(FormationRow.Back));
            Assert.That(f.Entries.Where(e => e.UnitId != "artillery").All(e => e.Row == FormationRow.Front), Is.True);
        }

        [Test]
        public void PlaceInFreeSlot_UsesClassRows_AndRespectsLimit()
        {
            var rig = new BattleRig();
            rig.Put(0, 0, "artillery", 1);
            rig.Put(1, 0, "infantry", 1);
            rig.Put(2, 0, "infantry", 1);
            rig.Put(3, 0, "infantry", 1);
            var roster = rig.Service.Roster();
            var artillery = roster.First(u => u.Item.TypeId == "artillery");
            Assert.That(rig.Service.PlaceInFreeSlot("1-1", artillery), Is.True);
            Assert.That(rig.Service.PlaceInFreeSlot("1-1", artillery), Is.False, "already placed");
            var placed = rig.Service.FormationFor("1-1").Get(FormationRow.Back, 2);
            Assert.That(placed?.UnitId, Is.EqualTo("artillery"), "artillery goes to the back row, centre lane");

            var infantry = roster.Where(u => u.Item.TypeId == "infantry").ToList();
            Assert.That(rig.Service.PlaceInFreeSlot("1-1", infantry[0]), Is.True);
            Assert.That(rig.Service.FormationFor("1-1").Get(FormationRow.Front, 2)?.UnitId, Is.EqualTo("infantry"));
            Assert.That(rig.Service.PlaceInFreeSlot("1-1", infantry[1]), Is.True);
            Assert.That(rig.Service.FormationFor("1-1").Get(FormationRow.Front, 1).HasValue, Is.True, "next lane left of centre");
            Assert.That(rig.Service.PlaceInFreeSlot("1-1", infantry[2]), Is.False, "limit of 3 reached");
        }

        [Test]
        public void MoveOrSwap_SwapsOccupiedSlots()
        {
            var rig = new BattleRig();
            rig.Put(0, 0, "armor", 2);
            rig.Put(1, 0, "infantry", 1);
            rig.Service.AutoFill("1-1");
            var f = rig.Service.FormationFor("1-1");
            Assert.That(f.Get(FormationRow.Front, 2)?.UnitId, Is.EqualTo("armor"));
            Assert.That(f.Get(FormationRow.Front, 1)?.UnitId, Is.EqualTo("infantry"));

            Assert.That(rig.Service.MoveOrSwap("1-1", (FormationRow.Front, 2), (FormationRow.Front, 1)), Is.True);
            Assert.That(f.Get(FormationRow.Front, 1)?.UnitId, Is.EqualTo("armor"));
            Assert.That(f.Get(FormationRow.Front, 2)?.UnitId, Is.EqualTo("infantry"));

            Assert.That(rig.Service.MoveOrSwap("1-1", (FormationRow.Front, 1), (FormationRow.Back, 4)), Is.True);
            Assert.That(f.Get(FormationRow.Front, 1), Is.Null);
            Assert.That(f.Get(FormationRow.Back, 4)?.SourceCell, Is.EqualTo(new Game.Logic.Merge.Cell(0, 0)));
            Assert.That(f.Count, Is.EqualTo(2));
        }

        [Test]
        public void CanStart_ChecksLockAndFormation()
        {
            var rig = new BattleRig();
            Assert.That(rig.Service.CanStart("nope"), Is.EqualTo(BattleStartFailure.UnknownLevel));
            Assert.That(rig.Service.CanStart("1-2"), Is.EqualTo(BattleStartFailure.Locked));
            Assert.That(rig.Service.CanStart("1-1"), Is.EqualTo(BattleStartFailure.EmptyFormation));
            Assert.That(rig.Service.Start("1-1"), Is.Null);
            rig.Put(0, 0, "armor", 8);
            rig.Service.AutoFill("1-1");
            Assert.That(rig.Service.CanStart("1-1"), Is.Null);
        }

        [Test]
        public void Victory_PaysGold_AndGemsOnlyForNewStars_AndUnlocksNextLevel()
        {
            var rig = new BattleRig();
            rig.Put(0, 0, "armor", 8);
            rig.Service.AutoFill("1-1");
            var result = rig.Service.Start("1-1").RunToEnd();
            Assert.That(result.Outcome, Is.EqualTo(BattleOutcome.Victory));
            Assert.That(result.Stars, Is.EqualTo(3));

            var reward = rig.Service.Complete("1-1", result);
            Assert.That(reward.Gold, Is.EqualTo(100));
            Assert.That(reward.NewStars, Is.EqualTo(3));
            Assert.That(reward.Gems, Is.EqualTo(6));
            Assert.That(rig.Wallet.Gold, Is.EqualTo(100));
            Assert.That(rig.Wallet.Gems, Is.EqualTo(6));
            Assert.That(rig.Progress.IsUnlocked("1-2"), Is.True);
            Assert.That(rig.Finished, Has.Count.EqualTo(1));

            var again = rig.Service.Complete("1-1", result);
            Assert.That(again.Gold, Is.EqualTo(100), "replays still pay gold");
            Assert.That(again.Gems, Is.Zero, "stars pay gems once");
            Assert.That(rig.Wallet.Gems, Is.EqualTo(6));
        }

        [Test]
        public void Defeat_PaysNothing_AndKeepsUnits()
        {
            var rig = new BattleRig();
            rig.Put(0, 0, "infantry", 1);
            rig.Service.AutoFill("1-1");
            rig.Progress.Record("1-1", 1);
            rig.Service.AutoFill("1-2");
            var result = rig.Service.Start("1-2").RunToEnd();
            Assert.That(result.Outcome, Is.EqualTo(BattleOutcome.Defeat));
            var reward = rig.Service.Complete("1-2", result);
            Assert.That(reward.Gold, Is.Zero);
            Assert.That(rig.Wallet.Gold, Is.Zero);
            Assert.That(rig.Progress.Stars("1-2"), Is.Zero);
            Assert.That(rig.Board.Board.ItemCount, Is.EqualTo(1), "units are never lost in battle");
        }

        [Test]
        public void Retry_KeepsTheSameFormation_AndReplaysIdentically()
        {
            var rig = new BattleRig();
            rig.Put(0, 0, "infantry", 3);
            rig.Put(1, 0, "artillery", 3);
            rig.Service.AutoFill("1-1");
            var first = rig.Service.Start("1-1").RunToEnd();
            var formation = rig.Service.FormationFor("1-1").Entries.Select(e => (e.Row, e.Column, e.UnitId, e.Level)).ToList();
            var second = rig.Service.Start("1-1").RunToEnd();
            Assert.That(rig.Service.FormationFor("1-1").Entries.Select(e => (e.Row, e.Column, e.UnitId, e.Level)), Is.EqualTo(formation));
            Assert.That(second.Checksum, Is.EqualTo(first.Checksum));
        }

        [Test]
        public void FormationFor_DropsUnitsMergedAway()
        {
            var rig = new BattleRig();
            rig.Put(0, 0, "infantry", 2);
            rig.Put(1, 0, "infantry", 2);
            rig.Service.AutoFill("1-1");
            Assert.That(rig.Service.FormationFor("1-1").Count, Is.EqualTo(2));
            rig.Board.Drag(new Game.Logic.Merge.Cell(0, 0), new Game.Logic.Merge.Cell(1, 0));
            Assert.That(rig.Service.FormationFor("1-1").Count, Is.Zero, "both entries now point at a changed or empty cell");
        }

        [Test]
        public void Progress_KeepsBestStars_AndRoundTripsThroughSave()
        {
            var rig = new BattleRig();
            Assert.That(rig.Progress.Record("1-1", 2), Is.EqualTo(2));
            Assert.That(rig.Progress.Record("1-1", 1), Is.Zero);
            Assert.That(rig.Progress.Record("1-1", 3), Is.EqualTo(1));
            Assert.That(rig.Progress.CurrentLevel.Id, Is.EqualTo("1-2"));

            var data = rig.Progress.Capture();
            var copy = new CampaignProgress(rig.Campaign, rig.Bus);
            copy.Restore(Game.Core.Json.JsonNode.Parse(data.ToJson()) as Game.Core.Json.JsonObject);
            Assert.That(copy.Stars("1-1"), Is.EqualTo(3));
            Assert.That(copy.TotalStars, Is.EqualTo(3));

            copy.Restore(null);
            Assert.That(copy.TotalStars, Is.Zero);
            Assert.That(copy.CurrentLevel.Id, Is.EqualTo("1-1"));
        }

        [Test]
        public void CampaignConfig_ValidatesLevels()
        {
            var battle = BattleTestHelpers.CreateConfig();
            Assert.That(BattleTestHelpers.CreateCampaign().Validate(battle), Is.Empty);
            var bad = BattleTestHelpers.CreateCampaign();
            bad.Levels[1].Id = "1-1";
            bad.Levels[0].Enemies.Add(BattleTestHelpers.Unit(FormationRow.Front, 2, "dragon"));
            bad.Levels[0].FastWinSeconds = 999;
            Assert.That(bad.Validate(battle).Count, Is.EqualTo(4), string.Join("\n", bad.Validate(battle)));
        }
    }
}

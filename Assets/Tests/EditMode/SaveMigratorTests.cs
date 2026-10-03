using System;
using Game.Core.Json;
using Game.Core.Save;
using NUnit.Framework;

namespace Game.Tests
{
    public class SaveMigratorTests
    {
        /// <summary>Example v1 → v2 step: the "fake" section renamed "coins" to "gold".</summary>
        private sealed class RenameCoinsToGold : ISaveMigration
        {
            public int FromVersion => 1;

            public void Migrate(JsonObject root)
            {
                root.GetObject(SaveFormat.SectionsKey)?.GetObject("fake")?.Rename("coins", "gold");
            }
        }

        /// <summary>Example v2 → v3 step: gold values were rescaled by 10.</summary>
        private sealed class ScaleGold : ISaveMigration
        {
            public int FromVersion => 2;

            public void Migrate(JsonObject root)
            {
                var fake = root.GetObject(SaveFormat.SectionsKey)?.GetObject("fake");
                if (fake != null) fake.Set("gold", fake.GetLong("gold") * 10);
            }
        }

        [Test]
        public void Migrate_AppliesStepsInOrderAndUpdatesVersion()
        {
            var migrator = new SaveMigrator(3).Register(new ScaleGold()).Register(new RenameCoinsToGold());
            var root = (JsonObject)JsonNode.Parse("{\"version\":1,\"sections\":{\"fake\":{\"coins\":4}}}");

            Assert.AreEqual(2, migrator.Migrate(root));
            Assert.AreEqual(3, root.GetInt(SaveFormat.VersionKey));
            Assert.AreEqual(40, root.GetObject(SaveFormat.SectionsKey).GetObject("fake").GetLong("gold"));
        }

        [Test]
        public void Migrate_CurrentVersion_DoesNothing()
        {
            var root = (JsonObject)JsonNode.Parse("{\"version\":3}");
            Assert.AreEqual(0, new SaveMigrator(3).Migrate(root));
        }

        [Test]
        public void Migrate_MissingStep_Throws()
        {
            var migrator = new SaveMigrator(3).Register(new RenameCoinsToGold());
            var root = (JsonObject)JsonNode.Parse("{\"version\":1}");

            Assert.Throws<FormatException>(() => migrator.Migrate(root));
        }

        [Test]
        public void Migrate_NewerOrInvalidVersion_Throws()
        {
            var migrator = new SaveMigrator(2);

            Assert.Throws<SaveTooNewException>(() => migrator.Migrate((JsonObject)JsonNode.Parse("{\"version\":3}")));
            Assert.Throws<FormatException>(() => migrator.Migrate((JsonObject)JsonNode.Parse("{\"version\":0}")));
            Assert.Throws<FormatException>(() => migrator.Migrate(new JsonObject()));
        }

        [Test]
        public void Register_RejectsDuplicatesAndOutOfRangeSteps()
        {
            var migrator = new SaveMigrator(2).Register(new RenameCoinsToGold());

            Assert.Throws<InvalidOperationException>(() => migrator.Register(new RenameCoinsToGold()));
            Assert.Throws<ArgumentOutOfRangeException>(() => migrator.Register(new ScaleGold()));
        }

        [Test]
        public void SaveService_LoadsAndMigratesOldSave()
        {
            var storage = new MemorySaveStorage();
            storage.Write("save", "{\"version\":1,\"savedAt\":1,\"sections\":{\"fake\":{\"coins\":4,\"name\":\"old\"}}}");
            var rig = new SaveRig(storage, new SaveMigrator(3).Register(new RenameCoinsToGold()).Register(new ScaleGold()));

            Assert.AreEqual(LoadStatus.Migrated, rig.Service.Load());
            Assert.AreEqual(40, rig.Section.Gold);
            Assert.AreEqual("old", rig.Section.Name);
            Assert.IsTrue(rig.Service.IsDirty, "migrated data should be written back");

            rig.Service.Flush();
            Assert.AreEqual(3, ((JsonObject)JsonNode.Parse(storage.Read("save"))).GetInt(SaveFormat.VersionKey));
        }
    }
}

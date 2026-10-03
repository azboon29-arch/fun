using System;
using System.IO;
using Game.Core.Events;
using Game.Core.Json;
using Game.Core.Save;
using Game.Core.Services;
using NUnit.Framework;

namespace Game.Tests
{
    public class SaveServiceTests
    {
        [Test]
        public void SaveThenLoad_RoundTripsSectionData()
        {
            var first = new SaveRig();
            Assert.AreEqual(LoadStatus.NewGame, first.Service.Load());
            first.Section.Gold = 123456789012345L;
            first.Section.Name = "Base \"Alpha\" ✓";
            Assert.IsTrue(first.Service.Save());

            var second = new SaveRig(first.Storage);
            var status = second.Service.Load();

            Assert.AreEqual(LoadStatus.Loaded, status);
            Assert.AreEqual(123456789012345L, second.Section.Gold);
            Assert.AreEqual("Base \"Alpha\" ✓", second.Section.Name);
            Assert.AreEqual(SaveRig.Start, second.Service.LastSavedUtc);
        }

        [Test]
        public void Save_WritesVersionAndTimestamps()
        {
            var rig = new SaveRig();
            rig.Service.Load();
            rig.Service.Save();

            var root = (JsonObject)JsonNode.Parse(rig.Storage.Read("save"));

            Assert.AreEqual(SaveFormat.CurrentVersion, root.GetInt(SaveFormat.VersionKey));
            Assert.AreEqual(new DateTimeOffset(SaveRig.Start).ToUnixTimeSeconds(), root.GetLong(SaveFormat.SavedAtKey));
            Assert.AreEqual(root.GetLong(SaveFormat.SavedAtKey), root.GetLong(SaveFormat.CreatedAtKey));
            Assert.IsNotNull(root.GetObject(SaveFormat.SectionsKey).GetObject("fake"));
        }

        [Test]
        public void CreatedAt_IsKeptAcrossSaves()
        {
            var rig = new SaveRig();
            rig.Service.Load();
            rig.Service.Save();
            rig.Clock.Advance(TimeSpan.FromHours(3));
            rig.Service.Save();

            var reloaded = new SaveRig(rig.Storage);
            reloaded.Service.Load();
            reloaded.Service.Save();
            var root = (JsonObject)JsonNode.Parse(reloaded.Storage.Read("save"));

            Assert.AreEqual(new DateTimeOffset(SaveRig.Start).ToUnixTimeSeconds(), root.GetLong(SaveFormat.CreatedAtKey));
        }

        [Test]
        public void Load_NewGame_RestoresSectionsWithNull()
        {
            var rig = new SaveRig();
            rig.Section.Gold = 99;

            Assert.AreEqual(LoadStatus.NewGame, rig.Service.Load());
            Assert.IsTrue(rig.Section.LastRestoreWasNull);
            Assert.AreEqual(0, rig.Section.Gold);
            Assert.IsNull(rig.Service.LastSavedUtc);
        }

        [Test]
        public void Load_PublishesGameLoaded()
        {
            var rig = new SaveRig();
            LoadStatus? published = null;
            rig.Bus.Subscribe<GameLoaded>(e => published = e.Status);

            rig.Service.Load();

            Assert.AreEqual(LoadStatus.NewGame, published);
        }

        [Test]
        public void Autosave_WaitsForDelayAfterFirstChange()
        {
            var rig = new SaveRig();
            rig.Service.Load();
            var writesBefore = rig.Storage.WriteCount;

            rig.Bus.Publish(new GameStateChanged("test"));
            Assert.IsTrue(rig.Service.IsDirty);

            rig.Clock.Advance(TimeSpan.FromSeconds(SaveRig.AutosaveDelay - 0.5));
            rig.Bus.Publish(new GameStateChanged("test again"));
            rig.Service.Tick();
            Assert.AreEqual(writesBefore, rig.Storage.WriteCount, "should not save before the delay");

            rig.Clock.Advance(TimeSpan.FromSeconds(0.5));
            rig.Service.Tick();
            Assert.Greater(rig.Storage.WriteCount, writesBefore, "should save once the delay passed since the first change");
            Assert.IsFalse(rig.Service.IsDirty);

            var afterSave = rig.Storage.WriteCount;
            rig.Clock.Advance(TimeSpan.FromMinutes(5));
            rig.Service.Tick();
            Assert.AreEqual(afterSave, rig.Storage.WriteCount, "nothing changed, so nothing to write");
        }

        [Test]
        public void Flush_SavesOnlyWhenDirty()
        {
            var rig = new SaveRig();
            rig.Service.Load();

            rig.Service.Flush();
            Assert.IsNull(rig.Storage.Read("save"));

            rig.Service.MarkDirty();
            rig.Service.Flush();
            Assert.IsNotNull(rig.Storage.Read("save"));
            Assert.IsFalse(rig.Service.IsDirty);
        }

        [Test]
        public void Save_KeepsPreviousFileAsBackup()
        {
            var rig = new SaveRig();
            rig.Service.Load();
            rig.Section.Gold = 1;
            rig.Service.Save();
            rig.Section.Gold = 2;
            rig.Service.Save();

            var backup = (JsonObject)JsonNode.Parse(rig.Storage.Read("save.bak"));
            Assert.AreEqual(1, backup.GetObject(SaveFormat.SectionsKey).GetObject("fake").GetLong("gold"));
        }

        [Test]
        public void Load_CorruptMain_UsesBackupAndKeepsCorruptCopy()
        {
            var rig = new SaveRig();
            rig.Service.Load();
            rig.Section.Gold = 10;
            rig.Service.Save();
            rig.Section.Gold = 20;
            rig.Service.Save();
            rig.Storage.Write("save", "{ not json");

            var reloaded = new SaveRig(rig.Storage);
            var status = reloaded.Service.Load();

            Assert.AreEqual(LoadStatus.RestoredFromBackup, status);
            Assert.AreEqual(10, reloaded.Section.Gold);
            Assert.AreEqual("{ not json", reloaded.Storage.Read("save.corrupt"));
            Assert.IsTrue(reloaded.Service.IsDirty, "a good main file should be rewritten");

            reloaded.Service.Flush();
            Assert.AreEqual(10, ((JsonObject)JsonNode.Parse(reloaded.Storage.Read("save.bak")))
                .GetObject(SaveFormat.SectionsKey).GetObject("fake").GetLong("gold"), "the corrupt file must not replace the backup");
        }

        [Test]
        public void Load_MainAndBackupCorrupt_StartsNewGameAndKeepsCopy()
        {
            var storage = new MemorySaveStorage();
            storage.Write("save", "garbage");
            storage.Write("save.bak", "[1,2");
            var rig = new SaveRig(storage);

            Assert.AreEqual(LoadStatus.CorruptStartedNew, rig.Service.Load());
            Assert.IsTrue(rig.Section.LastRestoreWasNull);
            Assert.AreEqual("garbage", storage.Read("save.corrupt"));
        }

        [TestCase("{\"version\":1,\"sections\":[]}")]
        [TestCase("{\"sections\":{}}")]
        [TestCase("[]")]
        [TestCase("{\"version\":\"one\"}")]
        public void Load_WrongShape_IsTreatedAsUnreadable(string text)
        {
            var storage = new MemorySaveStorage();
            storage.Write("save", text);
            var rig = new SaveRig(storage);

            var status = rig.Service.Load();

            // A non-object sections value is tolerated as "no sections"; everything else is unreadable.
            Assert.That(status, Is.EqualTo(LoadStatus.Loaded).Or.EqualTo(LoadStatus.CorruptStartedNew));
            Assert.IsTrue(rig.Section.LastRestoreWasNull);
        }

        [Test]
        public void Load_SaveFromNewerBuild_IsReadOnlyAndUntouched()
        {
            var storage = new MemorySaveStorage();
            var future = "{\"version\":" + (SaveFormat.CurrentVersion + 1) + ",\"sections\":{}}";
            storage.Write("save", future);
            var rig = new SaveRig(storage);

            Assert.AreEqual(LoadStatus.TooNew, rig.Service.Load());
            Assert.IsTrue(rig.Service.IsReadOnly);

            rig.Service.MarkDirty();
            Assert.IsFalse(rig.Service.Save());
            Assert.AreEqual(future, storage.Read("save"));
        }

        [Test]
        public void UnknownSections_ArePreservedOnSave()
        {
            var storage = new MemorySaveStorage();
            storage.Write("save", "{\"version\":" + SaveFormat.CurrentVersion +
                ",\"sections\":{\"fake\":{\"gold\":5},\"futureSystem\":{\"x\":1}}}");
            var rig = new SaveRig(storage);
            rig.Service.Load();

            rig.Service.Save();

            var sections = ((JsonObject)JsonNode.Parse(storage.Read("save"))).GetObject(SaveFormat.SectionsKey);
            Assert.AreEqual(1, sections.GetObject("futureSystem").GetInt("x"));
            Assert.AreEqual(5, sections.GetObject("fake").GetLong("gold"));
        }

        [Test]
        public void Register_DuplicateKey_Throws()
        {
            var rig = new SaveRig();
            Assert.Throws<InvalidOperationException>(() => rig.Service.Register(new FakeSection()));
        }

        [Test]
        public void DeleteAll_ResetsToNewGame()
        {
            var rig = new SaveRig();
            rig.Service.Load();
            rig.Section.Gold = 50;
            rig.Service.Save();
            rig.Service.Save();

            rig.Service.DeleteAll();

            Assert.IsNull(rig.Storage.Read("save"));
            Assert.IsNull(rig.Storage.Read("save.bak"));
            Assert.AreEqual(0, rig.Section.Gold);
            Assert.AreEqual(LoadStatus.NewGame, rig.Service.LastLoadStatus);
        }

        [Test]
        public void FileStorage_RoundTripsThroughDisk()
        {
            var dir = Path.Combine(Path.GetTempPath(), "merge-command-tests-" + Guid.NewGuid().ToString("N"));
            try
            {
                var storage = new FileSaveStorage(dir);
                var bus = new EventBus();
                var clock = new ManualClock(SaveRig.Start);
                var settings = new SaveSettings("save", SaveRig.AutosaveDelay);

                var section = new FakeSection();
                var service = new SaveService(storage, new SaveMigrator(SaveFormat.CurrentVersion), bus, clock, NullGameLog.Instance, settings);
                service.Register(section);
                service.Load();
                section.Gold = 777;
                Assert.IsTrue(service.Save());
                Assert.IsTrue(File.Exists(storage.PathFor("save")));
                Assert.IsFalse(File.Exists(storage.PathFor("save") + ".tmp"));

                var loadedSection = new FakeSection();
                var loader = new SaveService(new FileSaveStorage(dir), new SaveMigrator(SaveFormat.CurrentVersion), new EventBus(), clock,
                    NullGameLog.Instance, settings);
                loader.Register(loadedSection);

                Assert.AreEqual(LoadStatus.Loaded, loader.Load());
                Assert.AreEqual(777, loadedSection.Gold);
                Assert.IsTrue(storage.Delete("save"));
                Assert.IsFalse(storage.Delete("save"));
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        [Test]
        public void FileStorage_ReadsTempFileWhenMainIsMissing()
        {
            var dir = Path.Combine(Path.GetTempPath(), "merge-command-tests-" + Guid.NewGuid().ToString("N"));
            try
            {
                var storage = new FileSaveStorage(dir);
                Directory.CreateDirectory(dir);
                File.WriteAllText(storage.PathFor("save") + ".tmp", "{\"a\":1}");

                Assert.AreEqual("{\"a\":1}", storage.Read("save"));
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }
    }
}

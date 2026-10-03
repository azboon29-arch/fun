using System;
using Game.Core.Events;
using Game.Core.Json;
using Game.Core.Save;
using Game.Core.Services;

namespace Game.Tests
{
    /// <summary>A save section holding a few values, standing in for a real game system.</summary>
    internal sealed class FakeSection : ISaveSection
    {
        public FakeSection(string key = "fake") { Key = key; }

        public string Key { get; }
        public long Gold;
        public string Name = "";
        public int RestoreCalls;
        public bool LastRestoreWasNull;

        public JsonObject Capture() => new JsonObject().Set("gold", Gold).Set("name", Name);

        public void Restore(JsonObject data)
        {
            RestoreCalls++;
            LastRestoreWasNull = data == null;
            Gold = data?.GetLong("gold") ?? 0;
            Name = data?.GetString("name", "") ?? "";
        }
    }

    /// <summary>Builds a save service wired to in-memory storage and a manual clock.</summary>
    internal sealed class SaveRig
    {
        public const double AutosaveDelay = 2;
        public static readonly DateTime Start = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        public readonly MemorySaveStorage Storage;
        public readonly EventBus Bus = new EventBus();
        public readonly ManualClock Clock = new ManualClock(Start);
        public readonly SaveSettings Settings = new SaveSettings("save", AutosaveDelay);
        public readonly SaveService Service;
        public readonly FakeSection Section;

        public SaveRig(MemorySaveStorage storage = null, SaveMigrator migrator = null, FakeSection section = null)
        {
            Storage = storage ?? new MemorySaveStorage();
            Service = new SaveService(Storage, migrator ?? new SaveMigrator(SaveFormat.CurrentVersion), Bus, Clock,
                NullGameLog.Instance, Settings);
            Section = section ?? new FakeSection();
            Service.Register(Section);
        }
    }
}

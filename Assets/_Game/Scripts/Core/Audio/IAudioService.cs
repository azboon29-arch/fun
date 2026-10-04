namespace Game.Core.Audio
{
    /// <summary>Sound hooks. Systems and views call this; real audio arrives in Phase 7.</summary>
    public interface IAudioService
    {
        /// <summary>Plays a one-shot sound effect by id (see <see cref="SfxIds"/>).</summary>
        void PlaySfx(string id);
    }

    /// <summary>Silent implementation used until real audio exists, and in tests.</summary>
    public sealed class NullAudioService : IAudioService
    {
        /// <inheritdoc />
        public void PlaySfx(string id) { }
    }

    /// <summary>Sound effect ids.</summary>
    public static class SfxIds
    {
        /// <summary>Two items merged.</summary>
        public const string Merge = "merge";

        /// <summary>An item was bought.</summary>
        public const string Buy = "buy";

        /// <summary>An item was dropped on an empty slot or swapped.</summary>
        public const string Drop = "drop";

        /// <summary>An action was refused (board full, not enough gold, max level).</summary>
        public const string Denied = "denied";
    }
}

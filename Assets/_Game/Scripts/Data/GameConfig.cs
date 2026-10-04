using Game.Core.Save;
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// Top-level tunables for the app shell. Later phases add their own config assets
    /// (EconomyConfig, BattleConfig, ...) and reference them from here.
    /// </summary>
    [CreateAssetMenu(menuName = "Merge Command/Game Config", fileName = "GameConfig")]
    public sealed class GameConfig : ScriptableObject
    {
        [Header("Save")]
        [Tooltip("File name (without extension) of the save in Application.persistentDataPath.")]
        [SerializeField] private string saveFileName = "save";

        [Tooltip("Seconds after the first unsaved change before autosave writes the file.")]
        [Min(0f)]
        [SerializeField] private float autosaveDelaySeconds = 2f;

        [Header("Content")]
        [Tooltip("Merge board rules and item types.")]
        [SerializeField] private MergeConfigAsset mergeConfig;

        [Tooltip("Currencies, buildings, offline earnings and HQ levels.")]
        [SerializeField] private EconomyConfigAsset economyConfig;

        [Header("Display")]
        [Tooltip("Target frame rate on device.")]
        [SerializeField] private int targetFrameRate = 60;

        [Header("Debug")]
        [Tooltip("Shows the debug overlay (save status, reset save) in development builds and the Editor.")]
        [SerializeField] private bool showDebugOverlay = true;

        /// <summary>Merge board rules and item types.</summary>
        public MergeConfigAsset MergeConfig => mergeConfig;

        /// <summary>Currencies, buildings, offline earnings and HQ levels.</summary>
        public EconomyConfigAsset EconomyConfig => economyConfig;

        /// <summary>Target frame rate on device.</summary>
        public int TargetFrameRate => targetFrameRate;

        /// <summary>Whether the debug overlay is shown in development builds and the Editor.</summary>
        public bool ShowDebugOverlay => showDebugOverlay;

        /// <summary>Save settings for the plain C# save service.</summary>
        public SaveSettings CreateSaveSettings() => new SaveSettings(saveFileName, autosaveDelaySeconds);
    }
}

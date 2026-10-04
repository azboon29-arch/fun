using Game.Data;
using Game.Logic.Merge;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Creates the starting data assets with first-pass balance values when they are missing, and links them into
    /// GameConfig. Existing assets are never overwritten, so tuned values are safe.
    /// </summary>
    public static class DefaultContent
    {
        private const string MergeConfigPath = EditorPaths.Data + "/MergeConfig.asset";

        // First-pass balance. See DESIGN_NOTES.md (Phase 2) for the reasoning.
        private const int MaxLevel = 30;
        private const float PurchaseGrowth = 1.07f;
        private const float LevelMultiplier = 2f;

        /// <summary>Creates any missing default assets and assigns them to <paramref name="config"/>.</summary>
        public static void EnsureAll(GameConfig config)
        {
            var merge = LoadOrCreateMergeConfig();
            var serialized = new SerializedObject(config);
            var property = serialized.FindProperty("mergeConfig");
            if (property.objectReferenceValue == null)
            {
                property.objectReferenceValue = merge;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(config);
            }
            AssetDatabase.SaveAssets();
        }

        private static MergeConfigAsset LoadOrCreateMergeConfig()
        {
            var asset = AssetDatabase.LoadAssetAtPath<MergeConfigAsset>(MergeConfigPath);
            if (asset != null) return asset;

            SceneBuilder.EnsureFolder(EditorPaths.Data);
            asset = ScriptableObject.CreateInstance<MergeConfigAsset>();
            asset.SetAll(4, 5, 6, 7, 5, new[]
            {
                Entry("infantry", ItemKind.Unit, 100, new Color(0.55f, 0.8f, 0.45f), "INF"),
                Entry("artillery", ItemKind.Unit, 120, new Color(0.95f, 0.6f, 0.3f), "ART"),
                Entry("armor", ItemKind.Unit, 150, new Color(0.55f, 0.7f, 0.9f), "ARM"),
                Entry("goldmine", ItemKind.Building, 200, new Color(1f, 0.85f, 0.35f), "GOLD"),
                Entry("barracks", ItemKind.Building, 500, new Color(0.8f, 0.5f, 0.45f), "BRK"),
                Entry("warehouse", ItemKind.Building, 400, new Color(0.7f, 0.6f, 0.9f), "WH"),
            });
            AssetDatabase.CreateAsset(asset, MergeConfigPath);
            return asset;
        }

        private static MergeConfigAsset.ItemEntry Entry(string id, ItemKind kind, long baseCost, Color color, string label) =>
            new MergeConfigAsset.ItemEntry(id, "item." + id, kind, MaxLevel, baseCost, PurchaseGrowth, LevelMultiplier, color, label);
    }
}

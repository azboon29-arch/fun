using Game.Data;
using Game.Logic.Merge;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Editor
{
    /// <summary>
    /// Creates the starting data assets with first-pass balance values when they are missing, and links them into
    /// GameConfig. Existing assets are never overwritten, so tuned values are safe.
    /// </summary>
    public static class DefaultContent
    {
        private const string MergeConfigPath = EditorPaths.Data + "/MergeConfig.asset";
        private const string EconomyConfigPath = EditorPaths.Data + "/EconomyConfig.asset";

        // First-pass balance. See DESIGN_NOTES.md (Phase 2) for the reasoning.
        private const int MaxLevel = 30;
        private const float PurchaseGrowth = 1.07f;
        private const float LevelMultiplier = 2f;

        /// <summary>Creates any missing default assets and assigns them to <paramref name="config"/>.</summary>
        public static void EnsureAll(GameConfig config)
        {
            var serialized = new SerializedObject(config);
            Assign(serialized, "mergeConfig", LoadOrCreateMergeConfig());
            Assign(serialized, "economyConfig", LoadOrCreateEconomyConfig());
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }

        private static void Assign(SerializedObject serialized, string field, Object asset)
        {
            var property = serialized.FindProperty(field);
            if (property.objectReferenceValue == null) property.objectReferenceValue = asset;
        }

        private static EconomyConfigAsset LoadOrCreateEconomyConfig()
        {
            var asset = AssetDatabase.LoadAssetAtPath<EconomyConfigAsset>(EconomyConfigPath);
            if (asset != null) return asset;

            SceneBuilder.EnsureFolder(EditorPaths.Data);
            asset = ScriptableObject.CreateInstance<EconomyConfigAsset>();
            // HQ: each level costs 5x the previous. The first levels grow the board (4x5 to 6x7), later ones
            // raise the highest buy level. See DESIGN_NOTES.md (Phase 3).
            asset.SetHqLevels(new[]
            {
                new EconomyConfigAsset.HqLevelEntry(0, 4, 5, 5),
                new EconomyConfigAsset.HqLevelEntry(2_000, 5, 5, 7),
                new EconomyConfigAsset.HqLevelEntry(10_000, 5, 6, 9),
                new EconomyConfigAsset.HqLevelEntry(50_000, 6, 6, 11),
                new EconomyConfigAsset.HqLevelEntry(250_000, 6, 7, 13),
                new EconomyConfigAsset.HqLevelEntry(1_250_000, 6, 7, 16),
                new EconomyConfigAsset.HqLevelEntry(6_250_000, 6, 7, 19),
                new EconomyConfigAsset.HqLevelEntry(31_250_000, 6, 7, 22),
                new EconomyConfigAsset.HqLevelEntry(156_250_000, 6, 7, 25),
                new EconomyConfigAsset.HqLevelEntry(781_250_000, 6, 7, 30),
            });
            AssetDatabase.CreateAsset(asset, EconomyConfigPath);
            return asset;
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

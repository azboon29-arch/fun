using System.Linq;
using Game.Data;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Checks every data asset for invalid values. Later phases add levels and units here.</summary>
    public static class DataValidator
    {
        /// <summary>Menu entry: logs every problem found and a summary.</summary>
        [MenuItem(EditorPaths.MenuRoot + "Validate Data", priority = 20)]
        public static void ValidateAll()
        {
            var problems = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(MergeConfigAsset)))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<MergeConfigAsset>(path);
                foreach (var error in asset.ToModel().Validate())
                {
                    Debug.LogError($"[Game] {path}: {error}", asset);
                    problems++;
                }
            }
            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(EconomyConfigAsset)))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<EconomyConfigAsset>(path);
                foreach (var error in asset.ToEconomy().Validate().Concat(asset.ToHq().Validate()))
                {
                    Debug.LogError($"[Game] {path}: {error}", asset);
                    problems++;
                }
            }
            if (problems == 0) Debug.Log("[Game] Data validation passed.");
            else Debug.LogError($"[Game] Data validation found {problems} problem(s).");
        }
    }
}

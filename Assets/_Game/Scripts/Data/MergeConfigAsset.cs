using System;
using System.Collections.Generic;
using System.Linq;
using Game.Logic.Merge;
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// Designer-editable merge board data: board sizes, buy rules and every item type with its prices and placeholder look.
    /// Converted to the plain C# <see cref="MergeConfig"/> at boot.
    /// </summary>
    [CreateAssetMenu(menuName = "Merge Command/Merge Config", fileName = "MergeConfig")]
    public sealed class MergeConfigAsset : ScriptableObject
    {
        [Header("Board")]
        [Min(1)] [SerializeField] private int startColumns = 4;
        [Min(1)] [SerializeField] private int startRows = 5;
        [Min(1)] [SerializeField] private int maxColumns = 6;
        [Min(1)] [SerializeField] private int maxRows = 7;

        [Header("Buying")]
        [Tooltip("Purchases arrive at (highest level reached - this), minimum level 1.")]
        [Min(0)] [SerializeField] private int buyLevelOffset = 5;

        [Header("Items (display order)")]
        [SerializeField] private List<ItemEntry> items = new List<ItemEntry>();

        /// <summary>Item entries in display order.</summary>
        public IReadOnlyList<ItemEntry> Items => items;

        /// <summary>Builds the plain C# config.</summary>
        public MergeConfig ToModel() =>
            new MergeConfig(startColumns, startRows, maxColumns, maxRows, buyLevelOffset, items.Select(i => i.ToModel()));

        /// <summary>Finds the entry for <paramref name="id"/>, or null.</summary>
        public ItemEntry Find(string id) => items.FirstOrDefault(i => i.Id == id);

        /// <summary>Replaces all values. Used by the editor's default content generator.</summary>
        public void SetAll(int columns, int rows, int maxCols, int maxRowCount, int offset, IEnumerable<ItemEntry> entries)
        {
            startColumns = columns;
            startRows = rows;
            maxColumns = maxCols;
            maxRows = maxRowCount;
            buyLevelOffset = offset;
            items = entries.ToList();
        }

        private void OnValidate()
        {
            foreach (var error in ToModel().Validate()) Debug.LogWarning($"[Game] {name}: {error}", this);
        }

        /// <summary>One item type: rules plus placeholder visuals.</summary>
        [Serializable]
        public sealed class ItemEntry
        {
            [Tooltip("Stable id stored in saves. Never rename without a save migration.")]
            [SerializeField] private string id = "";

            [Tooltip("Localization key of the display name.")]
            [SerializeField] private string nameKey = "";

            [SerializeField] private ItemKind kind;
            [Min(1)] [SerializeField] private int maxLevel = 30;

            [Header("Price")]
            [Min(0)] [SerializeField] private long baseCost = 100;
            [Tooltip("Cost multiplier per previous purchase of this type.")]
            [Min(1f)] [SerializeField] private float costGrowthPerPurchase = 1.07f;
            [Tooltip("Cost multiplier per level above 1 when buying higher levels.")]
            [Min(1f)] [SerializeField] private float levelCostMultiplier = 2f;

            [Header("Placeholder look")]
            [SerializeField] private Color color = Color.white;
            [Tooltip("Short label drawn on the tile, e.g. INF.")]
            [SerializeField] private string shortLabel = "";
            [Tooltip("Optional sprite. When empty, a generated shape is used (circle for units, square for buildings).")]
            [SerializeField] private Sprite sprite;

            /// <summary>Creates an empty entry (used by the serializer and the inspector).</summary>
            public ItemEntry() { }

            /// <summary>Creates an entry (used by the default content generator).</summary>
            public ItemEntry(string id, string nameKey, ItemKind kind, int maxLevel, long baseCost, float costGrowthPerPurchase,
                float levelCostMultiplier, Color color, string shortLabel)
            {
                this.id = id;
                this.nameKey = nameKey;
                this.kind = kind;
                this.maxLevel = maxLevel;
                this.baseCost = baseCost;
                this.costGrowthPerPurchase = costGrowthPerPurchase;
                this.levelCostMultiplier = levelCostMultiplier;
                this.color = color;
                this.shortLabel = shortLabel;
            }

            /// <summary>Stable id.</summary>
            public string Id => id;

            /// <summary>Localization key of the display name.</summary>
            public string NameKey => nameKey;

            /// <summary>Unit or building.</summary>
            public ItemKind Kind => kind;

            /// <summary>Tile color.</summary>
            public Color Color => color;

            /// <summary>Short tile label.</summary>
            public string ShortLabel => shortLabel;

            /// <summary>Optional sprite override.</summary>
            public Sprite Sprite => sprite;

            /// <summary>Plain C# definition.</summary>
            public ItemDefinition ToModel() =>
                new ItemDefinition(id, kind, maxLevel, baseCost, costGrowthPerPurchase, levelCostMultiplier);
        }
    }
}

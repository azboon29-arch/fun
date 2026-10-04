using Game.Data;
using Game.Logic.Merge;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// The visible tile for one board slot. Shows the item's shape, color, label and level, and forwards drag input
    /// to its <see cref="BoardView"/>. Each slot owns one tile; the tile is hidden when the slot is empty.
    /// </summary>
    public sealed class ItemTileView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private const float IconInset = 0.08f;
        private const float LabelHeight = 0.38f;
        private const float DragAlpha = 0.85f;

        private BoardView _board;
        private Image _icon;
        private Text _label;
        private Text _level;
        private CanvasGroup _group;

        /// <summary>The slot this tile belongs to.</summary>
        public Cell Cell { get; private set; }

        /// <summary>Builds the tile's children.</summary>
        public void Init(BoardView board, Cell cell)
        {
            _board = board;
            Cell = cell;
            _group = gameObject.AddComponent<CanvasGroup>();

            // Transparent full-size image so the whole tile catches drags.
            var hit = gameObject.AddComponent<Image>();
            hit.color = Color.clear;

            _icon = Ui.Image("Icon", transform, Color.white);
            Ui.Stretch(_icon.rectTransform);
            _icon.rectTransform.anchorMin = new Vector2(IconInset, IconInset);
            _icon.rectTransform.anchorMax = new Vector2(1 - IconInset, 1 - IconInset);
            _icon.raycastTarget = false;

            _label = Ui.Label("Label", _icon.transform, "", UiStyle.BodySize, UiStyle.TextDark);
            Ui.Stretch(_label.rectTransform);
            _label.rectTransform.anchorMin = new Vector2(0, LabelHeight);
            _label.fontStyle = FontStyle.Bold;

            _level = Ui.Label("Level", _icon.transform, "", UiStyle.SmallSize, UiStyle.TextDark);
            Ui.Stretch(_level.rectTransform);
            _level.rectTransform.anchorMax = new Vector2(1, LabelHeight + 0.05f);
        }

        /// <summary>Shows <paramref name="item"/>, or hides the tile when null.</summary>
        public void Show(BoardItem? item, MergeConfigAsset visuals)
        {
            _group.alpha = item.HasValue ? 1f : 0f;
            _group.blocksRaycasts = item.HasValue;
            if (!item.HasValue) return;

            var entry = visuals != null ? visuals.Find(item.Value.TypeId) : null;
            var isUnit = entry == null || entry.Kind == ItemKind.Unit;
            _icon.sprite = entry?.Sprite != null ? entry.Sprite : isUnit ? PlaceholderSprites.Circle : PlaceholderSprites.RoundedSquare;
            _icon.type = Image.Type.Simple;
            _icon.color = entry?.Color ?? Color.gray;
            _label.text = entry != null && !string.IsNullOrEmpty(entry.ShortLabel) ? entry.ShortLabel : item.Value.TypeId;
            _level.text = Strings.Format("base.level", item.Value.Level);
        }

        /// <inheritdoc />
        public void OnBeginDrag(PointerEventData eventData)
        {
            _group.blocksRaycasts = false;
            _group.alpha = DragAlpha;
            _board.BeginDrag(this, eventData);
        }

        /// <inheritdoc />
        public void OnDrag(PointerEventData eventData) => _board.ContinueDrag(this, eventData);

        /// <inheritdoc />
        public void OnEndDrag(PointerEventData eventData)
        {
            _group.blocksRaycasts = true;
            _group.alpha = 1f;
            _board.EndDrag(this, eventData);
        }
    }
}

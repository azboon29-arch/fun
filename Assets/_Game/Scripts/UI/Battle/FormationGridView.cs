using System;
using System.Collections.Generic;
using Game.Data;
using Game.Logic.Battle;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Draws a 3x5 formation. The player's grid has its front row at the top (facing the enemy above); the enemy's
    /// grid has its front row at the bottom. The player's units can be dragged between slots or tapped to remove.
    /// </summary>
    public sealed class FormationGridView : MonoBehaviour
    {
        private const float SlotGap = 10f;
        private const float BadgeInset = 0.1f;
        private const float DimmedAlpha = 0.35f;
        private static readonly Color FrontSlot = new Color(1f, 1f, 1f, 0.2f);

        private readonly Dictionary<(FormationRow, int), Image> _slots = new Dictionary<(FormationRow, int), Image>();
        private readonly Dictionary<(FormationRow, int), RectTransform> _contents = new Dictionary<(FormationRow, int), RectTransform>();
        private bool _enemy;
        private MergeConfigAsset _visuals;
        private RectTransform _grid;
        private Formation _formation;
        private float _cellSize;
        private Image _highlighted;

        /// <summary>Called when a placed unit is dragged (player grid only).</summary>
        public Action<(FormationRow Row, int Column), DragChip, PointerEventData> UnitDragStarted;

        /// <summary>Forwarded drag movement for a placed unit.</summary>
        public Action<DragChip, PointerEventData> UnitDragging;

        /// <summary>Forwarded drag end for a placed unit.</summary>
        public Action<(FormationRow Row, int Column), DragChip, PointerEventData> UnitDragEnded;

        /// <summary>Called when a placed unit is tapped (player grid only).</summary>
        public Action<(FormationRow Row, int Column)> UnitTapped;

        /// <summary>Sets up the grid.</summary>
        public void Init(bool enemy, MergeConfigAsset visuals)
        {
            _enemy = enemy;
            _visuals = visuals;
            _grid = Ui.Rect("Grid", transform);
        }

        private void OnRectTransformDimensionsChange()
        {
            if (_formation != null) Show(_formation);
        }

        /// <summary>Redraws <paramref name="formation"/>.</summary>
        public void Show(Formation formation)
        {
            _formation = formation;
            if (_grid == null) return;
            var area = ((RectTransform)transform).rect.size;
            _cellSize = Mathf.Max(1f, Mathf.Min(area.x / Formation.ColumnCount, area.y / Formation.RowCount));
            Ui.ClearChildren(_grid);
            _slots.Clear();
            _contents.Clear();
            _highlighted = null;
            Ui.Place(_grid, new Vector2(0.5f, 0.5f), new Vector2(_cellSize * Formation.ColumnCount, _cellSize * Formation.RowCount), Vector2.zero);
            _grid.pivot = new Vector2(0.5f, 0.5f);

            for (var r = 0; r < Formation.RowCount; r++)
            {
                for (var column = 0; column < Formation.ColumnCount; column++)
                {
                    var row = (FormationRow)r;
                    var slot = Ui.Image($"Slot {row} {column}", _grid, row == FormationRow.Front ? FrontSlot : UiStyle.Slot, PlaceholderSprites.RoundedSquare);
                    slot.raycastTarget = false;
                    Position(slot.rectTransform, row, column);
                    _slots[(row, column)] = slot;

                    var entry = formation.Get(row, column);
                    if (!entry.HasValue) continue;
                    var content = Ui.Rect($"Unit {row} {column}", _grid);
                    Position(content, row, column);
                    _contents[(row, column)] = content;
                    var holder = Ui.Rect("Inset", content);
                    Ui.Stretch(holder);
                    holder.anchorMin = new Vector2(BadgeInset, BadgeInset);
                    holder.anchorMax = new Vector2(1 - BadgeInset, 1 - BadgeInset);
                    UnitBadge.Create("Badge", holder, false).Show(entry.Value.UnitId, entry.Value.Level, _visuals, _enemy);
                    if (!_enemy) MakeInteractive(content, (row, column));
                }
            }
        }

        private void MakeInteractive(RectTransform content, (FormationRow Row, int Column) slot)
        {
            var hit = content.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            var chip = content.gameObject.AddComponent<DragChip>();
            chip.DragStarted = (c, e) => UnitDragStarted?.Invoke(slot, c, e);
            chip.Dragging = (c, e) => UnitDragging?.Invoke(c, e);
            chip.DragEnded = (c, e) => UnitDragEnded?.Invoke(slot, c, e);
            chip.Tapped = _ => UnitTapped?.Invoke(slot);
        }

        /// <summary>Size of one cell in canvas units.</summary>
        public float CellSize => _cellSize;

        /// <summary>The formation slot under a screen position, if any.</summary>
        public (FormationRow Row, int Column)? SlotAt(PointerEventData eventData)
        {
            if (_grid == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(_grid, eventData.position, eventData.pressEventCamera, out var local))
            {
                return null;
            }
            var size = _grid.rect.size;
            var x = local.x + size.x * 0.5f;
            var yFromTop = size.y * 0.5f - local.y;
            if (x < 0 || yFromTop < 0 || x >= size.x || yFromTop >= size.y) return null;
            var column = Mathf.FloorToInt(x / _cellSize);
            var visualRow = Mathf.FloorToInt(yFromTop / _cellSize);
            var row = (FormationRow)(_enemy ? Formation.RowCount - 1 - visualRow : visualRow);
            return Formation.IsValidSlot(row, column) ? (row, column) : ((FormationRow, int)?)null;
        }

        /// <summary>Highlights a drop target (or clears the highlight).</summary>
        public void Highlight((FormationRow Row, int Column)? slot)
        {
            if (_highlighted != null) _highlighted.color = SlotColor(_highlighted);
            _highlighted = slot.HasValue && _slots.TryGetValue(slot.Value, out var image) ? image : null;
            if (_highlighted != null) _highlighted.color = UiStyle.SlotHighlight;
        }

        /// <summary>Dims or restores a placed unit while it is being dragged.</summary>
        public void SetDimmed((FormationRow Row, int Column) slot, bool dimmed)
        {
            if (!_contents.TryGetValue(slot, out var content)) return;
            var group = content.GetComponent<CanvasGroup>();
            if (group == null) group = content.gameObject.AddComponent<CanvasGroup>();
            group.alpha = dimmed ? DimmedAlpha : 1f;
            group.blocksRaycasts = !dimmed;
        }

        private Color SlotColor(Image image)
        {
            foreach (var pair in _slots)
            {
                if (pair.Value == image) return pair.Key.Item1 == FormationRow.Front ? FrontSlot : UiStyle.Slot;
            }
            return UiStyle.Slot;
        }

        private void Position(RectTransform rect, FormationRow row, int column)
        {
            var visualRow = _enemy ? Formation.RowCount - 1 - (int)row : (int)row;
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0.5f, 0.5f);
            var size = _cellSize - SlotGap;
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = new Vector2((column + 0.5f) * _cellSize, -(visualRow + 0.5f) * _cellSize);
        }
    }
}

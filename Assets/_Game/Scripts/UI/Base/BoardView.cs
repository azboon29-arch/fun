using System;
using System.Collections.Generic;
using Game.Core.Audio;
using Game.Core.Events;
using Game.Data;
using Game.Logic.Merge;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Draws the merge board and turns drags into <see cref="MergeBoardService.Drag"/> calls.
    /// Redraws on <see cref="BoardChanged"/> and plays merge/buy feedback (the animation and sound hooks).
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        private const float SlotGap = 12f;
        private const float MergePunchScale = 1.35f;
        private const float MergePunchSeconds = 0.25f;
        private const float BuyPopSeconds = 0.3f;

        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();
        private readonly Dictionary<Cell, ItemTileView> _tiles = new Dictionary<Cell, ItemTileView>();
        private readonly Dictionary<Cell, Image> _slots = new Dictionary<Cell, Image>();

        private MergeBoardService _service;
        private MergeConfigAsset _visuals;
        private IAudioService _audio;
        private RectTransform _grid;
        private RectTransform _dragLayer;
        private float _cellSize;
        private int _columns;
        private int _rows;
        private Image _highlighted;

        /// <summary>Raised when the player drops on an identical max-level item, so the screen can show a message.</summary>
        public event Action MaxLevelDrop;

        /// <summary>Connects the view to the board and builds the grid.</summary>
        /// <param name="service">Board to show.</param>
        /// <param name="visuals">Item colors, labels and sprites.</param>
        /// <param name="bus">Event bus for board events.</param>
        /// <param name="audio">Sound hooks.</param>
        /// <param name="dragLayer">Full-screen layer the dragged tile moves on, above everything else.</param>
        public void Init(MergeBoardService service, MergeConfigAsset visuals, EventBus bus, IAudioService audio, RectTransform dragLayer)
        {
            _service = service;
            _visuals = visuals;
            _audio = audio;
            _dragLayer = dragLayer;
            _grid = Ui.Rect("Grid", transform);

            _subscriptions.Add(bus.Subscribe<BoardChanged>(_ => Refresh()));
            _subscriptions.Add(bus.Subscribe<ItemMerged>(OnMerged));
            _subscriptions.Add(bus.Subscribe<ItemBought>(OnBought));
            _subscriptions.Add(bus.Subscribe<ItemDragged>(OnDragged));
            Refresh();
        }

        private void OnDestroy()
        {
            foreach (var subscription in _subscriptions) subscription.Dispose();
            _subscriptions.Clear();
        }

        private void OnRectTransformDimensionsChange()
        {
            if (_service != null) Rebuild();
        }

        /// <summary>Redraws every slot; rebuilds the grid first if the board size or available space changed.</summary>
        public void Refresh()
        {
            if (_service == null) return;
            var board = _service.Board;
            if (board.Columns != _columns || board.Rows != _rows || !Mathf.Approximately(CellSizeFor(board), _cellSize))
            {
                Rebuild();
                return;
            }
            foreach (var pair in _tiles) pair.Value.Show(board.Get(pair.Key), _visuals);
        }

        private float CellSizeFor(BoardModel board)
        {
            var area = ((RectTransform)transform).rect.size;
            return Mathf.Max(1f, Mathf.Min(area.x / board.Columns, area.y / board.Rows));
        }

        private void Rebuild()
        {
            var board = _service.Board;
            _columns = board.Columns;
            _rows = board.Rows;
            _cellSize = CellSizeFor(board);

            Ui.ClearChildren(_grid);
            _tiles.Clear();
            _slots.Clear();
            Ui.Place(_grid, new Vector2(0.5f, 0.5f), new Vector2(_cellSize * _columns, _cellSize * _rows), Vector2.zero);

            for (var row = 0; row < _rows; row++)
            {
                for (var column = 0; column < _columns; column++)
                {
                    var cell = new Cell(column, row);
                    var slot = Ui.Image($"Slot {column},{row}", _grid, UiStyle.Slot, PlaceholderSprites.RoundedSquare);
                    PlaceInGrid(slot.rectTransform, cell);
                    slot.raycastTarget = false;
                    _slots[cell] = slot;

                    var tileRect = Ui.Rect($"Tile {column},{row}", _grid);
                    PlaceInGrid(tileRect, cell);
                    var tile = tileRect.gameObject.AddComponent<ItemTileView>();
                    tile.Init(this, cell);
                    tile.Show(board.Get(cell), _visuals);
                    _tiles[cell] = tile;
                }
            }
        }

        private void PlaceInGrid(RectTransform rect, Cell cell)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0.5f, 0.5f);
            var size = _cellSize - SlotGap;
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = new Vector2((cell.Column + 0.5f) * _cellSize, -(cell.Row + 0.5f) * _cellSize);
            rect.localScale = Vector3.one;
        }

        internal void BeginDrag(ItemTileView tile, PointerEventData eventData)
        {
            tile.transform.SetParent(_dragLayer, true);
            tile.transform.SetAsLastSibling();
            ContinueDrag(tile, eventData);
        }

        internal void ContinueDrag(ItemTileView tile, PointerEventData eventData)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_dragLayer, eventData.position, eventData.pressEventCamera, out var local))
            {
                ((RectTransform)tile.transform).localPosition = local;
            }
            Highlight(CellAt(eventData));
        }

        internal void EndDrag(ItemTileView tile, PointerEventData eventData)
        {
            Highlight(null);
            tile.transform.SetParent(_grid, false);
            PlaceInGrid((RectTransform)tile.transform, tile.Cell);

            var target = CellAt(eventData);
            if (target.HasValue) _service.Drag(tile.Cell, target.Value);
            else Refresh();
        }

        private Cell? CellAt(PointerEventData eventData)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_grid, eventData.position, eventData.pressEventCamera, out var local))
            {
                return null;
            }
            // Grid pivot is centered; convert to top-left origin.
            var size = _grid.rect.size;
            var x = local.x + size.x * 0.5f;
            var y = size.y * 0.5f - local.y;
            if (x < 0 || y < 0 || x >= size.x || y >= size.y) return null;
            var cell = new Cell(Mathf.FloorToInt(x / _cellSize), Mathf.FloorToInt(y / _cellSize));
            return _service.Board.Contains(cell) ? cell : (Cell?)null;
        }

        private void Highlight(Cell? cell)
        {
            if (_highlighted != null) _highlighted.color = UiStyle.Slot;
            _highlighted = cell.HasValue && _slots.TryGetValue(cell.Value, out var slot) ? slot : null;
            if (_highlighted != null) _highlighted.color = UiStyle.SlotHighlight;
        }

        private void OnMerged(ItemMerged e)
        {
            _audio.PlaySfx(SfxIds.Merge);
            if (_tiles.TryGetValue(e.To, out var tile)) StartCoroutine(Tween.Punch(tile.transform, MergePunchScale, MergePunchSeconds));
        }

        private void OnBought(ItemBought e)
        {
            _audio.PlaySfx(SfxIds.Buy);
            if (_tiles.TryGetValue(e.Cell, out var tile)) StartCoroutine(Tween.PopIn(tile.transform, BuyPopSeconds));
        }

        private void OnDragged(ItemDragged e)
        {
            switch (e.Outcome)
            {
                case DragOutcome.Moved:
                case DragOutcome.Swapped:
                    _audio.PlaySfx(SfxIds.Drop);
                    break;
                case DragOutcome.MaxLevel:
                    _audio.PlaySfx(SfxIds.Denied);
                    MaxLevelDrop?.Invoke();
                    break;
            }
        }
    }
}

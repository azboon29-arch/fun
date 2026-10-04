using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Makes a UI element draggable and tappable, reporting to callbacks. Inside a horizontal <see cref="ScrollRect"/>,
    /// mostly sideways drags scroll the list instead of picking the element up.
    /// </summary>
    public sealed class DragChip : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        private ScrollRect _scroll;
        private bool _scrolling;

        /// <summary>Called when a drag starts on this element.</summary>
        public Action<DragChip, PointerEventData> DragStarted;

        /// <summary>Called for every drag movement.</summary>
        public Action<DragChip, PointerEventData> Dragging;

        /// <summary>Called when the drag ends.</summary>
        public Action<DragChip, PointerEventData> DragEnded;

        /// <summary>Called on a tap (no drag).</summary>
        public Action<DragChip> Tapped;

        /// <summary>Forwards sideways drags to <paramref name="scroll"/>.</summary>
        public void ForwardSidewaysDragsTo(ScrollRect scroll) => _scroll = scroll;

        /// <inheritdoc />
        public void OnBeginDrag(PointerEventData eventData)
        {
            _scrolling = _scroll != null && Mathf.Abs(eventData.delta.x) > Mathf.Abs(eventData.delta.y);
            if (_scrolling) _scroll.OnBeginDrag(eventData);
            else DragStarted?.Invoke(this, eventData);
        }

        /// <inheritdoc />
        public void OnDrag(PointerEventData eventData)
        {
            if (_scrolling) _scroll.OnDrag(eventData);
            else Dragging?.Invoke(this, eventData);
        }

        /// <inheritdoc />
        public void OnEndDrag(PointerEventData eventData)
        {
            if (_scrolling) _scroll.OnEndDrag(eventData);
            else DragEnded?.Invoke(this, eventData);
            _scrolling = false;
        }

        /// <inheritdoc />
        public void OnPointerClick(PointerEventData eventData)
        {
            if (!eventData.dragging) Tapped?.Invoke(this);
        }
    }
}

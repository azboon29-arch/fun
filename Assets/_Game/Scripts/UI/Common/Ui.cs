using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>Small helpers for building uGUI from code (screens are generated, not hand-edited).</summary>
    public static class Ui
    {
        /// <summary>Reference resolution for every canvas (portrait).</summary>
        public static readonly Vector2 ReferenceResolution = new Vector2(1080, 1920);

        private const string BuiltinFont = "LegacyRuntime.ttf";
        private static Font _font;

        /// <summary>Default UI font.</summary>
        public static Font Font => _font != null ? _font : _font = Resources.GetBuiltinResource<Font>(BuiltinFont);

        /// <summary>Creates a screen-space canvas scaled from the 1080x1920 reference, matching width.</summary>
        public static Canvas CreateCanvas(string name, int sortingOrder = 0)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;
            return canvas;
        }

        /// <summary>Creates an empty RectTransform child.</summary>
        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        /// <summary>Stretches <paramref name="rect"/> to fill its parent, with optional insets (left, bottom, right, top).</summary>
        public static RectTransform Stretch(RectTransform rect, float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            return rect;
        }

        /// <summary>Places <paramref name="rect"/> by anchor point, pivot, size and offset.</summary>
        public static RectTransform Place(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 offset)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = offset;
            return rect;
        }

        /// <summary>Creates a tinted image.</summary>
        public static Image Image(string name, Transform parent, Color color, Sprite sprite = null)
        {
            var rect = Rect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            if (sprite != null && sprite.border != Vector4.zero) image.type = UnityEngine.UI.Image.Type.Sliced;
            return image;
        }

        /// <summary>Creates a text label.</summary>
        public static Text Label(string name, Transform parent, string text, int fontSize, Color color,
            TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var rect = Rect(name, parent);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = Font;
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = alignment;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = Mathf.Max(1, fontSize / 2);
            label.resizeTextMaxSize = fontSize;
            return label;
        }

        /// <summary>Creates a button with a background and a label filling it.</summary>
        public static Button Button(string name, Transform parent, string text, int fontSize, Color background, Color textColor,
            UnityAction onClick, out Text label)
        {
            var image = Image(name, parent, background, PlaceholderSprites.RoundedSquare);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            if (onClick != null) button.onClick.AddListener(onClick);
            label = Label("Label", image.transform, text, fontSize, textColor);
            Stretch(label.rectTransform, 12, 8, 12, 8);
            return button;
        }

        /// <summary>Destroys every child of <paramref name="parent"/>.</summary>
        public static void ClearChildren(Transform parent)
        {
            for (var i = parent.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(parent.GetChild(i).gameObject);
        }

        /// <summary>Runs <paramref name="action"/> and logs instead of throwing, so one broken view does not stop the screen.</summary>
        public static void Safe(Action action, UnityEngine.Object context)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Debug.LogException(e, context);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// A modal popup built from code: dimmed backdrop, panel with title, body text and buttons.
    /// Tapping the backdrop closes it. Content can be refreshed while open.
    /// </summary>
    public sealed class Popup : MonoBehaviour
    {
        private const float PanelWidth = 920f;
        private const float PanelHeight = 900f;
        private const float Padding = 48f;
        private const float TitleHeight = 110f;
        private const float ButtonHeight = 150f;
        private const float ButtonGap = 24f;
        private static readonly Color Backdrop = new Color(0f, 0f, 0f, 0.6f);
        private static readonly Color PanelColor = new Color(0.16f, 0.2f, 0.16f, 1f);

        private Text _title;
        private Text _body;
        private RectTransform _buttons;
        private readonly List<Button> _buttonList = new List<Button>();

        /// <summary>Raised once when the popup closes.</summary>
        public event Action Closed;

        /// <summary>Creates a popup under <paramref name="parent"/> (usually the canvas root so it covers everything).</summary>
        public static Popup Create(Transform parent, string title, string body)
        {
            var backdrop = Ui.Image("Popup", parent, Backdrop);
            Ui.Stretch(backdrop.rectTransform);
            var popup = backdrop.gameObject.AddComponent<Popup>();
            var close = backdrop.gameObject.AddComponent<Button>();
            close.transition = Selectable.Transition.None;
            close.onClick.AddListener(popup.Close);

            var panel = Ui.Image("Panel", backdrop.transform, PanelColor, PlaceholderSprites.RoundedSquare);
            Ui.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(PanelWidth, PanelHeight), Vector2.zero);
            panel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            // The panel blocks the backdrop's close button.
            panel.raycastTarget = true;

            popup._title = Ui.Label("Title", panel.transform, title, UiStyle.TitleSize, UiStyle.Gold);
            Ui.Stretch(popup._title.rectTransform, Padding, 0, Padding, Padding);
            popup._title.rectTransform.anchorMin = new Vector2(0, 1);
            popup._title.rectTransform.sizeDelta = new Vector2(-Padding * 2, TitleHeight);
            popup._title.rectTransform.pivot = new Vector2(0.5f, 1);
            popup._title.rectTransform.anchoredPosition = new Vector2(0, -Padding);

            popup._body = Ui.Label("Body", panel.transform, body, UiStyle.BodySize, UiStyle.Text, TextAnchor.UpperCenter);
            Ui.Stretch(popup._body.rectTransform, Padding, Padding * 2 + ButtonHeight, Padding, Padding * 2 + TitleHeight);

            popup._buttons = Ui.Rect("Buttons", panel.transform);
            popup._buttons.anchorMin = new Vector2(0, 0);
            popup._buttons.anchorMax = new Vector2(1, 0);
            popup._buttons.pivot = new Vector2(0.5f, 0);
            popup._buttons.sizeDelta = new Vector2(-Padding * 2, ButtonHeight);
            popup._buttons.anchoredPosition = new Vector2(0, Padding);
            var layout = popup._buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = ButtonGap;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            popup.StartCoroutine(Tween.PopIn(panel.transform, 0.2f));
            return popup;
        }

        /// <summary>Adds a button. When <paramref name="closes"/> is true, the popup closes after the click.</summary>
        public Button AddButton(string text, Color color, Action onClick, bool closes = true)
        {
            var button = Ui.Button(text, _buttons, text, UiStyle.BodySize, color, UiStyle.TextDark, () =>
            {
                onClick?.Invoke();
                if (closes) Close();
            }, out _);
            _buttonList.Add(button);
            return button;
        }

        /// <summary>Replaces the title and body text.</summary>
        public void SetText(string title, string body)
        {
            _title.text = title;
            _body.text = body;
        }

        /// <summary>Closes and destroys the popup.</summary>
        public void Close()
        {
            if (this == null || gameObject == null) return;
            var closed = Closed;
            Closed = null;
            Destroy(gameObject);
            closed?.Invoke();
        }
    }
}

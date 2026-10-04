using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>A short message bar that appears, holds and fades out. Does not block input.</summary>
    public sealed class Toast : MonoBehaviour
    {
        private const float Height = 110f;
        private const float SideMargin = 32f;
        private const float HoldSeconds = 1.6f;
        private const float FadeSeconds = 0.4f;

        private Text _text;
        private CanvasGroup _group;
        private Coroutine _routine;

        /// <summary>Creates a hidden toast anchored to the bottom of <paramref name="parent"/>, raised by <paramref name="bottomOffset"/>.</summary>
        public static Toast Create(RectTransform parent, float bottomOffset)
        {
            var image = Ui.Image("Toast", parent, UiStyle.Panel, PlaceholderSprites.RoundedSquare);
            image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(1, 0);
            rect.pivot = new Vector2(0.5f, 0);
            rect.sizeDelta = new Vector2(-SideMargin * 2, Height);
            rect.anchoredPosition = new Vector2(0, bottomOffset);
            var toast = image.gameObject.AddComponent<Toast>();
            toast._group = image.gameObject.AddComponent<CanvasGroup>();
            toast._group.alpha = 0f;
            toast._group.blocksRaycasts = false;
            toast._text = Ui.Label("Text", image.transform, "", UiStyle.BodySize, UiStyle.Text);
            Ui.Stretch(toast._text.rectTransform, SideMargin, 0, SideMargin, 0);
            return toast;
        }

        /// <summary>Shows <paramref name="message"/>, replacing any message on screen.</summary>
        public void Show(string message)
        {
            _text.text = message;
            transform.SetAsLastSibling();
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            yield return Tween.ShowThenFade(_group, HoldSeconds, FadeSeconds);
            _routine = null;
        }
    }
}

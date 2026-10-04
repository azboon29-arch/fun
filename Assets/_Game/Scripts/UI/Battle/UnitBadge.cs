using Game.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// A unit's placeholder look: a colored circle with its short label and level, an optional enemy ring and an
    /// optional HP bar. Used on the planning grids, the roster and the battlefield.
    /// </summary>
    public sealed class UnitBadge : MonoBehaviour
    {
        private const float RingScale = 1.14f;
        private const float LabelSplit = 0.4f;
        private const float HpBarHeight = 0.12f;
        private const float HpBarGap = 0.04f;
        private static readonly Color EnemyRing = new Color(0.9f, 0.2f, 0.2f);
        private static readonly Color HpBack = new Color(0f, 0f, 0f, 0.6f);
        private static readonly Color HpPlayer = new Color(0.35f, 0.9f, 0.4f);
        private static readonly Color HpEnemy = new Color(0.95f, 0.35f, 0.3f);

        private Image _ring;
        private Image _body;
        private Text _label;
        private Text _level;
        private RectTransform _hpFill;
        private RectTransform _hpBar;

        /// <summary>The circle image (for tinting effects).</summary>
        public Image Body => _body;

        /// <summary>Creates a badge under <paramref name="parent"/>, stretched to fill it.</summary>
        public static UnitBadge Create(string name, Transform parent, bool withHpBar)
        {
            var root = Ui.Rect(name, parent);
            Ui.Stretch(root);
            var badge = root.gameObject.AddComponent<UnitBadge>();

            badge._ring = Ui.Image("Ring", root, EnemyRing, PlaceholderSprites.Circle);
            Ui.Stretch(badge._ring.rectTransform);
            badge._ring.rectTransform.localScale = new Vector3(RingScale, RingScale, 1f);
            badge._ring.raycastTarget = false;

            badge._body = Ui.Image("Body", root, Color.white, PlaceholderSprites.Circle);
            Ui.Stretch(badge._body.rectTransform);
            badge._body.raycastTarget = false;

            badge._label = Ui.Label("Label", badge._body.transform, "", UiStyle.BodySize, UiStyle.TextDark);
            Ui.Stretch(badge._label.rectTransform);
            badge._label.rectTransform.anchorMin = new Vector2(0, LabelSplit);
            badge._label.fontStyle = FontStyle.Bold;

            badge._level = Ui.Label("Level", badge._body.transform, "", UiStyle.SmallSize, UiStyle.TextDark);
            Ui.Stretch(badge._level.rectTransform);
            badge._level.rectTransform.anchorMax = new Vector2(1, LabelSplit + HpBarGap);

            if (withHpBar)
            {
                var back = Ui.Image("HP", root, HpBack);
                back.raycastTarget = false;
                badge._hpBar = back.rectTransform;
                badge._hpBar.anchorMin = new Vector2(0, -HpBarHeight - HpBarGap);
                badge._hpBar.anchorMax = new Vector2(1, -HpBarGap);
                badge._hpBar.offsetMin = badge._hpBar.offsetMax = Vector2.zero;
                var fill = Ui.Image("Fill", back.transform, HpPlayer);
                fill.raycastTarget = false;
                badge._hpFill = fill.rectTransform;
                Ui.Stretch(badge._hpFill);
            }
            return badge;
        }

        /// <summary>Shows unit <paramref name="typeId"/> at <paramref name="level"/>.</summary>
        public void Show(string typeId, int level, MergeConfigAsset visuals, bool enemy)
        {
            var entry = visuals != null ? visuals.Find(typeId) : null;
            _body.sprite = entry?.Sprite != null ? entry.Sprite : PlaceholderSprites.Circle;
            _body.color = entry?.Color ?? Color.gray;
            _label.text = entry != null && !string.IsNullOrEmpty(entry.ShortLabel) ? entry.ShortLabel : typeId;
            _level.text = Strings.Format("base.level", level);
            _ring.enabled = enemy;
            if (_hpFill != null) _hpFill.GetComponent<Image>().color = enemy ? HpEnemy : HpPlayer;
        }

        /// <summary>Sets the HP bar fill (0..1). No effect without a bar.</summary>
        public void SetHealth(float fraction)
        {
            if (_hpFill == null) return;
            _hpFill.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
            _hpFill.offsetMax = Vector2.zero;
        }
    }
}

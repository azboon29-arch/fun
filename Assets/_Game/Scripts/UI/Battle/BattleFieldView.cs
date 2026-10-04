using System.Collections;
using System.Collections.Generic;
using Game.Data;
using Game.Logic.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Draws a running <see cref="BattleSimulation"/>: one badge per unit with an HP bar, smoothed movement, hit
    /// punches, floating damage numbers (gold when a counter applied) and fading deaths. Purely visual: it never
    /// changes the simulation.
    /// </summary>
    public sealed class BattleFieldView : MonoBehaviour
    {
        private const float BadgeFill = 0.8f;
        private const float MarginCells = 1f;
        private const float FollowSharpness = 18f;
        private const float HitPunchScale = 1.18f;
        private const float HitPunchSeconds = 0.15f;
        private const float DeathFadeSeconds = 0.4f;
        private const float NumberRise = 90f;
        private const float NumberSeconds = 0.7f;
        private const int MaxFloatingNumbers = 24;
        private static readonly Color MiddleLine = new Color(1f, 1f, 1f, 0.12f);
        private static readonly Color NormalHit = Color.white;

        private readonly Dictionary<int, UnitBadge> _badges = new Dictionary<int, UnitBadge>();
        private readonly Dictionary<int, Vector2> _shown = new Dictionary<int, Vector2>();
        private MergeConfigAsset _visuals;
        private BattleSimulation _sim;
        private BattleConfig _config;
        private RectTransform _layer;
        private RectTransform _numbers;
        private float _scale;
        private float _badgeSize;
        private int _floating;

        /// <summary>Starts drawing <paramref name="sim"/>, replacing any previous battle.</summary>
        public void Show(BattleSimulation sim, BattleConfig config, MergeConfigAsset visuals)
        {
            _sim = sim;
            _visuals = visuals;
            if (_layer != null) Destroy(_layer.gameObject);
            _layer = Ui.Rect("Units", transform);
            Ui.Stretch(_layer);
            _badges.Clear();
            _shown.Clear();
            _floating = 0;

            var line = Ui.Image("Middle", _layer, MiddleLine);
            line.raycastTarget = false;
            line.rectTransform.anchorMin = new Vector2(0, 0.5f);
            line.rectTransform.anchorMax = new Vector2(1, 0.5f);
            line.rectTransform.sizeDelta = new Vector2(0, 4f);

            _config = config;
            FitToArea();

            foreach (var unit in sim.Units)
            {
                var holder = Ui.Rect($"Unit {unit.Id}", _layer);
                holder.anchorMin = holder.anchorMax = new Vector2(0.5f, 0.5f);
                holder.pivot = new Vector2(0.5f, 0.5f);
                holder.sizeDelta = new Vector2(_badgeSize, _badgeSize);
                var badge = UnitBadge.Create("Badge", holder, true);
                badge.Show(unit.TypeId, unit.Level, _visuals, unit.Side == Side.Enemy);
                badge.SetHealth(1f);
                _badges[unit.Id] = badge;
                var position = ToView(unit);
                _shown[unit.Id] = position;
                holder.anchoredPosition = position;
            }

            _numbers = Ui.Rect("Numbers", _layer);
            Ui.Stretch(_numbers);
        }

        private void OnRectTransformDimensionsChange()
        {
            if (_sim == null || _layer == null) return;
            FitToArea();
            foreach (var badge in _badges.Values)
            {
                if (badge != null) ((RectTransform)badge.transform.parent).sizeDelta = new Vector2(_badgeSize, _badgeSize);
            }
        }

        /// <summary>Scales the field so both armies' full formations plus a margin fit the view.</summary>
        private void FitToArea()
        {
            var halfWidth = (Formation.ColumnCount - 1) / 2f * _config.LaneSpacing + MarginCells * _config.LaneSpacing;
            var halfHeight = _config.FrontOffset + (Formation.RowCount - 1) * _config.RowSpacing + MarginCells * _config.RowSpacing;
            var area = ((RectTransform)transform).rect.size;
            _scale = Mathf.Max(0f, Mathf.Min(area.x / (halfWidth * 2f), area.y / (halfHeight * 2f)));
            _badgeSize = Mathf.Min(_config.LaneSpacing, _config.RowSpacing) * _scale * BadgeFill;
        }

        /// <summary>Plays feedback for the events of the latest tick.</summary>
        public void OnTick(IReadOnlyList<BattleEvent> events)
        {
            if (_sim == null) return;
            foreach (var e in events)
            {
                if (!_badges.TryGetValue(e.TargetId, out var badge)) continue;
                var target = _sim.Get(e.TargetId);
                if (e.Type == BattleEventType.Attack)
                {
                    badge.SetHealth((float)target.Hp / target.Stats.Hp);
                    StartCoroutine(Tween.Punch(badge.transform.parent, HitPunchScale, HitPunchSeconds));
                    if (_floating < MaxFloatingNumbers) StartCoroutine(FloatNumber(e, badge));
                }
                else if (e.Type == BattleEventType.Death)
                {
                    StartCoroutine(FadeOut(badge.transform.parent.gameObject));
                }
            }
        }

        private void Update()
        {
            if (_sim == null) return;
            // Ease the drawn position toward the simulated one so 20 Hz steps look smooth at any frame rate.
            var t = 1f - Mathf.Exp(-FollowSharpness * Time.unscaledDeltaTime);
            foreach (var unit in _sim.Units)
            {
                if (!_badges.TryGetValue(unit.Id, out var badge) || badge == null) continue;
                var position = Vector2.Lerp(_shown[unit.Id], ToView(unit), t);
                _shown[unit.Id] = position;
                ((RectTransform)badge.transform.parent).anchoredPosition = position;
            }
        }

        private Vector2 ToView(BattleUnit unit) => new Vector2(unit.X * _scale, unit.Y * _scale);

        private IEnumerator FloatNumber(BattleEvent e, UnitBadge badge)
        {
            _floating++;
            var label = Ui.Label("Damage", _numbers, NumberFormat.Short(e.Amount), UiStyle.BodySize, e.Countered ? UiStyle.Gold : NormalHit);
            label.fontStyle = e.Countered ? FontStyle.Bold : FontStyle.Normal;
            var rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(_badgeSize * 2f, UiStyle.BodySize * 1.5f);
            var start = ((RectTransform)badge.transform.parent).anchoredPosition + new Vector2(0, _badgeSize * 0.5f);
            var time = 0f;
            while (time < NumberSeconds && label != null)
            {
                time += Time.unscaledDeltaTime;
                var k = time / NumberSeconds;
                rect.anchoredPosition = start + new Vector2(0, NumberRise * k);
                var color = label.color;
                color.a = 1f - k;
                label.color = color;
                yield return null;
            }
            if (label != null) Destroy(label.gameObject);
            _floating--;
        }

        private static IEnumerator FadeOut(GameObject target)
        {
            var group = target.GetComponent<CanvasGroup>();
            if (group == null) group = target.AddComponent<CanvasGroup>();
            var time = 0f;
            while (time < DeathFadeSeconds && group != null)
            {
                time += Time.unscaledDeltaTime;
                group.alpha = 1f - time / DeathFadeSeconds;
                yield return null;
            }
            if (group != null) group.alpha = 0f;
        }
    }
}

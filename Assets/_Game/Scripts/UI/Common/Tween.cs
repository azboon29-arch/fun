using System.Collections;
using UnityEngine;

namespace Game.UI
{
    /// <summary>Minimal coroutine tweens (no DOTween dependency).</summary>
    public static class Tween
    {
        /// <summary>Scales <paramref name="target"/> up to <paramref name="peak"/> and back to 1 over <paramref name="duration"/> seconds.</summary>
        public static IEnumerator Punch(Transform target, float peak, float duration)
        {
            var time = 0f;
            while (time < duration && target != null)
            {
                time += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(time / duration);
                var s = 1f + (peak - 1f) * Mathf.Sin(t * Mathf.PI);
                target.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            if (target != null) target.localScale = Vector3.one;
        }

        /// <summary>Grows <paramref name="target"/> from zero with a slight overshoot.</summary>
        public static IEnumerator PopIn(Transform target, float duration)
        {
            const float overshoot = 1.70158f; // standard "back" easing constant
            var time = 0f;
            while (time < duration && target != null)
            {
                time += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(time / duration) - 1f;
                var s = t * t * ((overshoot + 1f) * t + overshoot) + 1f;
                target.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            if (target != null) target.localScale = Vector3.one;
        }

        /// <summary>Holds <paramref name="group"/> visible for <paramref name="hold"/> seconds, then fades it out.</summary>
        public static IEnumerator ShowThenFade(CanvasGroup group, float hold, float fade)
        {
            group.alpha = 1f;
            var time = 0f;
            while (time < hold) { time += Time.unscaledDeltaTime; yield return null; }
            time = 0f;
            while (time < fade && group != null)
            {
                time += Time.unscaledDeltaTime;
                group.alpha = 1f - Mathf.Clamp01(time / fade);
                yield return null;
            }
            if (group != null) group.alpha = 0f;
        }
    }
}

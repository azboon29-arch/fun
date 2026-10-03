using System;
using Game.Core;
using Game.Core.Events;
using Game.Core.Save;
using Game.Core.Services;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.App
{
    /// <summary>
    /// Development-only overlay (IMGUI) showing save status, with buttons to save, reset the save and switch scenes.
    /// Replaced by real UI from Phase 2 onwards; kept for debugging.
    /// </summary>
    public sealed class DebugOverlay : MonoBehaviour
    {
        private const float ReferenceWidth = 1080f;
        private const int FontSize = 36;
        private const float Margin = 24f;

        private SaveService _save;
        private IDisposable _savedSub;
        private string _lastEvent = "-";
        private bool _expanded = true;
        private GUIStyle _label;
        private GUIStyle _button;

        private void Start()
        {
            var services = GameServices.Current;
            if (services == null) return;
            _save = services.Get<SaveService>();
            _savedSub = services.Get<EventBus>().Subscribe<GameSaved>(e =>
                _lastEvent = "Saved " + DateTimeOffset.FromUnixTimeSeconds(e.SavedAtUnixSeconds).ToLocalTime().ToString("T"));
        }

        private void OnDestroy() => _savedSub?.Dispose();

        private void OnGUI()
        {
            if (_save == null) return;
            var scale = Screen.width / ReferenceWidth;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            EnsureStyles();

            var width = ReferenceWidth - Margin * 2;
            GUILayout.BeginArea(new Rect(Margin, Margin + Screen.safeArea.yMin / scale, width, Screen.height / scale));
            if (GUILayout.Button(_expanded ? "▼ Debug" : "▶ Debug", _button)) _expanded = !_expanded;
            if (_expanded)
            {
                GUILayout.Label($"{GameInfo.Title} · scene {SceneManager.GetActiveScene().name}", _label);
                GUILayout.Label($"Load: {_save.LastLoadStatus} · dirty: {_save.IsDirty} · {_lastEvent}", _label);
                GUILayout.Label($"Last saved: {(_save.LastSavedUtc.HasValue ? _save.LastSavedUtc.Value.ToLocalTime().ToString("g") : "never")}", _label);

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Save now", _button)) _save.Save();
                if (GUILayout.Button("Delete save", _button)) _save.DeleteAll();
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                if (GUILayout.Button(SceneNames.Base, _button)) SceneManager.LoadScene(SceneNames.Base);
                if (GUILayout.Button(SceneNames.Battle, _button)) SceneManager.LoadScene(SceneNames.Battle);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndArea();
        }

        private void EnsureStyles()
        {
            if (_label != null) return;
            _label = new GUIStyle(GUI.skin.label) { fontSize = FontSize, wordWrap = true };
            _button = new GUIStyle(GUI.skin.button) { fontSize = FontSize, fixedHeight = FontSize * 2.5f };
        }
    }
}

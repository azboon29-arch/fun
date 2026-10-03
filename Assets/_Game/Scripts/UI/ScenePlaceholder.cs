using Game.Core;
using UnityEngine;

namespace Game.UI
{
    /// <summary>Temporary scene content: draws the scene's title in the middle of the screen until real UI exists.</summary>
    public sealed class ScenePlaceholder : MonoBehaviour
    {
        private const float ReferenceWidth = 1080f;
        private const int FontSize = 64;

        [SerializeField] private string caption = "";

        private GUIStyle _style;

        /// <summary>Sets the caption text (used by the scene builder).</summary>
        public void SetCaption(string text) => caption = text;

        private void OnGUI()
        {
            var scale = Screen.width / ReferenceWidth;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = FontSize, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            }
            GUI.Label(new Rect(0, 0, ReferenceWidth, Screen.height / scale), $"{GameInfo.Title}\n{caption}", _style);
        }
    }
}

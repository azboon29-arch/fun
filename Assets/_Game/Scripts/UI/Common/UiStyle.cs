using UnityEngine;

namespace Game.UI
{
    /// <summary>Shared placeholder colors and sizes for generated UI. Replace with a theme asset during polish.</summary>
    public static class UiStyle
    {
        /// <summary>Screen background behind panels.</summary>
        public static readonly Color Background = new Color(0.13f, 0.17f, 0.13f);

        /// <summary>Panels and bars.</summary>
        public static readonly Color Panel = new Color(0.08f, 0.1f, 0.08f, 0.9f);

        /// <summary>Empty board slot.</summary>
        public static readonly Color Slot = new Color(1f, 1f, 1f, 0.12f);

        /// <summary>Slot highlighted as a drop target.</summary>
        public static readonly Color SlotHighlight = new Color(1f, 0.92f, 0.4f, 0.45f);

        /// <summary>Main call-to-action button.</summary>
        public static readonly Color Primary = new Color(0.95f, 0.62f, 0.15f);

        /// <summary>Secondary buttons.</summary>
        public static readonly Color Secondary = new Color(0.25f, 0.33f, 0.27f);

        /// <summary>Normal text.</summary>
        public static readonly Color Text = Color.white;

        /// <summary>Dark text on light buttons.</summary>
        public static readonly Color TextDark = new Color(0.1f, 0.08f, 0.05f);

        /// <summary>Gold amounts.</summary>
        public static readonly Color Gold = new Color(1f, 0.84f, 0.3f);

        /// <summary>Large title text size.</summary>
        public const int TitleSize = 56;

        /// <summary>Normal text size.</summary>
        public const int BodySize = 40;

        /// <summary>Small text size.</summary>
        public const int SmallSize = 30;

        /// <summary>Minimum touch target edge in reference pixels (about 9 mm on a typical phone).</summary>
        public const float MinTouch = 132f;
    }
}

using UnityEngine;

namespace Game.UI
{
    /// <summary>Generates simple white shapes at runtime, tinted per item. Swappable via sprites in the config assets.</summary>
    public static class PlaceholderSprites
    {
        private const int Size = 128;
        private const float CornerRadius = 0.22f;

        private static Sprite _circle;
        private static Sprite _roundedSquare;
        private static Sprite _star;

        /// <summary>A filled circle.</summary>
        public static Sprite Circle => _circle != null ? _circle : _circle = Create(CircleAlpha);

        /// <summary>A filled square with rounded corners. Also used for panels and buttons.</summary>
        public static Sprite RoundedSquare => _roundedSquare != null ? _roundedSquare : _roundedSquare = Create(RoundedSquareAlpha, true);

        /// <summary>A five-pointed star (result screen).</summary>
        public static Sprite Star => _star != null ? _star : _star = Create(StarAlpha);

        private static Sprite Create(System.Func<float, float, float> alpha, bool sliced = false)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "Placeholder",
            };
            var pixels = new Color32[Size * Size];
            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    // Sample at pixel centers in [-1, 1].
                    var u = (x + 0.5f) / Size * 2f - 1f;
                    var v = (y + 0.5f) / Size * 2f - 1f;
                    var a = Mathf.Clamp01(alpha(u, v));
                    pixels[y * Size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            var border = sliced ? Size * CornerRadius * 0.5f + 2 : 0;
            return Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        private static float CircleAlpha(float u, float v)
        {
            var distance = Mathf.Sqrt(u * u + v * v);
            return (1f - distance) * Size * 0.5f; // about one pixel of anti-aliasing at the edge
        }

        private static float StarAlpha(float u, float v)
        {
            const int points = 5;
            const float outer = 0.98f;
            const float inner = 0.42f;
            var radius = Mathf.Sqrt(u * u + v * v);
            if (radius < 1e-5f) return 1f;
            // Polar star: interpolate the edge radius between a tip and the neighbouring inner corner.
            var angle = Mathf.Atan2(u, v); // 0 = straight up
            var sector = Mathf.PI * 2f / points;
            var local = Mathf.Abs(Mathf.Repeat(angle + sector * 0.5f, sector) - sector * 0.5f) / (sector * 0.5f);
            var edge = Mathf.Lerp(outer, inner, local);
            return (edge - radius) * Size * 0.5f;
        }

        private static float RoundedSquareAlpha(float u, float v)
        {
            var r = CornerRadius;
            var dx = Mathf.Max(Mathf.Abs(u) - (1f - r), 0f);
            var dy = Mathf.Max(Mathf.Abs(v) - (1f - r), 0f);
            var outside = Mathf.Sqrt(dx * dx + dy * dy) - r;
            return -outside * Size * 0.5f;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace VikingsForHire.Followers
{
    /// <summary>
    /// Small round map markers for hirelings, one colour per kind: the game's hammer and house pins overlapped into a
    /// heap round a base. A dot fills about a third of the pin's square (the rest is transparent), with a dark rim so it
    /// shows on any ground.
    /// </summary>
    internal static class MapDots
    {
        private const int Size = 32;
        private const float Radius = 5.5f;
        private const float Rim = 1.5f;

        public static readonly Color Worker = new(0.45f, 0.8f, 1f);
        public static readonly Color Guard = new(1f, 0.4f, 0.3f);
        public static readonly Color Follower = new(1f, 0.85f, 0.25f);

        private static readonly Dictionary<Color, Sprite> Sprites = new();

        public static Sprite Get(Color color)
        {
            if (Sprites.TryGetValue(color, out Sprite s) && s != null)
                return s;
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var rim = new Color(0.08f, 0.06f, 0.04f, 1f);
            float c = (Size - 1) / 2f;
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    // Soft edges: a pixel's coverage of the dot and of the rim.
                    float dot = Mathf.Clamp01(Radius - d + 0.5f);
                    float all = Mathf.Clamp01(Radius + Rim - d + 0.5f);
                    Color px = Color.Lerp(rim, color, dot);
                    px.a = all;
                    tex.SetPixel(x, y, px);
                }
            tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
            s.name = "vfh_map_dot";
            Sprites[color] = s;
            return s;
        }
    }
}

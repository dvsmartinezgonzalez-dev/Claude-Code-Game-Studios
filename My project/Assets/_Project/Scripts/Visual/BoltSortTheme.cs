using UnityEngine;

namespace BoltSort.Visual
{
    public static class BoltSortTheme
    {
        public static readonly Color BackgroundDeep = HexColor("0A0A1A");
        public static readonly Color BackgroundMid  = HexColor("0D0D2B");

        public static readonly Color TubeBody     = HexColor("1A1A3E");
        public static readonly Color TubeRim      = HexColor("2A2A5E");
        public static readonly Color TubeGlow     = HexColor("3A3AFF", 0.15f);
        public static readonly Color TubeSelected = HexColor("4A90FF", 0.30f);

        public static readonly Color[] BoltColors =
        {
            HexColor("FF4757"), // Red    — vivid coral
            HexColor("2ED573"), // Green  — mint
            HexColor("1E90FF"), // Blue   — electric
            HexColor("FFA502"), // Orange — amber
            HexColor("A29BFE"), // Purple — lavender
            HexColor("FF6B9D"), // Pink   — rose
        };

        public static readonly Color BoltShine  = HexColor("FFFFFF", 0.35f);
        public static readonly Color BoltShadow = HexColor("000000", 0.25f);

        public static readonly Color HUDBackground = HexColor("080818", 0.95f);
        public static readonly Color HUDText       = HexColor("E8E8FF");
        public static readonly Color HUDAccent     = HexColor("4A90FF");

        public static readonly Color WinGold = HexColor("FFD700");
        public static readonly Color WinGlow = HexColor("FFD700", 0.40f);

        public static Color HexColor(string hex, float alpha = 1f)
        {
            if (ColorUtility.TryParseHtmlString("#" + hex, out Color c))
            {
                c.a = alpha;
                return c;
            }
            return new Color(1f, 0f, 1f, alpha);
        }

        public static Color BoltColorForId(int colorId)
        {
            if (colorId <= 0) return new Color(0.165f, 0.165f, 0.290f, 0.80f);
            return BoltColors[(colorId - 1) % BoltColors.Length];
        }

        public static Color BrightnessMult(Color c, float mult)
        {
            return new Color(
                Mathf.Clamp01(c.r * mult),
                Mathf.Clamp01(c.g * mult),
                Mathf.Clamp01(c.b * mult),
                c.a);
        }
    }
}

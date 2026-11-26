using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DoomDriver.Phone
{
    /// <summary>
    /// Shared look of the phone OS for UI that is built in code (KurierShop, shift briefing / report).
    /// Palette, sizes and focus behaviour are taken from PhoneOSCanvas.prefab and OrderCard.prefab, so
    /// code-built screens match the hand-made Courier app: cream background, white rounded cards,
    /// Fredoka font, salmon subtitles, light-blue info pills and a mint action button.
    /// Sprites and font are borrowed at runtime from the phone hierarchy (they are not in Resources).
    /// </summary>
    public static class PhoneUIStyle
    {
        public static readonly Color Background = Hex(0xFEF6EC);
        public static readonly Color Card = Color.white;
        public static readonly Color TextPrimary = Hex(0x3A3A3A);
        public static readonly Color TextMuted = Hex(0x3A3A3A, 0.65f);
        public static readonly Color Subtitle = Hex(0xF98A6C);
        public static readonly Color Indigo = Hex(0x4338CA);
        public static readonly Color IndigoDark = Hex(0x1E1B4B);
        public static readonly Color Pill = Hex(0xBBD7F3);
        public static readonly Color PillLight = Hex(0xDAEBFD);
        public static readonly Color Positive = Hex(0x32CB93);
        public static readonly Color ActionButton = Hex(0x34D399);
        public static readonly Color Negative = Hex(0xF43F5E);
        public static readonly Color NegativeLight = Hex(0xFECDD3);
        public static readonly Color Disabled = Hex(0xE7E1D8);
        public static readonly Color CardOutline = new Color(0.132f, 0.132f, 0.132f, 0.5f);
        public static readonly Color ButtonOutline = new Color(0.118f, 0.106f, 0.294f, 0.5f);

        public const float HEADER_SIZE = 32f;
        public const float SUBTITLE_SIZE = 22f;
        public const float CARD_TITLE_SIZE = 22f;
        public const float BODY_SIZE = 17f;
        public const float SMALL_SIZE = 15f;
        public const float BUTTON_TEXT_SIZE = 22f;
        public const float FOCUSED_SCALE = 1.04f;
        public const float SCALE_SPEED = 12f;

        public static readonly Vector2 CardOutlineDistance = new Vector2(2f, -2f);
        public static readonly Vector2 ButtonOutlineNormal = new Vector2(1f, -1f);
        public static readonly Vector2 ButtonOutlineFocused = new Vector2(3f, -3f);

        private const string FONT_NAME_HINT = "Fredoka";
        private const string LARGE_RADIUS_SPRITE = "RectangleRadius24";
        private const string SMALL_RADIUS_SPRITE = "RectangleRadius16";

        /// <summary>
        /// Font and rounded sprites resolved from the phone, used by the Create* helpers.
        /// </summary>
        public readonly struct Assets
        {
            public readonly TMP_FontAsset Font;
            public readonly Sprite LargeRadius;
            public readonly Sprite SmallRadius;

            public Assets(TMP_FontAsset font, Sprite largeRadius, Sprite smallRadius)
            {
                Font = font;
                LargeRadius = largeRadius;
                SmallRadius = smallRadius;
            }
        }

        /// <summary>
        /// Finds the Fredoka font and rounded-rectangle sprites anywhere under the phone OS that owns context.
        /// </summary>
        public static Assets Resolve(Component context)
        {
            Transform root = PhoneRoot(context);

            TMP_FontAsset font = null;
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.font == null) continue;
                if (font == null) font = text.font;
                if (text.font.name.Contains(FONT_NAME_HINT))
                {
                    font = text.font;
                    break;
                }
            }

            Sprite large = null;
            Sprite small = null;
            foreach (var image in root.GetComponentsInChildren<Image>(true))
            {
                if (image.sprite == null) continue;
                if (large == null && image.sprite.name == LARGE_RADIUS_SPRITE) large = image.sprite;
                if (small == null && image.sprite.name == SMALL_RADIUS_SPRITE) small = image.sprite;
            }

            return new Assets(font, large, small != null ? small : large);
        }

        /// <summary>
        /// Rounded image (sliced sprite). Falls back to a plain rectangle when the sprite is missing.
        /// </summary>
        public static Image AddRoundedImage(GameObject target, Sprite sprite, Color color)
        {
            var image = target.AddComponent<Image>();
            image.color = color;
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 1f;
            }

            return image;
        }

        /// <summary>
        /// White rounded card with the same soft dark outline as OrderCard.
        /// </summary>
        public static Image AddCard(GameObject target, Assets assets)
        {
            var image = AddRoundedImage(target, assets.LargeRadius, Card);
            var outline = target.AddComponent<Outline>();
            outline.effectColor = CardOutline;
            outline.effectDistance = CardOutlineDistance;
            return image;
        }

        public static TMP_Text CreateText(Transform parent, Assets assets, float size, FontStyles style, TextAlignmentOptions alignment, Color color, string name = "Text")
        {
            var text = CreateRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            if (assets.Font != null) text.font = assets.Font;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            return text;
        }

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Rewrites the "small salmon subtitle + big title" header (DeliveriesHeaderPanel) copied into app panels.
        /// </summary>
        public static void SetPanelHeader(Transform panel, string subtitle, string title)
        {
            foreach (var text in panel.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.transform.parent == null || text.transform.parent.name != "DeliveriesHeaderPanel") continue;
                if (text.name == "DateText") text.text = subtitle;
                else if (text.name == "HeaderText") text.text = title;
            }
        }

        private static Transform PhoneRoot(Component context)
        {
            var phone = context.GetComponentInParent<PhoneOSController>(true);
            if (phone != null) return phone.transform;
            return PhoneOSController.Instance != null ? PhoneOSController.Instance.transform : context.transform;
        }

        private static Color Hex(int rgb, float alpha = 1f)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);
        }
    }
}

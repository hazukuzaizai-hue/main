using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace TikTokLiveGame
{
    internal sealed class GiftNotificationHud
    {
        private const int Columns = 8;
        private const int Rows = 4;
        private const float FramesPerSecond = 12f;
        private Texture2D atlas;
        private bool loaded;
        private GUIStyle title;
        private GUIStyle detail;

        public void Draw(GiftEffectManager gift, float width, float top)
        {
            if (Event.current.type != EventType.Repaint) return;
            float bannerWidth = Mathf.Min(570f, width - 24f);
            if (bannerWidth < 100f) return;
            LoadAtlas();
            title ??= Label(Color.white);
            detail ??= Label(new Color(1f, 0.88f, 0.53f));
            float height = bannerWidth * 0.25f;
            float left = (width - bannerWidth) * 0.5f;
            Rect banner = new(left, top, bannerWidth, height);
            if (atlas != null)
            {
                int frame = Mathf.FloorToInt((Time.unscaledTime - gift.BannerStarted) * FramesPerSecond) % (Columns * Rows);
                float insetX = 0.5f / atlas.width;
                float insetY = 0.5f / atlas.height;
                Rect uv = new((frame % Columns) / (float)Columns + insetX,
                    1f - (frame / Columns + 1) / (float)Rows + insetY,
                    1f / Columns - insetX * 2f, 1f / Rows - insetY * 2f);
                GUI.DrawTextureWithTexCoords(banner, atlas, uv);
            }
            else
            {
                GUI.DrawTexture(new Rect(left + bannerWidth * 0.245f, top + height * 0.26f, bannerWidth * 0.61f, height * 0.47f),
                    Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0.42f, 0.045f, 0.055f, 0.96f), 0f, 12f);
            }

            Texture2D avatar = gift.BannerActor != null ? gift.BannerActor.AvatarTexture : null;
            if (avatar != null && atlas != null)
            {
                float diameter = height * 0.42f;
                Rect portrait = new(left + bannerWidth * 0.198f - diameter * 0.5f, top + height * 0.50f - diameter * 0.5f,
                    diameter, diameter);
                GUI.DrawTexture(portrait, avatar, ScaleMode.ScaleAndCrop, true, 0f, Color.white, 0f, diameter * 0.5f);
            }

            bool party = gift.BannerSender == null;
            string headline = party ? "Cả sàn cùng quẩy" : "Cảm ơn " + gift.BannerSender;
            title.fontSize = Mathf.Clamp(Mathf.RoundToInt(height * 0.155f), 11, 24);
            detail.fontSize = Mathf.Clamp(Mathf.RoundToInt(height * 0.11f), 9, 17);
            float textX = left + bannerWidth * 0.277f;
            float textWidth = bannerWidth * 0.537f;
            if (party)
            {
                DrawLine(new Rect(textX, top + height * 0.35f, textWidth, height * 0.27f), headline, title);
                return;
            }
            DrawLine(new Rect(textX, top + height * 0.288f, textWidth, height * 0.235f), headline, title);
            string quantity = gift.BannerQuantity > 1 ? $" ×{gift.BannerQuantity}" : string.Empty;
            DrawLine(new Rect(textX, top + height * 0.518f, textWidth, height * 0.185f), gift.BannerGift, detail, quantity);
        }

        private void LoadAtlas()
        {
            if (loaded) return;
            loaded = true;
            string path = Path.Combine(GamePaths.Library, "interface", "gift-thanks-atlas.png");
            try
            {
                if (!File.Exists(path)) return;
                atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!atlas.LoadImage(File.ReadAllBytes(path), true) || atlas.width != 3200 || atlas.height != 400)
                {
                    Dispose();
                    return;
                }
                atlas.wrapMode = TextureWrapMode.Clamp;
                atlas.filterMode = FilterMode.Bilinear;
            }
            catch (Exception error)
            {
                Dispose();
                Debug.LogWarning($"Không tải được banner quà: {error.Message}");
            }
        }

        public void Dispose()
        {
            if (atlas != null) UnityEngine.Object.Destroy(atlas);
            atlas = null;
        }

        private static void DrawLine(Rect rect, string value, GUIStyle style, string suffix = "")
        {
            value = value.Replace('\n', ' ').Replace('\r', ' ');
            GUIContent content = new(value + suffix);
            if (style.CalcSize(content).x > rect.width)
            {
                int[] boundaries = StringInfo.ParseCombiningCharacters(value);
                for (int i = boundaries.Length - 1; i >= 0; i--)
                {
                    content.text = value.Substring(0, boundaries[i]).TrimEnd() + "…" + suffix;
                    if (style.CalcSize(content).x <= rect.width) break;
                }
            }
            Color foreground = style.normal.textColor;
            style.normal.textColor = new Color(0.23f, 0.015f, 0.025f, 0.95f);
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), content, style);
            style.normal.textColor = foreground;
            GUI.Label(rect, content, style);
        }

        private static GUIStyle Label(Color color) => new(GUI.skin.label)
        {
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = color },
            padding = new RectOffset(),
            clipping = TextClipping.Clip,
            wordWrap = false,
            richText = false
        };
    }
}

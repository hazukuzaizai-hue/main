using System;
using System.Collections.Generic;
using UnityEngine;

namespace TikTokLiveGame
{
    internal sealed class GiftGuideHud
    {
        internal const float Top = 57f;
        private GiftGuideItem[] items = Array.Empty<GiftGuideItem>();
        private readonly Dictionary<string, Sprite> icons = new();
        private GUIStyle action;
        private GUIStyle price;
        internal bool HasItems => items.Length > 0;

        internal void SetItems(GiftGuideItem[] guide)
        {
            items = guide ?? Array.Empty<GiftGuideItem>();
            foreach (GiftGuideItem item in items)
            {
                string url = item.giftPictureUrl;
                if (string.IsNullOrEmpty(url) || icons.ContainsKey(url) || AvatarService.Instance == null) continue;
                icons[url] = null;
                AvatarService.Instance.Load(url, icon => icons[url] = icon);
            }
        }

        internal void Draw(float width)
        {
            if (items.Length == 0 || Event.current.type != EventType.Repaint) return;
            action ??= Label(13, Color.white, true);
            price ??= Label(14, new Color(1f, 0.84f, 0.54f), true);
            const float left = 12f, top = Top, padding = 5f;
            const float iconSize = 34f, cellHeight = 76f;
            float maxWidth = width * 0.5f;
            int columns = Mathf.Clamp(Mathf.FloorToInt((maxWidth - padding * 2f) / 70f), 1, 7);
            float cellWidth = (maxWidth - padding * 2f) / columns;
            int count = items.Length;
            float panelWidth = Mathf.Min(count, columns) * cellWidth + padding * 2f;
            float panelHeight = Mathf.CeilToInt(count / (float)columns) * cellHeight + padding * 2f;
            GUI.DrawTexture(new Rect(left, top, panelWidth, panelHeight), Texture2D.whiteTexture,
                ScaleMode.StretchToFill, true, 0f, new Color(0.025f, 0.04f, 0.09f, 0.63f), 0f, 8f);

            for (int i = 0; i < count; i++)
            {
                GiftGuideItem item = items[i];
                float x = left + padding + i % columns * cellWidth;
                float y = top + padding + i / columns * cellHeight;
                Rect iconRect = new(x + (cellWidth - iconSize) * 0.5f, y, iconSize, iconSize);
                if (!string.IsNullOrEmpty(item.giftPictureUrl) && icons.TryGetValue(item.giftPictureUrl, out Sprite icon) && icon != null)
                    GUI.DrawTexture(iconRect, icon.texture, ScaleMode.ScaleToFit);
                else
                    GUI.Label(iconRect, "◇", action);
                if (item.diamondCount > 0)
                    GUI.Label(new Rect(x, y + iconSize + 1f, cellWidth, 20f), $"{item.diamondCount:N0} xu", price);
                action.fontSize = 13;
                GUIContent label = new(item.label ?? "");
                const float textGap = 12f;
                float available = Mathf.Max(12f, cellWidth - textGap);
                float textWidth = action.CalcSize(label).x;
                if (textWidth > available)
                {
                    float loopWidth = textWidth + 28f;
                    float offset = Mathf.Repeat(Time.unscaledTime * 22f, loopWidth);
                    GUI.BeginGroup(new Rect(x + textGap * 0.5f, y + iconSize + 21f, available, 20f));
                    GUI.Label(new Rect(-offset, 0f, textWidth + 1f, 20f), label, action);
                    GUI.Label(new Rect(loopWidth - offset, 0f, textWidth + 1f, 20f), label, action);
                    GUI.EndGroup();
                }
                else
                    GUI.Label(new Rect(x + textGap * 0.5f, y + iconSize + 21f, available, 20f), label, action);
            }
        }

        private static GUIStyle Label(int size, Color color, bool bold) => new(GUI.skin.label)
        {
            fontSize = size,
            fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
            alignment = TextAnchor.MiddleCenter,
            padding = new RectOffset(),
            clipping = TextClipping.Clip,
            normal = { textColor = color }
        };
    }
}

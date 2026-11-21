using System.IO;
using UnityEngine;

namespace TikTokLiveGame
{
    internal sealed class ComboDragonHud
    {
        private Texture2D atlas;
        private bool loaded;
        private GUIStyle label;
        private Texture2D shade;
        private readonly Color[] shadePixels = new Color[128 * 128];

        public void Draw(GiftEffectManager effects, float width, float height)
        {
            if (effects == null || Time.unscaledTime >= effects.ComboUntil || Event.current.type != EventType.Repaint) return;
            if (!loaded)
            {
                loaded = true;
                string path = Path.Combine(GamePaths.Library, "interface", "combo-dragon-atlas.png");
                if (File.Exists(path))
                {
                    atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    atlas.LoadImage(File.ReadAllBytes(path), true);
                }
            }
            if (atlas == null) return;
            float elapsed = Time.unscaledTime - effects.ComboStarted;
            float appear = Mathf.SmoothStep(0f, 1f, elapsed / 0.3f);
            float fade = Mathf.Clamp01((effects.ComboUntil - Time.unscaledTime) / 0.4f);
            DrawShade(effects.ComboActor, width, height, appear * fade);
            float size = Mathf.Min(width * 1.05f, 950f) * Mathf.Lerp(0.8f, 1f, appear);
            Rect rect = new(width - size, 0f, size, size * 338f / 407f);
            int frame = Mathf.FloorToInt(elapsed / 0.09f) % 18;
            Rect uv = new(frame % 6 / 6f, 1f - (frame / 6 + 1) / 3f, 1f / 6f, 1f / 3f);
            Color original = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, appear * fade);
            GUI.DrawTextureWithTexCoords(rect, atlas, uv);
            label ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 25, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight,
                normal = { textColor = new Color(1f, 0.8f, 0.2f) }
            };
            GUI.Label(new Rect(width - 170f, rect.height * 0.45f, 150f, 34f), $"COMBO ×{effects.ComboCount}", label);
            GUI.color = original;
        }

        private void DrawShade(PlayerActor actor, float width, float height, float alpha)
        {
            shade ??= new Texture2D(128, 128, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
            };
            Vector2 center = new(-10f, -10f);
            Vector2 radius = Vector2.one;
            Camera camera = Camera.main;
            if (actor != null && camera != null)
            {
                Bounds bounds = actor.ComboBounds;
                Vector2 low = Vector2.one * float.PositiveInfinity;
                Vector2 high = Vector2.one * float.NegativeInfinity;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = camera.WorldToViewportPoint(bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f)));
                    if (point.z <= 0f) continue;
                    low = Vector2.Min(low, point);
                    high = Vector2.Max(high, point);
                }
                if (!float.IsInfinity(low.x))
                {
                    center = (low + high) * 0.5f;
                    radius = Vector2.Max((high - low) * 0.9f, new Vector2(0.1f, 0.1f));
                }
            }
            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 128; x++)
                {
                    float dx = ((x + 0.5f) / 128f - center.x) / radius.x;
                    float dy = ((y + 0.5f) / 128f - center.y) / radius.y;
                    float mask = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 1f, Mathf.Sqrt(dx * dx + dy * dy)));
                    shadePixels[y * 128 + x] = new Color(0.015f, 0.008f, 0.035f, 0.72f * alpha * mask);
                }
            shade.SetPixels(shadePixels);
            shade.Apply(false);
            GUI.DrawTexture(new Rect(0f, 0f, width, height), shade);
        }

        public void Dispose()
        {
            if (atlas != null) Object.Destroy(atlas);
            if (shade != null) Object.Destroy(shade);
        }
    }
}

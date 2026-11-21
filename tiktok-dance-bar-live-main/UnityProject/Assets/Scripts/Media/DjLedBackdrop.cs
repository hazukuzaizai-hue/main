using UnityEngine;

namespace TikTokLiveGame
{
    public sealed class DjLedBackdrop : MonoBehaviour
    {
        private const int Width = 256, Height = 128;
        private Texture2D texture;
        private Renderer screenRenderer;
        public void SetDisplay(Texture videoTexture, bool visible)
        {
            if (screenMaterial != null) screenMaterial.mainTexture = videoTexture != null ? videoTexture : texture;
            if (screenRenderer != null) screenRenderer.enabled = visible;
        }
        private Material screenMaterial;
        private Material frameMaterial;
        private readonly Material[] sideMaterials = new Material[12];
        private readonly Color[] pixels = new Color[Width * Height];
        private readonly float[] bands = new float[32];
        private DjAudioReactive audio;
        private float nextFrame;

        private void Start()
        {
            audio = FindFirstObjectByType<DjAudioReactive>();
            frameMaterial = GameMaterials.Lit(new Color(0.025f, 0.035f, 0.055f), 0.8f, 0.7f, new Color(0.005f, 0.012f, 0.025f));
            Box("LED Screen Frame", new Vector3(0f, 5.25f, -9.8f), new Vector3(9.4f, 4.55f, 0.18f), frameMaterial);
            Material template = GameMaterials.BackgroundSprite();
            screenMaterial = new Material(Shader.Find("Sprites/Default") ?? template.shader); Destroy(template);
            // Draw the LED image before the transparent moving-head beams (queue 2990).
            screenMaterial.renderQueue = 2980;
            texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp; texture.filterMode = FilterMode.Bilinear;
            screenMaterial.mainTexture = texture;
            GameObject screen = GameObject.CreatePrimitive(PrimitiveType.Quad);
            screen.name = "Audio Reactive LED Wall"; screen.transform.SetParent(transform, false);
            screen.transform.localPosition = new Vector3(0f, 5.25f, -9.68f);
            screen.transform.localScale = new Vector3(9.05f, 4.2f, 1f);
            Destroy(screen.GetComponent<Collider>()); screenRenderer = screen.GetComponent<Renderer>(); screenRenderer.sharedMaterial = screenMaterial;
            for (int i = 0; i < sideMaterials.Length; i++) sideMaterials[i] = GameMaterials.Club(Color.black, Color.black);
            for (int side = -1; side <= 1; side += 2)
            {
                Box("LED Wall Side Frame", new Vector3(side * 5.13f, 5.25f, -9.65f), new Vector3(0.38f, 4.55f, 0.18f), frameMaterial);
                for (int i = 0; i < 12; i++)
                    Box("LED Wall Side Segment", new Vector3(side * 5.13f, 3.25f + i * 0.36f, -9.53f), new Vector3(0.17f, 0.26f, 0.035f), sideMaterials[i]);
            }
        }

        private void Box(string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject item = GameObject.CreatePrimitive(PrimitiveType.Cube); item.name = name;
            item.transform.SetParent(transform, false); item.transform.localPosition = position; item.transform.localScale = scale;
            Destroy(item.GetComponent<Collider>()); item.GetComponent<Renderer>().sharedMaterial = material;
        }

        private void Update()
        {
            if (texture == null || Time.unscaledTime < nextFrame) return;
            nextFrame = Time.unscaledTime + 0.067f;
            float bass = audio != null ? audio.Bass : 0f;
            for (int i = 0; i < bands.Length; i++) bands[i] = audio != null ? audio.Band(Mathf.RoundToInt(i * 16f / 31f)) : 0f;
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    float u = x / (float)Width, v = y / (float)Height;
                    Color color = Color.Lerp(new Color(0.009f, 0.02f, 0.05f), new Color(0.028f, 0.008f, 0.055f), u);
                    int bar = Mathf.Min(31, x / 8);
                    float level = 0.035f + bands[bar] * 0.62f;
                    Color neon = Color.Lerp(new Color(0.04f, 0.8f, 1f), new Color(0.95f, 0.12f, 0.7f), u);
                    // Segmented columns sit below a thin, continuous waveform.
                    if (x % 8 < 5 && v < level && y % 5 < 3)
                        color += neon * (0.22f + 0.48f * (1f - v));
                    float wave = 0.77f + Mathf.Sin(u * 13f + Time.time * 1.5f) * (0.015f + bass * 0.07f);
                    float line = Mathf.Exp(-Mathf.Abs(v - wave) * 160f);
                    color += neon * line * (0.25f + bass * 0.3f);
                    if (x < 2 || x > Width - 3 || y < 2 || y > Height - 3) color += neon * 0.3f;
                    color.a = 1f; pixels[y * Width + x] = color;
                }
            texture.SetPixels(pixels); texture.Apply(false);
            for (int i = 0; i < sideMaterials.Length; i++)
            {
                Color color = Color.Lerp(new Color(0.03f, 0.65f, 1f), new Color(0.8f, 0.12f, 1f), i / 11f);
                float gain = 0.13f + (audio != null ? audio.Band(i) : 0f) * 0.7f;
                sideMaterials[i].SetColor("_EmissionColor", color * gain);
            }
        }

        private void OnDestroy()
        {
            if (texture != null) Destroy(texture);
            if (screenMaterial != null) Destroy(screenMaterial);
            if (frameMaterial != null) Destroy(frameMaterial);
            foreach (Material material in sideMaterials) if (material != null) Destroy(material);
        }
    }
}

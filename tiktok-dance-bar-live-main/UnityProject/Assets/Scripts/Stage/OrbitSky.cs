using UnityEngine;

namespace TikTokLiveGame
{
    internal static class OrbitSky
    {
        public static Texture2D Create()
        {
            const int width = 960;
            const int height = 840;
            Color32[] pixels = new Color32[width * height];
            Color deepSpace = new(0.012f, 0.014f, 0.048f);
            Color purple = new(0.35f, 0.09f, 0.57f);
            Color blue = new(0.045f, 0.31f, 0.55f);
            for (int y = 0; y < height; y++)
            {
                float v = y / (float)height;
                for (int x = 0; x < width; x++)
                {
                    float u = x / (float)width;
                    float cloud = Mathf.PerlinNoise(u * 6f + 14f, v * 8f + 9f);
                    float filaments = Mathf.PerlinNoise(u * 28f + v * 12f, v * 25f + 43f);
                    float ridge = v - 0.58f + (u - 0.5f) * 0.54f + Mathf.Sin(u * 8f) * 0.035f;
                    float diffuse = Mathf.Exp(-ridge * ridge * 26f);
                    float dust = Mathf.Exp(-ridge * ridge * 125f) * Mathf.Pow(cloud, 2f) * filaments;
                    Color tint = Color.Lerp(blue, purple, Mathf.SmoothStep(0f, 1f, u));
                    Color color = deepSpace + tint * (diffuse * cloud * 0.24f + dust * 0.77f);
                    color.a = 1f;
                    pixels[y * width + x] = color;
                }
            }

            System.Random random = new(8304);
            for (int i = 0; i < 1450; i++)
            {
                int x = random.Next(1, width - 1);
                int y = random.Next(1, height - 1);
                float intensity = (float)random.NextDouble();
                Color star = new Color(0.61f, 0.8f, 1f) * (0.22f + intensity * 0.74f);
                star.a = 1f;
                pixels[y * width + x] = star;
                if (intensity < 0.975f) continue;
                Color halo = star * 0.37f;
                halo.a = 1f;
                pixels[y * width + x - 1] = pixels[y * width + x + 1] = halo;
                pixels[(y - 1) * width + x] = pixels[(y + 1) * width + x] = halo;
            }

            Texture2D texture = new(width, height, TextureFormat.RGBA32, false);
            texture.name = "Orbit sky";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}

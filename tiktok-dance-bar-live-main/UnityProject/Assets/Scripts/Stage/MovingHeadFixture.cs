using UnityEngine;

namespace TikTokLiveGame
{
    public enum MovingHeadStyle
    {
        Spot,
        Wash,
        Beam,
        GoboDots,
        GoboStar
    }

    public sealed class MovingHeadFixture : MonoBehaviour
    {
        private static Material beamMaterial;
        private static Texture2D softSpotCookie;
        private static Texture2D dotGoboCookie;
        private static Texture2D starGoboCookie;

        private Color baseColor;
        private float phase;
        private Light spot;
        private LineRenderer beam;
        private static Material landingMaterial;
        private MeshRenderer landing;
        private MaterialPropertyBlock landingProperties;
        private int pair;
        private float side;
        private bool reachesFront;

        public void Initialize(Color color, float fixturePhase, MovingHeadStyle fixtureStyle, int pair, float side)
        {
            baseColor = color;
            phase = fixturePhase;
            this.pair = pair;
            this.side = side;
            reachesFront = pair % 2 == 0;

            bool wash = fixtureStyle == MovingHeadStyle.Wash;
            bool beamStyle = fixtureStyle == MovingHeadStyle.Beam;
            bool gobo = fixtureStyle == MovingHeadStyle.GoboDots || fixtureStyle == MovingHeadStyle.GoboStar;

            spot = gameObject.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.range = wash ? 19f : 24f;
            spot.spotAngle = wash ? 36f : beamStyle ? 10f : gobo ? 24f : 20f;
            spot.innerSpotAngle = spot.spotAngle * 0.62f;
            spot.intensity = wash ? 0.85f : beamStyle ? 3.4f : gobo ? 2.8f : 2.5f;
            spot.color = color;
            spot.cullingMask = -1; // Chiếu sáng mọi layer, bao gồm cả backdrop 2D
            spot.cookie = CookieForStyle(fixtureStyle);
            spot.renderMode = LightRenderMode.ForcePixel;
            spot.enabled = reachesFront;

            float baseStartWidth = wash ? 0.18f : 0.14f;
            float baseEndWidth = reachesFront ? 1.24f : 1.0f;
            beam = CreateRay("Soft Beam", baseStartWidth, baseEndWidth);
            if (landingMaterial == null)
            {
                landingMaterial = GameMaterials.BackgroundSprite();
                landingMaterial.mainTexture = SoftSpotCookie();
                landingMaterial.renderQueue = 3010;
            }
            GameObject footprint = GameObject.CreatePrimitive(PrimitiveType.Quad);
            footprint.name = "Moving Head Floor Spot";
            footprint.transform.SetParent(transform, false);
            Destroy(footprint.GetComponent<Collider>());
            landing = footprint.GetComponent<MeshRenderer>();
            landing.sharedMaterial = landingMaterial;
            landingProperties = new MaterialPropertyBlock();
        }

        private LineRenderer CreateRay(string name, float startWidth, float endWidth)
        {
            if (beamMaterial == null)
            {
                beamMaterial = GameMaterials.BackgroundSprite();
                var profile = new Texture2D(2, 64, TextureFormat.RGBA32, false);
                profile.wrapMode = TextureWrapMode.Clamp;
                profile.filterMode = FilterMode.Bilinear;
                for (int y = 0; y < profile.height; y++)
                {
                    float edge = Mathf.Abs(2f * y / (profile.height - 1) - 1f);
                    float falloff = 1f - edge * edge;
                    float halo = 0.35f * Mathf.Pow(falloff, 2.5f);
                    float core = 0.65f * Mathf.Pow(falloff, 15f);
                    Color pixel = new Color(1f, 1f, 1f, halo + core);
                    profile.SetPixel(0, y, pixel);
                    profile.SetPixel(1, y, pixel);
                }
                profile.Apply(false, true);
                beamMaterial.mainTexture = profile;
            }
            GameObject ray = new(name);
            ray.transform.SetParent(transform, false);
            LineRenderer line = ray.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.sharedMaterial = beamMaterial;
            line.startWidth = startWidth;
            line.endWidth = endWidth;
            return line;
        }

        private void Update()
        {
            if (spot == null || beam == null) return;
            float beat = ClubBeatClock.Beat;
            ClubPulseController show = ClubPulseController.Active;
            float drop = show != null ? show.DropMix : 0f;
            float kick = show != null ? show.Kick : 0f;
            float pairBeat = Mathf.Repeat(beat - pair * 2f, 4f);
            float pairMix = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(pairBeat / 0.24f))
                * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.65f, 2.24f, pairBeat)));
            float level = Mathf.Lerp(pairMix, 1f, drop);
            float sweep = Mathf.Sin(Time.time * 0.61f + phase) * 0.5f + 0.5f;
            float depth = Mathf.Sin(Time.time * 0.36f + phase) * 0.5f + 0.5f;
            Vector3 target = new(
                side * Mathf.Lerp(Mathf.Lerp(0.9f, 4.0f, sweep), 1.4f + pair * 1.16f, drop),
                0.09f,
                Mathf.Lerp(reachesFront ? Mathf.Lerp(9.0f, 13.25f, depth) : Mathf.Lerp(2.6f, 8.2f, depth),
                    12.9f - pair * 1.16f, drop)
            );

            transform.LookAt(target);
            spot.intensity = level * (0.7f + kick * 0.65f);

            Vector3 lens = transform.TransformPoint(new Vector3(0f, 0.08f, 0.32f));
            Color tint = Color.Lerp(baseColor, new Color(0.82f, 0.94f, 1f), kick * Mathf.Max(drop, 0.45f));
            DrawRay(beam, lens, target, Color.Lerp(tint, Color.white, 0.13f), level * (0.63f + kick * 0.24f));

            landing.transform.SetPositionAndRotation(target, Quaternion.Euler(90f, 0f, 0f));
            landing.transform.localScale = new Vector3(0.85f, 1.65f, 1f);
            tint.a = level * (0.48f + kick * 0.26f);
            landingProperties.SetColor("_Color", tint);
            landing.SetPropertyBlock(landingProperties);
        }

        private static void DrawRay(LineRenderer ray, Vector3 lens, Vector3 target, Color tint, float opacity)
        {
            ray.SetPosition(0, lens);
            ray.SetPosition(1, target);
            tint.a = opacity;
            ray.startColor = tint;
            tint.a = opacity * 0.40f;
            ray.endColor = tint;
        }

        private static Texture2D CookieForStyle(MovingHeadStyle fixtureStyle)
        {
            return fixtureStyle switch
            {
                MovingHeadStyle.GoboDots => DotGoboCookie(),
                MovingHeadStyle.GoboStar => StarGoboCookie(),
                _ => SoftSpotCookie()
            };
        }

        private static Texture2D SoftSpotCookie()
        {
            if (softSpotCookie != null) return softSpotCookie;
            const int size = 64;
            softSpotCookie = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = "Soft Moving Head Cookie",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;
                    float radius = Mathf.Sqrt(u * u + v * v);
                    float value = 1f - Mathf.SmoothStep(0.28f, 1f, radius);
                    pixels[y * size + x] = new Color(value, value, value, value);
                }
            softSpotCookie.SetPixels(pixels);
            softSpotCookie.Apply(false, true);
            return softSpotCookie;
        }

        private static Texture2D DotGoboCookie()
        {
            if (dotGoboCookie != null) return dotGoboCookie;
            dotGoboCookie = CreateCookie("Seven Dot Gobo", (u, v) =>
            {
                float value = Disc(u, v, 0f, 0f, 0.19f);
                for (int index = 0; index < 6; index++)
                {
                    float angle = index * Mathf.PI / 3f;
                    value = Mathf.Max(value, Disc(u, v, Mathf.Cos(angle) * 0.55f, Mathf.Sin(angle) * 0.55f, 0.16f));
                }
                return value;
            });
            return dotGoboCookie;
        }

        private static Texture2D StarGoboCookie()
        {
            if (starGoboCookie != null) return starGoboCookie;
            starGoboCookie = CreateCookie("Rotating Star Gobo", (u, v) =>
            {
                float radius = Mathf.Sqrt(u * u + v * v);
                if (radius > 0.92f) return 0f;
                float angle = Mathf.Atan2(v, u);
                float starEdge = Mathf.Lerp(0.25f, 0.82f, Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 5f)), 0.7f));
                float star = 1f - Mathf.SmoothStep(starEdge - 0.08f, starEdge + 0.03f, radius);
                float ring = 1f - Mathf.SmoothStep(0.035f, 0.09f, Mathf.Abs(radius - 0.78f));
                return Mathf.Max(star, ring * 0.72f);
            });
            return starGoboCookie;
        }

        private static Texture2D CreateCookie(string name, System.Func<float, float, float> sampler)
        {
            const int size = 96;
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false, true)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;
                    float value = Mathf.Clamp01(sampler(u, v));
                    pixels[y * size + x] = new Color(value, value, value, value);
                }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static float Disc(float u, float v, float centerX, float centerY, float radius)
        {
            float distance = Mathf.Sqrt((u - centerX) * (u - centerX) + (v - centerY) * (v - centerY));
            return 1f - Mathf.SmoothStep(radius * 0.72f, radius, distance);
        }
    }
}

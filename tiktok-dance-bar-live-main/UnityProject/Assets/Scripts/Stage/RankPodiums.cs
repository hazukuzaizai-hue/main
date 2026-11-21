using System.IO;
using UnityEngine;

namespace TikTokLiveGame
{
    internal sealed class RankPodiums : MonoBehaviour
    {
        private const float StageHeight = 0.75f;
        private readonly Material[][,] leds = new Material[3][,];
        private readonly Material[] crowns = new Material[3];
        private readonly PlayerActor[] occupants = new PlayerActor[3];
        private readonly Sprite[] portraits = new Sprite[3];
        private readonly MeshRenderer[] portraitRenderers = new MeshRenderer[3];
        private readonly Mesh[] portraitMeshes = new Mesh[3];
        private readonly Vector2[] portraitCoordinates = new Vector2[17];
        private readonly Vector2[] portraitUv = new Vector2[17];

        internal void SetOccupant(int rank, PlayerActor actor) => occupants[rank - 1] = actor;
        internal void ClearOccupants() => System.Array.Clear(occupants, 0, occupants.Length);

        internal static Vector3 StandingPosition(int rank, bool raised)
        {
            float height = StageHeight + (raised ? Height(rank) : 0f) + 0.012f;
            return rank switch
            {
                1 => new Vector3(0f, height, -6.24f),
                2 => new Vector3(-2.1f, height, -6.08f),
                3 => new Vector3(2.1f, height, -6.08f),
                _ => Vector3.zero
            };
        }

        internal static Color Accent(int rank) => rank switch
        {
            1 => new Color(1f, 0.73f, 0.20f),
            2 => new Color(0.18f, 0.81f, 1f),
            _ => new Color(1f, 0.43f, 0.22f)
        };

        private static float Height(int rank) => rank switch
        {
            1 => 1.02f,
            2 => 0.72f,
            _ => 0.46f
        };

        internal void Build()
        {
            Material cabinet = GameMaterials.Lit(new Color(0.032f, 0.041f, 0.068f), 0.5f, 0.62f,
                new Color(0.019f, 0.024f, 0.042f));
            Material cap = GameMaterials.Lit(new Color(0.042f, 0.055f, 0.080f), 0.55f, 0.80f, new Color(0.022f, 0.030f, 0.047f));
            Material glass = GameMaterials.Lit(new Color(0.025f, 0.036f, 0.056f), 0.72f, 0.90f, new Color(0.014f, 0.023f, 0.040f));
            Material frameMaterial = GameMaterials.BackgroundSprite();
            for (int rank = 1; rank <= 3; rank++)
            {
                float height = Height(rank);
                Color accent = Accent(rank);
                Transform podium = new GameObject($"Top {rank} podium").transform;
                podium.SetParent(transform, false);
                Vector3 standing = StandingPosition(rank, true);
                podium.localPosition = new Vector3(standing.x, StageHeight, standing.z);

                Material metal = GameMaterials.Lit(Color.Lerp(new Color(0.19f, 0.22f, 0.28f), accent, 0.21f),
                    0.72f, 0.72f, accent * 0.055f);
                float crownSize = rank == 1 ? 0.94f : 0.89f;
                Shell(podium, "Tapered chassis", 0.073f, height - 0.091f, 0.76f, 0.858f, 0.16f, cabinet);
                Shell(podium, "Floating plinth", 0f, 0.056f, crownSize, 0.82f, 0.19f, metal);
                Shell(podium, "Crown bevel", height - 0.082f, height - 0.016f, 0.86f, crownSize, 0.20f, metal);
                Shell(podium, "Smoked top glass", height - 0.014f, height, crownSize - 0.035f, crownSize - 0.05f, 0.18f, cap);
                Material crown = GameMaterials.Club(accent * 0.55f, Color.black);
                crowns[rank - 1] = crown;
                Shell(podium, "Crown light seam", height - 0.092f, height - 0.079f, 0.869f, 0.885f, 0.17f, crown);
                Shell(podium, "Underglow seam", 0.054f, 0.075f, 0.82f, 0.78f, 0.16f,
                    GameMaterials.Club(accent * 0.34f, accent * 0.16f));

                float panelHeight = height * 0.77f;
                float panelY = height * 0.50f;
                Face(podium, "Faceted screen surround", new Vector3(0f, panelY, 0.864f), 0.75f, panelHeight * 0.5f, 0.13f, metal);
                Face(podium, "Smoked front glass", new Vector3(0f, panelY, 0.868f), 0.725f, panelHeight * 0.5f - 0.023f, 0.12f, glass);
                float bezelWidth = BadgeSize(rank, height) * 0.53f + 0.035f;
                Face(podium, "Avatar glass recess", new Vector3(0f, panelY, 0.874f), bezelWidth,
                    panelHeight * 0.44f, 0.065f, cap);

                int rows = rank == 1 ? 5 : rank == 2 ? 4 : 3;
                Material[,] strips = new Material[rows, 2];
                float inner = bezelWidth + 0.035f;
                for (int row = 0; row < rows; row++)
                {
                    float level = row / (rows - 1f);
                    float y = Mathf.Lerp(height * 0.23f, height * 0.77f, level);
                    float outer = Mathf.Lerp(0.60f, 0.685f, Mathf.Sin(level * Mathf.PI));
                    float span = outer - inner;
                    for (int bank = 0; bank < 2; bank++)
                    {
                        int side = bank == 0 ? -1 : 1;
                        Material strip = GameMaterials.Club(accent * 0.48f, Color.black);
                        strips[row, bank] = strip;
                        Face(podium, "Inset LED crystal", new Vector3(side * (inner + span * 0.5f), y, 0.879f),
                            span * 0.5f, height * 0.036f, 0.033f, strip);
                    }
                }
                leds[rank - 1] = strips;

                BuildBadge(podium, rank, height, frameMaterial);
            }
        }

        private void BuildBadge(Transform podium, int rank, float height, Material frameMaterial)
        {
            string path = Path.Combine(GamePaths.Library, "interface", $"podium-top-{rank}.png");
            if (!File.Exists(path)) return;
            Texture2D texture = new(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(File.ReadAllBytes(path), true))
            {
                Destroy(texture);
                return;
            }
            texture.wrapMode = TextureWrapMode.Clamp;
            Transform badge = new GameObject("Podium avatar badge").transform;
            badge.SetParent(podium, false);
            badge.localPosition = new Vector3(0f, height * 0.5f + 0.014f, 0.888f);
            badge.localRotation = Quaternion.Euler(0f, 180f, 0f);
            badge.localScale = Vector3.one * BadgeSize(rank, height);
            SpriteRenderer frame = badge.gameObject.AddComponent<SpriteRenderer>();
            frame.sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width);
            frame.sharedMaterial = frameMaterial;
            frame.sortingOrder = 1;

            GameObject portrait = new("Ranked guest avatar");
            portrait.transform.SetParent(badge, false);
            portrait.transform.localPosition = new Vector3(0f, 0.09f, 0.003f);
            Mesh mesh = BuildPortraitMesh();
            portrait.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = portrait.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = GameMaterials.BackgroundSprite();
            renderer.enabled = false;
            portraitMeshes[rank - 1] = mesh;
            portraitRenderers[rank - 1] = renderer;
        }

        private Mesh BuildPortraitMesh()
        {
            const float width = 0.648f, height = 0.54f, radius = 0.055f;
            Vector3[] points = new Vector3[17];
            Color[] white = new Color[17];
            int[] triangles = new int[48];
            for (int i = 0; i < 16; i++)
            {
                int corner = i / 4;
                float angle = (corner * 90f + i % 4 * 30f) * Mathf.Deg2Rad;
                float x = (corner is 0 or 3 ? 1f : -1f) * (width * 0.5f - radius);
                float y = (corner < 2 ? 1f : -1f) * (height * 0.5f - radius);
                points[i + 1] = new Vector3(x + Mathf.Cos(angle) * radius, y + Mathf.Sin(angle) * radius, 0f);
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = (i + 1) % 16 + 1;
            }
            for (int i = 0; i < points.Length; i++)
            {
                portraitCoordinates[i] = new Vector2(points[i].x / width + 0.5f, points[i].y / width + 0.5f);
                white[i] = Color.white;
            }
            return new Mesh { vertices = points, colors = white, triangles = triangles };
        }

        private void RefreshPortrait(int index)
        {
            MeshRenderer renderer = portraitRenderers[index];
            if (renderer == null) return;
            Sprite sprite = occupants[index] != null ? occupants[index].AvatarSprite : null;
            if (portraits[index] == sprite) return;
            portraits[index] = sprite;
            renderer.enabled = sprite != null;
            if (sprite == null) return;
            Rect crop = sprite.textureRect;
            Texture2D texture = sprite.texture;
            for (int i = 0; i < portraitUv.Length; i++)
            {
                Vector2 point = portraitCoordinates[i];
                portraitUv[i] = new Vector2((crop.x + point.x * crop.width) / texture.width,
                    (crop.y + point.y * crop.height) / texture.height);
            }
            portraitMeshes[index].uv = portraitUv;
            renderer.sharedMaterial.mainTexture = texture;
        }

        private void Update()
        {
            float beat = ClubBeatClock.Beat;
            for (int index = 0; index < leds.Length; index++)
            {
                RefreshPortrait(index);
                Material[,] strips = leds[index];
                Color accent = Accent(index + 1);
                float localBeat = beat - index * 0.22f;
                int pattern = Mathf.FloorToInt(Mathf.Repeat(beat / 8f, 3f));
                float scan = Mathf.Repeat(localBeat / 1.5f, 1f) * 1.34f - 0.16f;
                float hit = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Repeat(localBeat, 1f) / 0.48f);
                float intensity = 1f - index * 0.11f;
                for (int row = 0; row < strips.GetLength(0); row++)
                {
                    float level = row / (strips.GetLength(0) - 1f);
                    for (int bank = 0; bank < 2; bank++)
                    {
                        float pulse = pattern switch
                        {
                            0 => Wave(scan, level, 0.42f),
                            1 => Mathf.Max(Wave(scan, level + bank * 0.15f, 0.33f),
                                Wave(scan - 0.42f, level + bank * 0.15f, 0.22f) * 0.85f),
                            _ => (row + bank + Mathf.FloorToInt(localBeat)) % 2 == 0 ? hit : hit * 0.32f
                        };
                        Color light = Color.Lerp(accent, Color.white, pulse * (index == 0 ? 0.66f : 0.40f));
                        strips[row, bank].SetColor("_Color", light * (0.34f + 0.96f * pulse) * intensity);
                    }
                }
                float crest = pattern == 2 ? hit : Wave(scan, 1.02f, 0.32f);
                crowns[index].SetColor("_Color", Color.Lerp(accent, Color.white, crest * 0.46f) * (0.64f + crest * 0.60f) * intensity);
            }
        }

        private static float Wave(float head, float level, float width) =>
            Mathf.SmoothStep(0f, 1f, 1f - Mathf.Abs(head - level) / width);

        private static float BadgeSize(int rank, float height) => height * (rank == 1 ? 0.76f : 0.72f);

        private static Vector2[] Corners(float x, float y, float cut) => new Vector2[]
        {
            new(-x + cut, y), new(x - cut, y), new(x, y - cut), new(x, -y + cut),
            new(x - cut, -y), new(-x + cut, -y), new(-x, -y + cut), new(-x, y - cut)
        };

        private static void Shell(Transform parent, string name, float bottom, float top,
            float lowerSize, float upperSize, float bevel, Material material)
        {
            Vector2[] lower = Corners(lowerSize, lowerSize, bevel);
            Vector2[] upper = Corners(upperSize, upperSize, bevel);
            Vector3[] vertices = new Vector3[40];
            int[] triangles = new int[66];
            for (int i = 0; i < 8; i++)
            {
                int j = (i + 1) % 8;
                int v = i * 4;
                vertices[v] = new Vector3(lower[i].x, bottom, lower[i].y);
                vertices[v + 1] = new Vector3(lower[j].x, bottom, lower[j].y);
                vertices[v + 2] = new Vector3(upper[j].x, top, upper[j].y);
                vertices[v + 3] = new Vector3(upper[i].x, top, upper[i].y);
                int t = i * 6;
                triangles[t] = v;
                triangles[t + 1] = v + 1;
                triangles[t + 2] = v + 2;
                triangles[t + 3] = v;
                triangles[t + 4] = v + 2;
                triangles[t + 5] = v + 3;
                vertices[32 + i] = new Vector3(upper[i].x, top, upper[i].y);
            }
            for (int i = 0; i < 6; i++)
            {
                triangles[48 + i * 3] = 32;
                triangles[49 + i * 3] = 33 + i;
                triangles[50 + i * 3] = 34 + i;
            }
            AddMesh(parent, name, Vector3.zero, vertices, triangles, material);
        }

        private static void Face(Transform parent, string name, Vector3 center,
            float halfWidth, float halfHeight, float bevel, Material material)
        {
            Vector2[] corners = Corners(halfWidth, halfHeight, Mathf.Min(bevel, halfHeight * 0.78f));
            Vector3[] vertices = new Vector3[8];
            int[] triangles = new int[18];
            for (int i = 0; i < corners.Length; i++) vertices[i] = corners[i];
            for (int i = 0; i < 6; i++)
            {
                triangles[i * 3 + 1] = i + 2;
                triangles[i * 3 + 2] = i + 1;
            }
            AddMesh(parent, name, center, vertices, triangles, material);
        }

        private static void AddMesh(Transform parent, string name, Vector3 position,
            Vector3[] vertices, int[] triangles, Material material)
        {
            GameObject item = new(name);
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            Mesh mesh = new() { vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            item.AddComponent<MeshFilter>().sharedMesh = mesh;
            item.AddComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}

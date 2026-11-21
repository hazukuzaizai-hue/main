using UnityEngine;
using System.Collections.Generic;

namespace TikTokLiveGame
{
    public sealed class NeonDanceFloor : MonoBehaviour
    {
        private readonly Material[] panels = new Material[16];
        private readonly Texture2D[] panelTextures = new Texture2D[16];
        private readonly Color[] gradientPixels = new Color[64 * 64];
        private float nextTextureUpdate;
        private readonly List<Mesh> waveMeshes = new List<Mesh>();
        private readonly List<Vector3[]> waveBaseVertices = new List<Vector3[]>();
        private readonly List<Vector3[]> waveVertices = new List<Vector3[]>();
        private float nextWaveUpdate;
        private DjAudioReactive audioReactive;
        private readonly float[] bassHistory = new float[64];
        private int bassCursor;
        private float floorBass;
        private float bassBaseline;
        private readonly List<MeshRenderer> tileSurfaces = new();
        private readonly MaterialPropertyBlock tileProperties = new();
        private bool showingDropWave;

        private void AddWaveMesh(MeshFilter filter)
        {
            Mesh mesh = Instantiate(filter.sharedMesh);
            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++) vertices[i] = filter.transform.localPosition + filter.transform.localRotation * Vector3.Scale(vertices[i], filter.transform.localScale);
            filter.transform.localPosition = Vector3.zero;
            filter.transform.localRotation = Quaternion.identity;
            filter.transform.localScale = Vector3.one;
            mesh.vertices = vertices; mesh.RecalculateBounds(); filter.sharedMesh = mesh;
            waveMeshes.Add(mesh); waveBaseVertices.Add((Vector3[])vertices.Clone()); waveVertices.Add(vertices);
        }

        private void AnimateBassFloor()
        {
            if (Time.unscaledTime < nextWaveUpdate) return;
            nextWaveUpdate = Time.unscaledTime + 1f / 30f;
            if (audioReactive == null) audioReactive = FindFirstObjectByType<DjAudioReactive>();
            float bass = audioReactive != null ? audioReactive.Bass : 0f;
            bassBaseline = Mathf.Lerp(bassBaseline, bass, 0.035f);
            float hit = Mathf.Clamp01(Mathf.Max(0f, bass - bassBaseline * 0.75f) * 2.5f);
            floorBass = Mathf.Lerp(floorBass, hit, hit > floorBass ? 0.95f : 0.34f);
            bassCursor = (bassCursor + 1) % bassHistory.Length;
            bassHistory[bassCursor] = floorBass;
            for (int m = 0; m < waveMeshes.Count; m++)
            {
                Vector3[] basis = waveBaseVertices[m], vertices = waveVertices[m];
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 v = basis[i];
                    // Bass pressure travels from the stage toward the front; no free-running ocean motion.
                    float distance = Mathf.Max(0f, v.z + 4f);
                    int delay = Mathf.Clamp(Mathf.RoundToInt(distance * 0.65f), 0, 20);
                    float pressure = bassHistory[(bassCursor - delay + bassHistory.Length) % bassHistory.Length];
                    float previous = bassHistory[(bassCursor - delay - 3 + bassHistory.Length) % bassHistory.Length];
                    float wave = (pressure - previous * 0.35f) * 0.24f;
                    wave *= 0.85f + 0.15f * Mathf.Cos(v.x * 0.6f);
                    vertices[i] = v + Vector3.up * wave;
                }
                waveMeshes[m].vertices = vertices;
                waveMeshes[m].RecalculateNormals(); waveMeshes[m].RecalculateBounds();
            }
        }
        private readonly Mesh[] panelMeshes = new Mesh[16];
        private Material glass;
        private Material shaftMaterial;
        private Texture2D shaftTexture;
        private Mesh shaftMesh;
        private Color[] shaftColors;
        private readonly List<Vector3> shaftVertices = new List<Vector3>();
        private readonly List<Vector2> shaftUvs = new List<Vector2>();
        private readonly List<int> shaftTriangles = new List<int>();
        private Material infinity;
        private Texture2D dotsTexture;
        private readonly Color[] palette = {
            new Color(0.72f, 0.12f, 1f), new Color(1f, 0.12f, 0.62f),
            new Color(0.08f, 0.65f, 1f), new Color(0.38f, 0.85f, 1f),
            new Color(0.12f, 1f, 0.35f), new Color(1f, 0.72f, 0.08f),
            new Color(0.06f, 1f, 0.88f), new Color(1f, 0.25f, 0.18f)
        };

        private static int Hash(int seed)
        {
            uint value = (uint)seed;
            value = (value ^ 61u) ^ (value >> 16);
            value *= 9u; value ^= value >> 4; value *= 0x27d4eb2du; value ^= value >> 15;
            return (int)(value & 0x7fffffffu);
        }

        private Color TileColor(int tile, int step) => palette[Hash(tile * 7919 + step * 104729) % palette.Length];
        private float TileOpacity(int tile, int step) => Hash(tile * 3571 + step * 1013) % 5 == 0 ? 0.08f : 0.35f + (Hash(tile + step * 9901) % 60) / 100f;


        private void Start()
        {
            glass = GameMaterials.Lit(new Color(0.032f, 0.045f, 0.075f), 0.62f, 0.96f, new Color(0.018f, 0.026f, 0.045f));
            dotsTexture = CreateTexture(true);
            infinity = GameMaterials.BackgroundSprite();
            infinity.mainTexture = dotsTexture;
            infinity.SetColor("_Color", new Color(0.70f, 0.82f, 1f, 0.82f));
            for (int i = 0; i < panels.Length; i++)
            {
                panels[i] = new Material(Shader.Find("Sprites/Default") ?? infinity.shader);
                panelTextures[i] = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                panelTextures[i].wrapMode = TextureWrapMode.Clamp;
                panels[i].mainTexture = panelTextures[i];
            }
            for (int row = 0; row < 14; row++)
                for (int column = 0; column < 10; column++)
                {
                    Vector3 position = new Vector3((column - 4.5f) * 1.3f, -0.025f, -3.5f + row * 1.3f);
                    GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    tile.name = "Glass Dance Tile " + row + "-" + column;
                    tile.transform.SetParent(transform, false);
                    tile.transform.localPosition = position + new Vector3(0f, -1.18f, 0f);
                    AddShaft(position);
                    tile.transform.localScale = new Vector3(1.265f, 0.035f, 1.265f);
                    Destroy(tile.GetComponent<Collider>());
                    tile.GetComponent<Renderer>().sharedMaterial = glass;
                    AddWaveMesh(tile.GetComponent<MeshFilter>());
                    GameObject surface = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    surface.name = "Infinity LED Surface";
                    surface.transform.SetParent(transform, false);
                    surface.transform.localPosition = position + new Vector3(0f, 0.045f, 0f);
                    surface.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    surface.transform.localScale = new Vector3(1.25f, 1.25f, 1f);
                    Destroy(surface.GetComponent<Collider>());
                    int group = (row * 7 + column * 3) % panels.Length;
                    MeshFilter filter = surface.GetComponent<MeshFilter>();
                    if (panelMeshes[group] == null)
                    {
                        panelMeshes[group] = Instantiate(filter.sharedMesh);
                        panelMeshes[group].colors = new[] { Color.white, Color.white, Color.white, Color.white };
                    }
                    filter.sharedMesh = panelMeshes[group];
                    MeshRenderer tileSurface = surface.GetComponent<MeshRenderer>();
                    tileSurface.sharedMaterial = panels[group];
                    tileSurfaces.Add(tileSurface);
                    AddWaveMesh(filter);
                }
            CreateShaftRenderer();
        }

        private void AddShaft(Vector3 position)
        {
            const float half = 0.6325f;
            Vector3[] corners = {
                new Vector3(-half, 0.04f, -half), new Vector3(half, 0.04f, -half),
                new Vector3(half, 0.04f, half), new Vector3(-half, 0.04f, half)
            };
            for (int side = 0; side < 4; side++)
            {
                Vector3 a = position + corners[side], b = position + corners[(side + 1) % 4];
                int start = shaftVertices.Count;
                shaftVertices.Add(a); shaftVertices.Add(b);
                shaftVertices.Add(b + Vector3.down * 1.18f); shaftVertices.Add(a + Vector3.down * 1.18f);
                shaftUvs.Add(new Vector2(0f, 1f)); shaftUvs.Add(new Vector2(1f, 1f));
                shaftUvs.Add(new Vector2(1f, 0f)); shaftUvs.Add(new Vector2(0f, 0f));
                shaftTriangles.Add(start); shaftTriangles.Add(start + 1); shaftTriangles.Add(start + 2);
                shaftTriangles.Add(start); shaftTriangles.Add(start + 2); shaftTriangles.Add(start + 3);
            }
        }

        private void CreateShaftRenderer()
        {
            const int width = 256, height = 192;
            shaftTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            shaftTexture.wrapMode = TextureWrapMode.Clamp;
            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    float u = (x + 0.5f) / width, v = (y + 0.5f) / height;
                    float dx = (Mathf.Repeat(u * 18f, 1f) - 0.5f) / 0.14f;
                    float dy = (Mathf.Repeat(v * 11f, 1f) - 0.5f) / 0.18f;
                    float dot = Mathf.Exp(-(dx * dx + dy * dy) * 1.3f);
                    float fade = 0.18f + 0.82f * v;
                    Color baseColor = new Color(0.015f, 0.022f, 0.055f) * (0.35f + v * 0.65f);
                    Color led = Color.Lerp(new Color(0.25f, 0.32f, 0.85f), new Color(0.82f, 0.88f, 1f), v);
                    Color color = baseColor + led * dot * fade;
                    if (v > 0.985f) color = new Color(0.16f, 0.22f, 0.38f);
                    color.a = 1f; pixels[y * width + x] = color;
                }
            shaftTexture.SetPixels(pixels); shaftTexture.Apply(false, true);
            shaftMaterial = new Material(Shader.Find("Sprites/Default") ?? infinity.shader);
            shaftMaterial.mainTexture = shaftTexture;
            // Render the batched well walls before glass overlays so they cannot cover distant tiles.
            shaftMaterial.renderQueue = 2450;
            GameObject shaft = new GameObject("Recessed LED Wells");
            shaft.transform.SetParent(transform, false);
            shaftMesh = new Mesh(); shaftMesh.SetVertices(shaftVertices); shaftMesh.SetUVs(0, shaftUvs); shaftMesh.SetTriangles(shaftTriangles, 0);
            Color[] whites = new Color[shaftVertices.Count]; for (int i = 0; i < whites.Length; i++) whites[i] = Color.white;
            shaftColors = whites;
            shaftMesh.colors = whites; shaftMesh.RecalculateNormals(); shaftMesh.RecalculateBounds();
            MeshFilter shaftFilter = shaft.AddComponent<MeshFilter>(); shaftFilter.sharedMesh = shaftMesh;
            AddWaveMesh(shaftFilter);
            Destroy(shaftMesh); shaftMesh = shaftFilter.sharedMesh;
            shaft.AddComponent<MeshRenderer>().sharedMaterial = shaftMaterial;
        }

        private Texture2D CreateTexture(bool dots)
        {
            const int size = 128;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    float v = (y + 0.5f) / size;
                    float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                    if (dots)
                    {
                        float sheen = 0.018f + 0.018f * Mathf.Exp(-Mathf.Pow((u + v - 0.85f) * 2.2f, 2f));
                        pixels[y * size + x] = new Color(0.65f, 0.78f, 1f, sheen);
                    }
                    else
                    {
                        float center = Mathf.Exp(-((u - 0.42f) * (u - 0.42f) + (v - 0.56f) * (v - 0.56f)) * 6f);
                        float rim = Mathf.Exp(-Mathf.Abs(edge - 0.012f) * 230f);
                        float brightness = Mathf.Clamp01(0.24f + center * 0.72f + rim * 0.22f);
                        pixels[y * size + x] = new Color(brightness, brightness, brightness, Mathf.SmoothStep(0f, 0.018f, edge));
                    }
                }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private void Update()
        {
            AnimateBassFloor();
            UpdateDropWave();
            if (Time.unscaledTime < nextTextureUpdate) return;
            nextTextureUpdate = Time.unscaledTime + 0.08f;
            float beat = ClubBeatClock.Beat;
            float cycle = beat * 0.5f;
            int step = Mathf.FloorToInt(cycle);
            float blend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Repeat(cycle, 1f) * 2.8f));
            if (shaftMesh != null && shaftColors != null)
            {
                for (int tile = 0; tile < shaftColors.Length / 16; tile++)
                {
                    Color tint = Color.Lerp(TileColor(tile, step), TileColor(tile, step + 1), blend);
                    tint = Color.Lerp(tint, Color.white, 0.22f); tint.a = 1f;
                    for (int vertex = 0; vertex < 16; vertex++) shaftColors[tile * 16 + vertex] = tint;
                }
                shaftMesh.colors = shaftColors;
            }
            for (int i = 0; i < panels.Length; i++)
            {
                if (panels[i] == null) continue;
                float on = Mathf.Lerp(TileOpacity(i, step), TileOpacity(i, step + 1), blend);
                Color color = Color.Lerp(TileColor(i, step), TileColor(i, step + 1), blend);
                color = Color.Lerp(color, Color.white, 0.12f);
                for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 64; x++)
                    {
                        float u = (x + 0.5f) / 64f, v = (y + 0.5f) / 64f;
                        float glow = 0.28f + 0.72f * Mathf.Exp(-((u - 0.44f) * (u - 0.44f) + (v - 0.55f) * (v - 0.55f)) * 5f);
                        Color pixel = color * glow;
                        pixel.a = Mathf.Lerp(0.025f, 1f, on);
                        gradientPixels[y * 64 + x] = pixel;
                    }
                panelTextures[i].SetPixels(gradientPixels);
                panelTextures[i].Apply(false);
            }
        }

        private void UpdateDropWave()
        {
            ClubPulseController show = ClubPulseController.Active;
            float age = show != null ? show.DropAge : float.PositiveInfinity;
            bool active = age < 1.12f;
            if (!active && !showingDropWave) return;
            showingDropWave = active;
            float waveRow = age * 16f - 1f;
            for (int i = 0; i < tileSurfaces.Count; i++)
            {
                if (!active)
                {
                    tileSurfaces[i].SetPropertyBlock(null);
                    continue;
                }
                float crest = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Abs(i / 10 - waveRow) / 2.5f));
                tileProperties.SetColor("_Color", Color.Lerp(Color.white, new Color(1.4f, 2.3f, 2.8f, 1f), crest));
                tileSurfaces[i].SetPropertyBlock(tileProperties);
            }
        }

        private void OnDestroy()
        {
            foreach (Material material in panels) if (material != null) Destroy(material);
            foreach (Texture2D texture in panelTextures) if (texture != null) Destroy(texture);
            foreach (Mesh mesh in panelMeshes) if (mesh != null) Destroy(mesh);
            foreach (Mesh mesh in waveMeshes) if (mesh != null) Destroy(mesh);
            if (shaftMaterial != null) Destroy(shaftMaterial);
            if (shaftTexture != null) Destroy(shaftTexture);
            if (glass != null) Destroy(glass);
            if (infinity != null) Destroy(infinity);
            if (dotsTexture != null) Destroy(dotsTexture);
        }
    }
}

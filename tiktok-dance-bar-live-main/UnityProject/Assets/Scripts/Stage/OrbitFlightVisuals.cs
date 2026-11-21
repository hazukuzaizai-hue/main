using UnityEngine;

namespace TikTokLiveGame
{
    internal sealed class OrbitFlightVisuals
    {
        private const int StarCount = 180;
        private const float Depth = 112f;
        private readonly Mesh streaks;
        private readonly Transform streakOrigin;
        private readonly Transform sky;
        private readonly LineRenderer[] rings = new LineRenderer[5];
        private readonly (Transform Root, Material Face, Material Edge)[] gates = new (Transform, Material, Material)[7];
        private readonly Vector3[] vertices = new Vector3[StarCount * 4];
        private readonly Color32[] colors = new Color32[StarCount * 4];
        private readonly Vector3[] seeds = new Vector3[StarCount];
        private readonly Color cyan;
        private readonly Color violet;

        public OrbitFlightVisuals(Transform parent, Color cyan, Color violet)
        {
            this.cyan = cyan;
            this.violet = violet;
            GameObject horizon = GameObject.CreatePrimitive(PrimitiveType.Quad);
            horizon.name = "Nebula horizon";
            horizon.transform.SetParent(parent, false);
            horizon.transform.localScale = new Vector3(182f, 182f, 1f);
            Object.Destroy(horizon.GetComponent<Collider>());
            Material backdrop = GameMaterials.BackgroundSprite();
            backdrop.mainTexture = OrbitSky.Create();
            backdrop.renderQueue = 2985;
            horizon.GetComponent<Renderer>().sharedMaterial = backdrop;
            sky = horizon.transform;

            Material light = GameMaterials.BackgroundSprite();
            light.renderQueue = 2991;
            GameObject particles = new("Passing starlight");
            particles.transform.SetParent(parent, false);
            streakOrigin = particles.transform;
            particles.AddComponent<MeshRenderer>().sharedMaterial = light;
            streaks = new Mesh { name = "Star streaks" };
            streaks.MarkDynamic();
            particles.AddComponent<MeshFilter>().sharedMesh = streaks;
            int[] indices = new int[StarCount * 6];
            System.Random random = new(629);
            for (int i = 0; i < StarCount; i++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float radius = Mathf.Lerp(2.1f, 38f, Mathf.Pow((float)random.NextDouble(), 1.65f));
                seeds[i] = new Vector3(Mathf.Cos(angle) * radius * 0.64f, Mathf.Sin(angle) * radius,
                    (float)random.NextDouble() * Depth);
                int vertex = i * 4;
                int index = i * 6;
                indices[index] = vertex;
                indices[index + 1] = vertex + 1;
                indices[index + 2] = vertex + 2;
                indices[index + 3] = vertex + 1;
                indices[index + 4] = vertex + 3;
                indices[index + 5] = vertex + 2;
            }
            streaks.vertices = vertices;
            streaks.triangles = indices;
            streaks.bounds = new Bounds(new Vector3(0f, 0f, -56f), new Vector3(104f, 104f, 140f));

            for (int i = 0; i < rings.Length; i++)
            {
                GameObject item = new("Passing neon arc");
                item.transform.SetParent(parent, false);
                LineRenderer ring = item.AddComponent<LineRenderer>();
                ring.useWorldSpace = false;
                ring.sharedMaterial = light;
                ring.positionCount = 73;
                ring.numCornerVertices = 2;
                ring.widthMultiplier = 0.095f;
                for (int p = 0; p < ring.positionCount; p++)
                {
                    float angle = p / (ring.positionCount - 1f) * Mathf.PI * 1.47f;
                    float radius = 12f + Mathf.Sin(angle * 3f) * 0.48f;
                    ring.SetPosition(p, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
                }
                rings[i] = ring;
            }

            for (int i = 0; i < gates.Length; i++)
            {
                GameObject gate = new(i % 2 == 0 ? "Square warp gate" : "Rectangular warp gate");
                gate.transform.SetParent(parent, false);
                Material face = GameMaterials.Club(Color.black, Color.black);
                Material edge = GameMaterials.Club(Color.black, Color.black);
                Vector2 opening = i % 2 == 0 ? new Vector2(11.8f, 11.8f) : new Vector2(15.5f, 8.9f);
                const float rail = 0.44f;
                const float thickness = 1.2f;
                for (int side = -1; side <= 1; side += 2)
                {
                    float x = side * opening.x * 0.5f;
                    float y = side * opening.y * 0.5f;
                    Rail(gate.transform, new Vector3(x, 0f, 0f), new Vector3(rail, opening.y - rail, thickness), face);
                    Rail(gate.transform, new Vector3(0f, y, 0f), new Vector3(opening.x + rail, rail, thickness), face);
                    Rail(gate.transform, new Vector3(x - side * 0.14f, 0f, thickness * 0.5f), new Vector3(0.086f, opening.y - 0.24f, 0.055f), edge);
                    Rail(gate.transform, new Vector3(0f, y - side * 0.14f, thickness * 0.5f), new Vector3(opening.x - 0.24f, 0.086f, 0.055f), edge);
                }
                gates[i] = (gate.transform, face, edge);
            }
        }

        private static void Rail(Transform gate, Vector3 position, Vector3 scale, Material material)
        {
            GameObject segment = GameObject.CreatePrimitive(PrimitiveType.Cube);
            segment.name = "Portal rail";
            segment.transform.SetParent(gate, false);
            segment.transform.localPosition = position;
            segment.transform.localScale = scale;
            Object.Destroy(segment.GetComponent<Collider>());
            segment.GetComponent<Renderer>().sharedMaterial = material;
        }

        public void Animate(Vector3 camera, float time, float distance, float bass, float boost)
        {
            sky.position = new Vector3(camera.x * 0.28f + Mathf.Sin(time * 0.19f) * 8f, 7f, camera.z - 122f);
            streakOrigin.position = new Vector3(0f, 0f, camera.z);
            for (int i = 0; i < StarCount; i++)
            {
                Vector3 seed = seeds[i];
                float depth = 2f + Mathf.Repeat(seed.z - distance, Depth);
                float life = Mathf.InverseLerp(2f, 9f, depth) * (1f - Mathf.InverseLerp(78f, 114f, depth));
                Vector3 head = new(seed.x, seed.y, -depth);
                Vector3 tail = head + Vector3.back * (2.9f + i % 5 * 0.91f + bass * 3f + boost * 9f);
                Vector3 side = new Vector3(-seed.y, seed.x, 0f).normalized * (0.017f + i % 4 * 0.009f);
                int v = i * 4;
                vertices[v] = head - side;
                vertices[v + 1] = head + side;
                vertices[v + 2] = tail - side * 0.12f;
                vertices[v + 3] = tail + side * 0.12f;
                Color hue = Color.Lerp(i % 3 == 0 ? violet : cyan, Color.white, 0.16f + (i % 5) * 0.12f);
                hue.a = life * (0.68f + 0.29f * boost);
                colors[v] = colors[v + 1] = hue;
                hue.a = 0f;
                colors[v + 2] = colors[v + 3] = hue;
            }
            streaks.SetVertices(vertices, 0, vertices.Length, UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds);
            streaks.SetColors(colors);

            for (int i = 0; i < rings.Length; i++)
            {
                float depth = 3f + Mathf.Repeat(i * (Depth / rings.Length) - distance, Depth);
                LineRenderer ring = rings[i];
                ring.transform.SetPositionAndRotation(new Vector3(Mathf.Sin(depth * 0.11f + time * 0.48f) * 2f, 0.55f, camera.z - depth),
                    Quaternion.Euler(0f, 0f, i * 93f + time * 17f));
                Color glow = Color.Lerp(cyan, violet, Mathf.PingPong(i * 0.32f + time * 0.3f, 1f));
                glow = Color.Lerp(glow, Color.white, 0.14f);
                glow.a = Mathf.InverseLerp(3f, 15f, depth) * (1f - Mathf.InverseLerp(55f, 115f, depth))
                    * (0.24f + 0.13f * bass + 0.25f * boost);
                ring.widthMultiplier = 0.08f + 0.105f * boost;
                ring.startColor = new Color(glow.r, glow.g, glow.b, 0f);
                ring.endColor = glow;
            }

            for (int i = 0; i < gates.Length; i++)
            {
                var gate = gates[i];
                float depth = -3f + Mathf.Repeat(12f + i * (Depth / gates.Length) - distance, Depth);
                gate.Root.gameObject.SetActive(depth > -1.5f && depth < 101f);
                float turn = (i % 2 == 0 ? 1f : -1f) * time * (26f + i * 2f);
                gate.Root.SetPositionAndRotation(
                    new Vector3(Mathf.Sin(i * 2.5f) * 1.25f, Mathf.Cos(i * 2.1f) * 0.85f, camera.z - depth),
                    Quaternion.Euler(Mathf.Sin(time * 1.1f + i) * 13f, Mathf.Cos(time * 0.86f + i) * 16f, i * 29f + turn));
                Color hue = Color.Lerp(cyan, violet, Mathf.PingPong(i * 0.38f + time * 0.24f, 1f));
                float visibility = 1f - Mathf.InverseLerp(53f, 101f, depth);
                gate.Face.SetColor("_Color", hue * (0.16f + bass * 0.10f) * visibility);
                gate.Edge.SetColor("_Color", Color.Lerp(hue, Color.white, 0.27f) * (0.73f + boost * 0.55f) * visibility);
            }
        }
    }
}

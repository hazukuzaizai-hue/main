using UnityEngine;

namespace TikTokLiveGame
{
    public sealed class StageFrontLeds : MonoBehaviour
    {
        private readonly Material[] segments = new Material[40];
        private readonly Material[] steps = new Material[3];
        private DjAudioReactive audio;
        private readonly Material[] guides = new Material[12];
        private Material ringMaterial;
        private Material stageMaterial;
        private LineRenderer djRing;

        private void Start()
        {
            audio = FindFirstObjectByType<DjAudioReactive>();
            GameObject deck = GameObject.Find("DJ Stage Deck");
            if (deck != null)
            {
                stageMaterial = deck.GetComponent<Renderer>().material;
                stageMaterial.SetColor("_Color", new Color(0.035f, 0.027f, 0.065f));
                stageMaterial.SetFloat("_Metallic", 0.65f);
                stageMaterial.SetFloat("_Glossiness", 0.88f);
                stageMaterial.SetColor("_EmissionColor", new Color(0.012f, 0.008f, 0.025f));
            }
            for (int i = 0; i < guides.Length; i++)
            {
                guides[i] = GameMaterials.Club(Color.black, Color.black);
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 from = new Vector3(side * Mathf.Lerp(5.9f, 3.65f, i / 12f), 0.765f, Mathf.Lerp(-5.5f, -8.65f, i / 12f));
                    Vector3 to = new Vector3(side * Mathf.Lerp(5.9f, 3.65f, (i + 0.75f) / 12f), 0.765f, Mathf.Lerp(-5.5f, -8.65f, (i + 0.75f) / 12f));
                    GameObject strip = new GameObject("Stage Guide LED"); strip.transform.SetParent(transform, false);
                    LineRenderer line = strip.AddComponent<LineRenderer>(); line.useWorldSpace = false; line.positionCount = 2;
                    line.SetPosition(0, from); line.SetPosition(1, to); line.startWidth = line.endWidth = 0.035f; line.sharedMaterial = guides[i];
                }
            }
            ringMaterial = GameMaterials.Club(Color.black, Color.black);
            GameObject ring = new GameObject("DJ Floor Halo"); ring.transform.SetParent(transform, false);
            djRing = ring.AddComponent<LineRenderer>(); djRing.useWorldSpace = false; djRing.loop = true; djRing.positionCount = 96;
            djRing.startWidth = djRing.endWidth = 0.045f; djRing.sharedMaterial = ringMaterial;
            for (int i = 0; i < 96; i++)
            {
                float angle = i * Mathf.PI * 2f / 96f;
                djRing.SetPosition(i, new Vector3(Mathf.Cos(angle) * 2f, 0.775f, -7.65f + Mathf.Sin(angle) * 1.2f));
            }
            for (int i = 0; i < segments.Length; i++)
            {
                segments[i] = GameMaterials.Club(Color.black, Color.black);
                float x = Mathf.Lerp(-6.2f, 6.2f, i / 39f);
                Box("Stage Fascia Audio Pixel", new Vector3(x, 0.42f, -5.075f), new Vector3(0.25f, 0.095f, 0.028f), segments[i]);
            }
            float[] widths = { 2.68f, 3.22f, 3.78f };
            float[] heights = { 0.76f, 0.54f, 0.335f };
            float[] depths = { -4.67f, -4.28f, -3.91f };
            for (int i = 0; i < steps.Length; i++)
            {
                steps[i] = GameMaterials.Club(Color.black, Color.black);
                Box("Step Neon Front " + i, new Vector3(0f, heights[i], depths[i]), new Vector3(widths[i], 0.026f, 0.025f), steps[i]);
                for (int side = -1; side <= 1; side += 2)
                    Box("Step Neon Return", new Vector3(side * widths[i] * 0.5f, heights[i], depths[i] - 0.19f), new Vector3(0.025f, 0.026f, 0.4f), steps[i]);
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
            float bass = audio != null ? audio.Bass : 0f;
            for (int i = 0; i < guides.Length; i++)
            {
                if (guides[i] == null) continue;
                float band = audio != null ? audio.Band(i) : 0f;
                Color color = Color.Lerp(new Color(0.03f, 0.75f, 1f), new Color(0.62f, 0.12f, 1f), i / 11f);
                guides[i].SetColor("_EmissionColor", color * (0.18f + band * 0.7f));
            }
            if (ringMaterial != null) ringMaterial.SetColor("_EmissionColor", Color.Lerp(new Color(0.08f, 0.7f, 1f), new Color(0.65f, 0.15f, 1f), bass) * (0.35f + bass * 0.65f));
            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i] == null) continue;
                float distance = Mathf.Abs(i - 19.5f) / 19.5f;
                float front = Mathf.Clamp01(bass * 1.35f);
                float intensity = 0.10f + Mathf.Clamp01((front - distance) * 6f) * 0.85f;
                Color color = Color.Lerp(new Color(0.03f, 0.78f, 1f), new Color(0.70f, 0.12f, 1f), distance);
                segments[i].SetColor("_EmissionColor", color * intensity);
            }
            for (int i = 0; i < steps.Length; i++)
            {
                if (steps[i] == null) continue;
                Color color = Color.Lerp(new Color(0.03f, 0.78f, 1f), new Color(0.65f, 0.12f, 1f), i / 2f);
                steps[i].SetColor("_EmissionColor", color * (0.45f + bass * 0.4f));
            }
        }

        private void OnDestroy()
        {
            foreach (Material material in guides) if (material != null) Destroy(material);
            if (ringMaterial != null) Destroy(ringMaterial);
            if (stageMaterial != null) Destroy(stageMaterial);
            foreach (Material material in segments) if (material != null) Destroy(material);
            foreach (Material material in steps) if (material != null) Destroy(material);
        }
    }
}

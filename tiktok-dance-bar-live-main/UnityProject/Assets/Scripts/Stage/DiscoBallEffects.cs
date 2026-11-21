using UnityEngine;

namespace TikTokLiveGame
{
    public sealed class DiscoBallEffects : MonoBehaviour
    {
        private readonly Material[] facets = new Material[12];
        private readonly Color cyan = new Color(0.12f, 0.65f, 0.85f);
        private readonly Color violet = new Color(0.50f, 0.22f, 0.78f);

        private void Start()
        {
            for (int i = 0; i < facets.Length; i++)
                facets[i] = GameMaterials.Lit(new Color(0.2f, 0.24f, 0.32f), 0.88f, 0.94f, Color.black);
            int index = 0;
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
            {
                if (!renderer.name.StartsWith("Mirror Tile")) continue;
                renderer.sharedMaterial = facets[index++ % facets.Length];
                renderer.transform.localScale = new Vector3(0.085f, 0.085f, 0.018f);
            }
        }

        private void Update()
        {
            for (int i = 0; i < facets.Length; i++)
            {
                if (facets[i] == null) continue;
                float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * 0.75f + i * 0.65f);
                Color tint = Color.Lerp(cyan, violet, wave);
                facets[i].SetColor("_EmissionColor", tint * (0.13f + 0.24f * wave));
            }
        }

        private void OnDestroy()
        {
            foreach (Material material in facets) if (material != null) Destroy(material);
        }
    }
}

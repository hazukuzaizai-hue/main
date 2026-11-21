using System.Collections.Generic;
using UnityEngine;

namespace TikTokLiveGame
{
    public sealed class DjAudioReactive : MonoBehaviour
    {
        private readonly float[] spectrum = new float[512];
        private readonly float[] samples = new float[256];
        private readonly Transform[] bars = new Transform[17];
        private readonly Material[] barMaterials = new Material[17];
        private readonly float[] levels = new float[17];
        private readonly float[] peaks = new float[17];
        private readonly List<Transform> cones = new List<Transform>();
        private readonly List<Vector3> conePositions = new List<Vector3>();
        private readonly List<LineRenderer> rings = new List<LineRenderer>();
        private readonly List<LineRenderer> halos = new List<LineRenderer>();
        private readonly List<Material> speakerLeds = new List<Material>();
        private readonly List<int> speakerSegments = new List<int>();
        private readonly List<Transform> caps = new List<Transform>();
        private readonly List<Vector3> capPositions = new List<Vector3>();
        private readonly List<(Transform Cone, Vector3 RestPosition)> recessedCones = new();
        private readonly List<(Material Ring, Color Emission)> recessedRings = new();
        private AudioSource music;
        private Transform performer;
        private Vector3 performerPosition;
        private Quaternion performerRotation;
        private Material ringMaterial;
        private float bass;
        private readonly List<Material> djPads = new();
        private bool reportedAudio;
        public float Bass => bass;
        public float Band(int index) => levels[Mathf.Clamp(index, 0, levels.Length - 1)];

        private void Start()
        {
            MusicPlaylistPlayer player = GetComponent<MusicPlaylistPlayer>();
            if (player != null) music = player.Output;
            for (int i = 0; i < bars.Length; i++)
            {
                GameObject bar = GameObject.Find("Booth Equalizer " + i);
                if (bar == null) continue;
                bars[i] = bar.transform;
                barMaterials[i] = bar.GetComponent<Renderer>().material;
                peaks[i] = 0.005f;
            }
            GameObject dj = GameObject.Find("Main Character GIF");
            if (dj != null) { performer = dj.transform; performerPosition = performer.localPosition; performerRotation = performer.localRotation; }
            ringMaterial = GameMaterials.BackgroundSprite();
            foreach (Renderer renderer in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (renderer.name.StartsWith("DJ Performance Pad ")) djPads.Add(renderer.material);
                if (renderer.name.StartsWith("Speaker LED "))
                {
                    speakerLeds.Add(renderer.material);
                    int.TryParse(renderer.name.Substring(renderer.name.LastIndexOf('-') + 1), out int segmentIndex);
                    speakerSegments.Add(segmentIndex);
                }
                if (renderer.name == "Recessed Speaker Cone")
                    recessedCones.Add((renderer.transform, renderer.transform.localPosition));
                if (renderer.name.StartsWith("Recessed Speaker Ring "))
                {
                    Material recessedRing = renderer.material;
                    recessedRings.Add((recessedRing, recessedRing.GetColor("_EmissionColor")));
                }
                if (renderer.name == "Speaker Dust Cap") { caps.Add(renderer.transform); capPositions.Add(renderer.transform.localPosition); }
                if (renderer.name != "Speaker Cone") continue;
                cones.Add(renderer.transform); conePositions.Add(renderer.transform.localPosition);
                GameObject ringObject = new GameObject("Speaker Audio Halo");
                ringObject.transform.SetParent(renderer.transform.parent, false);
                ringObject.transform.localPosition = renderer.transform.localPosition + new Vector3(0f, 0f, 0.10f);
                LineRenderer ring = ringObject.AddComponent<LineRenderer>();
                ring.useWorldSpace = false; ring.loop = true; ring.positionCount = 64;
                ring.sharedMaterial = ringMaterial; ring.startWidth = ring.endWidth = 0.058f;
                for (int point = 0; point < 64; point++)
                {
                    float angle = point * Mathf.PI * 2f / 64f;
                    ring.SetPosition(point, new Vector3(Mathf.Cos(angle) * 0.57f, Mathf.Sin(angle) * 0.57f, 0f));
                }
                rings.Add(ring);
                GameObject haloObject = new GameObject("Speaker Bass Glow");
                haloObject.transform.SetParent(ringObject.transform, false);
                LineRenderer halo = haloObject.AddComponent<LineRenderer>();
                halo.useWorldSpace = false; halo.loop = true; halo.positionCount = 64; halo.sharedMaterial = ringMaterial;
                halo.startWidth = halo.endWidth = 0.15f;
                for (int point = 0; point < 64; point++) halo.SetPosition(point, ring.GetPosition(point));
                halos.Add(halo);
            }
        }

        private void Update()
        {
            bool playing = music != null && music.isPlaying && music.volume > 0.001f;
            float energy = 0f;
            if (playing)
            {
                music.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);
                music.GetOutputData(samples, 0);
                foreach (float sample in samples) energy += sample * sample;
                energy = Mathf.Sqrt(energy / samples.Length);
                if (!reportedAudio && energy > 0.0001f) { reportedAudio = true; Debug.Log("DJ audio reactive: live music samples detected, RMS=" + energy); }
            }
            for (int pad = 0; pad < djPads.Count; pad++)
            {
                Color led = Color.HSVToRGB(Mathf.Repeat(pad * 0.12f + Time.time * 0.08f, 1f), 0.85f, 1f);
                djPads[pad].SetColor("_EmissionColor", led * (0.7f + bass * 1.8f + Mathf.Max(0f, Mathf.Sin(Time.time * 5f - pad)) * 0.8f));
            }
            float targetBass = 0f;
            for (int i = 0; i < bars.Length; i++)
            {
                int first = Mathf.Clamp(Mathf.FloorToInt(Mathf.Pow(300f, i / 17f)), 1, 510);
                int last = Mathf.Clamp(Mathf.CeilToInt(Mathf.Pow(300f, (i + 1f) / 17f)), first + 1, 512);
                float sum = 0f;
                if (playing) for (int bin = first; bin < last; bin++) sum += spectrum[bin];
                float amplitude = sum / (last - first);
                peaks[i] = Mathf.Max(amplitude, peaks[i] * Mathf.Exp(-Time.deltaTime * 0.45f), 0.00008f);
                float target = playing && energy > 0.00005f ? Mathf.Clamp01(amplitude / peaks[i]) : 0f;
                levels[i] = Mathf.Lerp(levels[i], target, 1f - Mathf.Exp(-Time.deltaTime * (target > levels[i] ? 22f : 7f)));
                if (i < 5) targetBass += levels[i] / 5f;
                if (bars[i] == null) continue;
                float height = 0.045f + levels[i] * 0.65f;
                Vector3 scale = bars[i].localScale; scale.y = height; bars[i].localScale = scale;
                Vector3 position = bars[i].localPosition; position.y = 1.67f + height * 0.5f; bars[i].localPosition = position;
                Color color = Color.HSVToRGB(Mathf.Repeat(0.46f + i * 0.031f + Time.time * 0.025f, 1f), 0.85f, 1f);
                barMaterials[i].SetColor("_EmissionColor", color * (0.15f + levels[i] * 1.2f));
            }
            bass = Mathf.Lerp(bass, targetBass, 1f - Mathf.Exp(-Time.deltaTime * (targetBass > bass ? 32f : 10f)));
            foreach (var driver in recessedCones)
            {
                if (driver.Cone == null) continue;
                Vector3 inward = -Mathf.Sign(driver.RestPosition.x) * Vector3.right;
                driver.Cone.localPosition = driver.RestPosition + inward * (bass * 0.18f);
                driver.Cone.localScale = new Vector3(0.095f + bass * 0.12f, 0.59f, 0.59f);
            }
            foreach (var ring in recessedRings)
                ring.Ring.SetColor("_EmissionColor", ring.Emission * (0.65f + bass * 2.1f));

            for (int i = 0; i < cones.Count; i++)
            {
                cones[i].localPosition = conePositions[i] + Vector3.forward * bass * 0.12f;
                Color glow = Color.HSVToRGB(Mathf.Repeat(0.48f + Time.time * 0.035f + i * 0.1f, 1f), 0.8f, 0.62f + bass * 0.38f);
                rings[i].startColor = rings[i].endColor = glow;
                rings[i].startWidth = rings[i].endWidth = 0.055f + bass * 0.055f;
                rings[i].transform.localScale = Vector3.one * (1f + bass * 0.025f);
                glow.a = 0.16f + bass * 0.30f;
                halos[i].startColor = halos[i].endColor = glow;
            }
            for (int i = 0; i < caps.Count; i++) caps[i].localPosition = capPositions[i] + Vector3.forward * bass * 0.12f;
            for (int i = 0; i < speakerLeds.Count; i++)
            {
                int segment = speakerSegments[i];
                float lit = segment / 16f < bass ? 1.35f : 0.20f;
                Color color = Color.HSVToRGB(Mathf.Repeat(0.5f + segment * 0.018f + Time.time * 0.035f, 1f), 0.8f, 1f);
                speakerLeds[i].SetColor("_EmissionColor", color * lit);
            }
            if (performer != null)
            {
                performer.localPosition = performerPosition + Vector3.up * bass * 0.10f;
                performer.localRotation = performerRotation * Quaternion.Euler(0f, 0f, playing ? Mathf.Sin(Time.time * 3f) * bass * 1.6f : 0f);
            }
        }

        private void OnDestroy()
        {
            foreach (Material material in barMaterials) if (material != null) Destroy(material);
            foreach (Material material in speakerLeds) if (material != null) Destroy(material);
            if (ringMaterial != null) Destroy(ringMaterial);
        }
    }
}

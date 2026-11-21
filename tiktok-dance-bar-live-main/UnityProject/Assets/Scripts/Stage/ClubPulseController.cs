using System.Collections.Generic;
using UnityEngine;

namespace TikTokLiveGame
{
    [DefaultExecutionOrder(50)]
    public sealed class ClubPulseController : MonoBehaviour
    {
        [SerializeField] private float bpm = 128f;
        private readonly List<LightState> lights = new();
        private readonly List<MaterialState> materials = new();
        private Transform discoBall;
        private DjAudioReactive audioReactive;
        private float bassAverage;
        private bool bassAboveThreshold;
        private int demoPhrase = -1;
        private int demoKickBeat = -1;
        private float dropStarted = float.NegativeInfinity;
        private float kickStarted = float.NegativeInfinity;
        private float lastAudibleBass = float.NegativeInfinity;

        public static ClubPulseController Active { get; private set; }
        public float DropAge => Time.time - dropStarted;
        public float DropMix => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(DropAge / 0.18f))
            * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.35f, 1.85f, DropAge)));
        public float Kick => 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.time - kickStarted) / 0.19f));

        private void OnEnable() => Active = this;

        private void OnDisable()
        {
            if (Active == this) Active = null;
        }

        private void Start()
        {
            bpm = ClubBeatClock.Bpm;
            foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.name.StartsWith("Moving Head") || light.name.StartsWith("Strobe Light") || light.name == "Spark Warm Flash") continue;
                lights.Add(new LightState(light, light.intensity));
            }
            foreach (Renderer renderer in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!renderer.name.StartsWith("LED") && !renderer.name.StartsWith("Floor Strip")) continue;
                Material material = renderer.material;
                if (!material.HasProperty("_EmissionColor")) continue;
                bool sideAccent = renderer.name.StartsWith("LED Club") || renderer.name.StartsWith("LED Wall Chevron")
                    || renderer.name.StartsWith("LED Mezzanine");
                bool ceiling = renderer.name.StartsWith("LED Ceiling Chase");
                float z = renderer.transform.position.z;
                float phase = sideAccent || ceiling ? z * 0.48f : materials.Count * 0.47f;
                materials.Add(new MaterialState(material, material.GetColor("_EmissionColor"), phase, sideAccent,
                    ceiling ? z + 8.5f : -1f));
            }
            GameObject disco = GameObject.Find("Disco Ball");
            if (disco != null) discoBall = disco.transform;
        }

        private void Update()
        {
            float beat = Time.time * bpm / 60f;
            UpdateDrop(beat);
            float phase = beat - Mathf.Floor(beat);
            float pulse = Mathf.Exp(-phase * 7f);
            foreach (LightState state in lights)
                if (state.Light != null) state.Light.intensity = state.BaseIntensity * (0.72f + pulse * 0.55f);
            float dropAccent = DropMix * Kick;
            foreach (MaterialState state in materials)
            {
                float chase = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(beat * Mathf.PI * 2f - state.Phase)), 8f);
                float wave = 0.42f + Mathf.Sin(Time.time * 1.7f + state.Phase) * 0.15f + pulse * 0.28f + chase * 1.1f;
                if (state.SideAccent) wave = 0.58f + pulse * 0.34f + chase * 1.05f + dropAccent * 1.35f;
                if (state.CeilingDepth >= 0f)
                {
                    float distance = Mathf.Abs(state.CeilingDepth - DropAge * 16f);
                    float crest = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(distance / 3.1f));
                    wave = 0.40f + pulse * 0.22f + chase * 0.83f + crest * DropMix * 1.65f;
                }
                state.Material.SetColor("_EmissionColor", state.BaseEmission * wave);
            }
            if (discoBall != null) discoBall.Rotate(0f, 38f * Time.deltaTime, 0f, Space.World);
        }

        private void UpdateDrop(float beat)
        {
            if (audioReactive == null) audioReactive = FindFirstObjectByType<DjAudioReactive>();
            float bass = audioReactive != null ? audioReactive.Bass : 0f;
            if (bass > 0.04f) lastAudibleBass = Time.time;
            bassAverage = Mathf.Lerp(bassAverage, bass, 1f - Mathf.Exp(-Time.deltaTime * 1.4f));
            float threshold = Mathf.Max(0.34f, bassAverage + 0.11f);
            bool bassHit = bass > threshold && !bassAboveThreshold;
            bassAboveThreshold = bass > (bassAboveThreshold ? threshold - 0.045f : threshold);

            // Keep the show visible in a silent preview on the same clock as the floor.
            int phrase = Mathf.FloorToInt((beat - 8f) / 32f);
            bool silent = Time.time - lastAudibleBass > 2f;
            bool demoDrop = silent && phrase >= 0 && phrase != demoPhrase && Mathf.Repeat(beat - 8f, 32f) < 1f;
            if (demoDrop || (!silent && bassHit && DropAge > 9f))
            {
                dropStarted = Time.time;
                kickStarted = Time.time;
            }
            if (demoDrop) demoPhrase = phrase;
            if (DropAge >= 1.85f) return;
            int wholeBeat = Mathf.FloorToInt(beat);
            if ((!silent && bassHit) || (silent && wholeBeat != demoKickBeat))
                kickStarted = Time.time;
            demoKickBeat = wholeBeat;
        }

        private readonly struct LightState
        {
            public readonly Light Light;
            public readonly float BaseIntensity;
            public LightState(Light light, float intensity) { Light = light; BaseIntensity = intensity; }
        }

        private readonly struct MaterialState
        {
            public readonly Material Material;
            public readonly Color BaseEmission;
            public readonly float Phase;
            public readonly bool SideAccent;
            public readonly float CeilingDepth;
            public MaterialState(Material material, Color emission, float phase, bool sideAccent, float ceilingDepth)
            {
                Material = material;
                BaseEmission = emission;
                Phase = phase;
                SideAccent = sideAccent;
                CeilingDepth = ceilingDepth;
            }
        }
    }
}

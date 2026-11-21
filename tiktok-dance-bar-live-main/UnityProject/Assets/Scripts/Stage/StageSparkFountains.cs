using UnityEngine;

namespace TikTokLiveGame
{
    public sealed class StageSparkFountains : MonoBehaviour
    {
        private readonly ParticleSystem[] fountains = new ParticleSystem[4];
        private readonly Light[] lights = new Light[4];
        private Material sparks;
        private Material housing;
        private Material ribbonMaterial;
        private Mesh ribbonMesh;
        private readonly ParticleSystem[] ribbonCannons = new ParticleSystem[2];
        private bool ribbonFired;
        private readonly bool[] emitting = new bool[4];

        private void Start()
        {
            Shader shader = Resources.Load<Shader>("Shaders/SimpleParticle");
            sparks = shader != null ? new Material(shader) : GameMaterials.BackgroundSprite();
            sparks.renderQueue = 3100;
            housing = GameMaterials.Lit(new Color(0.035f, 0.04f, 0.055f), 0.75f, 0.55f, new Color(0.008f, 0.012f, 0.02f));
            for (int i = 0; i < fountains.Length; i++)
            {
                float x = i == 0 ? -5.7f : i == 1 ? 5.7f : i == 2 ? -3.4f : 3.4f;
                Vector3 position = new Vector3(x, 0.82f, -5.4f);
                GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.name = "Spark Fountain Machine"; box.transform.SetParent(transform, false);
                box.transform.localPosition = position; box.transform.localScale = new Vector3(0.36f, 0.38f, 0.42f);
                box.GetComponent<Renderer>().sharedMaterial = housing; Destroy(box.GetComponent<Collider>());
                GameObject emitter = new GameObject("Stage Spark Jet"); emitter.transform.SetParent(transform, false);
                emitter.transform.localPosition = position + Vector3.up * 0.22f;
                emitter.transform.localRotation = Quaternion.LookRotation(Vector3.up);
                ParticleSystem ps = emitter.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main; main.playOnAwake = false; main.loop = true; main.startLifetime = new ParticleSystem.MinMaxCurve(0.65f, 1.4f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(8f, 13f); main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.065f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.68f, 0.20f), new Color(1f, 0.97f, 0.8f));
                main.gravityModifier = 0.7f; main.maxParticles = 2200; main.simulationSpace = ParticleSystemSimulationSpace.World;
                var emission = ps.emission; emission.rateOverTime = 1100f;
                var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 7f; shape.radius = 0.035f;
                var color = ps.colorOverLifetime; color.enabled = true;
                Gradient gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.38f, 0.05f), 1f) }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.55f), new GradientAlphaKey(0f, 1f) }); color.color = gradient;
                var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.15f));
                ParticleSystemRenderer renderer = emitter.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = sparks;
                renderer.renderMode = ParticleSystemRenderMode.Stretch; renderer.velocityScale = 0.055f; renderer.lengthScale = 3.2f;
                fountains[i] = ps;
                GameObject lamp = new GameObject("Spark Warm Flash"); lamp.transform.SetParent(transform, false); lamp.transform.localPosition = position + Vector3.up;
                lights[i] = lamp.AddComponent<Light>(); lights[i].color = new Color(1f, 0.65f, 0.25f); lights[i].range = 5f; lights[i].intensity = 0f;
            }
            CreateRibbonCannons();
        }

        private void CreateRibbonCannons()
        {
            ribbonMaterial = GameMaterials.BackgroundSprite();
            ribbonMaterial.mainTexture = Texture2D.whiteTexture;
            ribbonMaterial.renderQueue = 3105;
            ribbonMesh = new Mesh { name = "Curved Paper Ribbon" };
            ribbonMesh.vertices = new[]
            {
                new Vector3(-0.15f, -0.70f, 0f), new Vector3(0.15f, -0.70f, 0f),
                new Vector3(-0.04f, -0.22f, 0.12f), new Vector3(0.26f, -0.22f, 0.12f),
                new Vector3(-0.24f, 0.24f, -0.10f), new Vector3(0.06f, 0.24f, -0.10f),
                new Vector3(-0.14f, 0.70f, 0f), new Vector3(0.16f, 0.70f, 0f)
            };
            ribbonMesh.triangles = new[] { 0, 2, 1, 1, 2, 3, 2, 4, 3, 3, 4, 5, 4, 6, 5, 5, 6, 7 };
            ribbonMesh.uv = new[]
            {
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0.33f), new Vector2(1, 0.33f),
                new Vector2(0, 0.66f), new Vector2(1, 0.66f), new Vector2(0, 1), new Vector2(1, 1)
            };
            ribbonMesh.RecalculateBounds();

            for (int i = 0; i < ribbonCannons.Length; i++)
            {
                float side = i == 0 ? -1f : 1f;
                Vector3 origin = new(side * 4.35f, 0.95f, -5.55f);
                Vector3 direction = new(-side * 0.41f, 0.92f, 0.48f);
                GameObject machine = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                machine.name = "Ribbon Cannon";
                machine.transform.SetParent(transform, false);
                machine.transform.localPosition = origin;
                machine.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
                machine.transform.localScale = new Vector3(0.25f, 0.28f, 0.25f);
                machine.GetComponent<Renderer>().sharedMaterial = housing;
                Destroy(machine.GetComponent<Collider>());

                GameObject nozzle = new("Ribbon Cannon Nozzle");
                nozzle.transform.SetParent(transform, false);
                nozzle.transform.localPosition = origin + direction.normalized * 0.27f;
                nozzle.transform.localRotation = Quaternion.LookRotation(direction);
                ParticleSystem particles = nozzle.AddComponent<ParticleSystem>();
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = particles.main;
                main.playOnAwake = false;
                main.loop = false;
                main.duration = 3.8f;
                main.startLifetime = new ParticleSystem.MinMaxCurve(2.15f, 3.55f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(6.5f, 11.2f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.29f);
                main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
                main.gravityModifier = 0.42f;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 180;
                var emission = particles.emission;
                emission.enabled = false;
                var shape = particles.shape;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 20f;
                var rotation = particles.rotationOverLifetime;
                rotation.enabled = true;
                rotation.separateAxes = true;
                rotation.x = new ParticleSystem.MinMaxCurve(-3.1f, 3.1f);
                rotation.y = new ParticleSystem.MinMaxCurve(-4.2f, 4.2f);
                rotation.z = new ParticleSystem.MinMaxCurve(-3.8f, 3.8f);
                var renderer = nozzle.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Mesh;
                renderer.mesh = ribbonMesh;
                renderer.sharedMaterial = ribbonMaterial;
                ribbonCannons[i] = particles;
            }
        }

        private void FireRibbons()
        {
            foreach (ParticleSystem cannon in ribbonCannons)
            {
                cannon.Play();
                for (int color = 0; color < 7; color++)
                {
                    var ribbon = new ParticleSystem.EmitParams
                    {
                        startColor = Color.HSVToRGB(color / 7f, 0.73f, 1f)
                    };
                    cannon.Emit(ribbon, 12);
                }
            }
            Debug.Log("Stage ribbon cannons: synchronized burst");
        }

        private void Update()
        {
            ClubPulseController show = ClubPulseController.Active;
            bool dropBurst = show != null && show.DropAge < 0.56f;
            if (dropBurst && !ribbonFired) FireRibbons();
            ribbonFired = dropBurst;
            for (int i = 0; i < fountains.Length; i++)
            {
                bool fire = dropBurst && i < 2;
                if (fire != emitting[i])
                {
                    if (fire) { fountains[i].Play(); fountains[i].Emit(100); }
                    else fountains[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    emitting[i] = fire;
                    if (fire) Debug.Log("Stage spark fountain " + i + ": vertical burst");
                }
                if (lights[i] != null) lights[i].intensity = fire ? 2f + Mathf.PerlinNoise(Time.time * 19f, i) * 1.5f : 0f;
            }
        }

        private void OnDestroy() { if (sparks != null) Destroy(sparks); if (housing != null) Destroy(housing); Destroy(ribbonMaterial); Destroy(ribbonMesh); }
    }
}

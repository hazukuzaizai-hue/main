using UnityEngine;

namespace TikTokLiveGame
{
    public static class ClubSceneBuilder
    {
        private const int FloorLightingLayer = 8;
        private const float DjBoothHeightOffset = 0.85f;

        public static void Build()
        {
            RenderSettings.ambientLight = new Color(0.18f, 0.14f, 0.27f);
            RenderSettings.fog = false;
            RenderSettings.fogColor = new Color(0.04f, 0.005f, 0.01f);
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.018f;

            Camera camera = Camera.main;
            if (camera == null)
            {
                GameObject cameraObject = new("Main Camera");
                cameraObject.tag = "MainCamera";
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
            }
            camera.transform.position = new Vector3(0f, 7.2f, 14.2f);
            camera.transform.LookAt(new Vector3(0f, 1.25f, -1.2f));
            camera.fieldOfView = 45f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.01f, 0.005f, 0.025f);
            if (camera.GetComponent<ClubCameraController>() == null) camera.gameObject.AddComponent<ClubCameraController>();

            GameObject[] existingObjects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            CreateArchitecturalBackdrop();
            CreateSideArchitecture();
            CreateMezzanines();
            CreateSideFillSpeakers();
            CreateCeilingLighting();
            GameObject architecture = new GameObject("StageArchitecture");
            foreach (GameObject item in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                if (item != architecture && item.transform.parent == null && System.Array.IndexOf(existingObjects, item) < 0)
                    item.transform.SetParent(architecture.transform);

            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            string path = System.IO.Path.Combine(GamePaths.Backgrounds, "default.png");
            if (System.IO.File.Exists(path))
            {
                tex.LoadImage(System.IO.File.ReadAllBytes(path));
                tex.filterMode = FilterMode.Trilinear;
                tex.anisoLevel = 8;
                tex.wrapMode = TextureWrapMode.Mirror;
                tex.Apply();
            }
            else
            {
                Debug.LogWarning("Không tìm thấy ảnh map: " + path);
                Color backdropColor = new Color(0.025f, 0.012f, 0.06f);
                tex.SetPixels(new[] { backdropColor, backdropColor, backdropColor, backdropColor });
                tex.Apply();
            }
            Shader bgShader = Shader.Find("Unlit/Texture");
            if (bgShader == null) bgShader = Shader.Find("Sprites/Default");
            Material bgMat = new Material(bgShader);
            bgMat.mainTexture = tex;

            float texAspect = (float)tex.width / tex.height;
            float H = 40f;
            float W = H * texAspect;
            float D = W * 3f; // Depth for side walls

            Material centerMat = new Material(bgMat);
            centerMat.mainTextureScale = new Vector2(-1, 1);

            Material rightMat = new Material(bgMat);
            rightMat.mainTextureScale = new Vector2(-3, 1);
            rightMat.mainTextureOffset = new Vector2(-1, 0);

            Material leftMat = new Material(bgMat);
            leftMat.mainTextureScale = new Vector2(3, 1);
            leftMat.mainTextureOffset = new Vector2(-3, 0);

            Material ceilingMat = new Material(bgMat);
            ceilingMat.mainTextureScale = new Vector2(-1, -1); // Lật ngược để làm trần nhà phản chiếu
            ceilingMat.mainTextureOffset = new Vector2(0, 1);

            GameObject bgRoot = new GameObject("AmPhuBackdrop_Room");

            GameObject centerQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            centerQuad.name = "AmPhuBackdrop_Center";
            centerQuad.transform.SetParent(bgRoot.transform);
            centerQuad.transform.localPosition = new Vector3(0, -3.0f, -12.5f);
            centerQuad.transform.localScale = new Vector3(W, H, 1);
            centerQuad.transform.localRotation = Quaternion.Euler(0, 180, 0);
            centerQuad.GetComponent<Renderer>().sharedMaterial = centerMat;

            GameObject leftQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            leftQuad.name = "AmPhuBackdrop_Left";
            leftQuad.transform.SetParent(bgRoot.transform);
            leftQuad.transform.localPosition = new Vector3(W / 2f, -3.0f, -12.5f + D / 2f);
            leftQuad.transform.localScale = new Vector3(D, H, 1);
            leftQuad.transform.localRotation = Quaternion.Euler(0, -90, 0);
            leftQuad.GetComponent<Renderer>().sharedMaterial = leftMat;

            GameObject rightQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            rightQuad.name = "AmPhuBackdrop_Right";
            rightQuad.transform.SetParent(bgRoot.transform);
            rightQuad.transform.localPosition = new Vector3(-W / 2f, -3.0f, -12.5f + D / 2f);
            rightQuad.transform.localScale = new Vector3(D, H, 1);
            rightQuad.transform.localRotation = Quaternion.Euler(0, 90, 0);
            rightQuad.GetComponent<Renderer>().sharedMaterial = rightMat;

            // Trần nhà phản chiếu (Mirrored Ceiling) để chống hở mảng đen khi camera ngước lên quá cao
            GameObject ceilingQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ceilingQuad.name = "AmPhuBackdrop_Ceiling";
            ceilingQuad.transform.SetParent(bgRoot.transform);
            ceilingQuad.transform.localPosition = new Vector3(0, -3.0f + H / 2f, -12.5f + D / 2f);
            ceilingQuad.transform.localScale = new Vector3(W, D, 1);
            ceilingQuad.transform.localRotation = Quaternion.Euler(90, 180, 0); // Xoay 180 để mặt Quad hướng xuống dưới đất
            ceilingQuad.GetComponent<Renderer>().sharedMaterial = ceilingMat;

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Invisible Dance Floor";
            floor.transform.position = new Vector3(0f, -0.3f, -1f);
            floor.transform.localScale = new Vector3(100f, 0.5f, 100f);
            floor.GetComponent<Renderer>().sharedMaterial = GameMaterials.Lit(
                new Color(0.035f, 0.028f, 0.055f), 0.55f, 0.72f,
                new Color(0.008f, 0.005f, 0.015f), 0.06f, 12f);
            floor.GetComponent<Renderer>().enabled = false;

            GameObject neonFloor = new GameObject("NeonDanceFloor");
            neonFloor.transform.SetParent(architecture.transform, false);
            neonFloor.AddComponent<NeonDanceFloor>();
            CreateRaisedStage();
            GameObject podiums = new("Rank Podiums");
            podiums.transform.SetParent(architecture.transform, false);
            podiums.AddComponent<RankPodiums>().Build();
            new GameObject("Stage Front Lighting").AddComponent<StageFrontLeds>();
            new GameObject("Stage Spark Fountains").AddComponent<StageSparkFountains>();
            new GameObject("DJ LED Backdrop").AddComponent<DjLedBackdrop>();
            CreateDjBooth();
            CreateDjPerformer();
            CreateArchitecturalAccentLights();
            CreateCircularTrussRig();
            CreateLedBarRig();
            CreateStrobes();

            CreateSpeaker(-5.4f);
            CreateSpeaker(5.4f);

            CreateMirrorBall();

            // Group club elements and push them further back to create a huge dance space
            GameObject clubRoot = new GameObject("ClubRoot");
            foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (go.transform.parent == null && go.name != "Invisible Dance Floor" && !go.name.Contains("AmPhuBackdrop") && go.name != "Main Camera" && go != clubRoot)
                {
                    go.transform.SetParent(clubRoot.transform);
                }
            }
            clubRoot.transform.position = new Vector3(0, 0, -12f);

            new GameObject("Club Beat").AddComponent<ClubPulseController>();

            AddLight("Hell Red Light", new Vector3(-6f, 7f, 1f), new Color(0.08f, 0.55f, 1f), 4.8f, 20f);
            AddLight("Hell Orange Light", new Vector3(6f, 7f, 0f), new Color(1f, 0.12f, 0.65f), 4.6f, 20f);
            AddLight("Stage Demon Light", new Vector3(0f, 8f, -7f), new Color(0.55f, 0.18f, 1f), 5.8f, 23f);
        }

        private static void CreateRaisedStage()
        {
            Material deck = GameMaterials.Lit(new Color(0.04f, 0.01f, 0.015f), 0.22f, 0.32f, new Color(0.012f, 0.003f, 0.004f), 0.045f, 9f);
            Material fascia = GameMaterials.Lit(new Color(0.022f, 0.008f, 0.01f), 0.62f, 0.46f, Color.black);
            CreateLitBox("DJ Stage Deck", new Vector3(0f, 0.2f, -7.3f), new Vector3(13f, 1.1f, 4f), deck);
            CreateLitBox("DJ Stage Front", new Vector3(0f, 0.32f, -5.22f), new Vector3(13.2f, 0.86f, 0.22f), fascia);

            Color cyan = new(0.03f, 0.85f, 1f);
            Color violet = new(0.8f, 0.12f, 1f);
            CreateBox("Stage Fascia Top Strip", new Vector3(0f, 0.7f, -5.085f), new Vector3(12.7f, 0.035f, 0.035f), cyan * 0.08f, cyan * 0.55f);
            CreateBox("Stage Fascia Bottom Strip", new Vector3(0f, -0.02f, -5.085f), new Vector3(12.7f, 0.025f, 0.035f), violet * 0.06f, violet * 0.35f);
            for (int index = 0; index < 18; index++)
            {
                float x = Mathf.Lerp(-6.05f, 6.05f, index / 17f);
                Color glow = index % 2 == 0 ? cyan : violet;
                CreateGlowBulb($"Stage Fascia Pixel {index}", new Vector3(x, 0.34f, -5.075f), 0.035f, glow * 0.72f);
            }

            CreateLitBox("Stage Step High", new Vector3(0f, 0.57f, -4.92f), new Vector3(2.8f, 0.36f, 0.48f), deck);
            CreateLitBox("Stage Step Middle", new Vector3(0f, 0.38f, -4.52f), new Vector3(3.35f, 0.3f, 0.46f), deck);
            CreateLitBox("Stage Step Low", new Vector3(0f, 0.2f, -4.14f), new Vector3(3.9f, 0.25f, 0.44f), deck);
            CreateBox("Step Edge High", new Vector3(0f, 0.76f, -4.67f), new Vector3(2.65f, 0.025f, 0.025f), cyan * 0.08f, cyan * 0.42f);
            CreateBox("Step Edge Middle", new Vector3(0f, 0.54f, -4.28f), new Vector3(3.18f, 0.022f, 0.025f), violet * 0.07f, violet * 0.34f);
        }

        private static void CreateArchitecturalBackdrop()
        {
            Material wall = GameMaterials.Lit(new Color(0.012f, 0.015f, 0.022f), 0.15f, 0.2f, new Color(0.003f, 0.004f, 0.008f), 0.035f, 5f);
            Material metal = GameMaterials.Lit(new Color(0.055f, 0.06f, 0.075f), 0.78f, 0.5f, new Color(0.008f, 0.015f, 0.025f));
            CreateLitBox("Rear Acoustic Wall", new Vector3(0f, 4.65f, -10.35f), new Vector3(15.2f, 9.2f, 0.38f), wall);

            for (int side = -1; side <= 1; side += 2)
                for (int row = 0; row < 4; row++)
                {
                    float y = 2.05f + row * 1.55f;
                    CreateLitBox($"Acoustic Panel {side}-{row}", new Vector3(side * 5.55f, y, -10.08f), new Vector3(2.2f, 1.18f, 0.14f), GameMaterials.Lit(new Color(0.022f, 0.026f, 0.038f), 0.08f, 0.18f, Color.black, 0.045f, 10f));
                    CreateLitBox($"Acoustic Panel Trim {side}-{row}", new Vector3(side * 5.55f, y, -9.99f), new Vector3(2.28f, 1.26f, 0.025f), metal);
                    CreateLitBox($"Acoustic Panel Face {side}-{row}", new Vector3(side * 5.55f, y, -9.96f), new Vector3(2.12f, 1.1f, 0.025f), GameMaterials.Lit(new Color(0.018f, 0.021f, 0.032f), 0.04f, 0.12f, Color.black, 0.06f, 14f));
                }

            CreateLitBox("Backdrop Header Beam", new Vector3(0f, 8.35f, -9.95f), new Vector3(14.2f, 0.18f, 0.18f), metal);
            CreateLitBox("Backdrop Left Column", new Vector3(-7.05f, 4.6f, -9.96f), new Vector3(0.22f, 7.55f, 0.22f), metal);
            CreateLitBox("Backdrop Right Column", new Vector3(7.05f, 4.6f, -9.96f), new Vector3(0.22f, 7.55f, 0.22f), metal);
        }

        private static void CreateSideArchitecture()
        {
            Material wall = GameMaterials.Lit(new Color(0.047f, 0.052f, 0.079f), 0.12f, 0.23f,
                new Color(0.016f, 0.020f, 0.036f), 0.035f, 6f);
            Material panel = GameMaterials.Lit(new Color(0.027f, 0.033f, 0.054f), 0.18f, 0.28f,
                new Color(0.010f, 0.014f, 0.028f), 0.05f, 8f);
            Material trim = GameMaterials.Lit(new Color(0.065f, 0.071f, 0.098f), 0.45f, 0.4f,
                new Color(0.017f, 0.020f, 0.033f));
            Material walkway = GameMaterials.Lit(new Color(0.034f, 0.039f, 0.059f), 0.20f, 0.37f,
                new Color(0.012f, 0.015f, 0.024f));

            for (int side = -1; side <= 1; side += 2)
            {
                CreateLitBox($"Club Side Wall {side}", new Vector3(side * 7.68f, 4.42f, 8f),
                    new Vector3(0.4f, 9.3f, 37f), wall);
                CreateLitBox($"Club Side Walkway {side}", new Vector3(side * 6.99f, -0.18f, 8f),
                    new Vector3(1.08f, 0.26f, 37f), walkway);
                CreateLitBox($"Club Side Baseboard {side}", new Vector3(side * 7.43f, 0.13f, 8f),
                    new Vector3(0.10f, 0.36f, 37f), trim);

                Color accent = side < 0 ? new Color(0.11f, 0.62f, 1f) : new Color(0.72f, 0.21f, 1f);
                Material accentMaterial = GameMaterials.Club(accent * 0.06f, accent * 0.65f);
                Material circuit = GameMaterials.Club(accent * 0.18f, accent * 1.40f);
                Material gold = GameMaterials.Club(new Color(0.22f, 0.15f, 0.075f), new Color(0.26f, 0.15f, 0.055f));
                Material upperInset = GameMaterials.Lit(new Color(0.032f, 0.041f, 0.072f), 0.35f, 0.46f, accent * 0.09f);
                Material diffuser = GameMaterials.Club(accent * 0.14f, accent * 0.48f);
                Material upperLed = GameMaterials.Club(accent * 0.21f, accent * 0.93f);
                Material stepLight = GameMaterials.Club(accent * 0.15f, accent * 0.78f);
                for (int marker = 0; marker < 7; marker++)
                {
                    float z = -3.2f + marker * 2.65f;
                    Vector3 position = new(side * 7.31f, 0.39f, z);
                    CreateLitBox($"Walkway Recessed Trim {side}-{marker}", position,
                        new Vector3(0.14f, 0.25f, 0.68f), trim);
                    CreateLitBox($"LED Club Walkway Marker {side}-{marker}", position - side * Vector3.right * 0.079f,
                        new Vector3(0.025f, 0.075f, 0.48f), stepLight);

                    GameObject wash = new($"Walkway Marker Wash {side}-{marker}");
                    wash.transform.SetPositionAndRotation(position - side * Vector3.right * 0.12f,
                        Quaternion.LookRotation(new Vector3(-side * 0.66f, -1f, 0f), Vector3.forward));
                    Light light = wash.AddComponent<Light>();
                    light.type = LightType.Spot;
                    light.color = Color.Lerp(accent, Color.white, 0.28f);
                    light.range = 1.55f;
                    light.spotAngle = 70f;
                    light.innerSpotAngle = 28f;
                    light.intensity = 0.75f;
                    light.cullingMask = 1 << FloorLightingLayer;
                }
                CreateLitBox($"Club Wall Cornice {side}", new Vector3(side * 7.35f, 8.73f, 8f),
                    new Vector3(0.24f, 0.14f, 36.7f), trim);
                CreateLitBox($"Club Wall Upper Trim {side}", new Vector3(side * 7.33f, 5.35f, 8f),
                    new Vector3(0.09f, 0.07f, 36.7f), gold);
                CreateLitBox($"LED Club Wall Lower Rail {side}", new Vector3(side * 7.32f, 1.02f, 8f),
                    new Vector3(0.05f, 0.045f, 36.7f), circuit);
                for (int bay = 0; bay < 8; bay++)
                {
                    float z = -8.15f + bay * 4.45f;
                    CreateLitBox($"Club Upper Wall Frame {side}-{bay}", new Vector3(side * 7.41f, 7.08f, z),
                        new Vector3(0.16f, 2.65f, 3.91f), trim);
                    CreateLitBox($"Club Upper Wall Inset {side}-{bay}", new Vector3(side * 7.30f, 7.08f, z),
                        new Vector3(0.075f, 2.43f, 3.67f), upperInset);
                    CreateLitBox($"LED Club Upper Cove {side}-{bay}", new Vector3(side * 7.23f, 8.16f, z),
                        new Vector3(0.045f, 0.13f, 3.50f), diffuser);
                    for (int blade = 0; blade < 3; blade++)
                    {
                        float bladeZ = z + (blade - 1) * 0.79f;
                        float xFace = side * 7.23f;
                        Vector3 lower = new(xFace, 6.35f, bladeZ - 0.30f);
                        Vector3 upper = new(xFace, 7.80f, bladeZ + 0.40f);
                        CreateTubeBetween($"Club Upper Blade Housing {side}-{bay}-{blade}", lower, upper, trim, 0.083f);
                        lower.x -= side * 0.072f;
                        upper.x -= side * 0.072f;
                        CreateTubeBetween($"LED Club Upper Blade {side}-{bay}-{blade}", lower, upper, upperLed, 0.043f);
                    }

                    CreateLitBox($"Club Side Panel {side}-{bay}", new Vector3(side * 7.44f, 2.61f, z),
                        new Vector3(0.10f, 4.1f, 3.92f), panel);
                    CreateLitBox($"Club Side Column {side}-{bay}", new Vector3(side * 7.38f, 3.45f, z - 2.15f),
                        new Vector3(0.20f, 6.9f, 0.18f), trim);
                    CreateLitBox($"LED Club Side Accent {side}-{bay}", new Vector3(side * 7.25f, 2.75f, z - 2.15f),
                        new Vector3(0.045f, 4.15f, 0.075f), accentMaterial);

                    for (int segment = 0; segment < 5; segment++)
                    {
                        float runnerZ = z + (segment - 2) * 0.65f;
                        CreateLitBox($"LED Club Wall Runner {side}-{bay}-{segment}",
                            new Vector3(side * 7.32f, 5.13f, runnerZ), new Vector3(0.055f, 0.085f, 0.43f), circuit);
                    }

                    float x = side * 7.36f;
                    Vector3 low = new(x, 1.53f, z - 1.46f);
                    Vector3 elbow = new(x, 3.25f, z - 0.52f);
                    Vector3 notch = new(x, 2.94f, z + 0.16f);
                    Vector3 high = new(x, 4.69f, z + 1.46f);
                    CreateTubeBetween($"LED Wall Chevron Lower {side}-{bay}", low, elbow, circuit, 0.041f);
                    CreateTubeBetween($"LED Wall Chevron Bridge {side}-{bay}", elbow, notch, circuit, 0.041f);
                    CreateTubeBetween($"LED Wall Chevron Upper {side}-{bay}", notch, high, circuit, 0.041f);
                }
            }
        }

        internal const float MezzanineHeight = 2.55f;

        internal static Vector3 MezzanineSpot(int seat)
        {
            int side = seat % 2 == 0 ? -1 : 1;
            return new Vector3(side * 6.34f, MezzanineHeight, seat < 2 ? -2.05f : 0.05f);
        }

        private static void CreateMezzanines()
        {
            Material deck = GameMaterials.Lit(new Color(0.032f, 0.035f, 0.051f), 0.52f, 0.68f,
                new Color(0.014f, 0.017f, 0.028f), 0.045f, 12f);
            Material frame = GameMaterials.Lit(new Color(0.027f, 0.031f, 0.048f), 0.52f, 0.38f,
                new Color(0.012f, 0.016f, 0.026f));
            Material glass = GameMaterials.BackgroundSprite();
            glass.mainTexture = Texture2D.whiteTexture;
            glass.renderQueue = 3205;
            glass.SetColor("_Color", new Color(0.12f, 0.18f, 0.26f, 0.19f));

            const float centerZ = -0.80f;
            const float deckLength = 4.85f;
            const float frontZ = centerZ + deckLength / 2f;
            const float innerX = 5.29f;
            const float outerX = 7.42f;
            const float centerX = (innerX + outerX) / 2f;
            const float deckWidth = outerX - innerX;
            Material champagne = GameMaterials.Lit(new Color(0.34f, 0.26f, 0.16f), 0.73f, 0.53f,
                new Color(0.12f, 0.085f, 0.045f));
            for (int side = -1; side <= 1; side += 2)
            {
                Color accent = side < 0 ? new Color(0.12f, 0.72f, 1f) : new Color(0.72f, 0.24f, 1f);
                CreateLitBox($"Mezzanine Deck {side}", new Vector3(side * centerX, MezzanineHeight - 0.09f, centerZ),
                    new Vector3(deckWidth, 0.18f, deckLength), deck);
                CreateLitBox($"Mezzanine Fascia {side}", new Vector3(side * innerX, MezzanineHeight - 0.09f, centerZ),
                    new Vector3(0.085f, 0.18f, deckLength), frame);
                CreateBox($"LED Mezzanine Side {side}", new Vector3(side * (innerX - 0.06f), MezzanineHeight - 0.135f, centerZ),
                    new Vector3(0.022f, 0.024f, deckLength), accent * 0.12f, accent * 0.52f);
                CreateBox($"LED Mezzanine Front {side}", new Vector3(side * centerX, MezzanineHeight - 0.135f, frontZ + 0.02f),
                    new Vector3(deckWidth, 0.024f, 0.022f), accent * 0.12f, accent * 0.52f);
                CreateLitBox($"Mezzanine Side Glass {side}", new Vector3(side * innerX, MezzanineHeight + 0.33f, centerZ),
                    new Vector3(0.03f, 0.66f, deckLength), glass);
                CreateLitBox($"Mezzanine Front Glass {side}", new Vector3(side * centerX, MezzanineHeight + 0.33f, frontZ),
                    new Vector3(deckWidth, 0.66f, 0.03f), glass);
                CreateLitBox($"Mezzanine Side Handrail {side}", new Vector3(side * innerX, MezzanineHeight + 0.68f, centerZ),
                    new Vector3(0.055f, 0.039f, deckLength), champagne);
                CreateLitBox($"Mezzanine Front Handrail {side}", new Vector3(side * centerX, MezzanineHeight + 0.68f, frontZ),
                    new Vector3(deckWidth, 0.039f, 0.055f), champagne);
                CreateLitBox($"Mezzanine Inlay Side {side}", new Vector3(side * (innerX + 0.17f), MezzanineHeight + 0.004f, centerZ),
                    new Vector3(0.027f, 0.009f, deckLength - 0.34f), champagne);
                CreateLitBox($"Mezzanine Inlay Front {side}", new Vector3(side * centerX, MezzanineHeight + 0.004f, frontZ - 0.17f),
                    new Vector3(deckWidth - 0.34f, 0.009f, 0.027f), champagne);
                CreateLitBox($"Mezzanine Wall Pilaster {side}", new Vector3(side * 7.39f, MezzanineHeight + 0.96f, centerZ),
                    new Vector3(0.14f, 1.73f, deckLength - 0.24f), frame);
                Color warm = new(1f, 0.64f, 0.31f);
                for (int lamp = 0; lamp < 3; lamp++)
                {
                    float z = centerZ + (lamp - 1) * 1.37f;
                    CreateBox($"Mezzanine Warm Sconce {side}-{lamp}", new Vector3(side * 7.30f, MezzanineHeight + 1.04f, z),
                        new Vector3(0.045f, 0.42f, 0.042f), warm * 0.16f, warm * 0.53f);
                }
                for (int lamp = 0; lamp < 3; lamp++)
                {
                    Vector3 position = new(side * 6.47f, MezzanineHeight - 0.32f, centerZ + (lamp - 1) * 1.56f);
                    CreateBox($"LED Mezzanine Downlight {side}-{lamp}", position,
                        new Vector3(0.18f, 0.045f, 0.18f), accent * 0.18f, accent * 0.65f);
                    GameObject wash = new($"Mezzanine Walkway Wash {side}-{lamp}");
                    wash.transform.SetPositionAndRotation(position - Vector3.up * 0.08f,
                        Quaternion.LookRotation(new Vector3(-side * 0.16f, -1f, 0f), Vector3.forward));
                    Light light = wash.AddComponent<Light>();
                    light.type = LightType.Spot;
                    light.color = Color.Lerp(accent, Color.white, 0.52f);
                    light.range = 4.1f;
                    light.spotAngle = 62f;
                    light.innerSpotAngle = 33f;
                    light.intensity = 1.25f;
                    light.cullingMask = 1 << FloorLightingLayer;
                }
                for (int post = 0; post < 4; post++)
                {
                    float z = Mathf.Lerp(centerZ - deckLength / 2f, frontZ, post / 3f);
                    CreateLitBox($"Mezzanine Railing Post {side}-{post}", new Vector3(side * innerX, MezzanineHeight + 0.33f, z),
                        new Vector3(0.043f, 0.66f, 0.043f), frame);
                }
            }
        }

        private static void CreateCeilingLighting()
        {
            Material frame = GameMaterials.Lit(new Color(0.035f, 0.040f, 0.061f), 0.55f, 0.36f,
                new Color(0.009f, 0.012f, 0.022f));
            const float height = 9.82f;
            const float start = -8.5f;
            const float spacing = 1.26f;
            const int count = 15;
            float center = start + spacing * (count - 1) * 0.5f;
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 4.8f;
                Color accent = side < 0 ? new Color(0.67f, 0.25f, 1f) : new Color(0.14f, 0.78f, 1f);
                CreateLitBox($"Ceiling Track {side}", new Vector3(x, height, center),
                    new Vector3(0.36f, 0.17f, spacing * count), frame);
                for (int index = 0; index < count; index++)
                {
                    float z = start + index * spacing;
                    CreateBox($"LED Ceiling Chase {side}-{index}", new Vector3(x, height - 0.095f, z),
                        new Vector3(0.21f, 0.04f, 0.96f), accent * 0.16f, accent * 1.05f);
                }
            }
            Color violet = new(0.64f, 0.27f, 1f);
            Color cyan = new(0.14f, 0.73f, 1f);
            Material violetLed = GameMaterials.Club(violet * 0.14f, violet * 0.96f);
            Material cyanLed = GameMaterials.Club(cyan * 0.14f, cyan * 0.96f);
            for (int index = 0; index < 6; index++)
            {
                float z = start + index * 2.9f;
                Vector3 tip = new(0f, height - 0.15f, z + 1.58f);
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 edge = new(side * 4.73f, tip.y, z);
                    CreateTubeBetween($"Ceiling Chevron Housing {side}-{index}", edge + Vector3.up * 0.10f,
                        tip + Vector3.up * 0.10f, frame, 0.11f);
                    CreateTubeBetween($"LED Ceiling Chase Chevron {side}-{index}", edge, tip,
                        side < 0 ? violetLed : cyanLed, 0.068f);
                }
            }
        }

        private static void CreateSideFillSpeakers()
        {
            Material bezel = GameMaterials.Lit(new Color(0.026f, 0.032f, 0.052f), 0.62f, 0.42f,
                new Color(0.008f, 0.012f, 0.021f));
            Material cone = GameMaterials.Lit(new Color(0.070f, 0.085f, 0.124f), 0.25f, 0.62f,
                new Color(0.017f, 0.024f, 0.042f), 0.045f, 19f);
            Vector3 wallFacing = new(0f, 0f, 90f);
            for (int side = -1; side <= 1; side += 2)
            {
                const float z = 2.55f;
                Color accent = side < 0 ? new Color(0.74f, 0.24f, 1f) : new Color(0.12f, 0.79f, 1f);
                Material ring = GameMaterials.Club(accent * 0.12f, accent * 0.62f);
                CreateLitBox($"Recessed Speaker Bezel {side}", new Vector3(side * 7.39f, 2.55f, z),
                    new Vector3(0.10f, 1.98f, 1.10f), bezel);
                for (int index = 0; index < 2; index++)
                {
                    float y = 2.12f + index * 0.85f;
                    CreateOrientedCylinder($"Recessed Speaker Ring {side}-{index}", new Vector3(side * 7.31f, y, z),
                        new Vector3(0.76f, 0.024f, 0.76f), wallFacing, ring);
                    CreateOrientedCylinder($"Recessed Speaker Surround {side}-{index}", new Vector3(side * 7.275f, y, z),
                        new Vector3(0.69f, 0.023f, 0.69f), wallFacing, bezel);
                    GameObject driver = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    driver.name = "Recessed Speaker Cone";
                    driver.transform.position = new Vector3(side * 7.24f, y, z);
                    driver.transform.localScale = new Vector3(0.095f, 0.59f, 0.59f);
                    driver.GetComponent<Renderer>().sharedMaterial = cone;
                    Object.Destroy(driver.GetComponent<Collider>());
                }
            }
        }

        private static void CreateDjBooth()
        {
            Material chassis = GameMaterials.Lit(new Color(0.028f, 0.01f, 0.01f), 0.72f, 0.5f, new Color(0.007f, 0.002f, 0.002f));
            Material glass = GameMaterials.Lit(new Color(0.035f, 0.01f, 0.02f), 0.25f, 0.78f, new Color(0.035f, 0.006f, 0.018f));
            CreateLitBox("DJ Booth Core", new Vector3(0f, 1.2f + DjBoothHeightOffset, -7.65f), new Vector3(6.5f, 1.35f, 1.25f), chassis);
            CreateLitBox("DJ Booth Inset", new Vector3(0f, 1.18f + DjBoothHeightOffset, -6.99f), new Vector3(5.55f, 0.76f, 0.055f), glass);
            CreateLitBox("DJ Booth Top Cap", new Vector3(0f, 1.92f + DjBoothHeightOffset, -7.55f), new Vector3(6.72f, 0.13f, 1.42f), chassis);

            Color cyan = new(0.03f, 0.85f, 1f);
            Color violet = new(0.8f, 0.12f, 1f);
            CreateBox("DJ Booth Cyan Rail", new Vector3(0f, 1.87f + DjBoothHeightOffset, -6.95f), new Vector3(6.12f, 0.035f, 0.035f), cyan * 0.06f, cyan * 1.2f);
            CreateBox("DJ Booth Violet Rail", new Vector3(0f, 0.54f + DjBoothHeightOffset, -6.95f), new Vector3(5.62f, 0.028f, 0.03f), violet * 0.05f, violet * 0.95f);

            for (int index = 0; index < 17; index++)
            {
                float x = Mathf.Lerp(-2.45f, 2.45f, index / 16f);
                float height = 0.12f + Mathf.Abs(Mathf.Sin(index * 1.37f)) * 0.38f;
                Color glow = index % 3 == 0 ? violet : cyan;
                CreateBox($"Booth Equalizer {index}", new Vector3(x, 1.02f + DjBoothHeightOffset, -6.945f), new Vector3(0.12f, height, 0.025f), glow * 0.035f, glow * 0.3f);
            }

            CreateLitBox("DJ Console", new Vector3(0f, 2.06f + DjBoothHeightOffset, -7.55f), new Vector3(4.9f, 0.16f, 0.86f), chassis);

            CreateTurntable(-1.55f);
            CreateTurntable(1.55f);
            CreateMixer();
            for (int side = -1; side <= 1; side += 2)
            {
                for (int pad = 0; pad < 8; pad++)
                {
                    Color led = Color.HSVToRGB(pad / 8f, 0.85f, 1f);
                    CreateBox($"DJ Performance Pad {side}-{pad}",
                        new Vector3(side * 1.55f + (pad % 4 - 1.5f) * 0.18f, 3.04f, -7.08f + (pad / 4) * 0.13f),
                        new Vector3(0.13f, 0.04f, 0.09f), led * 0.3f, led * 2f);
                }
            }


            for (int side = -1; side <= 1; side += 2)
            {
                CreateLitBox($"Booth Side Pillar {side}", new Vector3(side * 3.14f, 1.2f + DjBoothHeightOffset, -6.96f), new Vector3(0.13f, 1.2f, 0.08f), chassis);
                CreateBox($"Booth Angle Light {side}", new Vector3(side * 2.62f, 1.18f + DjBoothHeightOffset, -6.92f), new Vector3(0.035f, 0.72f, 0.035f), cyan * 0.05f, cyan * 0.42f);
            }
        }

        private static void CreateDjPerformer()
        {
            // Thay thế DJ 3D bằng Nhân vật chính từ GIF (đã trích xuất thành mảng PNG)
            GameObject performer = GameObject.CreatePrimitive(PrimitiveType.Quad);
            performer.name = "Main Character GIF";

            // Xóa Collider
            Object.Destroy(performer.GetComponent<Collider>());

            // Đặt vị trí ngay giữa sân khấu DJ
            performer.transform.position = new Vector3(0f, 3.4f, -7.5f); // Đặt lên sàn DJ
            performer.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            performer.transform.localScale = new Vector3(4.5f, 4.5f, 1f); // Kích thước của nhân vật GIF

            // Thêm Script GifPlayer để tự động phát
            TikTokLiveBar.GifPlayer gifPlayer = performer.AddComponent<TikTokLiveBar.GifPlayer>();

            string frameDir = GamePaths.DjFrames;

            gifPlayer.FrameDirectory = frameDir;
            gifPlayer.FrameRate = 30f; // Tốc độ khung hình mượt mà

            // Ánh sáng chiếu vào nhân vật
            AddDjKeyLight("DJ Warm Key", new Vector3(-2.2f, 4.25f, -5.8f), new Color(1f, 0.55f, 0.38f), 3.2f);
            AddDjKeyLight("DJ Cool Rim", new Vector3(2.4f, 4.55f, -8.7f), new Color(0.22f, 0.55f, 1f), 2.7f);
        }

        private static void AddDjKeyLight(string name, Vector3 position, Color color, float intensity)
        {
            GameObject lightObject = new(name);
            lightObject.transform.position = position;
            lightObject.transform.LookAt(new Vector3(0f, 2.45f, -8.05f));
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = color;
            light.intensity = intensity;
            light.range = 9f;
            light.spotAngle = 46f;

            light.innerSpotAngle = 24f;
            light.shadows = LightShadows.Soft;
            light.cullingMask = ~(1 << FloorLightingLayer);

            for (int i = 0; i < 4; i++)
            {
                GameObject obj = new($"Disco Light {i}");
                obj.transform.position = new Vector3(-6f + i * 4f, 6.5f, 5f);
                obj.transform.rotation = Quaternion.Euler(50f, 180f, 0f);
                Light l = obj.AddComponent<Light>();
                l.type = LightType.Spot;
                l.range = 25f;
                l.spotAngle = 60f;
                l.innerSpotAngle = 30f;
                l.intensity = 0f;
                l.shadows = LightShadows.Soft;
                l.cullingMask = -1;
                DiscoLight disco = obj.AddComponent<DiscoLight>();
                disco.index = i;
            }
        }

        private static void CreateLedSideArrays()
        {
            Material frame = GameMaterials.Lit(new Color(0.014f, 0.016f, 0.023f), 0.68f, 0.42f, Color.black);
            for (int side = -1; side <= 1; side += 2)
            {
                float panelX = side * 4.72f;
                CreateLitBox($"LED Wall Frame {side}", new Vector3(panelX, 4.7f, -9.28f), new Vector3(1.55f, 4.65f, 0.18f), frame);
                CreateLitBox($"LED Wall Diffuser {side}", new Vector3(panelX, 4.7f, -9.16f), new Vector3(1.38f, 4.48f, 0.035f), GameMaterials.Lit(new Color(0.008f, 0.014f, 0.025f), 0.08f, 0.62f, new Color(0.004f, 0.012f, 0.024f)));
                for (int row = 0; row < 10; row++)
                    for (int column = 0; column < 4; column++)
                    {
                        Color glow = (row + column) % 3 == 0
                            ? new Color(0.52f, 0.16f, 0.95f)
                            : new Color(0.04f, 0.62f, 1f);
                        float x = panelX + (column - 1.5f) * 0.29f;
                        float y = 2.78f + row * 0.43f;
                        CreateGlowBulb($"LED Wall Diode {side}-{row}-{column}", new Vector3(x, y, -9.12f), 0.055f, glow * 0.62f);
                    }
            }
        }

        private static void CreateArchitecturalAccentLights()
        {
            Color cool = new(0.12f, 0.58f, 1f);
            for (int index = 0; index < 12; index++)
            {
                float x = Mathf.Lerp(-6.25f, 6.25f, index / 11f);
                CreateGlowBulb($"Backdrop Header Pin {index}", new Vector3(x, 8.18f, -9.72f), 0.045f, cool * 1.1f);
            }
            for (int index = 0; index < 14; index++)
            {
                float x = Mathf.Lerp(-5.95f, 5.95f, index / 13f);
                Color glow = index % 2 == 0 ? cool : new Color(0.52f, 0.14f, 0.92f);
                CreateGlowBulb($"Stage Footlight {index}", new Vector3(x, 0.8f, -5.02f), 0.05f, glow * 0.58f);
            }
        }

        private static void CreateGlowBulb(string name, Vector3 position, float size, Color glow)
        {
            GameObject bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bulb.name = name;
            bulb.transform.position = position;
            bulb.transform.localScale = Vector3.one * size;
            bulb.GetComponent<Renderer>().sharedMaterial = GameMaterials.Club(glow * 0.15f, glow * 1.2f);
        }

        private static void CreateMirrorBall()
        {
            GameObject root = new("Disco Ball");
            root.transform.position = new Vector3(0f, 8.05f, -6.5f);
            root.AddComponent<DiscoBallEffects>();
            Material darkCore = GameMaterials.Lit(new Color(0.025f, 0.03f, 0.045f), 0.88f, 0.72f, new Color(0.005f, 0.008f, 0.015f));
            Material mirror = GameMaterials.Lit(new Color(0.32f, 0.38f, 0.5f), 1f, 0.96f, new Color(0.035f, 0.055f, 0.1f));

            GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            core.name = "Mirror Ball Core";
            core.transform.SetParent(root.transform, false);
            core.transform.localScale = Vector3.one * 0.96f;
            core.GetComponent<Renderer>().sharedMaterial = darkCore;

            const int longitudeCount = 16;
            const int latitudeCount = 7;
            for (int latitude = 0; latitude < latitudeCount; latitude++)
            {
                float latitudeAngle = Mathf.Lerp(-62f, 62f, latitude / (float)(latitudeCount - 1)) * Mathf.Deg2Rad;
                for (int longitude = 0; longitude < longitudeCount; longitude++)
                {
                    float longitudeAngle = longitude * Mathf.PI * 2f / longitudeCount + (latitude % 2) * 0.08f;
                    Vector3 normal = new(
                        Mathf.Cos(latitudeAngle) * Mathf.Sin(longitudeAngle),
                        Mathf.Sin(latitudeAngle),
                        Mathf.Cos(latitudeAngle) * Mathf.Cos(longitudeAngle)
                    );
                    GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    tile.name = $"Mirror Tile {latitude}-{longitude}";
                    tile.transform.SetParent(root.transform, false);
                    tile.transform.localPosition = normal * 0.51f;
                    tile.transform.localRotation = Quaternion.FromToRotation(Vector3.forward, normal);
                    tile.transform.localScale = new Vector3(0.075f, 0.075f, 0.018f);
                    tile.GetComponent<Renderer>().sharedMaterial = mirror;
                }
            }

            Material cable = GameMaterials.Lit(new Color(0.01f, 0.012f, 0.016f), 0.7f, 0.3f, Color.black);
            CreateTubeBetween("Mirror Ball Cable", new Vector3(0f, 8.55f, -6.5f), new Vector3(0f, 9.4f, -6.5f), cable, 0.018f);
        }

        private static void CreateCircularTrussRig()
        {
            Vector3 center = new(0f, 6.35f, -2.25f);
            const float outerRadius = 3.7f;
            const float innerRadius = 3.34f;
            const int segments = 32;
            const float halfHeight = 0.2f;
            Quaternion rigTilt = Quaternion.Euler(10f, 0f, 0f);
            Material trussMaterial = GameMaterials.Lit(
                new Color(0.16f, 0.17f, 0.19f),
                0.82f,
                0.58f,
                new Color(0.012f, 0.02f, 0.03f)
            );

            Vector3 RingPoint(float radius, float angle, float height)
            {
                return center + rigTilt * new Vector3(Mathf.Sin(angle) * radius, height, Mathf.Cos(angle) * radius);
            }

            for (int index = 0; index < segments; index++)
            {
                float angle = index * Mathf.PI * 2f / segments;
                float nextAngle = (index + 1) * Mathf.PI * 2f / segments;

                CreateTrussBarBetween($"Circular Truss Outer Top {index}", RingPoint(outerRadius, angle, halfHeight), RingPoint(outerRadius, nextAngle, halfHeight), trussMaterial);
                CreateTrussBarBetween($"Circular Truss Outer Bottom {index}", RingPoint(outerRadius, angle, -halfHeight), RingPoint(outerRadius, nextAngle, -halfHeight), trussMaterial);
                CreateTrussBarBetween($"Circular Truss Inner Top {index}", RingPoint(innerRadius, angle, halfHeight), RingPoint(innerRadius, nextAngle, halfHeight), trussMaterial);
                CreateTrussBarBetween($"Circular Truss Inner Bottom {index}", RingPoint(innerRadius, angle, -halfHeight), RingPoint(innerRadius, nextAngle, -halfHeight), trussMaterial);

                if (index % 4 == 0)
                {
                    CreateTrussBarBetween($"Circular Truss Radial {index}", RingPoint(innerRadius, angle, 0f), RingPoint(outerRadius, angle, 0f), trussMaterial);
                    CreateTrussBarBetween($"Circular Truss Vertical Outer {index}", RingPoint(outerRadius, angle, -halfHeight), RingPoint(outerRadius, angle, halfHeight), trussMaterial);
                    CreateTrussBarBetween($"Circular Truss Vertical Inner {index}", RingPoint(innerRadius, angle, -halfHeight), RingPoint(innerRadius, angle, halfHeight), trussMaterial);
                }
            }

            Material cableMaterial = GameMaterials.Lit(new Color(0.008f, 0.009f, 0.012f), 0.65f, 0.28f, Color.black);
            for (int index = 0; index < 4; index++)
            {
                float angle = index * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
                Vector3 clampPosition = RingPoint(outerRadius, angle, halfHeight);
                CreateTubeBetween($"Truss Suspension Cable {index}", clampPosition, new Vector3(clampPosition.x, 9.25f, clampPosition.z), cableMaterial, 0.018f);
                GameObject clamp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                clamp.name = $"Truss Clamp {index}";
                clamp.transform.position = clampPosition;
                clamp.transform.localScale = new Vector3(0.11f, 0.065f, 0.11f);
                clamp.GetComponent<Renderer>().sharedMaterial = trussMaterial;
            }

            Color electricBlue = new(0.23f, 0.74f, 1f);
            Color violetPink = new(0.92f, 0.31f, 1f);
            for (int pair = 0; pair < 4; pair++)
            {
                float angle = (pair + 0.5f) * Mathf.PI / 4f;
                MovingHeadStyle style = pair % 2 == 0 ? MovingHeadStyle.Spot : MovingHeadStyle.Wash;
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 origin = RingPoint(3.52f, angle * side, -0.34f);
                    Color color = pair % 2 == 0 ? electricBlue : violetPink;
                    CreateMovingHead($"Moving Head Ring {pair} {side}", origin, color, pair * 1.76f, style, pair, side);
                }
            }
        }

        private static void CreateTrussBarBetween(string name, Vector3 start, Vector3 end, Material material)
        {
            CreateTubeBetween(name, start, end, material, 0.045f);
        }

        private static void CreateTubeBetween(string name, Vector3 start, Vector3 end, Material material, float radius)
        {
            GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bar.name = name;
            bar.transform.position = (start + end) * 0.5f;
            Vector3 direction = end - start;
            bar.transform.rotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
            bar.transform.localScale = new Vector3(radius, direction.magnitude * 0.5f, radius);
            bar.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void CreateMovingHead(string name, Vector3 origin, Color color, float phase, MovingHeadStyle style, int pair, float side)
        {
            GameObject fixture = new(name);
            fixture.transform.position = origin;
            Material metal = GameMaterials.Lit(new Color(0.025f, 0.028f, 0.035f), 0.72f, 0.48f, Color.black);
            bool wash = style == MovingHeadStyle.Wash;
            bool beam = style == MovingHeadStyle.Beam;
            Material lens = GameMaterials.Lit(color * 0.18f, 0.12f, 0.82f, color * (wash ? 1.25f : beam ? 2.1f : 1.8f));

            CreateFixturePart("Base", fixture.transform, PrimitiveType.Cylinder, new Vector3(0f, -0.12f, -0.08f), new Vector3(0.24f, 0.08f, 0.24f), metal);
            CreateFixturePart("Yoke Left", fixture.transform, PrimitiveType.Cube, new Vector3(-0.19f, 0f, 0.02f), new Vector3(0.055f, 0.34f, 0.085f), metal);
            CreateFixturePart("Yoke Right", fixture.transform, PrimitiveType.Cube, new Vector3(0.19f, 0f, 0.02f), new Vector3(0.055f, 0.34f, 0.085f), metal);
            CreateFixturePart("Lamp Body", fixture.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.08f, 0.08f), new Vector3(0.2f, 0.22f, 0.2f), metal, new Vector3(90f, 0f, 0f));
            CreateFixturePart("Lens", fixture.transform, PrimitiveType.Sphere, new Vector3(0f, 0.08f, 0.29f), wash ? new Vector3(0.16f, 0.16f, 0.055f) : new Vector3(0.125f, 0.125f, 0.045f), lens);
            MovingHeadFixture head = fixture.AddComponent<MovingHeadFixture>();
            head.Initialize(color, phase, style, pair, side);
        }

        private static void CreateFixturePart(string name, Transform parent, PrimitiveType type, Vector3 localPosition, Vector3 localScale, Material material, Vector3 localEuler = default)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localEulerAngles = localEuler;
            part.transform.localScale = localScale;
            part.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void CreateLedBarRig()
        {
            Material housing = GameMaterials.Lit(new Color(0.018f, 0.02f, 0.028f), 0.78f, 0.42f, Color.black);
            for (int index = 0; index < 8; index++)
            {
                Color glow = index % 2 == 0 ? new Color(0.04f, 0.62f, 1f) : new Color(0.52f, 0.14f, 0.92f);
                float x = Mathf.Lerp(-5.8f, 5.8f, index / 7f);
                float z = -6.35f + Mathf.Sin(index * 0.78f) * 0.8f;
                CreateLitBox($"LED Batten Housing {index}", new Vector3(x, 7.58f, z), new Vector3(0.15f, 0.11f, 1.15f), housing);
                CreateBox($"LED Batten Diffuser {index}", new Vector3(x, 7.515f, z), new Vector3(0.08f, 0.025f, 1.02f), glow * 0.04f, glow * 0.36f);
            }
            for (int side = -1; side <= 1; side += 2)
                for (int index = 0; index < 4; index++)
                {
                    Color glow = index % 2 == 0 ? new Color(0.04f, 0.62f, 1f) : new Color(0.52f, 0.14f, 0.92f);
                    float y = 2.2f + index * 1.25f;
                    CreateLitBox($"Wall Strip Housing {side}-{index}", new Vector3(side * 6.72f, y, -6.3f), new Vector3(0.12f, 0.92f, 0.18f), housing);
                    CreateBox($"Wall Strip Diffuser {side}-{index}", new Vector3(side * 6.64f, y, -6.2f), new Vector3(0.025f, 0.78f, 0.06f), glow * 0.04f, glow * 0.3f);
                }
        }

        private static void CreateStrobes()
        {
            Material housing = GameMaterials.Lit(new Color(0.018f, 0.02f, 0.026f), 0.76f, 0.44f, Color.black);
            Material lens = GameMaterials.Lit(new Color(0.32f, 0.34f, 0.4f), 0.08f, 0.72f, new Color(0.09f, 0.1f, 0.14f));
            float[] positions = { -5.2f, -1.8f, 1.8f, 5.2f };
            for (int index = 0; index < positions.Length; index++)
            {
                float x = positions[index];
                GameObject strobeObject = new($"Strobe Light {index}");
                strobeObject.transform.position = new Vector3(x, index is 1 or 2 ? 7.15f : 6.3f, index is 1 or 2 ? -4.15f : -5.2f);
                CreateFixturePart("Strobe Housing", strobeObject.transform, PrimitiveType.Cube, Vector3.zero, new Vector3(0.58f, 0.24f, 0.28f), housing);
                CreateFixturePart("Strobe Lens", strobeObject.transform, PrimitiveType.Cube, new Vector3(0f, 0f, 0.16f), new Vector3(0.46f, 0.14f, 0.045f), lens);
                Light strobe = strobeObject.AddComponent<Light>();
                strobe.type = LightType.Spot;
                strobe.color = Color.white;
                strobe.range = 22f;
                strobe.spotAngle = 72f;
                strobe.intensity = 0f;
                strobe.cullingMask = ~(1 << FloorLightingLayer);
                strobeObject.transform.LookAt(new Vector3(x * 0.3f, 0.7f, 0.8f));
                strobeObject.AddComponent<ClubStrobeLight>().Initialize(strobe, index * 0.055f);
            }
        }

        private static void CreateTurntable(float x)
        {
            Material chassis = GameMaterials.Lit(new Color(0.02f, 0.023f, 0.03f), 0.72f, 0.48f, Color.black);
            Material platterMaterial = GameMaterials.Lit(new Color(0.035f, 0.04f, 0.05f), 0.62f, 0.65f, new Color(0.004f, 0.008f, 0.014f));
            GameObject deck = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            deck.name = $"DJ Turntable {x}";
            deck.transform.position = new Vector3(x, 2.16f + DjBoothHeightOffset, -7.45f);
            deck.transform.localScale = new Vector3(1.05f, 0.055f, 1.05f);
            deck.GetComponent<Renderer>().sharedMaterial = chassis;

            GameObject platter = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            platter.name = "Turntable Platter";
            platter.transform.position = new Vector3(x, 2.225f + DjBoothHeightOffset, -7.45f);
            platter.transform.localScale = new Vector3(0.86f, 0.022f, 0.86f);
            platter.GetComponent<Renderer>().sharedMaterial = platterMaterial;

            CreateOrientedCylinder("Turntable LED Ring", new Vector3(x, 2.205f + DjBoothHeightOffset, -7.45f), new Vector3(1.08f, 0.008f, 1.08f), Vector3.zero,
                GameMaterials.Club(Color.black, new Color(0.04f, 0.8f, 1.5f)));
            CreateOrientedCylinder("Turntable Label", new Vector3(x, 2.255f + DjBoothHeightOffset, -7.45f), new Vector3(0.17f, 0.012f, 0.17f), Vector3.zero, GameMaterials.Lit(new Color(0.16f, 0.18f, 0.22f), 0.45f, 0.56f, new Color(0.01f, 0.018f, 0.03f)));
            CreateOrientedCylinder("Turntable Spindle", new Vector3(x, 2.3f + DjBoothHeightOffset, -7.45f), new Vector3(0.025f, 0.065f, 0.025f), Vector3.zero, chassis);
        }

        private static void CreateMixer()
        {
            Material mixerMaterial = GameMaterials.Lit(new Color(0.018f, 0.021f, 0.028f), 0.68f, 0.46f, Color.black);
            CreateLitBox("DJ Mixer", new Vector3(0f, 2.15f + DjBoothHeightOffset, -7.45f), new Vector3(1.05f, 0.12f, 0.72f), mixerMaterial);
            for (int index = -3; index <= 3; index++)
            {
                Color color = index % 2 == 0 ? new Color(0.02f, 0.75f, 1f) : new Color(1f, 0.04f, 0.52f);
                CreateBox($"Mixer Fader {index}", new Vector3(index * 0.12f, 2.225f + DjBoothHeightOffset, -7.42f), new Vector3(0.018f, 0.012f, 0.32f), color * 0.08f, color * 0.32f);
                CreateOrientedCylinder($"Mixer Knob {index}", new Vector3(index * 0.12f, 2.255f + DjBoothHeightOffset, -7.55f), new Vector3(0.035f, 0.028f, 0.035f), Vector3.zero, mixerMaterial);
            }
        }

        private static void CreateBox(string name, Vector3 position, Vector3 scale, Color color, Color emission)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.position = position;
            box.transform.localScale = scale;
            box.GetComponent<Renderer>().sharedMaterial = GameMaterials.Club(color, emission);
        }

        private static void CreateLitBox(string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.position = position;
            box.transform.localScale = scale;
            box.layer = FloorLightingLayer;
            box.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void CreateSpeaker(float x)
        {
            GameObject[] existing = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            Material cabinet = GameMaterials.Lit(new Color(0.025f, 0.03f, 0.042f), 0.65f, 0.6f, new Color(0.005f, 0.008f, 0.015f), 0.025f, 16f);
            Material bezel = GameMaterials.Lit(new Color(0.065f, 0.075f, 0.095f), 0.85f, 0.75f, new Color(0.012f, 0.018f, 0.03f));
            Material rubber = GameMaterials.Lit(new Color(0.008f, 0.012f, 0.023f), 0.1f, 0.38f, Color.black);
            Material cone = GameMaterials.Lit(new Color(0.06f, 0.075f, 0.11f), 0.65f, 0.68f, new Color(0.012f, 0.02f, 0.04f), 0.04f, 24f);
            CreateLitBox($"Speaker Cabinet {x}", new Vector3(x, 2.15f, -5.45f), new Vector3(1.7f, 4.3f, 1.25f), cabinet);
            CreateLitBox($"Speaker Front Baffle {x}", new Vector3(x, 2.15f, -4.79f), new Vector3(1.48f, 4.04f, 0.09f), rubber);
            CreateLitBox($"Speaker Top Bezel {x}", new Vector3(x, 4.23f, -4.77f), new Vector3(1.72f, 0.1f, 0.16f), bezel);
            CreateLitBox($"Speaker Bottom Bezel {x}", new Vector3(x, 0.08f, -4.77f), new Vector3(1.72f, 0.12f, 0.16f), bezel);
            for (int side = -1; side <= 1; side += 2)
            {
                CreateLitBox($"Speaker Edge {x}-{side}", new Vector3(x + side * 0.79f, 2.15f, -4.73f), new Vector3(0.07f, 4.12f, 0.12f), bezel);
                for (int segment = 0; segment < 16; segment++)
                    CreateBox($"Speaker LED {x}-{side}-{segment}", new Vector3(x + side * 0.70f, 0.34f + segment * 0.23f, -4.66f), new Vector3(0.045f, 0.16f, 0.03f), Color.black, new Color(0.02f, 0.7f, 1f));
            }
            for (int index = 0; index < 2; index++)
            {
                float y = 1.12f + index * 1.55f;
                CreateOrientedCylinder("Speaker Rubber Surround", new Vector3(x, y, -4.69f), new Vector3(1.2f, 0.065f, 1.2f), new Vector3(90f, 0f, 0f), rubber);
                CreateOrientedCylinder("Speaker Cone", new Vector3(x, y, -4.60f), new Vector3(1.02f, 0.055f, 1.02f), new Vector3(90f, 0f, 0f), cone);
                GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                cap.name = "Speaker Dust Cap"; cap.transform.position = new Vector3(x, y, -4.52f);
                cap.transform.localScale = new Vector3(0.39f, 0.39f, 0.13f);
                cap.GetComponent<Renderer>().sharedMaterial = bezel;
            }
            CreateLitBox($"Speaker Horn Frame {x}", new Vector3(x, 3.78f, -4.70f), new Vector3(0.95f, 0.47f, 0.13f), bezel);
            CreateLitBox($"Speaker Horn Mouth {x}", new Vector3(x, 3.78f, -4.60f), new Vector3(0.79f, 0.29f, 0.045f), rubber);
            GameObject speakerRoot = new GameObject($"Stage Speaker {x}");
            foreach (GameObject item in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                if (item != speakerRoot && item.transform.parent == null && System.Array.IndexOf(existing, item) < 0)
                    item.transform.SetParent(speakerRoot.transform, true);
            // Stage deck top is y=0.75; rear of cabinet stays in front of the rear wall.
            speakerRoot.transform.position = new Vector3(0f, 0.75f, -3.85f);
        }

        private static void CreateOrientedCylinder(string name, Vector3 position, Vector3 scale, Vector3 euler, Material material)
        {
            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = name;
            cylinder.transform.position = position;
            cylinder.transform.localEulerAngles = euler;
            cylinder.transform.localScale = scale;
            cylinder.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void AddLight(string name, Vector3 position, Color color, float intensity, float range)
        {
            GameObject lightObject = new(name);
            lightObject.transform.position = position;
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.cullingMask = ~(1 << FloorLightingLayer);
        }

    }
}

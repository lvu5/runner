using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace InfiniteRunner
{
    /// <summary>
    /// Builds the complete prototype when Play starts. Keeping the art procedural
    /// means the project has no missing prefab, material, or scene-reference setup.
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneLoadHook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateGame()
        {
            if (!Application.isPlaying || GameManager.Instance != null)
            {
                return;
            }

            ConfigureEnvironment();

            MediaPipeBridge poseBridge = EnsureMediaPipeBridge();
            poseBridge.PrepareForRun();

            GameObject gameRoot = new GameObject("Saigon Infinite Runner");
            PrototypePalette palette = new PrototypePalette();
            gameRoot.AddComponent<PrototypeMaterialOwner>().Configure(palette);
            RenderSettings.skybox = palette.Sky;

            GameObject systems = new GameObject("Game Systems");
            systems.transform.SetParent(gameRoot.transform, false);
            GameManager gameManager = systems.AddComponent<GameManager>();

            RunnerController runner = CreatePlayer(gameRoot.transform, gameManager, palette);
            gameManager.Configure(runner);

            GameObject trackObject = new GameObject("Infinite Track");
            trackObject.transform.SetParent(gameRoot.transform, false);
            TrackManager trackManager = trackObject.AddComponent<TrackManager>();
            trackManager.Configure(runner, gameManager, palette);

            CreateCamera(gameRoot.transform, runner.transform);
            CreateLighting(gameRoot.transform);
            SaigonScenery.CreateSkyline(gameRoot.transform, runner.transform, palette);

            RunnerHud hud = systems.AddComponent<RunnerHud>();
            hud.Configure(gameManager, trackManager);
        }

        private static MediaPipeBridge EnsureMediaPipeBridge()
        {
            if (MediaPipeBridge.Instance != null)
            {
                return MediaPipeBridge.Instance;
            }

            GameObject bridgeObject = new GameObject(MediaPipeBridge.BridgeObjectName);
            return bridgeObject.AddComponent<MediaPipeBridge>();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            CreateGame();
        }

        private static RunnerController CreatePlayer(
            Transform parent,
            GameManager gameManager,
            PrototypePalette palette)
        {
            GameObject playerObject = new GameObject("Player");
            playerObject.transform.SetParent(parent, false);
            playerObject.transform.position = new Vector3(0f, 0.02f, 4f);

            CharacterController controller = playerObject.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.42f;
            controller.center = new Vector3(0f, 1f, 0f);
            controller.stepOffset = 0.25f;
            controller.skinWidth = 0.06f;
            controller.slopeLimit = 45f;

            RunnerController runner = playerObject.AddComponent<RunnerController>();

            GameObject visualRootObject = new GameObject("Runner Visual");
            visualRootObject.transform.SetParent(playerObject.transform, false);
            Transform visualRoot = visualRootObject.transform;

            PrototypeFactory.CreatePrimitive(
                "Torso",
                PrimitiveType.Cube,
                visualRoot,
                new Vector3(0f, 1.18f, 0f),
                new Vector3(0.78f, 0.9f, 0.52f),
                palette.PlayerBody,
                false);

            PrototypeFactory.CreatePrimitive(
                "Head",
                PrimitiveType.Sphere,
                visualRoot,
                new Vector3(0f, 1.82f, 0f),
                Vector3.one * 0.52f,
                palette.Skin,
                false);

            // Non la: the conical palm-leaf hat, with a chin strap.
            GameObject hatObject = new GameObject("Non la hat");
            hatObject.transform.SetParent(visualRoot, false);
            var hat = new SceneryMeshBuilder(hatObject.transform);
            hat.Cone(new Vector3(0f, 1.98f, 0f), 0.62f, 0.42f, palette.Straw, 18);
            hat.Cone(new Vector3(0f, 1.975f, 0f), 0.6f, 0.005f, palette.Bamboo, 18);
            hat.Box("Chin strap", new Vector3(0.265f, 1.8f, 0f), new Vector3(0.02f, 0.34f, 0.04f), palette.ShopRed);
            hat.Box("Chin strap", new Vector3(-0.265f, 1.8f, 0f), new Vector3(0.02f, 0.34f, 0.04f), palette.ShopRed);
            hat.Finish();

            PrototypeFactory.CreatePrimitive(
                "Crossbody bag",
                PrimitiveType.Cube,
                visualRoot,
                new Vector3(0.18f, 0.92f, -0.3f),
                new Vector3(0.42f, 0.34f, 0.14f),
                palette.PlayerAccent,
                false);

            PrototypeFactory.CreatePrimitive(
                "Bag strap",
                PrimitiveType.Cube,
                visualRoot,
                new Vector3(0f, 1.25f, -0.27f),
                new Vector3(0.06f, 0.95f, 0.03f),
                palette.PlayerAccent,
                false,
                Quaternion.Euler(0f, 0f, -38f));

            Transform leftArm = PrototypeFactory.CreatePrimitive(
                "Left Arm",
                PrimitiveType.Cube,
                visualRoot,
                new Vector3(-0.55f, 1.18f, 0f),
                new Vector3(0.22f, 0.76f, 0.22f),
                palette.PlayerBody,
                false).transform;

            Transform rightArm = PrototypeFactory.CreatePrimitive(
                "Right Arm",
                PrimitiveType.Cube,
                visualRoot,
                new Vector3(0.55f, 1.18f, 0f),
                new Vector3(0.22f, 0.76f, 0.22f),
                palette.PlayerBody,
                false).transform;

            foreach (Transform arm in new[] { leftArm, rightArm })
            {
                PrototypeFactory.CreatePrimitive("Hand", PrimitiveType.Cube, arm, new Vector3(0f, -0.42f, 0f),
                    new Vector3(0.85f, 0.28f, 0.85f), palette.Skin, false);
            }

            Transform leftLeg = PrototypeFactory.CreatePrimitive(
                "Left Leg",
                PrimitiveType.Cube,
                visualRoot,
                new Vector3(-0.22f, 0.48f, 0f),
                new Vector3(0.28f, 0.78f, 0.3f),
                palette.PlayerDark,
                false).transform;

            Transform rightLeg = PrototypeFactory.CreatePrimitive(
                "Right Leg",
                PrimitiveType.Cube,
                visualRoot,
                new Vector3(0.22f, 0.48f, 0f),
                new Vector3(0.28f, 0.78f, 0.3f),
                palette.PlayerDark,
                false).transform;

            PrototypeFactory.CreatePrimitive(
                "Left Sandal",
                PrimitiveType.Cube,
                leftLeg,
                new Vector3(0f, -0.48f, 0.12f),
                new Vector3(1.25f, 0.13f, 1.9f),
                palette.ShopYellow,
                false);

            PrototypeFactory.CreatePrimitive(
                "Right Sandal",
                PrimitiveType.Cube,
                rightLeg,
                new Vector3(0f, -0.48f, 0.12f),
                new Vector3(1.25f, 0.13f, 1.9f),
                palette.ShopYellow,
                false);

            PrototypeFactory.CreatePrimitive(
                "Shirt hem",
                PrimitiveType.Cube,
                visualRoot,
                new Vector3(0f, 0.76f, 0f),
                new Vector3(0.8f, 0.1f, 0.54f),
                palette.LaneLine,
                false);

            runner.Configure(gameManager, visualRoot);

            RunnerVisualAnimator visualAnimator = playerObject.AddComponent<RunnerVisualAnimator>();
            visualAnimator.Configure(
                gameManager,
                runner,
                visualRoot,
                leftArm,
                rightArm,
                leftLeg,
                rightLeg);

            return runner;
        }

        private static void CreateCamera(Transform parent, Transform target)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.transform.SetParent(parent, false);
            cameraObject.tag = "MainCamera";

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 66f;
            camera.nearClipPlane = 0.08f;
            camera.farClipPlane = 600f;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.backgroundColor = new Color(0.62f, 0.8f, 0.93f);

            cameraObject.AddComponent<AudioListener>();
            CameraFollow follow = cameraObject.AddComponent<CameraFollow>();
            follow.Configure(target);
        }

        private static void CreateLighting(Transform parent)
        {
            GameObject lightObject = new GameObject("Saigon Afternoon Sun");
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.rotation = Quaternion.Euler(42f, -38f, 0f);

            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.9f, 0.74f);
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.55f;
            RenderSettings.sun = light;
        }

        private static void ConfigureEnvironment()
        {
            RenderSettings.skybox = null;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            // Warm, humid afternoon haze.
            RenderSettings.fogColor = new Color(0.93f, 0.86f, 0.74f);
            RenderSettings.fogStartDistance = 85f;
            RenderSettings.fogEndDistance = 470f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.72f, 0.8f, 0.9f);
            RenderSettings.ambientEquatorColor = new Color(0.78f, 0.72f, 0.64f);
            RenderSettings.ambientGroundColor = new Color(0.5f, 0.43f, 0.36f);
            QualitySettings.shadowDistance = 65f;

            QualitySettings.vSyncCount = 1;
#if UNITY_WEBGL
            Application.targetFrameRate = 60;
#else
            Application.targetFrameRate = 120;
#endif
        }
    }
}

using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace InfiniteRunner.Editor
{
    public static class MetroRouteValidation
    {
        [MenuItem("Infinite Runner/Validate Metro Route", priority = 27)]
        public static void Validate()
        {
            PrepareScene();
            Scene original = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var root = new GameObject("Metro validation course");
            var palette = new PrototypePalette();
            try
            {
                GameObject road = PrototypeFactory.CreatePrimitive("Test road", PrimitiveType.Cube, root.transform,
                    new Vector3(0, -0.25f, 200), new Vector3(9.6f, 0.5f, 400), palette.Road, true);
                road.AddComponent<TrackSurface>();
                var sections = new GameObject[4];
                for (int offset = 0; offset < 4; offset++)
                {
                    int serial = 4 + offset;
                    sections[offset] = MetroRoofRoute.Build(root.transform, MetroRoofRoute.PhaseForSegment(serial), 30, 2.75f, palette);
                    sections[offset].transform.localPosition = new Vector3(0, 0, serial * 30);
                }
                int seenMasks = 0;
                for (int route = 0; route < 7; route++)
                {
                    int serial = MetroRoofRoute.FirstSegment + route * MetroRoofRoute.RepeatEverySegments;
                    int mask = MetroRoofRoute.LaneMaskForSegment(serial);
                    Require(mask > 0 && mask <= 7, "Every layout must have an open lane");
                    seenMasks |= 1 << mask;
                    Require(route != 0 || mask == 5, "The first route must offer lanes 1 and 3");
                    for (int offset = 0; offset < 4; offset++)
                    {
                        Require(MetroRoofRoute.LaneMaskForSegment(serial + offset) == mask, "Layout changed mid-route");
                        MetroRoofRoute.ApplyLaneMask(sections[offset], MetroRoofRoute.PhaseForSegment(serial + offset), mask);
                    }
                    Physics.SyncTransforms();
                    ValidateSurfaces(mask);
                    foreach (float speed in new[] { 8f, 17f })
                    foreach (float dt in new[] { 1f / 60, 1f / 30 })
                    for (int lane = 0; lane < 3; lane++)
                    {
                        if (MetroRoofRoute.IsLaneOpen(mask, lane))
                        {
                            Traverse(root.transform, speed, dt, false, lane, mask);
                            Traverse(root.transform, speed, dt, true, lane, mask);
                        }
                        else
                        {
                            for (int action = 0; action < 3; action++)
                                ValidateBlockedLane(root.transform, speed, dt, lane, action);
                        }
                    }
                }
                Require(seenMasks == 254, "All seven nonempty lane combinations must appear");
                root.SetActive(false);
                ValidateRecycling(scene, palette);
                Debug.Log("METRO ROUTE CHECKS PASSED: all seven lane layouts, clear ramps and connected roof seams, blocked lanes fatal standing/jumping/crouching, ascent/descent at 8 and 17 m/s at 30/60 fps, rooftop movement, correct coin/vent lanes, advance warning space, and pooled reuse.");
            }
            finally
            {
                palette.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
                SceneManager.SetActiveScene(original);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void ValidateSurfaces(int mask)
        {
            for (int lane = 0; lane < 3; lane++)
            {
                float x = (lane - 1) * 2.75f;
                if (!MetroRoofRoute.IsLaneOpen(mask, lane))
                {
                    Require(Physics.Raycast(new Vector3(x, 6, 165), Vector3.down, out RaycastHit blocked, 8)
                        && blocked.collider.GetComponent<ObstacleMarker>() != null, "Closed lanes must have a hazard instead of a roof");
                    Require(Mathf.Abs(blocked.point.y - MetroRoofRoute.ClosureHeight) < 0.04f, "Closed-lane geometry must match collision");
                    continue;
                }
                for (float z = 119.75f; z < 240.5f; z += 0.5f)
                {
                    float expected = ExpectedHeight(z);
                    Require(Physics.Raycast(new Vector3(x, expected + 1, z), Vector3.down, out RaycastHit hit, 2), "Missing surface along an open metro lane");
                    Require(hit.collider.GetComponent<TrackSurface>() != null, "Open metro decks must be walkable");
                    Require(Mathf.Abs(hit.point.y - expected) < 0.04f, $"Uneven seam at x={x}, z={z}");
                }
                if (lane < 2 && MetroRoofRoute.IsLaneOpen(mask, lane + 1))
                    Require(Physics.Raycast(new Vector3(x + 1.375f, 5, 165), Vector3.down, out RaycastHit seam, 1)
                        && seam.collider.GetComponent<TrackSurface>() != null, "Adjacent open roofs must meet");
            }
        }

        private static void ValidateBlockedLane(Transform parent, float speed, float dt, int lane, int action)
        {
            var player = new GameObject("Closed lane probe");
            player.transform.SetParent(parent, false);
            player.transform.position = new Vector3((lane - 1) * 2.75f, action == 1 ? 2.15f : 0.03f, 128);
            CharacterController controller = player.AddComponent<CharacterController>();
            controller.height = action == 2 ? 1 : 2;
            controller.center = Vector3.up * controller.height / 2;
            controller.radius = 0.42f; controller.skinWidth = 0.06f;
            controller.stepOffset = 0.25f; controller.slopeLimit = 45;
            var contacts = player.AddComponent<MetroRouteContactProbe>();
            try
            {
                Physics.SyncTransforms();
                for (int step = 0; step < 180 && !contacts.HitHazard; step++)
                    controller.Move(new Vector3(0, 0, speed * dt));
                Require(contacts.HitHazard, $"Closed lane {lane + 1} did not report a hazard for action {action}, stopped at {player.transform.position}");
                Require(player.transform.position.z < 132, "A runner passed through the closure");
            }
            finally { UnityEngine.Object.DestroyImmediate(player); }
        }

        private static void Traverse(Transform parent, float speed, float dt, bool jump, int lane, int mask)
        {
            var player = new GameObject("Traversal probe");
            player.transform.SetParent(parent, false);
            player.transform.position = new Vector3((lane - 1) * 2.75f, 0.03f, 118);
            CharacterController controller = player.AddComponent<CharacterController>();
            controller.height = 2; controller.center = Vector3.up;
            controller.radius = 0.42f; controller.skinWidth = 0.06f;
            controller.stepOffset = 0.25f; controller.slopeLimit = 45;
            float vertical = -2, peak = 0, lateralVelocity = 0;
            bool jumped = false;
            var contacts = player.AddComponent<MetroRouteContactProbe>();
            try
            {
                Physics.SyncTransforms();
                for (int step = 0; step < 3000 && player.transform.position.z < 244; step++)
                {
                    bool grounded = controller.isGrounded;
                    if (grounded && vertical < 0) vertical = -2;
                    if (jump && !jumped && grounded && player.transform.position.z > 170)
                    { vertical = Mathf.Sqrt(2 * 26 * 2.15f); jumped = true; }
                    bool crouch = player.transform.position.z > 188 && player.transform.position.z < 195;
                    controller.height = crouch ? 1 : 2;
                    controller.center = Vector3.up * (crouch ? 0.5f : 1);
                    vertical -= 26 * dt;
                    int targetLane = lane;
                    if (player.transform.position.z > 156 && player.transform.position.z < 200)
                    {
                        if (lane < 2 && MetroRoofRoute.IsLaneOpen(mask, lane + 1)) targetLane = lane + 1;
                        else if (lane > 0 && MetroRoofRoute.IsLaneOpen(mask, lane - 1)) targetLane = lane - 1;
                    }
                    float targetX = (targetLane - 1) * 2.75f;
                    float x = Mathf.SmoothDamp(player.transform.position.x, targetX, ref lateralVelocity, 0.12f, 20, dt);
                    CollisionFlags flags = controller.Move(new Vector3(x - player.transform.position.x, vertical * dt, speed * dt));
                    bool supported = (flags & CollisionFlags.Below) != 0;
                    if (grounded && vertical <= 0)
                        supported |= RunnerGrounding.TrySnapToSurface(controller, controller.stepOffset + speed * dt);
                    if (supported && vertical < 0) vertical = -2;
                    peak = Mathf.Max(peak, player.transform.position.y);
                    if (!jump || player.transform.position.z < 170 || player.transform.position.z > 195)
                        Require(Mathf.Abs(player.transform.position.y - ExpectedHeight(player.transform.position.z)) < 0.24f,
                            $"Runner lost ground at speed={speed}, dt={dt}, z={player.transform.position.z}, y={player.transform.position.y}");
                }
                Require(!contacts.HitHazard, $"An open route touched a hazard: mask={mask}, lane={lane}, speed={speed}, dt={dt}, contact={contacts.LastHazard}");
                Require(player.transform.position.z >= 244, "Runner got stuck on the route");
                Require(Mathf.Abs(player.transform.position.y) < 0.15f, "Runner must return to street level");
                Require(peak >= (jump ? MetroRoofRoute.RoofHeight + 1.8f : MetroRoofRoute.RoofHeight - 0.15f), "Runner must reach the roof and retain the ability to jump");
            }
            finally { UnityEngine.Object.DestroyImmediate(player); }
        }

        private static void ValidateRecycling(Scene scene, PrototypePalette palette)
        {
            var item = new GameObject("Recycled CTA block");
            SceneManager.MoveGameObjectToScene(item, scene);
            TrackSegment segment = item.AddComponent<TrackSegment>();
            segment.Initialize(0, 30, 2.75f, null, palette);
            try
            {
                int afterWarmup = 0;
                for (int cycle = 0; cycle < 3; cycle++)
                {
                    for (int serial = 3; serial <= 88; serial++)
                    {
                        segment.Place(serial * 30, serial);
                        MetroRoutePhase phase = MetroRoofRoute.PhaseForSegment(serial);
                        int mask = MetroRoofRoute.LaneMaskForSegment(serial);
                        foreach (CoinPickup coin in item.GetComponentsInChildren<CoinPickup>())
                        {
                            Vector3 pos = coin.transform.position;
                            int lane = Mathf.RoundToInt(pos.x / 2.75f) + 1;
                            Require(phase == MetroRoutePhase.Street || MetroRoofRoute.IsLaneOpen(mask, lane), "A coin leads into a closed lane");
                            Require(pos.y > MetroRoofRoute.SurfaceHeight(phase, pos.z - serial * 30, 30) + 0.4f, "Coin generated below the route");
                        }
                        PooledObstacle[] obstacles = item.GetComponentsInChildren<PooledObstacle>();
                        if (phase == MetroRoutePhase.Rooftops && (mask & 3) != 3 && (mask & 6) != 6)
                        {
                            Require(obstacles.Length == 1, "Isolated roofs need room between required jumps");
                            Require(Mathf.Abs(obstacles[0].transform.localPosition.z - 15f) < 0.01f, "Required jumps must be spaced 30 meters apart");
                        }
                        if (phase == MetroRoutePhase.Ascent || phase == MetroRoutePhase.Descent)
                            Require(obstacles.Length == 0, "Open ramps must stay free of pooled hazards");
                        if (phase == MetroRoutePhase.Street && MetroRoofRoute.PhaseForSegment(serial + 1) == MetroRoutePhase.Ascent)
                            foreach (PooledObstacle obstacle in obstacles)
                                Require(obstacle.transform.localPosition.z < 10, "The final approach must allow a lane change");
                        foreach (PooledObstacle obstacle in obstacles)
                        {
                            if (phase == MetroRoutePhase.Rooftops)
                            {
                                int lane = Mathf.RoundToInt(obstacle.transform.localPosition.x / 2.75f) + 1;
                                Require(MetroRoofRoute.IsLaneOpen(mask, lane), "A vent spawned in a closed lane");
                                Require(Mathf.Abs(obstacle.transform.position.y - MetroRoofRoute.RoofHeight) < 0.001f, "Roof hazard generated at street height");
                                Require(obstacle.GetComponent<BoxCollider>().size.y < 1, "Vehicles must not appear on the roof");
                            }
                        }
                        int surfaces = item.GetComponentsInChildren<TrackSurface>().Length;
                        Require(phase != MetroRoutePhase.Street || surfaces == 1, "Recycled street retained an elevated collider");
                    }
                    int meshes = item.GetComponentsInChildren<MeshFilter>(true).Length;
                    if (cycle == 0) afterWarmup = meshes;
                    else Require(meshes == afterWarmup, "Repeated route cycles allocated more geometry");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(item); }
        }

        private static float ExpectedHeight(float worldZ)
        {
            int serial = Mathf.FloorToInt(worldZ / 30);
            return MetroRoofRoute.SurfaceHeight(MetroRoofRoute.PhaseForSegment(serial), worldZ - serial * 30, 30);
        }

        public static void ValidatePreviewAndBuild()
        {
            ValidateAndPreview();
            InfiniteRunnerProjectSetup.BuildWebGame();
        }

        public static void ValidateAndPreview()
        {
            Validate();
            StreetObstacleValidation.Validate();
            RenderPreview();
            RenderStreetPreview();
        }

        /// <summary>Renders the runner's-eye view of a Saigon street block and the skyline.</summary>
        public static void RenderStreetPreview()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Street previews require graphics. Run this command without -nographics.");
            PrepareScene();
            Scene original = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var palette = new PrototypePalette();
            var root = new GameObject("Street preview");
            RenderTexture target = null;
            Texture2D image = null;
            try
            {
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(0.72f, 0.8f, 0.9f);
                RenderSettings.ambientEquatorColor = new Color(0.78f, 0.72f, 0.64f);
                RenderSettings.ambientGroundColor = new Color(0.5f, 0.43f, 0.36f);
                RenderSettings.skybox = palette.Sky;
                RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogColor = new Color(0.93f, 0.86f, 0.74f);
                RenderSettings.fogStartDistance = 85; RenderSettings.fogEndDistance = 470;
                var sun = new GameObject("Street preview sun").AddComponent<Light>();
                sun.transform.SetParent(root.transform);
                sun.transform.rotation = Quaternion.Euler(42, -38, 0);
                sun.type = LightType.Directional; sun.color = new Color(1, 0.9f, 0.74f);
                sun.intensity = 1.15f; sun.shadows = LightShadows.Soft; sun.shadowStrength = 0.55f;
                for (int block = 0; block < 9; block++)
                {
                    var item = new GameObject("Street block");
                    item.transform.SetParent(root.transform, false);
                    TrackSegment segment = item.AddComponent<TrackSegment>();
                    segment.Initialize(block, 30, 2.75f, null, palette);
                    segment.Place(block * 30, block + 1);
                }
                var camera = new GameObject("Street preview camera").AddComponent<Camera>();
                camera.transform.SetParent(root.transform);
                camera.farClipPlane = 600; camera.nearClipPlane = 0.08f;
                SaigonScenery.CreateSkyline(root.transform, camera.transform, palette);
                camera.fieldOfView = 66; camera.clearFlags = CameraClearFlags.Skybox;
                target = new RenderTexture(1440, 960, 24) { antiAliasing = 4 };
                camera.targetTexture = target;
                image = new Texture2D(1440, 960, TextureFormat.RGB24, false);
                Directory.CreateDirectory("Builds/Previews");
                (Vector3 eye, Vector3 look, string file)[] shots =
                {
                    (new Vector3(0, 5.25f, -4.5f), new Vector3(0, 1.1f, 13f), "saigon-street.png"),
                    (new Vector3(2.5f, 2.2f, 8f), new Vector3(7.5f, 4.2f, 22f), "saigon-shopfronts.png"),
                    (new Vector3(0, 12f, -20f), new Vector3(0, 30f, 200f), "saigon-skyline.png")
                };
                foreach (var shot in shots)
                {
                    camera.transform.position = shot.eye;
                    camera.transform.LookAt(shot.look);
                    camera.Render(); RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, 1440, 960), 0, 0); image.Apply();
                    File.WriteAllBytes("Builds/Previews/" + shot.file, image.EncodeToPNG());
                }
                Debug.Log("Street previews rendered to Builds/Previews/saigon-*.png");
            }
            finally
            {
                RenderTexture.active = null;
                if (image != null) UnityEngine.Object.DestroyImmediate(image);
                if (target != null) UnityEngine.Object.DestroyImmediate(target);
                palette.Dispose(); UnityEngine.Object.DestroyImmediate(root);
                SceneManager.SetActiveScene(original); EditorSceneManager.CloseScene(scene, true);
            }
        }

        public static void RenderPreview()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Metro previews require graphics. Run this command without -nographics.");
            PrepareScene();
            Scene original = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var palette = new PrototypePalette();
            var root = new GameObject("Metro preview");
            RenderTexture target = null;
            Texture2D image = null;
            try
            {
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(0.72f, 0.8f, 0.9f);
                RenderSettings.ambientEquatorColor = new Color(0.78f, 0.72f, 0.64f);
                RenderSettings.ambientGroundColor = new Color(0.5f, 0.43f, 0.36f);
                RenderSettings.skybox = palette.Sky;
                RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogColor = new Color(0.93f, 0.86f, 0.74f);
                RenderSettings.fogStartDistance = 85; RenderSettings.fogEndDistance = 470;
                var sun = new GameObject("Metro preview sun").AddComponent<Light>();
                sun.transform.SetParent(root.transform);
                sun.transform.rotation = Quaternion.Euler(42, -38, 0);
                sun.type = LightType.Directional; sun.color = new Color(1, 0.9f, 0.74f);
                sun.intensity = 1.05f; sun.shadows = LightShadows.Soft; sun.shadowStrength = 0.55f;
                for (int block = 0; block < 4; block++)
                {
                    var item = new GameObject("Metro block");
                    item.transform.SetParent(root.transform, false);
                    TrackSegment segment = item.AddComponent<TrackSegment>();
                    segment.Initialize(block, 30, 2.75f, null, palette);
                    segment.Place(block * 30, block + 4);
                }
                var camera = new GameObject("Metro preview camera").AddComponent<Camera>();
                camera.transform.SetParent(root.transform);
                camera.transform.position = new Vector3(0, 8.0f, -7);
                camera.transform.LookAt(new Vector3(0, 3.0f, 31));
                camera.fieldOfView = 66; camera.clearFlags = CameraClearFlags.Skybox;
                target = new RenderTexture(1440, 960, 24) { antiAliasing = 4 };
                camera.targetTexture = target;
                camera.Render(); RenderTexture.active = target;
                image = new Texture2D(1440, 960, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1440, 960), 0, 0); image.Apply();
                Directory.CreateDirectory("Builds/Previews");
                File.WriteAllBytes("Builds/Previews/metro-rooftop-route.png", image.EncodeToPNG());
                foreach (TrackSegment segment in root.GetComponentsInChildren<TrackSegment>())
                foreach (Transform part in segment.transform)
                {
                    if (!part.name.StartsWith("Metro ") && part.name != "Road" && part.name != "Pooled Coins" && part.name != "Pooled Hazards")
                        part.gameObject.SetActive(false);
                }
                PrototypeFactory.CreatePrimitive("Overview pavement", PrimitiveType.Cube, root.transform,
                    new Vector3(0, -0.45f, 50), new Vector3(220, 0.3f, 220), palette.Road, false);
                camera.transform.position = new Vector3(-12, 10, 6);
                camera.transform.LookAt(new Vector3(0, 2.3f, 31));
                camera.Render();
                image.ReadPixels(new Rect(0, 0, 1440, 960), 0, 0); image.Apply();
                File.WriteAllBytes("Builds/Previews/metro-split-lanes.png", image.EncodeToPNG());
                foreach (TrackSegment segment in root.GetComponentsInChildren<TrackSegment>())
                    segment.Place(segment.transform.position.z, 16 + Mathf.RoundToInt(segment.transform.position.z / 30));
                camera.Render();
                image.ReadPixels(new Rect(0, 0, 1440, 960), 0, 0); image.Apply();
                File.WriteAllBytes("Builds/Previews/metro-single-lane.png", image.EncodeToPNG());
                Debug.Log("Metro preview rendered to Builds/Previews/metro-rooftop-route.png");
            }
            finally
            {
                RenderTexture.active = null;
                if (image != null) UnityEngine.Object.DestroyImmediate(image);
                if (target != null) UnityEngine.Object.DestroyImmediate(target);
                palette.Dispose(); UnityEngine.Object.DestroyImmediate(root);
                SceneManager.SetActiveScene(original); EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void PrepareScene()
        {
            if (Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
                EditorSceneManager.OpenScene("Assets/InfiniteRunner/Scenes/Main.unity", OpenSceneMode.Single);
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Metro route validation: " + message);
        }
    }
}

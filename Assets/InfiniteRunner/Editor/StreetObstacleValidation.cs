using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace InfiniteRunner.Editor
{
    public static class StreetObstacleValidation
    {
        [MenuItem("Infinite Runner/Validate Street Objects", priority = 25)]
        public static void Validate()
        {
            PrepareEditorScene();
            Scene original = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var root = new GameObject("Validation objects");
            SceneManager.MoveGameObjectToScene(root, scene);
            var palette = new PrototypePalette();
            try
            {
                PhysicsScene physics = scene.GetPhysicsScene();
                for (int variant = 0; variant < 3; variant++)
                {
                    var item = new GameObject("Pooled street object");
                    item.transform.SetParent(root.transform, false);
                    PooledObstacle obstacle = item.AddComponent<PooledObstacle>();
                    obstacle.Initialize(palette, variant);
                    foreach (ObstacleKind kind in Enum.GetValues(typeof(ObstacleKind)))
                    {
                        obstacle.Activate(kind, 1, 2.75f, 0);
                        Physics.SyncTransforms();
                        Require(Hits(physics, 0, 0, 2f), kind + " must stop a standing runner");
                        Require(!Hits(physics, -2.75f, 0, 2f) && !Hits(physics, 2.75f, 0, 2f), kind + " must leave both adjacent lanes clear");
                        if (kind == ObstacleKind.Hurdle || kind == ObstacleKind.RoofVent)
                            Require(!Hits(physics, 0, 1.1f, 2f), "A jump must clear the barricade");
                        if (kind == ObstacleKind.OverheadBar)
                        {
                            Require(!Hits(physics, 0, 0, 1f), "A crouched runner must fit under the scaffold");
                            Require(Hits(physics, 1.1f, 0, 1f), "Scaffold supports must be solid");
                        }
                        foreach (Collider collider in item.GetComponentsInChildren<Collider>())
                            Require(!collider.enabled || collider.GetComponent<ObstacleMarker>() != null, "Every active hazard collider must end the run");
                    }
                    // Reusing a slot must disable its former cabin and scaffold colliders/visuals.
                    for (int recycle = 0; recycle < 5; recycle++)
                    {
                        obstacle.Activate(ObstacleKind.OverheadBar, 1, 2.75f, 0);
                        obstacle.Activate(ObstacleKind.Block, 1, 2.75f, 0);
                        obstacle.Activate(ObstacleKind.Hurdle, 1, 2.75f, 0);
                        Physics.SyncTransforms();
                        Require(!Hits(physics, 0, 1.1f, 2f), "Recycled barriers must not retain a vehicle cabin");
                    }
                    int activeVisuals = 0;
                    foreach (Transform child in item.transform)
                        if (child.gameObject.activeSelf && child.GetComponentsInChildren<MeshRenderer>().Length > 0) activeVisuals++;
                    Require(activeVisuals == 1, "A pooled slot must show exactly one prop");
                    item.SetActive(false);
                }
                Debug.Log("STREET OBJECT CHECKS PASSED: all three vehicle variants, safe adjacent lanes, jumping, crouching, scaffold posts, hazard markers, and pooled reuse.");
            }
            finally
            {
                palette.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
                SceneManager.SetActiveScene(original);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        public static void PreviewValidateAndBuild()
        {
            RenderPreview();
            ValidateAndBuild();
        }

        public static void ValidateAndBuild()
        {
            Validate();
            InfiniteRunnerProjectSetup.BuildWebGame();
        }

        private static bool Hits(PhysicsScene physics, float x, float bottom, float height)
        {
            const float radius = 0.42f;
            var hits = new Collider[16];
            return physics.OverlapCapsule(new Vector3(x, bottom + radius, 0), new Vector3(x, bottom + height - radius, 0), radius, hits, ~0, QueryTriggerInteraction.Ignore) > 0;
        }

        private static void PrepareEditorScene()
        {
            if (Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
                EditorSceneManager.OpenScene("Assets/InfiniteRunner/Scenes/Main.unity", OpenSceneMode.Single);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Street object validation: " + message);
        }

        // Renders a contact sheet with the same models used by the live obstacle pool.
        [MenuItem("Infinite Runner/Render Street Object Preview", priority = 26)]
        public static void RenderPreview()
        {
            PrepareEditorScene();
            Scene original = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var palette = new PrototypePalette();
            var root = new GameObject("Street objects preview");
            RenderTexture target = null;
            Texture2D image = null;
            try
            {
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(0.68f, 0.81f, 0.94f);
                RenderSettings.ambientEquatorColor = new Color(0.67f, 0.73f, 0.74f);
                RenderSettings.ambientGroundColor = new Color(0.48f, 0.47f, 0.41f);
                RenderSettings.fog = false;
                var sun = new GameObject("Preview sunlight").AddComponent<Light>();
                sun.transform.SetParent(root.transform);
                sun.transform.rotation = Quaternion.Euler(48, -32, 0);
                sun.type = LightType.Directional;
                sun.color = new Color(1, 0.95f, 0.82f);
                sun.intensity = 1.05f;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 0.55f;
                PrototypeFactory.CreatePrimitive("Pavement", PrimitiveType.Cube, root.transform, new Vector3(0, -0.16f, 0), new Vector3(100, 0.3f, 100), palette.Road, false);
                for (int i = 0; i < 5; i++)
                {
                    ObstacleKind kind = i < 3 ? ObstacleKind.Block : i == 3 ? ObstacleKind.Hurdle : ObstacleKind.OverheadBar;
                    GameObject model = palette.StreetObjects.Create(root.transform, kind, i % 3);
                    model.transform.localPosition = i < 3 ? new Vector3((i - 1) * 3.3f, 0, 1) : new Vector3(i == 3 ? -2 : 2, 0, -3);
                }
                var camera = new GameObject("Preview camera").AddComponent<Camera>();
                camera.transform.SetParent(root.transform);
                camera.transform.position = new Vector3(-8.7f, 8.1f, -14.5f);
                camera.transform.LookAt(new Vector3(0, 0.8f, 0.5f));
                camera.fieldOfView = 45;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.77f, 0.88f, 0.94f);
                target = new RenderTexture(1440, 960, 24) { antiAliasing = 4 };
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image = new Texture2D(1440, 960, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1440, 960), 0, 0);
                image.Apply();
                Directory.CreateDirectory("Builds/Previews");
                File.WriteAllBytes("Builds/Previews/street-objects.png", image.EncodeToPNG());
                Debug.Log("Street object preview rendered to Builds/Previews/street-objects.png");
            }
            finally
            {
                RenderTexture.active = null;
                if (image != null) UnityEngine.Object.DestroyImmediate(image);
                if (target != null) UnityEngine.Object.DestroyImmediate(target);
                palette.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
                SceneManager.SetActiveScene(original);
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}

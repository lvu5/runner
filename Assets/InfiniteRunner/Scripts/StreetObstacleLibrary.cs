using System;
using System.Collections.Generic;
using UnityEngine;

namespace InfiniteRunner
{
    /// <summary>Builds each street prop once. Pooled instances share its meshes and materials.</summary>
    public sealed class StreetObstacleLibrary : IDisposable
    {
        public const float VehicleRearZ = -0.575f;
        public const float VehicleLength = 4.3f;
        public const float VehicleCenterZ = VehicleRearZ + VehicleLength * 0.5f;

        private readonly PrototypePalette palette;
        private readonly GameObject templates;
        private readonly Dictionary<int, GameObject> models = new Dictionary<int, GameObject>();

        public StreetObstacleLibrary(PrototypePalette palette)
        {
            this.palette = palette;
            templates = new GameObject("Shared street object meshes");
            templates.SetActive(false);
        }

        public GameObject Create(Transform parent, ObstacleKind kind, int variant)
        {
            int key = kind == ObstacleKind.Block ? variant % 3 : (int)kind + 2;
            if (!models.TryGetValue(key, out GameObject template))
            {
                template = new GameObject(key < 3 ? new[] { "Saigon taxi", "Fruit mini truck", "Green city minibus" }[key] : key == 3 ? "Sidewalk hotpot table" : key == 4 ? "Tet banner and cables" : "Metro roof air conditioner");
                template.transform.SetParent(templates.transform, false);
                var builder = new SceneryMeshBuilder(template.transform);
                if (key < 3) BuildVehicle(builder, key);
                else if (key == 3) BuildBarricade(builder);
                else if (key == 4) BuildScaffold(builder);
                else BuildRoofVent(builder);
                builder.Finish();
                models.Add(key, template);
            }

            var instance = new GameObject(template.name);
            instance.transform.SetParent(parent, false);
            // Do not clone the mesh owner: only the template owns the shared mesh lifetime.
            foreach (MeshFilter source in template.GetComponentsInChildren<MeshFilter>(true))
            {
                var part = new GameObject(source.name);
                part.transform.SetParent(instance.transform, false);
                part.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
                part.AddComponent<MeshRenderer>().sharedMaterial = source.GetComponent<MeshRenderer>().sharedMaterial;
            }
            if (key == 0)
                Label(instance.transform, "TAXI", new Vector3(0, 1.965f, 1.10f), 0.042f, Color.white);
            if (key == 1)
                Label(instance.transform, "TRAI CAY", new Vector3(0, 0.98f, -0.64f), 0.045f, new Color(1f, 0.92f, 0.3f));
            if (key == 2)
            {
                Label(instance.transform, "XE BUYT", new Vector3(0, 2.08f, -0.56f), 0.05f, new Color(1f, 0.85f, 0.1f));
                Label(instance.transform, "152", new Vector3(0, 1.62f, -0.56f), 0.09f, Color.white);
            }
            if (key == 3)
                Label(instance.transform, "LAU", new Vector3(0, 0.33f, -0.33f), 0.05f, new Color(1f, 0.9f, 0.2f));
            if (key == 4)
                Label(instance.transform, "CHUC MUNG NAM MOI", new Vector3(0, 1.62f, -0.56f), 0.04f, new Color(1f, 0.88f, 0.15f));
            return instance;
        }

        private void BuildVehicle(SceneryMeshBuilder b, int variant)
        {
            PrototypePalette p = palette;
            Material paint = variant == 0 ? p.TaxiPaint : variant == 1 ? p.CarPaint : p.VanPaint;
            float center = VehicleCenterZ;
            b.Box("Chassis", new Vector3(0, 0.36f, center), new Vector3(1.68f, 0.25f, 4.15f), p.Rubber);
            b.TaperedBox(new Vector3(0, 0.40f, center), new Vector2(1.68f, 4.05f), new Vector3(0, 0.94f, center), new Vector2(1.85f, VehicleLength), paint);
            b.TaperedBox(new Vector3(0, 0.94f, center), new Vector2(1.85f, VehicleLength), new Vector3(0, 1.06f, center), new Vector2(1.73f, 4.05f), paint);

            for (int side = -1; side <= 1; side += 2)
            {
                foreach (float wheelZ in new[] { 0.22f, 2.91f })
                {
                    Quaternion axle = Quaternion.Euler(0, 0, 90);
                    b.Cylinder(new Vector3(side * 0.86f, 0.34f, wheelZ), 0.34f, 0.22f, p.Rubber, axle);
                    b.Cylinder(new Vector3(side * 0.977f, 0.34f, wheelZ), 0.215f, 0.025f, p.Chrome, axle);
                    b.Cylinder(new Vector3(side * 0.995f, 0.34f, wheelZ), 0.09f, 0.03f, p.Steel, axle);
                    for (int spoke = 0; spoke < 5; spoke++)
                    {
                        float angle = spoke * Mathf.PI * 2 / 5;
                        b.Beam(new Vector3(side, 0.34f, wheelZ), new Vector3(side, 0.34f + Mathf.Cos(angle) * 0.19f, wheelZ + Mathf.Sin(angle) * 0.19f), 0.035f, p.TowerSilver);
                    }
                }
                b.Box("Lower sill", new Vector3(side * 0.9f, 0.39f, 1.52f), new Vector3(0.08f, 0.12f, 1.65f), paint);
                b.Box("Mirror arm", new Vector3(side * 0.9f, 1.17f, 2.53f), new Vector3(0.2f, 0.06f, 0.07f), p.Rubber);
                b.Box("Side mirror", new Vector3(side * 1.025f, 1.19f, 2.5f), new Vector3(0.14f, 0.16f, 0.27f), paint);
                b.Box("Mirror glass", new Vector3(side * 1.025f, 1.19f, 2.36f), new Vector3(0.12f, 0.13f, 0.015f), p.Chrome);
            }

            if (variant == 2) BuildBusCabin(b, paint);
            else if (variant == 1) BuildTruckBed(b, paint);
            else BuildCarCabin(b, paint, true);

            foreach (float endZ in new[] { VehicleRearZ - 0.025f, VehicleRearZ + VehicleLength + 0.025f })
            {
                bool rear = endZ < 0;
                b.Box("Bumper", new Vector3(0, 0.48f, endZ), new Vector3(1.75f, 0.18f, 0.09f), p.Rubber);
                b.Box("Bumper trim", new Vector3(0, 0.62f, endZ), new Vector3(1.64f, 0.06f, 0.045f), p.Chrome);
                b.Box("Vietnamese plate", new Vector3(0, 0.75f, endZ + (rear ? -0.03f : 0.03f)), new Vector3(0.47f, 0.13f, 0.02f), p.LaneLine);
                b.Box("Plate border", new Vector3(0, 0.75f, endZ + (rear ? -0.038f : 0.038f)), new Vector3(0.42f, 0.02f, 0.01f), p.Rubber);
                for (int side = -1; side <= 1; side += 2)
                {
                    b.Box("Light housing", new Vector3(side * 0.64f, 0.88f, endZ), new Vector3(0.40f, 0.21f, 0.06f), p.Rubber);
                    b.Box(rear ? "Tail lamp" : "Headlamp", new Vector3(side * 0.64f, 0.89f, endZ + (rear ? -0.04f : 0.04f)), new Vector3(0.34f, 0.16f, 0.025f), rear ? p.Taillight : p.Headlight);
                    b.Box("Indicator", new Vector3(side * 0.79f, 0.89f, endZ + (rear ? -0.055f : 0.055f)), new Vector3(0.055f, 0.13f, 0.02f), p.JumpObstacle);
                }
                if (!rear)
                {
                    b.Box("Grille", new Vector3(0, 0.91f, endZ), new Vector3(0.69f, 0.16f, 0.05f), p.Rubber);
                    for (int bar = 0; bar < 3; bar++)
                        b.Box("Grille trim", new Vector3(0, 0.86f + bar * 0.05f, endZ + 0.03f), new Vector3(0.62f, 0.017f, 0.015f), p.Chrome);
                }
            }
        }

        private void BuildCarCabin(SceneryMeshBuilder b, Material paint, bool taxi)
        {
            PrototypePalette p = palette;
            b.TaperedBox(new Vector3(0, 1.055f, 1.45f), new Vector2(1.60f, 2.8f), new Vector3(0, 1.76f, 1.35f), new Vector2(1.34f, 1.65f), p.VehicleGlass);
            b.Box("Roof", new Vector3(0, 1.78f, 1.35f), new Vector3(1.4f, 0.10f, 1.75f), paint);
            for (int side = -1; side <= 1; side += 2)
            {
                b.Beam(new Vector3(side * 0.8f, 1.07f, 0.05f), new Vector3(side * 0.67f, 1.76f, 0.525f), 0.085f, paint);
                b.Beam(new Vector3(side * 0.8f, 1.07f, 2.85f), new Vector3(side * 0.67f, 1.76f, 2.175f), 0.085f, paint);
                b.Beam(new Vector3(side * 0.8f, 1.07f, 1.35f), new Vector3(side * 0.67f, 1.76f, 1.35f), 0.07f, p.Rubber);
                b.Box("Door seam", new Vector3(side * 0.93f, 0.77f, 1.34f), new Vector3(0.008f, 0.40f, 0.018f), p.VehicleGlass);
                foreach (float doorZ in new[] { 0.8f, 2.1f })
                    b.Box("Door handle", new Vector3(side * 0.93f, 0.94f, doorZ), new Vector3(0.035f, 0.045f, 0.22f), p.Chrome);
                if (taxi)
                {
                    b.Box("Taxi green stripe", new Vector3(side * 0.935f, 0.8f, 1.55f), new Vector3(0.025f, 0.14f, 3.9f), p.ShopGreen);
                    b.Box("Taxi red pinstripe", new Vector3(side * 0.94f, 0.7f, 1.55f), new Vector3(0.02f, 0.04f, 3.9f), p.ShopRed);
                }
            }
            b.Box("Rear glass reflection", new Vector3(-0.2f, 1.48f, 0.313f), new Vector3(0.68f, 0.045f, 0.025f), p.MetroSky, Quaternion.Euler(-35, 0, 0));
            if (taxi)
            {
                b.Box("Taxi sign base", new Vector3(0, 1.85f, 1.28f), new Vector3(0.72f, 0.05f, 0.4f), p.Rubber);
                b.TaperedBox(new Vector3(0, 1.88f, 1.28f), new Vector2(0.67f, 0.34f), new Vector3(0, 2.05f, 1.28f), new Vector2(0.59f, 0.26f), p.ShopGreen);
            }
        }

        private void BuildBusCabin(SceneryMeshBuilder b, Material paint)
        {
            PrototypePalette p = palette;
            b.TaperedBox(new Vector3(0, 1.02f, 1.55f), new Vector2(1.78f, 4.2f), new Vector3(0, 2.3f, 1.5f), new Vector2(1.66f, 4.0f), paint);
            b.Box("Bus roof", new Vector3(0, 2.33f, 1.5f), new Vector3(1.6f, 0.06f, 3.9f), p.FacadeTrim);
            b.Box("Rear destination sign", new Vector3(0, 2.08f, -0.5f), new Vector3(1.1f, 0.24f, 0.04f), p.Rubber);
            b.Box("Rear window", new Vector3(0, 1.62f, -0.5f), new Vector3(1.3f, 0.5f, 0.03f), p.VehicleGlass);
            b.Box("Windshield", new Vector3(0, 1.72f, 3.62f), new Vector3(1.5f, 0.85f, 0.06f), p.VehicleGlass, Quaternion.Euler(-10, 0, 0));
            for (int side = -1; side <= 1; side += 2)
            {
                b.Box("Bus window band", new Vector3(side * 0.86f, 1.72f, 1.5f), new Vector3(0.03f, 0.62f, 3.7f), p.VehicleGlass);
                for (int post = 0; post < 5; post++)
                    b.Box("Window post", new Vector3(side * 0.875f, 1.72f, -0.15f + post * 0.85f), new Vector3(0.025f, 0.62f, 0.07f), paint);
                b.Box("Bus yellow stripe", new Vector3(side * 0.885f, 1.28f, 1.55f), new Vector3(0.02f, 0.12f, 4.1f), p.ShopYellow);
                b.Box("Bus door", new Vector3(side * 0.885f, 1.2f, 2.8f), new Vector3(0.02f, 1.6f, 0.7f), p.VehicleGlass);
                b.Box("Van rear lamp", new Vector3(side * 0.74f, 1.27f, -0.53f), new Vector3(0.12f, 0.36f, 0.05f), p.Taillight);
            }
        }

        private void BuildTruckBed(SceneryMeshBuilder b, Material paint)
        {
            PrototypePalette p = palette;
            // Open cargo bed facing the runner, piled with fruit crates.
            b.Box("Bed floor", new Vector3(0, 0.66f, 0.85f), new Vector3(1.78f, 0.1f, 2.85f), p.Steel);
            b.Box("Tailgate", new Vector3(0, 0.9f, -0.56f), new Vector3(1.78f, 0.5f, 0.05f), paint);
            for (int side = -1; side <= 1; side += 2)
                b.Box("Bed side board", new Vector3(side * 0.87f, 0.9f, 0.85f), new Vector3(0.05f, 0.5f, 2.85f), paint);
            Material[] fruit = { p.DragonFruit, p.Mango, p.Melon };
            for (int x = -1; x <= 1; x += 2)
            for (int z = 0; z < 3; z++)
            for (int level = 0; level < 2; level++)
            {
                if (level == 1 && z == 2) continue;
                Vector3 crate = new Vector3(x * 0.38f, 0.92f + level * 0.42f, 0.2f + z * 0.68f);
                b.Box("Plastic crate", crate, new Vector3(0.68f, 0.4f, 0.62f), (x + z + level) % 2 == 0 ? p.PlasticBlue : p.PlasticRed);
                Material produce = fruit[(z + level + (x + 1)) % fruit.Length];
                for (int item = 0; item < 4; item++)
                    b.Ellipsoid(crate + new Vector3((item % 2 - 0.5f) * 0.3f, 0.21f, (item / 2 - 0.5f) * 0.27f), new Vector3(0.13f, 0.1f, 0.12f), produce);
            }
            // Low, rounded mini-truck cab at the far end.
            b.TaperedBox(new Vector3(0, 0.62f, 3.03f), new Vector2(1.78f, 1.35f), new Vector3(0, 1.12f, 3.03f), new Vector2(1.8f, 1.35f), paint);
            b.TaperedBox(new Vector3(0, 1.12f, 2.98f), new Vector2(1.7f, 1.2f), new Vector3(0, 1.72f, 2.86f), new Vector2(1.5f, 0.85f), p.VehicleGlass);
            b.Box("Cab roof", new Vector3(0, 1.74f, 2.86f), new Vector3(1.52f, 0.06f, 0.88f), paint);
            b.Box("Cab back wall", new Vector3(0, 1.2f, 2.33f), new Vector3(1.7f, 1.1f, 0.06f), paint);
        }

        private void BuildBarricade(SceneryMeshBuilder b)
        {
            // A sidewalk hotpot setup spilling into the road: jump over it.
            PrototypePalette p = palette;
            b.Box("Folding table top", new Vector3(0, 0.56f, 0), new Vector3(1.25f, 0.04f, 0.62f), p.Chrome);
            b.Box("Table cloth", new Vector3(0, 0.36f, -0.3f), new Vector3(1.2f, 0.36f, 0.02f), p.ShopRed);
            foreach (float x in new[] { -0.55f, 0.55f })
            foreach (float z in new[] { -0.25f, 0.25f })
                b.Box("Table leg", new Vector3(x, 0.27f, z), new Vector3(0.04f, 0.54f, 0.04f), p.Steel);
            b.Cylinder(new Vector3(0, 0.62f, 0), 0.2f, 0.07f, p.Steel);
            b.Cylinder(new Vector3(0, 0.71f, 0), 0.24f, 0.13f, p.Chrome);
            b.Cylinder(new Vector3(0, 0.78f, 0), 0.22f, 0.02f, p.Ochre);
            foreach (float x in new[] { -0.4f, 0.4f })
                b.Cylinder(new Vector3(x, 0.61f, -0.12f), 0.08f, 0.06f, p.FacadeTrim);
            b.Box("Beer crate", new Vector3(0.25f, 0.15f, 0.1f), new Vector3(0.42f, 0.3f, 0.32f), p.ShopYellow);
            // Stacked plastic stools at both ends reach the full hurdle height.
            for (int end = -1; end <= 1; end += 2)
            {
                Material color = end < 0 ? p.PlasticRed : p.PlasticBlue;
                for (int stool = 0; stool < 4; stool++)
                    SaigonScenery.PlasticStool(b, new Vector3(end * 0.83f, stool * 0.1f, 0), color);
            }
        }

        private void BuildScaffold(SceneryMeshBuilder b)
        {
            // Bamboo poles holding a low Tet banner, drooping cables and lanterns: crouch under it.
            PrototypePalette p = palette;
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 1.10f;
                foreach (float z in new[] { -0.44f, 0.44f })
                {
                    b.Cylinder(new Vector3(x, 0.97f, z), 0.06f, 1.94f, p.Bamboo);
                    for (int node = 1; node < 4; node++)
                        b.Cylinder(new Vector3(x, node * 0.5f, z), 0.068f, 0.03f, p.Bark);
                    b.Box("Pole base block", new Vector3(x, 0.08f, z), new Vector3(0.26f, 0.16f, 0.26f), p.Concrete);
                }
                b.Beam(new Vector3(x, 0.35f, -0.44f), new Vector3(x, 1.5f, 0.44f), 0.04f, p.Bamboo);
                b.Beam(new Vector3(x, 0.35f, 0.44f), new Vector3(x, 1.5f, -0.44f), 0.04f, p.Bamboo);
                // Lanterns hang inside the overhead envelope only.
                b.Box("Lantern cord", new Vector3(side * 0.82f, 1.83f, -0.3f), new Vector3(0.015f, 0.12f, 0.015f), p.Cable);
                b.Ellipsoid(new Vector3(side * 0.82f, 1.66f, -0.3f), new Vector3(0.18f, 0.2f, 0.18f), p.Lantern);
            }
            foreach (float z in new[] { -0.44f, 0.44f })
                b.Cylinder(new Vector3(0, 1.88f, z), 0.06f, 2.3f, p.Bamboo, Quaternion.Euler(0, 0, 90));
            b.Box("Tet banner", new Vector3(0, 1.62f, -0.5f), new Vector3(2.08f, 0.36f, 0.03f), p.FlagRed);
            b.Box("Banner gold edge", new Vector3(0, 1.81f, -0.52f), new Vector3(2.08f, 0.03f, 0.02f), p.FlagYellow);
            b.Box("Banner gold edge", new Vector3(0, 1.43f, -0.52f), new Vector3(2.08f, 0.03f, 0.02f), p.FlagYellow);
            // A tangle of sagging cables draped over the bamboo frame.
            for (int cable = 0; cable < 6; cable++)
            {
                float z = -0.3f + cable * 0.12f;
                b.Catenary(new Vector3(-1.1f, 1.92f, z), new Vector3(1.1f, 1.9f - cable % 2 * 0.03f, z + 0.05f), 0.2f + cable % 3 * 0.08f, 0.03f, p.Cable);
            }
        }

        private void BuildRoofVent(SceneryMeshBuilder b)
        {
            PrototypePalette p = palette;
            b.Box("Roof unit mounting feet", new Vector3(0, 0.08f, 0), new Vector3(2.05f, 0.16f, 0.7f), p.Rubber);
            b.Box("AC housing", new Vector3(0, 0.44f, 0), new Vector3(1.94f, 0.62f, 0.64f), p.MetroWhite);
            b.Box("AC cap", new Vector3(0, 0.805f, 0), new Vector3(2.02f, 0.11f, 0.68f), p.TowerSilver);
            b.Box("AC blue band", new Vector3(0, 0.6f, -0.325f), new Vector3(1.9f, 0.1f, 0.02f), p.MetroBlue);
            b.Box("Louver opening", new Vector3(0, 0.36f, -0.328f), new Vector3(1.72f, 0.3f, 0.018f), p.Rubber);
            for (int slat = 0; slat < 4; slat++)
                b.Box("Louver", new Vector3(0, 0.25f + slat * 0.075f, -0.345f), new Vector3(1.72f, 0.026f, 0.028f), p.Chrome);
            foreach (float x in new[] { -0.51f, 0.51f })
            {
                b.Cylinder(new Vector3(x, 0.87f, 0), 0.245f, 0.018f, p.Steel);
                for (int blade = 0; blade < 4; blade++)
                    b.Box("Fan grille", new Vector3(x, 0.885f, 0), new Vector3(0.46f, 0.012f, 0.024f), p.TowerSilver, Quaternion.Euler(0, blade * 45, 0));
            }
            b.Box("Warning edge", new Vector3(0, 0.77f, -0.35f), new Vector3(1.96f, 0.09f, 0.025f), p.JumpObstacle);
        }

        private static void Label(Transform parent, string content, Vector3 position, float size, Color color)
        {
            var item = new GameObject(content);
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            var text = item.AddComponent<TextMesh>();
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 48;
            text.characterSize = size;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = color;
            text.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
        }

        public void Dispose()
        {
            if (templates != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(templates);
                else UnityEngine.Object.DestroyImmediate(templates);
            }
            models.Clear();
        }
    }
}

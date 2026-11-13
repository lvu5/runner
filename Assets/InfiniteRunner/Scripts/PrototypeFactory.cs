using System;
using System.Collections.Generic;
using UnityEngine;

namespace InfiniteRunner
{
    public sealed class PrototypePalette : IDisposable
    {
        private readonly List<Material> materials = new List<Material>();
        private StreetObstacleLibrary streetObjects;

        public StreetObstacleLibrary StreetObjects => streetObjects ?? (streetObjects = new StreetObstacleLibrary(this));

        public Material Road { get; }
        public Material RoadEdge { get; }
        public Material LaneLine { get; }
        public Material BlockObstacle { get; }
        public Material JumpObstacle { get; }
        public Material SlideObstacle { get; }
        public Material Coin { get; }
        public Material PlayerBody { get; }
        public Material PlayerAccent { get; }
        public Material PlayerDark { get; }
        public Material Skin { get; }
        public Material Straw { get; }
        public Material Window { get; }
        public Material Steel { get; }
        public Material Concrete { get; }
        public Material Terracotta { get; }
        public Material TileSeam { get; }
        public Material Foliage { get; }
        public Material LeafLight { get; }
        public Material Bark { get; }
        public Material Cable { get; }
        // Pastel tube-house facades.
        public Material[] Facades { get; }
        public Material FacadeTrim { get; }
        public Material TinRoof { get; }
        public Material WaterTank { get; }
        public Material ShopRed { get; }
        public Material ShopYellow { get; }
        public Material ShopBlue { get; }
        public Material ShopGreen { get; }
        public Material Lantern { get; }
        public Material FlagRed { get; }
        public Material FlagYellow { get; }
        public Material StreetSignBlue { get; }
        public Material Bamboo { get; }
        public Material PlasticRed { get; }
        public Material PlasticBlue { get; }
        public Material MetroWhite { get; }
        public Material MetroBlue { get; }
        public Material MetroSky { get; }
        public Material Brick { get; }
        public Material TowerGlass { get; }
        public Material TowerSilver { get; }
        public Material Ochre { get; }
        public Material DragonFruit { get; }
        public Material Mango { get; }
        public Material Melon { get; }
        public Material Cloud { get; }
        public Material Sky { get; }
        public Material TaxiPaint { get; }
        public Material CarPaint { get; }
        public Material VanPaint { get; }
        public Material VehicleGlass { get; }
        public Material Rubber { get; }
        public Material Chrome { get; }
        public Material Headlight { get; }
        public Material Taillight { get; }

        public PrototypePalette()
        {
            Road = Create(new Color(0.36f, 0.36f, 0.37f), 0.12f);
            RoadEdge = Create(new Color(0.78f, 0.74f, 0.66f), 0.1f);
            LaneLine = Create(new Color(0.98f, 0.97f, 0.92f), 0.15f);
            BlockObstacle = Create(new Color(0.94f, 0.22f, 0.25f), 0.3f);
            JumpObstacle = Create(new Color(1f, 0.55f, 0.08f), 0.25f);
            SlideObstacle = Create(new Color(0.83f, 0.1f, 0.12f), 0.3f);
            Coin = Create(new Color(1f, 0.78f, 0.1f), 0.6f, true);
            PlayerBody = Create(new Color(0.1f, 0.62f, 0.58f), 0.3f);
            PlayerAccent = Create(new Color(0.97f, 0.42f, 0.22f), 0.3f);
            PlayerDark = Create(new Color(0.12f, 0.16f, 0.24f), 0.15f);
            Skin = Create(new Color(0.86f, 0.64f, 0.47f), 0.2f);
            Straw = Create(new Color(0.93f, 0.84f, 0.6f), 0.1f);
            Window = Create(new Color(0.22f, 0.36f, 0.42f), 0.65f);
            Steel = Create(new Color(0.3f, 0.32f, 0.33f), 0.3f);
            Concrete = Create(new Color(0.7f, 0.69f, 0.66f), 0.08f);
            Terracotta = Create(new Color(0.75f, 0.42f, 0.31f), 0.08f);
            TileSeam = Create(new Color(0.58f, 0.32f, 0.25f), 0.05f);
            Foliage = Create(new Color(0.2f, 0.5f, 0.22f), 0.1f);
            LeafLight = Create(new Color(0.45f, 0.7f, 0.25f), 0.1f);
            Bark = Create(new Color(0.42f, 0.32f, 0.23f), 0.1f);
            Cable = Create(new Color(0.06f, 0.06f, 0.07f), 0.2f);
            Facades = new[]
            {
                Create(new Color(0.98f, 0.82f, 0.38f), 0.12f), // ochre yellow
                Create(new Color(0.62f, 0.86f, 0.76f), 0.12f), // mint
                Create(new Color(0.97f, 0.66f, 0.69f), 0.12f), // pink
                Create(new Color(0.56f, 0.76f, 0.93f), 0.12f), // sky blue
                Create(new Color(0.99f, 0.93f, 0.8f), 0.12f),  // cream
                Create(new Color(0.78f, 0.9f, 0.45f), 0.12f),  // lime
                Create(new Color(0.99f, 0.72f, 0.5f), 0.12f)   // peach
            };
            FacadeTrim = Create(new Color(0.97f, 0.96f, 0.93f), 0.15f);
            TinRoof = Create(new Color(0.55f, 0.2f, 0.17f), 0.25f);
            WaterTank = Create(new Color(0.84f, 0.86f, 0.88f), 0.85f);
            ShopRed = Create(new Color(0.86f, 0.12f, 0.14f), 0.35f, true);
            ShopYellow = Create(new Color(1f, 0.83f, 0.12f), 0.35f, true);
            ShopBlue = Create(new Color(0.1f, 0.38f, 0.78f), 0.35f, true);
            ShopGreen = Create(new Color(0.05f, 0.58f, 0.36f), 0.35f, true);
            Lantern = Create(new Color(0.95f, 0.18f, 0.12f), 0.4f, true);
            FlagRed = Create(new Color(0.85f, 0.1f, 0.12f), 0.2f);
            FlagYellow = Create(new Color(1f, 0.87f, 0.05f), 0.2f, true);
            StreetSignBlue = Create(new Color(0.08f, 0.3f, 0.66f), 0.2f);
            Bamboo = Create(new Color(0.78f, 0.69f, 0.4f), 0.2f);
            PlasticRed = Create(new Color(0.9f, 0.16f, 0.16f), 0.45f);
            PlasticBlue = Create(new Color(0.12f, 0.45f, 0.86f), 0.45f);
            MetroWhite = Create(new Color(0.94f, 0.95f, 0.96f), 0.55f);
            MetroBlue = Create(new Color(0.05f, 0.25f, 0.6f), 0.5f);
            MetroSky = Create(new Color(0.2f, 0.66f, 0.9f), 0.5f);
            Brick = Create(new Color(0.72f, 0.33f, 0.24f), 0.08f);
            TowerGlass = Create(new Color(0.42f, 0.58f, 0.68f), 0.7f);
            TowerSilver = Create(new Color(0.8f, 0.84f, 0.86f), 0.6f);
            Ochre = Create(new Color(0.93f, 0.74f, 0.36f), 0.1f);
            DragonFruit = Create(new Color(0.9f, 0.2f, 0.5f), 0.3f);
            Mango = Create(new Color(1f, 0.74f, 0.15f), 0.3f);
            Melon = Create(new Color(0.18f, 0.48f, 0.2f), 0.3f);
            Cloud = Create(new Color(1f, 0.98f, 0.94f), 0f);
            Cloud.SetFloat("_Unlit", 1f);
            TaxiPaint = Create(new Color(0.96f, 0.97f, 0.96f), 0.75f);
            CarPaint = Create(new Color(0.12f, 0.38f, 0.72f), 0.75f);
            VanPaint = Create(new Color(0.08f, 0.55f, 0.32f), 0.6f);
            VehicleGlass = Create(new Color(0.07f, 0.16f, 0.21f), 0.9f);
            Rubber = Create(new Color(0.045f, 0.055f, 0.065f), 0.05f);
            Chrome = Create(new Color(0.71f, 0.76f, 0.79f), 0.85f);
            Headlight = Create(new Color(1f, 0.95f, 0.73f), 0.65f, true);
            Taillight = Create(new Color(0.95f, 0.065f, 0.04f), 0.6f, true);
            Sky = new Material(Resources.Load<Shader>("SaigonSky")) { name = "Saigon afternoon sky" };
            materials.Add(Sky);
        }

        private Material Create(Color color, float smoothness, bool emission = false)
        {
            Shader shader = Resources.Load<Shader>("PrototypeLit");
            if (shader == null)
            {
                shader = Shader.Find("InfiniteRunner/PrototypeLit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new Material(shader)
            {
                name = $"Prototype {color}",
                color = color,
                enableInstancing = true
            };

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }

            if (emission && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 0.18f);
            }

            materials.Add(material);
            return material;
        }

        public void Dispose()
        {
            streetObjects?.Dispose();
            streetObjects = null;
            foreach (Material material in materials)
            {
                if (material != null)
                {
                    if (Application.isPlaying) UnityEngine.Object.Destroy(material);
                    else UnityEngine.Object.DestroyImmediate(material);
                }
            }

            materials.Clear();
        }
    }

    public static class PrototypeFactory
    {
        public static GameObject CreatePrimitive(
            string objectName,
            PrimitiveType primitiveType,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            bool keepCollider,
            Quaternion? localRotation = null)
        {
            GameObject instance = GameObject.CreatePrimitive(primitiveType);
            instance.name = objectName;
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = localRotation ?? Quaternion.identity;
            instance.transform.localScale = localScale;

            Renderer renderer = instance.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }

            if (!keepCollider)
            {
                Collider collider = instance.GetComponent<Collider>();
                if (collider != null)
                {
                    collider.enabled = false;
                    if (Application.isPlaying) UnityEngine.Object.Destroy(collider);
                    else UnityEngine.Object.DestroyImmediate(collider);
                }
            }

            return instance;
        }
    }

}

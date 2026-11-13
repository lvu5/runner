using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace InfiniteRunner
{
    /// <summary>
    /// Reusable, collider-free Saigon streets: narrow pastel tube houses with
    /// balconies and shop signs, tangled power lines, parked scooters, street
    /// food carts, and a distant District 1 skyline.
    /// </summary>
    public static class SaigonScenery
    {
        private const float HouseFrontX = 8.6f;
        private const float HouseDepth = 8f;

        private static readonly string[] StreetNames =
        {
            "DONG KHOI", "LE LOI", "NGUYEN HUE", "PASTEUR", "HAI BA TRUNG",
            "LE THANH TON", "TON DUC THANG", "NAM KY KHOI NGHIA", "BUI VIEN", "PHAM NGU LAO"
        };

        private static readonly string[] ShopNames =
        {
            "PHO BO", "BANH MI", "CA PHE", "COM TAM", "TAP HOA", "HU TIEU",
            "BUN BO HUE", "SUA XE", "NUOC MIA", "BANH XEO", "TRA SUA", "OC", "LAU DE", "HOTEL"
        };

        private static readonly string[] VerticalSigns = { "CAFE", "PHO", "KHACH SAN", "TIEM VANG", "BIA", "SPA" };

        public static void BuildStreet(Transform parent, int block, float length, float laneWidth, PrototypePalette p)
        {
            var b = new SceneryMeshBuilder(parent);
            var random = new System.Random(1709 + block * 97);

            b.Box("Ground", new Vector3(0, -0.38f, length / 2), new Vector3(65, 0.25f, length), p.RoadEdge);
            for (int side = -1; side <= 1; side += 2)
            {
                // Terracotta tile sidewalk with a concrete curb.
                b.Box("Tiled sidewalk", new Vector3(side * 6.7f, -0.02f, length / 2), new Vector3(3.8f, 0.16f, length), p.Terracotta);
                for (float z = 0.75f; z < length; z += 1.5f)
                    b.Box("Tile seam", new Vector3(side * 6.7f, 0.062f, z), new Vector3(3.7f, 0.006f, 0.04f), p.TileSeam);
                for (int seam = -1; seam <= 1; seam++)
                    b.Box("Tile seam", new Vector3(side * 6.7f + seam * 1.2f, 0.062f, length / 2), new Vector3(0.04f, 0.006f, length), p.TileSeam);
                b.Box("Curb", new Vector3(side * 4.85f, 0.08f, length / 2), new Vector3(0.2f, 0.22f, length), p.Concrete);
                for (int dash = 0; dash < 6; dash++)
                    b.Box("Painted lane marking", new Vector3(side * laneWidth / 2, 0.015f, 2 + dash * 5), new Vector3(0.11f, 0.025f, 2.4f), p.LaneLine);

                BuildHouses(parent, b, side, length, random, p);

                foreach (float poleZ in new[] { 4f, 19f })
                    UtilityPole(b, side * 5.35f, poleZ, side, p);
                // Sagging bundles run pole to pole, continuing into the next block.
                Wires(b, side * 5.35f, 4f, 19f, random, p);
                Wires(b, side * 5.35f, 19f, 34f, random, p);
                for (int drop = 0; drop < 2; drop++)
                {
                    float z = drop == 0 ? 4f : 19f;
                    b.Catenary(new Vector3(side * 5.35f, 7.6f, z), new Vector3(side * HouseFrontX, 6.2f, z + 2.5f + drop), 0.35f, 0.035f, p.Cable);
                }

                Tree(b, side * 6.95f, 11.5f, p);
                Tree(b, side * 6.95f, 26.5f, p);
                ScooterRow(b, side, 7f, 3 + random.Next(3), random, p);
                ScooterRow(b, side, 21.5f, 2 + random.Next(3), random, p);
                if ((block + side) % 2 == 0) FoodCart(parent, b, side * 7.4f, 15f, side, p);
                else PlasticStools(b, side * 7.3f, 15f, random, p);
                StreetSign(parent, b, side * 5.45f, 25f, StreetNames[(block * 2 + (side + 1) / 2) % StreetNames.Length], p);
            }

            // Occasional apartment blocks rise behind the tube houses for depth.
            if (block % 3 == 1)
                Apartment(b, -1 * 22f, length * 0.5f, 26 + random.Next(10), p);
            if (block % 4 == 2)
                Apartment(b, 23f, length * 0.35f, 22 + random.Next(12), p);
            b.Finish();
        }

        private static void BuildHouses(Transform parent, SceneryMeshBuilder b, int side, float length, System.Random random, PrototypePalette p)
        {
            float z = 0f;
            while (z < length - 0.5f)
            {
                float width = Mathf.Min(length - z, 3.6f + (float)random.NextDouble() * 2.2f);
                if (length - z - width < 2.5f) width = length - z;
                TubeHouse(parent, b, side, z + width / 2, width, random, p);
                z += width;
            }
        }

        private static void TubeHouse(Transform parent, SceneryMeshBuilder b, int side, float zc, float width, System.Random random, PrototypePalette p)
        {
            int floors = 3 + random.Next(4);
            const float groundHeight = 3.6f;
            const float floorHeight = 3.1f;
            float height = groundHeight + (floors - 1) * floorHeight;
            float setback = (float)random.NextDouble() * 0.35f;
            float faceX = side * (HouseFrontX + setback);
            float inward = -side; // Direction from the facade toward the road.
            Material facade = p.Facades[random.Next(p.Facades.Length)];
            float w = width - 0.08f;

            b.Box("Tube house", new Vector3(side * (HouseFrontX + setback + HouseDepth / 2), height / 2, zc), new Vector3(HouseDepth, height, w), facade);
            b.Box("Party wall trim", new Vector3(faceX + inward * 0.06f, height / 2, zc - w / 2 + 0.07f), new Vector3(0.14f, height, 0.14f), p.FacadeTrim);
            b.Box("Party wall trim", new Vector3(faceX + inward * 0.06f, height / 2, zc + w / 2 - 0.07f), new Vector3(0.14f, height, 0.14f), p.FacadeTrim);

            // Ground floor: open shopfront, goods, striped awning and a lit signboard.
            b.Box("Shop interior", new Vector3(faceX + inward * 0.02f, 1.45f, zc), new Vector3(0.05f, 2.7f, w - 0.5f), p.Window);
            b.Box("Roller shutter box", new Vector3(faceX + inward * 0.1f, 2.95f, zc), new Vector3(0.2f, 0.3f, w - 0.4f), p.Steel);
            // Goods spill onto the sidewalk: crates, sacks and a low display table.
            Material[] goodsColors = { p.ShopYellow, p.PlasticBlue, p.ShopRed, p.Mango, p.Melon, p.FacadeTrim };
            b.Box("Display table", new Vector3(faceX + inward * 0.55f, 0.42f, zc - w * 0.2f), new Vector3(0.7f, 0.06f, w * 0.4f), p.Steel);
            for (int item = 0; item < 4; item++)
            {
                float itemZ = zc - w * 0.35f + item * w * 0.1f;
                b.Box("Displayed goods", new Vector3(faceX + inward * 0.55f, 0.54f, itemZ), new Vector3(0.28f, 0.18f, 0.22f), goodsColors[random.Next(goodsColors.Length)]);
            }
            for (int crate = 0; crate < 2 + random.Next(3); crate++)
            {
                float crateZ = zc + w * 0.12f + crate * 0.45f;
                if (crateZ > zc + w / 2 - 0.3f) break;
                b.Box("Sidewalk crate", new Vector3(faceX + inward * 0.4f, 0.2f, crateZ), new Vector3(0.42f, 0.36f, 0.4f), goodsColors[random.Next(3)]);
                if (random.Next(2) == 0)
                    b.Box("Stacked crate", new Vector3(faceX + inward * 0.4f, 0.56f, crateZ), new Vector3(0.42f, 0.36f, 0.4f), goodsColors[random.Next(3)]);
            }
            // Snacks and goods hanging along the top of the shopfront.
            for (float hang = zc - w / 2 + 0.45f; hang < zc + w / 2 - 0.3f; hang += 0.35f)
                b.Box("Hanging goods", new Vector3(faceX + inward * 0.08f, 2.45f, hang), new Vector3(0.06f, 0.4f, 0.24f), goodsColors[random.Next(goodsColors.Length)]);

            Material[] shopColors = { p.ShopRed, p.ShopBlue, p.ShopGreen, p.ShopYellow };
            Material awningA = shopColors[random.Next(shopColors.Length)];
            Quaternion awningTilt = Quaternion.Euler(0, 0, side * 16f);
            int stripes = Mathf.Max(3, Mathf.RoundToInt(w / 0.45f));
            for (int stripe = 0; stripe < stripes; stripe++)
            {
                float stripeZ = zc - w / 2 + (stripe + 0.5f) * (w / stripes);
                b.Box("Awning stripe", new Vector3(faceX + inward * 0.75f, 3.12f, stripeZ), new Vector3(1.55f, 0.06f, w / stripes), stripe % 2 == 0 ? awningA : p.FacadeTrim, awningTilt);
            }

            Material sign = shopColors[random.Next(shopColors.Length)];
            b.Box("Shop signboard", new Vector3(faceX + inward * 0.09f, 4.0f, zc), new Vector3(0.12f, 0.85f, w - 0.5f), sign);
            Color text = sign == p.ShopYellow ? new Color(0.75f, 0.08f, 0.08f) : sign == p.ShopRed ? new Color(1f, 0.9f, 0.2f) : Color.white;
            Label(parent, ShopNames[random.Next(ShopNames.Length)], new Vector3(faceX + inward * 0.16f, 4.0f, zc), 0.07f, text, side * 90f);

            // Upper floors: balconies with railings, potted plants and green shutters.
            for (int floor = 1; floor < floors; floor++)
            {
                float y = groundHeight + (floor - 1) * floorHeight;
                b.Box("Floor band", new Vector3(faceX + inward * 0.04f, y, zc), new Vector3(0.1f, 0.22f, w), p.FacadeTrim);
                b.Box("Balcony door", new Vector3(faceX + inward * 0.03f, y + 1.35f, zc), new Vector3(0.05f, 2.1f, w * 0.55f), p.Window);
                for (int shutter = -1; shutter <= 1; shutter += 2)
                    b.Box("Green shutter", new Vector3(faceX + inward * 0.05f, y + 1.35f, zc + shutter * (w * 0.275f + 0.22f)), new Vector3(0.05f, 2.1f, 0.42f), p.ShopGreen);

                bool balcony = (floor + random.Next(3)) % 3 != 0;
                if (!balcony) continue;
                float depth = 0.9f;
                b.Box("Balcony slab", new Vector3(faceX + inward * depth / 2, y + 0.08f, zc), new Vector3(depth, 0.16f, w - 0.3f), p.Concrete);
                b.Box("Balcony rail", new Vector3(faceX + inward * depth, y + 1.05f, zc), new Vector3(0.06f, 0.06f, w - 0.3f), p.Steel);
                b.Box("Balcony rail", new Vector3(faceX + inward * depth, y + 0.55f, zc), new Vector3(0.04f, 0.04f, w - 0.3f), p.Steel);
                for (float post = zc - w / 2 + 0.3f; post <= zc + w / 2 - 0.3f; post += 0.32f)
                    b.Box("Rail baluster", new Vector3(faceX + inward * depth, y + 0.6f, post), new Vector3(0.03f, 0.95f, 0.03f), p.Steel);
                int plants = 1 + random.Next(3);
                for (int plant = 0; plant < plants; plant++)
                {
                    float plantZ = zc + ((plant + 0.5f) / plants - 0.5f) * (w - 0.8f);
                    b.Box("Plant pot", new Vector3(faceX + inward * (depth - 0.2f), y + 0.35f, plantZ), new Vector3(0.3f, 0.36f, 0.3f), p.Terracotta);
                    b.Crown(new Vector3(faceX + inward * (depth - 0.2f), y + 0.75f, plantZ), new Vector3(0.32f, 0.38f, 0.32f), plant % 2 == 0 ? p.Foliage : p.LeafLight);
                }
                if (random.Next(3) == 0)
                {
                    // Laundry on the rail.
                    for (int cloth = 0; cloth < 3; cloth++)
                        b.Box("Laundry", new Vector3(faceX + inward * (depth + 0.02f), y + 0.8f, zc - 0.6f + cloth * 0.55f), new Vector3(0.02f, 0.5f, 0.4f), cloth % 2 == 0 ? p.FacadeTrim : p.PlasticBlue);
                }
            }

            // Roofline: parapet, stainless water tank, tin attic roof or antenna.
            b.Box("Parapet", new Vector3(faceX + inward * 0.02f, height + 0.35f, zc), new Vector3(0.2f, 0.7f, w), p.FacadeTrim);
            int roofStyle = random.Next(3);
            float roofX = side * (HouseFrontX + setback + HouseDepth * 0.55f);
            if (roofStyle == 0)
            {
                b.Box("Water tank stand", new Vector3(roofX, height + 0.35f, zc), new Vector3(0.9f, 0.7f, 0.9f), p.Steel);
                b.Cylinder(new Vector3(roofX, height + 1.0f, zc), 0.55f, 1.3f, p.WaterTank, Quaternion.Euler(90, 0, 0));
            }
            else if (roofStyle == 1)
            {
                b.TaperedBox(new Vector3(roofX, height, zc), new Vector2(HouseDepth * 0.7f, w - 0.2f), new Vector3(roofX, height + 1.6f, zc), new Vector2(0.4f, w - 0.2f), p.TinRoof);
            }
            else
            {
                b.Cylinder(new Vector3(roofX - side * 1.5f, height + 0.6f, zc), 0.45f, 1.2f, p.WaterTank);
                b.Beam(new Vector3(roofX, height, zc + 0.5f), new Vector3(roofX, height + 3f, zc + 0.5f), 0.05f, p.Steel);
                b.Beam(new Vector3(roofX - 0.6f, height + 2.6f, zc + 0.5f), new Vector3(roofX + 0.6f, height + 2.6f, zc + 0.5f), 0.04f, p.Steel);
            }

            // Red lanterns under the awning and the odd vertical sign facing traffic.
            if (random.Next(2) == 0)
            {
                for (int lantern = -1; lantern <= 1; lantern += 2)
                {
                    Vector3 hang = new Vector3(faceX + inward * 1.1f, 2.45f, zc + lantern * w * 0.28f);
                    b.Box("Lantern cord", hang + Vector3.up * 0.32f, new Vector3(0.015f, 0.3f, 0.015f), p.Cable);
                    b.Ellipsoid(hang, new Vector3(0.22f, 0.26f, 0.22f), p.Lantern);
                    b.Box("Lantern tassel", hang - Vector3.up * 0.34f, new Vector3(0.05f, 0.18f, 0.05f), p.FlagYellow);
                }
            }
            if (floors >= 4 && random.Next(3) == 0)
            {
                string word = VerticalSigns[random.Next(VerticalSigns.Length)];
                float signHeight = Mathf.Max(1.6f, word.Length * 0.42f + 0.3f);
                Vector3 signCenter = new Vector3(faceX + inward * 0.75f, groundHeight + 1.2f + signHeight / 2, zc - w / 2 + 0.3f);
                b.Box("Vertical sign bracket", signCenter + new Vector3(side * 0.45f, 0, 0), new Vector3(0.9f, 0.06f, 0.06f), p.Steel);
                b.Box("Vertical sign", signCenter, new Vector3(0.75f, signHeight, 0.12f), sign == p.ShopRed ? p.ShopBlue : p.ShopRed);
                Label(parent, string.Join("\n", word.ToCharArray()), signCenter + new Vector3(0, 0, -0.07f), 0.055f, Color.white, 0f);
            }
        }

        private static void UtilityPole(SceneryMeshBuilder b, float x, float z, int side, PrototypePalette p)
        {
            b.TaperedBox(new Vector3(x, 0, z), new Vector2(0.32f, 0.32f), new Vector3(x, 8.6f, z), new Vector2(0.18f, 0.18f), p.Concrete);
            foreach (float y in new[] { 7.4f, 8.1f })
                b.Box("Pole crossarm", new Vector3(x, y, z), new Vector3(1.6f, 0.1f, 0.1f), p.Steel);
            b.Box("Transformer", new Vector3(x - side * 0.35f, 6.2f, z), new Vector3(0.45f, 0.75f, 0.45f), p.Steel);
            // Coils of spare cable hanging from the pole.
            for (int coil = 0; coil < 3; coil++)
                b.Cylinder(new Vector3(x + (coil - 1) * 0.12f, 5.3f - coil * 0.25f, z - 0.25f), 0.24f - coil * 0.03f, 0.06f, p.Cable, Quaternion.Euler(90, 0, 0));
            // Small Vietnamese flag on a street-facing bracket.
            b.Box("Flag bracket", new Vector3(x, 4.9f, z - 0.3f), new Vector3(0.04f, 0.04f, 0.6f), p.Steel);
            b.Box("Flag pole", new Vector3(x, 4.5f, z - 0.6f), new Vector3(0.035f, 1.4f, 0.035f), p.Steel);
            b.Box("Vietnamese flag", new Vector3(x - side * 0.45f, 4.75f, z - 0.6f), new Vector3(0.9f, 0.6f, 0.02f), p.FlagRed);
            b.Star(new Vector3(x - side * 0.45f, 4.75f, z - 0.615f), 0.2f, p.FlagYellow);
        }

        private static void Wires(SceneryMeshBuilder b, float x, float fromZ, float toZ, System.Random random, PrototypePalette p)
        {
            for (int wire = 0; wire < 7; wire++)
            {
                float y = 6.6f + wire * 0.22f;
                float offset = ((wire % 3) - 1) * 0.18f;
                float sag = 0.4f + (float)random.NextDouble() * 1.1f;
                b.Catenary(new Vector3(x + offset, y, fromZ), new Vector3(x + offset, y + (float)random.NextDouble() * 0.3f, toZ), sag,
                    0.03f + (wire % 2) * 0.015f, p.Cable);
            }
        }

        private static void Tree(SceneryMeshBuilder b, float x, float z, PrototypePalette p)
        {
            b.Box("Tree pit", new Vector3(x, 0.08f, z), new Vector3(1.2f, 0.12f, 1.2f), p.Concrete);
            b.Box("Tree trunk", new Vector3(x, 2.6f, z), new Vector3(0.32f, 5.2f, 0.32f), p.Bark);
            b.Box("Whitewashed trunk base", new Vector3(x, 0.55f, z), new Vector3(0.36f, 0.9f, 0.36f), p.FacadeTrim);
            b.Beam(new Vector3(x, 4.4f, z), new Vector3(x - Mathf.Sign(x) * 0.9f, 5.6f, z + 0.4f), 0.16f, p.Bark);
            // Lumpy tropical canopy built from overlapping blobs.
            b.Ellipsoid(new Vector3(x, 5.9f, z), new Vector3(1.9f, 1.1f, 1.8f), p.Foliage);
            b.Ellipsoid(new Vector3(x - Mathf.Sign(x) * 1.0f, 6.5f, z + 0.6f), new Vector3(1.4f, 0.95f, 1.3f), p.LeafLight);
            b.Ellipsoid(new Vector3(x + Mathf.Sign(x) * 0.5f, 6.9f, z - 0.7f), new Vector3(1.2f, 0.85f, 1.2f), p.Foliage);
            b.Ellipsoid(new Vector3(x - Mathf.Sign(x) * 0.2f, 7.3f, z + 0.1f), new Vector3(1.0f, 0.7f, 1.0f), p.LeafLight);
        }

        private static void ScooterRow(SceneryMeshBuilder b, int side, float startZ, int count, System.Random random, PrototypePalette p)
        {
            Material[] paints = { p.ShopRed, p.PlasticBlue, p.FacadeTrim, p.Rubber, p.Facades[1], p.ShopYellow };
            for (int index = 0; index < count; index++)
            {
                Vector3 position = new Vector3(side * 7.3f, 0.06f, startZ + index * 0.85f);
                Quaternion heading = Quaternion.Euler(0, side < 0 ? 62f : -118f, 0);
                Scooter(b, position, heading, paints[random.Next(paints.Length)], p);
            }
        }

        /// <summary>A step-through scooter, the backbone of Saigon traffic.</summary>
        public static void Scooter(SceneryMeshBuilder b, Vector3 origin, Quaternion heading, Material paint, PrototypePalette p)
        {
            Vector3 P(float x, float y, float z) => origin + heading * new Vector3(x, y, z);
            Quaternion axle = heading * Quaternion.Euler(0, 0, 90);
            foreach (float wheelZ in new[] { -0.62f, 0.62f })
            {
                b.Cylinder(P(0, 0.28f, wheelZ), 0.27f, 0.11f, p.Rubber, axle);
                b.Cylinder(P(0, 0.28f, wheelZ), 0.14f, 0.12f, p.Chrome, axle);
            }
            b.Box("Scooter floorboard", P(0, 0.42f, 0.05f), new Vector3(0.34f, 0.1f, 0.7f), p.Rubber, heading);
            b.Box("Scooter body", P(0, 0.62f, -0.38f), new Vector3(0.4f, 0.42f, 0.7f), paint, heading);
            b.Box("Scooter seat", P(0, 0.88f, -0.32f), new Vector3(0.32f, 0.1f, 0.75f), p.Rubber, heading);
            b.Box("Scooter leg shield", P(0, 0.75f, 0.45f), new Vector3(0.38f, 0.75f, 0.12f), paint, heading * Quaternion.Euler(-14, 0, 0));
            b.Beam(P(0, 0.35f, 0.62f), P(0, 1.15f, 0.48f), 0.06f, p.Chrome);
            b.Box("Handlebar", P(0, 1.15f, 0.47f), new Vector3(0.62f, 0.05f, 0.05f), p.Rubber, heading);
            b.Box("Headlamp", P(0, 1.08f, 0.55f), new Vector3(0.16f, 0.1f, 0.06f), p.Headlight, heading);
            b.Box("Tail lamp", P(0, 0.72f, -0.74f), new Vector3(0.16f, 0.06f, 0.03f), p.Taillight, heading);
        }

        private static void FoodCart(Transform parent, SceneryMeshBuilder b, float x, float z, int side, PrototypePalette p)
        {
            b.Box("Cart base", new Vector3(x, 0.75f, z), new Vector3(0.75f, 0.12f, 1.5f), p.FacadeTrim);
            b.Box("Cart cabinet", new Vector3(x, 0.45f, z), new Vector3(0.7f, 0.55f, 1.4f), p.ShopBlue);
            b.Box("Glass display", new Vector3(x, 1.12f, z), new Vector3(0.7f, 0.6f, 1.4f), p.Window);
            b.Box("Display roof", new Vector3(x, 1.45f, z), new Vector3(0.8f, 0.06f, 1.5f), p.ShopRed);
            for (int loaf = 0; loaf < 4; loaf++)
                b.Box("Baguette", new Vector3(x, 0.92f, z - 0.45f + loaf * 0.3f), new Vector3(0.12f, 0.1f, 0.26f), p.Ochre, Quaternion.Euler(0, 70, 0));
            foreach (float wheelZ in new[] { z - 0.55f, z + 0.55f })
                b.Cylinder(new Vector3(x - side * 0.36f, 0.2f, wheelZ), 0.2f, 0.06f, p.Rubber, Quaternion.Euler(0, 0, 90));
            // Market umbrella.
            b.Box("Umbrella pole", new Vector3(x + side * 0.5f, 1.3f, z), new Vector3(0.05f, 2.6f, 0.05f), p.Steel);
            b.Cone(new Vector3(x + side * 0.5f, 2.45f, z), 1.25f, 0.45f, p.ShopYellow, 10);
            Label(parent, "BANH MI", new Vector3(x, 1.3f, z - 0.76f), 0.04f, new Color(1f, 0.95f, 0.3f), 0f);
        }

        private static void PlasticStools(SceneryMeshBuilder b, float x, float z, System.Random random, PrototypePalette p)
        {
            b.Box("Low table top", new Vector3(x, 0.52f, z), new Vector3(0.7f, 0.04f, 0.7f), p.Chrome);
            b.Box("Low table leg", new Vector3(x, 0.26f, z), new Vector3(0.08f, 0.5f, 0.08f), p.Steel);
            for (int stool = 0; stool < 4; stool++)
            {
                float angle = stool * Mathf.PI / 2 + 0.4f;
                Vector3 position = new Vector3(x + Mathf.Cos(angle) * 0.62f, 0, z + Mathf.Sin(angle) * 0.62f);
                PlasticStool(b, position, random.Next(2) == 0 ? p.PlasticRed : p.PlasticBlue);
            }
            b.Cylinder(new Vector3(x, 0.62f, z), 0.08f, 0.16f, p.Ochre);
        }

        public static void PlasticStool(SceneryMeshBuilder b, Vector3 position, Material color)
        {
            b.TaperedBox(position, new Vector2(0.36f, 0.36f), position + Vector3.up * 0.4f, new Vector2(0.28f, 0.28f), color);
            b.Box("Stool seat", position + Vector3.up * 0.42f, new Vector3(0.32f, 0.04f, 0.32f), color);
        }

        private static void StreetSign(Transform parent, SceneryMeshBuilder b, float x, float z, string label, PrototypePalette p)
        {
            b.Box("Signpost", new Vector3(x, 1.9f, z), new Vector3(0.08f, 3.8f, 0.08f), p.Steel);
            b.Box("Street sign rim", new Vector3(x, 3.6f, z), new Vector3(2.4f, 0.62f, 0.08f), p.FacadeTrim);
            b.Box("Saigon street sign", new Vector3(x, 3.6f, z - 0.045f), new Vector3(2.3f, 0.52f, 0.03f), p.StreetSignBlue);
            Label(parent, label, new Vector3(x, 3.66f, z - 0.065f), label.Length > 12 ? 0.042f : 0.058f, Color.white, 0f);
            Label(parent, "QUAN 1", new Vector3(x, 3.43f, z - 0.065f), 0.028f, new Color(0.85f, 0.9f, 1f), 0f);
        }

        private static void Apartment(SceneryMeshBuilder b, float x, float z, float height, PrototypePalette p)
        {
            float faceX = x - Mathf.Sign(x) * 5f;
            b.Box("Apartment block", new Vector3(x, height / 2, z), new Vector3(10, height, 16), p.Facades[4]);
            for (float y = 3.5f; y < height - 1.5f; y += 3f)
            {
                b.Box("Apartment balcony band", new Vector3(faceX - Mathf.Sign(x) * 0.3f, y, z), new Vector3(0.6f, 0.15f, 15.6f), p.Concrete);
                for (int col = 0; col < 5; col++)
                    b.Box("Apartment window", new Vector3(faceX - Mathf.Sign(x) * 0.02f, y + 1.2f, z - 6 + col * 3), new Vector3(0.05f, 1.4f, 1.6f), p.Window);
            }
            b.Box("Rooftop sign frame", new Vector3(x, height + 1.2f, z), new Vector3(0.3f, 2.2f, 9f), p.ShopRed);
        }

        private static void Label(Transform parent, string content, Vector3 position, float size, Color color, float yaw)
        {
            var item = new GameObject(content);
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var text = item.AddComponent<TextMesh>();
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 48;
            text.fontStyle = FontStyle.Bold;
            text.characterSize = size;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = color;
            text.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
        }

        public static void CreateSkyline(Transform parent, Transform runner, PrototypePalette p)
        {
            var root = new GameObject("Saigon skyline");
            root.transform.SetParent(parent, false);
            root.AddComponent<SkylineFollow>().Configure(runner);
            var b = new SceneryMeshBuilder(root.transform);
            // A distant ground apron keeps the far end of the pooled street in the haze.
            b.Box("Distant city ground", new Vector3(0, -0.7f, 260), new Vector3(650, 0.5f, 580), p.RoadEdge);
            b.Box("Street into the horizon", new Vector3(0, -0.31f, 330), new Vector3(9.6f, 0.1f, 300), p.Road);

            Landmark81(b, new Vector3(-62, 0, 285), p);
            Bitexco(b, new Vector3(58, 0, 300), p);
            Cathedral(b, new Vector3(30, 0, 222), p);
            BenThanhMarket(b, new Vector3(-34, 0, 212), p);

            var random = new System.Random(84);
            for (int index = 0; index < 18; index++)
            {
                float x = -170 + index * 20;
                if (Mathf.Abs(x) < 16) continue;
                float height = 22 + random.Next(48);
                float z = 315 + (index % 3) * 14;
                Material wall = index % 3 == 0 ? p.TowerGlass : index % 3 == 1 ? p.Concrete : p.Facades[4];
                b.Box("District tower", new Vector3(x, height / 2, z), new Vector3(13, height, 13), wall);
                for (float y = 4; y < height - 2; y += 4)
                    b.Box("Tower floor line", new Vector3(x, y, z - 6.55f), new Vector3(12.6f, 0.4f, 0.1f), p.TowerSilver);
            }

            // Tall tropical cumulus.
            for (int cluster = 0; cluster < 6; cluster++)
            for (int puff = 0; puff < 4; puff++)
                b.Ellipsoid(new Vector3(-200 + cluster * 80 + puff * 15, 95 + cluster % 2 * 22 + puff % 2 * 10 + puff * 4, 400),
                    new Vector3(22 + puff * 2, 12 + puff * 3, 14), p.Cloud);
            b.Finish(false);
        }

        private static void Landmark81(SceneryMeshBuilder b, Vector3 o, PrototypePalette p)
        {
            // Bundled square tubes of staggered heights, like a bamboo bundle.
            float[,] heights = { { 118, 138, 112 }, { 140, 170, 132 }, { 108, 128, 100 } };
            for (int col = 0; col < 3; col++)
            for (int row = 0; row < 3; row++)
            {
                float h = heights[col, row];
                Vector3 c = o + new Vector3((col - 1) * 9, h / 2, (row - 1) * 9);
                b.Box("Landmark tube", c, new Vector3(9, h, 9), p.TowerGlass);
                for (int fin = -1; fin <= 1; fin++)
                    b.Box("Landmark fin", c + new Vector3(fin * 3, 0, -4.55f), new Vector3(0.25f, h, 0.1f), p.TowerSilver);
                b.Box("Landmark tube crown", o + new Vector3((col - 1) * 9, h + 1.2f, (row - 1) * 9), new Vector3(8.4f, 2.4f, 8.4f), p.TowerSilver);
            }
            b.TaperedBox(o + new Vector3(0, 172, 0), new Vector2(8, 8), o + new Vector3(0, 190, 0), new Vector2(1.2f, 1.2f), p.TowerSilver);
            b.Box("Landmark spire", o + new Vector3(0, 198, 0), new Vector3(0.6f, 16, 0.6f), p.TowerSilver);
        }

        private static void Bitexco(SceneryMeshBuilder b, Vector3 o, PrototypePalette p)
        {
            // Lotus-bud profile: swells above the base, narrows to a point.
            const float height = 130f;
            const int rings = 18;
            for (int ring = 0; ring < rings; ring++)
            {
                float t = (ring + 0.5f) / rings;
                float radius = 9.5f * (0.78f + 0.32f * Mathf.Sin(Mathf.PI * (0.1f + t * 0.8f))) * (1 - 0.62f * t * t * t);
                b.Cylinder(o + new Vector3(0, t * height, 0), radius, height / rings, ring % 3 == 2 ? p.TowerSilver : p.TowerGlass);
            }
            // The cantilevered helipad.
            b.Box("Helipad arm", o + new Vector3(-7, 0.6f * height, -3), new Vector3(9, 2.2f, 6), p.TowerSilver);
            b.Cylinder(o + new Vector3(-12, 0.6f * height + 0.6f, -5), 7.5f, 1.4f, p.TowerSilver);
            b.Cylinder(o + new Vector3(-12, 0.6f * height + 1.35f, -5), 5.5f, 0.1f, p.FacadeTrim);
            b.Cone(o + new Vector3(0, height, 0), 2.6f, 14f, p.TowerSilver, 10);
        }

        private static void Cathedral(SceneryMeshBuilder b, Vector3 o, PrototypePalette p)
        {
            b.Box("Cathedral nave", o + new Vector3(0, 9, 14), new Vector3(16, 18, 32), p.Brick);
            b.TaperedBox(o + new Vector3(0, 18, 14), new Vector2(16, 32), o + new Vector3(0, 24, 14), new Vector2(1, 30), p.Steel);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 tower = o + new Vector3(side * 6.5f, 0, -2);
                b.Box("Cathedral bell tower", tower + new Vector3(0, 17, 0), new Vector3(6, 34, 6), p.Brick);
                b.Box("Bell tower louvres", tower + new Vector3(0, 28, -3.05f), new Vector3(2.4f, 4, 0.1f), p.Window);
                b.TaperedBox(tower + new Vector3(0, 34, 0), new Vector2(5.6f, 5.6f), tower + new Vector3(0, 52, 0), new Vector2(0.3f, 0.3f), p.Steel);
                b.Box("Cross upright", tower + new Vector3(0, 54, 0), new Vector3(0.3f, 4, 0.3f), p.Steel);
                b.Box("Cross bar", tower + new Vector3(0, 54.6f, 0), new Vector3(1.8f, 0.3f, 0.3f), p.Steel);
            }
            b.Box("Cathedral front", o + new Vector3(0, 12, -2.2f), new Vector3(7, 24, 3), p.Brick);
            b.Cylinder(o + new Vector3(0, 15, -3.8f), 2.2f, 0.2f, p.Window, Quaternion.Euler(90, 0, 0));
        }

        private static void BenThanhMarket(SceneryMeshBuilder b, Vector3 o, PrototypePalette p)
        {
            b.Box("Market hall", o + new Vector3(0, 5, 14), new Vector3(46, 10, 28), p.Ochre);
            b.TaperedBox(o + new Vector3(0, 10, 14), new Vector2(46, 28), o + new Vector3(0, 15, 14), new Vector2(40, 4), p.TinRoof);
            b.Box("Clock tower", o + new Vector3(0, 12, 0), new Vector3(8, 24, 8), p.Ochre);
            b.Box("Clock tower cornice", o + new Vector3(0, 24.5f, 0), new Vector3(9, 1, 9), p.FacadeTrim);
            b.TaperedBox(o + new Vector3(0, 25, 0), new Vector2(8.4f, 8.4f), o + new Vector3(0, 30, 0), new Vector2(2.5f, 2.5f), p.TinRoof);
            b.Cylinder(o + new Vector3(0, 19.5f, -4.05f), 2.4f, 0.1f, p.FacadeTrim, Quaternion.Euler(90, 0, 0));
            b.Box("Clock hand", o + new Vector3(0, 20.2f, -4.15f), new Vector3(0.25f, 1.6f, 0.05f), p.Rubber);
            b.Box("Clock hand", o + new Vector3(0.5f, 19.5f, -4.15f), new Vector3(1.1f, 0.25f, 0.05f), p.Rubber);
            b.Box("Market entrance", o + new Vector3(0, 3, -4.05f), new Vector3(4, 6, 0.1f), p.Window);
            foreach (float x in new[] { -16f, 16f })
                b.Box("Market corner pavilion", o + new Vector3(x, 7, 0), new Vector3(6, 14, 6), p.Ochre);
        }
    }

    /// <summary>One mesh per material per block, with explicit lifetime ownership.</summary>
    public sealed class SceneryMeshBuilder
    {
        private sealed class Geometry
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly List<int> Triangles = new List<int>();
        }
        private readonly Transform parent;
        private readonly Dictionary<Material, Geometry> geometry = new Dictionary<Material, Geometry>();
        public SceneryMeshBuilder(Transform parent) { this.parent = parent; }
        private void Triangle(Vector3 a, Vector3 b, Vector3 c, Material material)
        {
            if (!geometry.TryGetValue(material, out Geometry g)) geometry.Add(material, g = new Geometry());
            int start = g.Vertices.Count;
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            g.Vertices.Add(a); g.Vertices.Add(b); g.Vertices.Add(c);
            g.Normals.Add(normal); g.Normals.Add(normal); g.Normals.Add(normal);
            g.Triangles.Add(start); g.Triangles.Add(start + 1); g.Triangles.Add(start + 2);
        }
        public void Box(string name, Vector3 center, Vector3 size, Material material, Quaternion? rotation = null)
        {
            Quaternion r = rotation ?? Quaternion.identity;
            Vector3 x = r * new Vector3(size.x / 2, 0, 0);
            Vector3 y = r * new Vector3(0, size.y / 2, 0);
            Vector3 z = r * new Vector3(0, 0, size.z / 2);
            Face(center + x, -z, y, material); Face(center - x, z, y, material);
            Face(center + y, x, -z, material); Face(center - y, x, z, material);
            Face(center + z, x, y, material); Face(center - z, -x, y, material);
        }
        private void Face(Vector3 c, Vector3 u, Vector3 v, Material m)
        {
            Triangle(c - u - v, c + u - v, c + u + v, m);
            Triangle(c - u - v, c + u + v, c - u + v, m);
        }
        public void Beam(Vector3 from, Vector3 to, float thickness, Material p)
        {
            Box("Beam", (from + to) / 2, new Vector3(thickness, (to - from).magnitude, thickness), p, Quaternion.FromToRotation(Vector3.up, to - from));
        }
        /// <summary>A sagging cable approximated by straight beams.</summary>
        public void Catenary(Vector3 from, Vector3 to, float sag, float thickness, Material material, int segments = 6)
        {
            Vector3 previous = from;
            for (int i = 1; i <= segments; i++)
            {
                float t = i / (float)segments;
                Vector3 point = Vector3.Lerp(from, to, t) + Vector3.down * (4f * sag * t * (1 - t));
                Beam(previous, point, thickness, material);
                previous = point;
            }
        }
        public void Crown(Vector3 center, Vector3 radius, Material material)
        {
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4;
                float next = (i + 1) * Mathf.PI / 4;
                Vector3 a = center + new Vector3(Mathf.Cos(angle) * radius.x, 0, Mathf.Sin(angle) * radius.z);
                Vector3 b = center + new Vector3(Mathf.Cos(next) * radius.x, 0, Mathf.Sin(next) * radius.z);
                Triangle(center + Vector3.up * radius.y, b, a, material);
                Triangle(center - Vector3.up * radius.y * 0.7f, a, b, material);
            }
        }
        /// <summary>A flat star facing -Z (toward the following camera).</summary>
        public void Star(Vector3 center, float radius, Material p, int points = 5)
        {
            int corners = points * 2;
            for (int i = 0; i < corners; i++)
            {
                float a = i * Mathf.PI * 2 / corners, b = (i + 1) * Mathf.PI * 2 / corners;
                Vector3 v1 = center + new Vector3(Mathf.Sin(a), Mathf.Cos(a), 0) * radius * (i % 2 == 0 ? 1 : 0.38f);
                Vector3 v2 = center + new Vector3(Mathf.Sin(b), Mathf.Cos(b), 0) * radius * (i % 2 == 0 ? 0.38f : 1);
                Triangle(center, v1, v2, p);
            }
        }
        /// <summary>An upright cone with its base centred on <paramref name="baseCenter"/>.</summary>
        public void Cone(Vector3 baseCenter, float radius, float height, Material material, int segments = 12)
        {
            Vector3 apex = baseCenter + Vector3.up * height;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments, next = (i + 1) * Mathf.PI * 2 / segments;
                Vector3 v1 = baseCenter + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius;
                Vector3 v2 = baseCenter + new Vector3(Mathf.Cos(next), 0, Mathf.Sin(next)) * radius;
                Triangle(apex, v2, v1, material);
                Triangle(baseCenter, v1, v2, material);
            }
        }
        public void Cylinder(Vector3 center, float radius, float height, Material material, Quaternion? rotation = null)
        {
            Quaternion r = rotation ?? Quaternion.identity;
            Vector3 up = r * Vector3.up * height / 2;
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI / 8, next = (i + 1) * Mathf.PI / 8;
                Vector3 v1 = r * new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius;
                Vector3 v2 = r * new Vector3(Mathf.Cos(next), 0, Mathf.Sin(next)) * radius;
                Triangle(center + up, center + up + v2, center + up + v1, material);
                Triangle(center - up, center - up + v1, center - up + v2, material);
                Triangle(center + v1 - up, center + v1 + up, center + v2 + up, material);
                Triangle(center + v1 - up, center + v2 + up, center + v2 - up, material);
            }
        }
        public void TaperedBox(Vector3 bottom, Vector2 bottomSize, Vector3 top, Vector2 topSize, Material material)
        {
            Vector3 a = bottom + new Vector3(-bottomSize.x / 2, 0, -bottomSize.y / 2);
            Vector3 b = bottom + new Vector3(bottomSize.x / 2, 0, -bottomSize.y / 2);
            Vector3 c = bottom + new Vector3(bottomSize.x / 2, 0, bottomSize.y / 2);
            Vector3 d = bottom + new Vector3(-bottomSize.x / 2, 0, bottomSize.y / 2);
            Vector3 e = top + new Vector3(-topSize.x / 2, 0, -topSize.y / 2);
            Vector3 f = top + new Vector3(topSize.x / 2, 0, -topSize.y / 2);
            Vector3 g = top + new Vector3(topSize.x / 2, 0, topSize.y / 2);
            Vector3 h = top + new Vector3(-topSize.x / 2, 0, topSize.y / 2);
            Quad(a, b, f, e, material); Quad(d, h, g, c, material);
            Quad(a, e, h, d, material); Quad(b, c, g, f, material);
            Quad(e, f, g, h, material); Quad(a, d, c, b, material);
        }
        private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Material material)
        {
            Triangle(a, c, b, material); Triangle(a, d, c, material);
        }
        public void Ellipsoid(Vector3 center, Vector3 radius, Material material)
        {
            for (int ring = 0; ring < 6; ring++)
            for (int slice = 0; slice < 12; slice++)
            {
                Vector3 a = SpherePoint(ring, slice, radius) + center;
                Vector3 b = SpherePoint(ring + 1, slice, radius) + center;
                Vector3 c = SpherePoint(ring + 1, slice + 1, radius) + center;
                Vector3 d = SpherePoint(ring, slice + 1, radius) + center;
                Triangle(a, d, c, material);
                Triangle(a, c, b, material);
            }
        }
        private static Vector3 SpherePoint(int ring, int slice, Vector3 radius)
        {
            float latitude = ring * Mathf.PI / 6;
            float longitude = slice * Mathf.PI / 6;
            return Vector3.Scale(new Vector3(Mathf.Sin(latitude) * Mathf.Cos(longitude), Mathf.Cos(latitude), Mathf.Sin(latitude) * Mathf.Sin(longitude)), radius);
        }
        public void Finish(bool shadows = true)
        {
            var owner = parent.gameObject.AddComponent<SceneryMeshOwner>();
            foreach (var pair in geometry)
            {
                var mesh = new Mesh { name = "Saigon scenery", indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(pair.Value.Vertices);
                mesh.SetNormals(pair.Value.Normals);
                mesh.SetTriangles(pair.Value.Triangles, 0);
                mesh.RecalculateBounds();
                mesh.UploadMeshData(true);
                owner.Meshes.Add(mesh);
                var item = new GameObject(pair.Key.name + " scenery");
                item.transform.SetParent(parent, false);
                item.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = item.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = pair.Key;
                renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                renderer.receiveShadows = shadows;
            }
        }
    }
}

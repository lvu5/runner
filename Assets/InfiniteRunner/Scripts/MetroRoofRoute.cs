using UnityEngine;

namespace InfiniteRunner
{
    public enum MetroRoutePhase { Street, Ascent, Rooftops, Descent }

    /// <summary>A four-block train excursion. Segment serials define continuous, recyclable surfaces.</summary>
    public static class MetroRoofRoute
    {
        public const int FirstSegment = 4;
        public const int RepeatEverySegments = 12;
        public const float RoofHeight = 4.2f;
        public const float ClosureHeight = 3.5f;
        // Bits 0..2 are the left, center and right lanes. Keep one layout for the whole excursion.
        private static readonly int[] LaneLayouts = { 5, 2, 3, 4, 6, 1, 7 };

        public static int LaneMaskForSegment(int serial) => serial < FirstSegment ? 7 :
            LaneLayouts[((serial - FirstSegment) / RepeatEverySegments) % LaneLayouts.Length];

        public static bool IsLaneOpen(int mask, int lane) => (mask & (1 << lane)) != 0;

        public static string LaneHint(int mask)
        {
            string lanes = "";
            for (int lane = 0; lane < 3; lane++)
                if (IsLaneOpen(mask, lane)) lanes += (lanes.Length == 0 ? "" : " + ") + (lane + 1);
            return lanes;
        }

        public static MetroRoutePhase PhaseForSegment(int serial)
        {
            if (serial < FirstSegment) return MetroRoutePhase.Street;
            int phase = (serial - FirstSegment) % RepeatEverySegments;
            if (phase == 0) return MetroRoutePhase.Ascent;
            if (phase == 1 || phase == 2) return MetroRoutePhase.Rooftops;
            return phase == 3 ? MetroRoutePhase.Descent : MetroRoutePhase.Street;
        }

        public static float SurfaceHeight(MetroRoutePhase phase, float z, float length)
        {
            if (phase == MetroRoutePhase.Ascent)
                return RoofHeight * Mathf.InverseLerp(length * (4f / 30f), length * 0.6f, z);
            if (phase == MetroRoutePhase.Rooftops) return RoofHeight;
            if (phase == MetroRoutePhase.Descent)
                return RoofHeight * (1f - Mathf.InverseLerp(length * (8f / 30f), length * (22f / 30f), z));
            return 0f;
        }

        public static GameObject Build(Transform parent, MetroRoutePhase phase, float length, float laneWidth, PrototypePalette p, int laneMask = 7)
        {
            var root = new GameObject("Metro " + phase);
            root.transform.SetParent(parent, false);
            // Cache the three lane pairs once per phase, then toggle them when a segment is recycled.
            for (int lane = 0; lane < 3; lane++)
            {
                var laneRoot = new GameObject("Lane " + (lane + 1));
                laneRoot.transform.SetParent(root.transform, false);
                laneRoot.transform.localPosition = new Vector3((lane - 1) * laneWidth, 0, 0);
                BuildOpenLane(laneRoot.transform, phase, length, laneWidth, p);
                BuildClosedLane(laneRoot.transform, phase, length, laneWidth, p);
            }
            if (phase != MetroRoutePhase.Rooftops)
            {
                var signs = new SceneryMeshBuilder(root.transform);
                RouteSign(root.transform, signs, phase == MetroRoutePhase.Descent ? length * 0.2f : length * 0.1f, p);
                signs.Finish();
            }
            ApplyLaneMask(root, phase, laneMask);
            return root;
        }

        public static void ApplyLaneMask(GameObject root, MetroRoutePhase phase, int mask)
        {
            for (int lane = 0; lane < 3; lane++)
            {
                Transform pair = root.transform.GetChild(lane);
                pair.GetChild(0).gameObject.SetActive(IsLaneOpen(mask, lane));
                pair.GetChild(1).gameObject.SetActive(!IsLaneOpen(mask, lane));
            }
            Transform instruction = root.transform.Find("Metro instruction");
            if (instruction != null)
                instruction.GetComponent<TextMesh>().text = phase == MetroRoutePhase.Descent
                    ? "XUONG DUONG / STREET\nRAMPS " + LaneHint(mask)
                    : "METRO SO 1 / ROOFTOP\nRAMPS " + LaneHint(mask);
        }

        private static void BuildOpenLane(Transform parent, MetroRoutePhase phase, float length, float width, PrototypePalette p)
        {
            var root = new GameObject("Open train lane");
            root.transform.SetParent(parent, false);
            var b = new SceneryMeshBuilder(root.transform);
            float roofFrom = phase == MetroRoutePhase.Ascent ? length * 0.6f : 0;
            float roofTo = phase == MetroRoutePhase.Descent ? length * (8f / 30f) : length;
            if (phase == MetroRoutePhase.Ascent)
                Ramp(root.transform, b, length * (4f / 30f), roofFrom, 0, RoofHeight, width, p);
            else if (phase == MetroRoutePhase.Descent)
                Ramp(root.transform, b, roofTo, length * (22f / 30f), RoofHeight, 0, width, p);

            // Only the roof is walkable. Hitting the train from the street ends the run.
            var deck = new GameObject("Walkable metro roof");
            deck.transform.SetParent(root.transform, false);
            BoxCollider surface = deck.AddComponent<BoxCollider>();
            surface.center = new Vector3(0, RoofHeight - 0.075f, (roofFrom + roofTo) / 2);
            surface.size = new Vector3(width, 0.15f, roofTo - roofFrom);
            deck.AddComponent<TrackSurface>();
            HazardBox(root.transform, "Train body", new Vector3(0, 2.0f, (roofFrom + roofTo) / 2),
                new Vector3(width - 0.15f, 4.0f, roofTo - roofFrom - 0.16f));
            int carCount = Mathf.CeilToInt((roofTo - roofFrom) / 15f);
            float carLength = (roofTo - roofFrom) / carCount;
            for (int car = 0; car < carCount; car++)
            {
                float from = roofFrom + car * carLength;
                TrainCar(b, 0, from, from + carLength, width, p);
                if (car == 0 && phase == MetroRoutePhase.Ascent)
                    Label(root.transform, "BEN THANH", new Vector3(0, 3.77f, from + 0.01f), 0.03f);
            }
            b.Finish();
        }

        private static void BuildClosedLane(Transform parent, MetroRoutePhase phase, float length, float width, PrototypePalette p)
        {
            var root = new GameObject("Closed street lane");
            root.transform.SetParent(parent, false);
            var b = new SceneryMeshBuilder(root.transform);
            float from = phase == MetroRoutePhase.Ascent ? length * 0.4f : 0;
            float to = phase == MetroRoutePhase.Descent ? length * (22f / 30f) : length;
            int count = Mathf.CeilToInt((to - from) / 14f);
            float size = (to - from) / count;
            // Tall construction containers visibly occupy the closed lane. Their tops are hazards too.
            for (int container = 0; container < count; container++)
            {
                float front = from + container * size;
                float z = front + size / 2;
                b.Box("Construction storage container", new Vector3(0, ClosureHeight / 2, z),
                    new Vector3(width - 0.1f, ClosureHeight, size), container % 2 == 0 ? p.MetroBlue : p.Ochre);
                HazardBox(root.transform, "Closed lane container", new Vector3(0, ClosureHeight / 2, z),
                    new Vector3(width - 0.1f, ClosureHeight, size));
                for (int side = -1; side <= 1; side += 2)
                {
                    for (float rib = front + 0.25f; rib < front + size; rib += 0.48f)
                        b.Box("Container side ribs", new Vector3(side * (width / 2 - 0.045f), ClosureHeight / 2, rib),
                            new Vector3(0.025f, ClosureHeight - 0.2f, 0.045f), p.Steel);
                    b.Box("Container door locking bar", new Vector3(side * 0.65f, 1.75f, front - 0.02f), new Vector3(0.05f, 3.1f, 0.045f), p.Chrome);
                    b.Box("Closure warning lamp", new Vector3(side * 0.9f, 3.32f, front - 0.05f), new Vector3(0.22f, 0.18f, 0.05f), p.Taillight);
                }
                b.Box("Road closed sign", new Vector3(0, 2.45f, front - 0.035f), new Vector3(width - 0.35f, 0.7f, 0.035f), p.FlagRed);
                if (container == 0 && phase == MetroRoutePhase.Ascent)
                    Label(root.transform, "CONG TRINH\nCLOSED", new Vector3(0, 2.45f, front - 0.062f), 0.06f);
                b.Box("Reflective closure stripe", new Vector3(0, 0.65f, front - 0.025f), new Vector3(width - 0.22f, 0.35f, 0.035f), p.LaneLine);
                for (int stripe = -2; stripe <= 2; stripe++)
                    b.Box("Orange warning stripe", new Vector3(stripe * 0.46f, 0.65f, front - 0.049f), new Vector3(0.22f, 0.35f, 0.015f), p.JumpObstacle, Quaternion.Euler(0, 0, -25));
            }
            b.Finish();
        }

        private static void HazardBox(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var item = new GameObject(name);
            item.transform.SetParent(parent, false);
            BoxCollider collider = item.AddComponent<BoxCollider>();
            collider.center = center; collider.size = size;
            item.AddComponent<ObstacleMarker>();
        }

        private static void Ramp(Transform parent, SceneryMeshBuilder b, float fromZ, float toZ, float fromY, float toY, float width, PrototypePalette p)
        {
            float slope = Mathf.Atan2(toY - fromY, toZ - fromZ);
            Quaternion rotation = Quaternion.Euler(-slope * Mathf.Rad2Deg, 0, 0);
            Vector3 normal = rotation * Vector3.up;
            float rampLength = new Vector2(toZ - fromZ, toY - fromY).magnitude;
            Vector3 center = new Vector3(0, (fromY + toY) / 2, (fromZ + toZ) / 2);
            GameObject ramp = PrototypeFactory.CreatePrimitive("Walkable access ramp", PrimitiveType.Cube, parent,
                center - normal * 0.12f, new Vector3(width, 0.24f, rampLength), p.Concrete, true, rotation);
            ramp.AddComponent<TrackSurface>();
            for (float z = fromZ + 0.45f; z < toZ; z += 0.85f)
            {
                float y = Mathf.Lerp(fromY, toY, Mathf.InverseLerp(fromZ, toZ, z));
                b.Box("Ramp grip strip", new Vector3(0, y, z) + normal * 0.012f, new Vector3(width - 0.3f, 0.018f, 0.065f), p.Steel, rotation);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                b.Box("Yellow ramp edge", center + new Vector3(side * (width / 2 - 0.12f), 0, 0) + normal * 0.02f, new Vector3(0.16f, 0.024f, rampLength), p.FlagYellow, rotation);
                for (float z = fromZ + 2; z < toZ - 1; z += 3)
                {
                    float height = Mathf.Lerp(fromY, toY, Mathf.InverseLerp(fromZ, toZ, z));
                    b.Box("Ramp support", new Vector3(side * (width / 2 - 0.15f), height / 2 - 0.1f, z), new Vector3(0.16f, Mathf.Max(0.1f, height - 0.2f), 0.16f), p.Steel);
                }
            }
        }

        private static void TrainCar(SceneryMeshBuilder b, float x, float from, float to, float laneWidth, PrototypePalette p)
        {
            // White metro car with a deep blue skirt, sky-blue accent and a black window band.
            float length = to - from;
            float z = (from + to) / 2;
            float width = laneWidth - 0.15f;
            b.Box("Metro undercarriage", new Vector3(x, 0.75f, z), new Vector3(width - 0.3f, 0.45f, length - 0.2f), p.Steel);
            b.Box("Metro white body", new Vector3(x, 2.43f, z), new Vector3(width, 3.24f, length - 0.16f), p.MetroWhite);
            b.Box("Walkable roof", new Vector3(x, RoofHeight - 0.075f, z), new Vector3(laneWidth, 0.15f, length), p.TowerSilver);
            foreach (float bogieZ in new[] { from + 1.5f, to - 1.5f })
            {
                b.Box("Train bogie", new Vector3(x, 0.53f, bogieZ), new Vector3(width - 0.15f, 0.32f, 1.6f), p.Rubber);
                for (int side = -1; side <= 1; side += 2)
                    b.Cylinder(new Vector3(x + side * (width / 2 - 0.04f), 0.43f, bogieZ), 0.39f, 0.12f, p.Steel, Quaternion.Euler(0, 0, 90));
            }
            for (int side = -1; side <= 1; side += 2)
            {
                float sideX = x + side * (width / 2 + 0.012f);
                b.Box("Metro blue skirt", new Vector3(sideX, 1.12f, z), new Vector3(0.025f, 0.5f, length - 0.24f), p.MetroBlue);
                b.Box("Metro sky stripe", new Vector3(sideX, 1.48f, z), new Vector3(0.025f, 0.1f, length - 0.24f), p.MetroSky);
                b.Box("Metro window band", new Vector3(sideX, 2.9f, z), new Vector3(0.02f, 1.15f, length - 0.5f), p.VehicleGlass);
                int windowCount = Mathf.Max(3, Mathf.FloorToInt(length / 1.8f));
                for (int window = 0; window < windowCount; window++)
                {
                    float windowZ = Mathf.Lerp(from + 0.6f, to - 0.6f, window / (float)(windowCount - 1));
                    b.Box("Window mullion", new Vector3(sideX + side * 0.012f, 2.9f, windowZ), new Vector3(0.02f, 1.15f, 0.08f), p.MetroWhite);
                }
                b.Box("Door frame", new Vector3(sideX + side * 0.025f, 2.15f, z), new Vector3(0.024f, 2.5f, 1.38f), p.MetroBlue);
                b.Box("Sliding doors", new Vector3(sideX + side * 0.042f, 2.15f, z), new Vector3(0.014f, 2.35f, 1.24f), p.MetroWhite);
                b.Box("Door center seam", new Vector3(sideX + side * 0.055f, 2.15f, z), new Vector3(0.014f, 2.35f, 0.025f), p.Steel);
                for (int door = -1; door <= 1; door += 2)
                    b.Box("Door window", new Vector3(sideX + side * 0.064f, 2.85f, z + door * 0.32f), new Vector3(0.014f, 0.8f, 0.48f), p.VehicleGlass);
                b.Box("Rail", new Vector3(x + side * 0.9f, 0.09f, z), new Vector3(0.09f, 0.14f, length), p.Steel);
            }
            foreach (float endZ in new[] { from + 0.07f, to - 0.07f })
            {
                float faceZ = endZ < z ? endZ - 0.02f : endZ + 0.02f;
                b.Box("Cab windscreen", new Vector3(x, 2.95f, faceZ), new Vector3(width - 0.35f, 1.05f, 0.045f), p.VehicleGlass);
                for (int side = -1; side <= 1; side += 2)
                    b.Box("Train lamp", new Vector3(x + side * 0.8f, 1.6f, faceZ), new Vector3(0.3f, 0.14f, 0.05f), p.Headlight);
                b.Box("Front blue chevron", new Vector3(x, 1.25f, faceZ), new Vector3(width - 0.1f, 0.45f, 0.05f), p.MetroBlue);
                b.Box("Destination board", new Vector3(x, 3.77f, faceZ), new Vector3(1.5f, 0.27f, 0.045f), p.Rubber);
                b.Box("Car coupler", new Vector3(x, 0.9f, endZ), new Vector3(0.4f, 0.24f, 0.2f), p.Steel);
            }
            // Sky-blue roof bands make the raised course readable from the following camera.
            b.Box("Roof stripe", new Vector3(x - width / 2 + 0.16f, RoofHeight + 0.012f, z), new Vector3(0.16f, 0.018f, length), p.MetroSky);
            b.Box("Roof stripe", new Vector3(x + width / 2 - 0.16f, RoofHeight + 0.012f, z), new Vector3(0.16f, 0.018f, length), p.MetroSky);
        }

        private static void RouteSign(Transform parent, SceneryMeshBuilder b, float z, PrototypePalette p)
        {
            b.Box("Metro route signpost", new Vector3(5.05f, 2.5f, z), new Vector3(0.1f, 5, 0.1f), p.Steel);
            b.Box("Metro route sign", new Vector3(5.05f, 4.8f, z), new Vector3(3.3f, 1.1f, 0.1f), p.MetroBlue);
            b.Box("Metro route sign stripe", new Vector3(5.05f, 4.28f, z - 0.055f), new Vector3(3.3f, 0.08f, 0.02f), p.MetroSky);
            Label(parent, "METRO", new Vector3(5.05f, 4.8f, z - 0.06f), 0.055f).name = "Metro instruction";
        }

        private static TextMesh Label(Transform parent, string text, Vector3 position, float size)
        {
            var label = new GameObject(text);
            label.transform.SetParent(parent, false);
            label.transform.localPosition = position;
            var mesh = label.AddComponent<TextMesh>();
            mesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            mesh.fontSize = 48;
            mesh.characterSize = size;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.text = text;
            mesh.color = Color.white;
            mesh.GetComponent<MeshRenderer>().sharedMaterial = mesh.font.material;
            return mesh;
        }
    }
}

using UnityEngine;

namespace InfiniteRunner
{
    public sealed class TrackSegment : MonoBehaviour
    {
        private const int ObstaclePoolSize = 16;
        private const int CoinPoolSize = 32;

        private readonly PooledObstacle[] obstacles = new PooledObstacle[ObstaclePoolSize];
        private readonly CoinPickup[] coins = new CoinPickup[CoinPoolSize];

        private PrototypePalette palette;
        private GameManager gameManager;
        private float segmentLength;
        private float laneWidth;
        private int obstacleCursor;
        private int coinCursor;
        private MetroRoutePhase routePhase;
        private int routeLaneMask = 7;
        private readonly GameObject[] elevatedSections = new GameObject[4];

        public float EndZ => transform.position.z + segmentLength;

        public void Initialize(
            int poolIndex,
            float length,
            float widthBetweenLanes,
            GameManager manager,
            PrototypePalette prototypePalette)
        {
            segmentLength = length;
            laneWidth = widthBetweenLanes;
            gameManager = manager;
            palette = prototypePalette;

            BuildRoad();
            SaigonScenery.BuildStreet(transform, poolIndex, segmentLength, laneWidth, palette);
            BuildPools(poolIndex);
        }

        public void Place(float startZ, int segmentSerial)
        {
            DeactivatePools();
            transform.position = new Vector3(0f, 0f, startZ);
            routePhase = MetroRoofRoute.PhaseForSegment(segmentSerial);
            routeLaneMask = MetroRoofRoute.LaneMaskForSegment(segmentSerial);
            foreach (GameObject section in elevatedSections)
                if (section != null) section.SetActive(false);
            if (routePhase != MetroRoutePhase.Street)
            {
                int index = (int)routePhase;
                if (elevatedSections[index] == null)
                    elevatedSections[index] = MetroRoofRoute.Build(transform, routePhase, segmentLength, laneWidth, palette, routeLaneMask);
                MetroRoofRoute.ApplyLaneMask(elevatedSections[index], routePhase, routeLaneMask);
                elevatedSections[index].SetActive(true);
            }
            GeneratePattern(segmentSerial);
        }

        private void BuildRoad()
        {
            GameObject road = PrototypeFactory.CreatePrimitive(
                "Road",
                PrimitiveType.Cube,
                transform,
                new Vector3(0f, -0.25f, segmentLength * 0.5f),
                new Vector3(9.6f, 0.5f, segmentLength),
                palette.Road,
                true);
            road.AddComponent<TrackSurface>();
        }

        private void BuildPools(int poolIndex)
        {
            GameObject hazardsRoot = new GameObject("Pooled Hazards");
            hazardsRoot.transform.SetParent(transform, false);

            for (int index = 0; index < obstacles.Length; index++)
            {
                GameObject obstacleObject = new GameObject($"Street Obstacle {index:00}");
                obstacleObject.transform.SetParent(hazardsRoot.transform, false);

                PooledObstacle obstacle = obstacleObject.AddComponent<PooledObstacle>();
                obstacle.Initialize(palette, poolIndex + index);
                obstacles[index] = obstacle;
            }

            GameObject coinsRoot = new GameObject("Pooled Coins");
            coinsRoot.transform.SetParent(transform, false);

            for (int index = 0; index < coins.Length; index++)
            {
                GameObject coinObject = PrototypeFactory.CreatePrimitive(
                    $"Coin {index:00}",
                    PrimitiveType.Cylinder,
                    coinsRoot.transform,
                    Vector3.zero,
                    new Vector3(0.38f, 0.075f, 0.38f),
                    palette.Coin,
                    false,
                    Quaternion.Euler(90f, 0f, 0f));

                SphereCollider trigger = coinObject.AddComponent<SphereCollider>();
                trigger.isTrigger = true;
                trigger.radius = 1.2f;

                Rigidbody rigidbody = coinObject.AddComponent<Rigidbody>();
                rigidbody.isKinematic = true;
                rigidbody.useGravity = false;

                CoinPickup pickup = coinObject.AddComponent<CoinPickup>();
                pickup.Configure(gameManager);
                coins[index] = pickup;
            }
        }

        private void GeneratePattern(int segmentSerial)
        {
            if (routePhase == MetroRoutePhase.Ascent || routePhase == MetroRoutePhase.Descent)
            {
                // Each usable ramp gets a complete coin trail, within the fixed pool budget.
                for (int lane = 0; lane < 3; lane++)
                    if (MetroRoofRoute.IsLaneOpen(routeLaneMask, lane))
                        AddCoinLine(lane, 2f, segmentLength - 2f, 3f, 0.95f);
                return;
            }
            if (routePhase == MetroRoutePhase.Rooftops)
            {
                var roofRandom = new System.Random(9137 + segmentSerial * 7919);
                bool connectedLanes = (routeLaneMask & 3) == 3 || (routeLaneMask & 6) == 6;
                // Isolated roofs require jumping, so allow a full landing/recovery between vents.
                float[] rows = connectedLanes ? new[] { 6.5f, segmentLength - 6.5f } : new[] { segmentLength * 0.5f };
                foreach (float row in rows)
                {
                    int ventLane = roofRandom.Next(3);
                    while (!MetroRoofRoute.IsLaneOpen(routeLaneMask, ventLane)) ventLane = (ventLane + 1) % 3;
                    AddObstacle(ObstacleKind.RoofVent, ventLane, row);
                    AddCoinArc(ventLane, row);
                    for (int lane = 0; lane < 3; lane++)
                        if (lane != ventLane && MetroRoofRoute.IsLaneOpen(routeLaneMask, lane))
                            AddCoinTrail(lane, row, 0.95f);
                }
                return;
            }
            if (segmentSerial <= 0)
            {
                AddCoinLine(1, 3.5f, segmentLength - 3.5f, 2.35f, 0.95f);
                return;
            }

            System.Random random = new System.Random(4283 + segmentSerial * 7919);
            // Two rows keep at least 13 metres between hazards across segment
            // boundaries, enough for an edge-to-edge lane recovery at max speed.
            const int rowCount = 2;
            float firstRow = 6.5f;
            float lastRow = segmentLength - 6.5f;

            for (int row = 0; row < rowCount; row++)
            {
                float t = rowCount == 1 ? 0.5f : row / (float)(rowCount - 1);
                float rowZ = Mathf.Lerp(firstRow, lastRow, t);

                if (row == rowCount - 1 && MetroRoofRoute.PhaseForSegment(segmentSerial + 1) == MetroRoutePhase.Ascent)
                {
                    // Remove the last traffic row so even an edge-to-edge move has a clear approach.
                    int nextMask = MetroRoofRoute.LaneMaskForSegment(segmentSerial + 1);
                    for (int lane = 0; lane < 3; lane++)
                        if (MetroRoofRoute.IsLaneOpen(nextMask, lane))
                            AddCoinLine(lane, segmentLength - 14f, segmentLength - 2f, 3f, 0.95f);
                    continue;
                }

                int pattern;
                if (segmentSerial == 2 && row == 0)
                {
                    pattern = 4; // The first explicit jump tutorial.
                }
                else if (segmentSerial == 3 && row == 0)
                {
                    pattern = 5; // The first explicit slide tutorial.
                }
                else
                {
                    // Random rows always leave a lane open. Fully spanning action
                    // rows are reserved for the deliberately spaced tutorials above.
                    int patternCount = segmentSerial < 2 ? 2 : 4;
                    pattern = random.Next(patternCount);
                }

                AddPattern(pattern, rowZ, random);
            }
        }

        private void AddPattern(int pattern, float rowZ, System.Random random)
        {
            switch (pattern)
            {
                case 0:
                {
                    int blockedLane = random.Next(3);
                    int coinLane = (blockedLane + 1 + random.Next(2)) % 3;
                    AddObstacle(ObstacleKind.Block, blockedLane, rowZ);
                    AddCoinTrail(coinLane, rowZ, 0.95f);
                    break;
                }

                case 1:
                {
                    int openLane = random.Next(3);
                    for (int lane = 0; lane < 3; lane++)
                    {
                        if (lane != openLane)
                        {
                            AddObstacle(ObstacleKind.Block, lane, rowZ);
                        }
                    }

                    AddCoinTrail(openLane, rowZ, 0.95f);
                    break;
                }

                case 2:
                {
                    int hurdleLane = random.Next(3);
                    AddObstacle(ObstacleKind.Hurdle, hurdleLane, rowZ);
                    AddCoinArc(hurdleLane, rowZ);
                    break;
                }

                case 3:
                {
                    int slideLane = random.Next(3);
                    AddObstacle(ObstacleKind.OverheadBar, slideLane, rowZ);
                    AddSlideTrail(slideLane, rowZ);
                    break;
                }

                case 4:
                    for (int lane = 0; lane < 3; lane++)
                    {
                        AddObstacle(ObstacleKind.Hurdle, lane, rowZ);
                    }

                    AddCoinArc(1, rowZ);
                    break;

                default:
                    for (int lane = 0; lane < 3; lane++)
                    {
                        AddObstacle(ObstacleKind.OverheadBar, lane, rowZ);
                    }

                    AddSlideTrail(1, rowZ);
                    break;
            }
        }

        private void AddObstacle(ObstacleKind kind, int lane, float localZ)
        {
            if (obstacleCursor >= obstacles.Length)
            {
                return;
            }

            PooledObstacle obstacle = obstacles[obstacleCursor++];
            obstacle.Activate(kind, lane, laneWidth, localZ);
            obstacle.transform.localPosition += Vector3.up * MetroRoofRoute.SurfaceHeight(routePhase, localZ, segmentLength);
        }

        private void AddCoinTrail(int lane, float centerZ, float y)
        {
            for (int index = -2; index <= 2; index++)
            {
                AddCoin(lane, y, centerZ + index * 1.25f);
            }
        }

        private void AddCoinArc(int lane, float centerZ)
        {
            for (int index = -2; index <= 2; index++)
            {
                float normalized = (index + 2) / 4f;
                float y = 0.82f + Mathf.Sin(normalized * Mathf.PI) * 1.35f;
                AddCoin(lane, y, centerZ + index * 0.85f);
            }
        }

        private void AddSlideTrail(int lane, float centerZ)
        {
            for (int index = -2; index <= 2; index++)
            {
                AddCoin(lane, 0.52f, centerZ + index * 0.78f);
            }
        }

        private void AddCoinLine(int lane, float fromZ, float toZ, float spacing, float y)
        {
            for (float z = fromZ; z <= toZ; z += spacing)
            {
                AddCoin(lane, y, z);
            }
        }

        private void AddCoin(int lane, float y, float localZ)
        {
            if (coinCursor >= coins.Length)
            {
                return;
            }

            CoinPickup coin = coins[coinCursor++];
            coin.transform.localPosition = new Vector3((lane - 1) * laneWidth,
                y + MetroRoofRoute.SurfaceHeight(routePhase, localZ, segmentLength), localZ);
            coin.gameObject.SetActive(true);
        }

        private void DeactivatePools()
        {
            foreach (PooledObstacle obstacle in obstacles)
            {
                if (obstacle != null)
                {
                    obstacle.gameObject.SetActive(false);
                }
            }

            foreach (CoinPickup coin in coins)
            {
                if (coin != null)
                {
                    coin.gameObject.SetActive(false);
                }
            }

            obstacleCursor = 0;
            coinCursor = 0;
        }
    }
}

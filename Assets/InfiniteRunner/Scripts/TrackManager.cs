using System.Collections.Generic;
using UnityEngine;

namespace InfiniteRunner
{
    public sealed class TrackManager : MonoBehaviour
    {
        [SerializeField] private int activeSegmentCount = 9;
        [SerializeField] private float segmentLength = 30f;
        [SerializeField] private float laneWidth = 2.75f;
        [SerializeField] private float recycleDistanceBehindPlayer = 12f;

        private readonly Queue<TrackSegment> activeSegments = new Queue<TrackSegment>();

        private RunnerController player;
        private GameManager gameManager;
        private PrototypePalette palette;
        private float nextSpawnZ;
        private int nextSegmentSerial;

        public MetroRoutePhase CurrentRoutePhase => player == null ? MetroRoutePhase.Street :
            MetroRoofRoute.PhaseForSegment(Mathf.FloorToInt(player.transform.position.z / segmentLength));

        public string RouteLocation
        {
            get
            {
                int serial = player == null ? 0 : Mathf.FloorToInt(player.transform.position.z / segmentLength);
                MetroRoutePhase phase = MetroRoofRoute.PhaseForSegment(serial);
                if (phase == MetroRoutePhase.Street)
                    return MetroRoofRoute.PhaseForSegment(serial + 1) == MetroRoutePhase.Ascent
                        ? "METRO AHEAD / RAMPS " + MetroRoofRoute.LaneHint(MetroRoofRoute.LaneMaskForSegment(serial + 1))
                        : "SAI GON / DISTRICT 1";
                if (phase == MetroRoutePhase.Ascent)
                    return "METRO LINE 1 / RAMPS " + MetroRoofRoute.LaneHint(MetroRoofRoute.LaneMaskForSegment(serial));
                return phase == MetroRoutePhase.Descent ? "METRO / BACK TO THE STREET" : "METRO LINE 1 / ROOFTOP RUN";
            }
        }

        public void Configure(
            RunnerController runner,
            GameManager manager,
            PrototypePalette prototypePalette)
        {
            player = runner;
            gameManager = manager;
            palette = prototypePalette;
            BuildInitialTrack();
        }

        private void BuildInitialTrack()
        {
            float firstStartZ = -segmentLength;

            for (int index = 0; index < activeSegmentCount; index++)
            {
                GameObject segmentObject = new GameObject($"Track Segment {index:00}");
                segmentObject.transform.SetParent(transform, false);

                TrackSegment segment = segmentObject.AddComponent<TrackSegment>();
                segment.Initialize(index, segmentLength, laneWidth, gameManager, palette);
                segment.Place(firstStartZ + index * segmentLength, index - 1);
                activeSegments.Enqueue(segment);
            }

            nextSpawnZ = firstStartZ + activeSegmentCount * segmentLength;
            nextSegmentSerial = activeSegmentCount - 1;
        }

        private void Update()
        {
            if (player == null || activeSegments.Count == 0)
            {
                return;
            }

            while (activeSegments.Peek().EndZ < player.transform.position.z - recycleDistanceBehindPlayer)
            {
                TrackSegment recycled = activeSegments.Dequeue();
                recycled.Place(nextSpawnZ, nextSegmentSerial++);
                nextSpawnZ += segmentLength;
                activeSegments.Enqueue(recycled);
            }
        }
    }
}

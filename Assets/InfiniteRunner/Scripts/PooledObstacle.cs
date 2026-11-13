using UnityEngine;

namespace InfiniteRunner
{
    public enum ObstacleKind { Block, Hurdle, OverheadBar, RoofVent }

    public sealed class PooledObstacle : MonoBehaviour
    {
        private readonly GameObject[] visuals = new GameObject[4];
        private PrototypePalette palette;
        private BoxCollider body;
        private BoxCollider cabin;
        private readonly BoxCollider[] scaffoldSides = new BoxCollider[2];
        private int vehicleVariant;

        public void Initialize(PrototypePalette palette, int appearanceIndex = 0)
        {
            this.palette = palette;
            vehicleVariant = Mathf.Abs(appearanceIndex) % 3;
            body = gameObject.AddComponent<BoxCollider>();
            gameObject.AddComponent<ObstacleMarker>();
            cabin = AddCollisionPart("Vehicle cabin");
            for (int side = 0; side < 2; side++)
                scaffoldSides[side] = AddCollisionPart("Scaffold side frame");
            gameObject.SetActive(false);
        }

        public void Activate(ObstacleKind kind, int lane, float laneWidth, float localZ)
        {
            transform.localPosition = new Vector3((lane - 1) * laneWidth, 0, localZ);
            transform.localScale = Vector3.one;
            for (int index = 0; index < visuals.Length; index++)
                if (visuals[index] != null) visuals[index].SetActive(false);
            cabin.enabled = false;
            foreach (BoxCollider side in scaffoldSides) side.enabled = false;

            switch (kind)
            {
                case ObstacleKind.Block:
                    // Keep the original first-contact Z while extending the car away from the runner.
                    body.center = new Vector3(0, 0.64f, StreetObstacleLibrary.VehicleCenterZ);
                    body.size = new Vector3(1.85f, 1f, StreetObstacleLibrary.VehicleLength);
                    cabin.enabled = true;
                    cabin.center = vehicleVariant == 2 ? new Vector3(0, 1.7f, 0.92f) : new Vector3(0, 1.45f, 1.35f);
                    cabin.size = vehicleVariant == 2 ? new Vector3(1.66f, 1.30f, 2.75f) : new Vector3(1.38f, 0.7f, 1.8f);
                    break;
                case ObstacleKind.Hurdle:
                case ObstacleKind.RoofVent:
                    body.center = new Vector3(0, 0.43f, 0);
                    body.size = new Vector3(2.05f, 0.86f, 0.7f);
                    break;
                case ObstacleKind.OverheadBar:
                    body.center = new Vector3(0, 1.68f, 0);
                    body.size = new Vector3(2.1f, 0.5f, 1.05f);
                    for (int side = 0; side < scaffoldSides.Length; side++)
                    {
                        scaffoldSides[side].enabled = true;
                        scaffoldSides[side].center = new Vector3(side == 0 ? -1.1f : 1.1f, 0.97f, 0);
                        scaffoldSides[side].size = new Vector3(0.13f, 1.94f, 1.01f);
                    }
                    break;
            }

            int visualIndex = (int)kind;
            if (visuals[visualIndex] == null)
                visuals[visualIndex] = palette.StreetObjects.Create(transform, kind, vehicleVariant);
            visuals[visualIndex].SetActive(true);
            gameObject.SetActive(true);
        }

        private BoxCollider AddCollisionPart(string partName)
        {
            var part = new GameObject(partName);
            part.transform.SetParent(transform, false);
            part.AddComponent<ObstacleMarker>();
            BoxCollider collider = part.AddComponent<BoxCollider>();
            collider.enabled = false;
            return collider;
        }
    }
}

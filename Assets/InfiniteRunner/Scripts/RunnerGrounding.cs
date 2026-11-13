using UnityEngine;

namespace InfiniteRunner
{
    public static class RunnerGrounding
    {
        private static readonly RaycastHit[] GroundHits = new RaycastHit[16];

        // Called only when the runner was grounded and has not started a jump.
        public static bool TrySnapToSurface(CharacterController controller, float maximumDrop)
        {
            const float probeLift = 0.35f;
            Vector3 feet = controller.transform.TransformPoint(controller.center - Vector3.up * (controller.height * 0.5f));
            int count = Physics.RaycastNonAlloc(feet + Vector3.up * probeLift, Vector3.down, GroundHits,
                maximumDrop + probeLift, ~0, QueryTriggerInteraction.Ignore);
            float closest = float.PositiveInfinity;
            int nearest = -1;
            for (int i = 0; i < count; i++)
            {
                Collider hit = GroundHits[i].collider;
                if (hit == controller || hit.transform.IsChildOf(controller.transform)) continue;
                if (GroundHits[i].distance < closest) { nearest = i; closest = GroundHits[i].distance; }
            }
            if (nearest < 0) return false;
            RaycastHit ground = GroundHits[nearest];
            // The nearest collider must be walkable: never snap through a hazard to a road below it.
            if (ground.collider.GetComponent<TrackSurface>() == null ||
                ground.normal.y < Mathf.Cos(controller.slopeLimit * Mathf.Deg2Rad)) return false;
            float drop = ground.distance - probeLift;
            if (drop < -controller.skinWidth || drop > maximumDrop) return false;
            CollisionFlags flags = controller.Move(Vector3.down * Mathf.Max(0.02f, drop + controller.skinWidth));
            return (flags & CollisionFlags.Below) != 0;
        }
    }
}

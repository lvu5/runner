#if UNITY_EDITOR
using UnityEngine;

namespace InfiniteRunner.Editor
{
    [ExecuteAlways]
    public sealed class MetroRouteContactProbe : MonoBehaviour
    {
        public bool HitHazard { get; private set; }
        public string LastHazard { get; private set; }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (hit.collider.GetComponent<ObstacleMarker>() == null) return;
            HitHazard = true;
            LastHazard = hit.collider.name;
        }
    }
}
#endif

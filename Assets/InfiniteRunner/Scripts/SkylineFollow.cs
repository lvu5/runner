using UnityEngine;

namespace InfiniteRunner
{
    public sealed class SkylineFollow : MonoBehaviour
    {
        private Transform runner;
        public void Configure(Transform target) { runner = target; }
        private void LateUpdate()
        {
            if (runner != null) transform.position = new Vector3(0, 0, runner.position.z);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace InfiniteRunner
{
    public sealed class SceneryMeshOwner : MonoBehaviour
    {
        public readonly List<Mesh> Meshes = new List<Mesh>();
        private void OnDestroy()
        {
            foreach (Mesh mesh in Meshes)
            {
                if (mesh == null) continue;
                if (Application.isPlaying) Destroy(mesh);
                else DestroyImmediate(mesh);
            }
        }
    }
}

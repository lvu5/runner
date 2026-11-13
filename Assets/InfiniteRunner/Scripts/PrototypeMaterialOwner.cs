using UnityEngine;

namespace InfiniteRunner
{
    public sealed class PrototypeMaterialOwner : MonoBehaviour
    {
        private PrototypePalette palette;

        public void Configure(PrototypePalette ownedPalette)
        {
            palette = ownedPalette;
        }

        private void OnDestroy()
        {
            palette?.Dispose();
            palette = null;
        }
    }
}


using Karmaan.Game;
using UnityEngine;

namespace Karmaan.Bootstrap
{
    /// <summary>
    /// The only component in the boot scene. GameRoot initialises in Awake, then this starts the
    /// app in Start, so service setup is always finished before the first screen is built.
    ///
    /// Keeping the scene to two components means the scene file stays trivial — nothing to
    /// silently break, nothing to merge-conflict.
    /// </summary>
    public sealed class AppLauncher : MonoBehaviour
    {
        private void Start()
        {
            if (GameRoot.Instance == null)
            {
                Debug.LogError("[AppLauncher] No GameRoot in the scene. Run Karmaan > Set up project.");
                return;
            }

            if (AppFlow.Instance != null) return;

            AppFlow.Create(GameRoot.Instance);
        }
    }
}

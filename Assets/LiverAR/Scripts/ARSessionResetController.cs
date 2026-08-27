using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace LiverAR.Runtime
{
    public sealed class ARSessionResetController : MonoBehaviour
    {
        [SerializeField] ARSession session;
        [SerializeField] ARPlacementController placementController;

        public void ResetSession()
        {
            if (placementController != null)
                placementController.ResetPlacement();

            if (session != null)
                session.Reset();
        }
    }
}

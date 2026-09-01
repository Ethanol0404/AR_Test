using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace LiverAR.Runtime
{
    public sealed class ARSessionResetController : MonoBehaviour
    {
        [SerializeField] ARSession session;
        [SerializeField] ARPlacementController placementController;
        [SerializeField] LiverModelWorkspace modelWorkspace;

        public void ResetSession()
        {
            if (placementController != null)
                placementController.ResetPlacement();

            if (modelWorkspace == null)
                modelWorkspace = FindAnyObjectByType<LiverModelWorkspace>();
            modelWorkspace?.ClearAllModels();

            if (session != null)
                session.Reset();
        }
    }
}

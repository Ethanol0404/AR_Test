using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace LiverAR.Runtime
{
    public sealed class ARStatusController : MonoBehaviour
    {
        [SerializeField] ARUIController uiController;
        [SerializeField] ARPlacementController placementController;
        [SerializeField] ARPlaneManager planeManager;
        [SerializeField] float virtualFallbackDelaySeconds = 8f;

        ARSurfaceState currentState;
        float searchStartedAt;
        bool hasDetectedPlane;
        bool hasState;

        void OnEnable()
        {
            AutoWireMissingReferences();
            ARSession.stateChanged += OnStateChanged;
            if (planeManager != null)
                planeManager.trackablesChanged.AddListener(OnPlanesChanged);
            if (placementController != null)
            {
                placementController.ModelPlaced += OnModelPlaced;
                placementController.PlacementReset += OnPlacementReset;
                placementController.PlacementAvailabilityChanged += OnPlacementAvailabilityChanged;
            }

            searchStartedAt = Time.time;
            SetState(Application.isEditor ? ARSurfaceState.VirtualMode : ARSurfaceState.Initialising);
        }

        void OnDisable()
        {
            ARSession.stateChanged -= OnStateChanged;
            if (planeManager != null)
                planeManager.trackablesChanged.RemoveListener(OnPlanesChanged);
            if (placementController != null)
            {
                placementController.ModelPlaced -= OnModelPlaced;
                placementController.PlacementReset -= OnPlacementReset;
                placementController.PlacementAvailabilityChanged -= OnPlacementAvailabilityChanged;
            }
        }

        void Update()
        {
            if (placementController != null && placementController.HasPlacedModel)
                return;

            if (!Application.isEditor && !hasDetectedPlane && Time.time - searchStartedAt >= virtualFallbackDelaySeconds)
                uiController?.SetVirtualSurfaceOptionVisible(true);
        }

        void OnStateChanged(ARSessionStateChangedEventArgs args)
        {
            if (Application.isEditor)
            {
                SetState(ARSurfaceState.VirtualMode);
                return;
            }

            switch (args.state)
            {
                case ARSessionState.Unsupported:
                    SetState(ARSurfaceState.Unsupported);
                    break;
                case ARSessionState.NeedsInstall:
                    SetState(ARSurfaceState.Unsupported);
                    break;
                case ARSessionState.SessionTracking:
                    SetState(hasDetectedPlane ? ARSurfaceState.Ready : ARSurfaceState.Searching);
                    break;
                case ARSessionState.SessionInitializing:
                    searchStartedAt = Time.time;
                    SetState(ARSurfaceState.Searching);
                    break;
            }
        }

        void OnPlanesChanged(ARTrackablesChangedEventArgs<ARPlane> args)
        {
            if (placementController != null && placementController.HasPlacedModel)
                return;

            if (args.added.Count == 0 && args.updated.Count == 0)
                return;

            hasDetectedPlane = true;
            uiController?.SetVirtualSurfaceOptionVisible(false);
            SetState(placementController != null && placementController.CanPlaceLiver ? ARSurfaceState.Ready : ARSurfaceState.SurfaceDetected);
        }

        void OnPlacementAvailabilityChanged(bool canPlace)
        {
            if (placementController != null && placementController.HasPlacedModel)
                return;

            if (placementController != null && placementController.IsUsingVirtualSurface)
                SetState(ARSurfaceState.VirtualMode);
            else if (canPlace)
                SetState(ARSurfaceState.Ready);
        }

        void OnModelPlaced(GameObject model)
        {
            SetState(ARSurfaceState.Placed);
            uiController?.SetVirtualSurfaceOptionVisible(false);
        }

        void OnPlacementReset()
        {
            hasDetectedPlane = false;
            searchStartedAt = Time.time;
            SetState(Application.isEditor ? ARSurfaceState.VirtualMode : ARSurfaceState.Searching);
        }

        void SetState(ARSurfaceState state)
        {
            if (hasState && currentState == state)
                return;

            currentState = state;
            hasState = true;
            uiController?.SetSurfaceState(state);
        }

        void AutoWireMissingReferences()
        {
            if (uiController == null)
                uiController = FindAnyObjectByType<ARUIController>();
            if (placementController == null)
                placementController = FindAnyObjectByType<ARPlacementController>();
            if (planeManager == null)
                planeManager = FindAnyObjectByType<ARPlaneManager>();
        }
    }
}

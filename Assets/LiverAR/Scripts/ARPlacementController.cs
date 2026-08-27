using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace LiverAR.Runtime
{
    public sealed class ARPlacementController : MonoBehaviour
    {
        [SerializeField] ARRaycastManager raycastManager;
        [SerializeField] ARPlaneManager planeManager;
        [SerializeField] GameObject modelPrefab;
        [SerializeField] GameObject diseaseModelPrefab;
        [SerializeField] ModelInteractionController interactionController;
        [SerializeField] AnatomyManager anatomyManager;
        [SerializeField] Camera arCamera;
        [SerializeField] float editorPlacementDistance = 1.4f;
        [SerializeField] float virtualSurfaceDistance = 1.2f;
        [SerializeField] GameObject virtualSurfaceVisual;

        static readonly List<ARRaycastHit> Hits = new List<ARRaycastHit>();
        static readonly string[] ExpectedSegmentIds =
        {
            "segment-1",
            "segment-2",
            "segment-3",
            "segment-4",
            "segment-5",
            "segment-6",
            "segment-7",
            "segment-8"
        };

        GameObject placedModel;
        GameObject normalModelInstance;
        GameObject diseaseModelInstance;
        Pose? pendingPose;
        bool virtualSurfaceEnabled;
        LiverModelType selectedModelType = LiverModelType.Normal;

        public event Action<GameObject> ModelPlaced;
        public event Action PlacementReset;
        public event Action<bool> PlacementAvailabilityChanged;
        public bool HasPlacedModel => placedModel != null;
        public bool CanPlaceLiver => !HasPlacedModel && (pendingPose.HasValue || virtualSurfaceEnabled || Application.isEditor);
        public bool IsUsingVirtualSurface => virtualSurfaceEnabled;
        public bool HasDiseaseModel => diseaseModelPrefab != null;
        public string LastPlacementFailureMessage { get; private set; } = string.Empty;

        void OnEnable()
        {
            TouchInput.Enable();
            AutoWireMissingReferences();
            SetVirtualSurfaceVisible(false);
        }

        void Update()
        {
            UpdatePlacementPose();

            if (HasPlacedModel || !TouchInput.TryGetPrimaryPointer(out var pointer) || TouchInput.IsPointerOverUi(pointer))
                return;

            if (TouchInput.IsBegan(pointer) && TryGetPlacementPose(pointer.ScreenPosition, out var pose))
                pendingPose = pose;
        }

        public void Place(Pose pose)
        {
            if (modelPrefab == null)
            {
                LastPlacementFailureMessage = "Liver model is not assigned.";
                Debug.LogError(LastPlacementFailureMessage);
                return;
            }

            try
            {
                LastPlacementFailureMessage = string.Empty;
                if (placedModel != null)
                    Destroy(placedModel);

                placedModel = new GameObject("Placed Liver Model");
                placedModel.transform.SetPositionAndRotation(pose.position, pose.rotation);
                normalModelInstance = Instantiate(modelPrefab, placedModel.transform);
                normalModelInstance.name = "Normal Liver";
                if (diseaseModelPrefab != null)
                {
                    diseaseModelInstance = Instantiate(diseaseModelPrefab, placedModel.transform);
                    diseaseModelInstance.name = "Disease Liver";
                }
                ReportMissingSegmentReferences(normalModelInstance);
                ApplySelectedModel();
                AlignVesselToLiver(normalModelInstance.transform);
                FitPlacedModelIntoCameraView();

                if (interactionController != null)
                {
                    interactionController.ModelRoot = placedModel.transform;
                    interactionController.CaptureOriginalTransform();
                }
                RefreshAnatomyReferences();

                SetPlaneVisuals(false);
                SetVirtualSurfaceVisible(false);
                Debug.Log($"Liver placed at {placedModel.transform.position} with {placedModel.GetComponentsInChildren<AnatomyPart>(true).Length} anatomy parts.");
                ModelPlaced?.Invoke(placedModel);
                PlacementAvailabilityChanged?.Invoke(false);
            }
            catch (Exception exception)
            {
                LastPlacementFailureMessage = "Liver placement failed. Please try again.";
                Debug.LogException(exception);
                if (placedModel != null)
                    Destroy(placedModel);
                placedModel = null;
                normalModelInstance = null;
                diseaseModelInstance = null;
            }
        }

        public bool SwitchModel(LiverModelType modelType)
        {
            if (modelType == LiverModelType.Disease && diseaseModelPrefab == null && diseaseModelInstance == null)
            {
                Debug.LogWarning("Disease model not yet assigned.");
                return false;
            }

            selectedModelType = modelType;
            ApplySelectedModel();
            RefreshAnatomyReferences();
            return true;
        }

        public bool PlaceLiver()
        {
            var hasPose = TryGetPlacementPose(GetScreenCenter(), out var pose);
            if (!hasPose && pendingPose.HasValue)
            {
                pose = pendingPose.Value;
                hasPose = true;
            }

            if (!hasPose)
            {
                pose = BuildPoseInFrontOfCamera(editorPlacementDistance);
                hasPose = true;
                Debug.Log("No AR plane pose was available. Using a safe default camera-relative placement.");
            }

            Place(pose);
            return HasPlacedModel;
        }

        public void UseVirtualSurface()
        {
            virtualSurfaceEnabled = true;
            pendingPose = BuildPoseInFrontOfCamera(virtualSurfaceDistance);
            SetVirtualSurfaceVisible(true);
            PlacementAvailabilityChanged?.Invoke(CanPlaceLiver);
        }

        public void ResetPlacement()
        {
            if (placedModel != null)
                Destroy(placedModel);

            placedModel = null;
            normalModelInstance = null;
            diseaseModelInstance = null;
            anatomyManager?.SetConfiguredParts(null);
            pendingPose = null;
            virtualSurfaceEnabled = false;
            SetPlaneVisuals(true);
            SetVirtualSurfaceVisible(false);
            PlacementReset?.Invoke();
            PlacementAvailabilityChanged?.Invoke(CanPlaceLiver);
        }

        bool TryGetPlacementPose(Vector2 screenPosition, out Pose pose)
        {
            if (!virtualSurfaceEnabled && raycastManager != null && raycastManager.Raycast(screenPosition, Hits, TrackableType.PlaneWithinPolygon))
            {
                pose = Hits[0].pose;
                return true;
            }

            if (virtualSurfaceEnabled)
            {
                pose = BuildPoseInFrontOfCamera(virtualSurfaceDistance);
                return true;
            }

            if (Application.isEditor)
            {
                pose = BuildPoseInFrontOfCamera(editorPlacementDistance);
                return true;
            }

            pose = default;
            return false;
        }

        void UpdatePlacementPose()
        {
            var canPlaceBefore = CanPlaceLiver;
            if (HasPlacedModel)
            {
                pendingPose = null;
            }
            else if (virtualSurfaceEnabled)
            {
                pendingPose = BuildPoseInFrontOfCamera(virtualSurfaceDistance);
            }
            else if (Application.isEditor)
            {
                pendingPose = BuildPoseInFrontOfCamera(editorPlacementDistance);
            }

            if (canPlaceBefore != CanPlaceLiver)
                PlacementAvailabilityChanged?.Invoke(CanPlaceLiver);
        }

        Pose BuildPoseInFrontOfCamera(float distance)
        {
            var cameraTransform = arCamera != null ? arCamera.transform : Camera.main != null ? Camera.main.transform : transform;
            var forward = cameraTransform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.forward;
            forward.Normalize();

            var position = cameraTransform.position + forward * distance;
            position.y = Mathf.Min(cameraTransform.position.y - 0.35f, 0f);
            return new Pose(position, Quaternion.LookRotation(forward, Vector3.up));
        }

        void FitPlacedModelIntoCameraView()
        {
            if (placedModel == null)
                return;

            var cameraTransform = arCamera != null ? arCamera.transform : Camera.main != null ? Camera.main.transform : null;
            if (cameraTransform == null)
                return;

            var pose = BuildPoseInFrontOfCamera(editorPlacementDistance);
            placedModel.transform.rotation = pose.rotation;

            if (!TryGetCombinedRendererBounds(placedModel, out var bounds))
            {
                LastPlacementFailureMessage = "Liver model could not be displayed.";
                return;
            }

            var maxSize = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            if (maxSize > 0.001f)
            {
                var desiredMaxSize = 0.42f;
                var scaleFactor = Mathf.Clamp(desiredMaxSize / maxSize, 0.05f, 20f);
                placedModel.transform.localScale *= scaleFactor;
            }

            if (!TryGetCombinedRendererBounds(placedModel, out bounds))
                return;

            var targetCenter = cameraTransform.position + cameraTransform.forward.normalized * editorPlacementDistance;
            targetCenter += cameraTransform.up * -0.08f;
            placedModel.transform.position += targetCenter - bounds.center;
        }

        public bool TryValidatePlacedModelVisibility(out string reason)
        {
            return TryValidateVisibleModel(placedModel, arCamera != null ? arCamera : Camera.main, out reason);
        }

        public static bool TryValidateVisibleModel(GameObject model, Camera camera, out string reason)
        {
            reason = string.Empty;

            if (model == null)
            {
                reason = "Liver placement failed. Please try again.";
                return false;
            }

            if (!model.activeInHierarchy)
            {
                reason = "Liver placement failed. Please try again.";
                return false;
            }

            if (!TryGetCombinedRendererBounds(model, out var bounds))
            {
                reason = "Liver model could not be displayed.";
                return false;
            }

            if (bounds.size.sqrMagnitude < 0.000001f || model.transform.lossyScale.sqrMagnitude < 0.000001f)
            {
                reason = "Liver model could not be displayed.";
                return false;
            }

            if (camera == null)
                return true;

            var renderers = model.GetComponentsInChildren<Renderer>(true);
            var hasVisibleLayer = false;
            foreach (var renderer in renderers)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    continue;
                if ((camera.cullingMask & (1 << renderer.gameObject.layer)) != 0)
                {
                    hasVisibleLayer = true;
                    break;
                }
            }

            if (!hasVisibleLayer)
            {
                reason = "Liver model could not be displayed.";
                return false;
            }

            if (!TryGetViewportCoverage(bounds, camera, out var hasPointInFront, out var hasPointInsideView))
            {
                reason = hasPointInFront ? "Unable to place liver in the current view." : "Liver model must be in front of the camera.";
                return false;
            }

            return true;
        }

        static bool TryGetViewportCoverage(Bounds bounds, Camera camera, out bool hasPointInFront, out bool hasPointInsideView)
        {
            hasPointInFront = false;
            hasPointInsideView = false;
            var min = bounds.min;
            var max = bounds.max;
            var points = new[]
            {
                bounds.center,
                new Vector3(min.x, min.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z)
            };

            foreach (var point in points)
            {
                var viewport = camera.WorldToViewportPoint(point);
                if (viewport.z <= camera.nearClipPlane)
                    continue;

                hasPointInFront = true;
                if (viewport.x >= -0.15f && viewport.x <= 1.15f && viewport.y >= -0.15f && viewport.y <= 1.15f)
                    hasPointInsideView = true;
            }

            return hasPointInFront && hasPointInsideView;
        }

        static bool TryGetCombinedRendererBounds(GameObject root, out Bounds bounds)
        {
            bounds = default;
            if (root == null)
                return false;

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var hasBounds = false;
            foreach (var renderer in renderers)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    continue;

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds;
        }

        Vector2 GetScreenCenter()
        {
            if (arCamera != null)
                return new Vector2(arCamera.pixelWidth * 0.5f, arCamera.pixelHeight * 0.5f);

            return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        }

        void SetVirtualSurfaceVisible(bool visible)
        {
            if (virtualSurfaceVisual != null)
            {
                if (visible && pendingPose.HasValue)
                    virtualSurfaceVisual.transform.SetPositionAndRotation(pendingPose.Value.position, Quaternion.Euler(90f, 0f, 0f));
                virtualSurfaceVisual.SetActive(visible);
            }
        }

        void ApplySelectedModel()
        {
            if (normalModelInstance != null)
                normalModelInstance.SetActive(selectedModelType == LiverModelType.Normal);
            if (diseaseModelInstance != null)
                diseaseModelInstance.SetActive(selectedModelType == LiverModelType.Disease);
        }

        void RefreshAnatomyReferences()
        {
            if (anatomyManager == null || placedModel == null)
                return;

            var activeModel = selectedModelType == LiverModelType.Disease && diseaseModelInstance != null ? diseaseModelInstance : normalModelInstance;
            var parts = activeModel != null ? activeModel.GetComponentsInChildren<AnatomyPart>(true) : placedModel.GetComponentsInChildren<AnatomyPart>(true);
            anatomyManager.SetConfiguredParts(parts);
            anatomyManager.ClearSelection();
            anatomyManager.ShowWholeLiverOverview();
        }

        static void AlignVesselToLiver(Transform modelRoot)
        {
            if (modelRoot == null)
                return;

            AnatomyPart liver = null;
            AnatomyPart vessel = null;
            foreach (var part in modelRoot.GetComponentsInChildren<AnatomyPart>(true))
            {
                if (part.Category == AnatomyCategory.WholeLiver)
                    liver = part;
                else if (part.Category == AnatomyCategory.Vessel)
                    vessel = part;
            }

            if (liver == null || vessel == null || liver.Renderers.Length == 0 || vessel.Renderers.Length == 0)
                return;

            var liverBounds = liver.Renderers[0].bounds;
            foreach (var renderer in liver.Renderers)
                if (renderer != null) liverBounds.Encapsulate(renderer.bounds);

            var vesselBounds = vessel.Renderers[0].bounds;
            foreach (var renderer in vessel.Renderers)
                if (renderer != null) vesselBounds.Encapsulate(renderer.bounds);

            vessel.transform.position += liverBounds.center - vesselBounds.center;
        }

        static void ReportMissingSegmentReferences(GameObject modelRoot)
        {
            if (modelRoot == null)
                return;

            var parts = modelRoot.GetComponentsInChildren<AnatomyPart>(true);
            foreach (var expectedId in ExpectedSegmentIds)
            {
                var found = false;
                foreach (var part in parts)
                {
                    if (part != null && part.StructureId == expectedId)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                    Debug.LogWarning($"Missing imported Couinaud segment reference: {expectedId}. Assign the existing segment GameObject or convert the source model to a Unity-renderable mesh.");
            }
        }

        void SetPlaneVisuals(bool enabled)
        {
            if (planeManager == null)
                return;

            foreach (var plane in planeManager.trackables)
                plane.gameObject.SetActive(enabled);

            planeManager.requestedDetectionMode = enabled ? PlaneDetectionMode.Horizontal : PlaneDetectionMode.None;
        }

        void AutoWireMissingReferences()
        {
            if (arCamera == null)
                arCamera = Camera.main;
            if (anatomyManager == null)
                anatomyManager = FindAnyObjectByType<AnatomyManager>();
            if (interactionController == null)
                interactionController = FindAnyObjectByType<ModelInteractionController>();
            if (virtualSurfaceVisual == null)
                virtualSurfaceVisual = CreateRuntimeVirtualSurface();
        }

        static GameObject CreateRuntimeVirtualSurface()
        {
            var surface = GameObject.CreatePrimitive(PrimitiveType.Quad);
            surface.name = "Virtual Placement Surface";
            surface.transform.localScale = new Vector3(0.8f, 0.8f, 1f);
            var renderer = surface.GetComponent<Renderer>();
            if (renderer != null)
            {
                var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                material.color = new Color(0.25f, 0.65f, 1f, 0.28f);
                renderer.sharedMaterial = material;
            }
            var collider = surface.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);
            surface.SetActive(false);
            return surface;
        }

    }
}

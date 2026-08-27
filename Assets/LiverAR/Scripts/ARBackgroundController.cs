using System;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace LiverAR.Runtime
{
    public sealed class ARBackgroundController : MonoBehaviour
    {
        const string CameraBackgroundPreferenceKey = "LiverAR.CameraBackground";

        [SerializeField] Camera targetCamera;
        [SerializeField] ARCameraBackground arCameraBackground;
        [SerializeField] ARPlaneManager planeManager;
        [SerializeField] Color virtualWorkspaceColor = new Color(0.12f, 0.16f, 0.18f, 1f);

        CameraClearFlags originalClearFlags;
        Color originalBackgroundColor;
        bool originalBackgroundEnabled;
        bool hasCapturedOriginalState;

        public event Action<string> StatusMessageChanged;
        public bool IsCameraBackgroundEnabled => arCameraBackground != null && arCameraBackground.enabled;

        void Awake()
        {
            AutoWireMissingReferences();
            CaptureOriginalState();

            var preferCamera = PlayerPrefs.GetInt(CameraBackgroundPreferenceKey, Application.isEditor ? 0 : 1) == 1;
            SetCameraBackgroundEnabled(preferCamera);
        }

        public bool SetCameraBackgroundEnabled(bool enabled)
        {
            AutoWireMissingReferences();
            CaptureOriginalState();

            if (enabled && (Application.isEditor || arCameraBackground == null))
            {
                ApplyVirtualWorkspace();
                PlayerPrefs.SetInt(CameraBackgroundPreferenceKey, 0);
                PlayerPrefs.Save();
                StatusMessageChanged?.Invoke("Camera background unavailable. Virtual environment is active.");
                return false;
            }

            if (enabled)
            {
                RestoreCameraBackground();
                PlayerPrefs.SetInt(CameraBackgroundPreferenceKey, 1);
                PlayerPrefs.Save();
                StatusMessageChanged?.Invoke(string.Empty);
                return true;
            }

            ApplyVirtualWorkspace();
            PlayerPrefs.SetInt(CameraBackgroundPreferenceKey, 0);
            PlayerPrefs.Save();
            StatusMessageChanged?.Invoke("Virtual environment active.");
            return true;
        }

        public void ConfigureForTests(Camera camera, ARCameraBackground background)
        {
            targetCamera = camera;
            arCameraBackground = background;
            CaptureOriginalState();
        }

        void ApplyVirtualWorkspace()
        {
            if (arCameraBackground != null)
                arCameraBackground.enabled = false;

            if (targetCamera != null)
            {
                targetCamera.clearFlags = CameraClearFlags.SolidColor;
                targetCamera.backgroundColor = virtualWorkspaceColor;
            }

            SetPlaneVisuals(false);
        }

        void RestoreCameraBackground()
        {
            if (targetCamera != null)
            {
                targetCamera.clearFlags = originalClearFlags;
                targetCamera.backgroundColor = originalBackgroundColor;
            }

            if (arCameraBackground != null)
                arCameraBackground.enabled = originalBackgroundEnabled || !Application.isEditor;

            SetPlaneVisuals(true);
        }

        void CaptureOriginalState()
        {
            if (hasCapturedOriginalState || targetCamera == null)
                return;

            originalClearFlags = targetCamera.clearFlags;
            originalBackgroundColor = targetCamera.backgroundColor;
            originalBackgroundEnabled = arCameraBackground == null || arCameraBackground.enabled;
            hasCapturedOriginalState = true;
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
            if (targetCamera == null)
                targetCamera = Camera.main;
            if (arCameraBackground == null && targetCamera != null)
                arCameraBackground = targetCamera.GetComponent<ARCameraBackground>();
            if (planeManager == null)
                planeManager = FindAnyObjectByType<ARPlaneManager>();
        }
    }
}

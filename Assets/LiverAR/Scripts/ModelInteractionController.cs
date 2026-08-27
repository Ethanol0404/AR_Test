using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation;

namespace LiverAR.Runtime
{
    public sealed class ModelInteractionController : MonoBehaviour
    {
        [SerializeField] Transform modelRoot;
        [SerializeField] ARRaycastManager raycastManager;
        [SerializeField] Camera arCamera;
        [SerializeField] float minScale = 0.05f;
        [SerializeField] float maxScale = 2f;
        [SerializeField] float rotateDegreesPerPixel = 0.18f;
        [SerializeField] float minCameraDistance = 0.35f;
        [SerializeField] float maxCameraDistance = 3f;
        [SerializeField] float minVerticalOffset = -0.6f;
        [SerializeField] float maxVerticalOffset = 0.8f;
        [SerializeField] float moveMetersPerPixel = 0.0015f;
        [SerializeField] float depthMetersPerPixel = 0.002f;
        [SerializeField] LiverARSettings settings = new LiverARSettings();

        Vector3 originalPosition;
        Quaternion originalRotation;
        Vector3 originalScale;
        float previousPinchDistance;

        public Transform ModelRoot
        {
            get => modelRoot;
            set => modelRoot = value;
        }

        public static float ClampScale(float value, float min, float max)
        {
            return Mathf.Clamp(value, min, max);
        }

        public static float ClampDistance(float value, float min, float max)
        {
            return Mathf.Clamp(value, min, max);
        }

        public static float ClampVertical(float value, float min, float max)
        {
            return Mathf.Clamp(value, min, max);
        }

        void OnEnable()
        {
            TouchInput.Enable();
        }

        public void CaptureOriginalTransform()
        {
            if (modelRoot == null)
                return;

            originalPosition = modelRoot.position;
            originalRotation = modelRoot.rotation;
            originalScale = modelRoot.localScale;
        }

        public void ResetTransform()
        {
            if (modelRoot == null)
                return;

            modelRoot.SetPositionAndRotation(originalPosition, originalRotation);
            modelRoot.localScale = originalScale;
        }

        public void TranslateCameraRelative(Vector2 deltaPixels, Camera camera)
        {
            var targetCamera = ResolveCamera(camera);
            if (modelRoot == null || targetCamera == null)
                return;

            var movement = (targetCamera.transform.right * deltaPixels.x + targetCamera.transform.up * deltaPixels.y)
                * moveMetersPerPixel * settings.InteractionSensitivity;
            SetConstrainedPosition(modelRoot.position + movement, targetCamera);
        }

        public void AdjustDepth(float deltaPixels, Camera camera)
        {
            var targetCamera = ResolveCamera(camera);
            if (modelRoot == null || targetCamera == null)
                return;

            var movement = targetCamera.transform.forward * deltaPixels * depthMetersPerPixel * settings.InteractionSensitivity;
            SetConstrainedPosition(modelRoot.position + movement, targetCamera);
        }

        void Update()
        {
            var touches = TouchInput.ActiveTouches;
            if (modelRoot == null)
                return;

            if (touches.Count >= 2)
            {
                if (TouchInput.IsAnyTouchOverUi())
                    return;

                HandlePinch();
                return;
            }

            previousPinchDistance = 0f;
            if (TouchInput.TryGetPrimaryPointer(out var pointer) && !TouchInput.IsPointerOverUi(pointer))
                HandleSinglePointer(pointer);

            HandleEditorScale();
        }

        void HandleSinglePointer(TouchInput.PointerInput pointer)
        {
            if (!TouchInput.IsMoved(pointer))
                return;

            TranslateCameraRelative(pointer.Delta, arCamera);
        }

        void HandlePinch()
        {
            var touches = TouchInput.ActiveTouches;
            var distance = Vector2.Distance(touches[0].screenPosition, touches[1].screenPosition);

            if (previousPinchDistance <= 0f)
            {
                previousPinchDistance = distance;
                return;
            }

            var ratio = distance / previousPinchDistance;
            var current = modelRoot.localScale.x;
            var adjustedRatio = 1f + ((ratio - 1f) * settings.ScaleSensitivity);
            var next = ClampScale(current * adjustedRatio, minScale, maxScale);
            modelRoot.localScale = Vector3.one * next;
            previousPinchDistance = distance;
        }

        void HandleEditorScale()
        {
            var mouse = Mouse.current;
            if (mouse == null)
                return;

            var wheel = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(wheel) < 0.01f)
                return;

            var current = modelRoot.localScale.x;
            var next = ClampScale(current + wheel * 0.0005f * settings.ScaleSensitivity, minScale, maxScale);
            modelRoot.localScale = Vector3.one * next;
        }

        Camera ResolveCamera(Camera camera)
        {
            return camera != null ? camera : arCamera != null ? arCamera : Camera.main;
        }

        void SetConstrainedPosition(Vector3 position, Camera camera)
        {
            var cameraTransform = camera.transform;
            var verticalOffset = position.y - cameraTransform.position.y;
            position.y = cameraTransform.position.y + ClampVertical(verticalOffset, minVerticalOffset, maxVerticalOffset);

            var relative = position - cameraTransform.position;
            var forwardDistance = Vector3.Dot(relative, cameraTransform.forward);
            var clampedForwardDistance = ClampDistance(forwardDistance, minCameraDistance, maxCameraDistance);
            var horizontalForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up);

            if (horizontalForward.sqrMagnitude > 0.000001f)
                position += horizontalForward * ((clampedForwardDistance - forwardDistance) / horizontalForward.sqrMagnitude);
            else
                position += cameraTransform.forward * (clampedForwardDistance - forwardDistance);

            modelRoot.position = position;
        }

        public void ApplySettings(LiverARSettings nextSettings)
        {
            settings = nextSettings ?? LiverARSettings.CreateDefault();
        }
    }
}

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
        [SerializeField] float yawDegreesPerPixel = 0.12f;
        [SerializeField] float pitchDegreesPerPixel = 0.10f;
        [SerializeField] float pinchDeadZonePixels = 2f;
        [SerializeField] float rotationDeadZonePixels = 1.5f;
        [SerializeField] float maxPitchDeltaPerFrame = 5f;
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

                HandleMultiTouch();
                return;
            }

            HandleEditorScale();
        }

        public void HandleMultiTouch()
        {
            var touches = TouchInput.ActiveTouches;
            if (touches.Count < 2 || TouchInput.IsAnyTouchOverUi())
                return;

            ApplyTwoFingerTransform(
                touches[0].screenPosition - touches[0].delta,
                touches[1].screenPosition - touches[1].delta,
                touches[0].screenPosition,
                touches[1].screenPosition,
                arCamera);
        }

        public void ApplyTwoFingerTransform(Vector2 previousFirst, Vector2 previousSecond, Vector2 currentFirst, Vector2 currentSecond, Camera camera)
        {
            var targetCamera = ResolveCamera(camera);
            if (modelRoot == null || targetCamera == null)
                return;

            var previousDistance = Vector2.Distance(previousFirst, previousSecond);
            var currentDistance = Vector2.Distance(currentFirst, currentSecond);
            var distanceDelta = currentDistance - previousDistance;
            if (Mathf.Abs(distanceDelta) >= pinchDeadZonePixels)
            {
                var ratio = currentDistance / Mathf.Max(previousDistance, 0.001f);
                var current = modelRoot.localScale.x;
                var adjustedRatio = 1f + ((ratio - 1f) * settings.ScaleSensitivity);
                var next = ClampScale(current * adjustedRatio, minScale, maxScale);
                modelRoot.localScale = Vector3.one * next;
            }

            var previousMidpoint = (previousFirst + previousSecond) * 0.5f;
            var currentMidpoint = (currentFirst + currentSecond) * 0.5f;
            var averageDelta = currentMidpoint - previousMidpoint;
            if (averageDelta.magnitude >= rotationDeadZonePixels)
            {
                var yaw = averageDelta.x * yawDegreesPerPixel * settings.RotationSpeed;
                var pitch = Mathf.Clamp(-averageDelta.y * pitchDegreesPerPixel * settings.RotationSpeed, -maxPitchDeltaPerFrame, maxPitchDeltaPerFrame);
                RotateAroundModelPivot(targetCamera.transform.up, yaw);
                RotateAroundModelPivot(targetCamera.transform.right, pitch);
            }

            var previousTwistAngle = Mathf.Atan2(previousSecond.y - previousFirst.y, previousSecond.x - previousFirst.x) * Mathf.Rad2Deg;
            var currentTwistAngle = Mathf.Atan2(currentSecond.y - currentFirst.y, currentSecond.x - currentFirst.x) * Mathf.Rad2Deg;
            var twistDelta = Mathf.DeltaAngle(previousTwistAngle, currentTwistAngle);
            if (Mathf.Abs(twistDelta) >= rotationDeadZonePixels)
                RotateAroundModelPivot(targetCamera.transform.forward, -twistDelta * rotateDegreesPerPixel * settings.RotationSpeed);
        }

        void RotateAroundModelPivot(Vector3 axis, float degrees)
        {
            if (modelRoot == null || axis.sqrMagnitude < 0.000001f || Mathf.Abs(degrees) < 0.001f)
                return;

            var centerBefore = GetVisibleBoundsCenter();
            modelRoot.Rotate(axis.normalized, degrees, Space.World);
            var centerAfter = GetVisibleBoundsCenter();
            modelRoot.position += centerBefore - centerAfter;
        }

        Vector3 GetVisibleBoundsCenter()
        {
            if (modelRoot == null)
                return Vector3.zero;

            var renderers = modelRoot.GetComponentsInChildren<Renderer>(false);
            var hasBounds = false;
            var bounds = new Bounds(modelRoot.position, Vector3.zero);
            foreach (var renderer in renderers)
            {
                if (renderer == null || !renderer.enabled)
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

            return hasBounds ? bounds.center : modelRoot.position;
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

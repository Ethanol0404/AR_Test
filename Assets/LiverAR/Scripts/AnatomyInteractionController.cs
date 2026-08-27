using System;
using UnityEngine;

namespace LiverAR.Runtime
{
    public sealed class AnatomyInteractionController : MonoBehaviour
    {
        [SerializeField] Camera arCamera;
        [SerializeField] AnatomyManager anatomyManager;
        [SerializeField] ARUIController uiController;
        [SerializeField] ModelInteractionController modelInteractionController;
        [SerializeField] LayerMask selectionMask = ~0;
        [SerializeField] float dragThresholdPixels = 12f;
        [SerializeField] float longPressSeconds = 1f;
        [SerializeField] float longPressTolerancePixels = 10f;

        AnatomyGestureState state = AnatomyGestureState.Idle;
        int activePointerId = int.MinValue;
        Vector2 startPosition;
        float startTime;
        AnatomyPart startedPart;

        public event Action<AnatomyPart, Vector2> LongPressedPart;

        void OnEnable()
        {
            TouchInput.Enable();
        }

        void Update()
        {
            if (TouchInput.ActiveTouches.Count >= 2)
            {
                CancelSingleTouch(AnatomyGestureState.PinchingOrRotating);
                return;
            }

            if (!TouchInput.TryGetPrimaryPointer(out var pointer))
            {
                if (state == AnatomyGestureState.PinchingOrRotating)
                    state = AnatomyGestureState.Idle;
                return;
            }

            if (state == AnatomyGestureState.PinchingOrRotating)
            {
                if (TouchInput.IsEnded(pointer))
                    state = AnatomyGestureState.Idle;
                return;
            }

            if (TouchInput.IsPointerOverUi(pointer))
            {
                CancelSingleTouch(AnatomyGestureState.Idle);
                return;
            }

            if (TouchInput.IsBegan(pointer))
            {
                BeginTouch(pointer);
                return;
            }

            if (pointer.PointerId != activePointerId)
                return;

            if (TouchInput.IsEnded(pointer))
            {
                EndTouch(pointer);
                return;
            }

            HandleActiveTouch(pointer);
        }

        void BeginTouch(TouchInput.PointerInput pointer)
        {
            activePointerId = pointer.PointerId;
            startPosition = pointer.ScreenPosition;
            startTime = Time.unscaledTime;
            startedPart = RaycastPart(pointer.ScreenPosition);
            state = startedPart != null ? AnatomyGestureState.LongPressPending : AnatomyGestureState.PossibleTap;
        }

        void HandleActiveTouch(TouchInput.PointerInput pointer)
        {
            var movementPixels = Vector2.Distance(startPosition, pointer.ScreenPosition);
            if (movementPixels >= dragThresholdPixels)
            {
                state = AnatomyGestureState.Dragging;
                modelInteractionController?.TranslateCameraRelative(pointer.Delta, arCamera);
                return;
            }

            if (state != AnatomyGestureState.LongPressPending)
                return;

            var classifiedState = AnatomyGestureClassifier.ClassifySingleTouch(
                Time.unscaledTime - startTime,
                movementPixels,
                dragThresholdPixels,
                longPressSeconds,
                longPressTolerancePixels,
                startedPart != null);

            if (classifiedState != AnatomyGestureState.LongPressTriggered)
                return;

            anatomyManager?.Select(startedPart);
            LongPressedPart?.Invoke(startedPart, startPosition);
            uiController?.OpenTransparencyPanelForSelection();
            state = AnatomyGestureState.LongPressTriggered;
        }

        void EndTouch(TouchInput.PointerInput pointer)
        {
            if (state == AnatomyGestureState.PossibleTap || state == AnatomyGestureState.LongPressPending)
            {
                var selectedPart = RaycastPart(pointer.ScreenPosition);
                if (selectedPart != null)
                {
                    anatomyManager?.Select(selectedPart);
                    uiController?.OpenInformationPanelForSelection();
                }
                else
                {
                    anatomyManager?.ClearSelection();
                    uiController?.CloseDetailOverlays();
                }
            }

            CancelSingleTouch(AnatomyGestureState.Idle);
        }

        AnatomyPart RaycastPart(Vector2 screenPosition)
        {
            if (arCamera == null)
                return null;

            var ray = arCamera.ScreenPointToRay(screenPosition);
            return Physics.Raycast(ray, out var hit, 100f, selectionMask)
                ? hit.collider.GetComponentInParent<AnatomyPart>()
                : null;
        }

        void CancelSingleTouch(AnatomyGestureState nextState)
        {
            state = nextState;
            activePointerId = int.MinValue;
            startedPart = null;
        }
    }
}

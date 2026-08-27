using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace LiverAR.Runtime
{
    static class TouchInput
    {
        public static IReadOnlyList<Touch> ActiveTouches => Touch.activeTouches;

        public readonly struct PointerInput
        {
            public PointerInput(Vector2 screenPosition, Vector2 delta, TouchPhase phase, int pointerId)
            {
                ScreenPosition = screenPosition;
                Delta = delta;
                Phase = phase;
                PointerId = pointerId;
            }

            public Vector2 ScreenPosition { get; }
            public Vector2 Delta { get; }
            public TouchPhase Phase { get; }
            public int PointerId { get; }
        }

        public static void Enable()
        {
            EnhancedTouchSupport.Enable();
        }

        public static bool TryGetPrimaryPointer(out PointerInput pointer)
        {
            if (TryGetTouch(0, out var touch))
            {
                pointer = new PointerInput(touch.screenPosition, touch.delta, touch.phase, touch.touchId);
                return true;
            }

            var mouse = Mouse.current;
            if (mouse == null)
            {
                pointer = default;
                return false;
            }

            if (mouse.leftButton.wasPressedThisFrame)
            {
                pointer = new PointerInput(mouse.position.ReadValue(), Vector2.zero, TouchPhase.Began, -1);
                return true;
            }

            if (mouse.leftButton.isPressed)
            {
                pointer = new PointerInput(mouse.position.ReadValue(), mouse.delta.ReadValue(), TouchPhase.Moved, -1);
                return true;
            }

            if (mouse.leftButton.wasReleasedThisFrame)
            {
                pointer = new PointerInput(mouse.position.ReadValue(), mouse.delta.ReadValue(), TouchPhase.Ended, -1);
                return true;
            }

            pointer = default;
            return false;
        }

        public static bool TryGetTouch(int index, out Touch touch)
        {
            var touches = Touch.activeTouches;
            if (touches.Count <= index)
            {
                touch = default;
                return false;
            }

            touch = touches[index];
            return true;
        }

        public static bool IsPointerOverUi(Touch touch)
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.touchId);
        }

        public static bool IsPointerOverUi(PointerInput pointer)
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(pointer.PointerId);
        }

        public static bool IsAnyTouchOverUi()
        {
            if (EventSystem.current == null)
                return false;

            foreach (var touch in Touch.activeTouches)
            {
                if (EventSystem.current.IsPointerOverGameObject(touch.touchId))
                    return true;
            }

            return false;
        }

        public static bool IsBegan(Touch touch)
        {
            return touch.phase == TouchPhase.Began;
        }

        public static bool IsBegan(PointerInput pointer)
        {
            return pointer.Phase == TouchPhase.Began;
        }

        public static bool IsMoved(Touch touch)
        {
            return touch.phase == TouchPhase.Moved;
        }

        public static bool IsMoved(PointerInput pointer)
        {
            return pointer.Phase == TouchPhase.Moved;
        }

        public static bool IsEnded(Touch touch)
        {
            return touch.phase == TouchPhase.Ended;
        }

        public static bool IsEnded(PointerInput pointer)
        {
            return pointer.Phase == TouchPhase.Ended;
        }
    }
}

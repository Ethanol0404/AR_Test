using UnityEngine;

namespace LiverAR.Runtime
{
    public sealed class AnatomySelectionController : MonoBehaviour
    {
        [SerializeField] Camera arCamera;
        [SerializeField] AnatomyManager anatomyManager;
        [SerializeField] LayerMask selectionMask = ~0;

        public bool IsSelectionHandlingEnabled => FindAnyObjectByType<AnatomyInteractionController>() == null;

        void OnEnable()
        {
            TouchInput.Enable();
        }

        void Update()
        {
            if (!IsSelectionHandlingEnabled)
                return;

            if (!TouchInput.TryGetPrimaryPointer(out var pointer) || TouchInput.IsPointerOverUi(pointer))
                return;

            if (!TouchInput.IsEnded(pointer) || arCamera == null || anatomyManager == null)
                return;

            var ray = arCamera.ScreenPointToRay(pointer.ScreenPosition);
            if (Physics.Raycast(ray, out var hit, 100f, selectionMask))
            {
                var part = hit.collider.GetComponentInParent<AnatomyPart>();
                if (part != null)
                {
                    anatomyManager.Select(part);
                    return;
                }
            }

            anatomyManager.ClearSelection();
        }
    }
}

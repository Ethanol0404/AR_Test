using UnityEngine;

namespace LiverAR.Runtime
{
    public sealed class TransparencyController : MonoBehaviour
    {
        [SerializeField] AnatomyManager anatomyManager;

        public static float ClampOpacity(float value)
        {
            return Mathf.Clamp01(value);
        }

        public bool SetSelectedOpacity(float opacity)
        {
            if (anatomyManager == null || anatomyManager.SelectedPart == null)
            {
                Debug.LogWarning("No anatomical structure is selected for transparency adjustment.");
                return false;
            }

            anatomyManager.SelectedPart.SetOpacity(opacity);
            return true;
        }

        public bool ResetSelectedOpacity()
        {
            if (anatomyManager == null || anatomyManager.SelectedPart == null)
            {
                Debug.LogWarning("No anatomical structure is selected for transparency reset.");
                return false;
            }

            anatomyManager.SelectedPart.ResetOpacity();
            return true;
        }
    }
}

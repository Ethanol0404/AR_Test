using UnityEngine;

namespace LiverAR.Runtime
{
    [System.Serializable]
    public sealed class LiverARSettings
    {
        const string InteractionSensitivityKey = "LiverAR.InteractionSensitivity";
        const string RotationSpeedKey = "LiverAR.RotationSpeed";
        const string ScaleSensitivityKey = "LiverAR.ScaleSensitivity";
        const string HapticsKey = "LiverAR.Haptics";

        [SerializeField] float interactionSensitivity = 1f;
        [SerializeField] float rotationSpeed = 1f;
        [SerializeField] float scaleSensitivity = 1f;
        [SerializeField] bool hapticFeedback = true;

        public float InteractionSensitivity => interactionSensitivity;
        public float RotationSpeed => rotationSpeed;
        public float ScaleSensitivity => scaleSensitivity;
        public bool HapticFeedback => hapticFeedback;

        public static LiverARSettings CreateDefault()
        {
            return new LiverARSettings();
        }

        public static LiverARSettings Load()
        {
            var settings = CreateDefault();
            settings.SetInteractionSensitivity(PlayerPrefs.GetFloat(InteractionSensitivityKey, settings.interactionSensitivity));
            settings.SetRotationSpeed(PlayerPrefs.GetFloat(RotationSpeedKey, settings.rotationSpeed));
            settings.SetScaleSensitivity(PlayerPrefs.GetFloat(ScaleSensitivityKey, settings.scaleSensitivity));
            settings.SetHapticFeedback(PlayerPrefs.GetInt(HapticsKey, settings.hapticFeedback ? 1 : 0) == 1);
            return settings;
        }

        public void Save()
        {
            PlayerPrefs.SetFloat(InteractionSensitivityKey, interactionSensitivity);
            PlayerPrefs.SetFloat(RotationSpeedKey, rotationSpeed);
            PlayerPrefs.SetFloat(ScaleSensitivityKey, scaleSensitivity);
            PlayerPrefs.SetInt(HapticsKey, hapticFeedback ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void Reset()
        {
            interactionSensitivity = 1f;
            rotationSpeed = 1f;
            scaleSensitivity = 1f;
            hapticFeedback = true;
        }

        public void SetInteractionSensitivity(float value)
        {
            interactionSensitivity = Mathf.Clamp(value, 0.2f, 3f);
        }

        public void SetRotationSpeed(float value)
        {
            rotationSpeed = Mathf.Clamp(value, 0.2f, 5f);
        }

        public void SetScaleSensitivity(float value)
        {
            scaleSensitivity = Mathf.Clamp(value, 0.1f, 4f);
        }

        public void SetHapticFeedback(bool enabled)
        {
            hapticFeedback = enabled;
        }
    }
}

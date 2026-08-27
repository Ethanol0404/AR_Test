using System;
using UnityEngine;

namespace LiverAR.Runtime
{
    public enum LiverModelType
    {
        Normal,
        Disease
    }

    public sealed class LiverModelSwitcher : MonoBehaviour
    {
        [SerializeField] GameObject normalModel;
        [SerializeField] GameObject diseaseModel;
        [SerializeField] AnatomyManager anatomyManager;

        public event Action<LiverModelType> ModelChanged;
        public LiverModelType CurrentModel { get; private set; } = LiverModelType.Normal;
        public bool HasDiseaseModel => diseaseModel != null;

        void Awake()
        {
            ApplyActiveModel(CurrentModel);
        }

        public bool SwitchTo(LiverModelType modelType)
        {
            if (modelType == LiverModelType.Disease && diseaseModel == null)
            {
                Debug.LogWarning("Disease model not yet assigned.");
                return false;
            }

            CurrentModel = modelType;
            ApplyActiveModel(modelType);
            anatomyManager?.ClearSelection();
            anatomyManager?.RegisterConfiguredParts();
            ModelChanged?.Invoke(CurrentModel);
            return true;
        }

        public void ConfigureForTests(GameObject normal, GameObject disease)
        {
            normalModel = normal;
            diseaseModel = disease;
        }

        void ApplyActiveModel(LiverModelType modelType)
        {
            if (normalModel != null)
                normalModel.SetActive(modelType == LiverModelType.Normal);

            if (diseaseModel != null)
                diseaseModel.SetActive(modelType == LiverModelType.Disease);
        }
    }
}

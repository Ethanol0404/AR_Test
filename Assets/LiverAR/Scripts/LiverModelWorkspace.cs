using System;
using System.Collections.Generic;
using UnityEngine;

namespace LiverAR.Runtime
{
    public sealed class LiverModelWorkspace : MonoBehaviour
    {
        [SerializeField] Transform modelsRoot;
        [SerializeField] Camera arCamera;
        [SerializeField] float duplicateOffset = .18f;
        readonly List<LiverModelRoot> models = new List<LiverModelRoot>();
        int nextId = 1;
        public IReadOnlyList<LiverModelRoot> Models => models;
        public LiverModelRoot ActiveModel { get; private set; }
        public event Action<LiverModelRoot> ActiveModelChanged;
        public event Action ModelsChanged;

        void Awake()
        {
            if (modelsRoot == null) { var host = new GameObject("ModelsRoot"); modelsRoot = host.transform; }
        }
        public LiverModelRoot Register(GameObject model, string typeName = "Normal Liver")
        {
            if (model == null) return null;
            model.transform.SetParent(modelsRoot, true);
            var root = model.GetComponent<LiverModelRoot>() ?? model.AddComponent<LiverModelRoot>();
            var id = nextId++;
            root.Initialize(id, $"{typeName} {id}");
            models.Add(root);
            Activate(root);
            ModelsChanged?.Invoke();
            return root;
        }
        public bool Activate(LiverModelRoot root)
        {
            if (root == null || !models.Contains(root)) return false;
            if (ActiveModel != null) ActiveModel.SetActiveVisual(false);
            ActiveModel = root; ActiveModel.SetActiveVisual(true); ActiveModelChanged?.Invoke(root); return true;
        }
        public LiverModelRoot DuplicateActive()
        {
            if (ActiveModel == null) return null;
            var source = ActiveModel;
            var clone = Instantiate(source.gameObject, modelsRoot);
            var root = clone.GetComponent<LiverModelRoot>();
            var id = nextId++;
            root.Initialize(id, $"Normal Liver {id}");
            root.CopyRuntimeStateFrom(source);
            var camera = arCamera != null ? arCamera : Camera.main;
            clone.transform.position += camera != null ? camera.transform.right * duplicateOffset : Vector3.right * duplicateOffset;
            models.Add(root); Activate(root); ModelsChanged?.Invoke(); return root;
        }
        public bool DeleteActive()
        {
            if (ActiveModel == null) return false;
            var deleted = ActiveModel;
            deleted.SetActiveVisual(false);
            models.Remove(deleted);
            Destroy(deleted.gameObject);
            ActiveModel = null;
            if (models.Count > 0)
                Activate(models[models.Count - 1]);
            else
                ActiveModelChanged?.Invoke(null);
            ModelsChanged?.Invoke();
            return true;
        }
        public void ClearAllModels() { foreach (var root in models) if (root != null) Destroy(root.gameObject); models.Clear(); ActiveModel = null; ActiveModelChanged?.Invoke(null); ModelsChanged?.Invoke(); }
        int CountType(string type) { var count = 0; foreach (var root in models) if (root != null && root.DisplayName.StartsWith(type, StringComparison.Ordinal)) count++; return count; }
    }
}

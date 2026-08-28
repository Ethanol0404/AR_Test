using System;
using System.Collections.Generic;
using UnityEngine;

namespace LiverAR.Runtime
{
    [DisallowMultipleComponent]
    public sealed class LiverModelRoot : MonoBehaviour
    {
        [SerializeField] int instanceId;
        [SerializeField] string displayName;
        [SerializeField] GameObject selectionIndicator;

        public AnatomyManager AnatomyManager { get; private set; }
        public int InstanceId => instanceId;
        public string DisplayName => displayName;

        public void Initialize(int id, string label)
        {
            instanceId = id;
            displayName = label;
            name = $"LiverModel_{id}";
            CenterVisualsAtRootPivot();
            AnatomyManager = GetComponent<AnatomyManager>() ?? gameObject.AddComponent<AnatomyManager>();
            AnatomyManager.SetConfiguredParts(GetComponentsInChildren<AnatomyPart>(true));
            EnsureIndicator();
            SetActiveVisual(false);
        }

        void Awake()
        {
            if (AnatomyManager == null)
            {
                AnatomyManager = GetComponent<AnatomyManager>();
                if (AnatomyManager != null)
                    AnatomyManager.SetConfiguredParts(GetComponentsInChildren<AnatomyPart>(true));
            }
        }

        public void SetActiveVisual(bool active)
        {
            EnsureIndicator();
            if (selectionIndicator != null)
                selectionIndicator.SetActive(active);
        }

        public void CopyRuntimeStateFrom(LiverModelRoot source)
        {
            if (source == null || source.AnatomyManager == null || AnatomyManager == null)
                return;
            var sourceById = new Dictionary<string, AnatomyPart>();
            foreach (var part in source.AnatomyManager.Parts)
                if (part != null) sourceById[part.StructureId] = part;
            foreach (var target in AnatomyManager.Parts)
            {
                if (target == null || !sourceById.TryGetValue(target.StructureId, out var sourcePart)) continue;
                target.SetVisible(sourcePart.IsVisible);
                target.SetOpacity(sourcePart.Opacity);
                if (source.AnatomyManager.SelectedPart == sourcePart)
                    AnatomyManager.Select(target);
            }
        }

        void EnsureIndicator()
        {
            if (selectionIndicator != null) return;
            selectionIndicator = new GameObject("Active Model Indicator");
            selectionIndicator.transform.SetParent(transform, false);
            // The root itself is the selection state. Do not add a world-space outline here:
            // model scale magnifies it into screen-spanning yellow lines on Android.
            selectionIndicator.SetActive(false);
        }

        void CenterVisualsAtRootPivot()
        {
            var visualRoot = transform.Find("LiverVisualRoot");
            if (visualRoot != null)
                return;

            visualRoot = new GameObject("LiverVisualRoot").transform;
            visualRoot.SetParent(transform, false);

            var children = new List<Transform>();
            for (var index = 0; index < transform.childCount; index++)
            {
                var child = transform.GetChild(index);
                if (child != visualRoot && child.gameObject != selectionIndicator)
                    children.Add(child);
            }

            foreach (var child in children)
                child.SetParent(visualRoot, true);

            if (!TryGetVisibleRendererBounds(visualRoot, out var bounds))
                return;

            // Keep the placement transform at the anatomical centre so rotating this
            // root is true self-rotation even when the imported mesh pivot is offset.
            visualRoot.position += transform.position - bounds.center;
        }

        static bool TryGetVisibleRendererBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            var hasBounds = false;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
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

            return hasBounds;
        }
    }
}

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
        LiverModelSelectionBoundary selectionBoundary;

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

            if (selectionBoundary == null)
                selectionBoundary = GetComponent<LiverModelSelectionBoundary>() ?? gameObject.AddComponent<LiverModelSelectionBoundary>();
            selectionBoundary.SetVisible(active);
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

    sealed class LiverModelSelectionBoundary : MonoBehaviour
    {
        static Material sharedMaterial;
        LineRenderer line;

        public void SetVisible(bool visible)
        {
            EnsureLine();
            if (line == null)
                return;

            if (visible)
                UpdateBounds();
            line.enabled = visible;
        }

        void EnsureLine()
        {
            if (line != null)
                return;

            var material = GetSharedMaterial();
            if (material == null)
                return;

            var boundary = new GameObject("Model Selection Boundary");
            boundary.transform.SetParent(transform, false);
            line = boundary.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = false;
            line.loop = false;
            line.positionCount = 16;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.alignment = LineAlignment.View;
            line.enabled = false;
        }

        void UpdateBounds()
        {
            if (!TryGetVisibleBounds(out var worldBounds))
            {
                line.enabled = false;
                return;
            }

            var center = transform.InverseTransformPoint(worldBounds.center);
            var size = Vector3.Scale(worldBounds.size, InverseLossyScale(transform.lossyScale));
            var half = size * .5f;
            var min = center - half;
            var max = center + half;
            var points = new[]
            {
                // Traverse only cuboid edges. Repeated vertices join the three vertical
                // edges without drawing through the model as the previous path did.
                new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z), new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, min.y, min.z), new Vector3(min.x, max.y, min.z),
                new Vector3(max.x, max.y, min.z), new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, max.y, min.z), new Vector3(max.x, max.y, max.z),
                new Vector3(max.x, min.y, max.z), new Vector3(max.x, max.y, max.z),
                new Vector3(min.x, max.y, max.z), new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, max.z), new Vector3(min.x, max.y, min.z)
            };
            line.SetPositions(points);
            var width = Mathf.Max(.004f, size.magnitude * .009f);
            line.startWidth = width;
            line.endWidth = width;
        }

        bool TryGetVisibleBounds(out Bounds bounds)
        {
            bounds = default;
            var hasBounds = false;
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || renderer == line || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
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

        static Vector3 InverseLossyScale(Vector3 scale)
        {
            return new Vector3(
                Mathf.Abs(scale.x) > .0001f ? 1f / scale.x : 1f,
                Mathf.Abs(scale.y) > .0001f ? 1f / scale.y : 1f,
                Mathf.Abs(scale.z) > .0001f ? 1f / scale.z : 1f);
        }

        static Material GetSharedMaterial()
        {
            if (sharedMaterial != null)
                return sharedMaterial;

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                return null;

            sharedMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
            sharedMaterial.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.98f));
            sharedMaterial.SetFloat("_ZWrite", 1f);
            sharedMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Overlay;
            return sharedMaterial;
        }
    }
}

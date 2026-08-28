using System;
using System.Collections.Generic;
using UnityEngine;

namespace LiverAR.Runtime
{
    [DisallowMultipleComponent]
    public sealed class AnatomyPart : MonoBehaviour
    {
        [SerializeField] string structureId = "unassigned";
        [SerializeField] string displayName = "Unassigned Anatomy";
        [SerializeField] AnatomyCategory category = AnatomyCategory.Other;
        [SerializeField] Color defaultColor = Color.white;
        [SerializeField] Renderer[] renderers = Array.Empty<Renderer>();

        MaterialPropertyBlock propertyBlock;
        AnatomySelectionOutline selectionOutline;
        readonly Dictionary<Material, int> originalRenderQueues = new Dictionary<Material, int>();
        Collider[] colliders = Array.Empty<Collider>();
        float opacity = 1f;
        bool isVisible = true;
        bool isSelected;

        public string StructureId => structureId;
        public string DisplayName => displayName;
        public AnatomyCategory Category => category;
        public Color DefaultColor => defaultColor;
        public float Opacity => opacity;
        public bool IsVisible => isVisible;
        public bool IsSelected => isSelected;
        public Renderer[] Renderers => renderers;
        public Transform ModelRoot
        {
            get
            {
                var current = transform;
                while (current != null)
                {
                    if (current.name.StartsWith("LiverModelRoot", StringComparison.Ordinal))
                        return current;
                    current = current.parent;
                }
                return transform.root;
            }
        }

        void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
            CacheReferences();
            selectionOutline = GetComponent<AnatomySelectionOutline>() ?? gameObject.AddComponent<AnatomySelectionOutline>();
            ApplyAppearance();
        }

        void Reset()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
        }

        public void Configure(string id, string name, AnatomyCategory partCategory, Color color, Renderer[] rendererReferences)
        {
            structureId = string.IsNullOrWhiteSpace(id) ? "unassigned" : id;
            displayName = string.IsNullOrWhiteSpace(name) ? structureId : name;
            category = partCategory;
            defaultColor = color;
            renderers = rendererReferences ?? Array.Empty<Renderer>();
            CacheReferences();
            ApplyAppearance();
        }

        public void SetVisible(bool visible)
        {
            isVisible = visible;
            ApplyRendererVisibility();
        }

        void ApplyRendererVisibility()
        {
            var renderVisible = isVisible && opacity > 0.001f;
            foreach (var partRenderer in renderers)
            {
                if (partRenderer != null)
                    partRenderer.enabled = renderVisible;
            }

            foreach (var partCollider in colliders)
            {
                if (partCollider != null)
                    partCollider.enabled = renderVisible;
            }

            UpdateSelectionOutline();
        }

        public void SetSelected(bool selected)
        {
            isSelected = selected;
            if (selectionOutline == null)
                selectionOutline = GetComponent<AnatomySelectionOutline>() ?? gameObject.AddComponent<AnatomySelectionOutline>();
            UpdateSelectionOutline();
            ApplyAppearance();
        }

        public void SetColor(Color color)
        {
            defaultColor = color;
            ApplyAppearance();
        }

        public void SetOpacity(float value)
        {
            opacity = TransparencyController.ClampOpacity(value);
            ApplyAppearance();
            ApplyRendererVisibility();
        }

        public void ResetOpacity()
        {
            opacity = 1f;
            ApplyAppearance();
            ApplyRendererVisibility();
        }

        public void ResetAppearance()
        {
            opacity = 1f;
            isSelected = false;
            ApplyAppearance();
            SetVisible(true);
        }

        public void CacheReferences()
        {
            if (renderers == null || renderers.Length == 0)
                renderers = GetComponentsInChildren<Renderer>(true);

            colliders = GetComponentsInChildren<Collider>(true);
        }

        void ApplyAppearance()
        {
            if (propertyBlock == null)
                propertyBlock = new MaterialPropertyBlock();

            var color = defaultColor;
            color.a = opacity;

            foreach (var partRenderer in renderers)
            {
                if (partRenderer == null)
                    continue;

                partRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor("_BaseColor", color);
                propertyBlock.SetColor("_Color", color);
                partRenderer.SetPropertyBlock(propertyBlock);
                ConfigureMaterialTransparency(partRenderer, opacity);
            }
        }

        void UpdateSelectionOutline()
        {
            if (selectionOutline != null)
                selectionOutline.SetVisible(isSelected && isVisible && opacity > 0.001f);
        }

        void ConfigureMaterialTransparency(Renderer partRenderer, float alpha)
        {
            var materials = partRenderer.materials;
            foreach (var material in materials)
            {
                if (material == null)
                    continue;

                if (material.shader == null || material.shader.name == "Hidden/InternalErrorShader")
                {
                    var fallbackShader = Shader.Find("Universal Render Pipeline/Lit");
                    if (fallbackShader == null)
                        fallbackShader = Shader.Find("Standard");
                    if (fallbackShader != null)
                        material.shader = fallbackShader;
                }

                if (!originalRenderQueues.ContainsKey(material))
                    originalRenderQueues[material] = material.renderQueue;

                var materialColor = defaultColor;
                materialColor.a = alpha;
                material.SetColor("_BaseColor", materialColor);
                material.SetColor("_Color", materialColor);

                if (alpha >= 0.99f)
                {
                    material.SetFloat("_Surface", 0f);
                    material.SetFloat("_Blend", 0f);
                    material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
                    material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
                    material.SetFloat("_ZWrite", 1f);
                    material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    material.SetOverrideTag("RenderType", "Opaque");
                    material.SetShaderPassEnabled("ShadowCaster", true);
                    material.renderQueue = originalRenderQueues[material];
                    continue;
                }

                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetShaderPassEnabled("ShadowCaster", false);
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
        }
    }
}

namespace LiverAR.Runtime
{
    [DisallowMultipleComponent]
    sealed class AnatomySelectionOutline : MonoBehaviour
    {
        static Material sharedMaterial;
        readonly List<GameObject> outlines = new List<GameObject>();
        bool initialized;

        public void SetVisible(bool visible)
        {
            EnsureInitialized();
            foreach (var outline in outlines)
            {
                if (outline != null)
                    outline.SetActive(visible);
            }
        }

        void EnsureInitialized()
        {
            if (initialized)
                return;

            initialized = true;
            var material = GetSharedMaterial();
            if (material == null)
                return;

            foreach (var source in GetComponentsInChildren<MeshFilter>(true))
            {
                if (source.sharedMesh == null || source.transform.name == "Selection Outline")
                    continue;

                var outline = new GameObject("Selection Outline");
                outline.transform.SetParent(source.transform.parent, false);
                outline.transform.localPosition = source.transform.localPosition;
                outline.transform.localRotation = source.transform.localRotation;
                outline.transform.localScale = source.transform.localScale * 1.015f;
                outline.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
                outline.AddComponent<MeshRenderer>().sharedMaterial = material;
                outline.SetActive(false);
                outlines.Add(outline);
            }
        }

        static Material GetSharedMaterial()
        {
            if (sharedMaterial != null)
                return sharedMaterial;

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                return null;

            sharedMaterial = new Material(shader)
            {
                hideFlags = HideFlags.DontSave
            };
            sharedMaterial.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.98f));
            sharedMaterial.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Front);
            sharedMaterial.SetFloat("_ZWrite", 0f);
            sharedMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 10;
            return sharedMaterial;
        }
    }
}

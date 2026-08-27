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
        [SerializeField] Color highlightColor = new Color(1f, 0.92f, 0.25f, 1f);

        MaterialPropertyBlock propertyBlock;
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

        void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
            CacheReferences();
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
            foreach (var partRenderer in renderers)
            {
                if (partRenderer != null)
                    partRenderer.enabled = visible;
            }

            foreach (var partCollider in colliders)
            {
                if (partCollider != null)
                    partCollider.enabled = visible;
            }
        }

        public void SetSelected(bool selected)
        {
            isSelected = selected;
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
        }

        public void ResetOpacity()
        {
            opacity = 1f;
            ApplyAppearance();
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

            var color = isSelected ? highlightColor : defaultColor;
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

        void ConfigureMaterialTransparency(Renderer partRenderer, float alpha)
        {
            var materials = partRenderer.materials;
            foreach (var material in materials)
            {
                if (material == null)
                    continue;

                if (!originalRenderQueues.ContainsKey(material))
                    originalRenderQueues[material] = material.renderQueue;

                if (alpha >= 0.99f)
                {
                    material.SetFloat("_Surface", 0f);
                    material.SetFloat("_Blend", 0f);
                    material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
                    material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
                    material.SetFloat("_ZWrite", 1f);
                    material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    material.renderQueue = originalRenderQueues[material];
                    continue;
                }

                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
        }
    }
}

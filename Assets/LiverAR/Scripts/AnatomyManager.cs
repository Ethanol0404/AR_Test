using System;
using System.Collections.Generic;
using UnityEngine;

namespace LiverAR.Runtime
{
    public sealed class AnatomyManager : MonoBehaviour
    {
        [SerializeField] AnatomyPart[] configuredParts = Array.Empty<AnatomyPart>();

        readonly Dictionary<string, AnatomyPart> partsById = new Dictionary<string, AnatomyPart>();
        public event Action<AnatomyPart> SelectionChanged;

        public AnatomyPart SelectedPart { get; private set; }
        public IReadOnlyCollection<AnatomyPart> Parts => partsById.Values;

        void Awake()
        {
            RegisterConfiguredParts();
        }

        public void RegisterConfiguredParts()
        {
            partsById.Clear();
            foreach (var part in configuredParts)
                Register(part);
        }

        public void SetConfiguredParts(AnatomyPart[] parts)
        {
            configuredParts = parts ?? Array.Empty<AnatomyPart>();
            RegisterConfiguredParts();
        }

        public void Register(AnatomyPart part)
        {
            if (part == null || string.IsNullOrWhiteSpace(part.StructureId))
                return;

            part.CacheReferences();
            partsById[part.StructureId] = part;
        }

        public bool TryGetPart(string id, out AnatomyPart part)
        {
            return partsById.TryGetValue(id, out part);
        }

        public void Select(AnatomyPart part)
        {
            if (SelectedPart == part)
                return;

            if (SelectedPart != null)
                SelectedPart.SetSelected(false);

            SelectedPart = part;

            if (SelectedPart != null)
                SelectedPart.SetSelected(true);

            SelectionChanged?.Invoke(SelectedPart);
        }

        public void ClearSelection()
        {
            Select(null);
        }

        public void ShowAll()
        {
            foreach (var part in partsById.Values)
                part.SetVisible(true);
        }

        public void ShowWholeLiverOverview()
        {
            foreach (var part in partsById.Values)
            {
                if (part.Category == AnatomyCategory.WholeLiver)
                    part.SetVisible(true);
                else if (part.Category == AnatomyCategory.LiverSegment)
                    part.SetVisible(false);
            }
            ClearSelection();
        }

        public void ShowLiverSegments()
        {
            foreach (var part in partsById.Values)
            {
                if (part.Category == AnatomyCategory.WholeLiver)
                    part.SetVisible(false);
                else if (part.Category == AnatomyCategory.LiverSegment)
                    part.SetVisible(true);
            }
            ClearSelection();
        }

        public void HideAll()
        {
            foreach (var part in partsById.Values)
                part.SetVisible(false);
            ClearSelection();
        }

        public void ResetAllAppearances()
        {
            foreach (var part in partsById.Values)
                part.ResetAppearance();
            ClearSelection();
        }

        public string ValidateConfiguration()
        {
            foreach (var part in configuredParts)
            {
                if (part == null)
                    return "An anatomy configuration entry is missing.";
                if (part.Renderers == null || part.Renderers.Length == 0)
                    return $"{part.DisplayName} has no renderer assigned.";
                if (part.GetComponentInChildren<Collider>(true) == null)
                    return $"{part.DisplayName} has no collider for selection.";
            }

            return string.Empty;
        }
    }
}

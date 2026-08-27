using System;
using UnityEngine;

namespace LiverAR.Runtime
{
    [CreateAssetMenu(menuName = "Liver AR/Anatomy Model Config", fileName = "AnatomyModelConfig")]
    public sealed class AnatomyModelConfig : ScriptableObject
    {
        [SerializeField] AnatomyStructureDefinition[] structures = Array.Empty<AnatomyStructureDefinition>();
        public AnatomyStructureDefinition[] Structures => structures;
    }

    [Serializable]
    public sealed class AnatomyStructureDefinition
    {
        public string structureId;
        public string displayName;
        public AnatomyCategory category;
        public Color defaultColor = Color.white;
    }
}

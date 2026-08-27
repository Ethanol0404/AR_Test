using System;
using UnityEngine;

namespace LiverAR.Runtime
{
    [CreateAssetMenu(menuName = "Liver AR/Anatomy Info Database", fileName = "AnatomyInfoDatabase")]
    public sealed class AnatomyInfoDatabase : ScriptableObject
    {
        [SerializeField] AnatomyInfoRecord[] records = Array.Empty<AnatomyInfoRecord>();

        public bool TryGetInfo(string structureId, out AnatomyInfoRecord record)
        {
            foreach (var item in records)
            {
                if (item != null && item.structureId == structureId)
                {
                    record = item;
                    return true;
                }
            }

            record = null;
            return false;
        }
    }

    [Serializable]
    public sealed class AnatomyInfoRecord
    {
        public string structureId;
        public string location;
        public string bloodSupply;
        public string venousDrainage;
        public string function;
        [TextArea(2, 6)] public string educationalDescription;
    }
}

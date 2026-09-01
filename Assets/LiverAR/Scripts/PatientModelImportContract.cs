using System;
using UnityEngine;

namespace LiverAR.Runtime
{
    [Serializable]
    public sealed class PatientModelMetadata
    {
        public int formatVersion;
        public string units;
        public PatientCoordinateSystem coordinateSystem;
        public string glbFile;
        public string glbRootNode;
        public PatientModelEntry[] models;
        public PatientModelEntry[] Models => models ?? Array.Empty<PatientModelEntry>();
    }

    [Serializable]
    public sealed class PatientCoordinateSystem
    {
        public string source;
        public string unityConversion;
    }

    [Serializable]
    public sealed class PatientModelEntry
    {
        public string name;
        public string file;
        public string role;
        public string id;
        public string displayName;
        public string Name => name ?? string.Empty;
        public string Id => string.IsNullOrWhiteSpace(id) ? Name : id;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Name : displayName;
        public string Role => role ?? string.Empty;
    }

    public static class PatientModelImportContract
    {
        public static PatientModelMetadata ParseMetadata(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Patient metadata is empty.", nameof(json));
            var metadata = JsonUtility.FromJson<PatientModelMetadata>(json);
            if (metadata == null || metadata.formatVersion != 1) throw new FormatException("Unsupported patient metadata format.");
            if (!string.Equals(metadata.units, "mm", StringComparison.OrdinalIgnoreCase)) throw new FormatException("Patient coordinates must be in millimetres.");
            if (metadata.coordinateSystem == null || !string.Equals(metadata.coordinateSystem.source, "LPS", StringComparison.OrdinalIgnoreCase) || !string.Equals(metadata.coordinateSystem.unityConversion, "metadata-defined", StringComparison.OrdinalIgnoreCase))
                throw new FormatException("Patient metadata must declare the LPS coordinate contract.");
            if (string.IsNullOrWhiteSpace(metadata.glbFile)) throw new FormatException("Patient metadata does not declare glbFile.");
            if (metadata.Models.Length == 0) throw new FormatException("Patient metadata contains no anatomy entries.");
            return metadata;
        }

        public static Vector3 ToUnityPosition(Vector3 lpsMillimetres)
        {
            var metres = lpsMillimetres * 0.001f;
            return new Vector3(-metres.x, metres.z, -metres.y);
        }
    }
}

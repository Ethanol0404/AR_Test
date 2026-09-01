using System;
using System.IO;
using System.Threading.Tasks;
using GLTFast;
using UnityEngine;

namespace LiverAR.Runtime
{
    public sealed class RuntimePatientGlbLoader : MonoBehaviour
    {
        [SerializeField] LiverModelWorkspace modelWorkspace;
        [SerializeField] Camera arCamera;
        [SerializeField] float placementDistance = 1.2f;
        [SerializeField] float targetModelSize = 0.45f;
        bool loading;

        public event Action<string> StatusChanged;
        public string LastError { get; private set; } = string.Empty;
        public bool IsLoading => loading;

        void Awake()
        {
            if (modelWorkspace == null) modelWorkspace = FindAnyObjectByType<LiverModelWorkspace>();
            if (arCamera == null) arCamera = Camera.main;
        }

        public void PickPatientModel()
        {
            LastError = string.Empty;
            Report("Choose the patient export folder...");
            if (!AndroidFolderPicker.TryPickFolder(Application.persistentDataPath))
                Report("Folder import is available in the Android build.");
        }

        void Update()
        {
            if (AndroidFolderPicker.TryConsumePickerStatus(out var status)) Report(status);
            if (!loading && AndroidFolderPicker.TryConsumePickedFolder(out var folder)) LoadExportFolder(folder);
        }

        public void LoadExportFolder(string folderPath)
        {
            if (!loading) _ = LoadExportFolderAsync(folderPath);
        }

        async Task LoadExportFolderAsync(string folderPath)
        {
            loading = true;
            LastError = string.Empty;
            try
            {
                if (modelWorkspace == null) throw new InvalidOperationException("Liver model workspace is unavailable.");
                if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath)) throw new DirectoryNotFoundException("Patient export folder was not found.");
                var folder = Path.GetFullPath(folderPath);
                var metadata = PatientModelImportContract.ParseMetadata(File.ReadAllText(Path.Combine(folder, "metadata.json")));
                var glbPath = ResolveChildPath(folder, metadata.glbFile);
                Report("Loading patient GLB...");

                var gltf = new GltfImport();
                if (!await gltf.LoadFile(glbPath)) throw new FormatException("Patient GLB could not be loaded.");
                var model = new GameObject(string.IsNullOrWhiteSpace(metadata.glbRootNode) ? "PatientModelRoot" : metadata.glbRootNode);
                PlaceInFrontOfCamera(model.transform);
                Report("Preparing patient model...");
                if (!await gltf.InstantiateMainSceneAsync(model.transform))
                {
                    Destroy(model);
                    throw new FormatException("Patient GLB scene could not be instantiated.");
                }

                ConfigureParts(model.transform, metadata);
                var root = modelWorkspace.Register(model, "Patient Model");
                root.AnatomyManager.ShowAll();
                FitToTargetSize(root.transform);
                Report("Patient GLB loaded successfully.");
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
                Report($"Patient GLB import failed: {LastError}");
            }
            finally { loading = false; }
        }

        static void ConfigureParts(Transform root, PatientModelMetadata metadata)
        {
            for (var index = 0; index < metadata.Models.Count; index++)
            {
                var entry = metadata.Models[index];
                var node = FindChild(root, entry.Name);
                if (node == null) { Debug.LogWarning($"GLB node not found: {entry.Name}"); continue; }
                var renderers = node.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) continue;
                var part = node.GetComponent<AnatomyPart>() ?? node.gameObject.AddComponent<AnatomyPart>();
                foreach (var meshFilter in node.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (meshFilter.sharedMesh == null) continue;
                    var collider = meshFilter.GetComponent<MeshCollider>() ?? meshFilter.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = meshFilter.sharedMesh;
                }
                part.Configure(ToStructureId(entry.Id), entry.DisplayName, ToCategory(entry), GetPartColor(entry, index), renderers);
            }

            Physics.SyncTransforms();
        }

        static Color GetPartColor(PatientModelEntry entry, int index)
        {
            if (entry.Name.IndexOf("vein", StringComparison.OrdinalIgnoreCase) >= 0)
                return new Color(0.12f, 0.42f, 0.95f, 1f);
            if (entry.Name.IndexOf("tumor", StringComparison.OrdinalIgnoreCase) >= 0)
                return new Color(0.95f, 0.22f, 0.18f, 1f);

            var hue = Mathf.Repeat(0.04f + index * 0.085f, 1f);
            return Color.HSVToRGB(hue, 0.62f, 0.9f);
        }

        void PlaceInFrontOfCamera(Transform root)
        {
            var camera = arCamera != null ? arCamera : Camera.main;
            if (camera == null) { root.position = new Vector3(0f, 0f, placementDistance); return; }
            root.position = camera.transform.position + camera.transform.forward * placementDistance;
            root.rotation = Quaternion.Euler(0f, camera.transform.eulerAngles.y, 0f);
        }

        void FitToTargetSize(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            var size = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            if (size > 0.0001f) root.localScale *= targetModelSize / size;
        }

        static Transform FindChild(Transform root, string name)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true)) if (string.Equals(child.name, name, StringComparison.OrdinalIgnoreCase)) return child;
            return null;
        }

        static string ResolveChildPath(string folder, string file)
        {
            var path = Path.GetFullPath(Path.Combine(folder, file));
            var prefix = folder.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ? folder : folder + Path.DirectorySeparatorChar;
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new FormatException("Patient GLB path escapes its export folder.");
            return path;
        }

        static string ToStructureId(string value) => (value ?? "anatomy").ToLowerInvariant().Replace('_', '-').Replace(' ', '-');
        static AnatomyCategory ToCategory(PatientModelEntry entry) => entry.Name.IndexOf("vein", StringComparison.OrdinalIgnoreCase) >= 0 ? AnatomyCategory.Vessel : entry.Name.IndexOf("tumor", StringComparison.OrdinalIgnoreCase) >= 0 ? AnatomyCategory.Lesion : AnatomyCategory.LiverSegment;
        void Report(string message) { StatusChanged?.Invoke(message); Debug.Log(message); }
    }
}

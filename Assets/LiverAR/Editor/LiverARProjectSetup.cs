using System;
using System.Collections.Generic;
using System.IO;
using LiverAR.Runtime;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.XR;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.XR.CoreUtils;

namespace LiverAR.Editor
{
    public static class LiverARProjectSetup
    {
        const string ScenePath = "Assets/Scenes/LiverARScene.unity";
        const string PrefabPath = "Assets/LiverAR/Prefabs/LiverAnatomyPrototype.prefab";
        const string ConfigPath = "Assets/LiverAR/Configs/LiverAnatomyConfig.asset";
        const string MaterialsPath = "Assets/LiverAR/Materials";
        const string SourceModelsPath = "Assets/LiverAR/Models/SourceFrom3DSlicer";
        const string ConvertedVesselsPath = "Assets/LiverAR/Models/ConvertedVessels";

        static readonly StructureSeed[] Seeds =
        {
            new StructureSeed("whole-liver", "Whole Liver", AnatomyCategory.WholeLiver, new Color(0.72f, 0.18f, 0.14f), Vector3.zero),
            new StructureSeed("segment-1", "Segment 1", AnatomyCategory.LiverSegment, new Color(0.78f, 0.18f, 0.18f), Vector3.zero),
            new StructureSeed("segment-2", "Segment 2", AnatomyCategory.LiverSegment, new Color(0.95f, 0.52f, 0.16f), Vector3.zero),
            new StructureSeed("segment-3", "Segment 3", AnatomyCategory.LiverSegment, new Color(0.96f, 0.75f, 0.22f), Vector3.zero),
            new StructureSeed("segment-4", "Segment 4", AnatomyCategory.LiverSegment, new Color(0.38f, 0.75f, 0.32f), Vector3.zero),
            new StructureSeed("segment-5", "Segment 5", AnatomyCategory.LiverSegment, new Color(0.12f, 0.58f, 0.82f), Vector3.zero),
            new StructureSeed("segment-6", "Segment 6", AnatomyCategory.LiverSegment, new Color(0.18f, 0.32f, 0.82f), Vector3.zero),
            new StructureSeed("segment-7", "Segment 7", AnatomyCategory.LiverSegment, new Color(0.47f, 0.28f, 0.78f), Vector3.zero),
            new StructureSeed("segment-8", "Segment 8", AnatomyCategory.LiverSegment, new Color(0.82f, 0.28f, 0.62f), Vector3.zero),
            new StructureSeed("blood-vessel", "Blood Vessel", AnatomyCategory.Vessel, new Color(0.10f, 0.55f, 0.95f), Vector3.zero),
        };

        [MenuItem("Liver AR/Generate Initial AR Scene")]
        public static void GenerateInitialArScene()
        {
            Directory.CreateDirectory("Assets/LiverAR/Prefabs");
            Directory.CreateDirectory("Assets/LiverAR/Configs");
            Directory.CreateDirectory(MaterialsPath);
            Directory.CreateDirectory(ConvertedVesselsPath);
            Directory.CreateDirectory("Assets/Scenes");

            CreateConfig();
            var prefab = CreateAnatomyPrefab();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "LiverARScene";

            var arSessionObject = new GameObject("AR Session");
            arSessionObject.AddComponent<ARSession>();

            var originObject = new GameObject("XR Origin");
            var origin = originObject.AddComponent<XROrigin>();
            originObject.AddComponent<ARPlaneManager>().requestedDetectionMode = PlaneDetectionMode.Horizontal;
            originObject.AddComponent<ARRaycastManager>();

            var cameraObject = new GameObject("AR Camera");
            cameraObject.transform.SetParent(originObject.transform);
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 20f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<ARCameraManager>();
            cameraObject.AddComponent<ARCameraBackground>();
            cameraObject.AddComponent<TrackedPoseDriver>();
            origin.Camera = camera;

            var managersObject = new GameObject("Liver AR Managers");
            var anatomyManager = managersObject.AddComponent<AnatomyManager>();
            var interaction = managersObject.AddComponent<ModelInteractionController>();
            var placement = managersObject.AddComponent<ARPlacementController>();
            var anatomyInteraction = managersObject.AddComponent<AnatomyInteractionController>();
            var transparency = managersObject.AddComponent<TransparencyController>();
            var reset = managersObject.AddComponent<ARSessionResetController>();
            var status = managersObject.AddComponent<ARStatusController>();
            var background = managersObject.AddComponent<ARBackgroundController>();
            var virtualSurface = GameObject.CreatePrimitive(PrimitiveType.Quad);
            virtualSurface.name = "Virtual Placement Surface";
            virtualSurface.transform.localScale = new Vector3(0.8f, 0.8f, 1f);
            virtualSurface.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            virtualSurface.SetActive(false);

            var canvas = CreateCanvas(out var ui);
            SetSerialized(ui, "anatomyManager", anatomyManager);
            SetSerialized(ui, "transparencyController", transparency);
            SetSerialized(ui, "modelInteractionController", interaction);
            SetSerialized(ui, "sessionResetController", reset);
            SetSerialized(ui, "placementController", placement);
            SetSerialized(ui, "backgroundController", background);
            SetSerialized(status, "uiController", ui);
            SetSerialized(status, "placementController", placement);
            SetSerialized(status, "planeManager", originObject.GetComponent<ARPlaneManager>());
            SetSerialized(transparency, "anatomyManager", anatomyManager);
            SetSerialized(placement, "raycastManager", originObject.GetComponent<ARRaycastManager>());
            SetSerialized(placement, "planeManager", originObject.GetComponent<ARPlaneManager>());
            SetSerialized(placement, "modelPrefab", prefab);
            SetSerialized(placement, "interactionController", interaction);
            SetSerialized(placement, "anatomyManager", anatomyManager);
            SetSerialized(placement, "arCamera", camera);
            SetSerialized(placement, "virtualSurfaceVisual", virtualSurface);
            SetSerialized(interaction, "raycastManager", originObject.GetComponent<ARRaycastManager>());
            SetSerialized(interaction, "arCamera", camera);
            SetSerialized(anatomyInteraction, "arCamera", camera);
            SetSerialized(anatomyInteraction, "anatomyManager", anatomyManager);
            SetSerialized(anatomyInteraction, "uiController", ui);
            SetSerialized(anatomyInteraction, "modelInteractionController", interaction);
            SetSerialized(reset, "session", arSessionObject.GetComponent<ARSession>());
            SetSerialized(reset, "placementController", placement);
            SetSerialized(background, "targetCamera", camera);
            SetSerialized(background, "arCameraBackground", cameraObject.GetComponent<ARCameraBackground>());
            SetSerialized(background, "planeManager", originObject.GetComponent<ARPlaneManager>());

            var lightObject = new GameObject("Directional Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var disclaimer = new GameObject("Educational Prototype Disclaimer");
            disclaimer.transform.SetParent(canvas.transform);
            var text = disclaimer.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = "This application is an educational prototype and is not intended for clinical diagnosis or treatment planning.";
            text.color = Color.white;
            text.fontSize = 18;
            text.alignment = TextAnchor.LowerCenter;
            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(0.05f, 0f);
            rect.anchorMax = new Vector2(0.95f, 0.08f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            ConfigureAndroidPlayerSettings();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            Debug.Log($"Generated Liver AR scene. Open {ScenePath}, then switch Build Settings to Android if Unity has not done so already.");
        }

        [MenuItem("Liver AR/Rebuild Anatomy Prefab From Source Models")]
        public static void RebuildAnatomyPrefabFromSourceModels()
        {
            Directory.CreateDirectory("Assets/LiverAR/Prefabs");
            Directory.CreateDirectory("Assets/LiverAR/Configs");
            Directory.CreateDirectory(MaterialsPath);
            Directory.CreateDirectory(ConvertedVesselsPath);
            AssetDatabase.ImportAsset(SourceModelsPath, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(ConvertedVesselsPath, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            CreateConfig();
            CreateAnatomyPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Rebuilt LiverAnatomyPrototype.prefab from SourceFrom3DSlicer OBJ segment models.");
        }

        [InitializeOnLoadMethod]
        static void QueueOneTimeSourceModelPrefabRebuild()
        {
            const string sessionKey = "LiverAR.SourceModelPrefabRebuilt";
            if (Application.isBatchMode || SessionState.GetBool(sessionKey, false))
                return;

            SessionState.SetBool(sessionKey, true);
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists($"{SourceModelsPath}/whole.obj") || !File.Exists($"{SourceModelsPath}/liver_segment_8.obj"))
                    return;

                RebuildAnatomyPrefabFromSourceModels();
            };
        }

        static AnatomyModelConfig CreateConfig()
        {
            var config = ScriptableObject.CreateInstance<AnatomyModelConfig>();
            var definitions = new AnatomyStructureDefinition[Seeds.Length];
            for (var i = 0; i < Seeds.Length; i++)
            {
                definitions[i] = new AnatomyStructureDefinition
                {
                    structureId = Seeds[i].Id,
                    displayName = Seeds[i].Name,
                    category = Seeds[i].Category,
                    defaultColor = Seeds[i].Color
                };
            }

            var serialized = new SerializedObject(config);
            serialized.FindProperty("structures").arraySize = definitions.Length;
            for (var i = 0; i < definitions.Length; i++)
            {
                var item = serialized.FindProperty("structures").GetArrayElementAtIndex(i);
                item.FindPropertyRelative("structureId").stringValue = definitions[i].structureId;
                item.FindPropertyRelative("displayName").stringValue = definitions[i].displayName;
                item.FindPropertyRelative("category").enumValueIndex = (int)definitions[i].category;
                item.FindPropertyRelative("defaultColor").colorValue = definitions[i].defaultColor;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.DeleteAsset(ConfigPath);
            AssetDatabase.CreateAsset(config, ConfigPath);
            AssetDatabase.SaveAssets();
            return config;
        }

        static GameObject CreateAnatomyPrefab()
        {
            var root = new GameObject("LiverAnatomyPrototype");
            var parts = new List<AnatomyPart>();

            foreach (var seed in Seeds)
            {
                var part = CreateStructure(root.transform, seed);
                if (part != null)
                    parts.Add(part);
            }

            var manager = root.AddComponent<AnatomyManager>();
            manager.SetConfiguredParts(parts.ToArray());
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        static AnatomyPart CreateStructure(Transform parent, StructureSeed seed)
        {
            var modelAsset = FindImportedModelAsset(seed);
            GameObject obj;
            if (modelAsset != null)
            {
                obj = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
                obj.name = seed.Name;
                obj.transform.localScale = Vector3.one * 0.001f;
            }
            else
            {
                if (seed.Category == AnatomyCategory.LiverSegment || seed.Category == AnatomyCategory.WholeLiver)
                {
                    Debug.LogWarning($"{seed.Name} has no Unity-renderable imported model object assigned. No procedural placeholder was generated.");
                    return null;
                }

                obj = GameObject.CreatePrimitive(seed.Category == AnatomyCategory.Vessel ? PrimitiveType.Cylinder : PrimitiveType.Sphere);
                obj.name = seed.Name + " Placeholder";
                obj.transform.localScale = seed.Category == AnatomyCategory.Vessel ? new Vector3(0.025f, 0.18f, 0.025f) : new Vector3(0.13f, 0.09f, 0.11f);
            }

            obj.transform.SetParent(parent);
            obj.transform.localPosition = seed.Position;
            obj.transform.localRotation = Quaternion.identity;

            var renderers = obj.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                renderer.sharedMaterial = CreateMaterial(seed);
            }

            if (obj.GetComponentInChildren<Collider>(true) == null)
                obj.AddComponent<BoxCollider>();

            var part = obj.AddComponent<AnatomyPart>();
            part.Configure(seed.Id, seed.Name, seed.Category, seed.Color, renderers);
            return part;
        }

        static GameObject FindImportedModelAsset(StructureSeed seed)
        {
            var explicitPath = GetModelPath(seed.Id);
            if (!string.IsNullOrWhiteSpace(explicitPath))
                return AssetDatabase.LoadAssetAtPath<GameObject>(explicitPath);

            var searchName = seed.Name.Replace(" ", "_").ToLowerInvariant();
            var guids = AssetDatabase.FindAssets($"{searchName} t:Model", new[] { SourceModelsPath });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model != null)
                    return model;
            }

            return null;
        }

        static string GetModelPath(string structureId)
        {
            switch (structureId)
            {
                case "whole-liver":
                    return $"{SourceModelsPath}/whole.obj";
                case "segment-1":
                    return $"{SourceModelsPath}/liver_segment_1.obj";
                case "segment-2":
                    return $"{SourceModelsPath}/liver_segment_2.obj";
                case "segment-3":
                    return $"{SourceModelsPath}/liver_segment_3.obj";
                case "segment-4":
                    return $"{SourceModelsPath}/liver_segment_4.obj";
                case "segment-5":
                    return $"{SourceModelsPath}/liver_segment_5.obj";
                case "segment-6":
                    return $"{SourceModelsPath}/liver_segment_6.obj";
                case "segment-7":
                    return $"{SourceModelsPath}/liver_segment_7.obj";
                case "segment-8":
                    return $"{SourceModelsPath}/liver_segment_8.obj";
                case "blood-vessel":
                    return $"{ConvertedVesselsPath}/blood-vessel.obj";
                default:
                    return string.Empty;
            }
        }

        static Material CreateMaterial(StructureSeed seed)
        {
            var materialPath = $"{MaterialsPath}/{seed.Id}-material.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            var material = existing != null ? existing : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.name = seed.Id + "-material";
            material.SetColor("_BaseColor", seed.Color);
            if (existing == null)
                AssetDatabase.CreateAsset(material, materialPath);
            return material;
        }

        static Canvas CreateCanvas(out ARUIController ui)
        {
            var canvasObject = new GameObject("Mobile AR UI");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObject.AddComponent<GraphicRaycaster>();
            ui = canvasObject.AddComponent<ARUIController>();

            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() == null)
            {
                var eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<EventSystem>();
                eventSystem.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }

            var instruction = CreateText(canvasObject.transform, "Surface Status", "Starting AR...", 22, TextAnchor.UpperCenter, new Vector2(0.08f, 0.88f), new Vector2(0.92f, 0.97f));
            var selectedName = CreateText(canvasObject.transform, "Selected Name", "No structure selected", 18, TextAnchor.UpperLeft, new Vector2(0.05f, 0.71f), new Vector2(0.48f, 0.78f));
            var selectedCategory = CreateText(canvasObject.transform, "Selected Category", "Tap an anatomical structure", 15, TextAnchor.UpperLeft, new Vector2(0.05f, 0.65f), new Vector2(0.48f, 0.71f));
            selectedName.gameObject.SetActive(false);
            selectedCategory.gameObject.SetActive(false);
            var uiController = ui;
            var placeButton = CreateButton(canvasObject.transform, "Place Liver", new Vector2(0.38f, 0.08f), new Vector2(0.24f, 0.08f), () => uiController.PlaceLiver());
            var menuButton = CreateButton(canvasObject.transform, "Menu", new Vector2(0.82f, 0.82f), new Vector2(0.12f, 0.08f), () => uiController.ToggleMenu());
            var virtualButton = CreateButton(canvasObject.transform, "Use Virtual Surface", new Vector2(0.31f, 0.18f), new Vector2(0.38f, 0.07f), () => uiController.UseVirtualSurface());
            virtualButton.gameObject.SetActive(false);

            var menuPanel = CreatePanel(canvasObject.transform, "Compact Menu", new Vector2(0.66f, 0.45f), new Vector2(0.31f, 0.35f));
            CreateButton(menuPanel.transform, "Model", new Vector2(0.08f, 0.76f), new Vector2(0.84f, 0.16f), () => uiController.SelectNormalModel());
            CreateButton(menuPanel.transform, "Segmentation", new Vector2(0.08f, 0.58f), new Vector2(0.84f, 0.16f), () => uiController.OpenSegmentationMenu());
            CreateButton(menuPanel.transform, "Settings", new Vector2(0.08f, 0.40f), new Vector2(0.84f, 0.16f), () => uiController.OpenSettingsPanel());
            CreateButton(menuPanel.transform, "Reset Placement", new Vector2(0.08f, 0.22f), new Vector2(0.84f, 0.16f), () => uiController.ResetPlacement());

            var segmentationMenuPanel = CreatePanel(canvasObject.transform, "Segmentation Menu", new Vector2(0.04f, 0.13f), new Vector2(0.32f, 0.30f));
            CreateButton(segmentationMenuPanel.transform, "Couinaud Segments", new Vector2(0.08f, 0.66f), new Vector2(0.84f, 0.20f), () => uiController.OpenCouinaudSegmentsPanel());
            CreateButton(segmentationMenuPanel.transform, "Blood Vessel", new Vector2(0.08f, 0.40f), new Vector2(0.84f, 0.20f), () => uiController.OpenVesselsPanel());
            CreateButton(segmentationMenuPanel.transform, "Back", new Vector2(0.08f, 0.14f), new Vector2(0.84f, 0.20f), () => uiController.ToggleMenu());

            var couinaudSegmentsPanel = CreatePanel(canvasObject.transform, "Couinaud Segments Panel", new Vector2(0.04f, 0.13f), new Vector2(0.32f, 0.58f));
            CreateButton(couinaudSegmentsPanel.transform, "Show All", new Vector2(0.08f, 0.18f), new Vector2(0.40f, 0.10f), () => uiController.ShowAllSegments());
            CreateButton(couinaudSegmentsPanel.transform, "Hide All", new Vector2(0.52f, 0.18f), new Vector2(0.40f, 0.10f), () => uiController.HideAllSegments());
            CreateButton(couinaudSegmentsPanel.transform, "Close", new Vector2(0.30f, 0.04f), new Vector2(0.40f, 0.10f), () => uiController.ClosePanels());

            var vesselPanel = CreatePanel(canvasObject.transform, "Vessel Panel", new Vector2(0.04f, 0.13f), new Vector2(0.32f, 0.58f));
            CreateButton(vesselPanel.transform, "Show All", new Vector2(0.08f, 0.18f), new Vector2(0.40f, 0.10f), () => uiController.ShowAllVessels());
            CreateButton(vesselPanel.transform, "Hide All", new Vector2(0.52f, 0.18f), new Vector2(0.40f, 0.10f), () => uiController.HideAllVessels());
            CreateButton(vesselPanel.transform, "Close", new Vector2(0.30f, 0.04f), new Vector2(0.40f, 0.10f), () => uiController.ClosePanels());

            var informationPanel = CreatePanel(canvasObject.transform, "Information Panel", new Vector2(0.55f, 0.16f), new Vector2(0.40f, 0.48f));
            var infoBody = CreateText(informationPanel.transform, "Information Body", "Select an anatomical structure to view details.", 15, TextAnchor.UpperLeft, new Vector2(0.06f, 0.20f), new Vector2(0.94f, 0.94f));
            CreateButton(informationPanel.transform, "Close", new Vector2(0.32f, 0.04f), new Vector2(0.36f, 0.12f), () => informationPanel.SetActive(false));

            var transparencyPanel = CreatePanel(canvasObject.transform, "Transparency Panel", new Vector2(0.55f, 0.62f), new Vector2(0.40f, 0.22f));
            var transparencyTitle = CreateText(transparencyPanel.transform, "Transparency Title", "Opacity: no selection", 15, TextAnchor.UpperLeft, new Vector2(0.06f, 0.68f), new Vector2(0.94f, 0.94f));
            CreateText(transparencyPanel.transform, "Transparency Label", "Transparency", 13, TextAnchor.MiddleLeft, new Vector2(0.06f, 0.50f), new Vector2(0.44f, 0.66f));
            var selectedOpacitySlider = CreateSlider(transparencyPanel.transform, "Selected Opacity", new Vector2(0.06f, 0.34f), new Vector2(0.88f, 0.12f), 0f, 1f, 1f);
            CreateButton(transparencyPanel.transform, "Reset", new Vector2(0.08f, 0.08f), new Vector2(0.36f, 0.16f), () => uiController.ResetSelectedTransparency());
            CreateButton(transparencyPanel.transform, "Close", new Vector2(0.56f, 0.08f), new Vector2(0.36f, 0.16f), () => transparencyPanel.SetActive(false));

            var settingsPanel = CreatePanel(canvasObject.transform, "Settings Panel", new Vector2(0.58f, 0.18f), new Vector2(0.36f, 0.42f));
            var interactionSlider = CreateSlider(settingsPanel.transform, "Interaction Sensitivity", new Vector2(0.08f, 0.70f), new Vector2(0.84f, 0.10f), 0.2f, 3f, 1f);
            var rotationSlider = CreateSlider(settingsPanel.transform, "Rotation Speed", new Vector2(0.08f, 0.54f), new Vector2(0.84f, 0.10f), 0.2f, 5f, 1f);
            var scaleSlider = CreateSlider(settingsPanel.transform, "Scale Sensitivity", new Vector2(0.08f, 0.38f), new Vector2(0.84f, 0.10f), 0.1f, 4f, 1f);
            var cameraToggle = CreateToggle(settingsPanel.transform, "Camera Background", new Vector2(0.08f, 0.48f), new Vector2(0.84f, 0.13f));
            var hapticToggle = CreateToggle(settingsPanel.transform, "Haptic Feedback", new Vector2(0.08f, 0.32f), new Vector2(0.84f, 0.13f));
            CreateButton(settingsPanel.transform, "Reset Settings", new Vector2(0.08f, 0.12f), new Vector2(0.40f, 0.13f), () => uiController.ResetSettings());
            CreateButton(settingsPanel.transform, "Back", new Vector2(0.54f, 0.12f), new Vector2(0.38f, 0.13f), () => uiController.ClosePanels());

            var editorLabel = CreateText(canvasObject.transform, "Editor Test Mode Label", "Editor Test Mode", 14, TextAnchor.UpperLeft, new Vector2(0.03f, 0.92f), new Vector2(0.30f, 0.98f));
            var modelMessage = CreateText(canvasObject.transform, "Model Message", "", 14, TextAnchor.LowerCenter, new Vector2(0.20f, 0.26f), new Vector2(0.80f, 0.32f));

            SetSerialized(ui, "instructionText", instruction);
            SetSerialized(ui, "surfaceStatusText", instruction);
            SetSerialized(ui, "selectedNameText", selectedName);
            SetSerialized(ui, "selectedCategoryText", selectedCategory);
            SetSerialized(ui, "informationBodyText", infoBody);
            SetSerialized(ui, "modelMessageText", modelMessage);
            SetSerialized(ui, "placeLiverButton", placeButton);
            SetSerialized(ui, "virtualSurfaceButton", virtualButton);
            SetSerialized(ui, "compactMenuPanel", menuPanel);
            SetSerialized(ui, "segmentationMenuPanel", segmentationMenuPanel);
            SetSerialized(ui, "couinaudSegmentsPanel", couinaudSegmentsPanel);
            SetSerialized(ui, "vesselPanel", vesselPanel);
            SetSerialized(ui, "informationPanel", informationPanel);
            SetSerialized(ui, "transparencyPanel", transparencyPanel);
            SetSerialized(ui, "transparencyTitleText", transparencyTitle);
            SetSerialized(ui, "selectedOpacitySlider", selectedOpacitySlider);
            SetSerialized(ui, "settingsPanel", settingsPanel);
            SetSerialized(ui, "editorModeLabel", editorLabel.gameObject);
            SetSerialized(ui, "interactionSensitivitySlider", interactionSlider);
            SetSerialized(ui, "rotationSpeedSlider", rotationSlider);
            SetSerialized(ui, "scaleSensitivitySlider", scaleSlider);
            SetSerialized(ui, "hapticToggle", hapticToggle);
            SetSerialized(ui, "cameraBackgroundToggle", cameraToggle);
            menuPanel.SetActive(false);
            segmentationMenuPanel.SetActive(false);
            couinaudSegmentsPanel.SetActive(false);
            vesselPanel.SetActive(false);
            informationPanel.SetActive(false);
            transparencyPanel.SetActive(false);
            settingsPanel.SetActive(false);
            return canvas;
        }

        static Text CreateText(Transform parent, string name, string value, int fontSize, TextAnchor alignment, Vector2 anchorMin, Vector2 anchorMax)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent);
            var text = obj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = alignment;
            var rect = text.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return text;
        }

        static GameObject CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 size)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent);
            var image = obj.AddComponent<Image>();
            image.color = new Color(0.08f, 0.09f, 0.10f, 0.84f);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMin + size;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return obj;
        }

        static Slider CreateSlider(Transform parent, string name, Vector2 anchorMin, Vector2 size, float min, float max, float value)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent);
            var slider = obj.AddComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;
            var rect = slider.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMin + size;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return slider;
        }

        static Toggle CreateToggle(Transform parent, string label, Vector2 anchorMin, Vector2 size)
        {
            var obj = new GameObject(label);
            obj.transform.SetParent(parent);
            var image = obj.AddComponent<Image>();
            image.color = new Color(0.08f, 0.11f, 0.14f, 0.88f);
            var toggle = obj.AddComponent<Toggle>();
            toggle.targetGraphic = image;
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMin + size;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            CreateText(obj.transform, "Label", label, 14, TextAnchor.MiddleLeft, new Vector2(0.04f, 0f), new Vector2(0.68f, 1f)).raycastTarget = false;
            CreateText(obj.transform, "Status", "ON", 12, TextAnchor.MiddleCenter, new Vector2(0.90f, 0f), new Vector2(1f, 1f)).raycastTarget = false;
            CreateToggleImage(obj.transform, "Toggle Box", new Color(0.18f, 0.23f, 0.27f, 1f), new Vector2(0.77f, 0.20f), new Vector2(0.90f, 0.80f));
            toggle.graphic = CreateToggleImage(obj.transform, "Checkmark", new Color(0.26f, 0.86f, 0.42f, 1f), new Vector2(0.795f, 0.28f), new Vector2(0.875f, 0.72f));
            return toggle;
        }

        static Image CreateToggleImage(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent);
            var image = obj.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return image;
        }

        static Button CreateButton(Transform parent, string label, Vector2 anchorMin, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            var obj = new GameObject(label + " Button");
            obj.transform.SetParent(parent);
            var image = obj.AddComponent<Image>();
            image.color = new Color(0.08f, 0.11f, 0.14f, 0.82f);
            var button = obj.AddComponent<Button>();
            button.onClick.AddListener(action);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMin + size;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var text = CreateText(obj.transform, "Label", label, 15, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
            text.color = Color.white;
            return button;
        }

        static void SetSerialized(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(scene => scene.path == scenePath))
                return;

            scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static void ConfigureAndroidPlayerSettings()
        {
            var androidTarget = NamedBuildTarget.Android;
            PlayerSettings.SetApplicationIdentifier(androidTarget, "com.fyp.liverar");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(androidTarget, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel30;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
        }

        readonly struct StructureSeed
        {
            public StructureSeed(string id, string name, AnatomyCategory category, Color color, Vector3 position)
            {
                Id = id;
                Name = name;
                Category = category;
                Color = color;
                Position = position;
            }

            public string Id { get; }
            public string Name { get; }
            public AnatomyCategory Category { get; }
            public Color Color { get; }
            public Vector3 Position { get; }
        }
    }
}

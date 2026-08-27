using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace LiverAR.Runtime
{
    public sealed class ARUIController : MonoBehaviour
    {
        [SerializeField] Text instructionText;
        [SerializeField] Text selectedNameText;
        [SerializeField] Text selectedCategoryText;
        [SerializeField] Text surfaceStatusText;
        [SerializeField] Text informationBodyText;
        [SerializeField] Text modelMessageText;
        [SerializeField] Slider selectedOpacitySlider;
        [SerializeField] Slider interactionSensitivitySlider;
        [SerializeField] Slider rotationSpeedSlider;
        [SerializeField] Slider scaleSensitivitySlider;
        [SerializeField] Toggle hapticToggle;
        [SerializeField] Toggle cameraBackgroundToggle;
        [SerializeField] Button placeLiverButton;
        [SerializeField] Button virtualSurfaceButton;
        [SerializeField] Button diseaseModelButton;
        [SerializeField] GameObject compactMenuPanel;
        [SerializeField] GameObject segmentationMenuPanel;
        [SerializeField] GameObject couinaudSegmentsPanel;
        [SerializeField] GameObject vesselPanel;
        [SerializeField] GameObject informationPanel;
        [SerializeField] GameObject transparencyPanel;
        [SerializeField] Text transparencyTitleText;
        [SerializeField] GameObject settingsPanel;
        [SerializeField] GameObject editorModeLabel;
        [SerializeField] AnatomyManager anatomyManager;
        [SerializeField] AnatomyInfoDatabase infoDatabase;
        [SerializeField] TransparencyController transparencyController;
        [SerializeField] ModelInteractionController modelInteractionController;
        [SerializeField] ARSessionResetController sessionResetController;
        [SerializeField] ARPlacementController placementController;
        [SerializeField] LiverModelSwitcher modelSwitcher;
        [SerializeField] ARBackgroundController backgroundController;

        LiverARSettings settings;
        GraphicRaycaster graphicRaycaster;
        Coroutine modelMessageRoutine;
        Image modelMessageBackground;
        readonly List<Toggle> segmentToggles = new List<Toggle>();
        readonly List<Toggle> vesselToggles = new List<Toggle>();
        bool buttonsBound;

        void Awake()
        {
            graphicRaycaster = GetComponent<GraphicRaycaster>();
            if (graphicRaycaster == null)
                graphicRaycaster = gameObject.AddComponent<GraphicRaycaster>();

            EnsureInputSystemUiModule();
            AutoWireMissingReferences();
            BuildRuntimeUiIfMissing();
            EnsureModelMessageBackground();
            EnsureCameraBackgroundToggle();
            BindSceneButtons();
            Debug.Log("Liver AR UI controller ready. Scene buttons bound once.");
        }

        void OnEnable()
        {
            if (anatomyManager != null)
                anatomyManager.SelectionChanged += OnSelectionChanged;
            if (backgroundController != null)
                backgroundController.StatusMessageChanged += OnBackgroundStatusMessageChanged;

            if (selectedOpacitySlider != null)
                selectedOpacitySlider.onValueChanged.AddListener(OnTransparencyChanged);

            settings = LiverARSettings.Load();
            BindSettingsControls();
            OnSelectionChanged(anatomyManager != null ? anatomyManager.SelectedPart : null);
            SetNavigationPanel(null);
            SetEditorModeVisible(Application.isEditor);
        }

        void OnDisable()
        {
            if (anatomyManager != null)
                anatomyManager.SelectionChanged -= OnSelectionChanged;
            if (backgroundController != null)
                backgroundController.StatusMessageChanged -= OnBackgroundStatusMessageChanged;

            if (selectedOpacitySlider != null)
                selectedOpacitySlider.onValueChanged.RemoveListener(OnTransparencyChanged);

            if (interactionSensitivitySlider != null)
                interactionSensitivitySlider.onValueChanged.RemoveListener(OnInteractionSensitivityChanged);
            if (rotationSpeedSlider != null)
                rotationSpeedSlider.onValueChanged.RemoveListener(OnRotationSpeedChanged);
            if (scaleSensitivitySlider != null)
                scaleSensitivitySlider.onValueChanged.RemoveListener(OnScaleSensitivityChanged);
            if (hapticToggle != null)
                hapticToggle.onValueChanged.RemoveListener(OnHapticChanged);
            if (cameraBackgroundToggle != null)
                cameraBackgroundToggle.onValueChanged.RemoveListener(OnCameraBackgroundChanged);
        }

        public void SetInstruction(string message)
        {
            if (instructionText != null)
                instructionText.text = message;
        }

        public void SetSurfaceState(ARSurfaceState state)
        {
            var message = ARSurfaceStatusMessage.GetMessage(state);
            SetInstruction(message);
            if (surfaceStatusText != null)
                surfaceStatusText.text = message;
            if (instructionText != null)
                instructionText.gameObject.SetActive(!string.IsNullOrWhiteSpace(message));
            if (surfaceStatusText != null)
                surfaceStatusText.gameObject.SetActive(!string.IsNullOrWhiteSpace(message));
            if (placeLiverButton != null)
                placeLiverButton.interactable = state != ARSurfaceState.Placed && state != ARSurfaceState.Unsupported;
        }

        public void SetVirtualSurfaceOptionVisible(bool visible)
        {
            if (virtualSurfaceButton != null)
                virtualSurfaceButton.gameObject.SetActive(visible);
        }

        public void PlaceLiver()
        {
            if (placementController == null)
            {
                Debug.LogWarning("Place Liver clicked, but no ARPlacementController is available.");
                ShowTemporaryModelMessage("Liver placement failed. Please try again.", 2.5f, false);
                return;
            }

            ShowPersistentModelMessage("Placing liver...", false);
            if (!placementController.PlaceLiver())
            {
                var failure = string.IsNullOrWhiteSpace(placementController.LastPlacementFailureMessage)
                    ? "Liver placement failed. Please try again."
                    : placementController.LastPlacementFailureMessage;
                Debug.LogWarning("Place Liver clicked, but no placement pose is available yet.");
                ShowTemporaryModelMessage(failure, 2.5f, false);
                return;
            }

            if (placementController.TryValidatePlacedModelVisibility(out var reason))
            {
                ShowTemporaryModelMessage("Liver placed successfully.", 2.5f, true);
                return;
            }

            ShowTemporaryModelMessage(string.IsNullOrWhiteSpace(reason) ? "Liver placement failed. Please try again." : reason, 3f, false);
        }

        public void UseVirtualSurface() => placementController?.UseVirtualSurface();
        public void ToggleMenu() => SetNavigationPanel(compactMenuPanel != null && compactMenuPanel.activeSelf ? null : compactMenuPanel);
        public void OpenSegmentationMenu()
        {
            UpdateVesselOptionVisibility();
            SetNavigationPanel(segmentationMenuPanel);
        }
        public void OpenCouinaudSegmentsPanel()
        {
            anatomyManager?.ShowLiverSegments();
            RebuildSegmentToggles();
            SetNavigationPanel(couinaudSegmentsPanel);
        }
        public void OpenVesselsPanel()
        {
            RebuildVesselToggles();
            SetNavigationPanel(vesselPanel);
        }
        public void OpenInformationPanelForSelection() => SetPanelActive(informationPanel, informationPanel);
        public void OpenTransparencyPanelForSelection() => SetPanelActive(transparencyPanel, transparencyPanel);
        public void OpenSettingsPanel() => SetNavigationPanel(settingsPanel);
        public void ClosePanels() => SetNavigationPanel(null);
        public void ResetPlacement() => sessionResetController?.ResetSession();

        public void SelectNormalModel()
        {
            if (placementController != null && placementController.SwitchModel(LiverModelType.Normal))
            {
                anatomyManager?.ClearSelection();
                SetModelMessage(string.Empty);
                return;
            }

            if (modelSwitcher != null && modelSwitcher.SwitchTo(LiverModelType.Normal))
                SetModelMessage(string.Empty);
        }

        public void SelectDiseaseModel()
        {
            if (placementController != null && placementController.SwitchModel(LiverModelType.Disease))
            {
                anatomyManager?.ClearSelection();
                SetModelMessage(string.Empty);
                return;
            }

            if (modelSwitcher == null || !modelSwitcher.SwitchTo(LiverModelType.Disease))
                SetModelMessage("Disease model not yet assigned.");
        }

        public void ShowSegments()
        {
            anatomyManager?.ShowLiverSegments();
            RebuildSegmentToggles();
        }
        public void ShowVessels() => ShowCategory(AnatomyCategory.Vessel);
        public void ShowAllSegments()
        {
            anatomyManager?.ShowLiverSegments();
            SetTogglesOn(segmentToggles, true);
        }

        public void HideAllSegments() => SetCategoryVisible(AnatomyCategory.LiverSegment, false, segmentToggles);
        public void ShowAllVessels() => SetCategoryVisible(AnatomyCategory.Vessel, true, vesselToggles);
        public void HideAllVessels() => SetCategoryVisible(AnatomyCategory.Vessel, false, vesselToggles);

        public void IsolateSelected()
        {
            if (anatomyManager == null || anatomyManager.SelectedPart == null)
                return;

            var selected = anatomyManager.SelectedPart;
            foreach (var part in anatomyManager.Parts)
                part.SetVisible(part == selected);
        }

        public void ShowAll() => anatomyManager?.ShowAll();
        public void HideAll()
        {
            anatomyManager?.HideAll();
            foreach (var toggle in segmentToggles)
            {
                toggle.SetIsOnWithoutNotify(false);
                UpdateToggleStatusText(toggle);
            }
        }
        public void ResetAppearance()
        {
            anatomyManager?.ResetAllAppearances();
            foreach (var toggle in segmentToggles)
            {
                toggle.SetIsOnWithoutNotify(true);
                UpdateToggleStatusText(toggle);
            }
        }
        public void ResetModelTransform() => modelInteractionController?.ResetTransform();
        public void ResetARSession() => sessionResetController?.ResetSession();
        public void ClearSelection() => anatomyManager?.ClearSelection();
        public void ShowSelected() => anatomyManager?.SelectedPart?.SetVisible(true);
        public void HideSelected() => anatomyManager?.SelectedPart?.SetVisible(false);

        public void SetSelectedColor(Color color)
        {
            Debug.Log("Colour editing is intentionally disabled; imported model materials are preserved.");
        }

        void OnTransparencyChanged(float value)
        {
            transparencyController?.SetSelectedOpacity(value);
        }

        void OnSelectionChanged(AnatomyPart part)
        {
            var hasSelection = part != null;
            if (selectedNameText != null)
                selectedNameText.text = hasSelection ? part.DisplayName : "No structure selected";
            if (selectedCategoryText != null)
                selectedCategoryText.text = hasSelection ? part.Category.ToString() : "Tap an anatomical structure";
            if (selectedOpacitySlider != null)
            {
                selectedOpacitySlider.interactable = hasSelection;
                if (hasSelection)
                    selectedOpacitySlider.SetValueWithoutNotify(part.Opacity);
            }

            if (transparencyTitleText != null)
                transparencyTitleText.text = hasSelection ? $"Opacity: {part.DisplayName}" : "Opacity: no selection";

            UpdateInformationPanel(part);
            if (hasSelection)
                OpenInformationPanelForSelection();
        }

        void UpdateVesselOptionVisibility()
        {
            if (segmentationMenuPanel == null)
                return;

            var vesselButton = segmentationMenuPanel.transform.Find("Vessels Button");
            if (vesselButton != null)
                vesselButton.gameObject.SetActive(HasAnatomyPart(AnatomyCategory.Vessel));
        }

        bool HasAnatomyPart(AnatomyCategory category)
        {
            if (anatomyManager == null)
                return false;

            foreach (var part in anatomyManager.Parts)
            {
                if (part != null && part.Category == category)
                    return true;
            }

            return false;
        }

        void ShowCategory(AnatomyCategory category)
        {
            if (anatomyManager == null)
                return;

            foreach (var part in anatomyManager.Parts)
                part.SetVisible(part.Category == category);
            anatomyManager.ClearSelection();
        }

        void SetNavigationPanel(GameObject activePanel)
        {
            SetPanelActive(compactMenuPanel, activePanel);
            SetPanelActive(segmentationMenuPanel, activePanel);
            SetPanelActive(couinaudSegmentsPanel, activePanel);
            SetPanelActive(vesselPanel, activePanel);
            SetPanelActive(settingsPanel, activePanel);
        }

        static void SetPanelActive(GameObject panel, GameObject activePanel)
        {
            if (panel != null)
                panel.SetActive(panel == activePanel);
        }

        void UpdateInformationPanel(AnatomyPart part)
        {
            if (informationBodyText == null)
                return;

            if (part == null)
            {
                informationBodyText.text = "Select an anatomical structure to view details.";
                return;
            }

            if (infoDatabase != null && infoDatabase.TryGetInfo(part.StructureId, out var record))
            {
                informationBodyText.text =
                    $"Location: {ValueOrPlaceholder(record.location)}\n" +
                    $"Blood supply: {ValueOrPlaceholder(record.bloodSupply)}\n" +
                    $"Venous drainage: {ValueOrPlaceholder(record.venousDrainage)}\n" +
                    $"Function: {ValueOrPlaceholder(record.function)}\n\n" +
                    ValueOrPlaceholder(record.educationalDescription);
                return;
            }

            informationBodyText.text = $"{part.DisplayName}\n{part.Category}\n\nEducational information placeholder - verified content not yet assigned.";
        }

        static string ValueOrPlaceholder(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "Placeholder - verified content not yet assigned" : value;
        }

        void BindSettingsControls()
        {
            if (settings == null)
                settings = LiverARSettings.CreateDefault();

            modelInteractionController?.ApplySettings(settings);

            if (interactionSensitivitySlider != null)
            {
                interactionSensitivitySlider.onValueChanged.RemoveListener(OnInteractionSensitivityChanged);
                interactionSensitivitySlider.SetValueWithoutNotify(settings.InteractionSensitivity);
                interactionSensitivitySlider.onValueChanged.AddListener(OnInteractionSensitivityChanged);
            }
            if (rotationSpeedSlider != null)
            {
                rotationSpeedSlider.onValueChanged.RemoveListener(OnRotationSpeedChanged);
                rotationSpeedSlider.SetValueWithoutNotify(settings.RotationSpeed);
                rotationSpeedSlider.onValueChanged.AddListener(OnRotationSpeedChanged);
            }
            if (scaleSensitivitySlider != null)
            {
                scaleSensitivitySlider.onValueChanged.RemoveListener(OnScaleSensitivityChanged);
                scaleSensitivitySlider.SetValueWithoutNotify(settings.ScaleSensitivity);
                scaleSensitivitySlider.onValueChanged.AddListener(OnScaleSensitivityChanged);
            }
            if (hapticToggle != null)
            {
                hapticToggle.onValueChanged.RemoveListener(OnHapticChanged);
                hapticToggle.SetIsOnWithoutNotify(settings.HapticFeedback);
                UpdateToggleStatusText(hapticToggle);
                hapticToggle.onValueChanged.AddListener(OnHapticChanged);
            }
            if (cameraBackgroundToggle != null)
            {
                cameraBackgroundToggle.onValueChanged.RemoveListener(OnCameraBackgroundChanged);
                cameraBackgroundToggle.SetIsOnWithoutNotify(backgroundController == null || backgroundController.IsCameraBackgroundEnabled);
                UpdateToggleStatusText(cameraBackgroundToggle);
                cameraBackgroundToggle.onValueChanged.AddListener(OnCameraBackgroundChanged);
            }

            if (diseaseModelButton != null)
                diseaseModelButton.interactable = placementController != null ? placementController.HasDiseaseModel : modelSwitcher == null || modelSwitcher.HasDiseaseModel;
        }

        void OnInteractionSensitivityChanged(float value)
        {
            settings.SetInteractionSensitivity(value);
            ApplyAndSaveSettings();
        }

        void OnRotationSpeedChanged(float value)
        {
            settings.SetRotationSpeed(value);
            ApplyAndSaveSettings();
        }

        void OnScaleSensitivityChanged(float value)
        {
            settings.SetScaleSensitivity(value);
            ApplyAndSaveSettings();
        }

        void OnHapticChanged(bool value)
        {
            settings.SetHapticFeedback(value);
            UpdateToggleStatusText(hapticToggle);
            ApplyAndSaveSettings();
        }

        void OnCameraBackgroundChanged(bool enabled)
        {
            if (backgroundController == null)
            {
                SetModelMessage(enabled ? "Camera background unavailable. Virtual environment is active." : "Virtual environment active.");
                if (cameraBackgroundToggle != null)
                {
                    cameraBackgroundToggle.SetIsOnWithoutNotify(false);
                    UpdateToggleStatusText(cameraBackgroundToggle);
                }
                return;
            }

            if (!backgroundController.SetCameraBackgroundEnabled(enabled))
            {
                if (cameraBackgroundToggle != null)
                {
                    cameraBackgroundToggle.SetIsOnWithoutNotify(false);
                    UpdateToggleStatusText(cameraBackgroundToggle);
                }
            }

            UpdateToggleStatusText(cameraBackgroundToggle);
        }

        void OnBackgroundStatusMessageChanged(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            SetInstruction(message);
            SetModelMessage(message);
        }

        public void ResetSettings()
        {
            settings.Reset();
            settings.Save();
            backgroundController?.SetCameraBackgroundEnabled(!Application.isEditor);
            BindSettingsControls();
        }

        void ApplyAndSaveSettings()
        {
            settings.Save();
            modelInteractionController?.ApplySettings(settings);
        }

        void SetModelMessage(string message)
        {
            if (modelMessageText != null)
            {
                modelMessageText.text = message;
                modelMessageText.gameObject.SetActive(!string.IsNullOrWhiteSpace(message));
            }

            if (modelMessageBackground != null)
                modelMessageBackground.gameObject.SetActive(!string.IsNullOrWhiteSpace(message));
        }

        void ShowPersistentModelMessage(string message, bool success)
        {
            if (modelMessageRoutine != null)
            {
                StopCoroutine(modelMessageRoutine);
                modelMessageRoutine = null;
            }

            ApplyModelMessageStyle(success);
            SetModelMessage(message);
        }

        void ShowTemporaryModelMessage(string message, float duration, bool success)
        {
            ShowPersistentModelMessage(message, success);
            if (isActiveAndEnabled)
                modelMessageRoutine = StartCoroutine(HideModelMessageAfterDelay(duration));
        }

        IEnumerator HideModelMessageAfterDelay(float duration)
        {
            yield return new WaitForSeconds(duration);
            SetModelMessage(string.Empty);
            modelMessageRoutine = null;
        }

        void ApplyModelMessageStyle(bool success)
        {
            if (modelMessageBackground != null)
                modelMessageBackground.color = success ? new Color(0.07f, 0.35f, 0.18f, 0.88f) : new Color(0.12f, 0.13f, 0.14f, 0.88f);
        }

        void SetEditorModeVisible(bool visible)
        {
            if (editorModeLabel != null)
                editorModeLabel.SetActive(visible);
        }

        void BuildRuntimeUiIfMissing()
        {
            if (placeLiverButton != null)
                return;

            HideObsoleteControls();
            var root = transform;
            placeLiverButton = CreateRuntimeButton(root, "Place Liver", new Vector2(0.38f, 0.08f), new Vector2(0.24f, 0.08f), PlaceLiver);
            CreateRuntimeButton(root, "Menu", new Vector2(0.82f, 0.82f), new Vector2(0.12f, 0.08f), ToggleMenu);
            virtualSurfaceButton = CreateRuntimeButton(root, "Use Virtual Surface", new Vector2(0.31f, 0.18f), new Vector2(0.38f, 0.07f), UseVirtualSurface);
            virtualSurfaceButton.gameObject.SetActive(false);

            compactMenuPanel = CreateRuntimePanel(root, "Compact Menu", new Vector2(0.66f, 0.45f), new Vector2(0.31f, 0.35f));
            CreateRuntimeButton(compactMenuPanel.transform, "Model", new Vector2(0.08f, 0.76f), new Vector2(0.84f, 0.16f), SelectNormalModel);
            CreateRuntimeButton(compactMenuPanel.transform, "Segmentation", new Vector2(0.08f, 0.58f), new Vector2(0.84f, 0.16f), OpenSegmentationMenu);
            CreateRuntimeButton(compactMenuPanel.transform, "Settings", new Vector2(0.08f, 0.40f), new Vector2(0.84f, 0.16f), OpenSettingsPanel);
            CreateRuntimeButton(compactMenuPanel.transform, "Reset Placement", new Vector2(0.08f, 0.22f), new Vector2(0.84f, 0.16f), ResetPlacement);

            segmentationMenuPanel = CreateRuntimePanel(root, "Segmentation Menu", new Vector2(0.04f, 0.13f), new Vector2(0.32f, 0.30f));
            CreateRuntimeButton(segmentationMenuPanel.transform, "Couinaud Segments", new Vector2(0.08f, 0.66f), new Vector2(0.84f, 0.20f), OpenCouinaudSegmentsPanel);
            CreateRuntimeButton(segmentationMenuPanel.transform, "Vessels", new Vector2(0.08f, 0.40f), new Vector2(0.84f, 0.20f), OpenVesselsPanel);
            CreateRuntimeButton(segmentationMenuPanel.transform, "Back", new Vector2(0.08f, 0.14f), new Vector2(0.84f, 0.20f), ToggleMenu);

            couinaudSegmentsPanel = CreateRuntimePanel(root, "Couinaud Segments Panel", new Vector2(0.04f, 0.13f), new Vector2(0.32f, 0.58f));
            CreateRuntimeButton(couinaudSegmentsPanel.transform, "Show All", new Vector2(0.08f, 0.18f), new Vector2(0.40f, 0.10f), ShowAllSegments);
            CreateRuntimeButton(couinaudSegmentsPanel.transform, "Hide All", new Vector2(0.52f, 0.18f), new Vector2(0.40f, 0.10f), HideAllSegments);
            CreateRuntimeButton(couinaudSegmentsPanel.transform, "Close", new Vector2(0.30f, 0.04f), new Vector2(0.40f, 0.10f), ClosePanels);

            vesselPanel = CreateRuntimePanel(root, "Vessel Panel", new Vector2(0.04f, 0.13f), new Vector2(0.32f, 0.58f));
            CreateRuntimeButton(vesselPanel.transform, "Show All", new Vector2(0.08f, 0.18f), new Vector2(0.40f, 0.10f), ShowAllVessels);
            CreateRuntimeButton(vesselPanel.transform, "Hide All", new Vector2(0.52f, 0.18f), new Vector2(0.40f, 0.10f), HideAllVessels);
            CreateRuntimeButton(vesselPanel.transform, "Close", new Vector2(0.30f, 0.04f), new Vector2(0.40f, 0.10f), ClosePanels);

            informationPanel = CreateRuntimePanel(root, "Information Panel", new Vector2(0.55f, 0.16f), new Vector2(0.40f, 0.48f));
            informationBodyText = CreateRuntimeText(informationPanel.transform, "Information Body", "Select an anatomical structure to view details.", 15, TextAnchor.UpperLeft, new Vector2(0.06f, 0.20f), new Vector2(0.94f, 0.94f));
            CreateRuntimeButton(informationPanel.transform, "Close", new Vector2(0.32f, 0.04f), new Vector2(0.36f, 0.12f), () => SetPanelActive(informationPanel, null));

            transparencyPanel = CreateRuntimePanel(root, "Transparency Panel", new Vector2(0.55f, 0.66f), new Vector2(0.40f, 0.18f));
            transparencyTitleText = CreateRuntimeText(transparencyPanel.transform, "Transparency Title", "Opacity: no selection", 15, TextAnchor.UpperLeft, new Vector2(0.06f, 0.62f), new Vector2(0.94f, 0.94f));
            selectedOpacitySlider = CreateRuntimeSlider(transparencyPanel.transform, "Selected Opacity", new Vector2(0.06f, 0.32f), new Vector2(0.88f, 0.14f), 0f, 1f, 1f);
            CreateRuntimeButton(transparencyPanel.transform, "Close", new Vector2(0.32f, 0.06f), new Vector2(0.36f, 0.16f), () => SetPanelActive(transparencyPanel, null));

            settingsPanel = CreateRuntimePanel(root, "Settings Panel", new Vector2(0.58f, 0.18f), new Vector2(0.36f, 0.42f));
            interactionSensitivitySlider = CreateRuntimeSlider(settingsPanel.transform, "Interaction Sensitivity", new Vector2(0.08f, 0.70f), new Vector2(0.84f, 0.10f), 0.2f, 3f, 1f);
            rotationSpeedSlider = CreateRuntimeSlider(settingsPanel.transform, "Rotation Speed", new Vector2(0.08f, 0.54f), new Vector2(0.84f, 0.10f), 0.2f, 5f, 1f);
            scaleSensitivitySlider = CreateRuntimeSlider(settingsPanel.transform, "Scale Sensitivity", new Vector2(0.08f, 0.38f), new Vector2(0.84f, 0.10f), 0.1f, 4f, 1f);
            cameraBackgroundToggle = CreateRuntimeToggle(settingsPanel.transform, "Camera Background", new Vector2(0.08f, 0.25f), new Vector2(0.84f, 0.10f));
            CreateRuntimeButton(settingsPanel.transform, "Reset Settings", new Vector2(0.08f, 0.08f), new Vector2(0.40f, 0.12f), ResetSettings);
            CreateRuntimeButton(settingsPanel.transform, "Back", new Vector2(0.54f, 0.08f), new Vector2(0.38f, 0.12f), ClosePanels);

            editorModeLabel = CreateRuntimeText(root, "Editor Test Mode Label", "Editor Test Mode", 14, TextAnchor.UpperLeft, new Vector2(0.03f, 0.92f), new Vector2(0.30f, 0.98f)).gameObject;
            modelMessageText = CreateRuntimeText(root, "Model Message", "", 14, TextAnchor.LowerCenter, new Vector2(0.20f, 0.26f), new Vector2(0.80f, 0.32f));
        }

        void EnsureCameraBackgroundToggle()
        {
            if (settingsPanel == null)
                return;

            EnsureSettingsPanelLayout();

            if (cameraBackgroundToggle == null)
                cameraBackgroundToggle = CreateRuntimeToggle(settingsPanel.transform, "Camera Background", new Vector2(0.08f, 0.48f), new Vector2(0.84f, 0.13f));

            EnsureToggleVisual(cameraBackgroundToggle, "Camera Background", null);
            if (hapticToggle != null)
                EnsureToggleVisual(hapticToggle, "Haptic Feedback", null);
        }

        void BindSceneButtons()
        {
            if (buttonsBound)
                return;

            BindButton("Place Liver Button", PlaceLiver);
            BindButton("Menu Button", ToggleMenu);
            BindButton("Use Virtual Surface Button", UseVirtualSurface);
            BindButton("Model Button", SelectNormalModel);
            BindButton("Normal Liver Button", SelectNormalModel);
            BindButton("Disease Liver Button", SelectDiseaseModel);
            BindButton("Segments Button", OpenCouinaudSegmentsPanel);
            BindButton("Isolate Button", IsolateSelected);
            BindButton("Reset Settings Button", ResetSettings);
            BindButton("Reset Segments Button", ResetAppearance);

            BindPanelButton(compactMenuPanel, "Model Button", SelectNormalModel);
            BindPanelButton(compactMenuPanel, "Segmentation Button", OpenSegmentationMenu);
            BindPanelButton(compactMenuPanel, "Settings Button", OpenSettingsPanel);
            BindPanelButton(compactMenuPanel, "Reset Placement Button", ResetPlacement);
            BindPanelButton(segmentationMenuPanel, "Couinaud Segments Button", OpenCouinaudSegmentsPanel);
            BindPanelButton(segmentationMenuPanel, "Vessels Button", OpenVesselsPanel);
            BindPanelButton(segmentationMenuPanel, "Back Button", ToggleMenu);
            BindPanelButton(couinaudSegmentsPanel, "Show All Button", ShowAllSegments);
            BindPanelButton(couinaudSegmentsPanel, "Hide All Button", HideAllSegments);
            BindPanelButton(couinaudSegmentsPanel, "Close Button", ClosePanels);
            BindPanelButton(vesselPanel, "Show All Button", ShowAllVessels);
            BindPanelButton(vesselPanel, "Hide All Button", HideAllVessels);
            BindPanelButton(vesselPanel, "Close Button", ClosePanels);
            BindPanelButton(settingsPanel, "Back Button", ClosePanels);
            BindPanelButton(informationPanel, "Close Button", () => SetPanelActive(informationPanel, null));
            BindPanelButton(transparencyPanel, "Close Button", () => SetPanelActive(transparencyPanel, null));
            buttonsBound = true;
        }

        static void BindPanelButton(GameObject panel, string objectName, UnityEngine.Events.UnityAction action)
        {
            if (panel == null)
                return;

            var button = panel.transform.Find(objectName)?.GetComponent<Button>();
            if (button == null)
                return;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        void BindButton(string objectName, UnityEngine.Events.UnityAction action)
        {
            foreach (var button in GetComponentsInChildren<Button>(true))
            {
                if (button.gameObject.name != objectName)
                    continue;

                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(action);
            }
        }

        void HideObsoleteControls()
        {
            var obsoleteNames = new[]
            {
                "Show All Button", "Hide All Button", "Reset Look Button", "Reset Model Button",
                "Reset AR Button", "Clear Button", "Show Sel Button", "Hide Sel Button", "Colour Button"
            };

            foreach (var obsoleteName in obsoleteNames)
            {
                var child = transform.Find(obsoleteName);
                if (child != null)
                    child.gameObject.SetActive(false);
            }
        }

        static GameObject CreateRuntimePanel(Transform parent, string name, Vector2 anchorMin, Vector2 size)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var image = obj.AddComponent<Image>();
            image.color = new Color(0.08f, 0.09f, 0.10f, 0.84f);
            SetAnchors(obj.GetComponent<RectTransform>(), anchorMin, size);
            obj.SetActive(false);
            return obj;
        }

        static Button CreateRuntimeButton(Transform parent, string label, Vector2 anchorMin, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            var obj = new GameObject(label + " Button");
            obj.transform.SetParent(parent, false);
            var image = obj.AddComponent<Image>();
            image.color = new Color(0.08f, 0.11f, 0.14f, 0.86f);
            var button = obj.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            SetAnchors(obj.GetComponent<RectTransform>(), anchorMin, size);
            CreateRuntimeText(obj.transform, "Label", label, 15, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
            return button;
        }

        static Text CreateRuntimeText(Transform parent, string name, string value, int fontSize, TextAnchor alignment, Vector2 anchorMin, Vector2 anchorMax)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
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

        static Slider CreateRuntimeSlider(Transform parent, string name, Vector2 anchorMin, Vector2 size, float min, float max, float value)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var slider = obj.AddComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;
            SetAnchors(obj.GetComponent<RectTransform>(), anchorMin, size);
            return slider;
        }

        static Toggle CreateRuntimeToggle(Transform parent, string label, Vector2 anchorMin, Vector2 size)
        {
            var obj = new GameObject(label);
            obj.transform.SetParent(parent, false);
            var toggle = obj.AddComponent<Toggle>();
            SetAnchors(obj.GetComponent<RectTransform>(), anchorMin, size);
            EnsureToggleVisual(toggle, label, null);
            return toggle;
        }

        void EnsureSettingsPanelLayout()
        {
            SetAnchorsIfPresent(settingsPanel.transform, "Camera Background", new Vector2(0.08f, 0.48f), new Vector2(0.84f, 0.13f));
            SetAnchorsIfPresent(settingsPanel.transform, "Haptic Feedback", new Vector2(0.08f, 0.32f), new Vector2(0.84f, 0.13f));
            SetAnchorsIfPresent(settingsPanel.transform, "Reset Settings Button", new Vector2(0.08f, 0.12f), new Vector2(0.40f, 0.13f));
            SetAnchorsIfPresent(settingsPanel.transform, "Back Button", new Vector2(0.54f, 0.12f), new Vector2(0.38f, 0.13f));

            if (hapticToggle != null)
                SetAnchors(hapticToggle.GetComponent<RectTransform>(), new Vector2(0.08f, 0.32f), new Vector2(0.84f, 0.13f));
            if (cameraBackgroundToggle != null)
                SetAnchors(cameraBackgroundToggle.GetComponent<RectTransform>(), new Vector2(0.08f, 0.48f), new Vector2(0.84f, 0.13f));
        }

        void RebuildSegmentToggles()
        {
            RebuildAnatomyToggles(couinaudSegmentsPanel, "Segment Toggle Rows", AnatomyCategory.LiverSegment, segmentToggles, FormatSegmentButtonLabel);
        }

        void RebuildVesselToggles()
        {
            RebuildAnatomyToggles(vesselPanel, "Vessel Toggle Rows", AnatomyCategory.Vessel, vesselToggles, FormatVesselButtonLabel);
        }

        void RebuildAnatomyToggles(GameObject panel, string containerName, AnatomyCategory category, List<Toggle> toggles, System.Func<AnatomyPart, int, string> labelFactory)
        {
            if (panel == null || anatomyManager == null)
                return;

            var existingContainer = panel.transform.Find(containerName);
            if (existingContainer != null)
                Destroy(existingContainer.gameObject);

            toggles.Clear();
            var anatomyParts = new List<AnatomyPart>();
            foreach (var part in anatomyManager.Parts)
            {
                if (part != null && part.Category == category)
                    anatomyParts.Add(part);
            }

            anatomyParts.Sort((left, right) => string.Compare(left.StructureId, right.StructureId, System.StringComparison.OrdinalIgnoreCase));
            if (anatomyParts.Count == 0)
                return;

            var container = new GameObject(containerName);
            container.transform.SetParent(panel.transform, false);
            SetAnchors(container.AddComponent<RectTransform>(), new Vector2(0.06f, 0.30f), new Vector2(0.88f, 0.64f));

            var rowHeight = Mathf.Min(0.10f, 0.92f / anatomyParts.Count);
            for (var index = 0; index < anatomyParts.Count; index++)
            {
                var part = anatomyParts[index];
                var y = 0.96f - rowHeight * (index + 1);
                var label = labelFactory(part, index);
                var toggle = CreateRuntimeToggle(container.transform, label, new Vector2(0f, y), new Vector2(1f, rowHeight * 0.86f));
                EnsureToggleVisual(toggle, label, part.DefaultColor);
                toggle.SetIsOnWithoutNotify(part.IsVisible);
                UpdateToggleStatusText(toggle);
                toggle.onValueChanged.AddListener(isOn =>
                {
                    part.SetVisible(isOn);
                    if (!isOn && anatomyManager.SelectedPart == part)
                        anatomyManager.ClearSelection();
                    UpdateToggleStatusText(toggle);
                });
                toggles.Add(toggle);
            }
        }

        void SetCategoryVisible(AnatomyCategory category, bool visible, List<Toggle> toggles)
        {
            if (anatomyManager != null)
            {
                foreach (var part in anatomyManager.Parts)
                {
                    if (part != null && part.Category == category)
                        part.SetVisible(visible);
                }
            }

            SetTogglesOn(toggles, visible);
        }

        static void SetTogglesOn(List<Toggle> toggles, bool isOn)
        {
            foreach (var toggle in toggles)
            {
                toggle.SetIsOnWithoutNotify(isOn);
                UpdateToggleStatusText(toggle);
            }
        }

        static string FormatSegmentButtonLabel(AnatomyPart part, int index)
        {
            if (part != null && !string.IsNullOrWhiteSpace(part.StructureId) && part.StructureId.StartsWith("segment-"))
            {
                var suffix = part.StructureId.Substring("segment-".Length);
                if (int.TryParse(suffix, out var number))
                    return $"Segment {number}";
            }

            return $"Segment {index + 1}";
        }

        static string FormatVesselButtonLabel(AnatomyPart part, int index)
        {
            return part != null && !string.IsNullOrWhiteSpace(part.DisplayName) ? part.DisplayName : $"Vessel {index + 1}";
        }

        static void EnsureToggleVisual(Toggle toggle, string label, Color? swatchColor)
        {
            if (toggle == null)
                return;

            var rootImage = toggle.GetComponent<Image>();
            if (rootImage == null)
                rootImage = toggle.gameObject.AddComponent<Image>();
            rootImage.color = new Color(0.08f, 0.11f, 0.14f, 0.88f);
            toggle.targetGraphic = rootImage;

            EnsureChildImage(toggle.transform, "Toggle Box", new Color(0.18f, 0.23f, 0.27f, 1f), new Vector2(0.77f, 0.20f), new Vector2(0.90f, 0.80f));
            var check = EnsureChildImage(toggle.transform, "Checkmark", new Color(0.26f, 0.86f, 0.42f, 1f), new Vector2(0.795f, 0.28f), new Vector2(0.875f, 0.72f));
            toggle.graphic = check;

            var labelText = EnsureChildText(toggle.transform, "Label", label, 16, TextAnchor.MiddleLeft, new Vector2(swatchColor.HasValue ? 0.16f : 0.04f, 0f), new Vector2(0.72f, 1f));
            labelText.raycastTarget = false;
            labelText.color = Color.white;

            var status = EnsureChildText(toggle.transform, "Status", toggle.isOn ? "ON" : "OFF", 12, TextAnchor.MiddleCenter, new Vector2(0.90f, 0f), new Vector2(1f, 1f));
            status.raycastTarget = false;

            if (swatchColor.HasValue)
                EnsureChildImage(toggle.transform, "Colour", swatchColor.Value, new Vector2(0.04f, 0.28f), new Vector2(0.12f, 0.72f));
        }

        static Image EnsureChildImage(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax)
        {
            var child = parent.Find(name);
            if (child == null)
            {
                var obj = new GameObject(name);
                obj.transform.SetParent(parent, false);
                child = obj.transform;
            }

            var image = child.GetComponent<Image>();
            if (image == null)
                image = child.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return image;
        }

        static Text EnsureChildText(Transform parent, string name, string value, int fontSize, TextAnchor alignment, Vector2 anchorMin, Vector2 anchorMax)
        {
            var child = parent.Find(name);
            if (child == null)
            {
                var obj = new GameObject(name);
                obj.transform.SetParent(parent, false);
                child = obj.transform;
            }

            var text = child.GetComponent<Text>();
            if (text == null)
                text = child.gameObject.AddComponent<Text>();
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

        static void UpdateToggleStatusText(Toggle toggle)
        {
            if (toggle == null)
                return;

            var status = toggle.transform.Find("Status")?.GetComponent<Text>();
            if (status != null)
                status.text = toggle.isOn ? "ON" : "OFF";
        }

        void EnsureModelMessageBackground()
        {
            if (modelMessageText == null || modelMessageBackground != null)
                return;

            var backgroundObject = new GameObject("Model Message Background");
            backgroundObject.transform.SetParent(modelMessageText.transform.parent, false);
            modelMessageBackground = backgroundObject.AddComponent<Image>();
            modelMessageBackground.color = new Color(0.12f, 0.13f, 0.14f, 0.88f);
            var messageRect = modelMessageText.rectTransform;
            var backgroundRect = modelMessageBackground.rectTransform;
            backgroundRect.anchorMin = messageRect.anchorMin;
            backgroundRect.anchorMax = messageRect.anchorMax;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;
            backgroundObject.transform.SetSiblingIndex(modelMessageText.transform.GetSiblingIndex());
            modelMessageText.transform.SetAsLastSibling();
            modelMessageBackground.gameObject.SetActive(false);
            modelMessageText.gameObject.SetActive(false);
        }

        static void SetAnchorsIfPresent(Transform parent, string childName, Vector2 anchorMin, Vector2 size)
        {
            var child = parent.Find(childName);
            if (child != null)
                SetAnchors(child.GetComponent<RectTransform>(), anchorMin, size);
        }

        static void SetChildActive(Transform parent, string childName, bool active)
        {
            var child = parent.Find(childName);
            if (child != null)
                child.gameObject.SetActive(active);
        }

        static void SetAnchors(RectTransform rect, Vector2 anchorMin, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMin + size;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static void EnsureInputSystemUiModule()
        {
            var eventSystems = FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            EventSystem eventSystem = null;
            foreach (var candidate in eventSystems)
            {
                if (eventSystem == null)
                {
                    eventSystem = candidate;
                    eventSystem.gameObject.SetActive(true);
                    continue;
                }

                Destroy(candidate.gameObject);
            }

            if (eventSystem == null)
            {
                var eventSystemObject = new GameObject("EventSystem");
                eventSystem = eventSystemObject.AddComponent<EventSystem>();
            }

            var inputSystemModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (inputSystemModule == null)
                inputSystemModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();

            if (inputSystemModule.actionsAsset == null)
                inputSystemModule.AssignDefaultActions();
            inputSystemModule.enabled = true;

            var standaloneModule = eventSystem.GetComponent<StandaloneInputModule>();
            if (standaloneModule != null)
                Destroy(standaloneModule);
        }

        void AutoWireMissingReferences()
        {
            if (anatomyManager == null)
                anatomyManager = FindAnyObjectByType<AnatomyManager>();
            if (transparencyController == null)
                transparencyController = FindAnyObjectByType<TransparencyController>();
            if (modelInteractionController == null)
                modelInteractionController = FindAnyObjectByType<ModelInteractionController>();
            if (sessionResetController == null)
                sessionResetController = FindAnyObjectByType<ARSessionResetController>();
            if (placementController == null)
                placementController = FindAnyObjectByType<ARPlacementController>();
            if (modelSwitcher == null)
                modelSwitcher = FindAnyObjectByType<LiverModelSwitcher>();
            if (backgroundController == null)
                backgroundController = FindAnyObjectByType<ARBackgroundController>();
            if (backgroundController == null)
                backgroundController = gameObject.AddComponent<ARBackgroundController>();
        }
    }
}

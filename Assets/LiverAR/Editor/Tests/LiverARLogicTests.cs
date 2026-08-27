using NUnit.Framework;
using UnityEngine;
using LiverAR.Runtime;

namespace LiverAR.Tests.EditMode
{
    public sealed class LiverARLogicTests
    {
        [Test]
        public void ScaleLimitsClampUniformScale()
        {
            Assert.That(ModelInteractionController.ClampScale(0.01f, 0.05f, 2f), Is.EqualTo(0.05f));
            Assert.That(ModelInteractionController.ClampScale(3f, 0.05f, 2f), Is.EqualTo(2f));
            Assert.That(ModelInteractionController.ClampScale(0.75f, 0.05f, 2f), Is.EqualTo(0.75f));
        }

        [Test]
        public void AnatomyPartVisibilityControlsRenderersAndColliders()
        {
            var root = new GameObject("segment");
            var renderer = root.AddComponent<MeshRenderer>();
            var collider = root.AddComponent<BoxCollider>();
            var part = root.AddComponent<AnatomyPart>();
            part.Configure("segment-i", "Segment I", AnatomyCategory.LiverSegment, Color.red, new[] { renderer });

            part.SetVisible(false);

            Assert.That(renderer.enabled, Is.False);
            Assert.That(collider.enabled, Is.False);
            Assert.That(part.IsVisible, Is.False);

            Object.DestroyImmediate(root);
        }

        [Test]
        public void TransparencyValuesAreClamped()
        {
            Assert.That(TransparencyController.ClampOpacity(-0.5f), Is.EqualTo(0f));
            Assert.That(TransparencyController.ClampOpacity(1.5f), Is.EqualTo(1f));
            Assert.That(TransparencyController.ClampOpacity(0.35f), Is.EqualTo(0.35f));
        }

        [Test]
        public void AnatomyManagerSelectionReplacesPreviousSelection()
        {
            var managerObject = new GameObject("manager");
            var manager = managerObject.AddComponent<AnatomyManager>();
            var a = CreatePart("segment-i", "Segment I");
            var b = CreatePart("portal-vein", "Portal Vein");

            manager.Register(a);
            manager.Register(b);
            manager.Select(a);
            manager.Select(b);

            Assert.That(a.IsSelected, Is.False);
            Assert.That(b.IsSelected, Is.True);
            Assert.That(manager.SelectedPart, Is.EqualTo(b));

            Object.DestroyImmediate(a.gameObject);
            Object.DestroyImmediate(b.gameObject);
            Object.DestroyImmediate(managerObject);
        }

        [Test]
        public void SurfaceStatusMessagesStayShortAndMatchPlacementFlow()
        {
            Assert.That(ARSurfaceStatusMessage.GetMessage(ARSurfaceState.Initialising), Is.EqualTo("Starting AR..."));
            Assert.That(ARSurfaceStatusMessage.GetMessage(ARSurfaceState.Searching), Is.EqualTo("Move your phone slowly to detect a flat surface."));
            Assert.That(ARSurfaceStatusMessage.GetMessage(ARSurfaceState.Ready), Is.EqualTo("Surface detected. Tap Place Liver."));
            Assert.That(ARSurfaceStatusMessage.GetMessage(ARSurfaceState.VirtualMode), Is.EqualTo("Virtual surface ready. Tap Place Liver."));
            Assert.That(ARSurfaceStatusMessage.GetMessage(ARSurfaceState.VirtualEnvironment), Is.EqualTo("Virtual environment active."));
            Assert.That(ARSurfaceStatusMessage.GetMessage(ARSurfaceState.Placed), Is.EqualTo(string.Empty));
        }

        [Test]
        public void PlacementVisibilityRequiresEnabledRendererInCameraView()
        {
            var cameraObject = new GameObject("camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = Vector3.zero;
            camera.transform.rotation = Quaternion.identity;

            var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            model.transform.position = new Vector3(0f, 0f, 2f);
            model.transform.localScale = Vector3.one * 0.2f;

            var visible = ARPlacementController.TryValidateVisibleModel(model, camera, out var reason);

            Assert.That(visible, Is.True, reason);

            Object.DestroyImmediate(model);
            Object.DestroyImmediate(cameraObject);
        }

        [Test]
        public void PlacementVisibilityRejectsModelBehindCamera()
        {
            var cameraObject = new GameObject("camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = Vector3.zero;
            camera.transform.rotation = Quaternion.identity;

            var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            model.transform.position = new Vector3(0f, 0f, -2f);

            var visible = ARPlacementController.TryValidateVisibleModel(model, camera, out var reason);

            Assert.That(visible, Is.False);
            Assert.That(reason, Does.Contain("front"));

            Object.DestroyImmediate(model);
            Object.DestroyImmediate(cameraObject);
        }

        [Test]
        public void AnatomyManagerCanSwitchBetweenWholeLiverAndSegmentViews()
        {
            var managerObject = new GameObject("manager");
            var manager = managerObject.AddComponent<AnatomyManager>();
            var whole = CreatePart("whole-liver", "Whole Liver", AnatomyCategory.WholeLiver);
            var segment = CreatePart("segment-i", "Segment 1", AnatomyCategory.LiverSegment);

            manager.SetConfiguredParts(new[] { whole, segment });
            manager.ShowWholeLiverOverview();

            Assert.That(whole.IsVisible, Is.True);
            Assert.That(segment.IsVisible, Is.False);

            manager.ShowLiverSegments();

            Assert.That(whole.IsVisible, Is.False);
            Assert.That(segment.IsVisible, Is.True);

            Object.DestroyImmediate(whole.gameObject);
            Object.DestroyImmediate(segment.gameObject);
            Object.DestroyImmediate(managerObject);
        }

        [Test]
        public void LiverSettingsClampInteractionValues()
        {
            var settings = LiverARSettings.CreateDefault();

            settings.SetInteractionSensitivity(-1f);
            settings.SetRotationSpeed(10f);
            settings.SetScaleSensitivity(0.01f);

            Assert.That(settings.InteractionSensitivity, Is.EqualTo(0.2f));
            Assert.That(settings.RotationSpeed, Is.EqualTo(5f));
            Assert.That(settings.ScaleSensitivity, Is.EqualTo(0.1f));
        }

        [Test]
        public void ModelSwitcherPreservesTransformAndAvoidsDuplicates()
        {
            var root = new GameObject("placement-root").transform;
            root.SetPositionAndRotation(new Vector3(1f, 2f, 3f), Quaternion.Euler(0f, 45f, 0f));
            root.localScale = Vector3.one * 0.4f;

            var normal = new GameObject("normal-liver");
            var disease = new GameObject("disease-liver");
            normal.transform.SetParent(root, false);
            disease.transform.SetParent(root, false);

            var switcher = root.gameObject.AddComponent<LiverModelSwitcher>();
            switcher.ConfigureForTests(normal, disease);

            Assert.That(switcher.SwitchTo(LiverModelType.Normal), Is.True);
            Assert.That(switcher.SwitchTo(LiverModelType.Disease), Is.True);

            Assert.That(normal.activeSelf, Is.False);
            Assert.That(disease.activeSelf, Is.True);
            Assert.That(root.position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(root.rotation.eulerAngles.y, Is.EqualTo(45f).Within(0.01f));
            Assert.That(root.localScale, Is.EqualTo(Vector3.one * 0.4f));
            Assert.That(root.childCount, Is.EqualTo(2));

            Object.DestroyImmediate(root.gameObject);
        }

        [Test]
        public void BackgroundToggleKeepsRenderingCameraEnabledInVirtualWorkspace()
        {
            var cameraObject = new GameObject("camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.backgroundColor = Color.magenta;
            var controller = cameraObject.AddComponent<ARBackgroundController>();
            controller.ConfigureForTests(camera, null);

            var disabled = controller.SetCameraBackgroundEnabled(false);

            Assert.That(disabled, Is.True);
            Assert.That(camera.enabled, Is.True);
            Assert.That(camera.clearFlags, Is.EqualTo(CameraClearFlags.SolidColor));
            Assert.That(controller.IsCameraBackgroundEnabled, Is.False);

            Object.DestroyImmediate(cameraObject);
        }

        [Test]
        public void NavigationPanelsDoNotForceCloseDetailOverlays()
        {
            Assert.That(AnatomyUiPanelState.ShouldCloseDetailOverlayOnNavigationChange(true), Is.False);
            Assert.That(AnatomyUiPanelState.ShouldCloseDetailOverlayOnNavigationChange(false), Is.True);
        }

        [Test]
        public void GestureClassifierPrefersDragOverLongPressAfterMovementThreshold()
        {
            var state = AnatomyGestureClassifier.ClassifySingleTouch(
                elapsedSeconds: 1.2f,
                movementPixels: 18f,
                dragThresholdPixels: 12f,
                longPressSeconds: 1f,
                longPressTolerancePixels: 10f,
                startedOnPart: true);

            Assert.That(state, Is.EqualTo(AnatomyGestureState.Dragging));
        }

        [Test]
        public void GestureClassifierTriggersLongPressOnlyInsideTolerance()
        {
            var state = AnatomyGestureClassifier.ClassifySingleTouch(
                elapsedSeconds: 1.05f,
                movementPixels: 4f,
                dragThresholdPixels: 12f,
                longPressSeconds: 1f,
                longPressTolerancePixels: 10f,
                startedOnPart: true);

            Assert.That(state, Is.EqualTo(AnatomyGestureState.LongPressTriggered));
        }

        static AnatomyPart CreatePart(string id, string displayName, AnatomyCategory category = AnatomyCategory.LiverSegment)
        {
            var root = new GameObject(displayName);
            var renderer = root.AddComponent<MeshRenderer>();
            var part = root.AddComponent<AnatomyPart>();
            part.Configure(id, displayName, category, Color.white, new[] { renderer });
            return part;
        }
    }
}

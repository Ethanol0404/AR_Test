# Android Anatomy Interaction Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build Android-first anatomy interaction: Draw.io menu hierarchy, independent segment/vessel visibility panels, tap information, one-second transparency controls, full 3D model translation, and converted vessel geometry.

**Architecture:** Keep the current `LiverAR.Runtime` layer but split responsibilities into focused runtime controllers. Navigation panels are mutually exclusive; information and transparency are independent detail overlays that can stay active while the user interacts with segments or vessels.

**Tech Stack:** Unity 6000.5.0f1, C#, AR Foundation/ARCore 6.5.0, URP 17.5.0, Unity Input System 1.19.0, NUnit EditMode tests, offline VTK-to-OBJ conversion.

**Spec:** `docs/superpowers/specs/2026-08-27-android-anatomy-interaction-design.md`

## Global Constraints

- Android AR is the primary platform.
- Editor support is limited to compile checks, EditMode tests, and basic functional testing.
- Preserve working AR placement, camera-background switching, existing Couinaud segment models, materials, and Android compatibility.
- Reuse existing Couinaud GameObjects and imported OBJ meshes.
- Do not overwrite original materials or original VTK files.
- Do not show a vessel option when its corresponding asset is missing.
- Touches over UI must not manipulate or select the 3D model.
- Navigation panels may be mutually exclusive; information and opacity detail overlays may coexist with segment/vessel visibility panels.

---

### Task 1: Add Pure Interaction and UI State Tests

**Files:**
- Modify: `Assets/LiverAR/Editor/Tests/LiverARLogicTests.cs`
- Create: `Assets/LiverAR/Scripts/AnatomyGestureState.cs`
- Create: `Assets/LiverAR/Scripts/AnatomyGestureClassifier.cs`
- Create: `Assets/LiverAR/Scripts/AnatomyUiPanelState.cs`

**Interfaces:**
- Produces: `enum AnatomyGestureState`
- Produces: `static class AnatomyGestureClassifier`
- Produces: `static class AnatomyUiPanelState` with `ShouldCloseDetailOverlayOnNavigationChange(bool isDetailOverlay)`

- [ ] **Step 1: Write failing tests**

Add tests proving:

```csharp
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
```

- [ ] **Step 2: Run tests to verify failure**

Run: Unity EditMode tests for `LiverARLogicTests`.

Expected: compile fails because the new types do not exist.

- [ ] **Step 3: Implement minimal state helpers**

Implement:

```csharp
namespace LiverAR.Runtime
{
    public enum AnatomyGestureState
    {
        Idle,
        PossibleTap,
        LongPressPending,
        Dragging,
        PinchingOrRotating,
        LongPressTriggered
    }
}
```

```csharp
namespace LiverAR.Runtime
{
    public static class AnatomyGestureClassifier
    {
        public static AnatomyGestureState ClassifySingleTouch(
            float elapsedSeconds,
            float movementPixels,
            float dragThresholdPixels,
            float longPressSeconds,
            float longPressTolerancePixels,
            bool startedOnPart)
        {
            if (movementPixels >= dragThresholdPixels)
                return AnatomyGestureState.Dragging;

            if (startedOnPart && elapsedSeconds >= longPressSeconds && movementPixels <= longPressTolerancePixels)
                return AnatomyGestureState.LongPressTriggered;

            return startedOnPart ? AnatomyGestureState.LongPressPending : AnatomyGestureState.PossibleTap;
        }
    }
}
```

```csharp
namespace LiverAR.Runtime
{
    public static class AnatomyUiPanelState
    {
        public static bool ShouldCloseDetailOverlayOnNavigationChange(bool isDetailOverlay)
        {
            return !isDetailOverlay;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify pass**

Run the same EditMode tests.

Expected: all existing tests and the three new tests pass.

### Task 2: Rebuild Runtime Menu Hierarchy

**Files:**
- Modify: `Assets/LiverAR/Scripts/ARUIController.cs`
- Modify: `Assets/LiverAR/Editor/LiverARProjectSetup.cs`
- Modify: `Assets/LiverAR/Editor/Tests/LiverARLogicTests.cs`

**Interfaces:**
- Consumes: `AnatomyUiPanelState.ShouldCloseDetailOverlayOnNavigationChange`
- Produces: `OpenSegmentationMenu()`, `OpenCouinaudSegmentsPanel()`, `OpenVesselsPanel()`, `OpenInformationPanelForSelection()`, `OpenTransparencyPanelForSelection()`

- [ ] **Step 1: Write failing tests**

Add a test for panel policy:

```csharp
[Test]
public void DetailPanelsCanCoexistWithNavigationPanels()
{
    Assert.That(AnatomyUiPanelState.ShouldCloseDetailOverlayOnNavigationChange(true), Is.False);
}
```

- [ ] **Step 2: Run tests to verify failure if method names are not wired**

Expected: missing methods on `ARUIController` are caught when compile reaches code that references them.

- [ ] **Step 3: Modify `ARUIController` fields**

Add serialized fields:

```csharp
[SerializeField] GameObject segmentationMenuPanel;
[SerializeField] GameObject couinaudSegmentsPanel;
[SerializeField] GameObject vesselPanel;
[SerializeField] GameObject transparencyPanel;
[SerializeField] Text transparencyTitleText;
[SerializeField] Slider selectedOpacitySlider;
readonly List<Toggle> vesselToggles = new List<Toggle>();
```

Keep `informationPanel` independent from navigation panel activation.

- [ ] **Step 4: Implement navigation activation**

Create:

```csharp
void SetNavigationPanel(GameObject activePanel)
{
    SetPanelActive(compactMenuPanel, activePanel);
    SetPanelActive(segmentationMenuPanel, activePanel);
    SetPanelActive(couinaudSegmentsPanel, activePanel);
    SetPanelActive(vesselPanel, activePanel);
    SetPanelActive(settingsPanel, activePanel);
}
```

Do not include `informationPanel` or `transparencyPanel` in this method.

- [ ] **Step 5: Replace main menu construction**

Build main menu buttons:

```text
Model
Segmentation
Settings
Reset Placement
```

Remove creation and binding of the old `Information Button`.

- [ ] **Step 6: Build segmentation submenu**

Build buttons:

```text
Couinaud Segments
Vessels
Back
```

`Back` returns to compact main menu.

- [ ] **Step 7: Split row builders**

Rename existing `RebuildSegmentToggles()` use to only target `couinaudSegmentsPanel`. Add `RebuildVesselToggles()` filtering `AnatomyCategory.Vessel`.

- [ ] **Step 8: Update project setup UI generator**

Mirror the runtime hierarchy in `LiverARProjectSetup.CreateCanvas()` so regenerated scenes get the same hierarchy.

- [ ] **Step 9: Run tests**

Run EditMode tests.

Expected: tests pass.

### Task 3: Implement Android-First Model Translation

**Files:**
- Modify: `Assets/LiverAR/Scripts/ModelInteractionController.cs`
- Modify: `Assets/LiverAR/Scripts/LiverARSettings.cs`
- Modify: `Assets/LiverAR/Editor/Tests/LiverARLogicTests.cs`

**Interfaces:**
- Produces: `TranslateCameraRelative(Vector2 deltaPixels, Camera camera)`
- Produces: `AdjustDepth(float deltaPixels, Camera camera)`
- Produces: serialized fields for min/max scale, min/max camera distance, vertical range, movement sensitivity, rotation sensitivity

- [ ] **Step 1: Write failing tests**

Add tests for clamps:

```csharp
[Test]
public void CameraDistanceClampKeepsModelInReach()
{
    Assert.That(ModelInteractionController.ClampDistance(0.1f, 0.35f, 3f), Is.EqualTo(0.35f));
    Assert.That(ModelInteractionController.ClampDistance(4f, 0.35f, 3f), Is.EqualTo(3f));
}

[Test]
public void VerticalClampKeepsModelInConfiguredRange()
{
    Assert.That(ModelInteractionController.ClampVertical(3f, -0.6f, 0.8f), Is.EqualTo(0.8f));
    Assert.That(ModelInteractionController.ClampVertical(-2f, -0.6f, 0.8f), Is.EqualTo(-0.6f));
}
```

- [ ] **Step 2: Run tests to verify failure**

Expected: new clamp methods do not exist.

- [ ] **Step 3: Add clamp methods and serialized fields**

Add:

```csharp
[SerializeField] float minCameraDistance = 0.35f;
[SerializeField] float maxCameraDistance = 3f;
[SerializeField] float minVerticalOffset = -0.6f;
[SerializeField] float maxVerticalOffset = 0.8f;
[SerializeField] float moveMetersPerPixel = 0.0015f;
[SerializeField] float depthMetersPerPixel = 0.002f;
```

Add static clamp helpers using `Mathf.Clamp`.

- [ ] **Step 4: Implement camera-relative movement**

Use camera right/up projected from `arCamera` or `Camera.main`. Apply deltas to `modelRoot.position`, then clamp height and distance from camera.

- [ ] **Step 5: Implement two-finger depth**

Use the vertical movement of the midpoint between touches to move along camera forward. Keep pinch scaling and twist rotation active from the same two-touch stream.

- [ ] **Step 6: Run tests**

Expected: EditMode tests pass.

### Task 4: Replace Conflicting Selection With Coordinated Anatomy Input

**Files:**
- Create: `Assets/LiverAR/Scripts/AnatomyInteractionController.cs`
- Modify: `Assets/LiverAR/Scripts/AnatomySelectionController.cs`
- Modify: `Assets/LiverAR/Scripts/ModelInteractionController.cs`
- Modify: `Assets/LiverAR/Editor/LiverARProjectSetup.cs`

**Interfaces:**
- Consumes: `AnatomyGestureClassifier`
- Consumes: `ModelInteractionController.TranslateCameraRelative`
- Consumes: `ModelInteractionController.AdjustDepth`
- Produces: `event Action<AnatomyPart, Vector2> LongPressedPart`

- [ ] **Step 1: Write failing integration-oriented EditMode tests where possible**

Test classifier behavior added in Task 1 is the pure coverage for this controller. Do not fake Unity touch internals in EditMode.

- [ ] **Step 2: Disable duplicate selection loop**

Either remove `AnatomySelectionController` from generated scenes or make it a compatibility wrapper disabled when `AnatomyInteractionController` exists.

- [ ] **Step 3: Implement `AnatomyInteractionController`**

On touch begin: ignore UI; raycast to part; store start position, start time, pointer id, and hit part.

On movement beyond threshold: cancel long press and translate root.

On touch end inside tap threshold before long press: select part and call `ARUIController.OpenInformationPanelForSelection()`.

On long press success: select part and call `ARUIController.OpenTransparencyPanelForSelection()`.

On second touch: cancel single-touch state and let multi-touch transform run.

- [ ] **Step 4: Wire setup**

`LiverARProjectSetup.GenerateInitialArScene()` adds and serializes `AnatomyInteractionController` instead of active standalone selection.

- [ ] **Step 5: Run tests and compile**

Expected: compile succeeds; EditMode tests pass.

### Task 5: Implement Detail Information and Transparency Panels

**Files:**
- Modify: `Assets/LiverAR/Scripts/ARUIController.cs`
- Modify: `Assets/LiverAR/Scripts/TransparencyController.cs`
- Modify: `Assets/LiverAR/Scripts/AnatomyPart.cs`
- Modify: `Assets/LiverAR/Editor/Tests/LiverARLogicTests.cs`

**Interfaces:**
- Produces: `TransparencyController.ResetSelectedOpacity()`
- Produces: `AnatomyPart.ResetOpacity()`
- Produces: `ARUIController.OpenInformationPanelForSelection()`
- Produces: `ARUIController.OpenTransparencyPanelForSelection()`

- [ ] **Step 1: Write failing tests**

Add:

```csharp
[Test]
public void ResetOpacityRestoresSelectedPartToOpaque()
{
    var part = CreatePart("segment-1", "Segment 1");
    part.SetOpacity(0.35f);
    part.ResetOpacity();
    Assert.That(part.Opacity, Is.EqualTo(1f));
    Object.DestroyImmediate(part.gameObject);
}
```

- [ ] **Step 2: Run tests to verify failure**

Expected: `ResetOpacity` does not exist.

- [ ] **Step 3: Implement reset opacity**

Add `ResetOpacity()` to `AnatomyPart`, then use it in `TransparencyController.ResetSelectedOpacity()`.

- [ ] **Step 4: Add independent transparency panel**

Panel content:

```text
<Selected structure name>
Transparency
[slider]
Reset
Close
```

Bind slider with `SetValueWithoutNotify(part.Opacity)` on selection changes.

- [ ] **Step 5: Make tap information open automatically**

`OpenInformationPanelForSelection()` activates `informationPanel` but does not close navigation panels.

- [ ] **Step 6: Run tests**

Expected: tests pass.

### Task 6: Convert and Import Vessel VTK

**Files:**
- Create: `Assets/LiverAR/Models/ConvertedVessels/blood-vessel.obj`
- Create: `Assets/LiverAR/Models/ConvertedVessels/README.md`
- Modify: `Assets/LiverAR/Editor/LiverARProjectSetup.cs`
- Modify: `Assets/LiverAR/Configs/LiverAnatomyConfig.asset`
- Modify: `Assets/LiverAR/Prefabs/LiverAnatomyPrototype.prefab`

**Interfaces:**
- Produces: `AnatomyPart` with `structureId = "blood-vessel"`, `displayName = "Blood Vessel"`, `category = AnatomyCategory.Vessel`

- [ ] **Step 1: Classify VTK files**

Record:

```text
blood vessel.vtk: legacy binary VTK POLYDATA, POINTS 28778, POLYGONS 57296, POINT_DATA NORMALS
neoplasm.vtk: legacy binary VTK POLYDATA, POINTS 658, POLYGONS 1312, POINT_DATA NORMALS
```

- [ ] **Step 2: Convert vessel surface to OBJ**

Use a local converter that reads legacy binary big-endian float/int VTK POLYDATA and writes OBJ vertices, normals, and triangular faces.

- [ ] **Step 3: Keep original files unchanged**

Copy no source VTK into place unless needed for traceability; if copied, place it under `SourceFrom3DSlicer` without editing.

- [ ] **Step 4: Add vessel seed**

Add to `LiverARProjectSetup.Seeds`:

```csharp
new StructureSeed("blood-vessel", "Blood Vessel", AnatomyCategory.Vessel, new Color(0.10f, 0.55f, 0.95f), Vector3.zero),
```

Map `"blood-vessel"` to `Assets/LiverAR/Models/ConvertedVessels/blood-vessel.obj`.

- [ ] **Step 5: Regenerate or patch prefab**

Ensure the vessel is a child of `LiverAnatomyPrototype` and has `AnatomyPart`, renderers, material, and collider.

- [ ] **Step 6: Run Unity import/compile**

Expected: converted OBJ appears as a mesh asset and the prefab includes one vessel part.

### Task 7: Final Verification and Android Handoff

**Files:**
- Modify: final report only if requested

**Interfaces:**
- Consumes: all tasks

- [ ] **Step 1: Run EditMode tests**

Expected: all LiverAR tests pass.

- [ ] **Step 2: Run Unity compile/import check**

Expected: no compiler errors in Unity logs.

- [ ] **Step 3: Manual Android test checklist**

Build/install on Android device and verify:

```text
Main menu has Model, Segmentation, Settings, Reset Placement
Segmentation has Couinaud Segments, Vessels, Back
Couinaud panel controls segments
Vessel panel controls vessel object
Tap part opens information
Long press part opens transparency
Drag moves root model
Pinch scales root model
Two-finger twist rotates root model
Two-finger vertical drag changes depth
UI touches do not move/select model
Reset Placement restores visible placement
```

- [ ] **Step 4: Report verification honestly**

Separate Editor results from Android-device results. Do not claim Android success unless physical-device testing was completed.

## Self-Review

Spec coverage: menu hierarchy, panel coexistence, Android-first gestures, information, transparency, vessel classification/import, and verification are covered.

Placeholder scan: no TBD/TODO placeholders remain.

Type consistency: planned method/type names are introduced before later tasks consume them.

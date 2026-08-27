# Android Anatomy Interaction Design

## Goal

Implement the Draw.io menu hierarchy, Android-first anatomy gestures, tap information, one-second transparency controls, and imported vessel geometry while preserving AR placement, camera-background switching, existing Couinaud OBJ meshes, materials, and Android compatibility.

## Current State

The Unity project uses Unity 6000.5.0f1, AR Foundation/ARCore 6.5.0, URP 17.5.0, and Input System 1.19.0. Runtime code lives in `Assets/LiverAR/Scripts`. The current `ARUIController` builds and binds a runtime menu with `Model`, `Segmentation`, `Information`, `Settings`, and `Reset Placement`. The current `ModelInteractionController` handles scale and a limited one-finger path that either snaps to an AR plane or rotates around Y. `AnatomySelectionController` separately treats touch end as a selection raycast, which can conflict with movement gestures.

Existing Couinaud assets are preserved as imported OBJ files in `Assets/LiverAR/Models/SourceFrom3DSlicer` and are assembled by `LiverARProjectSetup`. The supplied external VTK files are legacy binary `POLYDATA`: `blood vessel.vtk` has 28,778 points, 57,296 polygons, and point normals; `neoplasm.vtk` has 658 points, 1,312 polygons, and point normals.

## User Clarification

Only navigation panels should be mutually exclusive. The user must still be able to interact with segments/vessels while information and opacity panels are visible. Opening the Couinaud or Vessel visibility panel must not prevent tapping anatomical parts, updating information, long-pressing, or changing opacity, except where a touch begins over UI.

Android is the primary platform. Editor support is limited to compile checks, EditMode tests, and basic functional testing.

## UI Design

`ARUIController` will expose one active navigation panel at a time:

- Compact main menu: `Model`, `Segmentation`, `Settings`, `Reset Placement`.
- Segmentation submenu: `Couinaud Segments`, `Vessels`, `Back`.
- Couinaud visibility panel: one row per existing segment, color indicator, checked state, `Show All`, `Hide All`, `Close`.
- Vessel visibility panel: one row per imported vessel anatomy part, color indicator, checked state, `Show All`, `Hide All`, `Close`.

The information panel and transparency panel are detail overlays, not navigation panels. They can remain visible while the Couinaud or Vessel visibility panel is open. They close independently through their own Close buttons or by empty-space taps when no navigation panel is consuming the touch.

The separate main-menu Information button will be removed. Information opens from tap selection.

## Interaction Design

A coordinated input controller will replace the conflicting split between `AnatomySelectionController` and `ModelInteractionController` for user gestures. The state machine is:

- `Idle`
- `PossibleTap`
- `LongPressPending`
- `Dragging`
- `PinchingOrRotating`
- `LongPressTriggered`

Touch priority is:

1. UI touch
2. Multi-touch gesture
3. Drag movement
4. Long press
5. Tap

One-finger drag after a movement threshold translates the root placed liver model camera-relative: camera right for horizontal movement and camera up for vertical movement. Two-finger pinch scales the root. Two-finger twist rotates the root around the camera-facing model center. Two-finger vertical centroid movement adjusts depth along the camera forward axis. Movement clamps prevent the model from going behind the camera, too far away, too high/low, or outside reasonable scale bounds.

Tap without movement raycasts from the screen position to `AnatomyPart`, selects that exact part, highlights it through the existing selection mechanism, and opens or updates the information panel.

Long press starts only when the initial touch hits a selectable `AnatomyPart`, remains inside a configurable pixel tolerance, lasts at least 1.0 second, and is not interrupted by UI or a second finger. A successful long press selects that part and opens the transparency panel without also firing tap information.

## Anatomy Data

`AnatomyInfoDatabase` remains the source for structured display content. It will be extended only where needed to include display name/category-friendly fields while keeping content out of touch handlers. Missing verified content is labelled as placeholder content.

## Transparency Design

Transparency changes apply only to the selected `AnatomyPart`. Runtime material instances are cached per renderer/material slot. Original color, alpha, shader, render queue, and relevant URP transparency settings are captured before mutation. Slider changes reuse cached runtime materials and do not allocate each frame. Reset restores the selected part to its original opacity and material settings.

## Vessel Import Design

The original VTK files remain unchanged. Because the supplied vessel file is polygonal surface `POLYDATA`, it can be converted offline to OBJ without tubing. Conversion preserves points, polygon faces, normals when usable, relative scale, orientation, and LPS source notes. The converted vessel OBJ is stored under `Assets/LiverAR/Models/ConvertedVessels`. A vessel material is assigned from the existing `portal-vein-material` or a new medically readable material. The vessel object is added under the same placed liver root so it moves, rotates, and scales with the Couinaud segments.

`neoplasm.vtk` is also polygonal surface data, but the current requested menu is for vessels. It will be classified and documented; it will not be shown in the Vessels panel unless intentionally mapped as a lesion in a later task.

## Verification

Automated verification focuses on EditMode tests for pure logic: scale/depth clamps, gesture classification, navigation/detail overlay independence, vessel filtering by category, and opacity reset behavior. Unity compile is checked after code changes. Android device verification must be reported separately and cannot be claimed from Editor-only tests.
